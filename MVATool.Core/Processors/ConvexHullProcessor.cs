using MVATool.Core.Parameters;
using MVATool.Core.Pipeline;
using OpenCvSharp;

namespace MVATool.Core.Processors;

/// <summary>
/// CONVEX HULL (bao lồi) và CONVEXITY DEFECTS (chỗ lõm):
///   - Hull: đa giác lồi nhỏ nhất bọc quanh contour — như căng dây thun quanh vật.
///   - Defect: mỗi chỗ contour "lõm" vào so với hull; độ sâu = khoảng cách từ điểm
///     lõm nhất tới cạnh hull. Lọc theo "Độ sâu min" để bỏ lõm nhỏ do nhiễu.
///
/// Ứng dụng: phát hiện mẻ / khuyết trên vật lẽ ra phải lồi (nắp chai, viên thuốc...),
/// đếm khe giữa các ngón tay. Nối "Số defect" vào So sánh (= 0 → OK).
/// "Độ đặc" = diện tích vật / diện tích hull (1.0 = lồi hoàn hảo).
/// </summary>
public class ConvexHullProcessor : IImageProcessor
{
    private readonly InputPort<Point[][]> _contoursIn = new("Contours");
    private readonly NumberParameter _minDepth = new("Độ sâu defect min (px)", min: 0, max: 100, value: 5);
    private readonly ToolOutput<Point[][]> _hulls = new("Hulls");
    private readonly ToolOutput<double> _defects = new("Số defect");
    private readonly ToolOutput<double> _solidity = new("Độ đặc (vật lớn nhất)");

    public string Name => "Convex Hull & Defects";

    public IReadOnlyList<ProcessorParameter> Parameters { get; }

    public IReadOnlyList<ToolOutput> Outputs { get; }

    public ConvexHullProcessor()
    {
        Parameters = new ProcessorParameter[] { _contoursIn, _minDepth };
        Outputs = new ToolOutput[] { _hulls, _defects, _solidity };
    }

    public Mat Process(Mat input)
    {
        var contours = ContourHelper.FromPortOrImage(_contoursIn, input);
        var hulls = contours.Select(c => Cv2.ConvexHull(c)).ToArray();

        var output = input.ToBgr();
        Cv2.Polylines(output, hulls, true, new Scalar(0, 200, 0), 2);

        var defectCount = 0;
        foreach (var contour in contours)
        {
            foreach (var (start, end, far) in FindDefects(contour, _minDepth.Value))
            {
                defectCount++;
                Cv2.Line(output, start, end, new Scalar(0, 215, 255), 1);
                Cv2.Circle(output, far, 5, new Scalar(0, 0, 255), -1);
            }
        }

        _hulls.Value = hulls;
        _defects.Value = defectCount;

        var largest = ContourHelper.LargestIndex(contours);
        var hullArea = largest < 0 ? 0 : Cv2.ContourArea(hulls[largest]);
        _solidity.Value = hullArea > 0 ? Cv2.ContourArea(contours[largest]) / hullArea : 0;
        return output;
    }

    /// <summary>Các chỗ lõm sâu hơn <paramref name="minDepth"/> px: (điểm đầu, điểm cuối trên hull, điểm lõm nhất).</summary>
    public static IEnumerable<(Point Start, Point End, Point Far)> FindDefects(Point[] contour, double minDepth)
    {
        if (contour.Length < 4)
            return Array.Empty<(Point, Point, Point)>();

        Vec4i[] defects;
        try
        {
            var hullIndices = Cv2.ConvexHullIndices(contour);
            if (hullIndices.Length < 3)
                return Array.Empty<(Point, Point, Point)>();
            defects = Cv2.ConvexityDefects(contour, hullIndices);
        }
        catch (OpenCVException)
        {
            // Contour tự cắt nhau đôi khi làm OpenCV từ chối ("hull indices not monotonous") → bỏ qua vật đó.
            return Array.Empty<(Point, Point, Point)>();
        }

        // Item3 = độ sâu × 256 (fixed-point của OpenCV).
        return defects
            .Where(d => d.Item3 / 256.0 >= minDepth)
            .Select(d => (contour[d.Item0], contour[d.Item1], contour[d.Item2]))
            .ToList();
    }
}
