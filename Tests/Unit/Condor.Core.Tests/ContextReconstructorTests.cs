using Condor.Core.Context;
using Condor.Core.Models;
using Condor.Core.Serialization;

namespace Condor.Core.Tests;

public class ContextReconstructorTests
{
    [Fact]
    public void ReconstruyeContexto_ConAssessmentYProyecto_GeneraResumenCompleto()
    {
        var assessment = AssessmentConProyecto();

        var context = ContextReconstructor.Reconstruct(
            assessment,
            Array.Empty<OperativeArtifact>(),
            ContextLimits.Default);

        Assert.Equal(DetectionStatus.Detected, context.Status);
        Assert.Equal("1.0.0", context.SchemaVersion);
        Assert.Equal("condor", context.RootName);
        Assert.Equal("C:\\proyecto\\condor", context.WorkingDirectory);
        Assert.Equal(new[] { "C#", "Python" }, context.Summary.Languages);
        Assert.Equal(new[] { "ASP.NET Core" }, context.Summary.Frameworks);
        Assert.Equal(2, context.Summary.ManifestCount);
        Assert.Equal(1, context.Summary.DocumentationCount);
        Assert.True(context.Summary.IsGitRepository);
        Assert.Equal("main", context.Summary.GitBranch);
        Assert.Single(context.Summary.LastCommits);
    }

    [Fact]
    public void ReconstruyeContexto_ConAssessmentSinProyecto_GeneraContextoParcialValido()
    {
        var assessment = new AssessmentResult
        {
            SchemaVersion = "1.0.0",
            WorkingDirectory = "C:\\proyecto",
            Project = null
        };

        var context = ContextReconstructor.Reconstruct(
            assessment,
            Array.Empty<OperativeArtifact>(),
            ContextLimits.Default);

        Assert.Equal(DetectionStatus.Detected, context.Status);
        Assert.Null(context.Reason);
        Assert.Equal("", context.RootName);
        Assert.False(context.Summary.IsGitRepository);
        Assert.Empty(context.Risks);
        Assert.Equal(DetectionStatus.NotDetected, context.ContinuationPoint!.Status);
        Assert.Contains("proyecto descubierto", context.ContinuationPoint!.Reason!);
    }

    [Fact]
    public void ReconstruyeContexto_SinAssessment_DevuelveNoDetectadoConMotivo()
    {
        var context = ContextReconstructor.Reconstruct(
            null,
            Array.Empty<OperativeArtifact>(),
            ContextLimits.Default);

        Assert.Equal(DetectionStatus.NotDetected, context.Status);
        Assert.Contains("condor analizar", context.Reason!);
        Assert.Null(context.ContinuationPoint);
    }

    [Fact]
    public void ContinuationPoint_DetectaSiguienteTareaEnArtefactoKanban()
    {
        var kanban = new OperativeArtifact
        {
            Kind = OperativeArtifactKind.Kanban,
            RelativePath = "operacion/KANBAN.md",
            Content = "# KANBAN\n\n## Siguiente\n\nT-006 Flujo de intencion a plan.\n",
            Status = DetectionStatus.Detected
        };

        var context = ContextReconstructor.Reconstruct(
            AssessmentConProyecto(),
            new[] { kanban },
            ContextLimits.Default);

        Assert.Equal(DetectionStatus.Detected, context.ContinuationPoint!.Status);
        Assert.Equal("T-006 Flujo de intencion a plan.", context.ContinuationPoint.SuggestedNext);
        Assert.Contains("operacion/KANBAN.md linea", context.ContinuationPoint.Evidence[0]);
    }

    [Fact]
    public void ContinuationPoint_DetectaTareasPendientesEnArtefactoBacklog()
    {
        var backlog = new OperativeArtifact
        {
            Kind = OperativeArtifactKind.Backlog,
            RelativePath = "operacion/BACKLOG.md",
            Content = "T-006 Flujo de intencion a plan  Pendiente\nT-007 Builder inicial  Pendiente\nT-008 Verificacion inicial  Completa\n",
            Status = DetectionStatus.Detected
        };

        var context = ContextReconstructor.Reconstruct(
            AssessmentConProyecto(),
            new[] { backlog },
            ContextLimits.Default);

        Assert.Equal(2, context.ContinuationPoint!.PendingWork.Count);
        Assert.Equal("T-006 Flujo de intencion a plan  Pendiente", context.ContinuationPoint.PendingWork[0]);
        Assert.Equal("T-007 Builder inicial  Pendiente", context.ContinuationPoint.PendingWork[1]);
    }

