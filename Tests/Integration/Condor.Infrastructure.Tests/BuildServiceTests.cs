using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Condor.Core.Models;
using Condor.Core.Serialization;
using Condor.Infrastructure.Building;
using Condor.Infrastructure.State;

namespace Condor.Infrastructure.Tests;

public class BuildServiceTests
{
    [Fact]
    public async Task ApplyPlanAsync_ConPlanConCrear_AplicaArchivoYSumariza()
    {
        var storeDirectory = DirectorioTemporal();
        var projectDirectory = DirectorioProyecto(storeDirectory);
        var store = new LocalStateStore(storeDirectory);
        await store.SavePlanAsync(PlanConTarea("crear modelo", "Models/Perfil.cs", projectDirectory));
        var service = new BuildService(store);

        var result = await service.ApplyPlanAsync(CancellationToken.None);

        Assert.Equal(DetectionStatus.Detected, result.Status);
        Assert.Equal(1, result.Applied);
        Assert.Equal(0, result.Failed);
        Assert.True(File.Exists(Path.Combine(projectDirectory, "Models", "Perfil.cs")), "deberia crear el archivo");
    }

    [Fact]
    public async Task ApplyPlanAsync_ConPlanConActualizar_SobrescribeArchivoExistente()
    {
        var storeDirectory = DirectorioTemporal();
        var projectDirectory = DirectorioProyecto(storeDirectory);
        var target = Path.Combine(projectDirectory, "Data.txt");
        Directory.CreateDirectory(projectDirectory);
        File.WriteAllText(target, "contenido original");
        var store = new LocalStateStore(storeDirectory);
        await store.SavePlanAsync(PlanConTarea("modificar archivo", "Data.txt", projectDirectory));
        var service = new BuildService(store);

        var result = await service.ApplyPlanAsync(CancellationToken.None);

        Assert.Equal(DetectionStatus.Detected, result.Status);
        Assert.Equal(1, result.Applied);
        Assert.Equal(BuildAction.StatusApplied, result.Actions[0].Status);
        Assert.NotEqual("contenido original", File.ReadAllText(target));
    }

