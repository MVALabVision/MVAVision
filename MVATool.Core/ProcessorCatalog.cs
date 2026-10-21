using MVATool.Core.Pipeline;
using MVATool.Core.Processors;
using MVATool.Core.Tools;

namespace MVATool.Core;

/// <summary>
/// Mô tả một tool trong danh mục: Id + tên hiển thị + hàm factory tạo instance mới.
///
/// Vì sao cần factory (Func) thay vì giữ sẵn 1 instance?
/// Job có thể chứa CÙNG một tool 2 lần với tham số khác nhau
/// (ví dụ: blur nhẹ → threshold → blur mạnh), nên mỗi lần "Thêm vào job"
/// phải tạo ra một instance MỚI với bộ tham số riêng.
///
/// Id = tên class (vd "ThresholdProcessor") — dùng khi LƯU/MỞ file job.
/// Không dùng Name vì tên hiển thị tiếng Việt có thể sửa bất cứ lúc nào.
/// </summary>
public sealed record ProcessorInfo(string Id, string Name, Func<IVisionTool> Create);

public interface IProcessorCatalog
{
    IReadOnlyList<ProcessorInfo> Available { get; }
}

/// <summary>
/// Danh mục tool — NƠI DUY NHẤT phải sửa khi thêm tool mới.
/// </summary>
public class ProcessorCatalog : IProcessorCatalog
{
    public IReadOnlyList<ProcessorInfo> Available { get; } = new[]
    {
        // --- Tiền xử lý: màu, lọc, khử nhiễu, tăng nét ---
        Describe(() => new GrayscaleProcessor()),
        Describe(() => new InvertProcessor()),
        Describe(() => new GaussianBlurProcessor()),
        Describe(() => new MedianFilterProcess()),
        Describe(() => new BilateralFilterProcess()),
        Describe(() => new DenoiseProcessor()),
        Describe(() => new SharpenProcessor()),
        Describe(() => new HistogramEqualizeProcessor()),

        // --- Hình học: kích thước, xoay ---
        Describe(() => new ResizeProcessor()),
        Describe(() => new RotateProcessor()),

        // --- Phân đoạn & biên ---
        Describe(() => new ThresholdProcessor()),
        Describe(() => new MorphologyProcessor()),
        Describe(() => new SobelProcessor()),
        Describe(() => new LaplacianProcessor()),
        Describe(() => new CannyEdgeProcessor()),
        Describe(() => new MaskProcessor()),

        // --- Contour & đo lường (có terminal ra phụ: số, contour...) ---
        Describe(() => new FindContoursProcessor()),
        Describe(() => new ApproxContoursProcessor()),
        Describe(() => new BoundingBoxProcessor()),
        Describe(() => new ConvexHullProcessor()),
        Describe(() => new DrawContoursProcessor()),
        Describe(() => new MeanBrightnessProcessor()),
        Describe(() => new BlobCountProcessor()),

        // --- Phán định (IVisionTool: không cần ảnh) ---
        Describe(() => new CompareTool()),
        Describe(() => new LogicTool()),
        Describe(() => new ShowResultProcessor()),
    };

    /// <summary>Tìm tool theo Id (dùng khi mở file job). null nếu không có.</summary>
    public ProcessorInfo? Find(string id) => Available.FirstOrDefault(p => p.Id == id);

    /// <summary>Id của một instance tool — processor ảnh thì lấy class bên trong lớp bọc.</summary>
    public static string IdOf(IVisionTool tool) =>
        tool is ImageProcessorTool wrapped ? wrapped.Processor.GetType().Name : tool.GetType().Name;

    // Tạo tạm 1 instance để lấy Name — tránh phải gõ tên 2 lần (dễ lệch nhau).
    private static ProcessorInfo Describe(Func<IVisionTool> create)
    {
        var sample = create();
        return new(IdOf(sample), sample.Name, create);
    }

    // Processor ảnh được bọc thành tool (thêm terminal "Ảnh vào"/"Ảnh ra").
    private static ProcessorInfo Describe(Func<IImageProcessor> create) =>
        Describe(() => (IVisionTool)new ImageProcessorTool(create()));
}
