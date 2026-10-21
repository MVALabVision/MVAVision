using MVATool.Core.Parameters;
using OpenCvSharp;

namespace MVATool.Core.Processors;

/// <summary>
/// CÂN BẰNG HISTOGRAM — kéo giãn độ tương phản cho ảnh quá tối / quá mờ.
///   - Toàn cục : Cv2.EqualizeHist, nhanh nhưng dễ làm cháy vùng sáng.
///   - CLAHE    : cân bằng TỪNG Ô nhỏ, có giới hạn khuếch đại (clip limit) → tự nhiên hơn,
///                hay dùng cho ảnh công nghiệp bị chiếu sáng không đều.
/// Ảnh màu: chỉ cân bằng kênh độ sáng (Y của YCrCb) để không làm lệch màu.
/// </summary>
public class HistogramEqualizeProcessor : IImageProcessor
{
    private readonly ChoiceParameter _method = new("Phương pháp", new[] { "CLAHE", "Toàn cục" });
    private readonly NumberParameter _clipLimit = new("Clip limit", min: 0.5, max: 10, value: 2, step: 0.5);
    private readonly NumberParameter _tiles = new("Số ô lưới", min: 2, max: 16, value: 8);

    public string Name => "Cân bằng histogram";

    public IReadOnlyList<ProcessorParameter> Parameters { get; }

    public HistogramEqualizeProcessor()
    {
        Parameters = new ProcessorParameter[] { _method, _clipLimit, _tiles };
    }

    public Mat Process(Mat input)
    {
        if (input.Channels() == 1)
            return Equalize(input);

        using var bgr = input.ToBgr();
        using var ycrcb = new Mat();
        Cv2.CvtColor(bgr, ycrcb, ColorConversionCodes.BGR2YCrCb);
        var channels = Cv2.Split(ycrcb);
        try
        {
            using (var y = channels[0])
                channels[0] = Equalize(y);

            using var merged = new Mat();
            Cv2.Merge(channels, merged);
            var output = new Mat();
            Cv2.CvtColor(merged, output, ColorConversionCodes.YCrCb2BGR);
            return output;
        }
        finally
        {
            foreach (var channel in channels)
                channel.Dispose();
        }
    }

    private Mat Equalize(Mat gray)
    {
        var output = new Mat();
        if (_method.Value == "Toàn cục")
        {
            Cv2.EqualizeHist(gray, output);
        }
        else
        {
            var tiles = (int)_tiles.Value;
            using var clahe = Cv2.CreateCLAHE(_clipLimit.Value, new Size(tiles, tiles));
            clahe.Apply(gray, output);
        }
        return output;
    }
}
