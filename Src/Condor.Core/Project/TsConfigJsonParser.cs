using System.Text.Json;

namespace Condor.Core.Project;

public static class TsConfigJsonParser
{
    public static ManifestContent Parse(string text)
    {
        var content = new ManifestContent();
        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                content.ParseError = true;
                return content;
            }

            if (root.TryGetProperty("compilerOptions", out var options) && options.ValueKind == JsonValueKind.Object)
            {
                content.TsTarget = GetString(options, "target");
            }
        }
        catch (JsonException)
        {
            content.ParseError = true;
        }

        return content;
    }

    private static string? GetString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}