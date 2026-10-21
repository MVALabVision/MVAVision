using CommunityToolkit.Mvvm.ComponentModel;
using MVATool.Core.Pipeline;

namespace MVATool.Core.Parameters;

/// <summary>
/// Lớp cha của mọi tham số mà một thuật toán có thể phơi ra cho UI.
///
/// Điểm hay ở đây: parameter kế thừa ObservableObject nên bản thân nó
/// bind được trực tiếp lên Slider/CheckBox — UI đổi giá trị là thuật toán
/// "thấy" ngay, không cần code trung gian.
///
/// Mỗi tham số cũng là một CỔNG VÀO: thay vì nhập tay, có thể NỐI (Source)
/// vào đầu ra cùng kiểu của một bước đứng trước. Khi chạy, pipeline gọi
/// PullFromSource() để chép giá trị từ đầu ra đó sang tham số — nên code
/// thuật toán vẫn chỉ việc đọc .Value như bình thường.
/// </summary>
public abstract partial class ProcessorParameter : ObservableObject
{
    public string Name { get; }

    /// <summary>Kiểu dữ liệu nối được vào tham số này. null = không nối được (chỉ nhập tay).</summary>
    public virtual Type? LinkType => null;

    /// <summary>Đầu ra đang nối vào. null = dùng giá trị nhập tay.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLinked))]
    private ToolOutput? _source;

    public bool IsLinked => Source is not null;

    protected ProcessorParameter(string name)
    {
        Name = name;
    }

    // Hook của CommunityToolkit: chặn nối sai kiểu ngay lúc nối (không đợi tới lúc chạy).
    partial void OnSourceChanging(ToolOutput? value)
    {
        if (value is not null && value.ValueType != LinkType)
            throw new ArgumentException(
                $"Không nối được '{value.Name}' ({value.ValueType.Name}) vào tham số '{Name}'.");
    }

    /// <summary>Chép giá trị từ đầu ra đã nối sang tham số. Không nối thì không làm gì.</summary>
    public void PullFromSource()
    {
        if (Source is null)
            return;

        if (!Source.HasValue)
            throw new InvalidOperationException(
                $"Tham số '{Name}' nối tới '{Source.Name}' nhưng đầu ra đó chưa có giá trị (bước nguồn đang bị tắt?).");

        Accept(Source.BoxedValue!);
    }

    /// <summary>Lớp con nhận giá trị từ đầu ra (đã đúng kiểu LinkType).</summary>
    protected virtual void Accept(object value)
    {
    }
}
