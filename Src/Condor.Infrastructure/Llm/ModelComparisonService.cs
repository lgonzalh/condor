using System.Diagnostics;
using System.Text.Json;
using Condor.Core.Contracts;
using Condor.Core.Models;

namespace Condor.Infrastructure.Llm;

public sealed record ModelComparisonTarget(string Provider, string Model, ILlmClient Client);

public sealed class ModelComparisonEntry
{
    public string Provider { get; init; } = "";
    public string Model { get; init; } = "";
    public bool Success { get; init; }
    public string? Content { get; init; }
    public string? Error { get; init; }
    public LlmOutcome Outcome { get; init; }
    public long DurationMilliseconds { get; init; }
}

public sealed class ModelComparisonReport
{
    public string SchemaVersion { get; init; } = "1.0.0";
    public string Task { get; init; } = "";
    public DateTime StartedAtUtc { get; init; }
    public DateTime CompletedAtUtc { get; set; }
    public string ComparisonPolicy { get; init; } =
        "Mismo mensaje de sistema y tarea; solicitudes secuenciales, sin herramientas ni cambios de archivos.";
    public string ReportPath { get; set; } = "";
    public List<ModelComparisonEntry> Results { get; init; } = new();
}

public sealed class ModelComparisonService
{
    private const int MaxTaskCharacters = 16_000;
    private const int MaxResponseCharacters = 100_000;
    private const int OutputTokenLimit = 1_024;
    private readonly string _reportDirectory;

    public ModelComparisonService(string? reportDirectory = null)
    {
        _reportDirectory = string.IsNullOrWhiteSpace(reportDirectory)
            ? Path.Combine(Environment.CurrentDirectory, ".condor", "comparisons")
            : Path.GetFullPath(reportDirectory);
    }

    public async Task<ModelComparisonReport> CompareAsync(
        string task,
        IReadOnlyList<ModelComparisonTarget> targets,
        Func<ModelComparisonTarget, CancellationToken, Task<string?>>? prepareTarget = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(task))
            throw new ArgumentException("Indica la tarea que se comparará.", nameof(task));
        if (task.Length > MaxTaskCharacters)
            throw new ArgumentException($"La tarea supera el límite de {MaxTaskCharacters} caracteres.", nameof(task));
        if (targets is null || targets.Count is < 2 or > 4)
            throw new ArgumentException("La comparación requiere entre dos y cuatro modelos.", nameof(targets));
        if (targets.Any(target => target is null || string.IsNullOrWhiteSpace(target.Provider) ||
                                  string.IsNullOrWhiteSpace(target.Model) || target.Client is null))
            throw new ArgumentException("Cada opción debe incluir proveedor, modelo y cliente.", nameof(targets));
        if (targets.Select(target => target.Provider + "\0" + target.Model)
            .Distinct(StringComparer.OrdinalIgnoreCase).Count() != targets.Count)
            throw new ArgumentException("No repitas el mismo proveedor y modelo en una comparación.", nameof(targets));

        var startedAtUtc = DateTime.UtcNow;
        var reportPath = CreateReportPath(startedAtUtc);
        var report = new ModelComparisonReport
        {
            Task = task,
            StartedAtUtc = startedAtUtc,
            ReportPath = reportPath
        };
        var messages = new List<LlmMessage>
        {
            new()
            {
                Role = "system",
                Content = "Eres un modelo evaluado por Condor. Responde directamente a la tarea del usuario. " +
                          "Esta es una comparación de solo lectura: no tienes herramientas, no afirmes haber editado archivos, " +
                          "ejecutado pruebas ni inspeccionado contenido que no esté en el mensaje. Expón supuestos y límites relevantes."
            },
            new() { Role = "user", Content = task }
        };

        foreach (var target in targets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stopwatch = Stopwatch.StartNew();
            string? preparationError = null;
            try
            {
                if (prepareTarget is not null)
                    preparationError = await prepareTarget(target, cancellationToken).ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(preparationError))
                {
                    report.Results.Add(new ModelComparisonEntry
                    {
                        Provider = target.Provider,
                        Model = target.Model,
                        Success = false,
                        Error = preparationError,
                        Outcome = LlmOutcome.ServerUnavailable,
                        DurationMilliseconds = stopwatch.ElapsedMilliseconds
                    });
                    continue;
                }

                var request = new LlmRequest
                {
                    Model = target.Model,
                    Messages = messages.Select(message => new LlmMessage
                    {
                        Role = message.Role,
                        Content = message.Content
                    }).ToList(),
                    Temperature = 0.2,
                    MaxTokens = OutputTokenLimit
                };
                var response = await target.Client.CompleteAsync(request, cancellationToken).ConfigureAwait(false);
                report.Results.Add(new ModelComparisonEntry
                {
                    Provider = target.Provider,
                    Model = target.Model,
                    Success = response.Success,
                    Content = Clip(response.Content, MaxResponseCharacters),
                    Error = response.Success ? null : response.Error,
                    Outcome = response.Outcome,
                    DurationMilliseconds = stopwatch.ElapsedMilliseconds
                });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                report.Results.Add(new ModelComparisonEntry
                {
                    Provider = target.Provider,
                    Model = target.Model,
                    Success = false,
                    Error = "La solicitud falló (" + exception.GetType().Name + ").",
                    Outcome = LlmOutcome.ServerUnavailable,
                    DurationMilliseconds = stopwatch.ElapsedMilliseconds
                });
            }
        }

        report.CompletedAtUtc = DateTime.UtcNow;
        await SaveReportAsync(report, cancellationToken).ConfigureAwait(false);
        return report;
    }

    private string CreateReportPath(DateTime startedAtUtc)
    {
        var name = "model-comparison-" + startedAtUtc.ToString("yyyyMMdd-HHmmssfff") + "-" +
                   Guid.NewGuid().ToString("N")[..8] + ".json";
        return Path.Combine(_reportDirectory, name);
    }

    private static async Task SaveReportAsync(ModelComparisonReport report, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(report.ReportPath)!);
        await using var stream = new FileStream(
            report.ReportPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 16_384,
            options: FileOptions.Asynchronous | FileOptions.WriteThrough);
        await JsonSerializer.SerializeAsync(stream, report, new JsonSerializerOptions { WriteIndented = true }, cancellationToken)
            .ConfigureAwait(false);
    }

    private static string? Clip(string? value, int maxCharacters)
    {
        if (value is null || value.Length <= maxCharacters) return value;
        return value[..maxCharacters] + "\n...[respuesta recortada en el informe]";
    }
}
