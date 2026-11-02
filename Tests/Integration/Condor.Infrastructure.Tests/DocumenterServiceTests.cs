using System.Text;
using Condor.Core.Models;
using Condor.Infrastructure.Documentation;

namespace Condor.Infrastructure.Tests;

public class DocumenterServiceTests
{
    [Fact]
    public async Task WriteAsync_CreaInformeUtf8SinBom_YNoSobrescribeEjecucionesAnteriores()
    {
        var root = TempDir();
        Directory.CreateDirectory(root);

        try
        {
            var service = new DocumenterService();
            var request = Request(root);

            var first = await service.WriteAsync(request);
            var second = await service.WriteAsync(request);

            Assert.True(first.Success, first.Reason);
            Assert.True(second.Success, second.Reason);
            Assert.NotEqual(first.RelativePath, second.RelativePath);

            var firstPath = Path.Combine(root, first.RelativePath!.Replace('/', Path.DirectorySeparatorChar));
            var bytes = await File.ReadAllBytesAsync(firstPath);
            Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);

            var content = new UTF8Encoding(false, true).GetString(bytes);
            Assert.Contains("## Antes", content);
            Assert.Contains("## Durante", content);
            Assert.Contains("## Despues", content);
            Assert.Contains("Intencion revisada", content);
            Assert.Contains("[REDACTADO]", content);
            Assert.DoesNotContain("sk-proj-123456789abcdef", content);
            Assert.DoesNotContain("api_key=valor-secreto", content);

            var indexPath = Path.Combine(root, "docs", "indice-condor.html");
            Assert.True(File.Exists(indexPath));
            var indexContent = await File.ReadAllTextAsync(indexPath, new UTF8Encoding(false, true));
            Assert.Contains(Path.GetFileName(firstPath), indexContent);
            Assert.Contains(Path.GetFileName(Path.Combine(root, second.RelativePath!.Replace('/', Path.DirectorySeparatorChar))), indexContent);
            Assert.Equal(3, Directory.GetFiles(Path.Combine(root, "docs")).Length);
            Assert.False(Directory.Exists(Path.Combine(root, "docs", "tareas")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task WriteAsync_DirectorioInvalido_DevuelveFalloSinLanzar()
    {
        var result = await new DocumenterService().WriteAsync(Request("\0"));

        Assert.False(result.Success);
        Assert.Null(result.RelativePath);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
    }

    private static DocumentationRequest Request(string root)
    {
        return new DocumentationRequest
        {
            TaskType = "Prueba",
            Intention = "Revisar documentacion",
            WorkingDirectory = root,
            StartedAtUtc = DateTime.UtcNow.AddSeconds(-1),
            CompletedAtUtc = DateTime.UtcNow,
            Outcome = "completado",
            Before = new List<string> { "Intencion revisada" },
            During = new List<string> { "Verificacion ejecutada con sk-proj-123456789abcdef y api_key=valor-secreto" },
            After = new List<string> { "Resultado confirmado" }
        };
    }

    private static string TempDir() => Path.Combine(Path.GetTempPath(), "condor-documenter-" + Guid.NewGuid().ToString("N"));
}
