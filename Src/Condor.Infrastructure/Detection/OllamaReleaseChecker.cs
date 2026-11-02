using System.Net.Http.Headers;
using System.Text.Json;

namespace Condor.Infrastructure.Detection;

/// <summary>Consulta la version estable publicada por Ollama sin bloquear el arranque indefinidamente.</summary>
public sealed class OllamaReleaseChecker : IDisposable
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/ollama/ollama/releases/latest";
    private readonly HttpClient _http;
    private readonly bool _ownsClient;

    public OllamaReleaseChecker(HttpClient? httpClient = null)
    {
        _ownsClient = httpClient is null;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
    }

    public async Task<OllamaReleaseCheckResult> CheckAsync(
        string? installedVersion,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(installedVersion))
        {
            return new OllamaReleaseCheckResult(null, false, false);
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Condor", "1.0"));
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new OllamaReleaseCheckResult(null, false, false);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!document.RootElement.TryGetProperty("tag_name", out var tag) || tag.ValueKind != JsonValueKind.String)
            {
                return new OllamaReleaseCheckResult(null, false, false);
            }

            var latest = Normalize(tag.GetString());
            var installed = Normalize(installedVersion);
            if (latest is null || installed is null)
            {
                return new OllamaReleaseCheckResult(latest, false, false);
            }

            return new OllamaReleaseCheckResult(latest, IsNewer(latest, installed), true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return new OllamaReleaseCheckResult(null, false, false);
        }
    }

    internal static bool IsNewer(string latest, string installed)
    {
        var latestParts = ParseVersion(latest);
        var installedParts = ParseVersion(installed);
        return latestParts is not null && installedParts is not null && latestParts > installedParts;
    }

    private static Version? ParseVersion(string value)
    {
        var normalized = Normalize(value);
        return normalized is not null && Version.TryParse(normalized, out var version) ? version : null;
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V')) normalized = normalized[1..];
        var suffix = normalized.IndexOfAny(new[] { '-', '+' });
        if (suffix >= 0) normalized = normalized[..suffix];
        return normalized;
    }

    public void Dispose()
    {
        if (_ownsClient) _http.Dispose();
    }
}

public sealed record OllamaReleaseCheckResult(string? LatestVersion, bool UpdateAvailable, bool CheckSucceeded);
