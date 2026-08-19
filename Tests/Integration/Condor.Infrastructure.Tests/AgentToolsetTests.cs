using System.Threading.Tasks;
using Condor.Core.Models;
using Condor.Infrastructure.Agent;

namespace Condor.Infrastructure.Tests;

public class AgentToolsetTests
{
    [Fact]
    public async Task ReadFile_DevuelveRutaRelativaClara()
    {
        var dir = TempDir();
        Directory.CreateDirectory(Path.Combine(dir, "src"));
        File.WriteAllText(Path.Combine(dir, "src", "Calc.cs"), "public class Calc {}");
        var toolset = new AgentToolset(dir);

        var step = await toolset.ExecuteAsync(new AgentAction { Action = AgentAction.ActionReadFile, Path = "src/Calc.cs" }, 1, CancellationToken.None);

        Assert.True(step.Success);
        Assert.Contains("src/Calc.cs", step.ResultPreview);
        Assert.Contains("public class Calc {}", step.ResultPreview);
    }

    [Fact]
    public async Task ListDir_Raiz_MuestraEstructuraRelativa()
    {
        var dir = TempDir();
        Directory.CreateDirectory(Path.Combine(dir, "Calculator", "Tests"));
        File.WriteAllText(Path.Combine(dir, "Calculator", "Calc.cs"), "");
        File.WriteAllText(Path.Combine(dir, "Readme.md"), "");
        var toolset = new AgentToolset(dir);

        var step = await toolset.ExecuteAsync(new AgentAction { Action = AgentAction.ActionListDir, Path = "" }, 1, CancellationToken.None);

        Assert.True(step.Success);
        Assert.Contains("Calculator", step.ResultPreview);
        Assert.Contains("[d] Calculator", step.ResultPreview);
        Assert.Contains("[f] Readme.md", step.ResultPreview);
    }

    [Fact]
    public async Task Patch_ReemplazoExacto_ModificaSoloElFragmento()
    {
        var dir = TempDir();
        var file = Path.Combine(dir, "Calc.cs");
        File.WriteAllText(file, "namespace X;\n\npublic static class Calc\n{\n    public static int Sum(int a, int b)\n    {\n        return a - b;\n    }\n}\n");
        var toolset = new AgentToolset(dir);

        var step = await toolset.ExecuteAsync(new AgentAction
        {
            Action = AgentAction.ActionPatch,
            Path = "Calc.cs",
            Original = "return a - b;",
            Replacement = "return a + b;"
        }, 1, CancellationToken.None);

        Assert.True(step.Success, step.ResultPreview);
        var content = File.ReadAllText(file);
        Assert.Contains("return a + b;", content);
        Assert.DoesNotContain("return a - b;", content);
        // El resto del archivo queda intacto (ni truncado ni mutilado).
        Assert.Contains("public static class Calc", content);
        Assert.Contains("public static int Sum(int a, int b)", content);
        Assert.EndsWith("}\n", content, StringComparison.Ordinal);
        Assert.Contains("namespace X;", content);
    }

    [Fact]
    public async Task Patch_ConSaltosDeLineaNormales_ToleraCRLF()
    {
        var dir = TempDir();
        var file = Path.Combine(dir, "Calc.cs");
        // Archivo en CRLF (Windows), el modelo copio el fragmento con LF.
        File.WriteAllText(file, "public static int Sum(int a, int b)\r\n{\r\n    return a - b;\r\n}\r\n");
        var toolset = new AgentToolset(dir);

        var step = await toolset.ExecuteAsync(new AgentAction
        {
            Action = AgentAction.ActionPatch,
            Path = "Calc.cs",
            Original = "return a - b;",
            Replacement = "return a + b;"
        }, 1, CancellationToken.None);

        Assert.True(step.Success, step.ResultPreview);
        Assert.Contains("a + b;", File.ReadAllText(file));
    }

    [Fact]
    public async Task Patch_FragmentoNoEncontrado_DaErrorOrientativo()
    {
        var dir = TempDir();
        var file = Path.Combine(dir, "Calc.cs");
        File.WriteAllText(file, "public static int Sum(int a, int b) { return a + b; }");
        var toolset = new AgentToolset(dir);

        var step = await toolset.ExecuteAsync(new AgentAction
        {
            Action = AgentAction.ActionPatch,
            Path = "Calc.cs",
            Original = "return z - 9;",
            Replacement = "return a + b;"
        }, 1, CancellationToken.None);

        Assert.False(step.Success);
        Assert.Contains("No se encontro", step.ResultPreview);
    }

    [Fact]
    public async Task CreateFile_RutaInexistente_LaCreaAlAvanzar()
    {
        var dir = TempDir();
        var toolset = new AgentToolset(dir);

        var step = await toolset.ExecuteAsync(new AgentAction
        {
            Action = AgentAction.ActionCreateFile,
            Path = "src/Nuevo.cs",
            Content = "public class Nuevo {}"
        }, 1, CancellationToken.None);

        Assert.True(step.Success);
        Assert.True(File.Exists(Path.Combine(dir, "src", "Nuevo.cs")));
    }

    [Fact]
    public async Task ReadFile_RutaInexistente_SugiereCandidatos()
    {
        var dir = TempDir();
        Directory.CreateDirectory(Path.Combine(dir, "Calculator"));
        File.WriteAllText(Path.Combine(dir, "Calculator", "Calc.cs"), "x");
        var toolset = new AgentToolset(dir);

        var step = await toolset.ExecuteAsync(new AgentAction { Action = AgentAction.ActionReadFile, Path = "Calculator.cs" }, 1, CancellationToken.None);

        Assert.False(step.Success);
        Assert.Contains("Calculator", step.ResultPreview);
    }

    private static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "condor-toolset-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
