using Condor.Core.Models;

namespace Condor.Core.Contracts;

public interface ICycleService
{
    Task<CycleResult> AdvanceAsync(string userRequest, CancellationToken cancellationToken = default);
}
