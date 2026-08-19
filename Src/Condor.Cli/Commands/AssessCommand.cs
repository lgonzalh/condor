using Condor.Core.Contracts;
using Condor.Core.Models;
using Condor.Core.Serialization;
using Condor.Cli.Presentation;

namespace Condor.Cli.Commands;

/// <summary>
/// /analizar: analiza el proyecto/directorio actual (estructura, contenido,
/// senales, estado, intencion probable y contexto). El analisis de hardware y
/// modelos NO pertenece a este comando: forma parte de la preparacion
/// automatica de Condor al iniciar.
/// </summary>
public static class AssessCommand
{
    public static async Task<int> ExecuteAsync(
        IAssessmentService assessmentService,
        IStateStore stateStore,
        string[] args,
        CancellationToken cancellationToken = default)
    {
        var outputJson = args.Contains("--json", StringComparer.OrdinalIgnoreCase);

        var result = await stateStore.LoadAssessmentAsync(cancellationToken);
        if (result is null)
        {
            result = await assessmentService.ExecuteAsync(new AssessmentRequest(), cancellationToken);
            await stateStore.SaveAssessmentAsync(result, cancellationToken);
        }

        if (outputJson)
        {
            Console.WriteLine(AssessmentJson.Serialize(result));
        }
        else
        {
            Terminal.WriteLine();
            ProjectAnalysisRenderer.Render(result);
        }

        return result.Project is { Status: DetectionStatus.NotDetected } ? 1 : 0;
    }
}
