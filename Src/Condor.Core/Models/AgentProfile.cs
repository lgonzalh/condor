using System.Collections.Generic;

namespace Condor.Core.Models;

/// <summary>
/// Defines a specialized operating mode for an agent, including the tools it may use.
/// </summary>
public sealed record AgentProfile(
    string Id,
    string Name,
    string Objective,
    string Instructions,
    IReadOnlySet<string> AllowedActions,
    bool ReadOnly);
