using Condor.Core.Models;
using Condor.Infrastructure.Agent;

namespace Condor.Infrastructure.Tests;

public class AgentHarnessTests
{
    [Fact]
    public async Task VerifyAsync_ConRestauracionYDefecto_FallaHonestamente()
    {
        var project = TempProject("harnessdefecto");

        // Proyecto con defecto de logica: compila, pero el test falla.
        var harness = new AgentHarness(project.Root, AgentLimits.Default, new List<AgentStep>());
        var result = await harness.VerifyAsync(CancellationToken.None);

        Assert.False(result.Done);
        Assert.True(
            (result.Reason ?? "").Contains("Pruebas fallaron", StringComparison.OrdinalIgnoreCase) ||
            (result.Reason ?? "").Contains("Build fallo", StringComparison.OrdinalIgnoreCase),
            "Se espera un fallo honesto de harness (build o test). Razón: " + result.Reason);
        Assert.False(string.IsNullOrWhiteSpace(result.Detail));
    }

    [Fact]
    public async Task VerifyAsync_ProyectoCorregido_ConfirmaBuildYTest()
    {
        var project = TempProject("harnessok");
        File.WriteAllText(Path.Combine(project.Root, "Calc", "Calc.cs"),
            "namespace Calc;\npublic static class Ops\n{\n    public static int Sum(int a, int b) => a + b;\n}\n");
        File.WriteAllText(Path.Combine(project.Root, "Calc.Tests", "Test.cs"),
            "using Xunit;\nusing Calc;\n\npublic class Tests\n{\n    [Fact]\n    public void Suma() => Assert.Equal(5, Ops.Sum(2, 3));\n}\n");

        var harness = new AgentHarness(project.Root, AgentLimits.Default, new List<AgentStep>());
        var result = await harness.VerifyAsync(CancellationToken.None);

        Assert.True(result.Done, result.Reason);
    }

    [Fact]
    public void Restores_SeDetectaCuandoFaltaProjectAssets()
    {
        // Verifica el umbral de deteccion de fallo por restauracion.
        var builder = typeof(AgentHarness).GetMethod("LooksLikeRestoreFailure",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

        Assert.NotNull(builder);
        Assert.Equal((object)true, builder.Invoke(null, new object[] { "error NETSDK1004: no se encuentra project.assets.json" }));
        Assert.Equal((object)false, builder.Invoke(null, new object[] { "error CS0106" }));
    }

    private static (string Root, string Manifest) TempProject(string name)
    {
        var root = Path.Combine(Path.GetTempPath(), "condor-harness-" + name + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Calc"));
        Directory.CreateDirectory(Path.Combine(root, "Calc.Tests"));

        File.WriteAllText(Path.Combine(root, "Calc", "Calc.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(root, "Calc", "Calc.cs"),
            "namespace Calc;\npublic static class Ops\n{\n    public static int Sum(int a, int b) => a - b;\n}\n");
        File.WriteAllText(Path.Combine(root, "Calc.Tests", "Calc.Tests.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><IsPackable>false</IsPackable></PropertyGroup><ItemGroup><PackageReference Include=\"Microsoft.NET.Test.Sdk\" Version=\"17.14.1\"/><PackageReference Include=\"xunit\" Version=\"2.9.3\"/><PackageReference Include=\"xunit.runner.visualstudio\" Version=\"3.1.4\"/></ItemGroup><ItemGroup><ProjectReference Include=\"..\\Calc\\Calc.csproj\"/></ItemGroup></Project>");
        File.WriteAllText(Path.Combine(root, "Calc.Tests", "Test.cs"),
            "using Xunit;\nusing Calc;\n\npublic class Tests\n{\n    [Fact]\n    public void Suma() => Assert.Equal(5, Ops.Sum(2, 3));\n}\n");
        File.WriteAllText(Path.Combine(root, "CondorHarness.sln"),
            "Microsoft Visual Studio Solution File, Format Version 12.00\n# Visual Studio Version 17\nProject(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"Calc\", \"Calc\\Calc.csproj\", \"{11111111-1111-1111-1111-111111111111}\"\nEndProject\nProject(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"Calc.Tests\", \"Calc.Tests\\Calc.Tests.csproj\", \"{22222222-2222-2222-2222-222222222222}\"\nEndProject\nGlobal\n\tGlobalSection(SolutionConfigurationPlatforms) = preSolution\n\t\tDebug|Any CPU = Debug|Any CPU\n\tEndGlobalSection\n\tGlobalSection(ProjectConfigurationPlatforms) = postSolution\n\t\t{11111111-1111-1111-1111-111111111111}.Debug|Any CPU.ActiveCfg = Debug|Any CPU\n\t\t{11111111-1111-1111-1111-111111111111}.Debug|Any CPU.Build.0 = Debug|Any CPU\n\t\t{22222222-2222-2222-2222-222222222222}.Debug|Any CPU.ActiveCfg = Debug|Any CPU\n\t\t{22222222-2222-2222-2222-222222222222}.Debug|Any CPU.Build.0 = Debug|Any CPU\n\tEndGlobalSection\nEndGlobal\n");

        return (root, "CondorHarness.sln");
    }
}
