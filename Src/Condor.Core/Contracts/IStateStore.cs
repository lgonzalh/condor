using Condor.Core.Models;

namespace Condor.Core.Contracts;

public interface IStateStore
{
    Task SaveAssessmentAsync(AssessmentResult result, CancellationToken cancellationToken = default);
}
