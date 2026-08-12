using Condor.Core.Context;
using Condor.Core.Contracts;
using Condor.Core.Models;

namespace Condor.Infrastructure.Context;

public sealed class ContextService : IContextService
{
    private const string ReasonTimeout = "Tiempo excedido al construir el contexto.";

    private readonly IStateStore _stateStore;
    private readonly OperativeArtifactReader _artifactReader;
    private readonly ContextLimits _limits;

    public ContextService(
        IStateStore stateStore,
        OperativeArtifactReader? artifactReader = null,
        ContextLimits? limits = null)
    {
        _stateStore = stateStore;
        _artifactReader = artifactReader ?? new OperativeArtifactReader();
        _limits = limits ?? ContextLimits.Default;
    }

    public async Task<ProjectContext> BuildContextAsync(
        CancellationToken cancellationToken = default)
    {
        var timeout = TimeSpan.FromMilliseconds(_limits.ContextTimeoutMilliseconds);

        try
        {
            var assessment = await _stateStore
                .LoadAssessmentAsync(cancellationToken)
                .WaitAsync(timeout, cancellationToken);

            var workingDirectory = assessment?.WorkingDirectory ?? "";

            var artifacts = await _artifactReader
                .ReadAsync(workingDirectory, cancellationToken)
                .WaitAsync(timeout, cancellationToken);

            return ContextReconstructor.Reconstruct(assessment, artifacts, _limits);
        }
        catch (TimeoutException)
        {
            return new ProjectContext
            {
                SchemaVersion = "1.0.0",
                Status = DetectionStatus.Limited,
                Reason = ReasonTimeout,
                GeneratedAtUtc = DateTime.UtcNow
            };
        }
    }
}