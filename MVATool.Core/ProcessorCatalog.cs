using MVATool.Core.Processors;

namespace MVATool.Core;

/// <summary>
/// Mô tả một thuật toán trong danh mục: tên + hàm factory tạo instance mới.
///
/// Vì sao cần factory (Func) thay vì giữ sẵn 1 instance?
/// Pipeline có thể chứa CÙNG một thuật toán 2 lần với tham số khác nhau
/// (ví dụ: blur nhẹ → threshold → blur mạnh), nên mỗi lần "Thêm vào pipeline"
/// phải tạo ra một instance MỚI với bộ tham số riêng.
/// </summary>
public sealed record ProcessorInfo(string Name, Func<IImageProcessor> Create);

public interface IProcessorCatalog
{
    IReadOnlyList<ProcessorInfo> Available { get; }
}

/// <summary>
/// Danh mục thuật toán — NƠI DUY NHẤT phải sửa khi thêm processor mới.
/// </summary>
public class ProcessorCatalog : IProcessorCatalog
{
    public IReadOnlyList<ProcessorInfo> Available { get; } = new[]
    {
        Describe(() => new GrayscaleProcessor()),
        Describe(() => new InvertProcessor()),
        Describe(() => new GaussianBlurProcessor()),
        Describe(() => new ThresholdProcessor()),
        Describe(() => new CannyEdgeProcessor()),
        Describe(() => new BlobCountProcessor()),
    };

    // Tạo tạm 1 instance để lấy Name — tránh phải gõ tên 2 lần (dễ lệch nhau).
    private static ProcessorInfo Describe(Func<IImageProcessor> create) => new(create().Name, create);
}
