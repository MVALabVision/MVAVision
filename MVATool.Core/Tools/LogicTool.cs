using MVATool.Core.Parameters;
using MVATool.Core.Pipeline;

namespace MVATool.Core.Tools;

/// <summary>
/// Gộp 2 kết quả Đúng/Sai bằng AND / OR — dùng khi job có nhiều phép kiểm tra
/// (vd: đủ số contour AND độ sáng đạt) và chỉ cần 1 kết luận OK/NG chung.
/// </summary>
public class LogicTool : IVisionTool
{
    private readonly BoolParameter _a = new("A", value: true);
    private readonly ChoiceParameter _operator = new("Phép", new[] { "AND", "OR" });
    private readonly BoolParameter _b = new("B", value: true);
    private readonly ToolOutput<bool> _pass = new("Đạt");
    private readonly ToolOutput<string> _result = new("Kết quả");

    public string Name => "Logic AND/OR";

    public IReadOnlyList<ProcessorParameter> Parameters { get; }

    public IReadOnlyList<ToolOutput> Outputs { get; }

    public LogicTool()
    {
        Parameters = new ProcessorParameter[] { _a, _operator, _b };
        Outputs = new ToolOutput[] { _pass, _result };
    }

    public void Run()
    {
        var pass = _operator.Value == "AND" ? _a.Value && _b.Value : _a.Value || _b.Value;
        _pass.Value = pass;
        _result.Value = pass ? "OK" : "NG";
    }
}
