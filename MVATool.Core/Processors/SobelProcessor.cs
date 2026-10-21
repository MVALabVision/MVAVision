using MVATool.Core.Parameters;
using OpenCvSharp;

namespace MVATool.Core.Processors;

/// <summary>
/// Toán tử SOBEL — đạo hàm bậc 1 của độ sáng = độ dốc (gradient):
///   - X : làm nổi biên DỌC (độ sáng đổi theo phương ngang)
///   - Y : làm nổi biên NGANG
///   - Độ lớn = √(Gx² + Gy²): biên mọi hướng
/// Tính trên CV_16S (có số âm) rồi lấy trị tuyệt đối về 8 bit để hiển thị được.
/// </summary>
public class SobelProcessor : IImageProcessor
{
    private readonly ChoiceParameter _direction = new("Hướng", new[] { "Độ lớn (X+Y)", "X (biên dọc)", "Y (biên ngang)" });
    private readonly NumberParameter _kernelSize = new("Kernel", min: 1, max: 7, value: 3, step: 2);

    public string Name => "Sobel (đạo hàm bậc 1)";

    public IReadOnlyList<ProcessorParameter> Parameters { get; }

    public SobelProcessor()
    {
        Parameters = new ProcessorParameter[] { _direction, _kernelSize };
    }

    public Mat Process(Mat input)
    {
        using var gray = input.ToGrayscale();
        var ksize = (int)_kernelSize.Value;

        if (_direction.Value.StartsWith("X"))
            return Derivative(gray, 1, 0, ksize);
        if (_direction.Value.StartsWith("Y"))
            return Derivative(gray, 0, 1, ksize);

        using var gx = new Mat();
        using var gy = new Mat();
        Cv2.Sobel(gray, gx, MatType.CV_32F, 1, 0, ksize);
        Cv2.Sobel(gray, gy, MatType.CV_32F, 0, 1, ksize);
        using var magnitude = new Mat();
        Cv2.Magnitude(gx, gy, magnitude);

        var output = new Mat();
        Cv2.ConvertScaleAbs(magnitude, output);
        return output;
    }

    private static Mat Derivative(Mat gray, int dx, int dy, int ksize)
    {
        using var raw = new Mat();
        Cv2.Sobel(gray, raw, MatType.CV_16S, dx, dy, ksize);
        var output = new Mat();
        Cv2.ConvertScaleAbs(raw, output);
        return output;
    }
}
