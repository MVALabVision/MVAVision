using MVATool.Core.Parameters;
using MVATool.Core.Pipeline;
using OpenCvSharp;

namespace MVATool.Core.Processors;

/// <summary>
/// So khớp mẫu (Template Matching): tìm vị trí của ảnh mẫu trong ảnh đầu vào.
/// Hỗ trợ 3 chế độ: Basic (1 kết quả), Multi-Match (nhiều kết quả), Rotation-Invariant.
/// Ảnh mẫu có thể chọn từ file (FilePathParameter) HOẶC nối từ bước trước (InputPort).
/// Nếu cả hai đều có, InputPort được ưu tiên.
/// </summary>
public class TemplateMatchingProcessor : IImageProcessor
{
    private readonly FilePathParameter _templatePath = new("Ảnh mẫu (file)");
    private readonly InputPort<Mat> _template = new("Ảnh mẫu (nối dây)");

    private readonly ChoiceParameter _mode = new("Chế độ", new[]
    {
        "Basic (1 kết quả)",
        "Multi-Match (nhiều kết quả)",
        "Rotation-Invariant",
    });

    private readonly ChoiceParameter _method = new("Phương pháp", new[]
    {
        "TM_CCOEFF_NORMED",
        "TM_CCORR_NORMED",
        "TM_SQDIFF_NORMED",
    });

    private readonly NumberParameter _threshold = new("Ngưỡng khớp", min: 0, max: 1, value: 0.8, step: 0.05);
    private readonly NumberParameter _angleStep = new("Bước góc (°)", min: 5, max: 90, value: 15, step: 5);

    private readonly ToolOutput<double> _score = new("Điểm khớp");
    private readonly ToolOutput<double> _matchCount = new("Số vị trí khớp");

    public string Name => "So khớp mẫu (TemplateMatching)";

    public IReadOnlyList<ProcessorParameter> Parameters { get; }

    public IReadOnlyList<ToolOutput> Outputs { get; }

    public TemplateMatchingProcessor()
    {
        Parameters = new ProcessorParameter[] { _templatePath, _template, _mode, _method, _threshold, _angleStep };
        Outputs = new ToolOutput[] { _score, _matchCount };
    }

    public Mat Process(Mat input)
    {
        // Ưu tiên ảnh mẫu từ dây nối, nếu không có thì đọc từ file
        Mat? tmpl = _template.Value;
        Mat? tmplFromFile = null;

        if (tmpl is null && !string.IsNullOrEmpty(_templatePath.Value))
        {
            tmplFromFile = Cv2.ImRead(_templatePath.Value, ImreadModes.Unchanged);
            if (tmplFromFile.Empty())
            {
                tmplFromFile.Dispose();
                throw new InvalidOperationException($"Không đọc được ảnh mẫu: {_templatePath.Value}");
            }
            tmpl = tmplFromFile;
        }

        if (tmpl is null)
            return input.Clone();

        try
        {
            if (tmpl.Width > input.Width || tmpl.Height > input.Height)
                throw new InvalidOperationException("Ảnh mẫu phải nhỏ hơn ảnh đầu vào.");

            var matchMethod = _method.Value switch
            {
                "TM_CCORR_NORMED"  => TemplateMatchModes.CCorrNormed,
                "TM_SQDIFF_NORMED" => TemplateMatchModes.SqDiffNormed,
                _                  => TemplateMatchModes.CCoeffNormed,
            };

            return _mode.Value switch
            {
                "Multi-Match (nhiều kết quả)" => FindMultipleMatches(input, tmpl, matchMethod),
                "Rotation-Invariant"          => RotationInvariantMatching(input, tmpl, matchMethod),
                _                             => BasicTemplateMatching(input, tmpl, matchMethod),
            };
        }
        finally
        {
            tmplFromFile?.Dispose();
        }
    }

    // ==================== 1. BASIC ====================

    private Mat BasicTemplateMatching(Mat source, Mat template, TemplateMatchModes method)
    {
        using var result = new Mat();
        Cv2.MatchTemplate(source, template, result, method);
        Cv2.MinMaxLoc(result, out double minVal, out double maxVal,
                      out Point minLoc, out Point maxLoc);

        bool isSqDiff = method == TemplateMatchModes.SqDiffNormed;
        var bestLoc = isSqDiff ? minLoc : maxLoc;
        var bestScore = isSqDiff ? (1.0 - minVal) : maxVal;

        _score.Value = Math.Round(bestScore, 4);
        _matchCount.Value = bestScore >= _threshold.Value ? 1 : 0;

        var display = source.Clone();
        EnsureBgr(display);

        if (bestScore >= _threshold.Value)
        {
            var bottomRight = new Point(bestLoc.X + template.Cols, bestLoc.Y + template.Rows);
            Cv2.Rectangle(display, bestLoc, bottomRight, new Scalar(0, 255, 0), 3);
            Cv2.PutText(display, $"Score: {bestScore:F4}",
                        new Point(bestLoc.X, bestLoc.Y - 10),
                        HersheyFonts.HersheySimplex, 0.7, new Scalar(0, 255, 0), 2);
        }

        return display;
    }

