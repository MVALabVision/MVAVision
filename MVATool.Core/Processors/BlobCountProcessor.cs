using MVATool.Core.Parameters;
using OpenCvSharp;

namespace MVATool.Core.Processors;

/// <summary>
/// BÀI TẬP: đếm số vật thể (blob) trong ảnh và vẽ contour lên ảnh kết quả.
/// Hiện tại processor này chưa làm gì cả (trả về bản sao ảnh gốc).
///
/// Gợi ý các bước:
///   1. Nhị phân hóa: dùng input.ToGrayscale() rồi Cv2.Threshold
///      (hoặc giả định người dùng đã đặt bước Threshold TRƯỚC bước này trong pipeline).
///   2. Tìm contour: Cv2.FindContours(binary, out var contours, out _,
///          RetrievalModes.External, ContourApproximationModes.ApproxSimple).
///   3. Lọc contour nhỏ: chỉ giữ contour có Cv2.ContourArea(c) >= _minArea.Value.
///   4. Vẽ lên BẢN SAO ảnh gốc:
///        - Cv2.DrawContours với màu đỏ (Scalar(0, 0, 255) — chú ý BGR!)
///        - Cv2.PutText ghi số blob đếm được lên góc ảnh.
///   5. Trả về ảnh đã vẽ.
/// </summary>
public class BlobCountProcessor : IImageProcessor
{
    private readonly NumberParameter _minArea = new("Diện tích min", min: 0, max: 5000, value: 100, step: 50);

    public string Name => "Đếm blob (bài tập)";

    public IReadOnlyList<ProcessorParameter> Parameters { get; }

    public BlobCountProcessor()
    {
        Parameters = new ProcessorParameter[] { _minArea };
    }

    public Mat Process(Mat input)
    {
        // TODO: học viên implement theo gợi ý ở trên.
        return input.Clone();
    }
}
