using System.Text;
using Condor.Cli.Commands;
using Condor.Core.Contracts;
using Condor.Core.Models;
using Condor.Infrastructure.State;

namespace Condor.Infrastructure.Tests;

public class AskCommandTests
{
    [Fact]
    public async Task ExecuteAsync_CuandoElAssessmentEsInvalido_DevuelveErrorSinLlamarAlCliente()
    {
        var directory = Path.Combine(Path.GetTempPath(), "condor-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "assessment.json"),
            "json corrupto {{{",
            Encoding.UTF8);
        var stateStore = new LocalStateStore(directory);
        var llmClient = new LlmClientQueFallaSiSeUsa();

        var exitCode = await AskCommand.ExecuteAsync(llmClient, stateStore, new[] { "hola" });

        Assert.Equal(1, exitCode);
        Assert.False(llmClient.FueUsado);
    }

    [Fact]
    public async Task ExecuteAsync_CuandoElAssessmentEsParcialSinModelos_DevuelveErrorSinLlamarAlCliente()
    {
        var directory = Path.Combine(Path.GetTempPath(), "condor-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "assessment.json"),
            "{\"schemaVersion\":\"1.0.0\"}",
            Encoding.UTF8);
        var stateStore = new LocalStateStore(directory);
        var llmClient = new LlmClientQueFallaSiSeUsa();

        var exitCode = await AskCommand.ExecuteAsync(llmClient, stateStore, new[] { "hola" });

        Assert.Equal(1, exitCode);
        Assert.False(llmClient.FueUsado);
    }

    private class LlmClientQueFallaSiSeUsa : ILlmClient
    {
        public bool FueUsado { get; private set; }

        public Task<LlmResponse> CompleteAsync(
            LlmRequest request,
            CancellationToken cancellationToken = default)
        {
            FueUsado = true;
            throw new InvalidOperationException("No debio consultarse al modelo con un assessment invalido");
        }
    }
}
