namespace Condor.Infrastructure.Probing;

public static class CimProbe
{
    public static async Task<string?> QueryAsync(
        string command,
        int timeoutMilliseconds = 15000,
        CancellationToken cancellationToken = default)
    {
        var script =
            "[Console]::OutputEncoding=[System.Text.Encoding]::UTF8; " +
            command +
            " | ConvertTo-Json -Depth 4 -Compress";

        return await ProcessProbe.RunAsync(
            "powershell.exe",
            "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"" + script + "\"",
            timeoutMilliseconds,
            cancellationToken);
    }
}
