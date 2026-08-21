using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Condor.Core.Models;
using Condor.Infrastructure.Detection;

namespace Condor.Infrastructure.DependencyBootstrap;

/// <summary>
/// Verificador de salud de Ollama que DISTINGUE los cuatro estados posibles:
///   - No instalado (ni ejecutable).
///   - Instalado pero el server no responde (la app puede estar abierta).
///   - Instalado con server real respondiendo en el endpoint local.
///
/// Regla: NUNCA se da Ollama por disponible solo porque exista "ollama.exe". La
/// comprobacion valida es que el SERVER real responda correctamente mediante el
/// mismo mecanismo/endpoint local que usa Condor (GET /api/version sobre
/// 127.0.0.1:11434). Reutiliza OllamaDetector (autoridad real) y ToolDetector
/// (presencia del ejecutable).
/// </summary>
public class OllamaHealthChecker
{
    public const string DefaultApiBase = "http://127.0.0.1:11434";
    private const int DefaultProbeTimeoutMilliseconds = 3000;

    private readonly OllamaDetector _detector;
    private readonly string _apiBase;

    public OllamaHealthChecker(OllamaDetector? detector = null, string? apiBase = null)
    {
        _detector = detector ?? new OllamaDetector();
        _apiBase = string.IsNullOrWhiteSpace(apiBase) ? DefaultApiBase : apiBase;
    }

    /// <summary>
    /// Detecta el estado real de Ollama. Devuelve un OllamaStatus con
    /// Installed / ServerRunning poblados por autoridad real.
    /// </summary>
    public virtual async Task<OllamaStatus> DetectAsync(CancellationToken cancellationToken = default)
    {
        return await _detector.DetectAsync(cancellationToken);
    }

    /// <summary>True si el ejecutable de Ollama esta presente (esta instalado).</summary>
    public virtual bool IsInstalled()
        => ToolDetector.FindInPath("ollama") is not null;

    /// <summary>
    /// Comprueba que el SERVER real responde correctamente en el endpoint local
    /// (GET /api/version). Esta es la unica prueba de "server disponible".
    /// </summary>
    public virtual async Task<bool> IsServerAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMilliseconds(DefaultProbeTimeoutMilliseconds) };
            var version = await http.GetFromJsonAsync<OllamaVersionCheck>(_apiBase + "/api/version", cancellationToken);
            return !string.IsNullOrWhiteSpace(version?.Version);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Clasifica el estado de salud de Ollama en una de las ramas del bootstrap.</summary>
    public virtual async Task<OllamaHealth> ClassifyAsync(CancellationToken cancellationToken = default)
    {
        if (!IsInstalled())
        {
            return OllamaHealth.NotInstalled;
        }

        return await IsServerAvailableAsync(cancellationToken)
            ? OllamaHealth.ServerAvailable
            : OllamaHealth.InstalledServerDown;
    }

    private sealed class OllamaVersionCheck
    {
        public string? Version { get; set; }
    }
}
