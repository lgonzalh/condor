using System.Threading;
using System.Threading.Tasks;
using Condor.Core.Models;

namespace Condor.Core.Contracts;

public interface IDocumenter
{
    Task<DocumentationResult> WriteAsync(
        DocumentationRequest request,
        CancellationToken cancellationToken = default);
}
