using System.IO;
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
/// Test job dùng NHIỀU ẢNH: ảnh gốc + ảnh từ file (ảnh mẫu / ảnh chuẩn),
/// cùng các thuật toán cần ≥ 2 ảnh (Template matching, So sánh ảnh chuẩn).
/// </summary>
public class MultiImageTests : IDisposable
{
    // Thư mục tạm CÓ DẤU tiếng Việt — bắt lỗi Cv2.ImRead/ImWrite với đường dẫn Unicode.
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "MVATool Ảnh mẫu " + Guid.NewGuid().ToString("N"));

    public MultiImageTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private static T Param<T>(PipelineNode node, string name) where T : ProcessorParameter =>
        (T)node.Inputs.Single(p => p.Name == name);

    private static ToolOutput<T> Output<T>(PipelineNode node, string name) =>
        (ToolOutput<T>)node.Outputs.Single(o => o.Name == name);

    /// <summary>Ảnh nền xám 120×80 có "chi tiết" (chữ thập đen-trắng 12×12) tại các vị trí cho trước.</summary>
    private static Mat Scene(params Point[] parts)
    {
        var scene = new Mat(80, 120, MatType.CV_8UC3, Scalar.All(128));
        foreach (var p in parts)
        {
            scene[new Rect(p.X, p.Y, 12, 12)].SetTo(Scalar.All(0));
            scene[new Rect(p.X + 5, p.Y, 2, 12)].SetTo(Scalar.All(255));
            scene[new Rect(p.X, p.Y + 5, 12, 2)].SetTo(Scalar.All(255));
        }
        return scene;
    }

    private string SaveTemplate(string name = "chi-tiết.png")
    {
        using var scene = Scene(new Point(10, 10));
        using var template = new Mat(scene, new Rect(8, 8, 16, 16)).Clone(); // chi tiết + viền nền 2px
        var path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, template.ImEncode(".png"));
        return path;
    }

    private static (PipelineNode File, PipelineNode Match) TemplateJob(string templatePath, int maxMatches)
    {
        var file = new PipelineNode(new ImageFileTool());
        Param<FileParameter>(file, "File ảnh").Value = templatePath;

        var match = new PipelineNode(new TemplateMatchProcessor());
        Param<InputPort<Mat>>(match, "Ảnh mẫu").Source = Output<Mat>(file, "Ảnh");
        Param<NumberParameter>(match, "Số khớp tối đa").Value = maxMatches;
        return (file, match);
    }

    [Fact]
    public void TemplateMatching_TimDungViTriChiTiet()
    {
        var (file, match) = TemplateJob(SaveTemplate(), maxMatches: 1);
        using var scene = Scene(new Point(70, 50));

        using var result = new PipelineRunner().Run(scene.Clone(), new[] { file, match }).Result;

        Assert.True(Output<bool>(match, "Tìm thấy").Value);
        Assert.True(Output<double>(match, "Điểm cao nhất").Value > 0.95);
        // Tâm chi tiết = (70 + 6, 50 + 6)
        Assert.Equal(76, Output<double>(match, "X").Value, precision: 0);
        Assert.Equal(56, Output<double>(match, "Y").Value, precision: 0);
        Assert.Equal(scene.Size(), result.Size()); // ảnh ra = ẢNH GỐC có vẽ khung, không phải ảnh mẫu
    }

    [Theory]
    [InlineData("CCOEFF chuẩn hóa")]
    [InlineData("SQDIFF chuẩn hóa")]
    public void TemplateMatching_DemDuNhieuChiTiet_KhongBatTrung(string method)
    {
        var (file, match) = TemplateJob(SaveTemplate(), maxMatches: 10);
        Param<ChoiceParameter>(match, "Phương pháp").Value = method;
        using var scene = Scene(new Point(10, 10), new Point(60, 20), new Point(90, 55));

        using var _ = new PipelineRunner().Run(scene.Clone(), new[] { file, match }).Result;

        Assert.Equal(3, Output<double>(match, "Số khớp").Value);
    }

    [Fact]
    public void TemplateMatching_ChuaNoiAnhMau_BiBoQuaKemCanhBao_AnhGocDiTiep()
    {
        using var scene = Scene();
        var match = new PipelineNode(new TemplateMatchProcessor());
        var gray = new PipelineNode(new GrayscaleProcessor());

        var run = new PipelineRunner().Run(scene.Clone(), new[] { match, gray });
        using var result = run.Result;

        Assert.Contains("Ảnh mẫu", run.Warnings[0]);
        Assert.False(Output<double>(match, "Số khớp").HasValue);
        Assert.Equal(1, result.Channels()); // Grayscale vẫn chạy trên ảnh gốc
        Assert.Equal(new Size(120, 80), result.Size());
    }

    [Fact]
    public void TemplateMatching_AnhMauLonHonAnhVao_VanLaLoiThat()
    {
        // Cấu hình SAI (không phải "chưa xong") → vẫn báo lỗi, không lặng lẽ bỏ qua.
        var (file, match) = TemplateJob(SaveTemplate(), maxMatches: 1);
        using var tiny = new Mat(10, 10, MatType.CV_8UC3, Scalar.All(0));

        var ex = Assert.Throws<InvalidOperationException>(() => new PipelineRunner().Run(tiny.Clone(), new[] { file, match }));
        Assert.Contains("lớn hơn", ex.Message);
    }

    [Fact]
    public void AnhTuFile_KhongChenVaoChuoiAnh_ToolSauVanXuLyAnhGoc()
    {
        // [Ảnh từ file] → [Grayscale]: Grayscale không nối dây phải nhận ẢNH GỐC (120×80),
        // không phải ảnh mẫu (16×16) chỉ vì ảnh mẫu đứng ngay trước nó.
        var file = new PipelineNode(new ImageFileTool());
        Param<FileParameter>(file, "File ảnh").Value = SaveTemplate();
        var gray = new PipelineNode(new GrayscaleProcessor());
        using var scene = Scene();

        using var result = new PipelineRunner().Run(scene.Clone(), new[] { file, gray }).Result;

        Assert.Equal(new Size(120, 80), result.Size());
        Assert.Equal(1, result.Channels());
    }

    [Fact]
    public void AnhTuFile_MoiThem_ChuaChonFile_KhongLamHongJob()
    {
        // Mở ảnh gốc → thêm "Ảnh từ file" (chưa chọn file) → job vẫn chạy, ảnh gốc đi tiếp bình thường.
        var file = new PipelineNode(new ImageFileTool());
        var gray = new PipelineNode(new GrayscaleProcessor());
        using var scene = Scene();

        var run = new PipelineRunner().Run(scene.Clone(), new[] { file, gray });
        using var result = run.Result;

        Assert.Equal(new Size(120, 80), result.Size());
        Assert.Contains("Chưa chọn file", run.Warnings[0]);
        Assert.Null(run.Warnings[1]);

        // Đã nối Template matching vào mà chưa chọn file → cả hai bị bỏ qua kèm cảnh báo, KHÔNG ném lỗi.
        var match = new PipelineNode(new TemplateMatchProcessor());
        Param<InputPort<Mat>>(match, "Ảnh mẫu").Source = Output<Mat>(file, "Ảnh");
        var run2 = new PipelineRunner().Run(scene.Clone(), new[] { file, match });
        using var result2 = run2.Result;

        Assert.Contains("chưa chọn file", run2.Warnings[1]);
        Assert.Equal(scene.Size(), result2.Size());
    }

    [Fact]
    public async Task ViewModel_LapJobDoDang_KhongBaoLoi_CanhBaoTrenTool_ChonFileXongTuHet()
    {
        // Đúng thao tác người dùng: mở ảnh → thêm Ảnh từ file → thêm Template matching → nối dây → chọn file.
        var picker = new FakeFilePickerService();
        var viewModel = new MainWindowViewModel(picker, new FakeImageService(), new ProcessorCatalog(),
            new SystemStatusViewModel(new FakeSystemInfoService())) { IsAutoRunEnabled = false };
        await viewModel.LoadImageAsync(@"C:\anh\mau.png"); // ảnh giả 16×12

        foreach (var name in new[] { "Ảnh từ file", "Template matching" })
        {
            viewModel.SelectedProcessorInfo = viewModel.AvailableProcessors.First(p => p.Name.Contains(name));
            viewModel.AddStepCommand.Execute(null);
        }
        var (fileStep, matchStep) = (viewModel.Pipeline[0], viewModel.Pipeline[1]);
        viewModel.ConnectCommand.Execute(new TerminalPair(
            fileStep.Outputs.Single(), matchStep.Inputs.Single(i => i.Parameter.Name == "Ảnh mẫu")));

        await viewModel.RunPipelineCommand.ExecuteAsync(null);

        Assert.DoesNotContain("Lỗi", viewModel.StatusMessage);
        Assert.Contains("⚠", viewModel.StatusMessage);
        Assert.Contains("Chưa chọn file", fileStep.WarningText);
        Assert.NotEqual("", matchStep.WarningText);

        // Chọn file mẫu (8×8 cắt từ ảnh giả) → hết cảnh báo, template matching chạy thật.
        var template = Path.Combine(_folder, "mẫu.png");
        using (var patch = new Mat(8, 8, MatType.CV_8UC3, new Scalar(30, 60, 90)))
            File.WriteAllBytes(template, patch.ImEncode(".png"));
        picker.PathToReturn = template;
        await viewModel.BrowseFileParameterCommand.ExecuteAsync(fileStep.Inputs[0].Parameter);
        await viewModel.RunPipelineCommand.ExecuteAsync(null);

        Assert.DoesNotContain("⚠", viewModel.StatusMessage);
        Assert.Equal("", fileStep.WarningText);
        Assert.Equal("", matchStep.WarningText);
        Assert.True(((ToolOutput<bool>)matchStep.Outputs.Single(o => o.Output.Name == "Tìm thấy").Output).Value);
    }

    [Fact]
    public void AnhTuFile_ChayNhieuLan_VanCoAnh_VaDocLaiKhiFileDoi()
    {
        // Mô phỏng chạy cả thư mục: job chạy nhiều lần, ảnh mẫu phải còn nguyên mỗi lần
        // (job Dispose ảnh ra sau mỗi lần chạy — tool phải đưa bản sao, không đưa cache).
        var path = SaveTemplate();
        var (file, match) = TemplateJob(path, maxMatches: 1);
        var runner = new PipelineRunner();

        for (var i = 0; i < 3; i++)
        {
            using var scene = Scene(new Point(20 + i * 10, 30));
            using var _ = runner.Run(scene.Clone(), new[] { file, match }).Result;
            Assert.Equal(26 + i * 10, Output<double>(match, "X").Value, precision: 0);
        }

        // Ghi đè file mẫu bằng hoa văn khác (nửa đen / nửa trắng) → lần chạy sau phải đọc lại, không còn khớp.
        using (var other = new Mat(16, 16, MatType.CV_8UC3, Scalar.All(0)))
        {
            other[new Rect(8, 0, 8, 16)].SetTo(Scalar.All(255));
            File.WriteAllBytes(path, other.ImEncode(".png"));
        }
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));

        using var scene2 = Scene(new Point(20, 30));
        using var __ = runner.Run(scene2.Clone(), new[] { file, match }).Result;
        Assert.False(Output<bool>(match, "Tìm thấy").Value);
    }

    [Fact]
    public void SoSanhAnhChuan_GiongHet_0PhanTram_CoLoi_RaVungKhac()
    {
        using var golden = Scene(new Point(30, 30));
        var path = Path.Combine(_folder, "chuẩn.png");
        File.WriteAllBytes(path, golden.ImEncode(".png"));

        var file = new PipelineNode(new ImageFileTool());
        Param<FileParameter>(file, "File ảnh").Value = path;
        var diff = new PipelineNode(new ImageDiffProcessor());
        Param<InputPort<Mat>>(diff, "Ảnh chuẩn").Source = Output<Mat>(file, "Ảnh");
        var runner = new PipelineRunner();

        using (var good = runner.Run(golden.Clone(), new[] { file, diff }).Result)
            Assert.Equal(0, Output<double>(diff, "Tỉ lệ khác (%)").Value);

        using var defect = golden.Clone();
        defect[new Rect(80, 20, 10, 10)].SetTo(Scalar.All(255)); // vết lỗi 10×10
        using var mask = runner.Run(defect, new[] { file, diff }).Result;

        Assert.True(Output<double>(diff, "Số pixel khác").Value > 50);
        Assert.Equal(255, mask.At<byte>(25, 85)); // tâm vết lỗi trắng trên mặt nạ
        Assert.Equal(0, mask.At<byte>(5, 5));
    }

    [Fact]
    public void FileJob_LuuDuongDanTuongDoi_MoOThuMucKhacVanDung()
    {
        var templatePath = SaveTemplate();
        var (file, match) = TemplateJob(templatePath, maxMatches: 1);
        var runner = new PipelineRunner();

        var json = JobSerializer.Save(new[] { file, match }, runner.Source, _folder);
        Assert.Contains("\"chi-tiết.png\"", json); // tương đối, không lộ đường dẫn máy này

        // "Chép" job + ảnh mẫu sang thư mục khác rồi mở lại.
        var moved = Path.Combine(_folder, "may-khac");
        Directory.CreateDirectory(moved);
        File.Copy(templatePath, Path.Combine(moved, "chi-tiết.png"));
        var loaded = JobSerializer.Load(json, new ProcessorCatalog(), new PipelineRunner().Source, moved);

        Assert.Empty(loaded.Warnings);
        Assert.Equal(Path.Combine(moved, "chi-tiết.png"), Param<FileParameter>(loaded.Nodes[0], "File ảnh").Value);
        Assert.Same(Output<Mat>(loaded.Nodes[0], "Ảnh"), Param<InputPort<Mat>>(loaded.Nodes[1], "Ảnh mẫu").Source);
    }

    [Fact]
    public async Task LayAnhGocHienTaiLamMau_LuuFile_VaGanVaoThamSo()
    {
        var picker = new FakeFilePickerService();
        var images = new FakeImageService();
        var viewModel = new MainWindowViewModel(picker, images, new ProcessorCatalog(),
            new SystemStatusViewModel(new FakeSystemInfoService())) { IsAutoRunEnabled = false };
        var parameter = new FileParameter("File ảnh");

        // Chưa mở ảnh → không lưu gì
        await viewModel.CaptureFileParameterCommand.ExecuteAsync(parameter);
        Assert.Empty(images.SavedPaths);

        await viewModel.LoadImageAsync(@"C:\anh\mau.png");
        picker.PathToReturn = @"C:\job\mau.png";
        await viewModel.CaptureFileParameterCommand.ExecuteAsync(parameter);

        Assert.Equal(new[] { @"C:\job\mau.png" }, images.SavedPaths);
        Assert.Equal(@"C:\job\mau.png", parameter.Value);
    }
}
