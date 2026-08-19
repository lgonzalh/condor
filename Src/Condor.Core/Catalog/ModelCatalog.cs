using System.Collections.Generic;
using Condor.Core.Models;

namespace Condor.Core.Catalog;

/// <summary>
/// Catalogo de variantes de modelos candidatos para la seleccion automatica.
/// Cada entrada describe recursos reales (peso/contexto/cuantizacion) y un
/// perfil de capacidad de ingenieria relativo por dominio.
/// Los pesos y tamanos se verificaron contra el registro de Ollama. Los
/// niveles de capacidad (CodingLevel/MultiFileLevel/... ) son estimaciones
/// relativas de familia/dominio: la prueba definitiva es la E2E real del
/// agente. No son una promesa de exito.
/// </summary>
public static class ModelCatalog
{
    public static IReadOnlyList<ModelCandidate> Default { get; } = new List<ModelCandidate>
    {
        new()
        {
            Name = "qwen2.5-coder:3b",
            PullName = "qwen2.5-coder:3b",
            SizeBytes = 1848L * 1024 * 1024,
            WeightGb = 1.8,
            Family = "qwen2",
            ParameterSize = "3B",
            Quantization = "Q4_K_M",
            ContextWindow = 32768,
            CodingLevel = 3,
            MultiFileLevel = 2,
            StructuredOutput = true,
            ToolUse = true,
            Stability = true,
            Purpose = "agente",
            Capabilities = new List<string> { "completion", "tool-use", "structured-output", "coding" }
        },
        new()
        {
            // Alternativa menor al 3B: cuando la RAM no permite cargar
            // qwen2.5-coder:3b (ni mayor), Condor puede descargar y usar este
            // modelo como salida viable manteniendo capacidades de agente.
            Name = "qwen2.5-coder:1.5b",
            PullName = "qwen2.5-coder:1.5b",
            SizeBytes = 986L * 1024 * 1024,
            WeightGb = 0.92,
            Family = "qwen2",
            ParameterSize = "1.5B",
            Quantization = "Q4_K_M",
            ContextWindow = 32768,
            CodingLevel = 2,
            MultiFileLevel = 1,
            StructuredOutput = true,
            ToolUse = true,
            Stability = true,
            Purpose = "agente",
            Capabilities = new List<string> { "completion", "tool-use", "structured-output", "coding" }
        },
        new()
        {
            Name = "llama3.2:1b",
            PullName = "llama3.2:1b",
            SizeBytes = 1334L * 1024 * 1024,
            WeightGb = 1.28,
            Family = "llama",
            ParameterSize = "1.2B",
            Quantization = "Q4_K_M",
            ContextWindow = 128000,
            CodingLevel = 1,
            MultiFileLevel = 1,
            StructuredOutput = true,
            ToolUse = true,
            Stability = true,
            Purpose = "agente",
            Capabilities = new List<string> { "completion", "tool-use", "structured-output", "coding" }
        },
        new()
        {
            // Ultimo recurso: modelo muy pequeno, ultima salida viable si ni
            // siquiera el 1.5B cabe en RAM.
            Name = "qwen2.5-coder:0.5b",
            PullName = "qwen2.5-coder:0.5b",
            SizeBytes = 397L * 1024 * 1024,
            WeightGb = 0.37,
            Family = "qwen2",
            ParameterSize = "0.5B",
            Quantization = "Q4_K_M",
            ContextWindow = 32768,
            CodingLevel = 1,
            MultiFileLevel = 1,
            StructuredOutput = true,
            ToolUse = false,
            Stability = true,
            Purpose = "agente",
            Capabilities = new List<string> { "completion", "structured-output", "coding" }
        },
        new()
        {
            Name = "llama3.2:3b",
            PullName = "llama3.2:3b",
            SizeBytes = 1991L * 1024 * 1024,
            WeightGb = 1.9,
            Family = "llama",
            ParameterSize = "3.2B",
            Quantization = "Q4_K_M",
            ContextWindow = 128000,
            CodingLevel = 2,
            MultiFileLevel = 1,
            StructuredOutput = true,
            ToolUse = true,
            Stability = true,
            Purpose = "agente",
            Capabilities = new List<string> { "completion", "tool-use", "structured-output", "coding" }
        },
        new()
        {
            Name = "qwen2.5-coder:7b",
            PullName = "qwen2.5-coder:7b",
            SizeBytes = 4706L * 1024 * 1024,
            WeightGb = 4.36,
            Family = "qwen2",
            ParameterSize = "7.6B",
            Quantization = "Q4_K_M",
            ContextWindow = 32768,
            CodingLevel = 5,
            MultiFileLevel = 4,
            StructuredOutput = true,
            ToolUse = true,
            Stability = true,
            Purpose = "agente",
            Capabilities = new List<string> { "completion", "tool-use", "structured-output", "coding" }
        },
        new()
        {
            Name = "qwen3:8b",
            PullName = "qwen3:8b",
            SizeBytes = 5000L * 1024 * 1024,
            WeightGb = 5.0,
            Family = "qwen3",
            ParameterSize = "8B",
            Quantization = "Q4_K_M",
            ContextWindow = 128000,
            CodingLevel = 4,
            MultiFileLevel = 3,
            StructuredOutput = true,
            ToolUse = true,
            Stability = true,
            Purpose = "agente",
            Capabilities = new List<string> { "completion", "tool-use", "structured-output", "coding" }
        }
    };
}
