using Condor.Core.Models;
using Condor.Core.Planning;
using Condor.Core.Serialization;

namespace Condor.Core.Tests;

public class PlanGeneratorTests
{
    [Fact]
    public void GeneraPlan_IntencionNueva_DistingueNueva()
    {
        var context = ContextConProyecto();

        var plan = PlanGenerator.Generate(context, "crear una nueva aplicacion", PlanLimits.Default);

        Assert.Equal(DetectionStatus.Detected, plan.Status);
        Assert.Equal(PlanIntent.Nueva, plan.Intention);
        Assert.Contains("nueva aplicacion", plan.Objective, StringComparison.OrdinalIgnoreCase);
        Assert.True(plan.Tasks.Count > 0);
    }

    [Fact]
    public void GeneraPlan_IntencionContinuar_DistingueContinuarYUsaSiguienteTarea()
    {
        var context = ContextConProyecto();
        context.ContinuationPoint = new ContinuationPoint
        {
            Status = DetectionStatus.Detected,
            SuggestedNext = "T-006 Flujo de intencion a plan",
            Evidence = new List<string> { "operacion/KANBAN.md" }
        };

        var plan = PlanGenerator.Generate(context, "continuar el proyecto", PlanLimits.Default);

        Assert.Equal(PlanIntent.Continuar, plan.Intention);
        Assert.Contains("T-006", string.Join(" ", plan.Tasks.Select(t => t.Title)));
        Assert.Equal("T0", plan.Tasks[0].Id);
        Assert.Equal("T1", plan.Tasks[1].Id);
        Assert.Contains("T0", plan.Tasks[1].DependsOn);
    }

    [Fact]
    public void GeneraPlan_IntencionModificar_DistingueModificar()
    {
        var context = ContextConProyecto();

        var plan = PlanGenerator.Generate(context, "modificar el modulo de reportes", PlanLimits.Default);

        Assert.Equal(PlanIntent.Modificar, plan.Intention);
    }

    [Fact]
    public void GeneraPlan_IntencionIndefinida_DegradaALimited()
    {
        var context = ContextConProyecto();

        var plan = PlanGenerator.Generate(context, "proceder", PlanLimits.Default);

        Assert.Equal(DetectionStatus.Limited, plan.Status);
        Assert.Equal(PlanIntent.Indefinida, plan.Intention);
        Assert.NotNull(plan.Reason);
    }

    [Fact]
    public void GeneraPlan_Recomendaciones_SeConviertenEnTareasConEvidencia()
    {
        var context = ContextConProyecto();
        context.Recommendations.Add(new PlannerRecommendation
        {
            Text = "Revisa los manifiestos con error de parseo",
            Evidence = "package.json"
        });

        var plan = PlanGenerator.Generate(context, "modificar algo", PlanLimits.Default);

        Assert.Contains(plan.Tasks, t => t.Title.Contains("manifiestos", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(plan.Evidence, e => e.Contains("manifiestos", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GeneraPlan_Riesgos_SeConviertenEnTareasConPrioridad()
    {
        var context = ContextConProyecto();
        context.Risks.Add(new ContextRisk { Kind = "sin-git", Severity = "alta", Evidence = "raiz" });

        var plan = PlanGenerator.Generate(context, "continuar", PlanLimits.Default);

        Assert.Contains(plan.Tasks, t => t.Title.Contains("sin-git"));
        var riskTask = plan.Tasks.First(t => t.Title.Contains("sin-git"));
        Assert.Equal("alta", riskTask.Priority);
    }

    [Fact]
    public void GeneraPlan_LimiteDeTareas_RespetaMaxTasks()
    {
        var context = ContextConProyecto();
        for (var i = 0; i < 30; i++)
        {
            context.Recommendations.Add(new PlannerRecommendation { Text = "Recomendacion " + i, Evidence = "" });
        }

        var plan = PlanGenerator.Generate(context, "modificar", PlanLimits.Default);

        Assert.True(plan.Tasks.Count <= PlanLimits.Default.MaxTasks);
    }

    [Fact]
    public void GeneraPlan_SinContexto_DevuelveNotDetectedConMotivoInstructivo()
    {
        var plan = PlanGenerator.Generate(null, "crear algo", PlanLimits.Default);

        Assert.Equal(DetectionStatus.NotDetected, plan.Status);
        Assert.Contains("condor contexto", plan.Reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GeneraPlan_SolicitudVacia_DegradaALimited()
    {
        var plan = PlanGenerator.Generate(ContextConProyecto(), "   ", PlanLimits.Default);

        Assert.Equal(DetectionStatus.Limited, plan.Status);
        Assert.Contains("solicitud", plan.Reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Determinismo_DosGeneraciones_ProducenElMismoPlan()
    {
        var context = ContextConProyecto();
        context.Recommendations.Add(new PlannerRecommendation { Text = "Recomendacion A", Evidence = "x" });
        context.Risks.Add(new ContextRisk { Kind = "sin-git", Severity = "alta", Evidence = "raiz" });

        var first = PlanGenerator.Generate(context, "continuar el proyecto", PlanLimits.Default);
        first.GeneratedAtUtc = DateTime.MinValue;

        var second = PlanGenerator.Generate(context, "continuar el proyecto", PlanLimits.Default);
        second.GeneratedAtUtc = DateTime.MinValue;

        Assert.Equal(PlanJson.Serialize(first), PlanJson.Serialize(second));
    }

    [Fact]
    public void GeneraPlan_ColeccionesOrdenadasDeterministas()
    {
        var context = ContextConProyecto();
        context.Recommendations.Add(new PlannerRecommendation { Text = "Beta", Evidence = "" });
        context.Recommendations.Add(new PlannerRecommendation { Text = "Alfa", Evidence = "" });

        var plan = PlanGenerator.Generate(context, "modificar", PlanLimits.Default);

        Assert.Equal(plan.Evidence, plan.Evidence.OrderBy(e => e, StringComparer.Ordinal));
    }

    private static ProjectContext ContextConProyecto()
    {
        return new ProjectContext
        {
            SchemaVersion = "1.0.0",
            Status = DetectionStatus.Detected,
            WorkingDirectory = "C:\\proyecto",
            RootName = "condor",
            Summary = new ProjectContextSummary
            {
                Languages = new List<string> { "C#" },
                IsGitRepository = true,
                HasOperativeArtifacts = true
            },
            Risks = new List<ContextRisk>(),
            RelevantDependencies = new List<RelevantDependency>(),
            Recommendations = new List<PlannerRecommendation>(),
            LimitsApplied = new List<string>(),
            GeneratedAtUtc = DateTime.UtcNow
        };
    }
}
