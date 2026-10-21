using MVATool.Core.Parameters;
using MVATool.Core.Pipeline;
using OpenCvSharp;

namespace MVATool.Core.Processors;

/// <summary>
/// Tìm contour (đường bao vật thể) trên ảnh NHỊ PHÂN — đặt sau Threshold / Canny.
/// Pixel khác 0 được coi là vật thể.
///
/// Terminal ra:
///   - "Ảnh ra"      : ảnh vào đã vẽ contour (xanh lá)
///   - "Contours"    : danh sách contour → nối vào "Vẽ contour" ...
///   - "Số contour"  : số đếm được → nối vào "So sánh" để ra OK/NG
/// </summary>
public class FindContoursProcessor : IImageProcessor
{
    private readonly NumberParameter _minArea = new("Diện tích min", min: 0, max: 5000, value: 50, step: 10);
    private readonly ToolOutput<Point[][]> _contours = new("Contours");
    private readonly ToolOutput<double> _count = new("Số contour");

    public string Name => "Tìm contour";

    public IReadOnlyList<ProcessorParameter> Parameters { get; }

    public IReadOnlyList<ToolOutput> Outputs { get; }

    public FindContoursProcessor()
    {
        Parameters = new ProcessorParameter[] { _minArea };
        Outputs = new ToolOutput[] { _contours, _count };
    }

    public Mat Process(Mat input)
    {
        var contours = ContourHelper.FindExternal(input, _minArea.Value);

        _contours.Value = contours;
        _count.Value = contours.Length;

        var output = input.ToBgr();
        Cv2.DrawContours(output, contours, -1, new Scalar(0, 255, 0), 2);
        return output;
    }
}
