namespace Condor.Core.Project;

public static class RequirementsTxtParser
{
    private const string NameChars = "=<>~;! \t[";

    public static ManifestContent Parse(string text)
    {
        var content = new ManifestContent();

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith('-'))
            {
                continue;
            }

            var name = new string(line.TakeWhile(c => NameChars.IndexOf(c) < 0).ToArray()).Trim();
            if (name.Length > 0)
            {
                content.Dependencies.Add(name);
            }
        }

        content.CapAndSortDependencies();
        return content;
    }
}