    // ==================== 2. MULTI-MATCH ====================

    private Mat FindMultipleMatches(Mat source, Mat template, TemplateMatchModes method)
    {
        bool isSqDiff = method == TemplateMatchModes.SqDiffNormed;

        using var result = new Mat();
        Cv2.MatchTemplate(source, template, result, method);

        var matches = new List<(Point location, double score)>();
        for (int y = 0; y < result.Rows; y++)
        {
            for (int x = 0; x < result.Cols; x++)
            {
                float value = result.At<float>(y, x);
                double score = isSqDiff ? (1.0 - value) : value;
                if (score >= _threshold.Value)
                    matches.Add((new Point(x, y), score));
            }
        }

        matches = NonMaximumSuppression(matches, template.Width, template.Height, 0.3);

        _score.Value = matches.Count > 0 ? Math.Round(matches[0].score, 4) : 0;
        _matchCount.Value = matches.Count;

        var display = source.Clone();
        EnsureBgr(display);

        int count = 0;
        foreach (var match in matches)
        {
            count++;
            var bottomRight = new Point(match.location.X + template.Cols,
                                        match.location.Y + template.Rows);
            Cv2.Rectangle(display, match.location, bottomRight, new Scalar(0, 255, 0), 2);
            Cv2.PutText(display, $"#{count}: {match.score:F3}",
                        new Point(match.location.X, match.location.Y - 5),
                        HersheyFonts.HersheySimplex, 0.5, new Scalar(0, 255, 0), 1);
        }

        return display;
    }

    // ==================== 3. ROTATION-INVARIANT ====================

    private Mat RotationInvariantMatching(Mat source, Mat template, TemplateMatchModes method)
    {
        bool isSqDiff = method == TemplateMatchModes.SqDiffNormed;
        var allMatches = new List<(Point location, double score, double angle, Size templateSize)>();

        for (double angle = 0; angle < 360; angle += _angleStep.Value)
        {
            using var rotatedTemplate = RotateImage(template, angle);

            if (rotatedTemplate.Width > source.Width || rotatedTemplate.Height > source.Height)
                continue;

            using var result = new Mat();
            Cv2.MatchTemplate(source, rotatedTemplate, result, method);

            for (int y = 0; y < result.Rows; y++)
            {
                for (int x = 0; x < result.Cols; x++)
                {
                    float value = result.At<float>(y, x);
                    double score = isSqDiff ? (1.0 - value) : value;
                    if (score >= _threshold.Value)
                    {
                        allMatches.Add((new Point(x, y), score, angle,
                                        new Size(rotatedTemplate.Width, rotatedTemplate.Height)));
                    }
                }
            }
        }

        var filteredMatches = NonMaximumSuppressionRotation(allMatches, 0.3);

        _score.Value = filteredMatches.Count > 0 ? Math.Round(filteredMatches[0].score, 4) : 0;
        _matchCount.Value = filteredMatches.Count;

        var display = source.Clone();
        EnsureBgr(display);

        int count = 0;
        foreach (var match in filteredMatches)
        {
            count++;
            var center = new Point(match.location.X + match.templateSize.Width / 2,
                                   match.location.Y + match.templateSize.Height / 2);

            DrawRotatedRectangle(display, center, match.templateSize, match.angle,
                                 new Scalar(0, 255, 0), 2);

            Cv2.PutText(display, $"#{count} A:{match.angle:F0}° S:{match.score:F3}",
                        new Point(match.location.X, match.location.Y - 5),
                        HersheyFonts.HersheySimplex, 0.5, new Scalar(0, 255, 0), 1);
        }

        return display;
    }

    // ==================== HELPER ====================

    private static void EnsureBgr(Mat image)
    {
        if (image.Channels() == 1)
            Cv2.CvtColor(image, image, ColorConversionCodes.GRAY2BGR);
    }

    private static List<(Point location, double score)> NonMaximumSuppression(
        List<(Point location, double score)> matches, int w, int h, double overlapThreshold)
    {
        if (matches.Count == 0) return matches;

        var sorted = matches.OrderByDescending(m => m.score).ToList();
        var filtered = new List<(Point, double)>();

        while (sorted.Count > 0)
        {
            var best = sorted[0];
            filtered.Add(best);
            sorted.RemoveAt(0);
            sorted.RemoveAll(m =>
                CalculateOverlap(best.location, m.location, w, h) / (w * h) > overlapThreshold);
        }

        return filtered;
    }

