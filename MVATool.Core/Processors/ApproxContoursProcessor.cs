using MVATool.Core.Parameters;
using MVATool.Core.Pipeline;
using OpenCvSharp;

namespace MVATool.Core.Processors;

/// <summary>
/// XẤP XỈ CONTOUR bằng đa giác (thuật toán Douglas–Peucker, Cv2.ApproxPolyDP):
/// bỏ bớt điểm sao cho đường mới lệch khỏi contour gốc không quá epsilon.
/// epsilon = "Sai số %" × chu vi contour.
///
/// Ứng dụng kinh điển: NHẬN DẠNG HÌNH — đếm số đỉnh của vật lớn nhất
/// (3 = tam giác, 4 = tứ giác, nhiều = tròn) rồi nối "Số đỉnh" vào tool So sánh.
/// </summary>
public class ApproxContoursProcessor : IImageProcessor
{
    private readonly InputPort<Point[][]> _contoursIn = new("Contours");
    private readonly NumberParameter _epsilon = new("Sai số % chu vi", min: 0.1, max: 10, value: 2, step: 0.1);
    private readonly ToolOutput<Point[][]> _approx = new("Contours xấp xỉ");
    private readonly ToolOutput<double> _vertices = new("Số đỉnh (vật lớn nhất)");

    public string Name => "Xấp xỉ contour (đa giác)";

    public IReadOnlyList<ProcessorParameter> Parameters { get; }

    public IReadOnlyList<ToolOutput> Outputs { get; }

    public ApproxContoursProcessor()
    {
        Parameters = new ProcessorParameter[] { _contoursIn, _epsilon };
        Outputs = new ToolOutput[] { _approx, _vertices };
    }

    public Mat Process(Mat input)
    {
        var contours = ContourHelper.FromPortOrImage(_contoursIn, input);
        var approx = contours
            .Select(c => Cv2.ApproxPolyDP(c, _epsilon.Value / 100.0 * Cv2.ArcLength(c, true), true))
            .ToArray();

        _approx.Value = approx;
        var largest = ContourHelper.LargestIndex(approx);
        _vertices.Value = largest < 0 ? 0 : approx[largest].Length;

        var output = input.ToBgr();
        Cv2.Polylines(output, approx, true, new Scalar(0, 215, 255), 2); // vàng (BGR)
        foreach (var point in approx.SelectMany(p => p))
            Cv2.Circle(output, point, 3, new Scalar(255, 80, 30), -1);  // đỉnh: xanh dương
        return output;
    }
}
