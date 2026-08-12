namespace Condor.Core.Project;

public static class PyprojectTomlParser
{
    private const string NameChars = "=<>~!;[] \t";

    public static ManifestContent Parse(string text)
    {
        var content = new ManifestContent();
        var section = "";
        var lines = text.Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line.Substring(1, line.Length - 2).Trim();
                continue;
            }

            if (!section.Equals("project", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var equals = line.IndexOf('=');
            if (equals <= 0)
            {
                continue;
            }

            var key = line.Substring(0, equals).Trim();
            var valueText = line.Substring(equals + 1).Trim();
            if (key == "name")
            {
                content.Name = Unquote(valueText);
            }
            else if (key == "version")
            {
                content.Version = Unquote(valueText);
            }
            else if (key == "dependencies")
            {
                var arrayText = ReadArray(lines, i, valueText);
                if (arrayText is null)
                {
                    continue;
                }

                foreach (var item in arrayText.Split(','))
                {
                    var name = PackageName(item);
                    if (name.Length > 0)
                    {
                        content.Dependencies.Add(name);
                    }
                }
            }
        }

        content.CapAndSortDependencies();
        return content;
    }

    private static string? ReadArray(string[] lines, int lineIndex, string valueText)
    {
        var start = valueText.IndexOf('[');
        if (start < 0)
        {
            return null;
        }

        if (valueText.IndexOf(']') > start)
        {
            return TrimArray(valueText);
        }

        var current = valueText;
        for (var i = lineIndex + 1; i < lines.Length; i++)
        {
            current += " " + lines[i];
            if (current.IndexOf(']') >= 0)
            {
                break;
            }
        }

        return TrimArray(current);
    }

    private static string? TrimArray(string text)
    {
        var start = text.IndexOf('[');
        var end = text.LastIndexOf(']');
        if (start < 0 || end <= start)
        {
            return null;
        }

        return text.Substring(start + 1, end - start - 1);
    }

    private static string PackageName(string item)
    {
        var text = Unquote(item.Trim());
        return new string(text.TakeWhile(c => NameChars.IndexOf(c) < 0).ToArray()).Trim();
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            return value.Substring(1, value.Length - 2);
        }

        if (value.Length >= 2 && value[0] == '\'' && value[^1] == '\'')
        {
            return value.Substring(1, value.Length - 2);
        }

        return value;
    }
}