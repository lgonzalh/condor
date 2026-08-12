using Condor.Core.Models;
using Condor.Core.Project;
using Condor.Core.Serialization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Condor.Core.Tests;

public class DeterminismTests
{
    [Fact]
    public void Builder_OrdenaColeccionesYEliminaDuplicados()
    {
        var scan = new ProjectScan();
        scan.Directories.Add(new ScannedDirectory("zzz", false));
        scan.Directories.Add(new ScannedDirectory("b", false));
        scan.Files.Add(new ScannedFile("zzz.txt", 1));
        scan.Files.Add(new ScannedFile("b.txt", 1));
        scan.Files.Add(new ScannedFile("z/app.csproj", 1));
        scan.Files.Add(new ScannedFile("z/Program.cs", 1));
        scan.Files.Add(new ScannedFile("z/Extra.cs", 1));
        scan.ExtensionCounts[".ts"] = 2;
        scan.ExtensionCounts[".cs"] = 3;

        var manifests = new List<ManifestRecord>
        {
            new() { Kind = "csproj", Path = "z/app.csproj" },
            new() { Kind = "package.json", Path = "package.json" }
        };
        var classification = new ProjectClassifier().Classify(scan, manifests);
        var limits = new List<string> { "dependencies", "a", "dependencies" };

        var profile = ProjectProfileBuilder.Build(
            DetectionStatus.Detected,
            null,
            "C:\\p",
            "p",
            scan,
            manifests,
            classification,
            null,
            false,
            limits,
            DateTime.UtcNow);

        Assert.Equal(new[] { "b", "zzz" }, profile.TopLevelDirectories);
        Assert.Equal(new[] { "b.txt", "zzz.txt" }, profile.TopLevelFiles);
        Assert.Equal(new[] { ".cs", ".ts" }, profile.ExtensionCounts.Select(e => e.Name));
        Assert.Equal(new[] { "a", "dependencies" }, profile.LimitsApplied);
        Assert.Equal(new[] { "package.json", "z/app.csproj" }, profile.Manifests.Select(m => m.Path));
        Assert.Equal(5, profile.FilesCount);
        Assert.Equal(2, profile.DirectoriesCount);
    }

    [Fact]
    public void Builder_DosPerfilesConLosMismosDatos_SonIgualesExceptoGeneratedAt()
    {
        var scan = new ProjectScan();
        scan.Files.Add(new ScannedFile("a.txt", 5));
        scan.Files.Add(new ScannedFile("b.py", 5));
        scan.Files.Add(new ScannedFile("c.py", 5));
        scan.Files.Add(new ScannedFile("app.py", 5));
        scan.Directories.Add(new ScannedDirectory("src", false));
        scan.ExtensionCounts[".txt"] = 1;
        scan.ExtensionCounts[".py"] = 3;
        var manifests = new List<ManifestRecord>();
        var classification = new ProjectClassifier().Classify(scan, manifests);
        var limits = new List<string> { "a", "a" };

        var profileA = ProjectProfileBuilder.Build(
            DetectionStatus.Detected,
            null,
            "C:\\p",
            "p",
            scan,
            manifests,
            classification,
            null,
            false,
            limits,
            new DateTime(2026, 8, 11, 1, 0, 0, DateTimeKind.Utc));
        var profileB = ProjectProfileBuilder.Build(
            DetectionStatus.Detected,
            null,
            "C:\\p",
            "p",
            scan,
            manifests,
            classification,
            null,
            false,
            limits,
            new DateTime(2026, 8, 11, 2, 0, 0, DateTimeKind.Utc));

        var jsonA = Normalize(JsonSerializer.Serialize(profileA, AssessmentJson.Options));
        var jsonB = Normalize(JsonSerializer.Serialize(profileB, AssessmentJson.Options));

        Assert.Equal(jsonA, jsonB);
    }

    private static string Normalize(string json)
    {
        var node = JsonNode.Parse(json)!.AsObject();
        node.Remove("generatedAtUtc");
        return node.ToJsonString();
    }
}