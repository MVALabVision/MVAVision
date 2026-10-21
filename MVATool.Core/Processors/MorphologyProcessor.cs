using MVATool.Core.Parameters;
using OpenCvSharp;

namespace MVATool.Core.Processors;

/// <summary>
/// Phép toán HÌNH THÁI HỌC — thường dùng sau Threshold để làm sạch ảnh nhị phân:
///   - Erode (co)      : bào mòn biên, xóa đốm trắng nhỏ
///   - Dilate (giãn)   : nở vùng trắng, lấp lỗ nhỏ
///   - Open  (mở)      = Erode rồi Dilate → xóa nhiễu trắng, giữ kích thước vật
///   - Close (đóng)    = Dilate rồi Erode → lấp lỗ đen bên trong vật
///   - Gradient        = Dilate − Erode → còn lại đường viền
///   - TopHat / BlackHat: tách chi tiết sáng / tối nhỏ hơn kernel
/// </summary>
public class MorphologyProcessor : IImageProcessor
{
    private static readonly IReadOnlyList<string> Operations =
        new[] { "Erode (co)", "Dilate (giãn)", "Open (mở)", "Close (đóng)", "Gradient", "TopHat", "BlackHat" };

    private static readonly IReadOnlyList<string> Shapes = new[] { "Chữ nhật", "Elip", "Chữ thập" };

    private readonly ChoiceParameter _operation = new("Phép toán", Operations, "Open (mở)");
    private readonly ChoiceParameter _shape = new("Hình kernel", Shapes);
    private readonly NumberParameter _kernelSize = new("Kernel", min: 1, max: 31, value: 5, step: 2);
    private readonly NumberParameter _iterations = new("Số lần lặp", min: 1, max: 10, value: 1);

    public string Name => "Morphology (hình thái học)";

    public IReadOnlyList<ProcessorParameter> Parameters { get; }

    public MorphologyProcessor()
    {
        Parameters = new ProcessorParameter[] { _operation, _shape, _kernelSize, _iterations };
    }

    public Mat Process(Mat input)
    {
        var size = (int)_kernelSize.Value;
        var shape = _shape.Value switch
        {
            "Elip" => MorphShapes.Ellipse,
            "Chữ thập" => MorphShapes.Cross,
            _ => MorphShapes.Rect,
        };
        using var kernel = Cv2.GetStructuringElement(shape, new Size(size, size));

        var operation = _operation.Value switch
        {
            "Erode (co)" => MorphTypes.Erode,
            "Dilate (giãn)" => MorphTypes.Dilate,
            "Close (đóng)" => MorphTypes.Close,
            "Gradient" => MorphTypes.Gradient,
            "TopHat" => MorphTypes.TopHat,
            "BlackHat" => MorphTypes.BlackHat,
            _ => MorphTypes.Open,
        };

        var output = new Mat();
        Cv2.MorphologyEx(input, output, operation, kernel, iterations: (int)_iterations.Value);
        return output;
    }
}
