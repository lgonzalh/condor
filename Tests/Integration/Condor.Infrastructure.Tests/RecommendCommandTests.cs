using System.Text;
using Condor.Cli.Commands;
using Condor.Core.Models;
using Condor.Core.Serialization;
using Condor.Infrastructure.State;

namespace Condor.Infrastructure.Tests;

public class RecommendCommandTests
{
    private static readonly double Gb = 1024 * 1024 * 1024;

    [Fact]
    public async Task ExecuteAsync_SinAssessment_DevuelveError()
    {
        var directory = Path.Combine(Path.GetTempPath(), "condor-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var stateStore = new LocalStateStore(directory);

        var exitCode = await RecommendCommand.ExecuteAsync(stateStore, Array.Empty<string>());

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task ExecuteAsync_AssessmentCorrupto_DevuelveError()
    {
        var directory = Path.Combine(Path.GetTempPath(), "condor-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "assessment.json"),
            "json corrupto {{{",
            Encoding.UTF8);
        var stateStore = new LocalStateStore(directory);

        var exitCode = await RecommendCommand.ExecuteAsync(stateStore, Array.Empty<string>());

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task ExecuteAsync_AssessmentValidoConModelos_DevuelveExito()
    {
        var assessment = new AssessmentResult
        {
            Environment = new EnvironmentProfile
            {
                Memory = new MemoryInfo
                {
                    TotalBytes = (long)(15.4 * Gb),
                    FreeBytes = (long)(7.1 * Gb),
                    Status = DetectionStatus.Detected
                }
            },
            Tools = new ToolsProfile
            {
                Ollama = new OllamaStatus
                {
                    Installed = true,
                    ServerRunning = true,
                    Models = new List<ModelInfo>
                    {
                        new ModelInfo
                        {
                            Name = "qwen-tools:7b",
                            SizeBytes = (long)(4.36 * Gb),
                            Family = "qwen2",
                            ParameterSize = "7.6B",
                            Quantization = "Q4_K_M",
                            ContextLength = 32768,
                            Capabilities = new List<string> { "completion", "tools", "insert" }
                        }
                    }
                }
            }
        };

        var directory = Path.Combine(Path.GetTempPath(), "condor-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "assessment.json"),
            AssessmentJson.Serialize(assessment),
            Encoding.UTF8);
        var stateStore = new LocalStateStore(directory);

        var exitCode = await RecommendCommand.ExecuteAsync(stateStore, Array.Empty<string>());

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task ExecuteAsync_PropositoInvalido_DevuelveError()
    {
        var assessment = new AssessmentResult
        {
            Tools = new ToolsProfile
            {
                Ollama = new OllamaStatus { Installed = true, ServerRunning = true }
            }
        };

        var directory = Path.Combine(Path.GetTempPath(), "condor-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "assessment.json"),
            AssessmentJson.Serialize(assessment),
            Encoding.UTF8);
        var stateStore = new LocalStateStore(directory);

        var exitCode = await RecommendCommand.ExecuteAsync(
            stateStore,
            new[] { "--proposito", "raro" });

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task ExecuteAsync_CuandoSeUsaElArgumentoInglesPurpose_DevuelveError()
    {
        var directory = Path.Combine(Path.GetTempPath(), "condor-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var stateStore = new LocalStateStore(directory);

        var exitCode = await RecommendCommand.ExecuteAsync(
            stateStore,
            new[] { "--purpose", "desarrollo" });

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task ExecuteAsync_SinModelosDisponibles_DevuelveError()
    {
        var assessment = new AssessmentResult
        {
            Tools = new ToolsProfile
            {
                Ollama = new OllamaStatus { Installed = true, ServerRunning = true, Models = new List<ModelInfo>() }
            }
        };

        var directory = Path.Combine(Path.GetTempPath(), "condor-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "assessment.json"),
            AssessmentJson.Serialize(assessment),
            Encoding.UTF8);
        var stateStore = new LocalStateStore(directory);

        var exitCode = await RecommendCommand.ExecuteAsync(stateStore, Array.Empty<string>());

        Assert.Equal(1, exitCode);
    }
}
