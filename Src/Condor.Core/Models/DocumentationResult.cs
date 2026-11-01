using System;

namespace Condor.Core.Models;

public sealed class DocumentationResult
{
    public bool Success { get; init; }
    public string? RelativePath { get; init; }
    public string? Reason { get; init; }
    public DateTime GeneratedAtUtc { get; init; }
}
