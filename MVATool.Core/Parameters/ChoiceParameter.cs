using CommunityToolkit.Mvvm.ComponentModel;

namespace MVATool.Core.Parameters;

/// <summary>Tham số chọn 1 trong danh sách — UI hiển thị bằng ComboBox. Không nối dây được.</summary>
public partial class ChoiceParameter : ProcessorParameter
{
    public IReadOnlyList<string> Options { get; }

    [ObservableProperty]
    private string _value;

    public ChoiceParameter(string name, IReadOnlyList<string> options, string? value = null)
        : base(name)
    {
        Options = options;
        _value = value ?? options[0];
    }
}
