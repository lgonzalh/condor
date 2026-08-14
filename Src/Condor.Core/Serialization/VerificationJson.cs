using System.Text.Json;
using System.Text.Json.Serialization;
using Condor.Core.Models;

namespace Condor.Core.Serialization;

public static class VerificationJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Serialize(VerificationResult result)
    {
        return JsonSerializer.Serialize(result, Options);
    }

    public static VerificationResult? Deserialize(string json)
    {
        return JsonSerializer.Deserialize<VerificationResult>(json, Options);
    }
}
