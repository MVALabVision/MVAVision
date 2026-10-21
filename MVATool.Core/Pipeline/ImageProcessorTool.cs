using MVATool.Core.Parameters;
using OpenCvSharp;

namespace MVATool.Core.Pipeline;

/// <summary>
/// Bọc một IImageProcessor (1 ảnh vào → 1 ảnh ra) thành IVisionTool:
/// thêm terminal "Ảnh vào" và "Ảnh ra", giữ nguyên tham số + terminal ra phụ của processor.
///
/// "Ảnh vào" KHÔNG nối thì job tự cấp ảnh ra của tool đứng trước gần nhất —
/// nên xếp Grayscale → Blur → Threshold là chạy luôn, không cần nối dây.
/// </summary>
public sealed class ImageProcessorTool : IVisionTool
{
    public ImageProcessorTool(IImageProcessor processor)
    {
        Processor = processor;
        Parameters = new ProcessorParameter[] { Input }.Concat(processor.Parameters).ToList();
        Outputs = new ToolOutput[] { Image }.Concat(processor.Outputs).ToList();
    }

    public IImageProcessor Processor { get; }

    public string Name => Processor.Name;

    public InputPort<Mat> Input { get; } = new("Ảnh vào");

    public ToolOutput<Mat> Image { get; } = new("Ảnh ra");

    public IReadOnlyList<ProcessorParameter> Parameters { get; }

    public IReadOnlyList<ToolOutput> Outputs { get; }

    public void Run()
    {
        var input = Input.Value ?? throw new InvalidOperationException("Chưa có ảnh vào.");
        Image.Value = Processor.Process(input);
    }
}
