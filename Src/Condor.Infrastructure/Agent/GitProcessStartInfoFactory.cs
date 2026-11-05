using System.Diagnostics;

namespace Condor.Infrastructure.Agent;

internal static class GitProcessStartInfoFactory
{
    public static ProcessStartInfo Create(string workingDirectory, bool redirectStandardInput = false) => new("git")
    {
        WorkingDirectory = workingDirectory,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        RedirectStandardInput = redirectStandardInput,
        UseShellExecute = false,
        CreateNoWindow = true
    };
}
