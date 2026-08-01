namespace MVATool.Models;

/// <summary>
/// MODEL: một file ảnh trong thư mục đã mở.
/// Name tách sẵn để View bind thẳng, khỏi cần converter.
/// </summary>
public class ImageFileItem
{
    public string FullPath { get; }

    public string Name { get; }

    public ImageFileItem(string fullPath)
    {
        FullPath = fullPath;
        Name = Path.GetFileName(fullPath);
    }
}
