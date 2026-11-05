using System;
using System.IO;
using System.Threading.Tasks;
using Condor.Core.Contracts;
using Condor.Core.Models;
using Condor.Core.Planning;
using Condor.Infrastructure.Context;

namespace Condor.Infrastructure.Planning
{
    public sealed class PlanService : IPlanService
    {
        private const string ReasonTimeout = "Tiempo excedido al generar el plan.";

        private readonly IStateStore _stateStore;
        private readonly ContextService _contextService;
        private readonly PlanLimits _limits;
        private readonly string? _workingDirectory;

        public PlanService(
            IStateStore stateStore,
            ContextService? contextService = null,
            PlanLimits? limits = null,
            string? workingDirectory = null)
        {
            _stateStore = stateStore;
            _contextService = contextService ?? new ContextService(stateStore);
            _limits = limits ?? PlanLimits.Default;
            _workingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                ? null
                : Path.GetFullPath(workingDirectory);
        }

        public async Task<WorkPlan> BuildPlanAsync(
            string userRequest,
            CancellationToken cancellationToken = default)
        {
            var timeout = TimeSpan.FromMilliseconds(_limits.PlanTimeoutMilliseconds);

            try
            {
                var context = await _stateStore
                    .LoadContextAsync(cancellationToken)
                    .WaitAsync(timeout, cancellationToken);

                if (_workingDirectory is not null &&
                    (context is null || !SameDirectory(context.WorkingDirectory, _workingDirectory)))
                {
                    context = await _contextService
                        .BuildContextAsync(_workingDirectory, cancellationToken)
                        .WaitAsync(timeout, cancellationToken);
                }

                context ??= await _contextService
                    .BuildContextAsync(cancellationToken)
                    .WaitAsync(timeout, cancellationToken);

                return PlanGenerator.Generate(context, userRequest, _limits);
            }
            catch (TimeoutException)
            {
                return new WorkPlan
                {
                    SchemaVersion = "1.0.0",
                    Status = DetectionStatus.Limited,
                    Reason = ReasonTimeout,
                    GeneratedAtUtc = DateTime.UtcNow
                };
            }
        }

        private static bool SameDirectory(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            {
                return false;
            }

            try
            {
                var normalizedLeft = Path.TrimEndingDirectorySeparator(Path.GetFullPath(left));
                var normalizedRight = Path.TrimEndingDirectorySeparator(Path.GetFullPath(right));
                var comparison = OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal;
                return string.Equals(normalizedLeft, normalizedRight, comparison);
            }
            catch
            {
                return false;
            }
        }
    }
}
