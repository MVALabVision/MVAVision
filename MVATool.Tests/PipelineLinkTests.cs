using MVATool.Core;
using MVATool.Core.Parameters;
using MVATool.Core.Pipeline;
using MVATool.Core.Processors;
using MVATool.Core.Tools;
using MVATool.ViewModels;
using OpenCvSharp;
using Xunit;

namespace MVATool.Tests;

/// <summary>
/// Test cơ chế NỐI DÂY terminal ra → terminal vào giữa các tool trong job.
/// </summary>
public class PipelineLinkTests
{
    /// <summary>Ảnh đen 40×40 có <paramref name="count"/> ô vuông trắng tách rời.</summary>
    private static Mat ImageWithSquares(int count)
    {
        var image = new Mat(40, 40, MatType.CV_8UC3, Scalar.All(0));
        for (var i = 0; i < count; i++)
            image[new Rect(2 + i * 9, 10, 6, 6)].SetTo(Scalar.All(255));
        return image;
    }

    private static T Param<T>(PipelineNode node, string name) where T : ProcessorParameter =>
        (T)node.Inputs.Single(p => p.Name == name);

    private static ToolOutput<T> Output<T>(PipelineNode node, string name) =>
        (ToolOutput<T>)node.Outputs.Single(o => o.Name == name);

    [Theory]
    [InlineData(3, "OK")]
    [InlineData(2, "NG")]
    public void TimContour_NoiSoLuongVaoSoSanh_RaOkNg(int squares, string expected)
    {
        using var image = ImageWithSquares(squares);

        var find = new PipelineNode(new FindContoursProcessor());
        Param<NumberParameter>(find, "Diện tích min").Value = 0;
        var compare = new PipelineNode(new CompareTool());

        // Dây nối: Tìm contour › Số contour  →  So sánh › Giá trị
        Param<NumberParameter>(compare, "Giá trị").Source = Output<double>(find, "Số contour");
        Param<ChoiceParameter>(compare, "Phép so sánh").Value = "=";
        Param<NumberParameter>(compare, "Ngưỡng").Value = 3;

        using var result = new PipelineRunner().Run(image.Clone(), new[] { find, compare }).Result;

        Assert.Equal(squares, Output<double>(find, "Số contour").Value);
        Assert.Equal(expected, Output<string>(compare, "Kết quả").Value);
        Assert.Equal(expected == "OK", Output<bool>(compare, "Đạt").Value);
        // So sánh không ra ảnh → ảnh kết quả là ảnh của tool ảnh cuối cùng (Tìm contour)
        Assert.Equal(3, result.Channels());
    }

    [Fact]
    public void NoiBool_DatVaoHienThiOkNg()
    {
        using var image = ImageWithSquares(1);
        var compare = new PipelineNode(new CompareTool()); // Giá trị 0 ≥ Ngưỡng 1 → Sai → NG
        var show = new PipelineNode(new ShowResultProcessor());
        Param<BoolParameter>(show, "Đạt").Source = Output<bool>(compare, "Đạt");

        using var _ = new PipelineRunner().Run(image.Clone(), new[] { compare, show }).Result;

        Assert.False(Param<BoolParameter>(show, "Đạt").Value);
    }

    [Fact]
    public void NoiContours_VaoVeContour()
    {
        using var image = ImageWithSquares(2);
        var runner = new PipelineRunner();
        var threshold = new PipelineNode(new ThresholdProcessor());
        var find = new PipelineNode(new FindContoursProcessor());
        Param<NumberParameter>(find, "Diện tích min").Value = 0;
        var draw = new PipelineNode(new DrawContoursProcessor());
        Param<InputPort<Mat>>(draw, "Ảnh vào").Source = runner.Source;
        Param<InputPort<Point[][]>>(draw, "Contours").Source = Output<Point[][]>(find, "Contours");

        using var result = runner.Run(image.Clone(), new[] { threshold, find, draw }).Result;

        // Viền ô vuông đầu tiên được vẽ màu đỏ (BGR) lên ảnh gốc
        Assert.Equal(new Vec3b(0, 0, 255), result.At<Vec3b>(10, 2));
    }

    [Fact]
    public void NoiNguongThreshold_VaoDoSangTB()
    {
        // Ảnh nửa trái 50, nửa phải 150 → độ sáng TB = 100
        using var image = new Mat(4, 4, MatType.CV_8UC1, Scalar.All(50));
        image[new Rect(2, 0, 2, 4)].SetTo(Scalar.All(150));

        var measure = new PipelineNode(new MeanBrightnessProcessor());
        var threshold = new PipelineNode(new ThresholdProcessor());
        Param<NumberParameter>(threshold, "Ngưỡng").Value = 200; // nhập tay thì ảnh đen hết
        Param<NumberParameter>(threshold, "Ngưỡng").Source = Output<double>(measure, "Độ sáng TB");

        using var result = new PipelineRunner().Run(image.Clone(), new[] { measure, threshold }).Result;

        Assert.Equal(8, Cv2.CountNonZero(result)); // nửa phải (150 > 100) thành trắng
    }

