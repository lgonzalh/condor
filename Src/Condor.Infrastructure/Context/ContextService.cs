using Condor.Core.Context;
using Condor.Core.Contracts;
using Condor.Core.Models;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Condor.Infrastructure.Context
{
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

        public async Task<ProjectContext> BuildContextAsync(
            string workingDirectory,
            CancellationToken cancellationToken = default)
        {
            var timeout = TimeSpan.FromMilliseconds(_limits.ContextTimeoutMilliseconds);

            try
            {
                var normalizedDirectory = Path.GetFullPath(workingDirectory);
                var assessment = await new AssessmentService()
                    .ExecuteAsync(new AssessmentRequest { WorkingDirectory = normalizedDirectory }, cancellationToken)
                    .WaitAsync(timeout, cancellationToken);
                await _stateStore.SaveAssessmentAsync(assessment, cancellationToken)
                    .WaitAsync(timeout, cancellationToken);

                var artifacts = await _artifactReader
                    .ReadAsync(normalizedDirectory, cancellationToken)
                    .WaitAsync(timeout, cancellationToken);
                var context = ContextReconstructor.Reconstruct(assessment, artifacts, _limits);
                await _stateStore.SaveContextAsync(context, cancellationToken)
                    .WaitAsync(timeout, cancellationToken);
                return context;
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
}
