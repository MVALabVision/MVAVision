using CommunityToolkit.Mvvm.ComponentModel;
using MVATool.Core;
using MVATool.Core.Parameters;

namespace MVATool.ViewModels;

/// <summary>
/// ViewModel cho MỘT bước trong pipeline: bọc một IImageProcessor và thêm
/// trạng thái chỉ-dành-cho-UI (bật/tắt, thời gian chạy lần cuối).
///
/// Đây là pattern "wrapper ViewModel": Processor thuộc tầng Core không nên
/// biết gì về chuyện bật/tắt hay hiển thị — những thứ đó là việc của UI.
/// </summary>
public partial class PipelineStepViewModel : ObservableObject
{
    public IImageProcessor Processor { get; }

    public string Name => Processor.Name;

    public IReadOnlyList<ProcessorParameter> Parameters => Processor.Parameters;

    public bool HasParameters => Parameters.Count > 0;

    /// <summary>Bỏ tick = bước này bị bỏ qua khi chạy pipeline (không cần xóa).</summary>
    [ObservableProperty]
    private bool _isEnabled = true;

    /// <summary>Thời gian chạy lần gần nhất, MainWindowViewModel điền vào sau mỗi lần chạy.</summary>
    [ObservableProperty]
    private string _durationText = "";

    /// <summary>
    /// Gộp mọi thay đổi ảnh hưởng tới kết quả (bật/tắt + tham số) thành MỘT event,
    /// để MainWindowViewModel biết đường tự chạy lại pipeline (auto-run).
    /// </summary>
    public event EventHandler? SettingsChanged;

    public PipelineStepViewModel(IImageProcessor processor)
    {
        Processor = processor;

        // Lắng nghe tham số của processor. Chỉ quan tâm "Value" —
        // các property phụ như DisplayText đổi theo thì bỏ qua.
        foreach (var parameter in processor.Parameters)
        {
            parameter.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == "Value")
                    SettingsChanged?.Invoke(this, EventArgs.Empty);
            };
        }
    }

    // Hook do CommunityToolkit sinh ra: tự được gọi mỗi khi IsEnabled đổi.
    partial void OnIsEnabledChanged(bool value) => SettingsChanged?.Invoke(this, EventArgs.Empty);
}
