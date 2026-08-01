using MVATool.Core.Parameters;
using OpenCvSharp;

namespace MVATool.Core.Processors;

/// <summary>
/// VÍ DỤ MẪU đơn giản nhất: không có tham số. Hãy đọc class này đầu tiên.
/// </summary>
public class GrayscaleProcessor : IImageProcessor
{
    public string Name => "Grayscale (thang xám)";

    public IReadOnlyList<ProcessorParameter> Parameters { get; } = Array.Empty<ProcessorParameter>();

    public Mat Process(Mat input) => input.ToGrayscale();
}
