using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace MVATool.Services;

/// <summary>
/// Implementation thật của IFilePickerService, dùng StorageProvider của Avalonia.
/// Đây là class DUY NHẤT trong tầng Services được phép đụng tới Window.
/// </summary>
public class FilePickerService : IFilePickerService
{
    private readonly Window _owner;

    public FilePickerService(Window owner)
    {
        _owner = owner;
    }

    public async Task<string?> PickImageFileAsync()
    {
        var files = await _owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Chọn ảnh",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Ảnh") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp" } },
            },
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickSaveFileAsync(string suggestedName)
    {
        var file = await _owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Lưu ảnh kết quả",
            SuggestedFileName = suggestedName,
            DefaultExtension = "png",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("PNG") { Patterns = new[] { "*.png" } },
                new FilePickerFileType("JPEG") { Patterns = new[] { "*.jpg" } },
            },
        });

        return file?.TryGetLocalPath();
    }

    public async Task<string?> PickFolderAsync()
    {
        var folders = await _owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Chọn thư mục ảnh",
            AllowMultiple = false,
        });

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }
}
