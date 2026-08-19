using Condor.Cli.Presentation;
using Condor.Core.Models;
using Condor.Core.Project;

namespace Condor.Cli.Presentation;

public static class ProjectAnalysisRenderer
{
    public static void Render(AssessmentResult result)
    {
        var project = result.Project;

        Terminal.WriteHeader("ANALISIS DE PROYECTO");

        if (project is null || project.Status == DetectionStatus.NotDetected)
        {
            Terminal.WriteWarning("  No se identifico un proyecto en este directorio.");
            Terminal.WriteDim("  Describe con tus palabras que quieres hacer y Condor explorara el contenido.");
            return;
        }

        Terminal.WriteLine("  Nombre     : " + project.RootName);
        Terminal.WriteLine("  Directorio : " + project.RootPath);
        Terminal.WriteLine("  Git        : " + GitLine(project));

        if (project.Git is { Status: DetectionStatus.Detected } && project.Git.Commits.Count > 0)
        {
            foreach (var commit in project.Git.Commits)
            {
                Terminal.WriteDim("    Cambio: " + commit.Hash + " " + commit.Subject);
            }
        }

        Terminal.WriteLine("  Lenguajes  : " + LenguajesLine(project));
        Terminal.WriteLine("  Frameworks : " + FrameworksLine(project));
        Terminal.WriteLine("  Manifiestos: " + ManifiestosLine(project));
        Terminal.WriteLine("  Docs       : " + DocumentacionLine(project));
        Terminal.WriteLine("  Estructura : " + EstructuraLine(project));

        var volumen = project.FilesCount + " archivos / " + project.DirectoriesCount + " directorios / " +
                      FormatoBytes(project.TotalSizeBytes);
        if (project.TotalSizeExceeded)
        {
            volumen += " (tamano maximo excedido)";
        }

        Terminal.WriteLine("  Volumen    : " + volumen);

        Terminal.WriteLine();
        Terminal.WriteInfo("INTENCION POSIBLE");
        Terminal.WriteDim("  " + InferIntent(project));

        if (project.LimitsApplied.Count > 0)
        {
            Terminal.WriteLine();
            Terminal.WriteInfo("LIMITES APLICADOS EN EL DESCUBRIMIENTO");
            Terminal.WriteDim("  " + string.Join(" | ", project.LimitsApplied));
        }

        if (project.Status != DetectionStatus.Detected && !string.IsNullOrWhiteSpace(project.Reason))
        {
            Terminal.WriteLine();
            Terminal.WriteWarning("  Descubrimiento: " + project.Reason);
        }
    }

    private static string InferIntent(ProjectProfile project)
    {
        if (project.Frameworks.Count > 0)
        {
            var framework = string.Join(", ", project.Frameworks.Select(f => f.Name));
            return "Se identifica una solucion basada en " + framework +
                   " que parece en desarrollo o mantenimiento activo. " +
                   "Describe que quieres mejorar, arreglar o construir y Condor continuara desde aqui.";
        }

        if (project.Languages.Count > 0)
        {
            var language = project.Languages.First().Name;
            return "El directorio contiene codigo " + language +
                   ". Indica que necesidad tienes (revisar, corregir, ampliar o crear) y Condor actuara sobre el proyecto.";
        }

        return "El directorio no muestra senales claras de codigo fuente. " +
               "Describe la intencion y Condor observara el contenido para orientarse.";
    }

    private static string GitLine(ProjectProfile project)
    {
        if (!project.IsGitRepository || project.Git is null)
        {
            return "no es un repositorio Git.";
        }

        var git = project.Git;
        if (git.Status == DetectionStatus.Error)
        {
            var motivo = !string.IsNullOrWhiteSpace(git.Reason) ? " (" + git.Reason + ")" : "";
            return "estado no disponible" + motivo + ".";
        }

        var parts = new List<string>
        {
            "rama " + (git.Branch ?? "(sin rama)"),
            git.IsDirty ? "estado sucio" : "estado limpio",
            "ultimos " + git.Commits.Count + " cambios"
        };

        return string.Join(" | ", parts) + ".";
    }

    private static string LenguajesLine(ProjectProfile project)
    {
        if (project.Languages.Count == 0)
        {
            return "no identificados (estructura desconocida o sin senales)";
        }

        var items = project.Languages.Select(language =>
            language.Name + (language.Primary ? "" : " (secundario)"));
        return string.Join(", ", items);
    }

    private static string FrameworksLine(ProjectProfile project)
    {
        if (project.Frameworks.Count == 0)
        {
            return "ninguno identificado";
        }

        return string.Join(", ", project.Frameworks.Select(f => f.Name));
    }

    private static string ManifiestosLine(ProjectProfile project)
    {
        if (project.Manifests.Count == 0)
        {
            return "ninguno";
        }

        return string.Join(", ", project.Manifests.Select(m => m.Path));
    }

    private static string DocumentacionLine(ProjectProfile project)
    {
        return project.Documentation.Count == 0
            ? "(sin documentacion)"
            : string.Join(", ", project.Documentation.Select(d => d.Path));
    }

    private static string EstructuraLine(ProjectProfile project)
    {
        var items = project.TopLevelDirectories.Concat(project.TopLevelFiles).Take(12).ToList();
        if (items.Count == 0)
        {
            return "(directorio vacio)";
        }

        var text = string.Join(", ", items);
        return project.TopLevelDirectories.Count + project.TopLevelFiles.Count > 12
            ? text + ", ..."
            : text;
    }

    private static string FormatoBytes(long bytes)
    {
        if (bytes >= 1024L * 1024 * 1024)
        {
            return (bytes / 1024.0 / 1024.0 / 1024.0).ToString("0.0") + " GB";
        }

        if (bytes >= 1024 * 1024)
        {
            return (bytes / 1024.0 / 1024.0).ToString("0.0") + " MB";
        }

        if (bytes >= 1024)
        {
            return (bytes / 1024.0).ToString("0.0") + " KB";
        }

        return bytes + " B";
    }
}
