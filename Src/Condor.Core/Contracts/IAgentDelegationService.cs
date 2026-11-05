using System.Threading;
using System.Threading.Tasks;
using Condor.Core.Models;

namespace Condor.Core.Contracts;

/// <summary>Ejecuta subtareas en worktrees aislados y devuelve evidencia al agente principal.</summary>
public interface IAgentDelegationService
{
    Task<AgentStep> ExecuteAsync(
        AgentAction action,
        string workingDirectory,
        int iteration,
        IAgentProgressObserver? progress,
        CancellationToken cancellationToken = default);
}
