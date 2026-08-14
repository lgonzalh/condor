using System;
using System.Threading.Tasks;
using Condor.Core.Building;
using Condor.Core.Contracts;
using Condor.Core.Models;

namespace Condor.Infrastructure.Building;

public sealed class BuildService : IBuildService
{
    private const string ReasonTimeout = "Tiempo excedido al aplicar el plan.";

    private readonly IStateStore _stateStore;
    private readonly BuildLimits _limits;

    public BuildService(IStateStore stateStore, BuildLimits? limits = null)
    {
        _stateStore = stateStore;
        _limits = limits ?? BuildLimits.Default;
    }

    public async Task<BuildResult> ApplyPlanAsync(CancellationToken cancellationToken = default)
    {
        var timeout = TimeSpan.FromMilliseconds(_limits.BuildTimeoutMilliseconds);

        try
        {
            var plan = await _stateStore
                .LoadPlanAsync(cancellationToken)
                .WaitAsync(timeout, cancellationToken);

            var result = BuildDeriver.Derive(plan, _limits);

            if (result.Status == DetectionStatus.Detected)
            {
                new ProjectFileWriter().Apply(result.Actions, result.WorkingDirectory);
                Summarize(result);
            }

            return result;
        }
        catch (TimeoutException)
        {
            return new BuildResult
            {
                SchemaVersion = "1.0.0",
                Status = DetectionStatus.Limited,
                Reason = ReasonTimeout,
                GeneratedAtUtc = DateTime.UtcNow
            };
        }
    }

    private static void Summarize(BuildResult result)
    {
        var applied = 0;
        var omitted = 0;
        var failed = 0;

        foreach (var action in result.Actions)
        {
            switch (action.Status)
            {
                case BuildAction.StatusApplied:
                    applied++;
                    break;
                case BuildAction.StatusFailed:
                    failed++;
                    break;
                default:
                    omitted++;
                    break;
            }
        }

        result.Applied = applied;
        result.Omitted = omitted;
        result.Failed = failed;
    }
}
