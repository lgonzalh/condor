using Condor.Core.Models;

namespace Condor.Core.Contracts;

public interface ISetupService
{
    Task<SetupResult> PrepareAsync(bool refreshAssessment = false, CancellationToken cancellationToken = default);
}
