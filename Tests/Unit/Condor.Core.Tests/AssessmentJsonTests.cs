using Condor.Core.Models;
using Condor.Core.Serialization;

namespace Condor.Core.Tests;

public class AssessmentJsonTests
{
    [Fact]
    public void Serialize_ProduceJsonConSchemaVersion()
    {
        var result = new AssessmentResult();

        var json = AssessmentJson.Serialize(result);

        Assert.Contains("\"schemaVersion\": \"1.0.0\"", json);
    }

    [Fact]
    public void Serialize_Deserialize_ConservaDatosPrincipales()
    {
        var result = new AssessmentResult
        {
            SchemaVersion = "1.0.0",
            GeneratedAtUtc = new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc),
            WorkingDirectory = "C:\\proyecto",
            Environment = new EnvironmentProfile
            {
                Os = new OperatingSystemInfo
                {
                    Name = "Windows 11 Pro",
                    Version = "10.0.26200",
                    Architecture = "x64",
                    Status = DetectionStatus.Detected
                },
                Cpu = new ProcessorInfo { Name = "Intel Core i7", Cores = 8, LogicalProcessors = 16, Status = DetectionStatus.Detected },
                Memory = new MemoryInfo { TotalBytes = 16L * 1024 * 1024 * 1024, Status = DetectionStatus.Detected },
                GpuStatus = DetectionStatus.Detected,
                GpuList = new List<GpuInfo> { new GpuInfo { Name = "NVIDIA RTX" } },
                StorageStatus = DetectionStatus.Detected,
                StorageList = new List<StorageInfo> { new StorageInfo { Drive = "C:", TotalBytes = 512L * 1024 * 1024 * 1024 } }
            },
            Tools = new ToolsProfile
            {
                Git = new ToolInfo { Name = "git", Version = "2.45.1", Status = DetectionStatus.Detected },
                Ollama = new OllamaStatus
                {
                    Installed = true,
                    ServerRunning = true,
                    ServerVersion = "0.31.1",
                    Models = new List<ModelInfo> { new ModelInfo { Name = "qwen3:8b" } }
                }
            }
        };

        var json = AssessmentJson.Serialize(result);
        var restored = AssessmentJson.Deserialize(json);

        Assert.NotNull(restored);
        Assert.Equal(result.SchemaVersion, restored.SchemaVersion);
        Assert.Equal(result.WorkingDirectory, restored.WorkingDirectory);
        Assert.Equal(result.Environment.Os.Name, restored.Environment.Os.Name);
        Assert.Equal(result.Environment.Cpu.Cores, restored.Environment.Cpu.Cores);
        Assert.Equal(result.Environment.GpuList.Count, restored.Environment.GpuList.Count);
        Assert.Equal(result.Environment.StorageList.Count, restored.Environment.StorageList.Count);
        Assert.Equal(result.Tools.Git.Version, restored.Tools.Git.Version);
        Assert.Equal(result.Tools.Ollama.Models.Count, restored.Tools.Ollama.Models.Count);
        Assert.Equal(result.Tools.Ollama.Models[0].Name, restored.Tools.Ollama.Models[0].Name);
    }

    [Fact]
    public void Deserialize_JsonInvalido_LanzaJsonException()
    {
        Assert.Throws<System.Text.Json.JsonException>(() => AssessmentJson.Deserialize("{ no es json valido"));
    }
}