    [Fact]
    public void ContinuationPoint_UltimaActividadDesdeRegistroCambios()
    {
        var registro = new OperativeArtifact
        {
            Kind = OperativeArtifactKind.RegistroCambios,
            RelativePath = "operacion/REGISTRO_CAMBIOS.md",
            Content = "CH-012   T-004   Cierre\nCH-013   T-005   Formalizacion\n",
            Status = DetectionStatus.Detected
        };

        var context = ContextReconstructor.Reconstruct(
            AssessmentConProyecto(),
            new[] { registro },
            ContextLimits.Default);

        Assert.NotNull(context.ContinuationPoint!.LastActivity);
        Assert.Contains("CH-013", context.ContinuationPoint.LastActivity);
        Assert.Contains("CH-013", context.ContinuationPoint.Evidence[0]);
    }

    [Fact]
    public void ContinuationPoint_SinArtefactos_FallaAlUltimoCommitGit()
    {
        var context = ContextReconstructor.Reconstruct(
            AssessmentConProyecto(),
            Array.Empty<OperativeArtifact>(),
            ContextLimits.Default);

        Assert.Equal(DetectionStatus.Detected, context.ContinuationPoint!.Status);
        Assert.Contains("Git:", context.ContinuationPoint.LastActivity);
        Assert.Contains("a1b2c3d4", context.ContinuationPoint.LastActivity);
    }

    [Fact]
    public void ContinuationPoint_SinEvidencia_DevuelveNoDetectadoSinInventar()
    {
        var assessment = new AssessmentResult
        {
            SchemaVersion = "1.0.0",
            WorkingDirectory = "C:\\proyecto",
            Project = new ProjectProfile
            {
                Status = DetectionStatus.Detected,
                RootPath = "C:\\proyecto",
                RootName = "proyecto",
                IsGitRepository = false
            }
        };

        var context = ContextReconstructor.Reconstruct(
            assessment,
            Array.Empty<OperativeArtifact>(),
            ContextLimits.Default);

        Assert.Equal(DetectionStatus.NotDetected, context.ContinuationPoint!.Status);
        Assert.Null(context.ContinuationPoint.SuggestedNext);
        Assert.Empty(context.ContinuationPoint.PendingWork);
        Assert.NotNull(context.ContinuationPoint.Reason);
    }

    [Fact]
    public void Riesgos_DetectaCatalogoCompleto_YOrdenaPorSeveridad()
    {
        var assessment = AssessmentConProyecto();
        assessment.Project!.IsGitRepository = false;
        assessment.Project.Status = DetectionStatus.Limited;
        assessment.Project.Reason = "acceso denegado parcial";
        assessment.Project.TotalSizeExceeded = true;
        assessment.Project.DirectoriesCount = 4;
        assessment.Project.FilesCount = 10;
        assessment.Project.Languages = new List<LanguageEvidence>();
        assessment.Project.Documentation = new List<DocumentationInfo>();

        var context = ContextReconstructor.Reconstruct(
            assessment,
            Array.Empty<OperativeArtifact>(),
            ContextLimits.Default);

        Assert.Equal(new[] { "manifiesto-error", "perfil-degradado", "sin-git" },
            context.Risks.Take(3).Select(risk => risk.Kind));
        Assert.Contains(context.Risks, risk => risk.Kind == "sin-senales-lenguaje");
        Assert.Contains(context.Risks, risk => risk.Kind == "volumen-excedido");
        Assert.Contains(context.Risks, risk => risk.Kind == "documentacion-ausente");

        var ranks = context.Risks.Select(risk => SeverityRank(risk.Severity)).ToList();
        Assert.Equal(ranks.OrderByDescending(value => value), ranks);
    }

