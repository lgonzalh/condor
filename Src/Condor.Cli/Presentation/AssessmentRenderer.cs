using Condor.Core.Models;

namespace Condor.Cli.Presentation;

public static class AssessmentRenderer
{
    public static void RenderSummary(AssessmentResult result)
    {
        RenderEnvironment(result);
        RenderTools(result);
        RenderCapabilities(result);
        RenderDegradations(result);
    }

    private static void RenderEnvironment(AssessmentResult result)
    {
        var environment = result.Environment;

        Terminal.WriteHeader("ENTORNO");

        var osName = environment.Os.Status == DetectionStatus.Detected
            ? environment.Os.Name + " " + environment.Os.Version + " (" + environment.Os.Architecture + ")"
            : "(no detectable)";
        Terminal.WriteLine("  Sistema operativo : " + osName);

        var cpu = environment.Cpu.Status == DetectionStatus.Detected
            ? environment.Cpu.Name + " (" + environment.Cpu.Cores + " nucleos, " + environment.Cpu.LogicalProcessors + " hilos)"
            : "(no detectable)";
        Terminal.WriteLine("  CPU               : " + cpu);

        var memory = environment.Memory.Status == DetectionStatus.Detected
            ? environment.Memory.TotalGb.ToString("0.0") + " GB (libres " + environment.Memory.FreeGb.ToString("0.0") + " GB)"
            : "(no detectable)";
        Terminal.WriteLine("  RAM               : " + memory);

        if (environment.GpuStatus == DetectionStatus.Detected && environment.GpuList.Count > 0)
        {
            foreach (var gpu in environment.GpuList)
            {
                var vram = gpu.VramBytes > 0 ? " (" + (gpu.VramBytes / 1024.0 / 1024.0 / 1024.0).ToString("0.0") + " GB VRAM)" : "";
                Terminal.WriteLine("  GPU               : " + gpu.Name + vram);
            }
        }
        else
        {
            Terminal.WriteLine("  GPU               : (no detectable)");
        }

        if (environment.StorageStatus == DetectionStatus.Detected && environment.StorageList.Count > 0)
        {
            foreach (var disk in environment.StorageList)
            {
                var totalGb = disk.TotalBytes / 1024.0 / 1024.0 / 1024.0;
                var freeGb = disk.FreeBytes / 1024.0 / 1024.0 / 1024.0;
                Terminal.WriteLine("  Almacenamiento    : " + disk.Drive + " " + totalGb.ToString("0") + " GB (libres " + freeGb.ToString("0") + " GB)");
            }
        }
        else
        {
            Terminal.WriteLine("  Almacenamiento    : (no detectable)");
        }
    }

    private static void RenderTools(AssessmentResult result)
    {
        var tools = result.Tools;

        Terminal.WriteHeader("HERRAMIENTAS");

        if (tools.Git.Status == DetectionStatus.Detected)
        {
            Terminal.WriteLine("  Git     : " + (tools.Git.Version ?? "instalado") + " (instalado)");
        }
        else
        {
            Terminal.WriteLine("  Git     : (no detectado)");
        }

        if (tools.Ollama.Installed)
        {
            var server = tools.Ollama.ServerRunning
                ? "servidor activo (" + (tools.Ollama.ServerVersion ?? "version desconocida") + ")"
                : "instalado, servidor inactivo";
            Terminal.WriteLine("  Ollama  : " + server);

            if (tools.Ollama.Models.Count > 0)
            {
                Terminal.WriteLine("  Modelos : " + string.Join(", ", tools.Ollama.Models.Select(m => m.Name)));
            }
            else if (tools.Ollama.ServerRunning)
            {
                Terminal.WriteLine("  Modelos : (ninguno)");
            }
        }
        else
        {
            Terminal.WriteLine("  Ollama  : (no instalado)");
        }

        var otherTools = tools.DetectedTools
            .Where(t => t.Status == DetectionStatus.Detected && t.Name != "git" && t.Name != "ollama")
            .Select(t => t.Name)
            .ToList();

        Terminal.WriteLine("  Otras   : " + (otherTools.Count > 0 ? string.Join(", ", otherTools) : "(ninguna)"));
    }

    private static void RenderCapabilities(AssessmentResult result)
    {
        var capabilities = result.Capabilities;

        Terminal.WriteHeader("CAPACIDADES");
        Terminal.WriteLine("  LLM local : " + SiNo(capabilities.LocalLlm, "Ollama detectado", "Ollama no detectado"));
        Terminal.WriteLine("  GPU       : " + SiNo(capabilities.GpuDetected));
        Terminal.WriteLine("  Vision    : " + SiNo(capabilities.VisionCapable));
        Terminal.WriteLine("  Modo      : local, sin internet requerido");
    }

    private static void RenderDegradations(AssessmentResult result)
    {
        if (result.Capabilities.Issues.Count == 0)
        {
            return;
        }

        Terminal.WriteHeader("DEGRADACIONES");
        foreach (var issue in result.Capabilities.Issues)
        {
            Terminal.WriteWarning("  - " + issue.Capability + ": " + issue.Reason);
        }
    }

    private static string SiNo(bool value)
    {
        return value ? "SI" : "NO";
    }

    private static string SiNo(bool value, string yesDetail, string noDetail)
    {
        return value ? "SI (" + yesDetail + ")" : "NO (" + noDetail + ")";
    }
}
