using System.Text.Json;
using Condor.Core.Contracts;
using Condor.Core.Models;
using Condor.Infrastructure.Llm;

namespace Condor.Infrastructure.Tests;

public sealed class ModelComparisonServiceTests
{
    [Fact]
    public async Task CompareAsync_UsesSamePromptForDifferentProvidersAndPersistsComparableResults()
    {
        var reportDirectory = Path.Combine(Path.GetTempPath(), "condor-model-comparison-" + Guid.NewGuid().ToString("N"));
        var first = new RecordingLlmClient("Respuesta del primero.");
        var second = new RecordingLlmClient("Respuesta del segundo.");
        const string task = "Resume el objetivo del producto en tres puntos.";
        try
        {
            var service = new ModelComparisonService(reportDirectory);
            var report = await service.CompareAsync(task, new[]
            {
                new ModelComparisonTarget("Ollama", "qwen2.5-coder:3b", first),
                new ModelComparisonTarget("openai-compatible", "modelo-prueba", second)
            });

            Assert.Equal(2, report.Results.Count);
            Assert.Equal("Ollama", report.Results[0].Provider);
            Assert.Equal("qwen2.5-coder:3b", report.Results[0].Model);
            Assert.Equal("openai-compatible", report.Results[1].Provider);
            Assert.Equal("modelo-prueba", report.Results[1].Model);
            Assert.Equal("Respuesta del primero.", report.Results[0].Content);
            Assert.Equal("Respuesta del segundo.", report.Results[1].Content);
            Assert.True(report.Results.All(result => result.Success));

            Assert.Single(first.Requests);
            Assert.Single(second.Requests);
            var firstRequest = first.Requests[0];
            var secondRequest = second.Requests[0];
            Assert.Equal(task, firstRequest.Messages![1].Content);
            Assert.Equal(task, secondRequest.Messages![1].Content);
            Assert.Equal(firstRequest.Messages.Select(message => (message.Role, message.Content)),
                secondRequest.Messages.Select(message => (message.Role, message.Content)));
            Assert.Equal(firstRequest.Temperature, secondRequest.Temperature);
            Assert.Equal(1024, firstRequest.MaxTokens);
            Assert.Equal(1024, secondRequest.MaxTokens);
            Assert.Null(firstRequest.Tools);
            Assert.Null(secondRequest.Tools);

            Assert.True(File.Exists(report.ReportPath));
            var persisted = JsonSerializer.Deserialize<ModelComparisonReport>(await File.ReadAllTextAsync(report.ReportPath));
            Assert.NotNull(persisted);
            Assert.Equal(task, persisted.Task);
            Assert.Equal(report.Results.Select(result => (result.Provider, result.Model, result.Content)),
                persisted.Results.Select(result => (result.Provider, result.Model, result.Content)));
        }
        finally
        {
            if (Directory.Exists(reportDirectory)) Directory.Delete(reportDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task CompareAsync_WhenOneModelCannotBePrepared_RecordsFailureAndContinues()
    {
        var reportDirectory = Path.Combine(Path.GetTempPath(), "condor-model-comparison-" + Guid.NewGuid().ToString("N"));
        var blockedClient = new RecordingLlmClient("No debe ejecutarse.");
        var availableClient = new RecordingLlmClient("Respuesta disponible.");
        try
        {
            var service = new ModelComparisonService(reportDirectory);
            var report = await service.CompareAsync(
                "Explica por qué se verifican cambios.",
                new[]
                {
                    new ModelComparisonTarget("Ollama", "modelo-sin-memoria", blockedClient),
                    new ModelComparisonTarget("Ollama", "modelo-viable", availableClient)
                },
                (target, _) => Task.FromResult<string?>(
                    target.Model == "modelo-sin-memoria" ? "RAM libre insuficiente." : null));

            Assert.False(report.Results[0].Success);
            Assert.Equal("RAM libre insuficiente.", report.Results[0].Error);
            Assert.Equal(LlmOutcome.ServerUnavailable, report.Results[0].Outcome);
            Assert.Equal(2, report.Results.Count);
            Assert.Empty(blockedClient.Requests);
            Assert.Single(availableClient.Requests);
            Assert.True(File.Exists(report.ReportPath));
        }
        finally
        {
            if (Directory.Exists(reportDirectory)) Directory.Delete(reportDirectory, recursive: true);
        }
    }

    private sealed class RecordingLlmClient(string content) : ILlmClient
    {
        public List<LlmRequest> Requests { get; } = new();

        public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(new LlmResponse
            {
                Success = true,
                Model = request.Model,
                Content = content,
                Outcome = LlmOutcome.Ok
            });
        }
    }
}