    private static List<(Point location, double score, double angle, Size templateSize)>
        NonMaximumSuppressionRotation(
            List<(Point location, double score, double angle, Size templateSize)> matches,
            double overlapThreshold)
    {
        if (matches.Count == 0) return matches;

        var sorted = matches.OrderByDescending(m => m.score).ToList();
        var filtered = new List<(Point, double, double, Size)>();

        while (sorted.Count > 0)
        {
            var best = sorted[0];
            filtered.Add(best);
            sorted.RemoveAt(0);
            sorted.RemoveAll(m =>
            {
                double overlap = CalculateOverlapDifferentSizes(
                    best.location, best.templateSize.Width, best.templateSize.Height,
                    m.location, m.templateSize.Width, m.templateSize.Height);
                double union = best.templateSize.Width * best.templateSize.Height +
                               m.templateSize.Width * m.templateSize.Height - overlap;
                return union > 0 && (overlap / union) > overlapThreshold;
            });
        }

        return filtered;
    }

    private static double CalculateOverlap(Point a, Point b, int w, int h)
    {
        int x1 = Math.Max(a.X, b.X), y1 = Math.Max(a.Y, b.Y);
        int x2 = Math.Min(a.X + w, b.X + w), y2 = Math.Min(a.Y + h, b.Y + h);
        return (x2 > x1 && y2 > y1) ? (double)(x2 - x1) * (y2 - y1) : 0;
    }

    private static double CalculateOverlapDifferentSizes(
        Point a, int w1, int h1, Point b, int w2, int h2)
    {
        int x1 = Math.Max(a.X, b.X), y1 = Math.Max(a.Y, b.Y);
        int x2 = Math.Min(a.X + w1, b.X + w2), y2 = Math.Min(a.Y + h1, b.Y + h2);
        return (x2 > x1 && y2 > y1) ? (double)(x2 - x1) * (y2 - y1) : 0;
    }

    private static Mat RotateImage(Mat image, double angle)
    {
        if (Math.Abs(angle % 90) < 1e-6)
            return RotateByQuadrant(image, angle);

        var center = new Point2f(image.Width / 2f, image.Height / 2f);
        using var rotMat = Cv2.GetRotationMatrix2D(center, angle, 1.0);

        var corners = new[]
        {
            new Point2f(0, 0),
            new Point2f(image.Width, 0),
            new Point2f(image.Width, image.Height),
            new Point2f(0, image.Height)
        };

        var rotated = new Point2f[4];
        for (int i = 0; i < 4; i++)
        {
            double x = rotMat.At<double>(0, 0) * corners[i].X + rotMat.At<double>(0, 1) * corners[i].Y + rotMat.At<double>(0, 2);
            double y = rotMat.At<double>(1, 0) * corners[i].X + rotMat.At<double>(1, 1) * corners[i].Y + rotMat.At<double>(1, 2);
            rotated[i] = new Point2f((float)x, (float)y);
        }

        float minX = rotated.Min(p => p.X), maxX = rotated.Max(p => p.X);
        float minY = rotated.Min(p => p.Y), maxY = rotated.Max(p => p.Y);
        int newW = (int)Math.Ceiling(maxX - minX);
        int newH = (int)Math.Ceiling(maxY - minY);

        rotMat.At<double>(0, 2) += -minX;
        rotMat.At<double>(1, 2) += -minY;

        var output = new Mat();
        Cv2.WarpAffine(image, output, rotMat, new Size(newW, newH),
                        InterpolationFlags.Linear, BorderTypes.Constant, new Scalar(0));
        return output;
    }

    private static Mat RotateByQuadrant(Mat image, double angle)
    {
        int norm = ((int)Math.Round(angle) % 360 + 360) % 360;
        return norm switch
        {
            90  => image.Transpose().Flip(FlipMode.X),
            180 => image.Flip(FlipMode.XY),
            270 => image.Transpose().Flip(FlipMode.Y),
            _   => image.Clone(),
        };
    }

    private static void DrawRotatedRectangle(Mat image, Point center, Size size,
                                              double angle, Scalar color, int thickness)
    {
        var rr = new RotatedRect(new Point2f(center.X, center.Y),
                                 new Size2f(size.Width, size.Height), (float)angle);
        var verts = rr.Points();
        for (int i = 0; i < 4; i++)
            Cv2.Line(image, (Point)verts[i], (Point)verts[(i + 1) % 4], color, thickness);
    }
}
