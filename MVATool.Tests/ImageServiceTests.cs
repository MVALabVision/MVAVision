using System.IO;
using MVATool.Services;
using OpenCvSharp;
using Xunit;

namespace MVATool.Tests;

/// <summary>Test ImageService THẬT (đọc/ghi đĩa) — không dùng bản giả.</summary>
public class ImageServiceTests
{
    [Fact]
    public void LuuVaDocAnh_TrongThuMucCoDauTiengViet()
    {
        // Cv2.ImRead/ImWrite hỏng với đường dẫn kiểu này — ImageService phải chịu được.
        var folder = Path.Combine(Path.GetTempPath(), "MVATool Ảnh mẫu " + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(folder, "kết-quả.png");
        var service = new ImageService();

        try
        {
            using var original = new Mat(20, 30, MatType.CV_8UC3, new Scalar(10, 20, 30));
            service.Save(original, path);

            using var loaded = service.Load(path);
            Assert.Equal(30, loaded.Width);
            Assert.Equal(20, loaded.Height);
            Assert.Equal(new Vec3b(10, 20, 30), loaded.At<Vec3b>(5, 5));
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }
}
