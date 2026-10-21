using CommunityToolkit.Mvvm.ComponentModel;
using MVATool.Core.Parameters;
using MVATool.Core.Pipeline;

namespace MVATool.ViewModels;

/// <summary>
/// MỘT TERMINAL VÀO của tool — View vẽ chấm O rỗng ở bên trái dòng.
/// Bọc ProcessorParameter (Slider / CheckBox / ComboBox vẫn vẽ bằng DataTemplate cũ).
/// </summary>
public partial class InputTerminalViewModel : ObservableObject
{
    public InputTerminalViewModel(PipelineStepViewModel step, ProcessorParameter parameter, bool isMainImage)
    {
        Step = step;
        Parameter = parameter;
        IsMainImage = isMainImage;
        parameter.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ProcessorParameter.Source))
                OnPropertyChanged(nameof(HintText));
        };
    }

    public PipelineStepViewModel Step { get; }

    public ProcessorParameter Parameter { get; }

    /// <summary>"Ảnh vào" của tool xử lý ảnh — chưa nối thì tự lấy ảnh của tool trước.</summary>
    public bool IsMainImage { get; }

    /// <summary>Có chấm O để nối không (ChoiceParameter thì không).</summary>
    public bool IsLinkable => Parameter.LinkType is not null;

    public Type? DataType => Parameter.LinkType;

    public string TypeName => PortTypes.FriendlyName(DataType);

    /// <summary>"← #2 Tìm contour › Số contour" — MainWindowViewModel điền khi nối thay đổi.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HintText))]
    private string _sourceLabel = "";

    /// <summary>Dòng chú thích dưới tên terminal: nguồn đang nối, hoặc gợi ý khi chưa nối.</summary>
    public string HintText =>
        Parameter.IsLinked ? $"← {SourceLabel}"
        : IsMainImage ? "(ảnh của tool trước)"
        : Parameter is InputPort ? "(chưa nối)"
        : "";
}

/// <summary>MỘT TERMINAL RA — View vẽ chấm O đặc. Bọc ToolOutput.</summary>
public sealed class OutputTerminalViewModel
{
    public OutputTerminalViewModel(PipelineStepViewModel? step, ToolOutput output)
    {
        Step = step;
        Output = output;
    }

    /// <summary>null = terminal "Ảnh gốc" (không thuộc tool nào, đứng trên cùng).</summary>
    public PipelineStepViewModel? Step { get; }

    public ToolOutput Output { get; }

    public Type DataType => Output.ValueType;

    public string TypeName => PortTypes.FriendlyName(DataType);

    /// <summary>Nhãn khi được nối vào: "#2 Tìm contour › Số contour".</summary>
    public string Label => Step is null ? Output.Name : $"{Step.Title} › {Output.Name}";
}

/// <summary>Một yêu cầu nối dây từ View: kéo từ terminal A thả vào terminal B (chiều nào cũng được).</summary>
public sealed record TerminalPair(object From, object To);
