using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Condor.Cli.Commands;
using Condor.Core.Models;
using Condor.Infrastructure.SemanticVerification;
using Condor.Infrastructure.State;

namespace Condor.Infrastructure.Tests;

public class CheckCommandTests
{
    [Fact]
    public async Task ExecuteAsync_ProyectoValido_DevuelveExitCodeCero()
    {
        var dir = Path.Combine(DirectorioTemporalRoot(), "App");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "App.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(dir, "Program.cs"), "class Program { static void Main() {} }");
        Restaurar(dir);
        var store = new LocalStateStore(DirectorioTemporal());
        await store.SaveContextAsync(Contexto(dir));
        await store.SaveAssessmentAsync(AssessmentConDotnet());
        var service = new SemanticVerificationService(store);

        var exitCode = await CheckCommand.ExecuteAsync(service, store, new[] { "--compilar", "--json" }, CancellationToken.None);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task ExecuteAsync_SinContexto_DevuelveExitCodeUno()
    {
        var store = new LocalStateStore(DirectorioTemporal());
        var service = new SemanticVerificationService(store);

        var exitCode = await CheckCommand.ExecuteAsync(service, store, new[] { "--json" }, CancellationToken.None);

        Assert.Equal(1, exitCode);
    }

    private static void Restaurar(string dir)
    {
        using var p = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo { FileName = "dotnet", WorkingDirectory = dir, UseShellExecute = false }
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
                DetectedTools = new List<ToolInfo> { new() { Name = "dotnet", Status = DetectionStatus.Detected } }
            }
        };
    }

    private static string DirectorioTemporalRoot()
    {
        return Path.Combine(Path.GetTempPath(), "condor-semcli-root-" + Guid.NewGuid().ToString("N"));
    }

    private static string DirectorioTemporal()
    {
        return Path.Combine(Path.GetTempPath(), "condor-semcli-" + Guid.NewGuid().ToString("N"));
    }
}
