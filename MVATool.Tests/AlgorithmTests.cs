using MVATool.Core;
using MVATool.Core.Parameters;
using MVATool.Core.Pipeline;
using MVATool.Core.Processors;
using OpenCvSharp;
using Xunit;

namespace MVATool.Tests;

/// <summary>Test các thuật toán xử lý ảnh / contour — chạy không cần UI.</summary>
public class AlgorithmTests
{
    private static T Param<T>(IImageProcessor processor, string name) where T : ProcessorParameter =>
        (T)processor.Parameters.Single(p => p.Name == name);

    private static ToolOutput<T> Output<T>(IImageProcessor processor, string name) =>
        (ToolOutput<T>)processor.Outputs.Single(o => o.Name == name);

    /// <summary>Ảnh đen có 1 hình đa giác trắng.</summary>
    private static Mat Shape(params Point[] polygon)
    {
        var image = new Mat(200, 200, MatType.CV_8UC1, Scalar.All(0));
        Cv2.FillPoly(image, new[] { polygon }, Scalar.All(255));
        return image;
    }

    [Fact]
    public void Morphology_Open_XoaDomNhieuNho()
    {
        using var image = new Mat(50, 50, MatType.CV_8UC1, Scalar.All(0));
        image[new Rect(10, 10, 20, 20)].SetTo(Scalar.All(255)); // vật thật
        image.Set<byte>(40, 40, 255);                             // nhiễu 1 pixel

        var morphology = new MorphologyProcessor(); // mặc định: Open, kernel 5
        using var result = morphology.Process(image);

        Assert.Equal(0, result.At<byte>(40, 40));
        Assert.Equal(255, result.At<byte>(20, 20));
    }

    [Theory]
    // CLAHE cố ý GIỚI HẠN mức khuếch đại (clip limit) nên giãn ít hơn cân bằng toàn cục.
    [InlineData("CLAHE", 35)]
    [InlineData("Toàn cục", 200)]
    public void CanBangHistogram_KeoGianDoTuongPhan(string method, double minRange)
    {
        // Gradient hẹp 100..131 (dải 31) → sau cân bằng phải trải rộng hơn
        using var image = new Mat(64, 64, MatType.CV_8UC1);
        for (var x = 0; x < 64; x++)
            image.ColRange(x, x + 1).SetTo(Scalar.All(100 + x / 2));

        var equalize = new HistogramEqualizeProcessor();
        Param<ChoiceParameter>(equalize, "Phương pháp").Value = method;
        using var result = equalize.Process(image);

        Cv2.MinMaxLoc(result, out double min, out double max);
        Assert.True(max - min > minRange, $"dải sau cân bằng chỉ {max - min}");
    }

    [Fact]
    public void CanBangHistogram_AnhMau_VanLaAnhMau()
    {
        using var image = new Mat(32, 32, MatType.CV_8UC3, new Scalar(40, 80, 120));
        using var result = new HistogramEqualizeProcessor().Process(image);
        Assert.Equal(3, result.Channels());
    }

    [Fact]
    public void KhuNhieu_GiamDoLechChuan()
    {
        using var image = new Mat(64, 64, MatType.CV_8UC1, Scalar.All(128));
        using var noise = new Mat(64, 64, MatType.CV_8UC1);
        Cv2.Randn(noise, Scalar.All(0), Scalar.All(20));
        Cv2.Add(image, noise, image);

        using var result = new DenoiseProcessor().Process(image);

        Cv2.MeanStdDev(image, out _, out var before);
        Cv2.MeanStdDev(result, out _, out var after);
        Assert.True(after.Val0 < before.Val0);
    }

    [Fact]
    public void LamSacNet_TangDoTuongPhanTaiBien()
    {
        using var image = new Mat(20, 40, MatType.CV_8UC1, Scalar.All(100));
        image[new Rect(20, 0, 20, 20)].SetTo(Scalar.All(150));

        using var result = new SharpenProcessor().Process(image);

        // Unsharp mask tạo "vầng": bên tối của biên tối hơn, bên sáng sáng hơn
        Assert.True(result.At<byte>(10, 19) < 100);
        Assert.True(result.At<byte>(10, 20) > 150);
    }

    [Fact]
    public void ThayDoiKichThuoc_50PhanTram_VaRaKichThuocMoi()
    {
        using var image = new Mat(100, 200, MatType.CV_8UC3, Scalar.All(50));
        var resize = new ResizeProcessor(); // mặc định 50%

        using var result = resize.Process(image);

        Assert.Equal(new Size(100, 50), result.Size());
        Assert.Equal(100, Output<double>(resize, "Rộng").Value);
        Assert.Equal(50, Output<double>(resize, "Cao").Value);
    }

    [Fact]
    public void XoayAnh_90Do_GiuTronAnh_DoiChieuRongCao()
    {
        using var image = new Mat(100, 200, MatType.CV_8UC1, Scalar.All(255));
        var rotate = new RotateProcessor();
        Param<NumberParameter>(rotate, "Góc").Value = 90;

        using var result = rotate.Process(image);

        Assert.Equal(new Size(100, 200), result.Size());
    }

