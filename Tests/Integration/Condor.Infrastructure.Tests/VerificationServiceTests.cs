using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Condor.Core.Models;
using Condor.Core.Serialization;
using Condor.Infrastructure.State;
using Condor.Infrastructure.Verification;

namespace Condor.Infrastructure.Tests;

public class VerificationServiceTests
{
    [Fact]
    public async Task VerifyAsync_AccionAplicadaExistente_ResultaPasada()
    {
        var storeDirectory = DirectorioTemporal();
        var projectDirectory = DirectorioProyecto(storeDirectory);
        Directory.CreateDirectory(Path.Combine(projectDirectory, "Models"));
        File.WriteAllText(Path.Combine(projectDirectory, "Models", "Perfil.cs"), "crear modelo");
        var store = new LocalStateStore(storeDirectory);
        await store.SaveBuildAsync(BuildConAccion(projectDirectory, AccionAplicada("B0", "Models/Perfil.cs")));
        await store.SaveContextAsync(Contexto(projectDirectory));
        var service = new VerificationService(store);

        var result = await service.VerifyAsync(CancellationToken.None);

        Assert.Equal(DetectionStatus.Detected, result.Status);
        Assert.Equal(1, result.Passed);
        Assert.Equal(0, result.Failed);
        Assert.Equal(VerificationCheck.StatusPassed, result.Checks[0].Status);
    }

    [Fact]
    public async Task VerifyAsync_AccionAplicadaInexistente_ResultaFallida()
    {
        var storeDirectory = DirectorioTemporal();
        var projectDirectory = DirectorioProyecto(storeDirectory);
        Directory.CreateDirectory(projectDirectory);
        var store = new LocalStateStore(storeDirectory);
        await store.SaveBuildAsync(BuildConAccion(projectDirectory, AccionAplicada("B0", "NoExiste.cs")));
        await store.SaveContextAsync(Contexto(projectDirectory));
        var service = new VerificationService(store);

        var result = await service.VerifyAsync(CancellationToken.None);

        Assert.Equal(1, result.Failed);
        Assert.Equal(VerificationCheck.StatusFailed, result.Checks[0].Status);
    }

    [Fact]
    public async Task VerifyAsync_AccionOmitida_ResultaInformativa()
    {
        var storeDirectory = DirectorioTemporal();
        var projectDirectory = DirectorioProyecto(storeDirectory);
        Directory.CreateDirectory(projectDirectory);
        var store = new LocalStateStore(storeDirectory);
        await store.SaveBuildAsync(BuildConAccion(projectDirectory, AccionOmitida("B0", "A.cs")));
        await store.SaveContextAsync(Contexto(projectDirectory));
        var service = new VerificationService(store);

        var result = await service.VerifyAsync(CancellationToken.None);

        Assert.Equal(1, result.Informative);
        Assert.Equal(VerificationCheck.StatusInformative, result.Checks[0].Status);
    }

