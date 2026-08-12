using System.Diagnostics;
using System.Text;

namespace Condor.Infrastructure.Probing;

public static class ProcessProbe
{
    public static Task<string?> RunAsync(
        string fileName,
        string arguments,
        int timeoutMilliseconds,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(fileName, arguments, timeoutMilliseconds, null, cancellationToken);
    }

    public static Task<string?> RunAsync(
        string fileName,
        string arguments,
        int timeoutMilliseconds,
        string? workingDirectory,
        CancellationToken cancellationToken = default)
    {
        return RunCoreAsync(fileName, arguments, timeoutMilliseconds, workingDirectory, cancellationToken);
    }

    private static async Task<string?> RunCoreAsync(
        string fileName,
        string arguments,
        int timeoutMilliseconds,
        string? workingDirectory,
        CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            if (!string.IsNullOrWhiteSpace(workingDirectory))
            {
                startInfo.WorkingDirectory = workingDirectory;
            }

            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                return null;
            }

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeoutMilliseconds);

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // El proceso pudo terminar entre la cancelacion y el kill.
                }
            }

            var outputs = await Task.WhenAll(outputTask, errorTask);
            return outputs[0];
        }
        catch
        {
            return null;
        }
    }
}