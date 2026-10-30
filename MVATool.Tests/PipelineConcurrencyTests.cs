using System.IO;
using System.Windows.Media.Imaging;
using MVATool.Core;
using MVATool.Core.Parameters;
using MVATool.Core.Pipeline;
using MVATool.Services;
using MVATool.ViewModels;
using OpenCvSharp;
using Xunit;

namespace MVATool.Tests;

/// <summary>
/// Tái hiện thao tác THẬT của người dùng với job nhiều ảnh: đổi ảnh liên tục
/// (bấm ▶, click danh sách, kéo thả) trong lúc job đang chạy dở.
/// </summary>
public class PipelineConcurrencyTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "MVATool Đồng thời " + Guid.NewGuid().ToString("N"));

    public PipelineConcurrencyTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    /// <summary>Ảnh "camera" to (1600×1200, có hoa văn) — để mỗi lần chạy job tốn đủ thời gian mà chồng lên nhau.</summary>
    private sealed class BigImageService : IImageService
    {
        public Mat Load(string path)
        {
            var mat = new Mat(1200, 1600, MatType.CV_8UC3, Scalar.All(128));
            Cv2.Randu(mat, Scalar.All(0), Scalar.All(255));
            return mat;
        }

        public void Save(Mat mat, string path) => File.WriteAllBytes(path, mat.ImEncode(".png"));

        public BitmapSource ToBitmap(Mat mat) => null!;

        public IReadOnlyList<string> ListImageFiles(string folder) => Array.Empty<string>();
    }

    private static T Param<T>(PipelineStepViewModel step, string name) where T : ProcessorParameter =>
        (T)step.Inputs.Single(i => i.Parameter.Name == name).Parameter;

    private async Task<MainWindowViewModel> TemplateJobAsync()
    {
        var viewModel = new MainWindowViewModel(new FakeFilePickerService(), new BigImageService(),
            new ProcessorCatalog(), new SystemStatusViewModel(new FakeSystemInfoService()))
        {
            IsAutoRunEnabled = false,
        };
        await viewModel.LoadImageAsync("a.png");

        foreach (var name in new[] { "Ảnh từ file", "Template matching" })
        {
            viewModel.SelectedProcessorInfo = viewModel.AvailableProcessors.First(p => p.Name.Contains(name));
            viewModel.AddStepCommand.Execute(null);
        }

        var template = Path.Combine(_folder, "mẫu.png");
        using (var patch = new Mat(40, 40, MatType.CV_8UC3))
        {
            Cv2.Randu(patch, Scalar.All(0), Scalar.All(255));
            File.WriteAllBytes(template, patch.ImEncode(".png"));
        }
        Param<FileParameter>(viewModel.Pipeline[0], "File ảnh").Value = template;
        viewModel.ConnectCommand.Execute(new TerminalPair(
            viewModel.Pipeline[0].Outputs.Single(), viewModel.Pipeline[1].Inputs.Single(i => i.Parameter.Name == "Ảnh mẫu")));

        viewModel.IsAutoRunEnabled = true; // giống app thật: auto-run bật
        return viewModel;
    }

    [Fact]
    public async Task DoiAnhLienTuc_KhiJobDangChay_KhongLoiThieuAnhMau()
    {
        var viewModel = await TemplateJobAsync();
        var errors = new List<string>();
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.StatusMessage) && viewModel.StatusMessage.Contains("Lỗi"))
                lock (errors) errors.Add(viewModel.StatusMessage);
        };

        // Người dùng bấm ▶ ▶ ▶ thật nhanh: lần load sau tới khi job của lần trước chưa xong.
        var loads = Enumerable.Range(0, 6).Select(i => viewModel.LoadImageAsync($"{i}.png")).ToList();
        await Task.WhenAll(loads);
        await WaitIdleAsync(viewModel);

        Assert.Empty(errors);
        Assert.False(viewModel.IsBusy);
    }

    private static async Task WaitIdleAsync(MainWindowViewModel viewModel)
    {
        for (var i = 0; i < 200 && viewModel.IsBusy; i++)
            await Task.Delay(25);
    }
}