    [Fact]
    public async Task VerifyAsync_SinBuild_DevuelveNotDetected()
    {
        var store = new LocalStateStore(DirectorioTemporal());
        var service = new VerificationService(store);

        var result = await service.VerifyAsync(CancellationToken.None);

        Assert.Equal(DetectionStatus.NotDetected, result.Status);
        Assert.Contains("condor construir", result.Reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VerifyAsync_UsaWorkingDirectoryDelBuild()
    {
        var storeDirectory = DirectorioTemporal();
        var projectDirectory = DirectorioProyecto(storeDirectory);
        Directory.CreateDirectory(Path.Combine(projectDirectory, "Models"));
        File.WriteAllText(Path.Combine(projectDirectory, "Models", "Perfil.cs"), "crear modelo");
        var store = new LocalStateStore(storeDirectory);
        await store.SaveBuildAsync(BuildConAccion(projectDirectory, AccionAplicada("B0", "Models/Perfil.cs")));
        await store.SaveContextAsync(Contexto(projectDirectory));
        var service = new VerificationService(store);

        var result = await service.VerifyAsync(CancellationToken.None);

        Assert.Equal(projectDirectory, result.WorkingDirectory);
        Assert.Equal(1, result.Passed);
    }

    [Fact]
    public async Task VerifyAsync_SinWorkingDirectoryBuildNiContexto_DegradaALimited()
    {
        var storeDirectory = DirectorioTemporal();
        var projectDirectory = DirectorioProyecto(storeDirectory);
        Directory.CreateDirectory(projectDirectory);
        var store = new LocalStateStore(storeDirectory);
        await store.SaveBuildAsync(BuildConAccion("", AccionAplicada("B0", "A.cs")));
        var service = new VerificationService(store);

        var result = await service.VerifyAsync(CancellationToken.None);

        Assert.Equal(DetectionStatus.Limited, result.Status);
    }

    [Fact]
    public async Task SaveVerificationAsync_PersisteVerificationJson()
    {
        var storeDirectory = DirectorioTemporal();
        var projectDirectory = DirectorioProyecto(storeDirectory);
        Directory.CreateDirectory(projectDirectory);
        var store = new LocalStateStore(storeDirectory);
        var result = new VerificationResult
        {
            SchemaVersion = "1.0.0",
            Status = DetectionStatus.Detected,
            WorkingDirectory = projectDirectory,
            GeneratedAtUtc = DateTime.UtcNow
        };
        await store.SaveVerificationAsync(result);

        var persisted = await new LocalStateStore(storeDirectory).LoadVerificationAsync();

        Assert.NotNull(persisted);
        Assert.Equal("1.0.0", persisted.SchemaVersion);
    }

    [Fact]
    public async Task Determinismo_DosEjecuciones_ProducenElMismoResultado()
    {
        var storeDirectory = DirectorioTemporal();
        var projectDirectory = DirectorioProyecto(storeDirectory);
        Directory.CreateDirectory(Path.Combine(projectDirectory, "Models"));
        File.WriteAllText(Path.Combine(projectDirectory, "Models", "Perfil.cs"), "crear modelo");
        var store = new LocalStateStore(storeDirectory);
        await store.SaveBuildAsync(BuildConAccion(projectDirectory, AccionAplicada("B0", "Models/Perfil.cs")));
        await store.SaveContextAsync(Contexto(projectDirectory));
        var service = new VerificationService(store);

        var first = await service.VerifyAsync(CancellationToken.None);
        var second = await service.VerifyAsync(CancellationToken.None);

        first.GeneratedAtUtc = DateTime.MinValue;
        second.GeneratedAtUtc = DateTime.MinValue;

        Assert.Equal(VerificationJson.Serialize(first), VerificationJson.Serialize(second));
    }

    private static BuildResult BuildConAccion(string workingDirectory, BuildAction action)
    {
        return new BuildResult
        {
            SchemaVersion = "1.0.0",
            Status = DetectionStatus.Detected,
            WorkingDirectory = workingDirectory,
            RootName = "condor",
            Objective = "Verificar cambios",
            Actions = new List<BuildAction> { action },
            Applied = 1,
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    private static ProjectContext Contexto(string workingDirectory)
    {
        return new ProjectContext
        {
            SchemaVersion = "1.0.0",
            Status = DetectionStatus.Detected,
            WorkingDirectory = workingDirectory,
            RootName = "condor",
            Risks = new List<ContextRisk>(),
            RelevantDependencies = new List<RelevantDependency>(),
            Recommendations = new List<PlannerRecommendation>(),
            LimitsApplied = new List<string>(),
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    private static BuildAction AccionAplicada(string id, string path)
    {
        return new BuildAction
        {
            Id = id,
            Kind = BuildActionKind.Crear,
            RelativePath = path,
            Content = "crear modelo",
            Status = BuildAction.StatusApplied,
            Evidence = "e2e"
        };
    }

    private static BuildAction AccionOmitida(string id, string path)
    {
        return new BuildAction
        {
            Id = id,
            Kind = BuildActionKind.Crear,
            RelativePath = path,
            Content = "x",
            Status = BuildAction.StatusOmitted,
            StatusReason = "El archivo ya existe",
            Evidence = "e2e"
        };
    }

    private static string DirectorioProyecto(string storeDirectory)
    {
        return Path.Combine(storeDirectory, "proyecto");
    }

    private static string DirectorioTemporal()
    {
        return Path.Combine(Path.GetTempPath(), "condor-verify-" + Guid.NewGuid().ToString("N"));
    }
}
