using CommunityToolkit.Mvvm.ComponentModel;

namespace MVATool.Core.Parameters;

/// <summary>
/// Lớp cha của mọi tham số mà một thuật toán có thể phơi ra cho UI.
///
/// Điểm hay ở đây: parameter kế thừa ObservableObject nên bản thân nó
/// bind được trực tiếp lên Slider/CheckBox — UI đổi giá trị là thuật toán
/// "thấy" ngay, không cần code trung gian.
/// </summary>
public abstract class ProcessorParameter : ObservableObject
{
    public string Name { get; }

    protected ProcessorParameter(string name)
    {
        Name = name;
    }
}
