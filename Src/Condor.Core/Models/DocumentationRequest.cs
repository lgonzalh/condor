using System;
using System.Collections.Generic;

namespace Condor.Core.Models;

public sealed class DocumentationRequest
{
    public string TaskType { get; init; } = "";
    public string Intention { get; init; } = "";
    public string WorkingDirectory { get; init; } = "";
    public DateTime StartedAtUtc { get; init; }
    public DateTime CompletedAtUtc { get; init; }
    public string Outcome { get; init; } = "";
    public string? Reason { get; init; }
    public List<string> Before { get; init; } = new();
    public List<string> During { get; init; } = new();
    public List<string> After { get; init; } = new();
}
