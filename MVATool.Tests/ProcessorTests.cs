using MVATool.Core.Parameters;
using MVATool.Core.Processors;
using OpenCvSharp;
using Xunit;

namespace MVATool.Tests;

/// <summary>
/// Test tầng thuật toán: chạy được KHÔNG CẦN mở app, không cần UI —
/// vì MVATool.Core không phụ thuộc WPF.
/// </summary>
public class ProcessorTests
{
    [Fact]
    public void Threshold_ChiChoRaHaiGiaTri_0_Va_255()
    {
        // Ảnh gradient 0..255 → sau threshold chỉ còn đúng 2 giá trị
        using var gradient = new Mat(1, 256, MatType.CV_8UC1);
        for (var i = 0; i < 256; i++)
            gradient.Set<byte>(0, i, (byte)i);

        using var result = new ThresholdProcessor().Process(gradient);

        Cv2.MinMaxLoc(result, out double min, out double max);
        Assert.Equal(0, min);
        Assert.Equal(255, max);

        // Không được có giá trị trung gian nào (1..254)
        using var middleValues = new Mat();
        Cv2.InRange(result, new Scalar(1), new Scalar(254), middleValues);
        Assert.Equal(0, Cv2.CountNonZero(middleValues));
    }

    [Fact]
    public void GaussianBlur_KernelChan_VanChayDuoc()
    {
        using var input = new Mat(32, 32, MatType.CV_8UC3, new Scalar(100, 150, 200));
        var processor = new GaussianBlurProcessor();

        // Cố tình đặt kernel chẵn — processor phải tự sửa thành lẻ thay vì ném lỗi
        ((NumberParameter)processor.Parameters[0]).Value = 4;

        using var result = processor.Process(input);
        Assert.Equal(input.Size(), result.Size());
    }

    [Fact]
    public void Grayscale_AnhMau_ThanhMotKenh()
    {
        using var input = new Mat(8, 8, MatType.CV_8UC3, new Scalar(10, 20, 30));
        using var result = new GrayscaleProcessor().Process(input);
        Assert.Equal(1, result.Channels());
    }

    [Fact]
    public void Invert_HaiLan_TraVeAnhGoc()
    {
        using var input = new Mat(8, 8, MatType.CV_8UC3, new Scalar(10, 20, 30));
        var invert = new InvertProcessor();

        using var once = invert.Process(input);
        using var twice = invert.Process(once);

        using var diff = new Mat();
        Cv2.Absdiff(input, twice, diff);
        using var flat = diff.Reshape(1);
        Assert.Equal(0, Cv2.CountNonZero(flat));
    }
}
