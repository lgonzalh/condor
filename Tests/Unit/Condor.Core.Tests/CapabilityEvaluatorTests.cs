using Condor.Core.Evaluation;
using Condor.Core.Models;

namespace Condor.Core.Tests;

public class CapabilityEvaluatorTests
{
    private static EnvironmentProfile EntornoCompleto()
    {
        return new EnvironmentProfile
        {
            Cpu = new ProcessorInfo { Name = "CPU", Cores = 4, Status = DetectionStatus.Detected },
            Memory = new MemoryInfo { TotalBytes = 8L * 1024 * 1024 * 1024, Status = DetectionStatus.Detected },
            GpuStatus = DetectionStatus.Detected,
            GpuList = new List<GpuInfo> { new GpuInfo { Name = "GPU" } },
            StorageStatus = DetectionStatus.Detected,
            StorageList = new List<StorageInfo> { new StorageInfo { Drive = "C:" } }
        };
    }

    private static ToolsProfile HerramientasCompletas()
    {
        return new ToolsProfile
        {
            Ollama = new OllamaStatus
            {
                Installed = true,
                ServerRunning = true,
                Models = new List<ModelInfo> { new ModelInfo { Name = "modelo" } }
            },
            DetectedTools = new List<ToolInfo> { new ToolInfo { Name = "git", Status = DetectionStatus.Detected } }
        };
    }

    [Fact]
    public void Evaluate_EntornoCompleto_TodasLasCapacidadesActivas()
    {
        var capabilities = CapabilityEvaluator.Evaluate(EntornoCompleto(), HerramientasCompletas());

        Assert.True(capabilities.LocalLlm);
        Assert.True(capabilities.OllamaReady);
        Assert.True(capabilities.GpuDetected);
        Assert.True(capabilities.VisionCapable);
        Assert.Equal(1, capabilities.ModelsCount);
        Assert.Equal(1, capabilities.DetectedToolsCount);
        Assert.Empty(capabilities.Issues);
    }

    [Fact]
    public void Evaluate_SinGpu_VisionDesactivadaYIssueRegistrado()
    {
        var environment = EntornoCompleto();
        environment.GpuStatus = DetectionStatus.NotDetected;
        environment.GpuReason = "Sin controladores de video";
        environment.GpuList.Clear();

        var capabilities = CapabilityEvaluator.Evaluate(environment, HerramientasCompletas());

        Assert.False(capabilities.GpuDetected);
        Assert.False(capabilities.VisionCapable);
        Assert.Contains(capabilities.Issues, issue => issue.Capability == "gpu");
    }

    [Fact]
    public void Evaluate_SinOllama_LlmLocalDesactivadoYIssueRegistrado()
    {
        var tools = HerramientasCompletas();
        tools.Ollama = new OllamaStatus { Installed = false };

        var capabilities = CapabilityEvaluator.Evaluate(EntornoCompleto(), tools);

        Assert.False(capabilities.LocalLlm);
        Assert.False(capabilities.OllamaReady);
        Assert.Contains(capabilities.Issues, issue => issue.Capability == "ollama");
    }

    [Fact]
    public void Evaluate_OllamaInstaladoConServidorInactivo_OllamaReadyFalse()
    {
        var tools = HerramientasCompletas();
        tools.Ollama = new OllamaStatus { Installed = true, ServerRunning = false };

        var capabilities = CapabilityEvaluator.Evaluate(EntornoCompleto(), tools);

        Assert.True(capabilities.LocalLlm);
        Assert.False(capabilities.OllamaReady);
        Assert.Contains(capabilities.Issues, issue => issue.Capability == "ollama-server");
    }

    [Fact]
    public void Evaluate_CpuConError_IssueDeCpuRegistrado()
    {
        var environment = EntornoCompleto();
        environment.Cpu = new ProcessorInfo
        {
            Status = DetectionStatus.Error,
            Reason = "No fue posible consultar la CPU"
        };

        var capabilities = CapabilityEvaluator.Evaluate(environment, HerramientasCompletas());

        Assert.Contains(capabilities.Issues, issue => issue.Capability == "cpu");
    }

    [Fact]
    public void Evaluate_ConDegradacion_NoLanzaExcepcion()
    {
        var environment = new EnvironmentProfile();
        var tools = new ToolsProfile();

        var capabilities = CapabilityEvaluator.Evaluate(environment, tools);

        Assert.False(capabilities.LocalLlm);
        Assert.False(capabilities.GpuDetected);
        Assert.NotEmpty(capabilities.Issues);
    }
}
