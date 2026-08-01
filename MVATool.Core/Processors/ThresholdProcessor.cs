using MVATool.Core.Parameters;
using OpenCvSharp;

namespace MVATool.Core.Processors;

/// <summary>
/// Nhị phân hóa ảnh: pixel sáng hơn ngưỡng → trắng (255), còn lại → đen (0).
/// Có 2 loại tham số: số (Slider) và bool (CheckBox).
/// </summary>
public class ThresholdProcessor : IImageProcessor
{
    private readonly NumberParameter _threshold = new("Ngưỡng", min: 0, max: 255, value: 128);
    private readonly BoolParameter _useOtsu = new("Otsu (tự tìm ngưỡng)");

    public string Name => "Threshold (nhị phân)";

    public IReadOnlyList<ProcessorParameter> Parameters { get; }

    public ThresholdProcessor()
    {
        Parameters = new ProcessorParameter[] { _threshold, _useOtsu };
    }

    public Mat Process(Mat input)
    {
        // Threshold của OpenCV yêu cầu ảnh 1 kênh → chuyển thang xám trước.
        using var gray = input.ToGrayscale();

        var type = ThresholdTypes.Binary;
        if (_useOtsu.Value)
            type |= ThresholdTypes.Otsu; // Otsu bỏ qua giá trị ngưỡng, tự tính từ histogram

        var output = new Mat();
        Cv2.Threshold(gray, output, _threshold.Value, 255, type);
        return output;
    }
}
