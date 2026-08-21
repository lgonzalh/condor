using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Condor.Core.Contracts;

namespace Condor.Infrastructure.Llm;

/// <summary>
/// Sesion unica y reutilizable del proveedor local durante una ejecucion de
/// Condor. Centraliza un unico HttpClient y un unico modelo activo para que
/// ningun servicio cree su propio cliente/disponga de recursos duplicados.
///
/// Ownership:
/// - Condor es cliente de Ollama, no propietario de llama-server.exe (lo
///   gestiona Ollama internamente). Por tanto NUNCA se cierran procesos
///   externos aqui: la reutilizacion es por sesion de modelo y la liberacion se
///   delega al mecanismo oficial de Ollama (keep_alive=0).
/// - Reutilizacion: ante una solicitud para el mismo modelo ya activo y con el
///   proveedor disponible, no se crea ni inicializa ningun recurso nuevo.
/// - Liberacion: ReleaseAsync es idempotente y seguro de invocar en cualquier
///   ruta de cierre (normal, error, cancelacion, Ctrl+C, EOF, /salir).
/// </summary>
public sealed class LocalModelSession : ILlmProviderLifecycle, IDisposable
{
    private readonly object _gate = new();
    private readonly HttpClient _http;
    private readonly OllamaClient _llm;
    private string? _activeModel;
    private bool _released;
    private bool _disposed;

    public LocalModelSession()
        : this(new HttpClient { Timeout = TimeSpan.FromMilliseconds(OllamaClient.DefaultTimeoutMilliseconds) })
    {
    }

    public LocalModelSession(HttpClient httpClient)
    {
        _http = httpClient;
        _llm = new OllamaClient(httpClient);
    }

    /// <summary>Cliente LLM reutilizable de la sesion (unico para toda la ejecucion).</summary>
    public ILlmClient Llm => _llm;

    /// <summary>HttpClient compartido de la sesion, para que otros conectores (descarga/deteccion) lo reutilicen.</summary>
    public HttpClient SharedHttpClient => _http;

    /// <summary>Diagnostico de disponibilidad del proveedor para esta sesion.</summary>
    public ILlmProviderDiagnostics Diagnostics => _llm;

    public string ProviderName => _llm.ProviderName;

    public string? ActiveModel
    {
        get { lock (_gate) return _activeModel; }
    }

    public async Task<bool> EnsureAvailableAsync(string model, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return false;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        bool available;
        try
        {
            available = await _llm.IsAvailableAsync(cancellationToken);
        }
        catch
        {
            available = false;
        }

        // Deduplicacion: si ya existe la sesion activa para el MISMO modelo y el
        // proveedor responde, se reutiliza tal cual (sin tocar recursos).
        lock (_gate)
        {
            if (_activeModel is not null &&
                string.Equals(_activeModel, model, StringComparison.OrdinalIgnoreCase) &&
                !_released)
            {
                return available;
            }

            // Registrar la sesion activa = el modelo de esta ejecucion.
            _activeModel = model;
            _released = false;
        }

        return available;
    }

    /// <summary>
    /// Libera el modelo activo mediante el mecanismo oficial del proveedor
    /// (Ollama keep_alive=0). Idempotente: solo descarga la primera vez.
    /// </summary>
    public async Task ReleaseAsync(CancellationToken cancellationToken = default)
    {
        string? model;
        lock (_gate)
        {
            if (_released)
            {
                return;
            }
            model = _activeModel;
            _released = true;
        }

        if (model is null)
        {
            return;
        }

        try
        {
            await _llm.ReleaseModelAsync(model, cancellationToken);
        }
        catch
        {
            // La liberacion nunca debe impedir el cierre de Condor.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _http.Dispose();
    }
}
