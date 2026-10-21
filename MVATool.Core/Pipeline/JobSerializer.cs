using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using MVATool.Core.Parameters;

namespace MVATool.Core.Pipeline;

/// <summary>Kết quả mở file job: danh sách tool đã dựng lại + cảnh báo (tool lạ, tham số lạ...).</summary>
public sealed record JobLoadResult(IReadOnlyList<PipelineNode> Nodes, IReadOnlyList<string> Warnings);

/// <summary>
/// LƯU / MỞ JOB dạng JSON (đuôi .mvajob) — mở bằng Notepad cũng đọc được:
///
/// {
///   "app": "MVAVision", "version": 1,
///   "tools": [
///     { "type": "FindContoursProcessor", "name": "Tìm contour", "enabled": true,
///       "values": { "Diện tích min": 50 },
///       "links":  {} },
///     { "type": "CompareTool", ...,
///       "values": { "Phép so sánh": "=", "Ngưỡng": 3 },
///       "links":  { "Giá trị": { "tool": 0, "output": "Số contour" } } }
///   ]
/// }
///
/// - "type"  = Id trong ProcessorCatalog (tên class), KHÔNG phụ thuộc tên hiển thị.
/// - "links" : dây nối của terminal vào → (chỉ số tool nguồn, tên terminal ra).
///             tool = -1 nghĩa là "Ảnh gốc".
/// - Tham số khớp theo TÊN: thêm/bớt tham số ở phiên bản sau thì file cũ vẫn mở được,
///   chỗ nào không khớp chỉ sinh cảnh báo chứ không hỏng cả job.
/// </summary>
public static class JobSerializer
{
    public const string FileExtension = ".mvajob";
    private const int Version = 1;

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        // Giữ nguyên chữ tiếng Việt trong file thay vì ả...
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Save(IReadOnlyList<PipelineNode> nodes, ToolOutput source)
    {
        // Terminal ra → chỉ số tool sở hữu nó, để ghi dây nối dưới dạng (tool, tên terminal).
        var owners = new Dictionary<ToolOutput, int> { [source] = -1 };
        for (var i = 0; i < nodes.Count; i++)
        {
            foreach (var output in nodes[i].Outputs)
                owners[output] = i;
        }

        var tools = new JsonArray();
        foreach (var node in nodes)
        {
            var values = new JsonObject();
            var links = new JsonObject();
            foreach (var parameter in node.Inputs)
            {
                switch (parameter)
                {
                    case NumberParameter number:
                        values[parameter.Name] = number.Value;
                        break;
                    case BoolParameter flag:
                        values[parameter.Name] = flag.Value;
                        break;
                    case ChoiceParameter choice:
                        values[parameter.Name] = choice.Value;
                        break;
                }

                if (parameter.Source is { } linked && owners.TryGetValue(linked, out var owner))
                    links[parameter.Name] = new JsonObject { ["tool"] = owner, ["output"] = linked.Name };
            }

            tools.Add(new JsonObject
            {
                ["type"] = ProcessorCatalog.IdOf(node.Tool),
                ["name"] = node.Tool.Name, // chỉ để người đọc file dễ hiểu, khi mở không dùng
                ["enabled"] = node.IsEnabled,
                ["values"] = values,
                ["links"] = links,
            });
        }

        var root = new JsonObject
        {
            ["app"] = "MVAVision",
            ["version"] = Version,
            ["tools"] = tools,
        };
        return root.ToJsonString(WriteOptions);
    }

    /// <exception cref="InvalidDataException">File không phải job hợp lệ.</exception>
    public static JobLoadResult Load(string json, IProcessorCatalog catalog, ToolOutput source)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"File job hỏng: {ex.Message}", ex);
        }

        if (root?["tools"] is not JsonArray tools)
            throw new InvalidDataException("Không phải file job MVAVision (thiếu mục \"tools\").");

        var warnings = new List<string>();
        if ((int?)root["version"] > Version)
            warnings.Add($"File được lưu bởi phiên bản mới hơn (v{root["version"]}) — có thể thiếu tính năng.");

        // Bước 1: dựng tool + gán giá trị. Giữ chỗ null cho tool lạ để chỉ số dây nối không bị lệch.
        var nodes = new List<PipelineNode?>();
        for (var i = 0; i < tools.Count; i++)
        {
            var entry = tools[i];
            var type = (string?)entry?["type"] ?? "";
            var info = catalog.Available.FirstOrDefault(p => p.Id == type);
            if (info is null)
            {
                warnings.Add($"Tool #{i + 1}: không có tool \"{type}\" — bỏ qua.");
                nodes.Add(null);
                continue;
            }

            var node = new PipelineNode(info.Create()) { IsEnabled = (bool?)entry!["enabled"] ?? true };
            if (entry["values"] is JsonObject values)
            {
                foreach (var (name, value) in values)
                {
                    var parameter = node.Inputs.FirstOrDefault(p => p.Name == name);
                    if (parameter is null || value is null || !TryAssign(parameter, value))
                        warnings.Add($"Tool #{i + 1} {info.Name}: bỏ qua giá trị \"{name}\".");
                }
            }
            nodes.Add(node);
        }

        // Bước 2: nối dây (chỉ cho phép nối tới tool đứng TRƯỚC, như khi kéo tay).
        for (var i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is not { } node || tools[i]?["links"] is not JsonObject links)
                continue;

            foreach (var (name, link) in links)
            {
                var parameter = node.Inputs.FirstOrDefault(p => p.Name == name);
                var output = ResolveOutput(link, i, nodes, source);
                try
                {
                    if (parameter is null || output is null)
                        throw new ArgumentException("không tìm thấy terminal");
                    parameter.Source = output;
                }
                catch (ArgumentException)
                {
                    warnings.Add($"Tool #{i + 1} {node.Tool.Name}: không nối lại được dây vào \"{name}\".");
                }
            }
        }

        return new JobLoadResult(nodes.OfType<PipelineNode>().ToList(), warnings);
    }

    private static ToolOutput? ResolveOutput(JsonNode? link, int targetIndex, List<PipelineNode?> nodes, ToolOutput source)
    {
        var toolIndex = (int?)link?["tool"];
        var outputName = (string?)link?["output"];
        if (toolIndex == -1)
            return source;
        if (toolIndex is not { } index || index < 0 || index >= targetIndex || nodes[index] is not { } owner)
            return null;
        return owner.Outputs.FirstOrDefault(o => o.Name == outputName);
    }

    private static bool TryAssign(ProcessorParameter parameter, JsonNode value)
    {
        try
        {
            switch (parameter)
            {
                case NumberParameter number:
                    number.Value = value.GetValue<double>();
                    return true;
                case BoolParameter flag:
                    flag.Value = value.GetValue<bool>();
                    return true;
                case ChoiceParameter choice when choice.Options.Contains(value.GetValue<string>()):
                    choice.Value = value.GetValue<string>();
                    return true;
                default:
                    return false;
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException)
        {
            return false; // sai kiểu dữ liệu trong file
        }
    }
}
