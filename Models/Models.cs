using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FlowEditor.Models
{
    public class FlowFile
    {
        [JsonPropertyName("flows")]
        public List<FlowDef> Flows { get; set; } = new();
    }

    public class FlowDef
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("nodes")] public List<NodeDef> Nodes { get; set; } = new();
        [JsonPropertyName("connections")] public List<ConnectionDef> Connections { get; set; } = new();
    }

    public class NodeDef
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("nodeName")] public string NodeName { get; set; } = "";
        [JsonPropertyName("exeName")] public string ExeName { get; set; } = "";
        [JsonPropertyName("nodeType")] public string NodeType { get; set; } = "";

        // inputParameters 可能是 "" 也可能是对象，用 object 原样保留
        [JsonPropertyName("inputParameters")] public object? InputParameters { get; set; }

        // 画布布局信息：加载旧文件没有时自动排版；不需要可把这两行改成 [JsonIgnore]
        [JsonPropertyName("x"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? X { get; set; }
        [JsonPropertyName("y"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? Y { get; set; }
    }

    public class ConnectionDef
    {
        [JsonPropertyName("fromNodeId")] public string FromNodeId { get; set; } = "";
        [JsonPropertyName("toNodeId")] public string ToNodeId { get; set; } = "";
        [JsonPropertyName("condition")] public string Condition { get; set; } = "";
    }
}