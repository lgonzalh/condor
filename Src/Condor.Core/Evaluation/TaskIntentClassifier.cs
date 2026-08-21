namespace Condor.Core.Evaluation;

using System;
using Condor.Core.Models;

/// <summary>
/// Clasificador PURO de intento de tarea -> requisito de modelo. Traduce la
/// intencion del usuario a las capacidades necesarias (codigo, archivos
/// multiples, tool-use, salida estructurada) y a una polaridad de eficiencia.
///
/// Regla de diseno: NO depende de una familia concreta; solo determina QUÉ
/// capacidades requiere la tarea. La seleccion real la hace el selector con
/// base en el presupuesto y los modelos disponibles. Es determinista y sin IO.
/// </summary>
public static class TaskIntentClassifier
{
    /// <summary>
    /// Clasifica una intencion en un requisito de modelo. Por defecto, una
    /// tarea de agente de ingenieria requiere tool-use + salida estructurada y
    /// unos niveles de codigo. La eficiencia (preferir el menor suficiente) es
    /// la polaridad por defecto del harness.
    /// </summary>
    public static TaskModelRequirement Classify(string? intention)
    {
        var text = (intention ?? "").ToLowerInvariant();

        var requiresCoding = ContainsAny(text, CodingSignals);
        var readsMultipleFiles = ContainsAny(text, MultiFileSignals);
        var requiresVision = ContainsAny(text, VisionSignals);

        int codingLevel = 0;
        int multiFile = 0;
        if (requiresCoding) codingLevel = 3;
        if (readsMultipleFiles) { multiFile = 2; codingLevel = Math.Max(codingLevel, 2); }

        if (requiresVision)
        {
            return new TaskModelRequirement
            {
                IntentKind = TaskIntentKinds.Vision,
                RequiredCodingLevel = 0,
                RequiredMultiFileLevel = 0,
                RequiresToolUse = false,
                RequiresStructuredOutput = false,
                PreferSmallestSufficient = false,
                Label = "vision (capacidad multimodal)"
            };
        }

        return new TaskModelRequirement
        {
            IntentKind = TaskIntentKinds.Agent,
            RequiredCodingLevel = codingLevel,
            RequiredMultiFileLevel = multiFile,
            RequiresToolUse = true,
            RequiresStructuredOutput = true,
            PreferSmallestSufficient = true,
            Label = requiresCoding
                ? "agente de ingenieria (coding + herramientas)"
                : "agente de comprension/analisis"
        };
    }

    public static bool IsInformational(string? intention)
    {
        var text = (intention ?? "").ToLowerInvariant();
        return ContainsAny(text, InformationalSignals) &&
               !ContainsAny(text, ModificationSignals);
    }

    private static bool ContainsAny(string text, string[] signals)
    {
        foreach (var s in signals)
        {
            if (text.Contains(s, StringComparison.Ordinal)) return true;
        }
        return false;
    }

    private static readonly string[] CodingSignals =
        { "codi", "compila", "build", "bug", "corrig", "funcion", "programa", "archivo", "archivos", "refactor", "escribe el" };

    private static readonly string[] MultiFileSignals =
        { "proyecto", "repositorio", "repositor", "aplicacion", "estructura", "carpet", "src", "tests", "toda la app", "analiza el" };

    private static readonly string[] VisionSignals =
        { "imagen", "vision", "foto", "captura", "image", "screenshot", "ver esta imagen" };

    private static readonly string[] InformationalSignals =
        { "que hay", "que es", "describe", "cuent", "analiza", "explica", "resumen", "que contiene", "que hace", "revisa", "revisa y" };

    private static readonly string[] ModificationSignals =
        { "corrig", "arregla", "modifica", "cambia", "refactor", "implementa", "anade", "agrega", "crea", "escribe el", "build", "compila", "test" };
}
