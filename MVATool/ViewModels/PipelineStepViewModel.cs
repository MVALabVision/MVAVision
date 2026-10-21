using CommunityToolkit.Mvvm.ComponentModel;
using MVATool.Core;
using MVATool.Core.Parameters;
using MVATool.Core.Pipeline;

namespace MVATool.ViewModels;

/// <summary>
/// ViewModel cho MỘT TOOL trong job: bọc một PipelineNode và thêm trạng thái
/// chỉ-dành-cho-UI (số thứ tự, thời gian chạy, danh sách terminal để vẽ chấm O).
///
/// Đây là pattern "wrapper ViewModel": tool thuộc tầng Core không nên
/// biết gì về chuyện hiển thị — những thứ đó là việc của UI.
/// </summary>
public partial class PipelineStepViewModel : ObservableObject
{
    public PipelineNode Node { get; }

    public IVisionTool Tool => Node.Tool;

    public string Name => Tool.Name;

    /// <summary>Terminal vào (chấm O bên trái) — kèm Slider/CheckBox nếu nhập tay được.</summary>
    public IReadOnlyList<InputTerminalViewModel> Inputs { get; }

    /// <summary>Terminal ra (chấm O đặc) — hiện giá trị lần chạy gần nhất.</summary>
    public IReadOnlyList<OutputTerminalViewModel> Outputs { get; }

    /// <summary>Số thứ tự trong job (1, 2, 3...) — dùng trong nhãn nguồn "#2 Tìm contour › Số contour".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    private int _number;

    public string Title => $"#{Number} {Name}";

    /// <summary>Bỏ tick = tool này bị bỏ qua khi chạy job (không cần xóa).</summary>
    [ObservableProperty]
    private bool _isEnabled = true;

    /// <summary>Thời gian chạy lần gần nhất, MainWindowViewModel điền vào sau mỗi lần chạy.</summary>
    [ObservableProperty]
    private string _durationText = "";

    /// <summary>
    /// Gộp mọi thay đổi ảnh hưởng tới kết quả (bật/tắt, tham số, dây nối) thành MỘT event,
    /// để MainWindowViewModel biết đường tự chạy lại job (auto-run).
    /// </summary>
    public event EventHandler? SettingsChanged;

    public PipelineStepViewModel(IVisionTool tool)
        : this(new PipelineNode(tool))
    {
    }

    /// <summary>Bọc một node có sẵn (vd: vừa dựng lại từ file job) — giữ nguyên trạng thái bật/tắt.</summary>
    public PipelineStepViewModel(PipelineNode node)
    {
        Node = node;
        _isEnabled = node.IsEnabled;
        var tool = node.Tool;
        var mainImage = (tool as ImageProcessorTool)?.Input;

        Inputs = tool.Parameters
            .Select(p => new InputTerminalViewModel(this, p, isMainImage: p == mainImage))
            .ToList();
        Outputs = tool.Outputs.Select(o => new OutputTerminalViewModel(this, o)).ToList();

        foreach (var parameter in tool.Parameters)
        {
            parameter.PropertyChanged += (_, e) =>
            {
                // Đổi dây nối → chạy lại. Đổi Value khi ĐANG NỐI là do job chép
                // giá trị vào lúc chạy — nếu cũng chạy lại thì sẽ lặp vô tận.
                if (e.PropertyName == nameof(ProcessorParameter.Source)
                    || (e.PropertyName == "Value" && !parameter.IsLinked))
                {
                    SettingsChanged?.Invoke(this, EventArgs.Empty);
                }
            };
        }
    }

    // Hook do CommunityToolkit sinh ra: tự được gọi mỗi khi IsEnabled đổi.
    partial void OnIsEnabledChanged(bool value)
    {
        Node.IsEnabled = value;
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }
}
