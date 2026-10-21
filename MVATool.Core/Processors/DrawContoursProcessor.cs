using MVATool.Core.Parameters;
using OpenCvSharp;

namespace MVATool.Core.Processors;

/// <summary>
/// Vẽ contour nhận qua dây nối lên ảnh vào — ví dụ terminal vào kiểu KHÔNG phải ảnh/số.
///
/// Thử: [Threshold] → [Tìm contour] → [Vẽ contour], rồi nối:
///   - "Ảnh vào"  ← Ảnh gốc
///   - "Contours" ← #2 Tìm contour › Contours
/// → contour tìm trên ảnh nhị phân được vẽ lên ảnh màu gốc.
/// </summary>
public class DrawContoursProcessor : IImageProcessor
{
    private readonly InputPort<Point[][]> _contours = new("Contours");
    private readonly NumberParameter _thickness = new("Độ dày nét", min: 1, max: 10, value: 2);

    public string Name => "Vẽ contour";

    public IReadOnlyList<ProcessorParameter> Parameters { get; }

    public DrawContoursProcessor()
    {
        Parameters = new ProcessorParameter[] { _contours, _thickness };
    }

    public Mat Process(Mat input)
    {
        var output = input.ToBgr();
        if (_contours.Value is { } contours)
            Cv2.DrawContours(output, contours, -1, new Scalar(0, 0, 255), (int)_thickness.Value);
        return output;
    }
}
