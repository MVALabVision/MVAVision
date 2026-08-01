namespace MVATool.Models;

/// <summary>
/// MODEL: chỉ chứa dữ liệu thuần, không biết gì về UI hay OpenCV.
/// Một dòng trong "Lịch sử xử lý" hiển thị bên phải màn hình.
/// </summary>
public class LogEntry
{
    public DateTime Time { get; init; } = DateTime.Now;

    public string Operation { get; init; } = "";

    public double DurationMs { get; init; }

    // Property tính toán sẵn để View bind trực tiếp, khỏi cần StringFormat.
    public string TimeText => Time.ToString("HH:mm:ss");

    public string DurationText => $"{DurationMs:F1} ms";
}
