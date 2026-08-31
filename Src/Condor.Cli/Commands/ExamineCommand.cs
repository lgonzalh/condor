using Condor.Cli.Presentation;
using Condor.Core.Contracts;
using Condor.Core.Models;
using Condor.Core.Serialization;

namespace Condor.Cli.Commands;

public static class ExamineCommand
{
    public static async Task<int> ExecuteAsync(
        IVisionService visionService,
        IStateStore stateStore,
        string[] args,
        CancellationToken cancellationToken = default)
    {
        var outputJson = args.Contains("--json", StringComparer.OrdinalIgnoreCase);
        var imagePath = BuildImagePath(args, outputJson);

        if (!outputJson)
        {
            RenderActivity();
        }

        var result = await visionService.ExamineAsync(imagePath, cancellationToken);

        await stateStore.SaveVisionAsync(result, cancellationToken);

        if (outputJson)
        {
            Console.WriteLine(VisionJson.Serialize(result));
        }
        else
        {
            Terminal.WriteLine();
            if (result.Status == DetectionStatus.Detected)
            {
                Terminal.WriteCyan("Condor examino la imagen.");
            }
            else
            {
                Terminal.WriteWarning("Condor no pudo examinar la imagen.");
            }

            Terminal.WriteLine();
            ExamineRenderer.RenderExamine(result);
        }

        return result.Status == DetectionStatus.Detected ? 0 : 1;
    }

    private static string BuildImagePath(string[] args, bool outputJson)
    {
        var path = new System.Text.StringBuilder();

        foreach (var arg in args)
        {
            if (string.Equals(arg, "--json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (path.Length > 0)
            {
                path.Append(' ');
            }

            path.Append(arg);
        }

        return path.ToString().Trim();
    }

    private static void RenderActivity()
    {
        Terminal.WriteInfo("Condor examina la imagen...");
        Terminal.WriteDim("  Validando la imagen local");
        Terminal.WriteDim("  Verificando capacidad de vision");
        Terminal.WriteDim("  Consultando el modelo local");
    }
}
