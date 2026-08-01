namespace MVATool.Services;

/// <summary>Tình trạng MỘT ổ đĩa tại thời điểm đo.</summary>
public record DriveStatus(string Name, double FreeGb, double TotalGb)
{
    public double UsedPercent => TotalGb <= 0 ? 0 : (TotalGb - FreeGb) / TotalGb * 100.0;
}

/// <summary>
/// Ảnh chụp tài nguyên hệ thống tại một thời điểm — thuần dữ liệu,
/// không biết gì về UI hay chuyện đo đạc diễn ra thế nào.
/// </summary>
public record SystemStatusSnapshot(
    double CpuPercent,
    double RamUsedGb,
    double RamTotalGb,
    double ProcessMemoryMb,
    IReadOnlyList<DriveStatus> Drives)
{
    public double RamPercent => RamTotalGb <= 0 ? 0 : RamUsedGb / RamTotalGb * 100.0;
}

/// <summary>
/// Đo tài nguyên máy (CPU / RAM / ổ đĩa). Là interface để ViewModel
/// test được mà không cần gọi API hệ điều hành thật.
/// </summary>
public interface ISystemInfoService
{
    /// <summary>Đo một lần. Gọi định kỳ (~1 giây/lần) để có số liệu "sống".</summary>
    SystemStatusSnapshot Sample();
}
