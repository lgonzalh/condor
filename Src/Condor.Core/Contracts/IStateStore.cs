using Condor.Core.Models;

namespace Condor.Core.Contracts;

public interface IStateStore
{
    Task SaveAssessmentAsync(AssessmentResult result, CancellationToken cancellationToken = default);

    Task<AssessmentResult?> LoadAssessmentAsync(CancellationToken cancellationToken = default);

    Task SaveContextAsync(ProjectContext context, CancellationToken cancellationToken = default);

    Task<ProjectContext?> LoadContextAsync(CancellationToken cancellationToken = default);

    Task SavePlanAsync(WorkPlan plan, CancellationToken cancellationToken = default);

    Task<WorkPlan?> LoadPlanAsync(CancellationToken cancellationToken = default);
}
