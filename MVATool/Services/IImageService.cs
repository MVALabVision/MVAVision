using Avalonia.Media.Imaging;
using OpenCvSharp;

namespace MVATool.Services;

/// <summary>
/// SERVICE: cầu nối giữa dữ liệu ảnh OpenCV (Mat) và UI Avalonia (Bitmap).
/// ViewModel giữ ảnh dưới dạng Mat để xử lý, và chỉ convert sang Bitmap khi cần hiển thị.
/// </summary>
public interface IImageService
{
    /// <summary>Đọc ảnh từ file thành Mat (BGR). Ném exception nếu không đọc được.</summary>
    Mat Load(string path);

    /// <summary>Ghi Mat ra file (định dạng suy ra từ đuôi file). Tự tạo thư mục nếu chưa có.</summary>
    void Save(Mat mat, string path);

    /// <summary>Liệt kê các file ảnh trong một thư mục (png/jpg/jpeg/bmp), sắp theo tên.</summary>
    IReadOnlyList<string> ListImageFiles(string folder);

    /// <summary>Chuyển Mat thành Bitmap để control Image của Avalonia hiển thị.</summary>
    Bitmap ToBitmap(Mat mat);
}
