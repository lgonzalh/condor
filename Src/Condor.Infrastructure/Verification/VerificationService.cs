using System;
using System.Threading.Tasks;
using Condor.Core.Contracts;
using Condor.Core.Models;
using Condor.Core.Verification;

namespace Condor.Infrastructure.Verification;

public sealed class VerificationService : IVerificationService
{
    private const string ReasonTimeout = "Tiempo excedido al verificar los cambios.";

    private readonly IStateStore _stateStore;
    private readonly VerificationLimits _limits;

    public VerificationService(IStateStore stateStore, VerificationLimits? limits = null)
    {
        _stateStore = stateStore;
        _limits = limits ?? VerificationLimits.Default;
    }

    public async Task<VerificationResult> VerifyAsync(CancellationToken cancellationToken = default)
    {
        var timeout = TimeSpan.FromMilliseconds(_limits.VerifyTimeoutMilliseconds);

        try
        {
            var build = await _stateStore
                .LoadBuildAsync(cancellationToken)
                .WaitAsync(timeout, cancellationToken);

            var context = await _stateStore
                .LoadContextAsync(cancellationToken)
                .WaitAsync(timeout, cancellationToken);

            var workingDirectory = ResolveWorkingDirectory(build, context);

            if (build is not null &&
                build.Status != DetectionStatus.NotDetected &&
                build.Status != DetectionStatus.Limited &&
                build.Actions.Count > 0 &&
                !string.IsNullOrWhiteSpace(workingDirectory))
            {
                var probed = new ProjectFileProbe().Read(
                    build.Actions,
                    workingDirectory,
                    _limits.MaxContentLength);

                return Verifier.Verify(build, workingDirectory, probed, _limits);
            }

            return Verifier.Verify(build, workingDirectory ?? "", new System.Collections.Generic.Dictionary<string, ProbedFile?>(), _limits);
        }
        catch (TimeoutException)
        {
            return new VerificationResult
            {
                SchemaVersion = "1.0.0",
                Status = DetectionStatus.Limited,
                Reason = ReasonTimeout,
                GeneratedAtUtc = DateTime.UtcNow
            };
        }
    }

    private static string? ResolveWorkingDirectory(BuildResult? build, ProjectContext? context)
    {
        if (!string.IsNullOrWhiteSpace(build?.WorkingDirectory))
        {
            return build.WorkingDirectory;
        }

        return !string.IsNullOrWhiteSpace(context?.WorkingDirectory)
            ? context.WorkingDirectory
            : null;
    }
}
