using Framework;
using System;
using System.IO;
using System.Text.Json;

namespace FlowEditor.Configuration;

public sealed class EditorSettings
{
    public NodeColorSettings NodeColors { get; set; } = new();

    public static EditorSettings Current { get; } = Load();

    public string GetNodeColor(FlowStepType stepType) => stepType == FlowStepType.Flow
        ? NodeColors.Flow
        : NodeColors.Action;

    private static EditorSettings Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(path)) return new EditorSettings();
        try
        {
            return JsonSerializer.Deserialize<EditorSettings>(File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new EditorSettings();
        }
        catch (JsonException)
        {
            return new EditorSettings();
        }
    }
}

public sealed class NodeColorSettings
{
    public string Action { get; set; } = "#409EFF";
    public string Flow { get; set; } = "#7C3AED";
}
