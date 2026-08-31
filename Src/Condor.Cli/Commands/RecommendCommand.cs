using System.Globalization;
using Condor.Cli.Presentation;
using Condor.Core.Contracts;
using Condor.Core.Evaluation;
using Condor.Core.Models;

namespace Condor.Cli.Commands;

public static class RecommendCommand
{
    private static readonly HashSet<string> AllowedPurposes = new(StringComparer.OrdinalIgnoreCase)
    {
        "desarrollo", "general", "vision"
    };

    public static async Task<int> ExecuteAsync(
        IStateStore stateStore,
        string[] args,
        CancellationToken cancellationToken = default)
    {
        if (args.Any(argument => argument.Equals("--purpose", StringComparison.OrdinalIgnoreCase)))
        {
            Terminal.WriteError("El argumento '--purpose' ya no se usa. Usa '--proposito <tipo>'.");
            return 1;
        }

        var purpose = ParsePurpose(args);
        if (purpose is null)
        {
            Terminal.WriteError("Proposito no valido. Usa: desarrollo, general o vision.");
            return 1;
        }

        var assessment = await stateStore.LoadAssessmentAsync(cancellationToken);
        if (assessment is null)
        {
            Terminal.WriteError("No hay Assessment guardado.");
            Terminal.WriteDim("Ejecuta 'condor analizar' primero para analizar el entorno.");
            return 1;
        }

        var result = new ModelRecommender().Recommend(assessment, PurposeToInternal(purpose));

        Terminal.WriteLine();
        Terminal.WriteInfo("RECOMENDACION (" + PurposeToPublic(result.Purpose) + ")");
        Terminal.WriteLine();

        if (result.HasRecommendation && result.Recommended is not null)
        {
            var recommended = result.Recommended;
            Terminal.WriteDim("Modelo recomendado: " + recommended.Model.Name);
            Terminal.WriteLine();
            RenderReasons(recommended);
            Terminal.WriteLine();

            if (result.Alternatives.Count > 0)
            {
                Terminal.WriteInfo("Alternativas:");
                for (var i = 0; i < result.Alternatives.Count; i++)
                {
                    var alternative = result.Alternatives[i];
                    Terminal.WriteLine("  " + (i + 1) + ". " + alternative.Model.Name + " (" + FormatScore(alternative.Score) + ")");
                }
                Terminal.WriteLine();
            }
        }

        if (result.Excluded.Count > 0)
        {
            Terminal.WriteInfo("Descartados por compatibilidad:");
            foreach (var excluded in result.Excluded)
            {
                var reason = excluded.Reasons.Count > 0 ? excluded.Reasons[excluded.Reasons.Count - 1] : "";
                Terminal.WriteDim("  " + excluded.Model.Name + " - " + reason);
            }
            Terminal.WriteLine();
        }

        if (result.Limitations.Count > 0)
        {
            Terminal.WriteInfo("Limitaciones:");
            foreach (var limitation in result.Limitations)
            {
                Terminal.WriteDim("  - " + limitation);
            }
            Terminal.WriteLine();
        }

        var inputs = result.Inputs;
        Terminal.WriteDim(string.Format(
            CultureInfo.InvariantCulture,
            "Entrada: {0:0.0} GB RAM ({1:0.0} libres) | GPU: {2} | Modelos: {3}",
            inputs.RamTotalGb,
            inputs.RamFreeGb,
            inputs.GpuDetected ? "SI" : "NO",
            inputs.ModelsCount));

        return result.HasRecommendation ? 0 : 1;
    }

    private static string? ParsePurpose(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], "--proposito", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (i + 1 >= args.Length)
            {
                return null;
            }

            var value = args[i + 1].ToLowerInvariant();
            return AllowedPurposes.Contains(value) ? value : null;
        }

        return "desarrollo";
    }

    private static string PurposeToInternal(string value)
    {
        return string.Equals(value, "desarrollo", StringComparison.OrdinalIgnoreCase)
            ? "development"
            : value;
    }

    private static string PurposeToPublic(string value)
    {
        return string.Equals(value, "development", StringComparison.OrdinalIgnoreCase)
            ? "desarrollo"
            : value;
    }

    private static void RenderReasons(ModelRecommendationEntry entry)
    {
        Terminal.WriteDim("    Motivos:");
        foreach (var reason in entry.Reasons)
        {
            Terminal.WriteDim("      - " + reason);
        }
    }

    private static string FormatScore(double score)
    {
        return score.ToString("0.0", CultureInfo.InvariantCulture);
    }
}
