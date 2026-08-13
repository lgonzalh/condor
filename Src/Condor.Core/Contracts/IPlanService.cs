using Condor.Core.Models;

namespace Condor.Core.Contracts;

public interface IPlanService
{
    Task<WorkPlan> BuildPlanAsync(string userRequest, CancellationToken cancellationToken = default);
}
