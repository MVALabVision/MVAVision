using System.Diagnostics;
using MVATool.Core.Parameters;
using OpenCvSharp;

namespace MVATool.Core.Pipeline;

/// <summary>
/// Kết quả 1 lần chạy. Durations[i] = null nghĩa là tool i không chạy (bị tắt / bị bỏ qua).
/// Warnings[i] != null nghĩa là tool i bị BỎ QUA vì chưa sẵn sàng (lý do để hiện lên UI).
/// </summary>
public sealed record PipelineRunResult(Mat Result, IReadOnlyList<double?> Durations, IReadOnlyList<string?> Warnings);

/// <summary>
/// Chạy job từ trên xuống (giống ToolBlock của VisionPro), truyền dữ liệu qua dây nối.
///
/// Với mỗi tool:
///   1. Kéo giá trị từ terminal ra đã nối sang terminal vào (PullFromSource).
///   2. "Ảnh vào" chưa nối → cấp ảnh của tool XỬ LÝ ẢNH đứng trước gần nhất
///      (hoặc ảnh gốc). Ảnh phụ từ tool nguồn (Ảnh từ file) chỉ đi theo dây nối.
///   3. Gọi tool.Run() — tool tự ghi các terminal ra.
///
/// Tool CHƯA SẴN SÀNG (dây nối tới terminal chưa có giá trị, hoặc tool ném
/// ToolNotReadyException) thì bị BỎ QUA kèm cảnh báo, job vẫn chạy tiếp —
/// tool nào phụ thuộc vào nó cũng tự bị bỏ qua theo. Lỗi thật thì dừng cả job.
///
/// Vì chỉ nối được tới tool ĐỨNG TRƯỚC, chạy từ trên xuống là đủ — không thể có vòng lặp.
///
/// Bộ nhớ: mọi ảnh trung gian được giữ tới hết lần chạy (tool sau còn có thể cần),
/// xong thì Dispose hết, chỉ trả về ảnh cuối cùng.
/// </summary>
public sealed class PipelineRunner
{
    /// <summary>Terminal "Ảnh gốc" — tool nào cũng nối được vào.</summary>
    public ToolOutput<Mat> Source { get; } = new("Ảnh gốc");

    /// <summary>
    /// Chạy job. Runner NHẬN QUYỀN SỞ HỮU <paramref name="source"/>:
    /// caller không được dùng/Dispose nó nữa (hãy truyền vào bản Clone).
    /// </summary>
    public PipelineRunResult Run(Mat source, IReadOnlyList<PipelineNode> nodes)
    {
        foreach (var node in nodes)
        {
            foreach (var output in node.Outputs)
                output.Reset();
        }

        // So sánh theo THAM CHIẾU: tool có thể ghi cùng 1 Mat vào 2 terminal.
        var produced = new HashSet<Mat>(ReferenceEqualityComparer.Instance) { source };
        var durations = new double?[nodes.Count];
        var warnings = new string?[nodes.Count];
        Mat? keep = null;

        try
        {
            Source.Value = source;
            var current = source;

            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (!node.IsEnabled)
                    continue;

                // Dây nối tới terminal CHƯA CÓ GIÁ TRỊ (tool nguồn tắt / bị bỏ qua / chưa chọn file)
                // → tool này chưa chạy được. Bỏ qua chứ không làm hỏng cả job.
                if (node.Inputs.FirstOrDefault(p => p.Source is { HasValue: false }) is { } missing)
                {
                    warnings[i] = $"Chưa có dữ liệu cho \"{missing.Name}\" — tool nguồn đang tắt hoặc chưa cấu hình xong "
                                  + "(vd Ảnh từ file chưa chọn file).";
                    continue;
                }

                try
                {
                    foreach (var parameter in node.Inputs)
                        parameter.PullFromSource();

                    if (node.Tool is ImageProcessorTool imageTool && !imageTool.Input.IsLinked)
                        imageTool.Input.Feed(current);

                    var stopwatch = Stopwatch.StartNew();
                    node.Tool.Run();
                    stopwatch.Stop();
                    durations[i] = stopwatch.Elapsed.TotalMilliseconds;
                }
                catch (ToolNotReadyException ex)
                {
                    warnings[i] = ex.Message;
                }
                catch (Exception ex)
                {
                    // Gắn số thứ tự tool vào thông báo lỗi — dễ tìm tool hỏng hơn nhiều.
                    throw new InvalidOperationException($"Tool #{i + 1} {node.Tool.Name}: {ex.Message}", ex);
                }
                finally
                {
                    // Gom mọi ảnh tool vừa tạo (kể cả khi lỗi giữa chừng) để Dispose sau.
                    foreach (var output in node.Outputs)
                    {
                        if (output.BoxedValue is Mat mat)
                            produced.Add(mat);
                    }
                }

                if (warnings[i] is not null)
                {
                    // Tool bỏ qua: xóa mọi đầu ra nó lỡ ghi dở (ảnh đã được gom để Dispose ở trên)
                    // để tool phía sau thấy "chưa có giá trị" mà bỏ qua theo, chuỗi ảnh giữ nguyên.
                    foreach (var output in node.Outputs)
                        output.Reset();
                    continue;
                }

                // Ảnh "hiện hành" của chuỗi = "Ảnh ra" của tool XỬ LÝ ẢNH. Tool nguồn ảnh phụ
                // (Ảnh từ file...) không chen vào chuỗi — nếu không, tool đứng sau ảnh mẫu
                // sẽ xử lý nhầm ảnh mẫu thay cho ảnh gốc. Muốn dùng ảnh phụ thì nối dây.
                if (node.Tool is ImageProcessorTool { Image.Value: { } image })
                    current = image;
            }

            keep = current;
            return new PipelineRunResult(current, durations, warnings);
        }
        finally
        {
            // Bỏ mọi tham chiếu tới ảnh trước khi Dispose (terminal số/chữ vẫn giữ để UI hiển thị).
            Source.Reset();
            foreach (var node in nodes)
            {
                foreach (var output in node.Outputs)
                {
                    if (output.ValueType == typeof(Mat))
                        output.Reset();
                }
                foreach (var parameter in node.Inputs)
                    (parameter as InputPort)?.Clear();
            }

            foreach (var mat in produced)
            {
                if (!ReferenceEquals(mat, keep))
                    mat.Dispose();
            }
        }
    }
}
