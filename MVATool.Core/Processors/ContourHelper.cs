using MVATool.Core.Parameters;
using OpenCvSharp;

namespace MVATool.Core.Processors;

/// <summary>Helper dùng chung cho các tool phân tích contour.</summary>
public static class ContourHelper
{
    /// <summary>
    /// Contour NGOÀI CÙNG trên ảnh nhị phân (pixel khác 0 = vật thể).
    /// Ảnh xám/màu thì cũng coi mọi pixel khác 0 là vật thể → nên đặt sau Threshold.
    /// </summary>
    public static Point[][] FindExternal(Mat image, double minArea = 0)
    {
        using var gray = image.ToGrayscale();
        using var binary = new Mat();
        Cv2.Threshold(gray, binary, 0, 255, ThresholdTypes.Binary);
        Cv2.FindContours(binary, out var contours, out _,
            RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        return contours.Where(c => Cv2.ContourArea(c) >= minArea).ToArray();
    }

    /// <summary>Contour lấy qua dây nối nếu có; chưa nối thì tự tìm trên ảnh vào.</summary>
    public static Point[][] FromPortOrImage(InputPort<Point[][]> port, Mat image) =>
        port.Value ?? FindExternal(image);

    /// <summary>Chỉ số contour có diện tích lớn nhất, -1 nếu rỗng.</summary>
    public static int LargestIndex(Point[][] contours)
    {
        var best = -1;
        var bestArea = -1.0;
        for (var i = 0; i < contours.Length; i++)
        {
            var area = Cv2.ContourArea(contours[i]);
            if (area > bestArea)
            {
                bestArea = area;
                best = i;
            }
        }
        return best;
    }

    /// <summary>Đổi Point2f (số thực) sang Point (số nguyên) để vẽ.</summary>
    public static Point[] ToPoints(IEnumerable<Point2f> points) =>
        points.Select(p => new Point((int)Math.Round(p.X), (int)Math.Round(p.Y))).ToArray();
}
