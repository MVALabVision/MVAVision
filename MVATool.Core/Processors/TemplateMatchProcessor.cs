using MVATool.Core.Parameters;
using MVATool.Core.Pipeline;
using OpenCvSharp;

namespace MVATool.Core.Processors;

/// <summary>
/// TÌM VẬT THEO ẢNH MẪU (Template matching) — thuật toán cần HAI ảnh:
/// ảnh cần tìm ("Ảnh vào") và ảnh mẫu ("Ảnh mẫu").
///
/// Thử: [Ảnh từ file] → [Template matching], rồi nối
///   "Ảnh mẫu" ← #1 Ảnh từ file › Ảnh
/// (mẹo: cắt một vật trên ảnh gốc → nút 📷 ở tool Ảnh từ file để lưu làm mẫu → Khôi phục).
/// Nối tiếp "Số khớp" → [So sánh] là có job đếm/kiểm tra sự có mặt của chi tiết.
///
/// Giới hạn của template matching: chỉ chịu được dịch chuyển — vật bị xoay hay
/// đổi kích thước nhiều thì điểm khớp tụt (muốn chịu xoay phải dùng feature matching).
/// </summary>
public class TemplateMatchProcessor : IImageProcessor
{
    public static readonly IReadOnlyList<string> Methods = new[] { "CCOEFF chuẩn hóa", "CCORR chuẩn hóa", "SQDIFF chuẩn hóa" };

    private readonly InputPort<Mat> _template = new("Ảnh mẫu");
    private readonly ChoiceParameter _method = new("Phương pháp", Methods);
    private readonly NumberParameter _minScore = new("Điểm tối thiểu", min: 0, max: 1, value: 0.8, step: 0.01);
    private readonly NumberParameter _maxMatches = new("Số khớp tối đa", min: 1, max: 50, value: 1);

    private readonly ToolOutput<double> _count = new("Số khớp");
    private readonly ToolOutput<double> _bestScore = new("Điểm cao nhất");
    private readonly ToolOutput<double> _x = new("X");
    private readonly ToolOutput<double> _y = new("Y");
    private readonly ToolOutput<bool> _found = new("Tìm thấy");

    public string Name => "Template matching (ảnh mẫu)";

    public IReadOnlyList<ProcessorParameter> Parameters { get; }

    public IReadOnlyList<ToolOutput> Outputs { get; }

    public TemplateMatchProcessor()
    {
        Parameters = new ProcessorParameter[] { _template, _method, _minScore, _maxMatches };
        Outputs = new ToolOutput[] { _count, _bestScore, _x, _y, _found };
    }

    public Mat Process(Mat input)
    {
        if (_template.Value is null)
            throw new ToolNotReadyException("Chưa nối \"Ảnh mẫu\" — thêm tool Ảnh từ file phía trên rồi kéo dây \"Ảnh\" vào.");

        // So khớp trên ảnh xám: nhanh gấp 3 và không lỗi khi ảnh vào màu còn ảnh mẫu xám.
        using var gray = input.ToGrayscale();
        using var template = _template.Value.ToGrayscale();
        if (template.Width > gray.Width || template.Height > gray.Height)
            throw new InvalidOperationException(
                $"Ảnh mẫu ({template.Width}×{template.Height}) lớn hơn ảnh vào ({gray.Width}×{gray.Height}).");

        var matches = FindMatches(gray, template);

        _count.Value = matches.Count;
        _found.Value = matches.Count > 0;
        _bestScore.Value = matches.Count > 0 ? matches[0].Score : 0;
        // Tâm của khớp tốt nhất — nối sang tool khác để đo vị trí / căn chỉnh.
        _x.Value = matches.Count > 0 ? matches[0].Box.X + matches[0].Box.Width / 2.0 : 0;
        _y.Value = matches.Count > 0 ? matches[0].Box.Y + matches[0].Box.Height / 2.0 : 0;

        var output = input.ToBgr();
        var thickness = Math.Max(2, output.Width / 400);
        for (var i = 0; i < matches.Count; i++)
        {
            var (box, score) = matches[i];
            var color = i == 0 ? new Scalar(0, 255, 0) : new Scalar(0, 200, 255); // tốt nhất: xanh lá
            Cv2.Rectangle(output, box, color, thickness);
            Cv2.PutText(output, score.ToString("0.00"), new Point(box.X, Math.Max(12, box.Y - 4)),
                HersheyFonts.HersheySimplex, Math.Max(0.4, output.Width / 1600.0), color, thickness / 2 + 1);
        }
        return output;
    }

    /// <summary>
    /// Tìm tối đa N vị trí khớp, điểm từ cao xuống thấp.
    /// Sau mỗi lần lấy đỉnh cao nhất, "xóa" vùng quanh nó trên bản đồ điểm để lần sau
    /// không bắt lại đúng vật đó (non-maximum suppression kiểu đơn giản).
    /// </summary>
    private List<(Rect Box, double Score)> FindMatches(Mat image, Mat template)
    {
        var method = _method.Value switch
        {
            "CCORR chuẩn hóa" => TemplateMatchModes.CCorrNormed,
            "SQDIFF chuẩn hóa" => TemplateMatchModes.SqDiffNormed,
            _ => TemplateMatchModes.CCoeffNormed,
        };

        using var scores = new Mat();
        Cv2.MatchTemplate(image, template, scores, method);

        // SQDIFF: càng NHỎ càng giống → đảo thành 1 - x để mọi phương pháp đều "càng lớn càng giống".
        if (method == TemplateMatchModes.SqDiffNormed)
            Cv2.Subtract(Scalar.All(1), scores, scores);

        var matches = new List<(Rect, double)>();
        for (var i = 0; i < (int)_maxMatches.Value; i++)
        {
            Cv2.MinMaxLoc(scores, out _, out var max, out _, out var location);
            if (max < _minScore.Value)
                break;

            matches.Add((new Rect(location.X, location.Y, template.Width, template.Height), max));

            // Xóa vùng lân cận rộng bằng nửa ảnh mẫu mỗi phía (2 vật chồng nhau quá nửa coi là 1).
            var suppress = new Rect(
                location.X - template.Width / 2, location.Y - template.Height / 2,
                template.Width, template.Height);
            Cv2.Rectangle(scores, suppress, Scalar.All(-1), -1);
        }
        return matches;
    }
}
