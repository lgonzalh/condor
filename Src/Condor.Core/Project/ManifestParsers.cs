namespace Condor.Core.Project;

public static class ManifestParsers
{
    public static bool IsParsedKind(string kind)
    {
        return kind is "package.json"
            or ".tsconfig.json"
            or "csproj"
            or "pom.xml"
            or "Cargo.toml"
            or "pyproject.toml"
            or "requirements.txt"
            or "go.mod";
    }

    public static ManifestContent Parse(string kind, string text)
    {
        return kind switch
        {
            "package.json" => PackageJsonParser.Parse(text),
            ".tsconfig.json" => TsConfigJsonParser.Parse(text),
            "csproj" => CsprojParser.Parse(text),
            "pom.xml" => PomXmlParser.Parse(text),
            "Cargo.toml" => CargoTomlParser.Parse(text),
            "pyproject.toml" => PyprojectTomlParser.Parse(text),
            "requirements.txt" => RequirementsTxtParser.Parse(text),
            "go.mod" => GoModParser.Parse(text),
            _ => new ManifestContent()
        };
    }
}