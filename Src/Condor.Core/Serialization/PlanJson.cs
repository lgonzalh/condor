using System.Text.Json;
using System.Text.Json.Serialization;
using Condor.Core.Models;

namespace Condor.Core.Serialization;

public static class PlanJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize(WorkPlan plan)
    {
        return JsonSerializer.Serialize(plan, Options);
    }

    public static WorkPlan? Deserialize(string json)
    {
        return JsonSerializer.Deserialize<WorkPlan>(json, Options);
    }
}
