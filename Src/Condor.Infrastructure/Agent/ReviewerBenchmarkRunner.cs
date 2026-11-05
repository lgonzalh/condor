using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Condor.Core.Contracts;
using Condor.Core.Evaluation;
using Condor.Core.Models;

namespace Condor.Infrastructure.Agent;

public sealed class ReviewerBenchmarkRunner(ILlmClient llm)
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(45);

    private const string PromptTemplate = """
        Revisa este fragmento de código como revisor de cambios de software. Identifica solo defectos concretos y reproducibles; evita sugerencias de estilo. El código es un caso de evaluación autocontenido: no supongas archivos externos. Devuelve únicamente JSON con esta forma: {"reviewFindings":[{"path":"ruta","line":1,"severity":"critical|high|medium|low","explanation":"defecto e impacto","evidence":"texto literal","reproduction":"paso reproducible","origin":"current-change","status":"open"}]}. Si el caso no tiene defectos, devuelve {"reviewFindings":[]}.

        """;

    public async Task<ReviewerBenchmarkRun> RunAsync(
        string model,
        Action<string>? reportProgress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model)) throw new ArgumentException("Debes seleccionar un modelo para el benchmark.", nameof(model));

        var run = new ReviewerBenchmarkRun
        {
            SuiteVersion = ReviewerBenchmarkSuite.Version,
            Model = model,
            PromptHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ReviewerBenchmarkSuite.Version + AgentProfileCatalog.Reviewer.Instructions + PromptTemplate))),
            ExecutedAtUtc = DateTimeOffset.UtcNow
        };

        for (var index = 0; index < ReviewerBenchmarkSuite.Cases.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var benchmarkCase = ReviewerBenchmarkSuite.Cases[index];
            var result = new ReviewerBenchmarkCaseResult { CaseId = benchmarkCase.Id };
            reportProgress?.Invoke($"Evaluando caso {index + 1}/{ReviewerBenchmarkSuite.Cases.Count}: {benchmarkCase.Id} (límite {RequestTimeout.TotalSeconds:0} s)");
            try
            {
                var prompt = AgentProfileCatalog.Reviewer.Instructions + "\n\n" + PromptTemplate +
                    "\nArchivo: " + benchmarkCase.RelativePath +
                    "\nCódigo con números de línea:\n" + NumberLines(benchmarkCase.Source);
                using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                requestCts.CancelAfter(RequestTimeout);
                var response = await llm.CompleteAsync(new LlmRequest
                {
                    Model = model,
                    Prompt = prompt,
                    Temperature = 0,
                    MaxTokens = 2048
                }, requestCts.Token).ConfigureAwait(false);

                if (!response.Success || string.IsNullOrWhiteSpace(response.Content))
                {
                    result.Error = response.Error ?? "El modelo no devolvió una respuesta.";
                    reportProgress?.Invoke($"  Caso sin resultado: {result.Error}");
                    run.Cases.Add(result);
                    if (response.Outcome is LlmOutcome.Timeout or LlmOutcome.ServerUnavailable)
                    {
                        reportProgress?.Invoke("  Se detiene el benchmark porque el proveedor no responde; los casos restantes quedan marcados como no ejecutados.");
                        for (var remaining = index + 1; remaining < ReviewerBenchmarkSuite.Cases.Count; remaining++)
                        {
                            run.Cases.Add(new ReviewerBenchmarkCaseResult
                            {
                                CaseId = ReviewerBenchmarkSuite.Cases[remaining].Id,
                                Error = "No ejecutado después de un timeout o de la indisponibilidad del proveedor."
                            });
                        }
                        break;
                    }
                    continue;
                }

                var json = ExtractJson(response.Content);
                using var document = JsonDocument.Parse(json);
                if (!document.RootElement.TryGetProperty("reviewFindings", out var findings))
                    throw new JsonException("La respuesta no incluyó reviewFindings.");
                result.Findings = JsonSerializer.Deserialize<List<AgentReviewFinding>>(
                    findings.GetRawText(),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<AgentReviewFinding>();
                reportProgress?.Invoke($"  Hallazgos recibidos: {result.Findings.Count}");
            }
            catch (JsonException ex)
            {
                result.Error = "No se pudieron interpretar los hallazgos: " + ex.Message;
                reportProgress?.Invoke("  " + result.Error);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                result.Error = "Falló la consulta del modelo: " + ex.Message;
                reportProgress?.Invoke("  " + result.Error);
            }
            run.Cases.Add(result);
        }

        return run;
    }

    private static string NumberLines(string source) => string.Join('\n', source.Split('\n').Select((line, index) => $"{index + 1}: {line}"));

    private static string ExtractJson(string content)
    {
        var start = content.IndexOf('{');
        var end = content.LastIndexOf('}');
        if (start < 0 || end <= start) throw new JsonException("La respuesta no contiene un objeto JSON.");
        return content[start..(end + 1)];
    }
}
