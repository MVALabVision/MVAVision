using CommunityToolkit.Mvvm.ComponentModel;

namespace MVATool.Core.Parameters;

/// <summary>Tham số bật/tắt — UI hiển thị bằng CheckBox.</summary>
public partial class BoolParameter : ProcessorParameter
{
    [ObservableProperty]
    private bool _value;

    public BoolParameter(string name, bool value = false)
        : base(name)
    {
        _value = value;
    }
}
