using MVATool.Core.Parameters;
using OpenCvSharp;

namespace MVATool.Core;

/// <summary>
/// ĐIỂM MỞ RỘNG cho các thuật toán machine vision.
///
/// Muốn thêm một thuật toán mới, học viên chỉ cần:
///   1. Tạo class trong Processors/ implement interface này.
///   2. Khai báo tham số (nếu có) bằng NumberParameter / BoolParameter.
///   3. Đăng ký 1 dòng trong ProcessorCatalog.
/// Không cần sửa ViewModel hay View — UI tự sinh slider/checkbox cho tham số.
/// </summary>
public interface IImageProcessor
{
    /// <summary>Tên hiển thị trên UI.</summary>
    string Name { get; }

    /// <summary>Các tham số hiện lên UI. Trả về mảng rỗng nếu không có.</summary>
    IReadOnlyList<ProcessorParameter> Parameters { get; }

    /// <summary>
    /// Nhận ảnh vào, trả về ảnh MỚI đã xử lý.
    /// Không được sửa trực tiếp ảnh input (caller vẫn đang giữ nó).
    /// Lưu ý: hàm này được gọi trên background thread (Task.Run).
    /// </summary>
    Mat Process(Mat input);
}
