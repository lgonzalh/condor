using System.Text.Json;

namespace Condor.Core.Project;

public static class PackageJsonParser
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

            content.Name = GetString(root, "name");
            content.Version = GetString(root, "version");
            AddDependencyKeys(root, content.Dependencies, "dependencies");
            AddDependencyKeys(root, content.Dependencies, "devDependencies");
            content.CapAndSortDependencies();
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

    private static void AddDependencyKeys(JsonElement element, List<string> target, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in value.EnumerateObject())
        {
            target.Add(property.Name);
        }
    }
}