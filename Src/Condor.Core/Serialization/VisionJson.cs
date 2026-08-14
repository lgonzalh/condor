using System.Text.Json;
using System.Text.Json.Serialization;
using Condor.Core.Models;

namespace Condor.Core.Serialization;

public static class VisionJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Serialize(VisionResult result)
    {
        return JsonSerializer.Serialize(result, Options);
    }

    public static VisionResult? Deserialize(string json)
    {
        return JsonSerializer.Deserialize<VisionResult>(json, Options);
    }
}
