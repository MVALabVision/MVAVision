using MVATool.Core.Parameters;
using MVATool.Core.Pipeline;
using OpenCvSharp;

namespace MVATool.Core.Processors;

/// <summary>
/// BOUNDING BOX và MINIMUM AREA RECTANGLE của từng contour:
///   - Bounding Box (Cv2.BoundingRect): hộp THẲNG nhỏ nhất chứa vật — nhanh, dùng để cắt ROI.
///   - Min Area Rect (Cv2.MinAreaRect): hộp XOAY nhỏ nhất ôm sát vật — cho kích thước
///     thật + góc nghiêng của vật đặt lệch.
///
/// Terminal ra (của vật LỚN NHẤT): Rộng, Cao, Góc. Góc chuẩn hóa về (-45°, 45°]
/// → nối vào "Góc" của tool Xoay ảnh để xoay thẳng vật.
/// </summary>
public class BoundingBoxProcessor : IImageProcessor
{
    private const string Upright = "Bounding Box (thẳng)";
    private const string Rotated = "Min Area Rect (xoay)";
    private const string Both = "Cả hai";

    private readonly InputPort<Point[][]> _contoursIn = new("Contours");
    private readonly ChoiceParameter _mode = new("Kiểu hộp", new[] { Both, Upright, Rotated });
    private readonly ToolOutput<double> _count = new("Số hộp");
    private readonly ToolOutput<double> _width = new("Rộng (vật lớn nhất)");
    private readonly ToolOutput<double> _height = new("Cao (vật lớn nhất)");
    private readonly ToolOutput<double> _angle = new("Góc");

    public string Name => "Bounding Box / Min Area Rect";

    public IReadOnlyList<ProcessorParameter> Parameters { get; }

    public IReadOnlyList<ToolOutput> Outputs { get; }

    public BoundingBoxProcessor()
    {
        Parameters = new ProcessorParameter[] { _contoursIn, _mode };
        Outputs = new ToolOutput[] { _count, _width, _height, _angle };
    }

    public Mat Process(Mat input)
    {
        var contours = ContourHelper.FromPortOrImage(_contoursIn, input);
        var drawUpright = _mode.Value != Rotated;
        var drawRotated = _mode.Value != Upright;

        var output = input.ToBgr();
        foreach (var contour in contours)
        {
            if (drawUpright)
                Cv2.Rectangle(output, Cv2.BoundingRect(contour), new Scalar(0, 200, 0), 2);
            if (drawRotated)
            {
                var box = ContourHelper.ToPoints(Cv2.BoxPoints(Cv2.MinAreaRect(contour)));
                Cv2.Polylines(output, new[] { box }, true, new Scalar(0, 0, 255), 2);
            }
        }

        _count.Value = contours.Length;
        var largest = ContourHelper.LargestIndex(contours);
        if (largest < 0)
        {
            _width.Value = 0;
            _height.Value = 0;
            _angle.Value = 0;
            return output;
        }

        if (drawRotated)
        {
            var (width, height, angle) = Normalize(Cv2.MinAreaRect(contours[largest]));
            _width.Value = width;
            _height.Value = height;
            _angle.Value = angle;
        }
        else
        {
            var rect = Cv2.BoundingRect(contours[largest]);
            _width.Value = rect.Width;
            _height.Value = rect.Height;
            _angle.Value = 0;
        }
        return output;
    }

    /// <summary>
    /// Góc của RotatedRect phụ thuộc phiên bản OpenCV (0..90 hoặc -90..0) → đưa về (-45, 45]
    /// và hoán đổi rộng/cao cho khớp, để "Góc" luôn là độ nghiêng nhỏ nhất của vật.
    /// </summary>
    public static (double Width, double Height, double Angle) Normalize(RotatedRect rect)
    {
        double width = rect.Size.Width, height = rect.Size.Height, angle = rect.Angle;
        while (angle > 45)
        {
            angle -= 90;
            (width, height) = (height, width);
        }
        while (angle <= -45)
        {
            angle += 90;
            (width, height) = (height, width);
        }
        return (width, height, angle);
    }
}
