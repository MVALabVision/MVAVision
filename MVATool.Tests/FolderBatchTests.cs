using MVATool.Core;
using MVATool.ViewModels;
using Xunit;

namespace MVATool.Tests;

/// <summary>Test chế độ thư mục: nạp danh sách, điều hướng, chạy hàng loạt.</summary>
public class FolderBatchTests
{
    private static (MainWindowViewModel ViewModel, FakeFilePickerService Picker, FakeImageService Images) Create()
    {
        var picker = new FakeFilePickerService();
        var images = new FakeImageService();
        var viewModel = new MainWindowViewModel(picker, images, new ProcessorCatalog(),
            new SystemStatusViewModel(new FakeSystemInfoService()))
        {
            IsAutoRunEnabled = false,
        };
        return (viewModel, picker, images);
    }

    private static async Task OpenFolderWithFilesAsync(
        MainWindowViewModel viewModel, FakeFilePickerService picker, FakeImageService images, params string[] files)
    {
        picker.PathToReturn = @"C:\anh";
        images.FolderFiles.AddRange(files);
        await viewModel.OpenFolderCommand.ExecuteAsync(null);
    }

    [Fact]
    public async Task MoThuMuc_NapDanhSach_VaTuLoadAnhDauTien()
    {
        var (viewModel, picker, images) = Create();

        await OpenFolderWithFilesAsync(viewModel, picker, images,
            @"C:\anh\a.png", @"C:\anh\b.png", @"C:\anh\c.png");

        Assert.Equal(3, viewModel.ImageFiles.Count);
        Assert.Equal(@"C:\anh\a.png", viewModel.SelectedImageFile?.FullPath);
        Assert.Contains("a.png", viewModel.ImageInfo); // ảnh đầu đã được load thật
        Assert.Equal("1/3", viewModel.FolderPositionText);
        Assert.True(viewModel.RunFolderCommand.CanExecute(null));
    }

    [Fact]
    public async Task DieuHuong_TruocSau_DoiAnh_VaKhoaNutODauCuoi()
    {
        var (viewModel, picker, images) = Create();
        await OpenFolderWithFilesAsync(viewModel, picker, images,
            @"C:\anh\a.png", @"C:\anh\b.png", @"C:\anh\c.png");

        // Đang ở ảnh đầu → không lùi được nữa
        Assert.False(viewModel.PreviousImageCommand.CanExecute(null));
        Assert.True(viewModel.NextImageCommand.CanExecute(null));

        viewModel.NextImageCommand.Execute(null);
        Assert.Equal("2/3", viewModel.FolderPositionText);
        Assert.Contains("b.png", viewModel.ImageInfo); // chọn là load ngay

        viewModel.NextImageCommand.Execute(null);
        Assert.Equal("3/3", viewModel.FolderPositionText);
        Assert.False(viewModel.NextImageCommand.CanExecute(null)); // hết danh sách
    }

    [Fact]
    public async Task ChayCaThuMuc_XuLyDuMoiAnh_VaChiGhiMotDongTongKet()
    {
        var (viewModel, picker, images) = Create();
        await OpenFolderWithFilesAsync(viewModel, picker, images, @"C:\anh\a.png", @"C:\anh\b.png");

        viewModel.SelectedProcessorInfo =
            viewModel.AvailableProcessors.First(p => p.Name.Contains("Grayscale"));
        viewModel.AddStepCommand.Execute(null);

        await viewModel.RunFolderCommand.ExecuteAsync(null);

        // Không ghi từng ảnh — chỉ một dòng tổng kết
        var entry = Assert.Single(viewModel.History);
        Assert.Contains("2/2", entry.Operation);
        Assert.Contains("Xong cả thư mục", viewModel.StatusMessage);
        Assert.False(viewModel.IsBatchRunning);
    }

    [Fact]
    public async Task ChayCaThuMuc_BatLuuKetQua_GoiSaveChoTungAnh_VaoThuMucKetQua()
    {
        var (viewModel, picker, images) = Create();
        await OpenFolderWithFilesAsync(viewModel, picker, images, @"C:\anh\a.png", @"C:\anh\b.png");
        viewModel.IsBatchSaveEnabled = true;

        await viewModel.RunFolderCommand.ExecuteAsync(null); // pipeline rỗng → kết quả = ảnh gốc

        Assert.Equal(2, images.SavedPaths.Count);
        Assert.All(images.SavedPaths, p => Assert.Contains("ket-qua", p));
        Assert.Contains(images.SavedPaths, p => p.EndsWith("a.png"));
    }
}