    [Fact]
    public void Riesgos_SinProyecto_NoGeneraRiesgos()
    {
        var assessment = new AssessmentResult
        {
            SchemaVersion = "1.0.0",
            WorkingDirectory = "C:\\proyecto",
            Project = null
        };

        var context = ContextReconstructor.Reconstruct(
            assessment,
            Array.Empty<OperativeArtifact>(),
            ContextLimits.Default);

        Assert.Empty(context.Risks);
    }

    [Fact]
    public void Dependencias_DeManifiestosYHerramientas_OrdenadasYSinDuplicados()
    {
        var assessment = AssessmentConProyecto();

        var context = ContextReconstructor.Reconstruct(
            assessment,
            Array.Empty<OperativeArtifact>(),
            ContextLimits.Default);

        var dependencias = context.RelevantDependencies.ToList();
        Assert.Equal(dependencias.OrderBy(dependency => dependency.Name, StringComparer.Ordinal),
            dependencias);
        Assert.Equal(4, dependencias.Select(dependency => dependency.Name).Distinct().Count());
        Assert.Contains(dependencias, dependency =>
            dependency.Name == "System.Text.Json" && dependency.Source == "Manifest");
        Assert.Contains(dependencias, dependency =>
            dependency.Name == "git" && dependency.Source == "Tools");
        Assert.Contains(dependencias, dependency =>
            dependency.Name == "Ollama" && dependency.Source == "Tools");
    }

