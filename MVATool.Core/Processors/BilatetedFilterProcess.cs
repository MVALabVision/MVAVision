using MVATool.Core.Parameters;
using OpenCvSharp;

namespace MVATool.Core.Processors;

public class BilateralFilterProcess : IImageProcessor
{
    // Min=1, Step=2 => Slider chỉ cho chọn 1, 3, 5, ... (kernel của OpenCV phải là số lẻ)
    private readonly NumberParameter _d = new("D", min: 1, max: 50, value: 9, step: 1);
    private readonly NumberParameter _sigmaColor = new("Sigma Color", min: 1, max: 255, value: 75, step: 1);
    private readonly NumberParameter _sigmaSpace = new("Sigma Space", min: 1, max: 500, value: 75, step: 1);

    public string Name => "Bilateral Filter (lọc song song)";

    public IReadOnlyList<ProcessorParameter> Parameters { get; }

    public BilateralFilterProcess()
    {
        Parameters = new ProcessorParameter[] { _d, _sigmaColor, _sigmaSpace };
    }

    public Mat Process(Mat input)
    {
        // Phòng thủ: nếu vì lý do nào đó giá trị là số chẵn thì tự sửa thành lẻ.
        var d = (int)_d.Value;
        var sigmaColor = (int)_sigmaColor.Value;
        var sigmaSpace = (int)_sigmaSpace.Value;

        var output = new Mat();
        Cv2.BilateralFilter(input, output, d, sigmaColor, sigmaSpace);
        return output;
    }
}
