using System.Collections.Generic;
using Condor.Core.Models;

namespace Condor.Core.Catalog;

/// <summary>
/// Catalogo minimo de modelos candidatos para la obtencion automatica.
/// Justificacion (respaldo en el repositorio):
///   - T-003 y la calibracion de memoria indican que un modelo 7B Q4 (~4,7 GB)
///     es viable con RAM libre suficiente y que un 8B queda al limite.
///   - T-002/T-003 probaron modelos de la familia coder de Ollama para el
///     proposito development.
///   - Estas son variantes base oficiales de Ollama (obtenibles por nombre).
/// No es un catalogo cerrado; la seleccion final la decide ModelSelector
/// segun el hardware real.
/// </summary>
public static class ModelCatalog
{
    public static IReadOnlyList<ModelCandidate> Default { get; } = new List<ModelCandidate>
    {
        new()
        {
            Name = "qwen2.5-coder:7b",
            PullName = "qwen2.5-coder:7b",
            SizeBytes = 4706L * 1024 * 1024,
            Family = "qwen2",
            ParameterSize = "7.6B",
            Quantization = "Q4_K_M",
            Capabilities = new List<string> { "completion" }
        },
        new()
        {
            Name = "llama3.2:3b",
            PullName = "llama3.2:3b",
            SizeBytes = 1991L * 1024 * 1024,
            Family = "llama",
            ParameterSize = "3.2B",
            Quantization = "Q4_K_M",
            Capabilities = new List<string> { "completion" }
        },
        new()
        {
            Name = "qwen3:8b",
            PullName = "qwen3:8b",
            SizeBytes = 5000L * 1024 * 1024,
            Family = "qwen3",
            ParameterSize = "8B",
            Quantization = "Q4_K_M",
            Capabilities = new List<string> { "completion" }
        }
    };
}
