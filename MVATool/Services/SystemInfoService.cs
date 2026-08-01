using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MVATool.Services;

/// <summary>
/// Đo tài nguyên hệ thống bằng API của Windows (app này ship kèm
/// OpenCvSharp4.runtime.win nên Windows là môi trường chính):
///   - CPU toàn máy : GetSystemTimes — so sánh delta giữa 2 lần đo.
///   - RAM          : GlobalMemoryStatusEx — tổng / còn trống của máy.
///   - Ổ đĩa        : DriveInfo — chỉ lấy ổ cứng cố định, bỏ qua USB/CD.
/// Trên hệ khác vẫn chạy được nhờ nhánh fallback dùng API của .NET.
/// </summary>
public sealed class SystemInfoService : ISystemInfoService
{
    private const double BytesPerGb = 1024.0 * 1024.0 * 1024.0;

    private readonly Process _process = Process.GetCurrentProcess();

    // Số liệu lần đo trước — CPU % chỉ tính được từ HIỆU của 2 lần đo.
    private long _lastIdle, _lastKernel, _lastUser;
    private TimeSpan _lastProcessCpu;
    private DateTime _lastSampleAt = DateTime.MinValue;
    private double _lastCpuPercent;

    public SystemStatusSnapshot Sample()
    {
        var (ramUsed, ramTotal) = SampleRam();
        return new SystemStatusSnapshot(
            SampleCpu(),
            ramUsed,
            ramTotal,
            Environment.WorkingSet / 1024.0 / 1024.0,
            SampleDrives());
    }

    // ===== CPU =====

    private double SampleCpu()
    {
        if (OperatingSystem.IsWindows() && GetSystemTimes(out var idle, out var kernel, out var user))
        {
            // kernel time ĐÃ BAO GỒM idle time → busy = (kernel + user) - idle.
            var idleDelta = idle - _lastIdle;
            var totalDelta = (kernel - _lastKernel) + (user - _lastUser);
            var isFirstSample = _lastKernel == 0;
            (_lastIdle, _lastKernel, _lastUser) = (idle, kernel, user);

            if (!isFirstSample && totalDelta > 0)
                _lastCpuPercent = Math.Clamp((totalDelta - idleDelta) * 100.0 / totalDelta, 0, 100);
            return _lastCpuPercent;
        }

        // Fallback: CPU của riêng app, quy về % toàn máy.
        _process.Refresh();
        var now = DateTime.UtcNow;
        var cpuTime = _process.TotalProcessorTime;
        if (_lastSampleAt != DateTime.MinValue)
        {
            var wall = (now - _lastSampleAt).TotalMilliseconds * Environment.ProcessorCount;
            if (wall > 0)
                _lastCpuPercent = Math.Clamp((cpuTime - _lastProcessCpu).TotalMilliseconds * 100.0 / wall, 0, 100);
        }
        (_lastSampleAt, _lastProcessCpu) = (now, cpuTime);
        return _lastCpuPercent;
    }

    // ===== RAM =====

    private static (double UsedGb, double TotalGb) SampleRam()
    {
        if (OperatingSystem.IsWindows())
        {
            var status = new MemoryStatusEx { dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>() };
            if (GlobalMemoryStatusEx(ref status))
            {
                var total = status.ullTotalPhys / BytesPerGb;
                return (total - status.ullAvailPhys / BytesPerGb, total);
            }
        }

        // Fallback đa nền tảng: số liệu GC của .NET (kém chi tiết hơn chút).
        var info = GC.GetGCMemoryInfo();
        return (info.MemoryLoadBytes / BytesPerGb, info.TotalAvailableMemoryBytes / BytesPerGb);
    }

    // ===== Ổ đĩa =====

    private static IReadOnlyList<DriveStatus> SampleDrives()
    {
        var drives = new List<DriveStatus>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.DriveType == DriveType.Fixed && drive.IsReady)
                {
                    drives.Add(new DriveStatus(
                        drive.Name.TrimEnd('\\', '/'),
                        drive.AvailableFreeSpace / BytesPerGb,
                        drive.TotalSize / BytesPerGb));
                }
            }
            catch (IOException) { /* ổ đang bận / vừa rút ra — bỏ qua */ }
            catch (UnauthorizedAccessException) { /* không có quyền đọc — bỏ qua */ }
        }
        return drives;
    }

    // ===== P/Invoke (chỉ dùng trên Windows) =====

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out long idleTime, out long kernelTime, out long userTime);

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
}
