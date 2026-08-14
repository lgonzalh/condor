using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Condor.Core.Models;
using Condor.Infrastructure.SemanticVerification;
using Condor.Infrastructure.State;

namespace Condor.Infrastructure.Tests;

public class SemanticVerificationServiceTests
{
    [Fact]
    public async Task VerifySemanticAsync_ProyectoValido_BuildCorrecto()
    {
        var proj = CrearProyectoNet(TemporalRoot(), errorCompilacion: false);
        RestaurarProyecto(proj);
        var store = new LocalStateStore(DirectorioTemporal());
        await store.SaveContextAsync(Contexto(proj));
        await store.SaveAssessmentAsync(AssessmentConDotnet());
        var service = new SemanticVerificationService(store);

        var result = await service.VerifySemanticAsync(true, false, CancellationToken.None);

        Assert.Equal(DetectionStatus.Detected, result.Status);
        Assert.Contains(result.Checks, c => c.Kind == SemanticCheck.KindCompile && c.Status == SemanticCheck.StatusCorrect);
        Assert.All(result.Checks, c => Assert.Contains("--no-restore", c.Command));
    }

    [Fact]
    public async Task VerifySemanticAsync_ProyectoConErrorCompilacion_Fallida()
    {
        var proj = CrearProyectoNet(TemporalRoot(), errorCompilacion: true);
        RestaurarProyecto(proj);
        var store = new LocalStateStore(DirectorioTemporal());
        await store.SaveContextAsync(Contexto(proj));
        await store.SaveAssessmentAsync(AssessmentConDotnet());
        var service = new SemanticVerificationService(store);

        var result = await service.VerifySemanticAsync(true, false, CancellationToken.None);

        Assert.Equal(DetectionStatus.Limited, result.Status);
        Assert.Contains(result.Checks, c => c.Kind == SemanticCheck.KindCompile && c.Status == SemanticCheck.StatusFailed);
    }

    [Fact]
    public async Task VerifySemanticAsync_SinContexto_NotDetected()
    {
        var store = new LocalStateStore(DirectorioTemporal());
        var service = new SemanticVerificationService(store);

        var result = await service.VerifySemanticAsync(true, true, CancellationToken.None);

        Assert.Equal(DetectionStatus.NotDetected, result.Status);
    }

    [Fact]
    public async Task VerifySemanticAsync_SinDotnet_NotAvailable()
    {
        var proj = CrearProyectoNet(TemporalRoot(), errorCompilacion: false);
        var store = new LocalStateStore(DirectorioTemporal());
        await store.SaveContextAsync(Contexto(proj));
        await store.SaveAssessmentAsync(AssessmentSinDotnet());
        var service = new SemanticVerificationService(store);

        var result = await service.VerifySemanticAsync(true, true, CancellationToken.None);

        Assert.Equal(DetectionStatus.Limited, result.Status);
        Assert.Contains(result.Checks, c => c.Status == SemanticCheck.StatusNotAvailable);
    }

    [Fact]
    public async Task VerifySemanticAsync_SinManifiesto_NoSoportado()
    {
        var dir = Path.Combine(TemporalRoot(), "vacio");
        Directory.CreateDirectory(dir);
        var store = new LocalStateStore(DirectorioTemporal());
        await store.SaveContextAsync(Contexto(dir));
        await store.SaveAssessmentAsync(AssessmentConDotnet());
        var service = new SemanticVerificationService(store);

        var result = await service.VerifySemanticAsync(true, true, CancellationToken.None);

        Assert.Equal(DetectionStatus.Limited, result.Status);
        Assert.Contains(result.Checks, c => c.Status == SemanticCheck.StatusNotSupported);
    }

    [Fact]
    public async Task VerifySemanticAsync_Cancelacion_Cancelada()
    {
        var proj = CrearProyectoNet(TemporalRoot(), errorCompilacion: false);
        RestaurarProyecto(proj);
        var store = new LocalStateStore(DirectorioTemporal());
        await store.SaveContextAsync(Contexto(proj));
        await store.SaveAssessmentAsync(AssessmentConDotnet());
        var service = new SemanticVerificationService(store);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await service.VerifySemanticAsync(true, true, cts.Token);

        Assert.NotEqual(DetectionStatus.Detected, result.Status);
        Assert.DoesNotContain(result.Checks, c => c.Status == SemanticCheck.StatusFailed);
    }

    private static string CrearProyectoNet(string root, bool errorCompilacion)
    {
        var dir = Path.Combine(root, "App");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "App.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(dir, "Program.cs"),
            errorCompilacion
                ? "class Program { void Main() { error_de_compilacion() } }"
                : "class Program { static void Main() { System.Console.WriteLine(\"hi\"); } }");
        return dir;
    }

    private static void RestaurarProyecto(string dir)
    {
        using var p = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = dir,
                UseShellExecute = false
            }
        };
        p.StartInfo.ArgumentList.Add("build");
        p.Start();
        p.WaitForExit();
    }

    private static ProjectContext Contexto(string workingDirectory)
    {
        return new ProjectContext
        {
            Status = DetectionStatus.Detected,
            WorkingDirectory = workingDirectory,
            RootName = "app",
            Risks = new List<ContextRisk>(),
            RelevantDependencies = new List<RelevantDependency>(),
            Recommendations = new List<PlannerRecommendation>(),
            LimitsApplied = new List<string>(),
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    private static AssessmentResult AssessmentConDotnet()
    {
        return new AssessmentResult
        {
            Tools = new ToolsProfile
            {
                DetectedTools = new List<ToolInfo>
                {
                    new() { Name = "dotnet", Status = DetectionStatus.Detected }
                }
            }
        };
    }

    private static AssessmentResult AssessmentSinDotnet()
    {
        return new AssessmentResult { Tools = new ToolsProfile { DetectedTools = new List<ToolInfo>() } };
    }

    private static string TemporalRoot()
    {
        return Path.Combine(Path.GetTempPath(), "condor-sem-obj-" + Guid.NewGuid().ToString("N"));
    }

    private static string DirectorioTemporal()
    {
        return Path.Combine(Path.GetTempPath(), "condor-sem-" + Guid.NewGuid().ToString("N"));
    }
}
