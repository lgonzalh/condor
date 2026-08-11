namespace Condor.Core.Project;

public sealed record FamilySignals(string Name, string[] ManifestKinds, string[] Extensions, string[] MarkerFiles);

public static class SignalCatalog
{
    public static readonly IReadOnlyList<FamilySignals> Families = new[]
    {
        new FamilySignals("C#", new[] { "csproj", "sln", "slnx", "global.json", "Directory.Build.props" }, new[] { ".cs" }, Array.Empty<string>()),
        new FamilySignals("JavaScript/TypeScript", new[] { "package.json", ".tsconfig.json" }, new[] { ".js", ".ts", ".jsx", ".tsx", ".mjs", ".cjs" }, Array.Empty<string>()),
        new FamilySignals("Python", new[] { "requirements.txt", "pyproject.toml", "setup.py" }, new[] { ".py" }, Array.Empty<string>()),
        new FamilySignals("Java", new[] { "pom.xml", "build.gradle" }, new[] { ".java" }, Array.Empty<string>()),
        new FamilySignals("Go", new[] { "go.mod" }, new[] { ".go" }, Array.Empty<string>()),
        new FamilySignals("Rust", new[] { "Cargo.toml" }, new[] { ".rs" }, Array.Empty<string>()),
        new FamilySignals("C/C++", new[] { "CMakeLists.txt", "Makefile" }, new[] { ".c", ".cpp", ".h", ".hpp", ".cc", ".cxx" }, Array.Empty<string>()),
        new FamilySignals("HTML/CSS", Array.Empty<string>(), new[] { ".html", ".css" }, new[] { "index.html" }),
        new FamilySignals("Shell/Windows", Array.Empty<string>(), new[] { ".ps1", ".bat", ".cmd" }, Array.Empty<string>())
    };

    public static readonly IReadOnlyList<string> ReadmeNames = new[] { "README.md", "README" };
    public static readonly IReadOnlyList<string> LicenseNames = new[] { "LICENSE", "LICENSE.md", "LICENSE.txt" };
    public static readonly IReadOnlyList<string> ChangelogNames = new[] { "CHANGELOG.md", "CHANGELOG" };

    private static readonly Dictionary<string, string> ExactManifestKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["package.json"] = "package.json",
        [".tsconfig.json"] = ".tsconfig.json",
        ["pom.xml"] = "pom.xml",
        ["Cargo.toml"] = "Cargo.toml",
        ["pyproject.toml"] = "pyproject.toml",
        ["requirements.txt"] = "requirements.txt",
        ["go.mod"] = "go.mod",
        ["Makefile"] = "Makefile",
        ["CMakeLists.txt"] = "CMakeLists.txt",
        ["build.gradle"] = "build.gradle",
        ["setup.py"] = "setup.py",
        ["global.json"] = "global.json",
        ["Directory.Build.props"] = "Directory.Build.props"
    };

    private static readonly Dictionary<string, string> SuffixManifestKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["csproj"] = "csproj",
        ["sln"] = "sln",
        ["slnx"] = "slnx"
    };

    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "node_modules", "bin", "obj", "dist", "build", ".vs"
    };

    private static readonly HashSet<string> BinaryExtensions = new()
    {
        ".dll", ".exe", ".so", ".dylib", ".o", ".obj", ".a", ".lib",
        ".bin", ".dat", ".db", ".sqlite", ".zip", ".gz", ".7z", ".rar", ".tar", ".iso",
        ".jar", ".class", ".pyc", ".wasm",
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".pdf",
        ".ttf", ".otf", ".woff", ".woff2", ".eot",
        ".mp3", ".mp4", ".mpg", ".avi", ".mkv"
    };

    public static bool IsManifestName(string fileName)
    {
        return ManifestKindOf(fileName) is not null;
    }

    public static string? ManifestKindOf(string fileName)
    {
        if (ExactManifestKinds.TryGetValue(fileName, out var exactKind))
        {
            return exactKind;
        }

        var dot = fileName.LastIndexOf('.');
        if (dot < 0 || dot == fileName.Length - 1)
        {
            return null;
        }

        return SuffixManifestKinds.TryGetValue(fileName.Substring(dot + 1), out var suffixKind)
            ? suffixKind
            : null;
    }

    public static string ExtensionKey(string fileName)
    {
        var dot = fileName.LastIndexOf('.');
        if (dot < 0 || dot == fileName.Length - 1)
        {
            return "";
        }

        return fileName.Substring(dot).ToLowerInvariant();
    }

    public static bool IsBinaryExtension(string fileName)
    {
        return BinaryExtensions.Contains(ExtensionKey(fileName));
    }

    public static bool IsExcludedDirectory(string name)
    {
        return ExcludedDirectories.Contains(name);
    }
}