using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using MVATool.Services;

namespace MVATool.ViewModels;

/// <summary>
/// ViewModel "con" cho khối giám sát tài nguyên (CPU / RAM / ổ đĩa / đồng hồ).
/// MainWindowViewModel phơi nó ra qua property SystemStatus để View bind
/// kiểu {Binding SystemStatus.CpuText} — pattern child-viewmodel: tách riêng
/// một mảng chức năng để ViewModel chính không phình to.
///
/// Timer CHỈ chạy khi được gọi Start() — App gọi lúc khởi động; unit test
/// không gọi nên không cần UI thread, cứ thế test Refresh() trực tiếp.
/// </summary>
public partial class SystemStatusViewModel : ObservableObject
{
    private static readonly CultureInfo Vietnamese = new("vi-VN");

    private readonly ISystemInfoService _systemInfo;
    private DispatcherTimer? _timer;

    public SystemStatusViewModel(ISystemInfoService systemInfo)
    {
        _systemInfo = systemInfo;
    }

    // ===== Số liệu để View bind =====

    [ObservableProperty] private double _cpuPercent;
    [ObservableProperty] private string _cpuText = "—";
    [ObservableProperty] private bool _isCpuHigh;

    [ObservableProperty] private double _ramPercent;
    [ObservableProperty] private string _ramText = "—";
    [ObservableProperty] private bool _isRamHigh;

    /// <summary>RAM riêng của app (working set) — để ý xem có rò rỉ Mat không!</summary>
    [ObservableProperty] private string _processMemoryText = "—";

    [ObservableProperty] private string _clockText = "";
    [ObservableProperty] private string _dateText = "";

    /// <summary>Chuỗi gọn "CPU 12% · RAM 40%" cho thanh trạng thái.</summary>
    [ObservableProperty] private string _compactText = "";

    /// <summary>Mỗi ổ đĩa một dòng. Item được CẬP NHẬT tại chỗ (không tạo mới) để UI khỏi nháy.</summary>
    public ObservableCollection<DriveStatusViewModel> Drives { get; } = new();

    /// <summary>Bắt đầu đo định kỳ 1 giây/lần trên UI thread. Gọi nhiều lần vô hại.</summary>
    public void Start()
    {
        if (_timer is not null)
            return;

        Refresh();
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
    }

    /// <summary>Đo một lần rồi đổ số liệu vào các property. Public để test gọi thẳng.</summary>
    public void Refresh()
    {
        SystemStatusSnapshot snapshot;
        try
        {
            snapshot = _systemInfo.Sample();
        }
        catch
        {
            return; // đo lỗi thì giữ nguyên số cũ — không để monitor làm sập app
        }

        CpuPercent = snapshot.CpuPercent;
        CpuText = $"{snapshot.CpuPercent:F0}%";
        IsCpuHigh = snapshot.CpuPercent >= 90;

        RamPercent = snapshot.RamPercent;
        RamText = $"{snapshot.RamUsedGb:F1}/{snapshot.RamTotalGb:F1} GB";
        IsRamHigh = snapshot.RamPercent >= 90;

        ProcessMemoryText = $"{snapshot.ProcessMemoryMb:F0} MB";
        CompactText = $"CPU {snapshot.CpuPercent:F0}%  ·  RAM {snapshot.RamPercent:F0}%";

        var now = DateTime.Now;
        ClockText = now.ToString("HH:mm:ss");
        DateText = Vietnamese.TextInfo.ToTitleCase(now.ToString("dddd, dd/MM/yyyy", Vietnamese));

        SyncDrives(snapshot.Drives);
    }

    private void SyncDrives(IReadOnlyList<DriveStatus> drives)
    {
        foreach (var drive in drives)
        {
            var row = Drives.FirstOrDefault(d => d.Name == drive.Name);
            if (row is null)
                Drives.Add(row = new DriveStatusViewModel(drive.Name));
            row.Update(drive);
        }

        for (var i = Drives.Count - 1; i >= 0; i--)
        {
            if (drives.All(d => d.Name != Drives[i].Name))
                Drives.RemoveAt(i);
        }
    }
}

/// <summary>Một dòng ổ đĩa trong khối giám sát: "C: [▓▓▓░░] 121 GB".</summary>
public partial class DriveStatusViewModel : ObservableObject
{
    public string Name { get; }

    [ObservableProperty] private double _usedPercent;
    [ObservableProperty] private string _freeText = "";
    [ObservableProperty] private string _detailText = "";
    [ObservableProperty] private bool _isAlmostFull;

    public DriveStatusViewModel(string name) => Name = name;

    public void Update(DriveStatus status)
    {
        UsedPercent = status.UsedPercent;
        FreeText = $"{status.FreeGb:F0} GB";
        DetailText = $"Ổ {status.Name} — còn trống {status.FreeGb:F1} GB / {status.TotalGb:F1} GB ({100 - status.UsedPercent:F0}%)";
        IsAlmostFull = status.UsedPercent >= 90;
    }
}
