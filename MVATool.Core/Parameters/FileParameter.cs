using CommunityToolkit.Mvvm.ComponentModel;

namespace MVATool.Core.Parameters;

/// <summary>
/// Tham số ĐƯỜNG DẪN FILE — UI hiển thị ô đường dẫn + nút "…" chọn file.
/// Dùng cho tool cần dữ liệu ngoài ảnh gốc: ảnh mẫu (template), ảnh chuẩn (golden)...
/// Không nối dây được. Lưu trong file job dưới dạng chuỗi (tương đối theo thư mục job nếu được).
/// </summary>
public partial class FileParameter : ProcessorParameter
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    private string _value;

    public FileParameter(string name, string value = "")
        : base(name)
    {
        _value = value;
    }

    public bool HasValue => !string.IsNullOrWhiteSpace(Value);

    /// <summary>Chỉ tên file cho gọn (đường dẫn đầy đủ để trong tooltip).</summary>
    public string DisplayText => HasValue ? Path.GetFileName(Value) : "(chưa chọn file)";
}