    [Fact]
    public void Recomendaciones_DerivadasDeRiesgos_RespetanTope()
    {
        var assessment = AssessmentConProyecto();
        assessment.Project!.Status = DetectionStatus.Limited;
        assessment.Project.Reason = "error";
        assessment.Project.IsGitRepository = false;
        assessment.Project.TotalSizeExceeded = true;
        assessment.Project.Documentation = new List<DocumentationInfo>();
        assessment.Project.Languages = new List<LanguageEvidence>();

        var context = ContextReconstructor.Reconstruct(
            assessment,
            Array.Empty<OperativeArtifact>(),
            ContextLimits.Default);

        Assert.NotEmpty(context.Recommendations);
        Assert.True(context.Recommendations.Count <= ContextLimits.Default.MaxRecommendations);
        Assert.Contains(context.Recommendations, recommendation =>
            recommendation.Text.Contains("manifiestos", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ArtefactoDegradado_ContextoLimitedConMotivo()
    {
        var degradado = new OperativeArtifact
        {
            Kind = OperativeArtifactKind.Releve,
            RelativePath = "operacion/RELEVO.md",
            Status = DetectionStatus.Limited,
            Reason = "acceso denegado"
        };

        var context = ContextReconstructor.Reconstruct(
            AssessmentConProyecto(),
            new[] { degradado },
            ContextLimits.Default);

        Assert.Equal(DetectionStatus.Limited, context.Status);
        Assert.Contains("artifact-access", context.LimitsApplied);
        Assert.Contains("operacion/RELEVO.md", context.Reason!);
        Assert.False(context.Summary.HasOperativeArtifacts);
    }

    [Fact]
    public void ArtefactoExcesivo_DeclaraLimiteDeTamano()
    {
        var excesivo = new OperativeArtifact
        {
            Kind = OperativeArtifactKind.Backlog,
            RelativePath = "operacion/BACKLOG.md",
            Status = DetectionStatus.Limited,
            Reason = "supera el limite de tamano"
        };

        var context = ContextReconstructor.Reconstruct(
            AssessmentConProyecto(),
            new[] { excesivo },
            ContextLimits.Default);

        Assert.Contains(ContextLimits.LimitArtifactSize, context.LimitsApplied);
    }

    [Fact]
    public void Pendientes_MasDelTope_RespetaMaxPendingTasks()
    {
        var lines = new List<string>();
        for (var i = 0; i < 15; i++)
        {
            lines.Add("T-0" + (100 + i).ToString() + " Tarea pendiente " + i);
        }
        var backlog = new OperativeArtifact
        {
            Kind = OperativeArtifactKind.Backlog,
            RelativePath = "operacion/BACKLOG.md",
            Content = string.Join("\n", lines) + "\n",
            Status = DetectionStatus.Detected
        };

        var context = ContextReconstructor.Reconstruct(
            AssessmentConProyecto(),
            new[] { backlog },
            ContextLimits.Default);

        Assert.True(context.ContinuationPoint!.PendingWork.Count <= ContextLimits.Default.MaxPendingTasks);
    }

    [Fact]
    public void LineasPorArtefacto_SuperaLimite400_DeclaraLimiteYNoEscaneaMasAlla()
    {
        var lines = new List<string>();
        for (var i = 0; i < 450; i++)
        {
            lines.Add("linea con contenido de relleno " + i);
        }
        lines.Add("T-099 Tarea pendiente mas alla del limite");
        var backlog = new OperativeArtifact
        {
            Kind = OperativeArtifactKind.Backlog,
            RelativePath = "operacion/BACKLOG.md",
            Content = string.Join("\n", lines) + "\n",
            Status = DetectionStatus.Detected
        };

        var context = ContextReconstructor.Reconstruct(
            AssessmentConProyecto(),
            new[] { backlog },
            ContextLimits.Default);

        Assert.Contains(ContextLimits.LimitLines, context.LimitsApplied);
        Assert.DoesNotContain(context.ContinuationPoint!.PendingWork,
            tarea => tarea.Contains("T-099", StringComparison.Ordinal));
    }

    [Fact]
    public void Determinismo_DosReconstrucciones_ProducenElMismoContexto()
    {
        var artifacts = new[]
        {
            new OperativeArtifact
            {
                Kind = OperativeArtifactKind.Backlog,
                RelativePath = "operacion/BACKLOG.md",
                Content = "T-006 Flujo de intencion a plan  Pendiente\nT-007 Builder inicial  Pendiente\n",
                Status = DetectionStatus.Detected
            }
        };

        var first = ContextReconstructor.Reconstruct(
            AssessmentConProyecto(),
            artifacts,
            ContextLimits.Default);
        first.GeneratedAtUtc = DateTime.MinValue;

        var second = ContextReconstructor.Reconstruct(
            AssessmentConProyecto(),
            artifacts,
            ContextLimits.Default);
        second.GeneratedAtUtc = DateTime.MinValue;

        Assert.Equal(ContextJson.Serialize(first), ContextJson.Serialize(second));
    }

    private static AssessmentResult AssessmentConProyecto()
    {
        return new AssessmentResult
        {
            SchemaVersion = "1.0.0",
            WorkingDirectory = "C:\\proyecto\\condor",
            Tools = new ToolsProfile
            {
                Git = new ToolInfo { Name = "git", Status = DetectionStatus.Detected, Version = "2.40.0" },
                Ollama = new OllamaStatus { Installed = true, ServerRunning = true }
            },
            Project = new ProjectProfile
            {
                Status = DetectionStatus.Detected,
                RootPath = "C:\\proyecto\\condor",
                RootName = "condor",
                IsGitRepository = true,
                Git = new GitProjectState
                {
                    Branch = "main",
                    IsDirty = false,
                    Status = DetectionStatus.Detected,
                    Commits = new List<GitCommitSummary>
                    {
                        new() { Hash = "a1b2c3d4", Subject = "Update RELEVO.md" }
                    }
                },
                Languages = new List<LanguageEvidence>
                {
                    new() { Name = "Python" },
                    new() { Name = "C#" }
                },
                Frameworks = new List<FrameworkEvidence>
                {
                    new() { Name = "ASP.NET Core", Signal = "Sdk web", Manifest = "condor.csproj" }
                },
                Manifests = new List<ManifestInfo>
                {
                    new()
                    {
                        Kind = "csproj",
                        Path = "condor.csproj",
                        Name = "condor",
                        Dependencies = new List<string> { "System.Text.Json" }
                    },
                    new()
                    {
                        Kind = "requirements.txt",
                        Path = "requirements.txt",
                        Dependencies = new List<string> { "pytest" },
                        ParseError = true
                    }
                },
                Documentation = new List<DocumentationInfo>
                {
                    new() { Kind = "README", Path = "README.md" }
                },
                FilesCount = 10,
                DirectoriesCount = 4
            }
        };
    }

    private static int SeverityRank(string severity)
    {
        return severity switch
        {
            "alta" => 3,
            "baja" => 1,
            _ => 2
        };
    }
}