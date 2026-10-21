using System.Windows;
using Microsoft.Win32;

namespace MVATool.Services;

/// <summary>
/// Implementation thật của IFilePickerService, dùng dialog của WPF (Microsoft.Win32).
/// Đây là class DUY NHẤT trong tầng Services được phép đụng tới Window.
/// Dialog của WPF là API đồng bộ → bọc kết quả trong Task.FromResult để giữ
/// nguyên interface bất đồng bộ (ViewModel không cần biết khác biệt này).
/// </summary>
public class FilePickerService : IFilePickerService
{
    private readonly Window _owner;

    public FilePickerService(Window owner)
    {
        _owner = owner;
    }

    public Task<string?> PickImageFileAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Chọn ảnh",
            Filter = "Ảnh|*.png;*.jpg;*.jpeg;*.bmp|Tất cả file|*.*",
        };

        return Task.FromResult(dialog.ShowDialog(_owner) == true ? dialog.FileName : null);
    }

    public Task<string?> PickSaveFileAsync(string suggestedName)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Lưu ảnh kết quả",
            FileName = suggestedName,
            DefaultExt = ".png",
            Filter = "PNG|*.png|JPEG|*.jpg",
        };

        return Task.FromResult(dialog.ShowDialog(_owner) == true ? dialog.FileName : null);
    }

    public Task<string?> PickFolderAsync()
    {
        // OpenFolderDialog có từ .NET 8 — trước đó WPF phải mượn WinForms.
        var dialog = new OpenFolderDialog
        {
            Title = "Chọn thư mục ảnh",
        };

        return Task.FromResult(dialog.ShowDialog(_owner) == true ? dialog.FolderName : null);
    }

    private const string JobFilter = "Job MVAVision (*.mvajob)|*.mvajob|Tất cả file|*.*";

    public Task<string?> PickJobOpenFileAsync()
    {
        var dialog = new OpenFileDialog { Title = "Mở job", Filter = JobFilter };
        return Task.FromResult(dialog.ShowDialog(_owner) == true ? dialog.FileName : null);
    }

    public Task<string?> PickJobSaveFileAsync(string suggestedName)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Lưu job",
            FileName = suggestedName,
            DefaultExt = ".mvajob",
            Filter = JobFilter,
        };
        return Task.FromResult(dialog.ShowDialog(_owner) == true ? dialog.FileName : null);
    }
}
