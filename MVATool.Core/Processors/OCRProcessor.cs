using System.Diagnostics;
using System.IO;
using MVATool.Core.Parameters;
using MVATool.Core.Pipeline;
using OpenCvSharp;

namespace MVATool.Core.Processors;

/// <summary>
/// Nhận dạng ký tự (OCR) bằng Tesseract — gọi tesseract.exe qua command line.
/// Yêu cầu: cài Tesseract OCR (https://github.com/UB-Mannheim/tesseract/wiki).
/// Ảnh ra = ảnh gốc, đầu ra phụ "Văn bản" chứa kết quả OCR.
/// </summary>
public class OCRProcessor : IImageProcessor
{
    private static readonly IReadOnlyList<string> Languages = new[]
        { "eng", "vie", "eng+vie", "jpn", "kor", "chi_sim", "chi_tra", "fra", "deu" };

    private static readonly IReadOnlyList<string> PsmModes = new[]
    {
        "3 - Tự động (mặc định)",
        "4 - Giả định 1 cột văn bản",
        "6 - Giả định 1 khối văn bản",
        "7 - Một dòng văn bản",
        "8 - Một từ",
        "11 - Sparse text",
        "13 - Raw line",
    };

    private readonly ChoiceParameter _lang = new("Ngôn ngữ", Languages, "eng");
    private readonly ChoiceParameter _psm = new("Chế độ PSM", PsmModes, "3 - Tự động (mặc định)");
    private readonly BoolParameter _preprocess = new("Tiền xử lý (xám + nhị phân)", value: false);

    private readonly ToolOutput<string> _text = new("Văn bản");
    private readonly ToolOutput<double> _charCount = new("Số ký tự");

    public string Name => "Nhận dạng ký tự (OCR)";

    public IReadOnlyList<ProcessorParameter> Parameters { get; }

    public IReadOnlyList<ToolOutput> Outputs { get; }

    public OCRProcessor()
    {
        Parameters = new ProcessorParameter[] { _lang, _psm, _preprocess };
        Outputs = new ToolOutput[] { _text, _charCount };
    }

    public Mat Process(Mat input)
    {
        var tesseractPath = FindTesseract();
        if (string.IsNullOrEmpty(tesseractPath))
            throw new InvalidOperationException(
                "Không tìm thấy Tesseract OCR. Cài đặt từ: https://github.com/UB-Mannheim/tesseract/wiki");

        // Chuẩn bị ảnh tạm để Tesseract đọc
        Mat imageToOcr = input;
        Mat? preprocessed = null;

        if (_preprocess.Value)
        {
            var gray = input.ToGrayscale();
            preprocessed = new Mat();
            Cv2.Threshold(gray, preprocessed, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
            gray.Dispose();
            imageToOcr = preprocessed;
        }

        string tempImage = Path.Combine(Path.GetTempPath(), $"mva_ocr_{Guid.NewGuid():N}.png");
        string outputBase = Path.Combine(Path.GetTempPath(), $"mva_ocr_{Guid.NewGuid():N}");
        string outputFile = outputBase + ".txt";

        try
        {
            Cv2.ImWrite(tempImage, imageToOcr);

            var psm = _psm.Value.Split(' ')[0]; // lấy số đầu tiên: "3 - Tự động" → "3"
            var lang = _lang.Value;

            var startInfo = new ProcessStartInfo
            {
                FileName = tesseractPath,
                Arguments = $"\"{tempImage}\" \"{outputBase}\" -l {lang} --psm {psm}",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var proc = System.Diagnostics.Process.Start(startInfo)!;
            proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            if (proc.ExitCode != 0)
                throw new InvalidOperationException($"Tesseract thất bại (exit code {proc.ExitCode}).");

            var text = File.Exists(outputFile) ? File.ReadAllText(outputFile).Trim() : string.Empty;
            _text.Value = text;
            _charCount.Value = text.Length;

            // Vẽ kết quả OCR lên góc ảnh
            var output = input.ToBgr();
            if (!string.IsNullOrWhiteSpace(text))
            {
                var lines = text.Split('\n');
                var scale = Math.Max(0.4, output.Height / 800.0);
                var thickness = Math.Max(1, (int)(scale * 1.5));
                var y = 25;
                foreach (var line in lines.Take(10)) // hiển thị tối đa 10 dòng
                {
                    Cv2.PutText(output, line, new Point(10, y),
                                HersheyFonts.HersheySimplex, scale, new Scalar(0, 255, 0), thickness);
                    y += (int)(25 * scale + 10);
                }

                if (lines.Length > 10)
                {
                    Cv2.PutText(output, $"... (+{lines.Length - 10} dong)", new Point(10, y),
                                HersheyFonts.HersheySimplex, scale, new Scalar(0, 200, 255), thickness);
                }
            }

            return output;
        }
        finally
        {
            preprocessed?.Dispose();
            try { if (File.Exists(tempImage)) File.Delete(tempImage); } catch { }
            try { if (File.Exists(outputBase)) File.Delete(outputBase); } catch { }
            try { if (File.Exists(outputFile)) File.Delete(outputFile); } catch { }
        }
    }

    private static string? FindTesseract()
    {
        string[] paths =
        {
            @"C:\Program Files\Tesseract-OCR\tesseract.exe",
            @"C:\Program Files (x86)\Tesseract-OCR\tesseract.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                         @"Tesseract-OCR\tesseract.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                         @"Tesseract-OCR\tesseract.exe"),
        };

        foreach (var path in paths)
        {
            if (File.Exists(path))
                return path;
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
        {
            foreach (var dir in pathEnv.Split(';'))
            {
                var exe = Path.Combine(dir.Trim(), "tesseract.exe");
                if (File.Exists(exe))
                    return exe;
            }
        }

        return null;
    }
}
