using Condor.Core.Contracts;
using Condor.Core.Models;
using Condor.Core.Serialization;
using Condor.Cli.Presentation;

namespace Condor.Cli.Commands;

public static class AssessCommand
{
    public static async Task<int> ExecuteAsync(
        IAssessmentService assessmentService,
        IStateStore stateStore,
        string[] args,
        CancellationToken cancellationToken = default)
    {
        var outputJson = args.Contains("--json", StringComparer.OrdinalIgnoreCase);

        if (!outputJson)
        {
            RenderActivity();
        }

        var request = new AssessmentRequest
        {
            WorkingDirectory = Environment.CurrentDirectory
        };

        var result = await assessmentService.ExecuteAsync(request, cancellationToken);

        await stateStore.SaveAssessmentAsync(result, cancellationToken);

        if (outputJson)
        {
            Console.WriteLine(AssessmentJson.Serialize(result));
        }
        else
        {
            Terminal.WriteLine();
            Terminal.WriteSuccess("Condor observo el entorno.");
            Terminal.WriteLine();
            AssessmentRenderer.RenderSummary(result);
            Terminal.WriteLine();
            Terminal.WriteDim("Condor quedo preparado para recomendar un modelo compatible (T-003).");
        }

        return result.Project is { Status: DetectionStatus.NotDetected } ? 1 : 0;
    }

    private static void RenderActivity()
    {
        Terminal.WriteInfo("Condor observa el entorno...");
        Terminal.WriteDim("  Detectando sistema operativo");
        Terminal.WriteDim("  Analizando CPU y memoria");
        Terminal.WriteDim("  Buscando GPU");
        Terminal.WriteDim("  Revisando almacenamiento");
        Terminal.WriteDim("  Verificando Git y herramientas");
        Terminal.WriteDim("  Descubriendo el proyecto local");
        Terminal.WriteDim("  Verificando Ollama y modelos locales");
    }
}