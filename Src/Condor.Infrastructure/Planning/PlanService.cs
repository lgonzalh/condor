using System;
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

        public PlanService(
            IStateStore stateStore,
            ContextService? contextService = null,
            PlanLimits? limits = null)
        {
            _stateStore = stateStore;
            _contextService = contextService ?? new ContextService(stateStore);
            _limits = limits ?? PlanLimits.Default;
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
    }
}
