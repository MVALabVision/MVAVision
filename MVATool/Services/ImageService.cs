using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenCvSharp;

namespace MVATool.Services;

public class ImageService : IImageService
{
    public Mat Load(string path)
    {
        var mat = Cv2.ImRead(path, ImreadModes.Color);
        if (mat.Empty())
            throw new InvalidOperationException($"Không đọc được ảnh: {path}");
        return mat;
    }

    public void Save(Mat mat, string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory); // không làm gì nếu đã tồn tại

        if (!Cv2.ImWrite(path, mat))
            throw new InvalidOperationException($"Không ghi được file: {path}");
    }

    private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".bmp" };

    public IReadOnlyList<string> ListImageFiles(string folder) =>
        Directory.EnumerateFiles(folder)
            .Where(f => ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// Convert bằng BitmapSource.Create — đọc thẳng buffer của Mat (không encode
    /// PNG trung gian) — đủ nhanh để auto-run mượt khi kéo slider trên ảnh lớn.
    /// Layout pixel của OpenCV (Gray8 / BGR24 / BGRA32) trùng khớp PixelFormats
    /// của WPF nên KHÔNG cần đổi kênh màu, copy nguyên khối là xong.
    /// </summary>
    public BitmapSource ToBitmap(Mat mat)
    {
        var format = mat.Channels() switch
        {
            1 => PixelFormats.Gray8,
            3 => PixelFormats.Bgr24,
            4 => PixelFormats.Bgra32,
            _ => throw new NotSupportedException($"Ảnh {mat.Channels()} kênh chưa được hỗ trợ."),
        };

        var stride = (int)mat.Step(); // số byte mỗi dòng (có thể > width*channels vì padding)
        var bitmap = BitmapSource.Create(
            mat.Width, mat.Height,
            96, 96,
            format,
            null,
            mat.Data,             // BitmapSource.Create COPY dữ liệu → Mat dispose sau đó vẫn an toàn
            stride * mat.Height,
            stride);

        // Freeze: bitmap thành bất biến — thread nào cũng đọc được, render nhanh hơn.
        bitmap.Freeze();
        return bitmap;
    }
}
