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

        // Analiza el directorio actual (CWD) donde se invoca, no un working dir
        // de una sesion anterior. El analisis describe el contenido real del
        // directorio; no exige ningun lenguaje/ecosistema concreto.
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
            ProjectAnalysisRenderer.Render(result);
        }

        return result.Project is { Status: DetectionStatus.NotDetected } ? 1 : 0;
    }
}
