using MVATool.Core.Parameters;
using OpenCvSharp;

namespace MVATool.Core.Processors;

/// <summary>
/// KHỬ NHIỄU bằng Non-Local Means: mỗi pixel được thay bằng trung bình các mảng ảnh
/// GIỐNG NÓ trong vùng lân cận → khử nhiễu hạt mà vẫn giữ biên sắc nét
/// (khác Gaussian / Median chỉ trung bình theo khoảng cách).
///
/// Lưu ý: thuật toán này CHẬM hơn nhiều so với các bộ lọc khác — ảnh lớn có thể
/// mất vài trăm ms; tăng "Vùng tìm kiếm" sẽ càng chậm.
/// </summary>
public class DenoiseProcessor : IImageProcessor
{
    private readonly NumberParameter _strength = new("Độ mạnh (h)", min: 1, max: 30, value: 10);
    private readonly NumberParameter _templateSize = new("Cửa sổ mẫu", min: 3, max: 11, value: 7, step: 2);
    private readonly NumberParameter _searchSize = new("Vùng tìm kiếm", min: 7, max: 35, value: 21, step: 2);

    public string Name => "Khử nhiễu (Non-Local Means)";

    public IReadOnlyList<ProcessorParameter> Parameters { get; }

    public DenoiseProcessor()
    {
        Parameters = new ProcessorParameter[] { _strength, _templateSize, _searchSize };
    }

    public Mat Process(Mat input)
    {
        var h = (float)_strength.Value;
        var template = (int)_templateSize.Value;
        var search = (int)_searchSize.Value;

        var output = new Mat();
        if (input.Channels() == 1)
        {
            Cv2.FastNlMeansDenoising(input, output, h, template, search);
        }
        else
        {
            using var bgr = input.ToBgr();
            Cv2.FastNlMeansDenoisingColored(bgr, output, h, h, template, search);
        }
        return output;
    }
}
