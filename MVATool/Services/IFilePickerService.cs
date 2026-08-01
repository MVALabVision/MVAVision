namespace MVATool.Services;

/// <summary>
/// SERVICE: ViewModel không được phép mở dialog trực tiếp (đó là việc của tầng View).
/// ViewModel chỉ gọi qua interface này — nhờ đó ViewModel không tham chiếu tới Window
/// và có thể unit test được (xem FakeFilePickerService trong MVATool.Tests).
/// </summary>
public interface IFilePickerService
{
    /// <summary>Mở hộp thoại chọn file ảnh. Trả về đường dẫn, hoặc null nếu người dùng bấm Cancel.</summary>
    Task<string?> PickImageFileAsync();

    /// <summary>Mở hộp thoại lưu file. Trả về đường dẫn, hoặc null nếu người dùng bấm Cancel.</summary>
    Task<string?> PickSaveFileAsync(string suggestedName);

    /// <summary>Mở hộp thoại chọn thư mục. Trả về đường dẫn, hoặc null nếu người dùng bấm Cancel.</summary>
    Task<string?> PickFolderAsync();
}
