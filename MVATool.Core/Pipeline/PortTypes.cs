using OpenCvSharp;

namespace MVATool.Core.Pipeline;

/// <summary>Tên thân thiện của các kiểu dữ liệu đi trên dây nối — dùng cho UI và thông báo lỗi.</summary>
public static class PortTypes
{
    public static string FriendlyName(Type? type) =>
        type is null ? "—"
        : type == typeof(Mat) ? "Ảnh"
        : type == typeof(double) ? "Số"
        : type == typeof(bool) ? "Đúng/Sai"
        : type == typeof(string) ? "Chữ"
        : type == typeof(Point[][]) ? "Contours"
        : type.Name;
}
