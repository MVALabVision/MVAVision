using System.Windows.Media.Imaging;
using MVATool.Services;
using OpenCvSharp;

namespace MVATool.Tests;

/// <summary>
/// Các implementation GIẢ của service — đây chính là lý do ViewModel chỉ được
/// phụ thuộc vào INTERFACE: khi test, ta thay dialog thật / file thật bằng đồ giả,
/// và ViewModel không hề hay biết.
/// </summary>
public class FakeFilePickerService : IFilePickerService
{
    /// <summary>Giả lập người dùng chọn file này (null = bấm Cancel).</summary>
    public string? PathToReturn { get; set; }

    public Task<string?> PickImageFileAsync() => Task.FromResult(PathToReturn);

    public Task<string?> PickSaveFileAsync(string suggestedName) => Task.FromResult(PathToReturn);

    public Task<string?> PickFolderAsync() => Task.FromResult(PathToReturn);

    public Task<string?> PickJobOpenFileAsync() => Task.FromResult(PathToReturn);

    public Task<string?> PickJobSaveFileAsync(string suggestedName) => Task.FromResult(PathToReturn);
}

public class FakeImageService : IImageService
{
    public List<string> SavedPaths { get; } = new();

    // Không đọc file thật — trả về ảnh 16(rộng) × 12(cao) tự tạo.
    // Cố tình KHÔNG vuông để test xoay ảnh thấy được chiều đổi chỗ cho nhau.
    public Mat Load(string path) => new(12, 16, MatType.CV_8UC3, new Scalar(30, 60, 90));

    public void Save(Mat mat, string path) => SavedPaths.Add(path);

    // BitmapSource thật cần UI thread của WPF — unit test không có UI nên trả null.
    public BitmapSource ToBitmap(Mat mat) => null!;

    /// <summary>Giả lập nội dung thư mục — test tự điền danh sách file vào đây.</summary>
    public List<string> FolderFiles { get; } = new();

    public IReadOnlyList<string> ListImageFiles(string folder) => FolderFiles;
}

public class FakeSystemInfoService : ISystemInfoService
{
    /// <summary>Số liệu cố định — test nào cần giá trị khác thì gán đè.</summary>
    public SystemStatusSnapshot Snapshot { get; set; } = new(
        CpuPercent: 12.5,
        RamUsedGb: 8,
        RamTotalGb: 16,
        ProcessMemoryMb: 250,
        Drives: new[] { new DriveStatus("C:", FreeGb: 100, TotalGb: 500) });

    public SystemStatusSnapshot Sample() => Snapshot;
}
