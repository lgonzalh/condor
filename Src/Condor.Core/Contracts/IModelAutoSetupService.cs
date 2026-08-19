using Condor.Core.Models;

namespace Condor.Core.Contracts;

public interface IModelAutoSetupService
{
    Task<ModelSelectionResult> EnsureModelAsync(
        string? purpose = null,
        CancellationToken cancellationToken = default,
        IStartupProgressObserver? progress = null);
}
