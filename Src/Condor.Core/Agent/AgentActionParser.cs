using System.Text.Json;
using System.Text;
using Condor.Core.Models;

namespace Condor.Core.Agent;

public static class AgentActionParser
{
    private static readonly JsonSerializerOptions _options = new() { PropertyNameCaseInsensitive = true };

    public static AgentAction? Parse(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;
        var json = ExtractJson(content);
        if (json is null) return null;

        try
        {
            var normalized = NormalizePropertyNames(json);
            var action = JsonSerializer.Deserialize<AgentAction>(normalized, _options);
            if (action?.PlanItems is not null)
            {
                // planItems es la representación canónica. Modelos que aún siguen
                // instrucciones anteriores pueden añadir también las listas legadas;
                // nunca deben desplazar ni invalidar los datos agrupados.
                action.PlanSteps = new List<string>(action.PlanItems.Count);
                action.ExpectedResults = new List<string>(action.PlanItems.Count);
                action.VerificationSteps = new List<string>(action.PlanItems.Count);
                foreach (var item in action.PlanItems)
                {
                    action.PlanSteps.Add(item?.Task ?? "");
                    action.ExpectedResults.Add(item?.ExpectedResult ?? "");
                    action.VerificationSteps.Add(item?.VerificationStep ?? "");
                }
            }

            return action;
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizePropertyNames(string json)
    {
        using var document = JsonDocument.Parse(json);
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output))
        {
            WriteNormalized(document.RootElement, writer);
        }

        return Encoding.UTF8.GetString(output.ToArray());
    }

    private static void WriteNormalized(JsonElement element, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                var propertyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in element.EnumerateObject())
                {
                    var normalizedName = ToCamelCase(property.Name);
                    if (!propertyNames.Add(normalizedName))
                        throw new JsonException("El JSON repite una propiedad tras normalizar su nombre.");
                    writer.WritePropertyName(normalizedName);
                    WriteNormalized(property.Value, writer);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray()) WriteNormalized(item, writer);
                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static string ToCamelCase(string propertyName)
    {
        if (!propertyName.Contains('_')) return propertyName;

        var result = new StringBuilder(propertyName.Length);
        var uppercaseNext = false;
        foreach (var character in propertyName)
        {
            if (character == '_')
            {
                uppercaseNext = true;
                continue;
            }

            result.Append(uppercaseNext ? char.ToUpperInvariant(character) : character);
            uppercaseNext = false;
        }

        return result.ToString();
    }

    private static string? ExtractJson(string text)
    {
        var start = text.IndexOf('{');
        if (start < 0) return null;

        var depth = 0;
        var inString = false;
        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '"' && (i == 0 || text[i - 1] != '\\')) inString = !inString;
            if (inString) continue;
            if (c == '{') depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0) return text.Substring(start, i - start + 1);
            }
        }

        return start < text.Length ? text.Substring(start) : null;
    }
}
