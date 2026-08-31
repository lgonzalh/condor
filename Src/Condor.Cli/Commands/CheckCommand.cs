using Condor.Cli.Presentation;
using Condor.Core.Contracts;
using Condor.Core.Models;
using Condor.Core.Serialization;

namespace Condor.Cli.Commands;

public static class CheckCommand
{
    public static async Task<int> ExecuteAsync(
        ISemanticVerificationService service,
        IStateStore stateStore,
        string[] args,
        CancellationToken cancellationToken = default)
    {
        var outputJson = args.Contains("--json", StringComparer.OrdinalIgnoreCase);
        var compile = args.Contains("--compilar", StringComparer.OrdinalIgnoreCase);
        var test = args.Contains("--probar", StringComparer.OrdinalIgnoreCase);
        var both = !compile && !test;

        if (!outputJson)
        {
            RenderActivity(both, compile, test);
        }

        var result = await service.VerifySemanticAsync(
            both || compile,
            both || test,
            cancellationToken);

        await stateStore.SaveSemanticVerificationAsync(result, cancellationToken);

        if (outputJson)
        {
            Console.WriteLine(SemanticVerificationJson.Serialize(result));
        }
        else
        {
            Terminal.WriteLine();
            if (result.Status == DetectionStatus.NotDetected)
            {
                Terminal.WriteError("No hay verificacion semantica disponible.");
                Terminal.WriteDim(result.Reason ?? "Ejecuta 'condor contexto' primero.");
            }
            else if (result.Status == DetectionStatus.Detected)
            {
                Terminal.WriteCyan("Condor verifico semanticamente el proyecto.");
            }
            else
            {
                Terminal.WriteWarning("Condor encontro condiciones pendientes en la verificacion semantica.");
            }

            Terminal.WriteLine();
            SemanticVerificationRenderer.Render(result);
        }

        return result.Status == DetectionStatus.NotDetected ? 1 : 0;
    }

    private static void RenderActivity(bool both, bool compile, bool test)
    {
        Terminal.WriteInfo("Condor verifica semanticamente el proyecto...");
        Terminal.WriteDim("  Detectando la herramienta de compilacion/pruebas");
        Terminal.WriteDim("  Compilando" + (both ? " y ejecutando pruebas" : compile ? " el proyecto" : test ? " (pruebas)" : "") + " con --no-restore");
    }
}
