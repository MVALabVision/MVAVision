using MVATool.Core.Parameters;
using OpenCvSharp;

namespace MVATool.Core.Processors;

/// <summary>
/// LÀM SẮC NÉT bằng Unsharp Mask:
///     sắc nét = ảnh + Mức độ × (ảnh − ảnh_làm_mờ)
/// Phần (ảnh − ảnh mờ) chính là chi tiết/biên; cộng thêm nó vào là biên nổi rõ hơn.
/// "Bán kính" là sigma của Gaussian: lớn → làm nổi chi tiết thô, nhỏ → chi tiết mịn.
/// </summary>
public class SharpenProcessor : IImageProcessor
{
    private readonly NumberParameter _amount = new("Mức độ", min: 0, max: 5, value: 1.5, step: 0.1);
    private readonly NumberParameter _radius = new("Bán kính (sigma)", min: 0.5, max: 10, value: 2, step: 0.5);

    public string Name => "Làm sắc nét (Unsharp)";

    public IReadOnlyList<ProcessorParameter> Parameters { get; }

    public SharpenProcessor()
    {
        Parameters = new ProcessorParameter[] { _amount, _radius };
    }

    public Mat Process(Mat input)
    {
        using var blurred = new Mat();
        Cv2.GaussianBlur(input, blurred, new Size(0, 0), _radius.Value);

        // output = input × (1 + amount) + blurred × (−amount)
        var output = new Mat();
        Cv2.AddWeighted(input, 1 + _amount.Value, blurred, -_amount.Value, 0, output);
        return output;
    }
}
