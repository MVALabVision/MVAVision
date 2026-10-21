using MVATool.Core.Parameters;
using MVATool.Core.Pipeline;

namespace MVATool.Core.Tools;

/// <summary>
/// So sánh một số đo với ngưỡng → ra Đúng/Sai và chữ OK/NG.
/// Đây là tool KHÔNG xử lý ảnh nên implement thẳng IVisionTool (không có "Ảnh vào/ra").
///
/// Thử: [Threshold] → [Tìm contour] → [So sánh], nối "Giá trị" ← "Số contour",
/// chọn "=" và đặt "Ngưỡng" = số vật thể mong đợi → job tự phán OK/NG.
/// </summary>
public class CompareTool : IVisionTool
{
    public static readonly IReadOnlyList<string> Operators = new[] { "≥", ">", "≤", "<", "=", "≠" };

    private readonly NumberParameter _value = new("Giá trị", min: 0, max: 1000, value: 0);
    private readonly ChoiceParameter _operator = new("Phép so sánh", Operators);
    private readonly NumberParameter _threshold = new("Ngưỡng", min: 0, max: 1000, value: 1);
    private readonly ToolOutput<bool> _pass = new("Đạt");
    private readonly ToolOutput<string> _result = new("Kết quả");

    public string Name => "So sánh (OK/NG)";

    public IReadOnlyList<ProcessorParameter> Parameters { get; }

    public IReadOnlyList<ToolOutput> Outputs { get; }

    public CompareTool()
    {
        Parameters = new ProcessorParameter[] { _value, _operator, _threshold };
        Outputs = new ToolOutput[] { _pass, _result };
    }

    public void Run()
    {
        var a = _value.Value;
        var b = _threshold.Value;
        var pass = _operator.Value switch
        {
            "≥" => a >= b,
            ">" => a > b,
            "≤" => a <= b,
            "<" => a < b,
            "=" => Math.Abs(a - b) < 1e-9,
            "≠" => Math.Abs(a - b) >= 1e-9,
            _ => throw new InvalidOperationException($"Phép so sánh lạ: {_operator.Value}"),
        };

        _pass.Value = pass;
        _result.Value = pass ? "OK" : "NG";
    }
}
