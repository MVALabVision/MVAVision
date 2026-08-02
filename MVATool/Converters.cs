using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MVATool;

// WPF không có sẵn binding phủ định (!x) hay ObjectConverters như Avalonia,
// nên cần vài converter nhỏ. Tất cả chỉ dùng MỘT chiều (UI đọc trạng thái).

/// <summary>true → ẨN, false → HIỆN. Dùng cho "hiện nút Chạy khi KHÔNG batch".</summary>
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>null → HIỆN. Dùng cho dòng gợi ý "kéo thả ảnh vào đây" khi chưa có ảnh.</summary>
public class NullToVisibleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>0 → HIỆN. Dùng cho dòng gợi ý khi danh sách (pipeline, thư mục ảnh) còn rỗng.</summary>
public class ZeroToVisibleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
