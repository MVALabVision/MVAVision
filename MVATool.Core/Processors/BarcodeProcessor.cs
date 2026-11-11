using System.Runtime.InteropServices;
using MVATool.Core.Parameters;
using MVATool.Core.Pipeline;
using OpenCvSharp;
using ZXing;
using ZXing.Common;

namespace MVATool.Core.Processors;

/// <summary>
/// Đọc mã vạch / QR code bằng ZXing.Net.
/// Hỗ trợ: 1D (EAN, UPC, Code128, Code39...) và 2D (QR, DataMatrix, Aztec, PDF417).
/// Tự tiền xử lý ảnh (CLAHE, Otsu, Adaptive...) để tăng tỉ lệ đọc thành công.
/// </summary>
public class BarcodeProcessor : IImageProcessor
{
    private static readonly IReadOnlyList<string> Modes = new[]
    {
        "Tự động (1D + 2D)",
        "Chỉ 1D (Barcode)",
        "Chỉ 2D (QR/DataMatrix)",
    };

    private readonly ChoiceParameter _mode = new("Loại mã", Modes);
    private readonly BoolParameter _preprocess = new("Tiền xử lý (tăng nhận diện)", value: true);

    private readonly ToolOutput<string> _text = new("Nội dung");
    private readonly ToolOutput<string> _format = new("Định dạng");
    private readonly ToolOutput<bool> _found = new("Tìm thấy");
    private readonly ToolOutput<double> _count = new("Số mã");

    public string Name => "Đọc mã vạch / QR (Barcode)";

    public IReadOnlyList<ProcessorParameter> Parameters { get; }

    public IReadOnlyList<ToolOutput> Outputs { get; }

    public BarcodeProcessor()
    {
        Parameters = new ProcessorParameter[] { _mode, _preprocess };
        Outputs = new ToolOutput[] { _text, _format, _found, _count };
    }

    public Mat Process(Mat input)
    {
        var readers = CreateReaders();
        var allResults = new List<Result>();

        if (_preprocess.Value)
        {
            var variants = PreprocessImage(input);
            try
            {
                foreach (var variant in variants)
                {
                    foreach (var reader in readers)
                    {
                        try
                        {
                            var results = reader.DecodeMultiple(variant);
                            if (results is { Length: > 0 })
                            {
                                foreach (var r in results)
                                {
                                    if (!allResults.Any(existing => existing.Text == r.Text))
                                        allResults.Add(r);
                                }
                            }
                        }
                        catch { /* variant không đọc được → thử tiếp */ }
                    }
                    if (allResults.Count > 0 && !_mode.Value.StartsWith("Tự động"))
                        break;
                }
            }
            finally
            {
                foreach (var v in variants) v.Dispose();
            }
        }
        else
        {
            foreach (var reader in readers)
            {
                try
                {
                    var results = reader.DecodeMultiple(input);
                    if (results is { Length: > 0 })
                    {
                        foreach (var r in results)
                        {
                            if (!allResults.Any(existing => existing.Text == r.Text))
                                allResults.Add(r);
                        }
                    }
                }
                catch { }
            }
        }

        // Ghi đầu ra
        _found.Value = allResults.Count > 0;
        _count.Value = allResults.Count;
        _text.Value = allResults.Count > 0
            ? string.Join(" | ", allResults.Select(r => r.Text))
            : "";
        _format.Value = allResults.Count > 0
            ? string.Join(" | ", allResults.Select(r => r.BarcodeFormat.ToString()))
            : "";

        // Vẽ kết quả lên ảnh
        var output = input.ToBgr();
        var scale = Math.Max(0.4, output.Height / 800.0);
        var thickness = Math.Max(1, (int)(scale * 1.5));

        foreach (var result in allResults)
        {
            // Vẽ bounding box nếu có ResultPoints
            if (result.ResultPoints is { Length: >= 2 })
            {
                var points = result.ResultPoints
                    .Select(p => new Point((int)p.X, (int)p.Y))
                    .ToArray();

                if (points.Length >= 4)
                {
                    for (int i = 0; i < points.Length; i++)
                        Cv2.Line(output, points[i], points[(i + 1) % points.Length],
                                 new Scalar(0, 255, 0), thickness + 1);
                }
                else if (points.Length >= 2)
                {
                    Cv2.Rectangle(output,
                        new Rect(
                            Math.Min(points[0].X, points[1].X),
                            Math.Min(points[0].Y, points[1].Y),
                            Math.Abs(points[1].X - points[0].X),
                            Math.Abs(points[1].Y - points[0].Y)),
                        new Scalar(0, 255, 0), thickness + 1);
                }

                // Vẽ text cạnh vị trí mã
                var textPos = new Point(points[0].X, Math.Max(15, points[0].Y - 10));
                Cv2.PutText(output, $"[{result.BarcodeFormat}] {result.Text}",
                            textPos, HersheyFonts.HersheySimplex, scale,
                            new Scalar(0, 255, 0), thickness);
            }
        }

        // Nếu không tìm thấy, hiện thông báo
        if (allResults.Count == 0)
        {
            Cv2.PutText(output, "No barcode found", new Point(10, 30),
                        HersheyFonts.HersheySimplex, scale, new Scalar(0, 0, 255), thickness);
        }

        return output;
    }

