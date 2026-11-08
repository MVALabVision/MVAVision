using MVATool.Core.Parameters;
using MVATool.Core.Pipeline;
using OpenCvSharp;

namespace MVATool.Core.Tools;

/// <summary>
/// NGUỒN ẢNH THỨ HAI của job (giống CogImageFileTool của VisionPro): đọc một file ảnh
/// cố định — ảnh mẫu cho Template matching, ảnh chuẩn để so sánh, mặt nạ vẽ sẵn...
///
/// Khác "Ảnh gốc": ảnh gốc đổi theo từng ảnh khi chạy cả thư mục, còn ảnh này
/// ĐỨNG YÊN (đường dẫn lưu trong file job).
///
/// Ảnh ra KHÔNG tự chảy vào tool kế tiếp (chuỗi mặc định vẫn là ảnh gốc) —
/// phải nối dây "Ảnh" → terminal ảnh phụ của tool cần dùng (vd "Ảnh mẫu").
/// </summary>
public class ImageFileTool : IVisionTool
{
    private readonly FileParameter _file = new("File ảnh");
    private readonly ChoiceParameter _mode = new("Màu", new[] { "Giữ nguyên", "Ảnh xám" });
    private readonly ToolOutput<Mat> _image = new("Ảnh");

    // Đọc file 1 lần rồi giữ trong bộ nhớ — chạy cả thư mục 500 ảnh không phải đọc lại 500 lần.
    // Nhận ra file đổi (chọn file khác / ghi đè) nhờ so đường dẫn + thời điểm sửa file.
    private Mat? _cache;
    private (string Path, DateTime Modified, string Mode) _cacheKey;

    public string Name => "Ảnh từ file (ImageFile)";

    public IReadOnlyList<ProcessorParameter> Parameters { get; }

    public IReadOnlyList<ToolOutput> Outputs { get; }

    public ImageFileTool()
    {
        Parameters = new ProcessorParameter[] { _file, _mode };
        Outputs = new ToolOutput[] { _image };
    }

    public void Run()
    {
        // Chưa chọn file = tool mới thêm, chưa cấu hình xong → job chỉ BỎ QUA tool này (kèm ⚠),
        // không làm hỏng cả job. Tool nào nối vào "Ảnh" cũng tự bị bỏ qua theo.
        if (!_file.HasValue)
            throw new ToolNotReadyException("Chưa chọn file ảnh — bấm … để chọn, hoặc 📷 để lưu ảnh gốc hiện tại.");

        var path = _file.Value;
        if (!File.Exists(path))
            throw new FileNotFoundException($"Không tìm thấy file: {path}");

        var key = (path, File.GetLastWriteTimeUtc(path), _mode.Value);
        if (_cache is null || key != _cacheKey)
        {
            _cache?.Dispose();
            _cache = null;
            _cache = Read(path, _mode.Value == "Ảnh xám" ? ImreadModes.Grayscale : ImreadModes.Color);
            _cacheKey = key;
        }

        // Job sẽ Dispose ảnh ra sau mỗi lần chạy → đưa BẢN SAO, giữ bản gốc trong cache.
        _image.Value = _cache.Clone();
    }

    /// <summary>Đọc bằng .NET rồi ImDecode — Cv2.ImRead hỏng với đường dẫn có dấu tiếng Việt.</summary>
    private static Mat Read(string path, ImreadModes mode)
    {
        var mat = Cv2.ImDecode(File.ReadAllBytes(path), mode);
        if (mat.Empty())
        {
            mat.Dispose();
            throw new InvalidOperationException($"Không đọc được ảnh: {path}");
        }
        return mat;
    }
}
