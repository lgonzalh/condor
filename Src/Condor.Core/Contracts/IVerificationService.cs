using Condor.Core.Models;

namespace Condor.Core.Contracts;

public interface IVerificationService
{
    Task<VerificationResult> VerifyAsync(CancellationToken cancellationToken = default);
}
