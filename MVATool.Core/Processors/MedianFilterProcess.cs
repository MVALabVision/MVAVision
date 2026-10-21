using MVATool.Core.Parameters;
using OpenCvSharp;

namespace MVATool.Core.Processors;

public class MedianFilterProcess : IImageProcessor
{
    // Min=1, Step=2 => Slider chỉ cho chọn 1, 3, 5, ... (kernel của OpenCV phải là số lẻ)
    private readonly NumberParameter _kernelSize = new("Kernel", min: 1, max: 51, value: 15, step: 2);

    public string Name => "Median Filter (làm mờ)";

    public IReadOnlyList<ProcessorParameter> Parameters { get; }

    public MedianFilterProcess()
    {
        Parameters = new ProcessorParameter[] { _kernelSize };
    }

    public Mat Process(Mat input)
    {
        // Phòng thủ: nếu vì lý do nào đó giá trị là số chẵn thì tự sửa thành lẻ.
        var kernel = (int)_kernelSize.Value;
        if (kernel % 2 == 0)
            kernel++;

        var output = new Mat();
        Cv2.MedianBlur(input, output, kernel);
        return output;
    }
}

