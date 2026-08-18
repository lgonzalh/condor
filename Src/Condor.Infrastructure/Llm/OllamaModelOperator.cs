using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Condor.Infrastructure.Detection;

namespace Condor.Infrastructure.Llm;

public sealed class OllamaModelOperator
{
    private const string ApiBase = "http://127.0.0.1:11434";

    private readonly HttpClient _http;
    private readonly OllamaDetector _detector;

    public OllamaModelOperator()
        : this(new HttpClient { Timeout = Timeout.InfiniteTimeSpan })
    {
    }

    public OllamaModelOperator(HttpClient http, OllamaDetector? detector = null)
    {
        _http = http;
        _detector = detector ?? new OllamaDetector();
    }

    public async Task<bool> PullAsync(
        string model,
        int timeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return false;
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(timeoutMilliseconds);

        try
        {
            var response = await _http.PostAsJsonAsync(
                ApiBase + "/api/pull",
                new { model, stream = false },
                linked.Token);

            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            await VerifyInstallationAsync(model, timeoutMilliseconds, cancellationToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> IsInstalledAsync(string model, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return false;
        }

        try
        {
            var status = await _detector.DetectAsync(cancellationToken);
            foreach (var m in status.Models ?? new System.Collections.Generic.List<Condor.Core.Models.ModelInfo>())
            {
                if (string.Equals(m.Name, model, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private async Task VerifyInstallationAsync(string model, int timeout, CancellationToken ct)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(timeout);

        var attempts = 0;
        while (attempts < 10)
        {
            if (await IsInstalledAsync(model, linked.Token))
            {
                return;
            }

            attempts++;
            try
            {
                await Task.Delay(500, linked.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
