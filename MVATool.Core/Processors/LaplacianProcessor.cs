using MVATool.Core.Parameters;
using OpenCvSharp;

namespace MVATool.Core.Processors;

/// <summary>
/// Toán tử LAPLACIAN — đạo hàm bậc 2: phản ứng mạnh ở nơi độ sáng đổi đột ngột,
/// biên mọi hướng trong 1 phép tính (không cần X/Y như Sobel).
/// Rất nhạy nhiễu → thường làm mờ trước ("Làm mờ trước" = Gaussian 3×3).
/// </summary>
public class LaplacianProcessor : IImageProcessor
{
    private readonly NumberParameter _kernelSize = new("Kernel", min: 1, max: 7, value: 3, step: 2);
    private readonly BoolParameter _blurFirst = new("Làm mờ trước (giảm nhiễu)", value: true);

    public string Name => "Laplacian (đạo hàm bậc 2)";

    public IReadOnlyList<ProcessorParameter> Parameters { get; }

    public LaplacianProcessor()
    {
        Parameters = new ProcessorParameter[] { _kernelSize, _blurFirst };
    }

    public Mat Process(Mat input)
    {
        using var gray = input.ToGrayscale();
        if (_blurFirst.Value)
            Cv2.GaussianBlur(gray, gray, new Size(3, 3), 0);

        using var raw = new Mat();
        Cv2.Laplacian(gray, raw, MatType.CV_16S, (int)_kernelSize.Value);
        var output = new Mat();
        Cv2.ConvertScaleAbs(raw, output);
        return output;
    }
}
