using System.Windows;
using MVATool.ViewModels;

namespace MVATool.Views;

/// <summary>
/// Code-behind của View. Trong MVVM file này gần như rỗng — NGOẠI LỆ hợp lệ
/// là những việc thuần UI mà XAML không tự làm được:
///   1. Kéo thả file: nhận sự kiện drag-drop rồi CHUYỂN NGAY cho ViewModel xử lý.
///   2. Đổi theme sáng/tối: thuần túy chuyện hiển thị, không phải business logic.
/// Quy tắc: code-behind được phép "dịch" sự kiện UI thành lời gọi ViewModel,
/// nhưng không bao giờ được chứa logic nghiệp vụ (xử lý ảnh, quản lý pipeline...).
/// </summary>
public partial class MainWindow : Window
{
    private static bool _isDarkTheme = true;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            await viewModel.LoadImageAsync(files[0]);
    }

    /// <summary>
    /// Đổi theme = thay ResourceDictionary ở vị trí [0] của App (xem App.xaml).
    /// Mọi {DynamicResource MvaXxx} trên UI tự đổi màu theo — WPF không có
    /// ThemeVariant như Avalonia nên phải tự hoán đổi dictionary.
    /// </summary>
    private void OnToggleThemeClick(object sender, RoutedEventArgs e)
    {
        _isDarkTheme = !_isDarkTheme;
        Application.Current.Resources.MergedDictionaries[0] = new ResourceDictionary
        {
            Source = new Uri($"Themes/{(_isDarkTheme ? "Dark" : "Light")}.xaml", UriKind.Relative),
        };
    }
}
