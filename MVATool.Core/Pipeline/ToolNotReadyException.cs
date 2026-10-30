namespace MVATool.Core.Pipeline;

/// <summary>
/// Tool CHƯA SẴN SÀNG chạy vì người dùng chưa cấu hình xong (chưa nối "Ảnh mẫu",
/// chưa chọn file...) — khác với lỗi thật (file hỏng, ảnh sai kích thước...).
///
/// Job bắt riêng loại này: chỉ BỎ QUA tool đó kèm cảnh báo, các tool khác vẫn chạy
/// và ảnh kết quả vẫn cập nhật. Đang lắp job dở dang mà cứ kéo slider là báo lỗi
/// cả job thì rất khó chịu (VisionPro cũng chỉ đánh dấu tool đó, không dừng cả job).
/// </summary>
public class ToolNotReadyException : InvalidOperationException
{
    public ToolNotReadyException(string message)
        : base(message)
    {
    }
}
