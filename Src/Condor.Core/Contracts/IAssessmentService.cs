using Condor.Core.Models;

namespace Condor.Core.Contracts;

public interface IAssessmentService
{
    Task<AssessmentResult> ExecuteAsync(AssessmentRequest request, CancellationToken cancellationToken = default);
}
