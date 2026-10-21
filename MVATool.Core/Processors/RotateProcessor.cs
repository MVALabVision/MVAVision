using MVATool.Core.Parameters;
using OpenCvSharp;

namespace MVATool.Core.Processors;

/// <summary>
/// XOAY ẢNH một góc bất kỳ quanh tâm (WarpAffine).
///   - "Giữ trọn ảnh" bật: mở rộng khung để không bị cắt góc (thêm viền đen).
///   - Tắt: giữ nguyên kích thước, góc ảnh bị cắt.
///
/// Mẹo: nối "Góc" vào terminal "Góc" của tool Bounding Box (MinAreaRect)
/// để tự XOAY THẲNG vật thể bị đặt nghiêng.
/// </summary>
public class RotateProcessor : IImageProcessor
{
    private readonly NumberParameter _angle = new("Góc", min: -180, max: 180, value: 15);
    private readonly NumberParameter _scale = new("Tỉ lệ", min: 0.1, max: 3, value: 1, step: 0.1);
    private readonly BoolParameter _keepAll = new("Giữ trọn ảnh (không cắt góc)", value: true);

    public string Name => "Xoay ảnh";

    public IReadOnlyList<ProcessorParameter> Parameters { get; }

    public RotateProcessor()
    {
        Parameters = new ProcessorParameter[] { _angle, _scale, _keepAll };
    }

    public Mat Process(Mat input)
    {
        var center = new Point2f(input.Width / 2f, input.Height / 2f);
        // OpenCV: góc dương = xoay NGƯỢC chiều kim đồng hồ.
        using var matrix = Cv2.GetRotationMatrix2D(center, _angle.Value, _scale.Value);
        var size = input.Size();

        if (_keepAll.Value)
        {
            // Kích thước khung bao ảnh sau xoay, rồi dời tâm vào giữa khung mới.
            var cos = Math.Abs(matrix.At<double>(0, 0));
            var sin = Math.Abs(matrix.At<double>(0, 1));
            // Trừ 1e-6 trước khi làm tròn lên: cos(90°) = 6e-17 chứ không phải 0 tuyệt đối,
            // nếu không ảnh xoay 90° bị thừa 1 px.
            size = new Size((int)Math.Ceiling(input.Height * sin + input.Width * cos - 1e-6),
                            (int)Math.Ceiling(input.Height * cos + input.Width * sin - 1e-6));
            matrix.Set(0, 2, matrix.At<double>(0, 2) + size.Width / 2.0 - center.X);
            matrix.Set(1, 2, matrix.At<double>(1, 2) + size.Height / 2.0 - center.Y);
        }

        var output = new Mat();
        Cv2.WarpAffine(input, output, matrix, size, InterpolationFlags.Linear, BorderTypes.Constant, Scalar.All(0));
        return output;
    }
}
