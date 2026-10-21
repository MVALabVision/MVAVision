using System.IO;
using MVATool.Core;
using MVATool.Core.Parameters;
using MVATool.Core.Pipeline;
using MVATool.Core.Processors;
using MVATool.Core.Tools;
using MVATool.ViewModels;
using OpenCvSharp;
using Xunit;

namespace MVATool.Tests;

/// <summary>Test LƯU / MỞ job (.mvajob).</summary>
public class JobFileTests
{
    private static T Param<T>(PipelineNode node, string name) where T : ProcessorParameter =>
        (T)node.Inputs.Single(p => p.Name == name);

    /// <summary>Job mẫu: Threshold → Tìm contour → So sánh, có dây nối + tham số đã chỉnh.</summary>
    private static (List<PipelineNode> Nodes, PipelineRunner Runner) SampleJob()
    {
        var runner = new PipelineRunner();
        var threshold = new PipelineNode(new ThresholdProcessor());
        Param<NumberParameter>(threshold, "Ngưỡng").Value = 77;
        Param<BoolParameter>(threshold, "Otsu (tự tìm ngưỡng)").Value = true;

        var find = new PipelineNode(new FindContoursProcessor()) { IsEnabled = false };
        Param<InputPort<Mat>>(find, "Ảnh vào").Source = threshold.Outputs[0];

        var compare = new PipelineNode(new CompareTool());
        Param<ChoiceParameter>(compare, "Phép so sánh").Value = "=";
        Param<NumberParameter>(compare, "Giá trị").Source = find.Outputs.Single(o => o.Name == "Số contour");

        var mask = new PipelineNode(new MaskProcessor());
        Param<InputPort<Mat>>(mask, "Ảnh vào").Source = runner.Source;

        return (new List<PipelineNode> { threshold, find, compare, mask }, runner);
    }

    [Fact]
    public void LuuRoiMo_GiuNguyenToolThamSoBatTatVaDayNoi()
    {
        var (nodes, runner) = SampleJob();
        var json = JobSerializer.Save(nodes, runner.Source);

        var newRunner = new PipelineRunner();
        var loaded = JobSerializer.Load(json, new ProcessorCatalog(), newRunner.Source);

        Assert.Empty(loaded.Warnings);
        Assert.Equal(4, loaded.Nodes.Count);
        var (threshold, find, compare, mask) = (loaded.Nodes[0], loaded.Nodes[1], loaded.Nodes[2], loaded.Nodes[3]);

        Assert.IsType<ThresholdProcessor>(((ImageProcessorTool)threshold.Tool).Processor);
        Assert.Equal(77, Param<NumberParameter>(threshold, "Ngưỡng").Value);
        Assert.True(Param<BoolParameter>(threshold, "Otsu (tự tìm ngưỡng)").Value);
        Assert.False(find.IsEnabled);
        Assert.Equal("=", Param<ChoiceParameter>(compare, "Phép so sánh").Value);

        // Dây nối trỏ đúng vào terminal của tool MỚI dựng lại (không phải tool cũ)
        Assert.Same(threshold.Outputs[0], Param<InputPort<Mat>>(find, "Ảnh vào").Source);
        Assert.Same(find.Outputs.Single(o => o.Name == "Số contour"), Param<NumberParameter>(compare, "Giá trị").Source);
        Assert.Same(newRunner.Source, Param<InputPort<Mat>>(mask, "Ảnh vào").Source);
    }

    [Fact]
    public void FileGiuChuTiengVietDocDuoc()
    {
        var (nodes, runner) = SampleJob();
        var json = JobSerializer.Save(nodes, runner.Source);
        Assert.Contains("\"Ngưỡng\": 77", json);
        Assert.Contains("\"type\": \"FindContoursProcessor\"", json);
    }

    [Fact]
    public void ToolLa_ThamSoLa_ChiCanhBao_KhongHongCaJob()
    {
        const string json = """
        {
          "version": 1,
          "tools": [
            { "type": "ToolKhongTonTai", "values": {} },
            { "type": "ThresholdProcessor", "enabled": true,
              "values": { "Ngưỡng": 99, "Tham số đã bị xóa": 5 },
              "links": { "Ảnh vào": { "tool": 0, "output": "Ảnh ra" } } }
          ]
        }
        """;

        var loaded = JobSerializer.Load(json, new ProcessorCatalog(), new PipelineRunner().Source);

        var threshold = Assert.Single(loaded.Nodes);
        Assert.Equal(99, Param<NumberParameter>(threshold, "Ngưỡng").Value);
        Assert.Equal(3, loaded.Warnings.Count); // tool lạ, tham số lạ, dây nối tới tool lạ
    }

    [Fact]
    public void FileKhongPhaiJob_BaoLoi()
    {
        Assert.Throws<InvalidDataException>(() =>
            JobSerializer.Load("{ \"abc\": 1 }", new ProcessorCatalog(), new PipelineRunner().Source));
        Assert.Throws<InvalidDataException>(() =>
            JobSerializer.Load("không phải json", new ProcessorCatalog(), new PipelineRunner().Source));
    }

    [Fact]
    public void MoiToolTrongDanhMuc_LuuVaMoLaiDuoc()
    {
        var catalog = new ProcessorCatalog();
        var runner = new PipelineRunner();
        var nodes = catalog.Available.Select(p => new PipelineNode(p.Create())).ToList();

        var loaded = JobSerializer.Load(JobSerializer.Save(nodes, runner.Source), catalog, runner.Source);

        Assert.Empty(loaded.Warnings);
        Assert.Equal(nodes.Select(n => n.Tool.Name), loaded.Nodes.Select(n => n.Tool.Name));
    }

    [Fact]
    public async Task ViewModel_LuuRoiMoJob_DungThuTuVaDayNoi()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mva-test-{Guid.NewGuid():N}.mvajob");
        try
        {
            var picker = new FakeFilePickerService { PathToReturn = path };
            var viewModel = new MainWindowViewModel(picker, new FakeImageService(), new ProcessorCatalog(),
                new SystemStatusViewModel(new FakeSystemInfoService())) { IsAutoRunEnabled = false };

            foreach (var tool in new[] { "Tìm contour", "So sánh" })
            {
                viewModel.SelectedProcessorInfo = viewModel.AvailableProcessors.First(p => p.Name.Contains(tool));
                viewModel.AddStepCommand.Execute(null);
            }
            var count = viewModel.Pipeline[0].Outputs.Single(o => o.Output.Name == "Số contour");
            var value = viewModel.Pipeline[1].Inputs.Single(i => i.Parameter.Name == "Giá trị");
            viewModel.ConnectCommand.Execute(new TerminalPair(count, value));

            await viewModel.SaveJobCommand.ExecuteAsync(null);
            viewModel.ClearPipelineCommand.Execute(null);
            await viewModel.OpenJobCommand.ExecuteAsync(null);

            Assert.Equal(new[] { "#1 Tìm contour", "#2 So sánh (OK/NG)" }, viewModel.Pipeline.Select(s => s.Title));
            var reloaded = viewModel.Pipeline[1].Inputs.Single(i => i.Parameter.Name == "Giá trị");
            Assert.Equal("← #1 Tìm contour › Số contour", reloaded.HintText);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
