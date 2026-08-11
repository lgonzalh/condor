namespace Condor.Core.Project;

public static class GoModParser
{
    public static ManifestContent Parse(string text)
    {
        var content = new ManifestContent();
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.StartsWith("module ", StringComparison.Ordinal))
            {
                var module = line.Substring("module ".Length).Trim();
                if (module.Length > 0)
                {
                    content.Name = Unquote(module);
                    break;
                }
            }
        }

        return content;
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            return value.Substring(1, value.Length - 2);
        }

        return value;
    }
}