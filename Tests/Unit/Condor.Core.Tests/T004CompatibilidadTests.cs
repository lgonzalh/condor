using Condor.Core.Models;
using Condor.Core.Serialization;

namespace Condor.Core.Tests;

public class T004CompatibilidadTests
{
    [Fact]
    public void AssessmentPrevioSinProject_SeDeserializaComoValido()
    {
        var json = """
        {
          "schemaVersion": "1.0.0",
          "generatedAtUtc": "2026-08-10T12:00:00Z",
          "workingDirectory": "C:\\proyecto",
          "environment": { "os": { "name": "Windows 11 Pro" } },
          "tools": {},
          "capabilities": {}
        }
        """;

        var result = AssessmentJson.Deserialize(json);

        Assert.NotNull(result);
        Assert.Equal("1.0.0", result.SchemaVersion);
        Assert.Equal("C:\\proyecto", result.WorkingDirectory);
        Assert.Null(result.Project);
    }

    [Fact]
    public void SerializacionSinProject_NoEmiteElCampoProject()
    {
        var result = new AssessmentResult { WorkingDirectory = "C:\\proyecto" };

        var json = AssessmentJson.Serialize(result);

        Assert.DoesNotContain("\"project\"", json);
    }

    [Fact]
    public void SerializacionConProject_EmiteElCampoProject()
    {
        var result = new AssessmentResult
        {
            WorkingDirectory = "C:\\proyecto",
            Project = new ProjectProfile { RootPath = "C:\\proyecto", RootName = "proyecto" }
        };

        var json = AssessmentJson.Serialize(result);

        Assert.Contains("\"project\"", json);
        Assert.Contains("\"rootName\": \"proyecto\"", json);
    }
}