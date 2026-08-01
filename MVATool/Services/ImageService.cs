using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
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
    /// Convert bằng cách copy pixel TRỰC TIẾP vào WriteableBitmap (không encode
    /// PNG trung gian) — đủ nhanh để auto-run mượt khi kéo slider trên ảnh lớn.
    /// </summary>
    public unsafe Bitmap ToBitmap(Mat mat)
    {
        // Avalonia hiển thị pixel dạng BGRA 32-bit → đưa mọi ảnh về BGRA trước.
        Mat bgra;
        var ownsBgra = true;
        switch (mat.Channels())
        {
            case 1:
                bgra = new Mat();
                Cv2.CvtColor(mat, bgra, ColorConversionCodes.GRAY2BGRA);
                break;
            case 3:
                bgra = new Mat();
                Cv2.CvtColor(mat, bgra, ColorConversionCodes.BGR2BGRA);
                break;
            case 4:
                bgra = mat;
                ownsBgra = false; // không được dispose ảnh của caller
                break;
            default:
                throw new NotSupportedException($"Ảnh {mat.Channels()} kênh chưa được hỗ trợ.");
        }

        try
        {
            var bitmap = new WriteableBitmap(
                new PixelSize(bgra.Width, bgra.Height),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Opaque);

            using var buffer = bitmap.Lock();

            // Copy từng dòng vì stride (số byte mỗi dòng) của Mat và Bitmap có thể khác nhau.
            var srcStride = (int)bgra.Step();
            var rowBytes = bgra.Width * 4;
            var src = (byte*)bgra.Data;
            var dst = (byte*)buffer.Address;
            for (var y = 0; y < bgra.Height; y++)
            {
                Buffer.MemoryCopy(
                    src + (long)y * srcStride,
                    dst + (long)y * buffer.RowBytes,
                    buffer.RowBytes,
                    rowBytes);
            }

            return bitmap;
        }
        finally
        {
            if (ownsBgra)
                bgra.Dispose();
        }
    }
}
