namespace MVATool.Core.Parameters;

/// <summary>Lớp cha không-generic của InputPort&lt;T&gt; — để View có một DataTemplate chung.</summary>
public abstract class InputPort : ProcessorParameter
{
    protected InputPort(string name)
        : base(name)
    {
    }

    /// <summary>Job gọi sau mỗi lần chạy để không giữ tham chiếu tới dữ liệu cũ (ảnh đã Dispose).</summary>
    internal abstract void Clear();
}

/// <summary>
/// TERMINAL VÀO thuần (không nhập tay được) — chỉ nhận dữ liệu qua dây nối.
/// Ví dụ: InputPort&lt;Mat&gt;("Mặt nạ"), InputPort&lt;Point[][]&gt;("Contours").
///
/// Value chỉ có trong lúc job đang chạy; dữ liệu thuộc về tool nguồn —
/// đọc thôi, không Dispose, không sửa.
/// </summary>
public sealed class InputPort<T> : InputPort
{
    public InputPort(string name)
        : base(name)
    {
    }

    public override Type? LinkType => typeof(T);

    /// <summary>Dữ liệu nhận được. null/default = chưa nối.</summary>
    public T? Value { get; private set; }

    protected override void Accept(object value) => Value = (T)value;

    /// <summary>Job cấp giá trị mặc định khi terminal chưa nối (vd: "Ảnh vào" ← ảnh của tool trước).</summary>
    internal void Feed(T value) => Value = value;

    internal override void Clear() => Value = default;
}
