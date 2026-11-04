using Condor.Core.Contracts;
using Condor.Core.Models;
using Condor.Infrastructure.Documentation;

namespace Condor.Infrastructure.Tests;

public class DocumentingAgentServiceTests
{
    [Fact]
    public async Task RunAsync_GeneraInformeReal_UsaSoloPasosObservadosYConservaResultado()
    {
        var root = Path.Combine(Path.GetTempPath(), "condor-agent-documenter-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var original = new AgentResult
            {
                Success = true,
                Model = "qwen2.5-coder:7b",
                Objective = "actualizar una clase",
                Reason = "La tarea termino.",
                Steps = new List<AgentStep>
                {
                    new()
                    {
                        Action = "edit_file",
                        Path = "src/Clase.cs",
                        Success = true,
                        ResultPreview = "SECRETO_DE_PRUEBA"
                    }
                }
            };
            var service = new DocumentingAgentService(
                new StubAgentService(original),
                new DocumenterService(),
                () => root);

            var result = await service.RunAsync("actualiza la clase");

            Assert.Same(original, result);
            Assert.True(result.Success);
            Assert.NotNull(result.Documentation);
            Assert.True(result.Documentation!.Success, result.Documentation.Reason);

            var reportPath = Path.Combine(root, result.Documentation.RelativePath!.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(reportPath));
            var content = await File.ReadAllTextAsync(reportPath, new System.Text.UTF8Encoding(false, true));
            Assert.Contains("Modelo usado: qwen2.5-coder:7b", content);
            Assert.Contains("## Antes", content);
            Assert.Contains("## Durante", content);
            Assert.Contains("## Despues", content);
            Assert.Contains("src/Clase.cs", content);
            Assert.DoesNotContain("SECRETO_DE_PRUEBA", content);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_SiFallaLaDocumentacion_NoFalseaElResultadoDelAgente()
    {
        var original = new AgentResult { Success = false, Reason = "sin modelo" };
        var service = new DocumentingAgentService(
            new StubAgentService(original),
            new DocumenterService(),
            () => "\0");

        var result = await service.RunAsync("tarea no disponible");

        Assert.False(result.Success);
        Assert.NotNull(result.Documentation);
        Assert.False(result.Documentation!.Success);
    }

    private sealed class StubAgentService(AgentResult result) : IAgentService
    {
        public Task<AgentResult> RunAsync(
            string intention,
            IAgentProgressObserver? progress = null,
            CancellationToken cancellationToken = default) => Task.FromResult(result);
    }
}
