using CommunityToolkit.Mvvm.ComponentModel;
using OpenCvSharp;

namespace MVATool.Core.Pipeline;

/// <summary>
/// MỘT TERMINAL RA của tool — ví dụ "Ảnh ra", "Số contour", "Kết quả" (OK/NG).
///
/// Tool GHI giá trị vào đây khi chạy; tool đứng SAU nối terminal vào cùng kiểu
/// (ProcessorParameter.Source) vào đây để dùng kết quả đó.
/// Kiểu nào cũng được: ảnh (Mat), số (double), Đúng/Sai (bool), chữ (string),
/// contour (Point[][])...
/// </summary>
public abstract class ToolOutput : ObservableObject
{
    public string Name { get; }

    /// <summary>Kiểu dữ liệu — chỉ nối được vào terminal vào cùng kiểu.</summary>
    public abstract Type ValueType { get; }

    /// <summary>false = lần chạy này tool chưa ghi gì (bị tắt, hoặc chưa chạy tới).</summary>
    public bool HasValue { get; protected set; }

    /// <summary>Giá trị dạng object — để job chuyển sang tham số mà không cần biết kiểu cụ thể.</summary>
    public abstract object? BoxedValue { get; }

    /// <summary>Chuỗi hiển thị trên UI ("128", "OK", "640×480", "12 contour").</summary>
    public abstract string DisplayText { get; }

    /// <summary>Xóa giá trị — job gọi trước/sau mỗi lần chạy.</summary>
    public abstract void Reset();

    protected ToolOutput(string name)
    {
        Name = name;
    }
}

/// <summary>Terminal ra có kiểu cụ thể. Tool khai báo: <c>new ToolOutput&lt;double&gt;("Số contour")</c>.</summary>
public sealed class ToolOutput<T> : ToolOutput
{
    private T? _value;

    // Tính sẵn chuỗi hiển thị lúc GHI: UI đọc DisplayText trên thread khác, có khi
    // ảnh đã bị Dispose rồi — đọc Mat.Width lúc đó sẽ lỗi.
    private string _displayText = "—";

    public ToolOutput(string name)
        : base(name)
    {
    }

    public override Type ValueType => typeof(T);

    public T? Value
    {
        get => _value;
        set
        {
            _value = value;
            HasValue = value is not null;
            _displayText = Format(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayText));
        }
    }

    public override object? BoxedValue => _value;

    public override string DisplayText => _displayText;

    public override void Reset()
    {
        _value = default;
        HasValue = false;
        _displayText = "—";
        OnPropertyChanged(nameof(Value));
        OnPropertyChanged(nameof(DisplayText));
    }

    private static string Format(T? value) => value switch
    {
        null => "—",
        double d => d.ToString("0.##"),
        bool b => b ? "Đúng" : "Sai",
        Mat m => $"{m.Width}×{m.Height}",
        Point[][] c => $"{c.Length} contour",
        _ => value.ToString() ?? "—",
    };
}