    // ==================== HELPERS ====================

    private List<BarcodeReader<Mat>> CreateReaders()
    {
        var readers = new List<BarcodeReader<Mat>>();

        if (_mode.Value != "Chỉ 2D (QR/DataMatrix)")
        {
            readers.Add(new BarcodeReader<Mat>(ConvertMatToLuminanceSource)
            {
                AutoRotate = true,
                Options = new DecodingOptions
                {
                    PossibleFormats = new[]
                    {
                        BarcodeFormat.EAN_13, BarcodeFormat.EAN_8,
                        BarcodeFormat.UPC_A, BarcodeFormat.UPC_E,
                        BarcodeFormat.CODE_128, BarcodeFormat.CODE_39,
                        BarcodeFormat.CODE_93, BarcodeFormat.CODABAR,
                        BarcodeFormat.ITF,
                    },
                    TryHarder = true,
                    TryInverted = true,
                },
            });
        }

        if (_mode.Value != "Chỉ 1D (Barcode)")
        {
            readers.Add(new BarcodeReader<Mat>(ConvertMatToLuminanceSource)
            {
                AutoRotate = true,
                Options = new DecodingOptions
                {
                    PossibleFormats = new[]
                    {
                        BarcodeFormat.QR_CODE, BarcodeFormat.DATA_MATRIX,
                        BarcodeFormat.AZTEC, BarcodeFormat.PDF_417,
                    },
                    TryHarder = true,
                },
            });
        }

        return readers;
    }

    private static LuminanceSource ConvertMatToLuminanceSource(Mat mat)
    {
        // Chuyển sang BGR nếu ảnh xám — ZXing cần RGB/BGR 3 bytes/pixel
        using var bgr = (mat.Channels() == 1)
            ? mat.CvtColor(ColorConversionCodes.GRAY2BGR)
            : mat.Clone();

        int width = bgr.Width, height = bgr.Height;
        var rgbBytes = new byte[width * height * 3];

        for (int y = 0; y < height; y++)
        {
            Marshal.Copy(bgr.Ptr(y), rgbBytes, y * width * 3, width * 3);
        }

        return new RGBLuminanceSource(rgbBytes, width, height, RGBLuminanceSource.BitmapFormat.BGR24);
    }

    private static Mat[] PreprocessImage(Mat original)
    {
        var variants = new List<Mat>();

        // 1. Original
        variants.Add(original.Clone());

        // 2. Grayscale
        using var gray = (original.Channels() >= 3)
            ? original.CvtColor(ColorConversionCodes.BGR2GRAY)
            : original.Clone();
        variants.Add(gray.Clone());

        // 3. CLAHE
        var clahe = new Mat();
        using var claheFilter = Cv2.CreateCLAHE(2.0, new Size(8, 8));
        claheFilter.Apply(gray, clahe);
        variants.Add(clahe);

        // 4. Otsu threshold
        var binary = new Mat();
        Cv2.Threshold(gray, binary, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
        variants.Add(binary);

        // 5. Adaptive threshold
        var adaptive = new Mat();
        Cv2.AdaptiveThreshold(gray, adaptive, 255,
            AdaptiveThresholdTypes.GaussianC, ThresholdTypes.Binary, 11, 2);
        variants.Add(adaptive);

        // 6. Morphology close
        var morph = new Mat();
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(2, 2));
        Cv2.MorphologyEx(binary, morph, MorphTypes.Close, kernel);
        variants.Add(morph);

        // 7. Sharpen
        var sharpened = new Mat();
        using var blur = new Mat();
        Cv2.GaussianBlur(gray, blur, new Size(0, 0), 3);
        Cv2.AddWeighted(gray, 1.5, blur, -0.5, 0, sharpened);
        variants.Add(sharpened);

        // 8. Resize nếu ảnh nhỏ
        if (original.Width < 300)
        {
            var resized = new Mat();
            double scale = 300.0 / original.Width;
            Cv2.Resize(gray, resized, new Size(), scale, scale, InterpolationFlags.Cubic);
            variants.Add(resized);
        }

        return variants.ToArray();
    }
}
