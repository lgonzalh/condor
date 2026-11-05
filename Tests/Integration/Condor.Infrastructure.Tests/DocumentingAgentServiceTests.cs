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

    [Fact]
    public async Task RunAsync_FalloRegistraIncidenteConImpactoCausaYArchivos()
    {
        var root = Path.Combine(Path.GetTempPath(), "condor-agent-incident-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var original = new AgentResult
            {
                Success = false,
                Reason = "Build fallo: referencia no resuelta.",
                Steps = new List<AgentStep>
                {
                    new() { Action = "edit_file", Path = "src/Clase.cs", Success = true },
                    new() { Action = "build", Path = "src/Clase.csproj", Success = false }
                }
            };
            var service = new DocumentingAgentService(
                new StubAgentService(original),
                new DocumenterService(),
                () => root);

            var result = await service.RunAsync("corrige la clase");

            Assert.Same(original, result);
            Assert.False(result.Success);
            Assert.NotNull(result.Documentation);
            Assert.True(result.Documentation!.Success, result.Documentation.Reason);
            var reportPath = Path.Combine(root, result.Documentation.RelativePath!.Replace('/', Path.DirectorySeparatorChar));
            var content = await File.ReadAllTextAsync(reportPath, new System.Text.UTF8Encoding(false, true));
            Assert.Contains("Incidente del agente", content);
            Assert.Contains("Impacto: la tarea no se completó", content);
            Assert.Contains("src/Clase.cs", content);
            Assert.Contains("Causa reportada (no confirmada como causa raíz)", content);
            Assert.Contains("Build fallo: referencia no resuelta.", content);
            var learningStatePath = Path.Combine(root, ".condor", "incident-learning.json");
            Assert.True(File.Exists(learningStatePath));
            var learningState = await File.ReadAllTextAsync(learningStatePath);
            Assert.Contains("build_failure", learningState);
            Assert.DoesNotContain("Build fallo: referencia no resuelta.", learningState);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task IncidentLearning_RepeatedAllowlistedPatternActivatesApprovedPromptRule()
    {
        var root = Path.Combine(Path.GetTempPath(), "condor-incident-learning-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var learning = new IncidentLearningService();
            var first = await learning.RecordAsync(root, "provider timeout", Array.Empty<string>());
            Assert.Empty(first.ActivatedRuleIds);
            Assert.Empty(IncidentLearningService.LoadActiveInstructions(root));

            var second = await learning.RecordAsync(root, "provider timeout", Array.Empty<string>());
            Assert.Equal(new[] { "provider_timeout" }, second.ActivatedRuleIds);

            var instructions = IncidentLearningService.LoadActiveInstructions(root);
            Assert.Single(instructions);
            Assert.Contains("timeout del proveedor", instructions[0], StringComparison.OrdinalIgnoreCase);
            var prompt = Condor.Infrastructure.Agent.ModelPromptBuilder.BuildSystemPrompt(root, null, null);
            Assert.Contains(instructions[0], prompt);

            var statePath = Path.Combine(root, ".condor", "incident-learning.json");
            var state = await File.ReadAllTextAsync(statePath);
            Assert.DoesNotContain("provider timeout", state, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("instrucción arbitraria del usuario", state, StringComparison.OrdinalIgnoreCase);

            await learning.RecordAsync(root, "instrucción arbitraria del usuario", Array.Empty<string>());
            Assert.Single(IncidentLearningService.LoadActiveInstructions(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_InformeDeRevisionCalculaProporcionDeHallazgosDescartados()
    {
        var root = Path.Combine(Path.GetTempPath(), "condor-agent-review-metrics-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var original = new AgentResult
            {
                Success = true,
                Reason = "Revisión terminada.",
                ReviewFindings = new List<AgentReviewFinding>
                {
                    new() { Path = "src/A.cs", Line = 10, Severity = "high", Status = "resolved" },
                    new() { Path = "src/B.cs", Line = 20, Severity = "low", Status = "accepted", ClosureReason = "Comportamiento intencional." },
                    new() { Path = "src/C.cs", Line = 30, Severity = "medium", Status = "open" }
                }
            };
            var service = new DocumentingAgentService(
                new StubAgentService(original),
                new DocumenterService(),
                () => root);

            var result = await service.RunAsync("revisa el cambio");

            Assert.NotNull(result.Documentation);
            Assert.True(result.Documentation!.Success, result.Documentation.Reason);
            var reportPath = Path.Combine(root, result.Documentation.RelativePath!.Replace('/', Path.DirectorySeparatorChar));
            var content = await File.ReadAllTextAsync(reportPath, new System.Text.UTF8Encoding(false, true));
            Assert.Contains("Hallazgos de revisión: 3 total; 1 resueltos; 1 aceptados/descartados; 1 abiertos.", content);
            Assert.Contains("Proporción de hallazgos descartados entre los adjudicados:", content);
            Assert.Contains("(1/2)", content);
            Assert.Contains("Observación corregida: src/A.cs:10 (high); hallazgo:", content);
            Assert.Contains("Observación descartada o justificada como intencional: src/B.cs:20 (low); hallazgo:", content);
            Assert.Contains("justificación: Comportamiento intencional.", content);
            Assert.DoesNotContain("src/C.cs:30 (medium)", content);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_InformeDeRevisionCalculaProporcionUtilYAcumuladoDeVariasEjecuciones()
    {
        var root = Path.Combine(Path.GetTempPath(), "condor-agent-review-history-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var first = new DocumentingAgentService(
                new StubAgentService(new AgentResult
                {
                    Success = true,
                    ReviewFindings = new List<AgentReviewFinding>
                    {
                        new() { Path = "src/A.cs", Line = 10, Status = "resolved" },
                        new() { Path = "src/B.cs", Line = 20, Status = "accepted" },
                        new() { Path = "src/C.cs", Line = 30, Status = "open" }
                    }
                }),
                new DocumenterService(),
                () => root);
            var second = new DocumentingAgentService(
                new StubAgentService(new AgentResult
                {
                    Success = true,
                    ReviewFindings = new List<AgentReviewFinding>
                    {
                        new() { Path = "src/D.cs", Line = 40, Status = "resolved" },
                        new() { Path = "src/E.cs", Line = 50, Status = "resolved" }
                    }
                }),
                new DocumenterService(),
                () => root);

            _ = await first.RunAsync("revisa la primera parte");
            var result = await second.RunAsync("revisa la segunda parte");

            Assert.NotNull(result.Documentation);
            Assert.True(result.Documentation!.Success, result.Documentation.Reason);
            var reportPath = Path.Combine(root, result.Documentation.RelativePath!.Replace('/', Path.DirectorySeparatorChar));
            var content = await File.ReadAllTextAsync(reportPath, new System.Text.UTF8Encoding(false, true));
            Assert.Contains("Historial de revisión: 2 ejecuciones; 2 en los últimos 30 días; 3 útiles; 1 descartados; 1 abiertos; 0 sin clasificar.", content);
            Assert.Contains("Proporción histórica de hallazgos útiles entre los adjudicados:", content);
            Assert.Contains("(3/4)", content);
            Assert.Contains("Proporción de hallazgos útiles en los últimos 30 días:", content);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class StubAgentService(AgentResult result) : IAgentService
    {
        public Task<AgentResult> RunAsync(
            string intention,
            IAgentProgressObserver? progress = null,
            CancellationToken cancellationToken = default) => Task.FromResult(result);
    }
}
