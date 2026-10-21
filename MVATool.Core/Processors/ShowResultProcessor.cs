using MVATool.Core.Parameters;
using OpenCvSharp;

namespace MVATool.Core.Processors;

/// <summary>
/// Ghi chữ OK (xanh) / NG (đỏ) to lên góc ảnh — bước cuối của job kiểm tra.
/// Nối "Đạt" ← terminal "Đạt" của tool So sánh.
/// </summary>
public class ShowResultProcessor : IImageProcessor
{
    private readonly BoolParameter _pass = new("Đạt", value: true);

    public string Name => "Hiển thị OK/NG";

    public IReadOnlyList<ProcessorParameter> Parameters { get; }

    public ShowResultProcessor()
    {
        Parameters = new ProcessorParameter[] { _pass };
    }

    public Mat Process(Mat input)
    {
        var output = input.ToBgr();
        var text = _pass.Value ? "OK" : "NG";
        var color = _pass.Value ? new Scalar(0, 200, 0) : new Scalar(0, 0, 255); // BGR!

        // Cỡ chữ tỉ lệ theo ảnh để ảnh nhỏ hay lớn đều đọc được.
        var scale = Math.Max(1.0, output.Height / 200.0);
        var thickness = (int)Math.Max(2, scale * 2);
        var size = Cv2.GetTextSize(text, HersheyFonts.HersheySimplex, scale, thickness, out var baseline);
        var origin = new Point(10, 10 + size.Height);

        Cv2.Rectangle(output, new Rect(0, 0, size.Width + 20, size.Height + baseline + 20), Scalar.All(0), -1);
        Cv2.PutText(output, text, origin, HersheyFonts.HersheySimplex, scale, color, thickness);
        return output;
    }
}
