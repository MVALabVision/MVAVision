using CommunityToolkit.Mvvm.ComponentModel;

namespace MVATool.Core.Parameters;

/// <summary>
/// Tham số đường dẫn file — UI hiển thị TextBox + nút "Chọn file".
/// Dùng cho các thuật toán cần nạp ảnh mẫu từ file (ví dụ: Template Matching).
/// </summary>
public partial class FilePathParameter : ProcessorParameter
{
    public string Filter { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    private string _value = string.Empty;

    /// <summary>Tên file rút gọn để hiển thị.</summary>
    public string DisplayText => string.IsNullOrEmpty(Value)
        ? "(chưa chọn)"
        : System.IO.Path.GetFileName(Value);

    /// <param name="name">Tên tham số hiện trên UI.</param>
    /// <param name="filter">Bộ lọc cho dialog chọn file, ví dụ "Ảnh|*.bmp;*.png;*.jpg".</param>
    public FilePathParameter(string name, string filter = "Ảnh|*.bmp;*.png;*.jpg;*.jpeg;*.tif;*.tiff")
        : base(name)
    {
        Filter = filter;
    }
}
