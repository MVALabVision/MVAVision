using MVATool.Core.Parameters;
using MVATool.Core.Pipeline;

namespace MVATool.Core;

/// <summary>
/// MỘT TOOL trong job (giống CogTool của VisionPro): có các TERMINAL vào/ra
/// và tự chạy khi job tới lượt.
///
///   - Terminal vào = Parameters. Tham số nào nối được (LinkType != null) thì
///     nối vào một terminal ra CÙNG KIỂU của tool đứng trước. Job chép giá trị
///     vào tham số trước khi gọi Run(), nên trong Run() chỉ việc đọc .Value.
///   - Terminal ra = Outputs. Run() ghi kết quả vào .Value của từng ToolOutput.
///
/// Tool xử lý ảnh đơn giản (1 ảnh vào → 1 ảnh ra) thì viết IImageProcessor cho gọn —
/// ImageProcessorTool sẽ bọc nó lại và tự thêm terminal "Ảnh vào"/"Ảnh ra".
/// Tool KHÔNG xử lý ảnh (so sánh số, logic AND/OR...) thì implement thẳng interface này.
/// </summary>
public interface IVisionTool
{
    string Name { get; }

    IReadOnlyList<ProcessorParameter> Parameters { get; }

    IReadOnlyList<ToolOutput> Outputs { get; }

    /// <summary>Chạy trên background thread. Dữ liệu nhận qua terminal vào là của tool nguồn — chỉ đọc, không Dispose.</summary>
    void Run();
}
