using System.Collections.Generic;

namespace Condor.Core.Models;

public class ModelCandidate
{
    public string Name { get; set; } = "";
    public string PullName { get; set; } = "";
    public long SizeBytes { get; set; }
    public string? Family { get; set; }
        public string? ParameterSize { get; set; }
        public string? Quantization { get; set; }
        public List<string> Capabilities { get; set; } = new();

        // Perfil de recursos del modelo (viabilidad real, no solo peso).
        public double WeightGb { get; set; }
        public int ContextWindow { get; set; }

        // Capacidad de ingenieria por dominio (consulta|coding|agente|vision).
        public int CodingLevel { get; set; }
        public int MultiFileLevel { get; set; }
        public bool StructuredOutput { get; set; }
        public bool ToolUse { get; set; }
        public bool Stability { get; set; }
        public string? Purpose { get; set; }
    }

public class ModelSelectionResult
{
    public ModelCandidate? Desired { get; set; }
    public bool AlreadyInstalled { get; set; }
    public string? InstalledName { get; set; }
    public string? Reason { get; set; }
    public List<string> Alternatives { get; set; } = new();
    public List<string> Limitations { get; set; } = new();

    /// <summary>Instantanea de recursos usada para la seleccion (informativa).</summary>
    public ResourceSnapshot? Resources { get; set; }

    /// <summary>True si la seleccion quedo bloqueada por recursos (ningun modelo cabe).</summary>
    public bool BlockedByResources { get; set; }
}
