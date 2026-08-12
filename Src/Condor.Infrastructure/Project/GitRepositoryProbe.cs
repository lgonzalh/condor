using Condor.Core.Models;
using Condor.Core.Project;
using Condor.Infrastructure.Probing;

namespace Condor.Infrastructure.Project;

public sealed class GitProbeOutcome
{
    public GitProjectState? State { get; set; }
    public bool CouldNotVerify { get; set; }
}

public sealed class GitRepositoryProbe
{
    private readonly DiscoveryLimits limits;

    public GitRepositoryProbe(DiscoveryLimits? limits = null)
    {
        this.limits = limits ?? DiscoveryLimits.Default;
    }

    public async Task<GitProbeOutcome> ProbeAsync(
        string rootPath,
        string gitPath,
        CancellationToken cancellationToken = default)
    {
        var insideWorkTree = await RunAsync(gitPath, "rev-parse --is-inside-work-tree", rootPath, cancellationToken);

        if (insideWorkTree is null)
        {
            return new GitProbeOutcome { CouldNotVerify = true };
        }

        if (!GitOutputParser.IsInsideWorkTree(insideWorkTree))
        {
            return new GitProbeOutcome();
        }

        var state = new GitProjectState { Status = DetectionStatus.Detected };

        var branchOutput = await RunAsync(gitPath, "branch --show-current", rootPath, cancellationToken);
        if (branchOutput is null)
        {
            MarkError(state, "no fue posible consultar la rama actual");
        }
        else
        {
            state.Branch = GitOutputParser.ParseBranch(branchOutput);
        }

        var statusOutput = await RunAsync(gitPath, "status --porcelain", rootPath, cancellationToken);
        if (statusOutput is null)
        {
            MarkError(state, "no fue posible consultar el estado del repositorio");
        }
        else
        {
            state.IsDirty = GitOutputParser.IsDirty(statusOutput);
        }

        var logOutput = await RunAsync(
            gitPath,
            "log -n " + limits.MaxGitCommits + " --abbrev=" + limits.CommitHashLength + " --format=%h|%s",
            rootPath,
            cancellationToken);
        if (logOutput is null)
        {
            MarkError(state, "no fue posible consultar los cambios recientes");
        }
        else
        {
            state.Commits.AddRange(GitOutputParser.ParseLog(
                logOutput,
                limits.MaxGitCommits,
                limits.CommitHashLength,
                limits.MaxCommitSubjectLength));
        }

        return new GitProbeOutcome { State = state };
    }

    private static async Task<string?> RunAsync(
        string gitPath,
        string arguments,
        string rootPath,
        CancellationToken cancellationToken)
    {
        return await ProcessProbe.RunAsync(
            gitPath,
            arguments,
            DiscoveryLimits.Default.GitOperationTimeoutMilliseconds,
            rootPath,
            cancellationToken);
    }

    private static void MarkError(GitProjectState state, string reason)
    {
        state.Status = DetectionStatus.Error;
        if (state.Reason is null)
        {
            state.Reason = reason;
        }
    }
}