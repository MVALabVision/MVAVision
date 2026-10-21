using MVATool.Core.Parameters;
using OpenCvSharp;

namespace MVATool.Core.Processors;

/// <summary>
/// VÍ DỤ MẪU có HAI ẢNH VÀO: giữ lại phần ảnh nằm trong vùng trắng của mặt nạ,
/// phần còn lại tô đen.
///
/// Thử: [Threshold] → [Áp mặt nạ], rồi nối:
///   - "Ảnh vào" ← Ảnh gốc
///   - "Mặt nạ"  ← #1 Threshold › Ảnh ra
/// → được ảnh màu gốc, chỉ còn các vùng sáng.
/// </summary>
public class MaskProcessor : IImageProcessor
{
    private readonly InputPort<Mat> _mask = new("Mặt nạ");

    public string Name => "Áp mặt nạ (Mask)";

    public IReadOnlyList<ProcessorParameter> Parameters { get; }

    public MaskProcessor()
    {
        Parameters = new ProcessorParameter[] { _mask };
    }

    public Mat Process(Mat input)
    {
        // Chưa nối mặt nạ → không có gì để làm.
        if (_mask.Value is null)
            return input.Clone();

        using var mask = _mask.Value.ToGrayscale();
        if (mask.Size() != input.Size())
            throw new InvalidOperationException("Mặt nạ phải cùng kích thước với ảnh vào.");

        var output = new Mat(input.Size(), input.Type(), Scalar.All(0));
        input.CopyTo(output, mask); // CopyTo coi mọi pixel mask khác 0 là "giữ lại"
        return output;
    }
}
