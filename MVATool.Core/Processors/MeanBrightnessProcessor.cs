using MVATool.Core.Parameters;
using MVATool.Core.Pipeline;
using OpenCvSharp;

namespace MVATool.Core.Processors;

/// <summary>
/// VÍ DỤ MẪU có ĐẦU RA PHỤ: đo độ sáng trung bình rồi ghi vào ToolOutput.
/// Ảnh đi qua giữ nguyên — bước này chỉ "đo".
///
/// Thử: [Đo độ sáng] → [Threshold], rồi nối tham số "Ngưỡng" của Threshold
/// vào "Độ sáng TB" → ngưỡng tự chạy theo từng ảnh (rất tiện khi chạy cả thư mục).
/// </summary>
public class MeanBrightnessProcessor : IImageProcessor
{
    private readonly ToolOutput<double> _mean = new("Độ sáng TB");

    public string Name => "Đo độ sáng TB";

    public IReadOnlyList<ProcessorParameter> Parameters { get; } = Array.Empty<ProcessorParameter>();

    public IReadOnlyList<ToolOutput> Outputs { get; }

    public MeanBrightnessProcessor()
    {
        Outputs = new ToolOutput[] { _mean };
    }

    public Mat Process(Mat input)
    {
        using var gray = input.ToGrayscale();
        _mean.Value = Cv2.Mean(gray).Val0;
        return input.Clone();
    }
}
