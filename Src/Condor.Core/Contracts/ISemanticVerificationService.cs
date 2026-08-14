using Condor.Core.Models;

namespace Condor.Core.Contracts;

public interface ISemanticVerificationService
{
    Task<SemanticVerificationResult> VerifySemanticAsync(
        bool compile,
        bool test,
        CancellationToken cancellationToken = default);
}
