using MVATool.Core.Parameters;
using MVATool.Core.Pipeline;
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
///
/// NỐI THUẬT TOÁN (đầu vào ↔ đầu ra):
///   - Đầu vào: ảnh của Process(input) + mọi tham số trong Parameters.
///     Tham số nào cũng nối được vào đầu ra CÙNG KIỂU của bước đứng trước
///     (NumberParameter ← double, BoolParameter ← bool, ImageParameter ← ảnh).
///   - Đầu ra: ảnh trả về từ Process + các ToolOutput khai báo trong Outputs
///     (ghi .Value bên trong Process). Xem MeanBrightnessProcessor, MaskProcessor.
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

    /// <summary>
    /// Đầu ra PHỤ ngoài ảnh kết quả (số đo, cờ đạt/không đạt, ảnh phụ...).
    /// Mặc định không có — processor cũ không cần sửa gì.
    /// </summary>
    IReadOnlyList<ToolOutput> Outputs => Array.Empty<ToolOutput>();
}
