using Condor.Core.Contracts;
using Condor.Core.Evaluation;
using Condor.Core.Models;
using Condor.Infrastructure.Agent;

namespace Condor.Infrastructure.Tests;

public sealed class ReviewerBenchmarkRunnerTests
{
    [Fact]
    public async Task RunAsync_EjecutaCasosConocidosYCalculaPrecisionYRecall()
    {
        var client = new QueueLlmClient(
            Finding("src/Sample/AccessControl.cs", 3, "high", "no verifica que el usuario sea propietario; cualquier usuario autenticado puede leer documentos privados"),
            Finding("src/Sample/ArchiveReader.cs", 3, "high", "el nombre puede escapar del directorio raíz"),
            Finding("src/Sample/Cart.cs", 3, "medium", "una cantidad negativa aumenta el inventario"),
            "{\"reviewFindings\":[]}");

        var run = await new ReviewerBenchmarkRunner(client).RunAsync("modelo-prueba");
        var score = ReviewerBenchmarkEvaluator.Score(ReviewerBenchmarkSuite.Cases, run);

        Assert.Equal(ReviewerBenchmarkSuite.Cases.Count, client.Requests.Count);
        Assert.All(client.Requests, request =>
        {
            Assert.Equal("modelo-prueba", request.Model);
            Assert.Equal(0, request.Temperature);
            Assert.True(request.RequireJson);
            Assert.Contains("reviewFindings", request.Prompt);
            Assert.Contains("no digas que falta un método, condición o validación si aparece en el código", request.Prompt, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("la evidencia literal respalde y no contradiga cada explicación", request.Prompt, StringComparison.OrdinalIgnoreCase);
        });
        Assert.Equal(3, score.TruePositives);
        Assert.Equal(0, score.FalsePositives);
        Assert.Equal(0, score.FalseNegatives);
        Assert.Equal(1, score.Precision);
        Assert.Equal(1, score.Recall);
        Assert.Equal(0, score.Regressions);
    }

    [Fact]
    public void Score_NoCuentaComoAciertoUnHallazgoConExplicacionIncorrecta()
    {
        var run = Run(FindingObject(
            "src/Sample/AccessControl.cs",
            3,
            "high",
            "no se valida que el usuario tenga autenticación"));

        var score = ReviewerBenchmarkEvaluator.Score(ReviewerBenchmarkSuite.Cases, run);

        Assert.Equal(0, score.TruePositives);
        Assert.Equal(1, score.FalsePositives);
        Assert.Equal(3, score.FalseNegatives);
    }

    [Fact]
    public void Score_NoCuentaExplicacionesRealesQueAtribuyenDefectosInexistentes()
    {
        var run = Run(
            FindingObject("src/Sample/AccessControl.cs", 3, "high", "CanRead está incompleto; debe comprobar el rol o si está autenticado"),
            FindingObject("src/Sample/ArchiveReader.cs", 3, "high", "Read no está implementado en ArchiveReader"),
            FindingObject("src/Sample/Cart.cs", 3, "medium", "AddToStock no verifica que requestedQuantity sea menor o igual a stock"),
            FindingObject("src/Sample/Clamp.cs", 3, "low", "Clamp no verifica el rango antes de usar Math.Min y Math.Max"));

        var score = ReviewerBenchmarkEvaluator.Score(ReviewerBenchmarkSuite.Cases, run);

        Assert.Equal(0, score.TruePositives);
        Assert.Equal(4, score.FalsePositives);
        Assert.Equal(3, score.FalseNegatives);
    }

    [Fact]
    public void Score_CuentaElHallazgoCuandoDescribeLaFallaDePropiedadYAutenticacion()
    {
        var run = Run(FindingObject(
            "src/Sample/AccessControl.cs",
            3,
            "high",
            "la condición permite que cualquier usuario autenticado lea documentos sin ser propietario"));

        var score = ReviewerBenchmarkEvaluator.Score(ReviewerBenchmarkSuite.Cases, run);

        Assert.Equal(1, score.TruePositives);
        Assert.Equal(0, score.FalsePositives);
        Assert.Equal(2, score.FalseNegatives);
    }

    [Fact]
    public void Score_ComparaContraLineaBaseYCuentaRegresionesYFalsosPositivos()
    {
        var baseline = Run(
            FindingObject("src/Sample/AccessControl.cs", 3, "high"),
            FindingObject("src/Sample/ArchiveReader.cs", 3, "high"),
            FindingObject("src/Sample/Cart.cs", 3, "medium"));
        var candidate = Run(
            FindingObject("src/Sample/ArchiveReader.cs", 3, "high"),
            FindingObject("src/Sample/Cart.cs", 3, "medium"),
            FindingObject("src/Sample/Clamp.cs", 3, "low"));

        var score = ReviewerBenchmarkEvaluator.Score(ReviewerBenchmarkSuite.Cases, candidate, baseline);

        Assert.Equal(2, score.TruePositives);
        Assert.Equal(1, score.FalsePositives);
        Assert.Equal(1, score.FalseNegatives);
        Assert.Equal(1, score.Regressions);
    }

    [Fact]
    public async Task RunAsync_SeDetieneAnteTimeoutYMarcaLosCasosRestantes()
    {
        var run = await new ReviewerBenchmarkRunner(new TimeoutLlmClient()).RunAsync("modelo-lento");

        Assert.Equal(ReviewerBenchmarkSuite.Cases.Count, run.Cases.Count);
        Assert.Contains("timeout", run.Cases[0].Error, StringComparison.OrdinalIgnoreCase);
        Assert.All(run.Cases.Skip(1), result => Assert.Contains("No ejecutado", result.Error));
    }

    private static string Finding(string path, int line, string severity, string explanation) =>
        "{\"reviewFindings\":[{\"path\":\"" + path + "\",\"line\":" + line + ",\"severity\":\"" + severity + "\",\"explanation\":\"" + explanation + "\",\"evidence\":\"return stock;\",\"reproduction\":\"revisar entrada indicada\",\"origin\":\"current-change\",\"status\":\"open\"}]}";

    private static ReviewerBenchmarkRun Run(params AgentReviewFinding[] findings) => new()
    {
        Cases = ReviewerBenchmarkSuite.Cases.Select((benchmarkCase, index) => new ReviewerBenchmarkCaseResult
        {
            CaseId = benchmarkCase.Id,
            Findings = findings.Where(finding => string.Equals(finding.Path, benchmarkCase.RelativePath, StringComparison.OrdinalIgnoreCase)).ToList()
        }).ToList()
    };

    private static AgentReviewFinding FindingObject(string path, int line, string severity, string? explanation = null) => new()
    {
        Path = path,
        Line = line,
        Severity = severity,
        Explanation = explanation ?? (path switch
        {
            "src/Sample/AccessControl.cs" => "permite a un usuario autenticado leer documentos ajenos sin ser propietario",
            "src/Sample/ArchiveReader.cs" => "el nombre con ../ permite escapar del directorio raíz",
            "src/Sample/Cart.cs" => "una cantidad negativa aumenta el stock",
            _ => "hallazgo esperado"
        }),
        Evidence = "evidencia",
        Reproduction = "pasos",
        Origin = "current-change",
        Status = "open"
    };

    private sealed class QueueLlmClient(params string[] responses) : ILlmClient
    {
        private readonly Queue<string> _responses = new(responses);
        public List<LlmRequest> Requests { get; } = new();

        public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(new LlmResponse
            {
                Success = true,
                Model = request.Model,
                Content = _responses.Dequeue()
            });
        }
    }

    private sealed class TimeoutLlmClient : ILlmClient
    {
        public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new LlmResponse
            {
                Success = false,
                Outcome = LlmOutcome.Timeout,
                Error = "Timeout de prueba"
            });
    }
}