    [Fact]
    public void Sobel_X_ChiBatBienDoc()
    {
        using var image = new Mat(40, 40, MatType.CV_8UC1, Scalar.All(0));
        image[new Rect(20, 0, 20, 40)].SetTo(Scalar.All(255)); // biên dọc ở x = 20

        var sobel = new SobelProcessor();
        Param<ChoiceParameter>(sobel, "Hướng").Value = "X (biên dọc)";
        using var result = sobel.Process(image);

        Assert.True(result.At<byte>(20, 20) > 100);  // trên biên
        Assert.Equal(0, result.At<byte>(20, 5));     // vùng phẳng
    }

    [Fact]
    public void Laplacian_PhanUngTaiBien_KhongPhanUngVungPhang()
    {
        using var image = new Mat(40, 40, MatType.CV_8UC1, Scalar.All(0));
        image[new Rect(20, 0, 20, 40)].SetTo(Scalar.All(255));

        using var result = new LaplacianProcessor().Process(image);

        Assert.True(result.At<byte>(20, 20) > 50);
        Assert.Equal(0, result.At<byte>(20, 5));
    }

    [Theory]
    [InlineData(3)] // tam giác
    [InlineData(4)] // tứ giác
    [InlineData(6)] // lục giác
    public void XapXiContour_DemDungSoDinh(int sides)
    {
        var polygon = Enumerable.Range(0, sides)
            .Select(i => new Point(100 + 70 * Math.Cos(2 * Math.PI * i / sides), 100 + 70 * Math.Sin(2 * Math.PI * i / sides)))
            .ToArray();
        using var image = Shape(polygon);
        var approx = new ApproxContoursProcessor();

        using var _ = approx.Process(image); // Contours chưa nối → tự tìm trên ảnh

        Assert.Equal(sides, Output<double>(approx, "Số đỉnh (vật lớn nhất)").Value);
    }

    [Fact]
    public void MinAreaRect_DoDungGocNghiengVaKichThuoc()
    {
        var rect = new RotatedRect(new Point2f(100, 100), new Size2f(100, 40), 20);
        using var image = Shape(ContourHelper.ToPoints(Cv2.BoxPoints(rect)));
        var box = new BoundingBoxProcessor();

        using var _ = box.Process(image);

        Assert.Equal(1, Output<double>(box, "Số hộp").Value);
        Assert.InRange(Math.Abs(Output<double>(box, "Góc").Value), 18, 22);
        Assert.InRange(Output<double>(box, "Rộng (vật lớn nhất)").Value, 96, 104);
        Assert.InRange(Output<double>(box, "Cao (vật lớn nhất)").Value, 37, 44);
    }

    [Fact]
    public void BoundingBox_NoiGocVaoXoayAnh_XoayThangVat()
    {
        // Hộp nghiêng 20° → đo góc → nối vào Xoay ảnh → đo lại phải gần 0°
        var rect = new RotatedRect(new Point2f(100, 100), new Size2f(100, 40), 20);
        using var image = Shape(ContourHelper.ToPoints(Cv2.BoxPoints(rect)));

        var runner = new PipelineRunner();
        var measure = new PipelineNode(new BoundingBoxProcessor());
        var rotate = new PipelineNode(new RotateProcessor());
        var check = new PipelineNode(new BoundingBoxProcessor());
        rotate.Inputs.Single(p => p.Name == "Góc").Source = measure.Outputs.Single(o => o.Name == "Góc");
        ((InputPort<Mat>)rotate.Inputs.Single(p => p.Name == "Ảnh vào")).Source = runner.Source;

        using var _ = runner.Run(image.Clone(), new[] { measure, rotate, check }).Result;

        var after = (ToolOutput<double>)check.Outputs.Single(o => o.Name == "Góc");
        Assert.InRange(after.Value, -1.5, 1.5);
    }

    [Fact]
    public void ConvexHull_HinhChuL_CoMotDefect_HinhVuongKhongCo()
    {
        // Chữ L: lõm ở góc trong
        using var lShape = Shape(new Point(20, 20), new Point(60, 20), new Point(60, 140),
            new Point(160, 140), new Point(160, 180), new Point(20, 180));
        var hull = new ConvexHullProcessor();
        using (hull.Process(lShape)) { }
        Assert.Equal(1, Output<double>(hull, "Số defect").Value);
        Assert.True(Output<double>(hull, "Độ đặc (vật lớn nhất)").Value < 0.8);

        using var square = Shape(new Point(20, 20), new Point(120, 20), new Point(120, 120), new Point(20, 120));
        using (hull.Process(square)) { }
        Assert.Equal(0, Output<double>(hull, "Số defect").Value);
        Assert.InRange(Output<double>(hull, "Độ đặc (vật lớn nhất)").Value, 0.99, 1.0);
    }
}
