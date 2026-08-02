using MVATool.Core;
using MVATool.ViewModels;
using OpenCvSharp;
using Xunit;

namespace MVATool.Tests;

/// <summary>
/// Test các thao tác với ảnh gốc (cắt / xoay / lật / khôi phục / đọc pixel).
/// Ảnh giả trong FakeImageService là 16 × 12, mọi pixel có màu B=30 G=60 R=90.
/// </summary>
public class ImageEditingTests
{
    private static async Task<MainWindowViewModel> CreateViewModelWithImageAsync()
    {
        var viewModel = new MainWindowViewModel(
            new FakeFilePickerService(),
            new FakeImageService(),
            new ProcessorCatalog(),
            new SystemStatusViewModel(new FakeSystemInfoService()))
        {
            IsAutoRunEnabled = false,
        };
        await viewModel.LoadImageAsync(@"C:\anh\mau.png");
        return viewModel;
    }

    [Fact]
    public async Task CatAnh_CapNhatKichThuoc_VaTuTatCheDoCat()
    {
        var viewModel = await CreateViewModelWithImageAsync();
        viewModel.IsCropMode = true;

        viewModel.ApplyCropCommand.Execute(new Rect(2, 2, 8, 4));

        Assert.Contains("8 × 4", viewModel.ImageInfo);
        Assert.False(viewModel.IsCropMode); // cắt xong phải tự thoát chế độ cắt
    }

    [Fact]
    public async Task CatAnh_VungVuotRaNgoai_BiKepVaoTrongAnh()
    {
        var viewModel = await CreateViewModelWithImageAsync();

        // Vùng 100x100 tại (10,10) trên ảnh 16x12 → phải kẹp còn 6 × 2
        viewModel.ApplyCropCommand.Execute(new Rect(10, 10, 100, 100));

        Assert.Contains("6 × 2", viewModel.ImageInfo);
    }

    [Fact]
    public async Task XoayPhai90_DoiCho_ChieuRong_ChieuCao()
    {
        var viewModel = await CreateViewModelWithImageAsync();
        Assert.Contains("16 × 12", viewModel.ImageInfo);

        viewModel.RotateRightCommand.Execute(null);

        Assert.Contains("12 × 16", viewModel.ImageInfo);
    }

    [Fact]
    public async Task KhoiPhuc_TraVeAnhNhuLucMoFile()
    {
        var viewModel = await CreateViewModelWithImageAsync();

        viewModel.ApplyCropCommand.Execute(new Rect(0, 0, 5, 5));
        viewModel.RotateRightCommand.Execute(null);
        Assert.DoesNotContain("16 × 12", viewModel.ImageInfo);

        viewModel.RestoreOriginalCommand.Execute(null);

        Assert.Contains("16 × 12", viewModel.ImageInfo);
    }

    [Fact]
    public async Task HoverPixel_HienToaDo_VaMauRGB()
    {
        var viewModel = await CreateViewModelWithImageAsync();

        viewModel.HoverOriginalPixelCommand.Execute(new Point(3, 4));

        Assert.Contains("(3, 4)", viewModel.PixelInfo);
        // Scalar(30, 60, 90) theo thứ tự BGR của OpenCV → R=90 G=60 B=30
        Assert.Contains("R=90", viewModel.PixelInfo);
        Assert.Contains("B=30", viewModel.PixelInfo);
    }

    [Fact]
    public async Task HoverRaNgoaiAnh_XoaPixelInfo()
    {
        var viewModel = await CreateViewModelWithImageAsync();

        viewModel.HoverOriginalPixelCommand.Execute(new Point(3, 4));
        Assert.NotEqual("", viewModel.PixelInfo);

        viewModel.HoverOriginalPixelCommand.Execute(null);
        Assert.Equal("", viewModel.PixelInfo);
    }

    [Fact]
    public void ChuaMoAnh_CacNutThaoTacAnh_PhaiBiKhoa()
    {
        var viewModel = new MainWindowViewModel(
            new FakeFilePickerService(),
            new FakeImageService(),
            new ProcessorCatalog(),
            new SystemStatusViewModel(new FakeSystemInfoService()));

        Assert.False(viewModel.RotateLeftCommand.CanExecute(null));
        Assert.False(viewModel.FlipHorizontalCommand.CanExecute(null));
        Assert.False(viewModel.RestoreOriginalCommand.CanExecute(null));
    }
}
