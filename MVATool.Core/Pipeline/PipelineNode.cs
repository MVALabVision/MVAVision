using MVATool.Core.Parameters;

namespace MVATool.Core.Pipeline;

/// <summary>MỘT TOOL trong job + trạng thái bật/tắt.</summary>
public sealed class PipelineNode
{
    public PipelineNode(IVisionTool tool)
    {
        Tool = tool;
    }

    /// <summary>Tiện cho processor ảnh: tự bọc bằng ImageProcessorTool.</summary>
    public PipelineNode(IImageProcessor processor)
        : this(new ImageProcessorTool(processor))
    {
    }

    public IVisionTool Tool { get; }

    /// <summary>Tắt = bỏ qua khi chạy; các terminal ra của nó sẽ không có giá trị.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Terminal vào (= tham số của tool).</summary>
    public IReadOnlyList<ProcessorParameter> Inputs => Tool.Parameters;

    /// <summary>Terminal ra.</summary>
    public IReadOnlyList<ToolOutput> Outputs => Tool.Outputs;
}
