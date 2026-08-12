using System.Text.Json;
using System.Text.Json.Serialization;
using Condor.Core.Models;

namespace Condor.Core.Serialization;

public static class ContextJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize(ProjectContext context)
    {
        return JsonSerializer.Serialize(context, Options);
    }

    public static ProjectContext? Deserialize(string json)
    {
        return JsonSerializer.Deserialize<ProjectContext>(json, Options);
    }
}