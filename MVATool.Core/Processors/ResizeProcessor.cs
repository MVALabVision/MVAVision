using MVATool.Core.Parameters;
using MVATool.Core.Pipeline;
using OpenCvSharp;

namespace MVATool.Core.Processors;

/// <summary>
/// THAY ĐỔI KÍCH THƯỚC theo tỉ lệ %. Chọn kiểu nội suy hợp lý:
///   - Thu nhỏ: Area (trung bình vùng) cho ảnh mịn, không răng cưa.
///   - Phóng to: Linear (nhanh) hoặc Cubic (mượt hơn).
///   - Nearest: giữ nguyên giá trị pixel — dùng cho ảnh nhị phân / nhãn.
/// Ngoài ảnh còn ra kích thước mới để tool sau dùng.
/// </summary>
public class ResizeProcessor : IImageProcessor
{
    private readonly NumberParameter _scale = new("Tỉ lệ %", min: 10, max: 400, value: 50, step: 5);
    private readonly ChoiceParameter _interpolation =
        new("Nội suy", new[] { "Tự động", "Nearest", "Linear", "Cubic", "Area", "Lanczos" });
    private readonly ToolOutput<double> _width = new("Rộng");
    private readonly ToolOutput<double> _height = new("Cao");

    public string Name => "Thay đổi kích thước";

    public IReadOnlyList<ProcessorParameter> Parameters { get; }

    public IReadOnlyList<ToolOutput> Outputs { get; }

    public ResizeProcessor()
    {
        Parameters = new ProcessorParameter[] { _scale, _interpolation };
        Outputs = new ToolOutput[] { _width, _height };
    }

    public Mat Process(Mat input)
    {
        var factor = _scale.Value / 100.0;
        var interpolation = _interpolation.Value switch
        {
            "Nearest" => InterpolationFlags.Nearest,
            "Linear" => InterpolationFlags.Linear,
            "Cubic" => InterpolationFlags.Cubic,
            "Area" => InterpolationFlags.Area,
            "Lanczos" => InterpolationFlags.Lanczos4,
            _ => factor < 1 ? InterpolationFlags.Area : InterpolationFlags.Cubic,
        };

        var size = new Size(Math.Max(1, (int)Math.Round(input.Width * factor)),
                            Math.Max(1, (int)Math.Round(input.Height * factor)));
        var output = new Mat();
        Cv2.Resize(input, output, size, 0, 0, interpolation);

        _width.Value = output.Width;
        _height.Value = output.Height;
        return output;
    }
}
