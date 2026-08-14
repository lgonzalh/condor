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

    Task SaveBuildAsync(BuildResult result, CancellationToken cancellationToken = default);

    Task<BuildResult?> LoadBuildAsync(CancellationToken cancellationToken = default);

    Task SaveVerificationAsync(VerificationResult result, CancellationToken cancellationToken = default);

    Task<VerificationResult?> LoadVerificationAsync(CancellationToken cancellationToken = default);

    Task SaveCycleAsync(CycleResult result, CancellationToken cancellationToken = default);

    Task<CycleResult?> LoadCycleAsync(CancellationToken cancellationToken = default);
}
