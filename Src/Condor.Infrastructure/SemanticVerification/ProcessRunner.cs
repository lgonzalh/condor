using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Condor.Core.Models;

namespace Condor.Infrastructure.SemanticVerification;

public sealed class ProcessRunner
{
    private const string DotNet = "dotnet";

    public async Task<ProcessRunResult> RunAsync(
        string workingDirectory,
        string manifest,
        string kind,
        int timeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        var validation = Validate(workingDirectory, manifest);
        if (!validation.Success)
        {
            return ProcessRunResult.OfInvalid(validation.Reason!);
        }

        return await ExecuteAsync(workingDirectory, manifest, kind, timeoutMilliseconds, cancellationToken);
    }

    private static async Task<ProcessRunResult> ExecuteAsync(
        string workingDirectory,
        string manifest,
        string kind,
        int timeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = DotNet,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        psi.ArgumentList.Add(kind == SemanticCheck.KindTest ? "test" : "build");
        psi.ArgumentList.Add(manifest);
        psi.ArgumentList.Add("--no-restore");

        return await ExecuteAsync(psi, timeoutMilliseconds, cancellationToken);
    }

    private static async Task<ProcessRunResult> ExecuteAsync(
        ProcessStartInfo psi,
        int timeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = psi };

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data is not null) { stdout.AppendLine(e.Data); } };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) { stderr.AppendLine(e.Data); } };

        try
        {
            if (!process.Start())
            {
                return ProcessRunResult.OfNotExecutable();
            }
        }
        catch
        {
            return ProcessRunResult.OfNotExecutable();
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linked.CancelAfter(timeoutMilliseconds);

            try
            {
                await process.WaitForExitAsync(linked.Token);
            }
            catch (OperationCanceledException)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    TryKill(process);
                    return ProcessRunResult.OfCancelled();
                }

                TryKill(process);
                await process.WaitForExitAsync(CancellationToken.None);
                return ProcessRunResult.OfTimedOut(process.ExitCode, stdout.ToString() + stderr.ToString());
            }

            var combined = stdout.ToString() + stderr.ToString();
            return ProcessRunResult.OfOk(process.ExitCode, combined);
        }
        catch
        {
            TryKill(process);
            return ProcessRunResult.OfIncomplete();
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(true);
            }
        }
        catch
        {
        }
    }

    private static ValidationResult Validate(string workingDirectory, string manifest)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory) || !Directory.Exists(workingDirectory))
        {
            return ValidationResult.Fail("El WorkingDirectory no existe o no es valido.");
        }

        if (string.IsNullOrWhiteSpace(manifest))
        {
            return ValidationResult.Fail("No se indico un manifiesto .NET.");
        }

        var manifestPath = Path.IsPathRooted(manifest)
            ? Path.GetFullPath(manifest)
            : Path.GetFullPath(Path.Combine(workingDirectory, manifest));

        if (!IsWithin(workingDirectory, manifestPath))
        {
            return ValidationResult.Fail("El manifiesto esta fuera del WorkingDirectory.");
        }

        return ValidationResult.Ok();
    }

    private static bool IsWithin(string workingDirectory, string manifestPath)
    {
        var baseFull = Path.GetFullPath(workingDirectory).TrimEnd(Path.DirectorySeparatorChar);
        var targetFull = Path.GetFullPath(manifestPath);

        return targetFull.Equals(baseFull, StringComparison.OrdinalIgnoreCase) ||
               targetFull.StartsWith(baseFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private readonly record struct ValidationResult(bool Success, string? Reason)
    {
        public static ValidationResult Ok() => new(true, null);
        public static ValidationResult Fail(string reason) => new(false, reason);
    }
}

public readonly record struct ProcessRunResult(
    bool Executed,
    int? ExitCode,
    string Output,
    bool TimedOut,
    bool Cancelled,
    bool NotExecutable,
    bool Incomplete,
    string? ValidationReason)
{
    public static ProcessRunResult OfOk(int exitCode, string output) =>
        new(true, exitCode, output, false, false, false, false, null);

    public static ProcessRunResult OfInvalid(string reason) =>
        new(false, null, "", false, false, false, false, reason);

    public static ProcessRunResult OfNotExecutable() =>
        new(false, null, "", false, false, true, false, null);

    public static ProcessRunResult OfTimedOut(int? exitCode, string output) =>
        new(true, exitCode, output, true, false, false, false, null);

    public static ProcessRunResult OfCancelled() =>
        new(false, null, "", false, true, false, false, null);

    public static ProcessRunResult OfIncomplete() =>
        new(false, null, "", false, false, false, true, null);
}
