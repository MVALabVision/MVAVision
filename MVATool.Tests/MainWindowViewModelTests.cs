using MVATool.Core;
using MVATool.ViewModels;
using Xunit;

namespace MVATool.Tests;

/// <summary>
/// Test ViewModel KHÔNG CẦN mở cửa sổ nào — lợi ích lớn nhất của MVVM.
/// Toàn bộ hành vi (mở ảnh, quản lý pipeline, enable/disable nút)
/// kiểm chứng được bằng code thuần.
/// </summary>
public class MainWindowViewModelTests
{
    private static MainWindowViewModel CreateViewModel(FakeFilePickerService? picker = null)
    {
        var viewModel = new MainWindowViewModel(
            picker ?? new FakeFilePickerService(),
            new FakeImageService(),
            new ProcessorCatalog(),
            new SystemStatusViewModel(new FakeSystemInfoService()));

        // Tắt auto-run để test tự quyết định lúc nào pipeline chạy (deterministic).
        viewModel.IsAutoRunEnabled = false;
        return viewModel;
    }

    [Fact]
    public void ChuaMoAnh_NutChayPipeline_PhaiBiKhoa()
    {
        var viewModel = CreateViewModel();
        Assert.False(viewModel.RunPipelineCommand.CanExecute(null));
        Assert.False(viewModel.SaveResultCommand.CanExecute(null));
    }

    [Fact]
    public async Task MoAnh_MoKhoaNutChay_VaCapNhatImageInfo()
    {
        var viewModel = CreateViewModel();

        await viewModel.LoadImageAsync(@"C:\anh\mau.png");

        Assert.True(viewModel.RunPipelineCommand.CanExecute(null));
        Assert.Contains("mau.png", viewModel.ImageInfo);
    }

    [Fact]
    public async Task ChayPipeline_GhiLichSu_VaThoiGianTungBuoc()
    {
        var viewModel = CreateViewModel();
        await viewModel.LoadImageAsync(@"C:\anh\mau.png");

        viewModel.SelectedProcessorInfo =
            viewModel.AvailableProcessors.First(p => p.Name.Contains("Grayscale"));
        viewModel.AddStepCommand.Execute(null);

        await viewModel.RunPipelineCommand.ExecuteAsync(null);

        Assert.Single(viewModel.History);
        Assert.NotEqual("", viewModel.Pipeline[0].DurationText);
    }

    [Fact]
    public void ThemDiChuyenXoaBuoc_HoatDongDung()
    {
        var viewModel = CreateViewModel();

        viewModel.SelectedProcessorInfo = viewModel.AvailableProcessors[0];
        viewModel.AddStepCommand.Execute(null);
        viewModel.SelectedProcessorInfo = viewModel.AvailableProcessors[1];
        viewModel.AddStepCommand.Execute(null);
        Assert.Equal(2, viewModel.Pipeline.Count);

        var firstStep = viewModel.Pipeline[0];
        viewModel.MoveStepDownCommand.Execute(firstStep);
        Assert.Equal(firstStep, viewModel.Pipeline[1]);

        viewModel.RemoveStepCommand.Execute(firstStep);
        Assert.Single(viewModel.Pipeline);
        Assert.NotEqual(firstStep, viewModel.Pipeline[0]);
    }

    [Fact]
    public async Task LuuKetQua_GoiImageService_VoiDuongDanNguoiDungChon()
    {
        var picker = new FakeFilePickerService { PathToReturn = @"C:\anh\mau.png" };
        var imageService = new FakeImageService();
        var viewModel = new MainWindowViewModel(picker, imageService, new ProcessorCatalog(),
            new SystemStatusViewModel(new FakeSystemInfoService()))
        {
            IsAutoRunEnabled = false,
        };

        await viewModel.LoadImageAsync(@"C:\anh\mau.png");
        await viewModel.RunPipelineCommand.ExecuteAsync(null); // pipeline rỗng → kết quả = ảnh gốc

        picker.PathToReturn = @"C:\anh\ket-qua.png";
        await viewModel.SaveResultCommand.ExecuteAsync(null);

        Assert.Contains(@"C:\anh\ket-qua.png", imageService.SavedPaths);
    }
}