    [Fact]
    public void ApMatNa_HaiAnhVao_GiuPhanTrongMatNa()
    {
        using var image = new Mat(4, 4, MatType.CV_8UC3, new Scalar(30, 60, 90));
        image[new Rect(0, 0, 2, 4)].SetTo(Scalar.All(0)); // nửa trái tối

        var runner = new PipelineRunner();
        var threshold = new PipelineNode(new ThresholdProcessor());
        Param<NumberParameter>(threshold, "Ngưỡng").Value = 20;
        var mask = new PipelineNode(new MaskProcessor());
        Param<InputPort<Mat>>(mask, "Ảnh vào").Source = runner.Source;
        Param<InputPort<Mat>>(mask, "Mặt nạ").Source = Output<Mat>(threshold, "Ảnh ra");

        using var result = runner.Run(image.Clone(), new[] { threshold, mask }).Result;

        Assert.Equal(new Vec3b(30, 60, 90), result.At<Vec3b>(0, 3));
        Assert.Equal(new Vec3b(0, 0, 0), result.At<Vec3b>(0, 0));
    }

    [Fact]
    public void NoiToiToolBiTat_BaoLoiRoRang()
    {
        var find = new PipelineNode(new FindContoursProcessor()) { IsEnabled = false };
        var compare = new PipelineNode(new CompareTool());
        Param<NumberParameter>(compare, "Giá trị").Source = Output<double>(find, "Số contour");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new PipelineRunner().Run(new Mat(4, 4, MatType.CV_8UC1), new[] { find, compare }));
        Assert.Contains("#2", ex.Message);
    }

    [Fact]
    public void NoiSaiKieu_BiChanNgay()
    {
        var compare = new PipelineNode(new CompareTool());
        Assert.Throws<ArgumentException>(() =>
            Param<NumberParameter>(compare, "Giá trị").Source = new ToolOutput<string>("Kết quả"));
    }

    // ===== ViewModel: kéo-thả nối dây =====

    private static MainWindowViewModel CreateViewModel(params string[] tools)
    {
        var viewModel = new MainWindowViewModel(new FakeFilePickerService(), new FakeImageService(),
            new ProcessorCatalog(), new SystemStatusViewModel(new FakeSystemInfoService()))
        {
            IsAutoRunEnabled = false,
        };
        foreach (var tool in tools)
        {
            viewModel.SelectedProcessorInfo = viewModel.AvailableProcessors.First(p => p.Name.Contains(tool));
            viewModel.AddStepCommand.Execute(null);
        }
        return viewModel;
    }

    private static OutputTerminalViewModel Out(PipelineStepViewModel step, string name) =>
        step.Outputs.Single(o => o.Output.Name == name);

    private static InputTerminalViewModel In(PipelineStepViewModel step, string name) =>
        step.Inputs.Single(i => i.Parameter.Name == name);

    [Fact]
    public void KeoTha_NoiDay_ChieuNaoCungDuoc()
    {
        var viewModel = CreateViewModel("Tìm contour", "So sánh");
        var count = Out(viewModel.Pipeline[0], "Số contour");
        var value = In(viewModel.Pipeline[1], "Giá trị");

        // Kéo NGƯỢC: từ terminal vào thả lên terminal ra
        viewModel.ConnectCommand.Execute(new TerminalPair(value, count));

        Assert.Same(count.Output, value.Parameter.Source);
        Assert.Equal("← #1 Tìm contour › Số contour", value.HintText);

        viewModel.DisconnectCommand.Execute(value);
        Assert.False(value.Parameter.IsLinked);
    }

    [Fact]
    public void KeoTha_SaiKieu_HoacNoiNguocLen_BiTuChoi()
    {
        var viewModel = CreateViewModel("Tìm contour", "So sánh");
        var find = viewModel.Pipeline[0];
        var compare = viewModel.Pipeline[1];

        // Contours (Point[][]) → Giá trị (số): khác kiểu
        viewModel.ConnectCommand.Execute(new TerminalPair(Out(find, "Contours"), In(compare, "Giá trị")));
        Assert.False(In(compare, "Giá trị").Parameter.IsLinked);
        Assert.Contains("Khác kiểu", viewModel.StatusMessage);

        // Tool dưới → tool trên: job chạy từ trên xuống nên không cho
        viewModel.MoveStepUpCommand.Execute(compare);
        viewModel.ConnectCommand.Execute(new TerminalPair(Out(find, "Số contour"), In(compare, "Giá trị")));
        Assert.False(In(compare, "Giá trị").Parameter.IsLinked);
    }

    [Fact]
    public void DoiChoTool_TuCatDayToiToolKhongConDungTruoc()
    {
        var viewModel = CreateViewModel("Tìm contour", "So sánh");
        var compare = viewModel.Pipeline[1];
        viewModel.ConnectCommand.Execute(new TerminalPair(Out(viewModel.Pipeline[0], "Số contour"), In(compare, "Giá trị")));
        Assert.True(In(compare, "Giá trị").Parameter.IsLinked);

        viewModel.MoveStepUpCommand.Execute(compare);

        Assert.False(In(compare, "Giá trị").Parameter.IsLinked);
        Assert.Equal("#1 So sánh (OK/NG)", compare.Title);
    }

    [Fact]
    public async Task ChayJob_TuViewModel_HienKetQuaOkNg()
    {
        var viewModel = CreateViewModel("Tìm contour", "So sánh");
        await viewModel.LoadImageAsync(@"C:\anh\mau.png"); // ảnh giả: màu đồng nhất → 1 contour
        var compare = viewModel.Pipeline[1];
        viewModel.ConnectCommand.Execute(new TerminalPair(Out(viewModel.Pipeline[0], "Số contour"), In(compare, "Giá trị")));

        await viewModel.RunPipelineCommand.ExecuteAsync(null);

        Assert.Equal("OK", Out(compare, "Kết quả").Output.DisplayText); // 1 ≥ 1
    }
}
