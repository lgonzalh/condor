using System;
using System.Collections.Generic;
using System.Linq;
using Condor.Core.Building;
using Condor.Core.Models;
using Condor.Core.Serialization;

namespace Condor.Core.Tests;

public class BuildDeriverTests
{
    [Fact]
    public void Deriva_SinPlan_DevuelveNotDetectedConMotivoInstructivo()
    {
        var result = BuildDeriver.Derive(null, BuildLimits.Default);

        Assert.Equal(DetectionStatus.NotDetected, result.Status);
        Assert.Contains("condor planear", result.Reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Deriva_PlanNotDetected_DegradaANotDetected()
    {
        var plan = PlanConDetectado(DetectionStatus.NotDetected, PlanTaskConRuta("modificar", "src/X.cs"));

        var result = BuildDeriver.Derive(plan, BuildLimits.Default);

        Assert.Equal(DetectionStatus.NotDetected, result.Status);
    }

    [Fact]
    public void Deriva_PlanLimited_DegradaALimited()
    {
        var plan = PlanConDetectado(DetectionStatus.Limited, PlanTaskConRuta("crear", "src/Nuevo.cs"));

        var result = BuildDeriver.Derive(plan, BuildLimits.Default);

        Assert.Equal(DetectionStatus.Limited, result.Status);
    }

    [Fact]
    public void Deriva_PlanSinTareasDerivables_DegradaALimited()
    {
        var plan = PlanConDetectado(DetectionStatus.Detected, new PlanTask
        {
            Id = "T0",
            Title = "Refactorizar el nucleo",
            Detail = "Mejorar la cohesion sin declarar rutas"
        });

        var result = BuildDeriver.Derive(plan, BuildLimits.Default);

        Assert.Equal(DetectionStatus.Limited, result.Status);
        Assert.Contains("ruta", result.Reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Deriva_TareaCrear_GeneraAccionCrearConContenido()
    {
        var plan = PlanConDetectado(DetectionStatus.Detected,
            PlanTaskConRuta("crear un modelo nuevo", "Models/Usuario.cs"));

        var result = BuildDeriver.Derive(plan, BuildLimits.Default);

        Assert.Equal(DetectionStatus.Detected, result.Status);
        Assert.Single(result.Actions);
        Assert.Equal(BuildActionKind.Crear, result.Actions[0].Kind);
        Assert.Equal("Models/Usuario.cs", result.Actions[0].RelativePath);
        Assert.Equal("B0", result.Actions[0].Id);
    }

    [Fact]
    public void Deriva_TareaModificar_GeneraAccionActualizar()
    {
        var plan = PlanConDetectado(DetectionStatus.Detected,
            PlanTaskConRuta("modificar el servicio", "Services/ClienteService.cs"));

        var result = BuildDeriver.Derive(plan, BuildLimits.Default);

        Assert.Equal(BuildActionKind.Actualizar, result.Actions[0].Kind);
    }

    [Fact]
    public void Deriva_MultiplesTareas_ConservaOrdenOrdinal()
    {
        var plan = PlanConDetectado(DetectionStatus.Detected,
            PlanTaskConRuta("crear contrato", "Contracts/IUsuario.cs"),
            PlanTaskConRuta("crear modelo", "Models/Usuario.cs"));

        var result = BuildDeriver.Derive(plan, BuildLimits.Default);

        Assert.Equal(2, result.Actions.Count);
        Assert.Equal("B0", result.Actions[0].Id);
        Assert.Equal("B1", result.Actions[1].Id);
    }

    [Theory]
    [InlineData("../fuera.cs")]
    [InlineData("./relativo.cs")]
    [InlineData("/absoluta.cs")]
    [InlineData("C:\\windows\\algo.cs")]
    [InlineData("C:/windows/algo.cs")]
    public void Deriva_RutaInvalida_NoGeneraAccion(string path)
    {
        var plan = PlanConDetectado(DetectionStatus.Detected,
            PlanTaskConRuta("crear archivo", path));

        var result = BuildDeriver.Derive(plan, BuildLimits.Default);

        Assert.Equal(DetectionStatus.Limited, result.Status);
    }

    [Fact]
    public void Deriva_LimiteDeAcciones_RespetaMaxActions()
    {
        var tasks = new List<PlanTask>();
        for (var i = 0; i < 40; i++)
        {
            tasks.Add(PlanTaskConRuta("crear archivo", "src/A" + i + ".cs"));
        }

        var plan = PlanConDetectado(DetectionStatus.Detected, tasks.ToArray());

        var result = BuildDeriver.Derive(plan, BuildLimits.Default);

        Assert.True(result.Actions.Count <= BuildLimits.Default.MaxActions);
        Assert.Contains(BuildLimits.LimitActions, result.LimitsApplied);
    }

    [Fact]
    public void Deriva_RutaExcedeMaxPathLength_OmiteYRutaDeclaraLimite()
    {
        var longPath = new string('c', BuildLimits.Default.MaxRelativePathLength + 1) + ".cs";
        var plan = PlanConDetectado(DetectionStatus.Detected,
            PlanTaskConRuta("crear archivo", longPath));

        var result = BuildDeriver.Derive(plan, BuildLimits.Default);

        Assert.Equal(DetectionStatus.Limited, result.Status);
    }

    [Fact]
    public void Determinismo_DosDerivaciones_ProducenElMismoResultado()
    {
        var plan = PlanConDetectado(DetectionStatus.Detected,
            PlanTaskConRuta("crear contrato", "Contracts/IUsuario.cs"),
            PlanTaskConRuta("crear modelo", "Models/Usuario.cs"));

        var first = BuildDeriver.Derive(plan, BuildLimits.Default);
        first.GeneratedAtUtc = DateTime.MinValue;

        var second = BuildDeriver.Derive(plan, BuildLimits.Default);
        second.GeneratedAtUtc = DateTime.MinValue;

        Assert.Equal(BuildJson.Serialize(first), BuildJson.Serialize(second));
    }

    private static WorkPlan PlanConDetectado(DetectionStatus status, params PlanTask[] tasks)
    {
        return new WorkPlan
        {
            SchemaVersion = "1.0.0",
            Status = status,
            WorkingDirectory = "C:\\proyecto",
            RootName = "condor",
            Intention = "modificar",
            Objective = "Mejorar el proyecto",
            Tasks = tasks.ToList(),
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    private static PlanTask PlanTaskConRuta(string title, string path)
    {
        return new PlanTask
        {
            Id = "T0",
            Title = title,
            Detail = "Tarea con [ruta:" + path + "] acotada.",
            Evidence = "origen-plan"
        };
    }
}
