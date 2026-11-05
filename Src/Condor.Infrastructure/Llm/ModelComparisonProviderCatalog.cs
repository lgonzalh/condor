using Condor.Core.Contracts;

namespace Condor.Infrastructure.Llm;

public sealed record ModelComparisonProvider(string Id, string Name, ILlmClient Client, bool RequiresLocalModelSelection);

/// <summary>Registra Ollama siempre y proveedores opcionales configurados explícitamente por el usuario.</summary>
public sealed class ModelComparisonProviderCatalog : IDisposable
{
    public const string OpenAiCompatibleBaseUrlVariable = "CONDOR_OPENAI_COMPATIBLE_BASE_URL";
    public const string OpenAiCompatibleApiKeyVariable = "CONDOR_OPENAI_COMPATIBLE_API_KEY";

    private readonly List<IDisposable> _ownedClients = new();
    private readonly Dictionary<string, ModelComparisonProvider> _providers = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<ModelComparisonProvider> Providers => _providers.Values;

    private ModelComparisonProviderCatalog(ILlmClient ollamaClient, string? compatibleBaseUrl, string? compatibleApiKey)
    {
        _providers.Add("ollama", new ModelComparisonProvider("ollama", "Ollama", ollamaClient, RequiresLocalModelSelection: true));

        if (!string.IsNullOrWhiteSpace(compatibleBaseUrl))
        {
            var client = new OpenAiCompatibleClient(compatibleBaseUrl, compatibleApiKey);
            _ownedClients.Add(client);
            _providers.Add("openai-compatible", new ModelComparisonProvider(
                "openai-compatible", "OpenAI-compatible", client, RequiresLocalModelSelection: false));
        }
    }

    public static ModelComparisonProviderCatalog CreateDefault(LocalModelSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new ModelComparisonProviderCatalog(
            session.Llm,
            Environment.GetEnvironmentVariable(OpenAiCompatibleBaseUrlVariable),
            Environment.GetEnvironmentVariable(OpenAiCompatibleApiKeyVariable));
    }

    public bool TryGet(string providerId, out ModelComparisonProvider provider)
        => _providers.TryGetValue(providerId, out provider!);

    public void Dispose()
    {
        foreach (var client in _ownedClients) client.Dispose();
        _ownedClients.Clear();
    }
}
