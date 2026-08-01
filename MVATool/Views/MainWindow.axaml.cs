using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
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
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains(DataFormats.Files)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        var path = e.Data.GetFiles()?.FirstOrDefault()?.TryGetLocalPath();
        if (path is not null)
            await viewModel.LoadImageAsync(path);
    }

    private void OnToggleThemeClick(object? sender, RoutedEventArgs e)
    {
        var app = Application.Current!;
        app.RequestedThemeVariant = app.ActualThemeVariant == ThemeVariant.Dark
            ? ThemeVariant.Light
            : ThemeVariant.Dark;
    }
}
