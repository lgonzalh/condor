using System;
using System.IO;
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
        => await PullAsync(model, timeoutMilliseconds, null, cancellationToken);

    /// <summary>
    /// Obtiene el modelo de Ollama. Si se proporciona un callback de progreso,
    /// la descarga se realiza en modo streaming y se reporta el porcentaje REAL
    /// de descarga (0-100) emitido por el propio servidor; en otro caso se usa
    /// el modo no-streaming (sin progreso). El porcentaje jamas se inventa.
    /// </summary>
    public async Task<bool> PullAsync(
        string model,
        int timeoutMilliseconds,
        Action<double?>? progress,
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
                new { model, stream = progress is not null },
                linked.Token);

            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            if (progress is not null)
            {
                using var stream = await response.Content.ReadAsStreamAsync(linked.Token);
                using var reader = new StreamReader(stream);
                string? line;
                while ((line = await reader.ReadLineAsync()) is not null)
                {
                    var percent = ParseDownloadPercent(line);
                    progress(percent);
                }
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

    /// <summary>
    /// Extrae el porcentaje real de descarga del JSON de progreso emitido por
    /// Ollama. Devuelve null cuando el dato no es un porcentaje o el servidor
    /// no reporto progreso (p. ej. eventos de estado como "pulling manifest"
    /// o "success"). Exige total/completed reales; nunca inventa numeros.
    /// </summary>
    internal static double? ParseDownloadPercent(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!root.TryGetProperty("status", out var status) ||
                status.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var statusText = status.GetString();
            if (statusText is null ||
                !statusText.Equals("downloading", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (!root.TryGetProperty("completed", out var completedEl) ||
                completedEl.ValueKind != JsonValueKind.Number ||
                !root.TryGetProperty("total", out var totalEl) ||
                totalEl.ValueKind != JsonValueKind.Number)
            {
                return null;
            }

            var completed = completedEl.GetInt64();
            var total = totalEl.GetInt64();
            if (total <= 0)
            {
                return null;
            }

            var percent = (double)completed / total * 100.0;
            return Math.Clamp(percent, 0.0, 100.0);
        }
        catch
        {
            return null;
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
