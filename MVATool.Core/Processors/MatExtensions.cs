using OpenCvSharp;

namespace MVATool.Core.Processors;

/// <summary>Helper dùng chung cho các processor (đây là extension method của C#).</summary>
public static class MatExtensions
{
    /// <summary>Trả về BẢN SAO thang xám của ảnh — ảnh gốc giữ nguyên.</summary>
    public static Mat ToGrayscale(this Mat mat)
    {
        if (mat.Channels() == 1)
            return mat.Clone();

        var gray = new Mat();
        Cv2.CvtColor(mat, gray,
            mat.Channels() == 4 ? ColorConversionCodes.BGRA2GRAY : ColorConversionCodes.BGR2GRAY);
        return gray;
    }
}
