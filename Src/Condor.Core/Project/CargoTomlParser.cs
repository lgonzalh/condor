namespace Condor.Core.Project;

public static class CargoTomlParser
{
    public static ManifestContent Parse(string text)
    {
        var content = new ManifestContent();
        var section = "";

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line.Substring(1, line.Length - 2).Trim();
                if (section.StartsWith("dependencies.", StringComparison.OrdinalIgnoreCase))
                {
                    var dependencyName = section.Substring("dependencies.".Length).Trim();
                    if (dependencyName.Length > 0)
                    {
                        content.Dependencies.Add(dependencyName);
                    }
                }

                continue;
            }

            if (section.Equals("package", StringComparison.OrdinalIgnoreCase))
            {
                var equals = line.IndexOf('=');
                if (equals <= 0)
                {
                    continue;
                }

                var key = line.Substring(0, equals).Trim();
                var value = Unquote(line.Substring(equals + 1).Trim());
                if (key == "name")
                {
                    content.Name = value;
                }
                else if (key == "version")
                {
                    content.Version = value;
                }
            }
            else if (section.Equals("dependencies", StringComparison.OrdinalIgnoreCase))
            {
                var equals = line.IndexOf('=');
                if (equals <= 0)
                {
                    continue;
                }

                var key = line.Substring(0, equals).Trim();
                if (key.Length > 0 && !key.StartsWith('"'))
                {
                    content.Dependencies.Add(key);
                }
            }
        }

        content.CapAndSortDependencies();
        return content;
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