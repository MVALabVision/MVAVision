using MVATool.Core.Parameters;
using OpenCvSharp;

namespace MVATool.Core.Processors;

/// <summary>
/// VÍ DỤ MẪU có tham số: kernel size hiện lên UI thành Slider,
/// kéo slider là ảnh mờ hơn/nét hơn ngay (khi bật auto-run).
/// </summary>
public class DilatedProcessor : IImageProcessor
{
    // Min=1, Step=2 => Slider chỉ cho chọn 1, 3, 5, ... (kernel của OpenCV phải là số lẻ)
    private readonly NumberParameter _kernelSize = new("Kernel", min: 1, max: 51, value: 15, step: 2);
    private readonly NumberParameter _iterations = new("iterations (vòng lặp)", min: 1, max: 51, value: 1, step: 1);

    public string Name => "Giãn nở (Dilation)";

    public IReadOnlyList<ProcessorParameter> Parameters { get; }

    public DilatedProcessor()
    {
        Parameters = new ProcessorParameter[] { _kernelSize, _iterations };
    }

    public Mat Process(Mat input)
    {
        // Phòng thủ: nếu vì lý do nào đó giá trị là số chẵn thì tự sửa thành lẻ.
        var kernelSize = (int)_kernelSize.Value;
        if (kernelSize % 2 == 0)
            kernelSize++;
        var it = (int)_iterations.Value;
        Mat kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(kernelSize, kernelSize));
        Mat dilated = new Mat();
        Cv2.Dilate(input, dilated, kernel, iterations: it);
        return dilated;
    }
}