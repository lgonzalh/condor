using System.Text.Json;
using System.Text.Json.Serialization;
using Condor.Core.Models;

namespace Condor.Core.Serialization;

public static class AssessmentJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize(AssessmentResult result)
    {
        return JsonSerializer.Serialize(result, Options);
    }

    public static AssessmentResult? Deserialize(string json)
    {
        return JsonSerializer.Deserialize<AssessmentResult>(json, Options);
    }
}
