using MVATool.Core.Parameters;
using OpenCvSharp;

namespace MVATool.Core.Processors;

/// <summary>
/// Tìm biên (edge) bằng thuật toán Canny.
/// Kéo 2 slider ngưỡng khi bật auto-run để cảm nhận trực quan
/// tác dụng của hysteresis thresholding.
/// </summary>
public class CannyEdgeProcessor : IImageProcessor
{
    private readonly NumberParameter _threshold1 = new("Ngưỡng dưới", min: 0, max: 255, value: 50);
    private readonly NumberParameter _threshold2 = new("Ngưỡng trên", min: 0, max: 255, value: 150);

    public string Name => "Canny Edge (tìm biên)";

    public IReadOnlyList<ProcessorParameter> Parameters { get; }

    public CannyEdgeProcessor()
    {
        Parameters = new ProcessorParameter[] { _threshold1, _threshold2 };
    }

    public Mat Process(Mat input)
    {
        using var gray = input.ToGrayscale();
        var output = new Mat();
        Cv2.Canny(gray, output, _threshold1.Value, _threshold2.Value);
        return output;
    }
}
