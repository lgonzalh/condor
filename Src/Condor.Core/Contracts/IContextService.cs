using Condor.Core.Models;

namespace Condor.Core.Contracts;

public interface IContextService
{
    Task<ProjectContext> BuildContextAsync(CancellationToken cancellationToken = default);
}