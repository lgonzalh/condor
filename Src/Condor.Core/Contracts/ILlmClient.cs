using Condor.Core.Models;

namespace Condor.Core.Contracts;

public interface ILlmClient
{
    Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default);
}
