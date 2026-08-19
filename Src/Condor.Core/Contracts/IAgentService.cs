using System.Threading;
using System.Threading.Tasks;
using Condor.Core.Models;

namespace Condor.Core.Contracts;

public interface IAgentService
{
    Task<AgentResult> RunAsync(string intention, CancellationToken cancellationToken = default);
}
