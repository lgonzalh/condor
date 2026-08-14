using System.Text.Json;
using System.Text.Json.Serialization;
using Condor.Core.Models;

namespace Condor.Core.Serialization;

public static class BuildJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Serialize(BuildResult result)
    {
        return JsonSerializer.Serialize(result, Options);
    }

    public static BuildResult? Deserialize(string json)
    {
        return JsonSerializer.Deserialize<BuildResult>(json, Options);
    }
}
