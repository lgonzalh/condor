using System;
using System.Collections.Generic;
using System.Linq;
using Condor.Core.Models;

namespace Condor.Core.Semantic;

public static class SemanticVerifier
{
    public const string ReasonDotNetMissing =
        "No se detecto la herramienta dotnet. Usa 'condor preparar' para revisar el entorno.";

    public const string ReasonNoManifest =
        "No se encontro un manifiesto .NET (.sln o .csproj) dentro del WorkingDirectory.";

    public const string ReasonNotSupported =
        "El proyecto no es de un tipo .NET soportado o no tiene un manifiesto aplicable.";

    public const string ReasonNotRestored =
        "Las dependencias no estan restauradas y el --no-restore impide la compilacion/prueba. Restaura manualmente las dependencias (externo a Condor).";

    public static bool IsDotNetAvailable(AssessmentResult? assessment)
    {
        return (assessment?.Tools?.DetectedTools ?? new List<ToolInfo>())
            .Any(t => string.Equals(t.Name, "dotnet", StringComparison.OrdinalIgnoreCase) &&
                      t.Status == DetectionStatus.Detected);
    }

    public static string? ResolveDotNetManifest(IReadOnlyList<string> candidateManifests)
    {
        var sln = candidateManifests
            .Where(m => m.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
            .OrderBy(m => m, StringComparer.Ordinal)
            .FirstOrDefault();

        if (sln is not null)
        {
            return sln;
        }

        var csproj = candidateManifests
            .Where(m => m.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .OrderBy(m => m, StringComparer.Ordinal)
            .FirstOrDefault();

        return csproj;
    }

    public static string[] BuildArguments(string manifest, string kind)
    {
        return kind == SemanticCheck.KindCompile
            ? new[] { manifest, "--no-restore" }
            : new[] { manifest, "--no-restore" };
    }

    public static string Classify(
        string kind,
        int? exitCode,
        bool timedOut,
        bool cancelled,
        bool executableStarted,
        bool notRestored)
    {
        if (cancelled)
        {
            return SemanticCheck.StatusCancelled;
        }

        if (timedOut)
        {
            return SemanticCheck.StatusTimeout;
        }

        if (!executableStarted)
        {
            return SemanticCheck.StatusNotExecutable;
        }

        if (notRestored)
        {
            return SemanticCheck.StatusNotAvailable;
        }

        return exitCode == 0 ? SemanticCheck.StatusCorrect : SemanticCheck.StatusFailed;
    }

    public static string Truncate(string value, int maxLength)
    {
        if (value is null)
        {
            return "";
        }

        if (value.Length <= maxLength)
        {
            return value;
        }

        return value.Substring(0, maxLength).TrimEnd();
    }
}
