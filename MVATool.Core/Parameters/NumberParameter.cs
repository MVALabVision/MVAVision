using CommunityToolkit.Mvvm.ComponentModel;

namespace MVATool.Core.Parameters;

/// <summary>
/// Tham số dạng số — UI hiển thị bằng Slider (xem DataTemplate trong MainWindow.xaml).
/// </summary>
public partial class NumberParameter : ProcessorParameter
{
    public double Min { get; }
    public double Max { get; }

    /// <summary>Bước nhảy của Slider. Đặt 2 với Min lẻ để chỉ cho chọn số lẻ (kernel OpenCV).</summary>
    public double Step { get; }

    // [NotifyPropertyChangedFor]: khi Value đổi thì DisplayText cũng phải được
    // báo là đã đổi, nếu không TextBlock hiển thị giá trị sẽ không cập nhật.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    private double _value;

    /// <summary>Chuỗi hiển thị cạnh Slider.</summary>
    public string DisplayText => Step >= 1 ? Value.ToString("F0") : Value.ToString("F2");

    public NumberParameter(string name, double min, double max, double value, double step = 1)
        : base(name)
    {
        Min = min;
        Max = max;
        Step = step;
        _value = value;
    }

    /// <summary>Nối được vào đầu ra kiểu số (vd: "Độ sáng TB" của bước trước làm ngưỡng).</summary>
    public override Type? LinkType => typeof(double);

    protected override void Accept(object value) => Value = (double)value;
}
