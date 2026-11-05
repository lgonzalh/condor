using System.Diagnostics;
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
    public async Task UndoFile_TrasCambioProblematico_RestauraVersionAnteriorYEliminaArchivoNuevo()
    {
        var dir = TempDir();
        const string original = "# Proyecto\n\nLa versión estable suma correctamente.\n";
        const string regressed = "# Proyecto\n\nLa versión defectuosa resta en lugar de sumar.\n";
        var existingPath = Path.Combine(dir, "README.md");
        try
        {
            await File.WriteAllTextAsync(existingPath, original);
            var toolset = new AgentToolset(dir);

            var edit = await toolset.ExecuteAsync(new AgentAction
            {
                Action = AgentAction.ActionEditFile,
                Path = "README.md",
                Content = regressed
            }, 1, CancellationToken.None);
            Assert.True(edit.Success, edit.ResultPreview);
            Assert.Equal(regressed, await File.ReadAllTextAsync(existingPath));

            var rollback = await toolset.ExecuteAsync(new AgentAction
            {
                Action = AgentAction.ActionUndoFile,
                Path = "README.md"
            }, 2, CancellationToken.None);
            Assert.True(rollback.Success, rollback.ResultPreview);
            Assert.Equal(original, await File.ReadAllTextAsync(existingPath));

            var create = await toolset.ExecuteAsync(new AgentAction
            {
                Action = AgentAction.ActionCreateFile,
                Path = "incident.txt",
                Content = "artefacto introducido por el cambio problemático"
            }, 3, CancellationToken.None);
            Assert.True(create.Success, create.ResultPreview);

            var rollbackCreate = await toolset.ExecuteAsync(new AgentAction
            {
                Action = AgentAction.ActionUndoFile,
                Path = "incident.txt"
            }, 4, CancellationToken.None);
            Assert.True(rollbackCreate.Success, rollbackCreate.ResultPreview);
            Assert.False(File.Exists(Path.Combine(dir, "incident.txt")));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Theory]
    [InlineData(AgentAction.ActionCreateFile)]
    [InlineData(AgentAction.ActionEditFile)]
    public async Task FileMutation_ContenidoIgualALaRuta_NoLaEscribeComoCodigo(string action)
    {
        var dir = TempDir();
        var path = "Sample.Tests/CalculatorTests.cs";
        var fullPath = Path.Combine(dir, "Sample.Tests", "CalculatorTests.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        const string existingContent = "namespace Sample.Tests;";
        if (action == AgentAction.ActionEditFile)
            File.WriteAllText(fullPath, existingContent);
        var toolset = new AgentToolset(dir);

        var step = await toolset.ExecuteAsync(new AgentAction
        {
            Action = action,
            Path = path,
            Content = path
        }, 1, CancellationToken.None);

        Assert.False(step.Success);
        Assert.Contains("content contiene solo la ruta", step.ResultPreview, StringComparison.OrdinalIgnoreCase);
        if (action == AgentAction.ActionCreateFile)
            Assert.False(File.Exists(fullPath));
        else
            Assert.Equal(existingContent, await File.ReadAllTextAsync(fullPath));
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

    [Fact]
    public async Task GitLogYGitShow_MuestranPropositoYAlcanceDeCadaCommit()
    {
        var dir = TempDir();
        try
        {
            RunGit(dir, "init", "--quiet");
            RunGit(dir, "config", "user.name", "Condor Tests");
            RunGit(dir, "config", "user.email", "condor-tests@example.invalid");
            Directory.CreateDirectory(Path.Combine(dir, "src"));
            var path = Path.Combine(dir, "src", "Calculator.cs");
            await File.WriteAllTextAsync(path, "public static class Calculator { }\n");
            RunGit(dir, "add", "src/Calculator.cs");
            RunGit(dir, "commit", "--quiet", "-m", "feat: add calculator type");
            await File.WriteAllTextAsync(path, "public static class Calculator { public static int Sum(int a, int b) => a + b; }\n");
            RunGit(dir, "add", "src/Calculator.cs");
            RunGit(dir, "commit", "--quiet", "-m", "feat: add sum operation");
            var head = RunGit(dir, "rev-parse", "HEAD");

            var toolset = new AgentToolset(dir);
            var log = await toolset.ExecuteAsync(
                new AgentAction { Action = AgentAction.ActionGitLog }, 1, CancellationToken.None);
            var commit = await toolset.ExecuteAsync(
                new AgentAction { Action = AgentAction.ActionGitShow, Path = head }, 2, CancellationToken.None);

            Assert.True(log.Success, log.ResultPreview);
            Assert.Contains("feat: add calculator type", log.ResultPreview, StringComparison.Ordinal);
            Assert.Contains("feat: add sum operation", log.ResultPreview, StringComparison.Ordinal);
            Assert.Contains("src/Calculator.cs", log.ResultPreview, StringComparison.Ordinal);
            Assert.True(commit.Success, commit.ResultPreview);
            Assert.Contains("feat: add sum operation", commit.ResultPreview, StringComparison.Ordinal);
            Assert.Contains("src/Calculator.cs", commit.ResultPreview, StringComparison.Ordinal);
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(dir, recursive: true);
        }
    }

    private static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "condor-toolset-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string RunGit(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("No se pudo iniciar Git.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException("Git falló: " + error);
        return output.Trim();
    }
}
