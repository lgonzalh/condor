using Condor.Core.Models;

namespace Condor.Core.Contracts;

public interface IBuildService
{
    Task<BuildResult> ApplyPlanAsync(CancellationToken cancellationToken = default);
}
