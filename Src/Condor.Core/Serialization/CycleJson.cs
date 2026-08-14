using System.Text.Json;
using System.Text.Json.Serialization;
using Condor.Core.Models;

namespace Condor.Core.Serialization;

public static class CycleJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Serialize(CycleResult result)
    {
        return JsonSerializer.Serialize(result, Options);
    }

    public static CycleResult? Deserialize(string json)
    {
        return JsonSerializer.Deserialize<CycleResult>(json, Options);
    }
}
