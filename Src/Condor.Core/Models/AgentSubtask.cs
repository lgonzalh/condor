namespace Condor.Core.Models;

/// <summary>Solicitud acotada para un agente hijo, ejecutada en su propio worktree.</summary>
public sealed class AgentSubtask
{
    public string Id { get; set; } = "";
    public string Task { get; set; } = "";
    public string ProfileId { get; set; } = "engineer";
    public int MaxDurationSeconds { get; set; } = 300;
    public int MaxIterations { get; set; } = 8;
    public int MaxOutputTokens { get; set; } = 4096;
}
