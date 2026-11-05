namespace Condor.Core.Evaluation;

public sealed record IncidentPatternDefinition(string Id, string Instruction);

/// <summary>Patrones y reglas seguras aprobadas para el aprendizaje local de incidentes.</summary>
public static class IncidentPatternCatalog
{
    public static IReadOnlyList<IncidentPatternDefinition> Definitions { get; } = new[]
    {
        new IncidentPatternDefinition(
            "test_failure",
            "Cuando fallen pruebas, conserva su salida, identifica el defecto concreto y deja una prueba de regresión permanente si se corrigió un error. Reejecuta la prueba afectada antes de declarar la tarea terminada."),
        new IncidentPatternDefinition(
            "build_failure",
            "Cuando falle la compilación, usa el primer diagnóstico como evidencia, corrige la causa y vuelve a compilar. No repitas una compilación sin un cambio o diagnóstico nuevo."),
        new IncidentPatternDefinition(
            "provider_timeout",
            "Ante un timeout del proveedor, no repitas la misma solicitud sin cambios. Conserva el punto de continuidad, reduce o acota el contexto cuando sea posible y comunica el límite sin afirmar éxito."),
        new IncidentPatternDefinition(
            "scope_violation",
            "Si el harness rechaza una escritura fuera del alcance, no intentes la misma ruta otra vez. Actualiza el plan y pide autorización para ampliar el alcance antes de escribir.")
    };

    public static IReadOnlyList<string> Classify(string? reason, IEnumerable<string> failedActions)
    {
        var patterns = new HashSet<string>(StringComparer.Ordinal);
        foreach (var action in failedActions)
        {
            if (action.Equals("test", StringComparison.OrdinalIgnoreCase)) patterns.Add("test_failure");
            if (action.Equals("build", StringComparison.OrdinalIgnoreCase)) patterns.Add("build_failure");
        }

        if (ContainsAny(reason, "timeout", "timed out", "tiempo de espera", "tiempo maximo de espera"))
            patterns.Add("provider_timeout");
        if (ContainsAny(reason, "fuera del alcance", "fuera de alcance", "outside the declared scope", "scope violation"))
            patterns.Add("scope_violation");

        return Definitions
            .Where(definition => patterns.Contains(definition.Id))
            .Select(definition => definition.Id)
            .ToList();
    }

    public static IncidentPatternDefinition? Find(string id) =>
        Definitions.FirstOrDefault(definition => string.Equals(definition.Id, id, StringComparison.Ordinal));

    private static bool ContainsAny(string? value, params string[] candidates) =>
        !string.IsNullOrWhiteSpace(value) && candidates.Any(candidate => value.Contains(candidate, StringComparison.OrdinalIgnoreCase));
}
