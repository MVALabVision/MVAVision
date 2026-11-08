using MVATool.Core.Parameters;
using MVATool.Core.Pipeline;
using OpenCvSharp;

namespace MVATool.Core.Processors;

/// <summary>
/// SO SÁNH VỚI ẢNH CHUẨN (golden template) — kiểm tra lỗi kiểu "khác mẫu là lỗi":
/// trừ ảnh vào cho ảnh chuẩn, pixel nào chênh hơn ngưỡng là pixel KHÁC.
/// Ảnh ra = mặt nạ trắng ở chỗ khác → nối tiếp [Tìm contour] / [Đếm blob] để khoanh lỗi.
///
/// Thử: [Ảnh từ file] (chọn ảnh sản phẩm tốt) → [Phát hiện khác biệt], nối
///   "Ảnh chuẩn" ← #1 Ảnh từ file › Ảnh
/// rồi nối "Tỉ lệ khác (%)" → [So sánh] với ngưỡng nhỏ là có job phán OK/NG.
///
/// Lưu ý: hai ảnh phải chụp cùng góc, cùng vị trí (camera cố định, đồ gá cố định).
/// </summary>
public class ImageDiffProcessor : IImageProcessor
{
    private readonly InputPort<Mat> _reference = new("Ảnh chuẩn");
    private readonly NumberParameter _threshold = new("Ngưỡng chênh", min: 0, max: 255, value: 30);
    private readonly NumberParameter _blur = new("Làm mờ trước", min: 1, max: 15, value: 3, step: 2);

    private readonly ToolOutput<double> _percent = new("Tỉ lệ khác (%)");
    private readonly ToolOutput<double> _pixels = new("Số pixel khác");

    public string Name => "Phát hiện khác biệt (ImageDiff)";

    public IReadOnlyList<ProcessorParameter> Parameters { get; }

    public IReadOnlyList<ToolOutput> Outputs { get; }

    public ImageDiffProcessor()
    {
        Parameters = new ProcessorParameter[] { _reference, _threshold, _blur };
        Outputs = new ToolOutput[] { _percent, _pixels };
    }

    public Mat Process(Mat input)
    {
        if (_reference.Value is null)
            throw new ToolNotReadyException("Chưa nối \"Ảnh chuẩn\" — thêm tool Ảnh từ file phía trên rồi kéo dây \"Ảnh\" vào.");

        using var gray = input.ToGrayscale();
        using var reference = _reference.Value.ToGrayscale();
        if (reference.Size() != gray.Size())
            throw new InvalidOperationException(
                $"Ảnh chuẩn ({reference.Width}×{reference.Height}) phải cùng kích thước ảnh vào ({gray.Width}×{gray.Height}).");

        // Làm mờ nhẹ cả hai trước khi trừ: bỏ qua nhiễu cảm biến và lệch 1 pixel.
        var k = (int)_blur.Value;
        if (k > 1)
        {
            Cv2.GaussianBlur(gray, gray, new Size(k, k), 0);
            Cv2.GaussianBlur(reference, reference, new Size(k, k), 0);
        }

        using var diff = new Mat();
        Cv2.Absdiff(gray, reference, diff);

        var mask = new Mat();
        Cv2.Threshold(diff, mask, _threshold.Value, 255, ThresholdTypes.Binary);

        var count = Cv2.CountNonZero(mask);
        _pixels.Value = count;
        _percent.Value = 100.0 * count / (mask.Width * mask.Height);
        return mask;
    }
}
