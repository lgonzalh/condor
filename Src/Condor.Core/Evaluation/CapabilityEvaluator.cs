using Condor.Core.Models;

namespace Condor.Core.Evaluation;

public static class CapabilityEvaluator
{
    public static CapabilitiesSummary Evaluate(EnvironmentProfile environment, ToolsProfile tools)
    {
        var issues = new List<CapabilityIssue>();

        if (environment.Cpu.Status != DetectionStatus.Detected)
        {
            issues.Add(new CapabilityIssue
            {
                Capability = "cpu",
                Status = environment.Cpu.Status,
                Reason = environment.Cpu.Reason ?? "CPU no detectable"
            });
        }

        if (environment.Memory.Status != DetectionStatus.Detected)
        {
            issues.Add(new CapabilityIssue
            {
                Capability = "ram",
                Status = environment.Memory.Status,
                Reason = environment.Memory.Reason ?? "RAM no detectable"
            });
        }

        if (environment.GpuStatus != DetectionStatus.Detected)
        {
            issues.Add(new CapabilityIssue
            {
                Capability = "gpu",
                Status = environment.GpuStatus,
                Reason = environment.GpuReason ?? "GPU no detectable"
            });
        }

        if (environment.StorageStatus != DetectionStatus.Detected)
        {
            issues.Add(new CapabilityIssue
            {
                Capability = "storage",
                Status = environment.StorageStatus,
                Reason = environment.StorageReason ?? "Almacenamiento no detectable"
            });
        }

        if (!tools.Ollama.Installed)
        {
            issues.Add(new CapabilityIssue
            {
                Capability = "ollama",
                Status = DetectionStatus.NotDetected,
                Reason = "Ollama no esta instalado"
            });
        }
        else if (!tools.Ollama.ServerRunning)
        {
            issues.Add(new CapabilityIssue
            {
                Capability = "ollama-server",
                Status = DetectionStatus.NotDetected,
                Reason = tools.Ollama.Note ?? "El servidor de Ollama no responde"
            });
        }

        var gpuDetected = environment.GpuStatus == DetectionStatus.Detected && environment.GpuList.Count > 0;

        return new CapabilitiesSummary
        {
            LocalLlm = tools.Ollama.Installed,
            OllamaReady = tools.Ollama.Installed && tools.Ollama.ServerRunning,
            GpuDetected = gpuDetected,
            VisionCapable = gpuDetected,
            DetectedToolsCount = tools.DetectedTools.Count,
            ModelsCount = tools.Ollama.Models.Count,
            Issues = issues
        };
    }
}