    [Fact]
    public async Task ApplyPlanAsync_CrearSobreExistente_OmiteConMotivo()
    {
        var storeDirectory = DirectorioTemporal();
        var projectDirectory = DirectorioProyecto(storeDirectory);
        var target = Path.Combine(projectDirectory, "Existente.cs");
        Directory.CreateDirectory(projectDirectory);
        File.WriteAllText(target, "existente");
        var store = new LocalStateStore(storeDirectory);
        await store.SavePlanAsync(PlanConTarea("crear archivo", "Existente.cs", projectDirectory));
        var service = new BuildService(store);

        var result = await service.ApplyPlanAsync(CancellationToken.None);

        Assert.Equal(1, result.Omitted);
        Assert.Equal(0, result.Failed);
        Assert.Contains("ya existe", result.Actions[0].StatusReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApplyPlanAsync_ActualizarSobreInexistente_OmiteConMotivo()
    {
        var storeDirectory = DirectorioTemporal();
        var projectDirectory = DirectorioProyecto(storeDirectory);
        var store = new LocalStateStore(storeDirectory);
        await store.SavePlanAsync(PlanConTarea("modificar archivo", "NoExiste.cs", projectDirectory));
        var service = new BuildService(store);

        var result = await service.ApplyPlanAsync(CancellationToken.None);

        Assert.Equal(1, result.Omitted);
        Assert.Equal(0, result.Failed);
        Assert.False(File.Exists(Path.Combine(projectDirectory, "NoExiste.cs")));
    }

    [Fact]
    public async Task ApplyPlanAsync_AccionConTraversal_SeRechazaEnDerivacionSinEscribirFuera()
    {
        var storeDirectory = DirectorioTemporal();
        var projectDirectory = DirectorioProyecto(storeDirectory);
        Directory.CreateDirectory(projectDirectory);
        var outside = Path.Combine(storeDirectory, "fuera.txt");
        var store = new LocalStateStore(storeDirectory);
        await store.SavePlanAsync(PlanConTarea("crear archivo", "../../fuera.txt", projectDirectory));
        var service = new BuildService(store);

        var result = await service.ApplyPlanAsync(CancellationToken.None);

        Assert.Equal(DetectionStatus.Limited, result.Status);
        Assert.Empty(result.Actions);
        Assert.False(File.Exists(outside));
    }

    [Fact]
    public async Task ApplyPlanAsync_SinPlan_DevuelveNotDetected()
    {
        var store = new LocalStateStore(DirectorioTemporal());
        var service = new BuildService(store);

        var result = await service.ApplyPlanAsync(CancellationToken.None);

        Assert.Equal(DetectionStatus.NotDetected, result.Status);
        Assert.Contains("condor planear", result.Reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApplyPlanAsync_PlanLimited_DejaEnLimitedSinEscribir()
    {
        var storeDirectory = DirectorioTemporal();
        var projectDirectory = DirectorioProyecto(storeDirectory);
        Directory.CreateDirectory(projectDirectory);
        var store = new LocalStateStore(storeDirectory);
        await store.SavePlanAsync(new WorkPlan
        {
            SchemaVersion = "1.0.0",
            Status = DetectionStatus.Limited,
            WorkingDirectory = projectDirectory,
            RootName = "condor",
            Tasks = new List<PlanTask>()
        });
        var service = new BuildService(store);

        var result = await service.ApplyPlanAsync(CancellationToken.None);

        Assert.Equal(DetectionStatus.Limited, result.Status);
    }

    [Fact]
    public async Task SaveBuildAsync_PersisteBuildJson()
    {
        var storeDirectory = DirectorioTemporal();
        var projectDirectory = DirectorioProyecto(storeDirectory);
        var store = new LocalStateStore(storeDirectory);
        await store.SavePlanAsync(PlanConTarea("crear modelo", "Models/Perfil.cs", projectDirectory));
        var service = new BuildService(store);

        var result = await service.ApplyPlanAsync(CancellationToken.None);
        var store2 = new LocalStateStore(storeDirectory);
        await store2.SaveBuildAsync(result);
        var persisted = await store2.LoadBuildAsync();

        Assert.NotNull(persisted);
        Assert.Equal("1.0.0", persisted.SchemaVersion);
    }

    [Fact]
    public async Task Determinismo_DosEjecuciones_ProducenElMismoBuild()
    {
        var storeDirectory = DirectorioTemporal();
        var projectDirectory = DirectorioProyecto(storeDirectory);
        var store = new LocalStateStore(storeDirectory);
        await store.SavePlanAsync(PlanConTarea("crear modelo", "Models/Perfil.cs", projectDirectory));
        var service = new BuildService(store);

        var first = await service.ApplyPlanAsync(CancellationToken.None);

        var createdFile = Path.Combine(projectDirectory, "Models", "Perfil.cs");
        if (File.Exists(createdFile))
        {
            File.Delete(createdFile);
        }

        var secondStore = new LocalStateStore(storeDirectory);
        await secondStore.SavePlanAsync(PlanConTarea("crear modelo", "Models/Perfil.cs", projectDirectory));
        var secondService = new BuildService(secondStore);
        var second = await secondService.ApplyPlanAsync(CancellationToken.None);

        first.GeneratedAtUtc = DateTime.MinValue;
        second.GeneratedAtUtc = DateTime.MinValue;

        Assert.Equal(BuildJson.Serialize(first), BuildJson.Serialize(second));
    }

    private static WorkPlan PlanConTarea(string title, string relativePath, string workingDirectory)
    {
        return new WorkPlan
        {
            SchemaVersion = "1.0.0",
            Status = DetectionStatus.Detected,
            WorkingDirectory = workingDirectory,
            RootName = "condor",
            Intention = "modificar",
            Objective = "Aplicar cambios",
            Tasks = new List<PlanTask>
            {
                new()
                {
                    Id = "T0",
                    Title = title,
                    Detail = "Tarea con [ruta:" + relativePath + "].",
                    Evidence = "origen-plan"
                }
            },
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    private static string DirectorioProyecto(string storeDirectory)
    {
        return Path.Combine(storeDirectory, "proyecto");
    }

    private static string DirectorioTemporal()
    {
        return Path.Combine(Path.GetTempPath(), "condor-build-" + Guid.NewGuid().ToString("N"));
    }
}
