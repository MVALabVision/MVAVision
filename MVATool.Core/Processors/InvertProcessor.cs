using MVATool.Core.Parameters;
using OpenCvSharp;

namespace MVATool.Core.Processors;

/// <summary>Đảo màu ảnh (âm bản): mỗi pixel = 255 - giá trị cũ.</summary>
public class InvertProcessor : IImageProcessor
{
    public string Name => "Âm bản (Invert)";

    public IReadOnlyList<ProcessorParameter> Parameters { get; } = Array.Empty<ProcessorParameter>();

    public Mat Process(Mat input)
    {
        var output = new Mat();
        Cv2.BitwiseNot(input, output);
        return output;
    }
}
