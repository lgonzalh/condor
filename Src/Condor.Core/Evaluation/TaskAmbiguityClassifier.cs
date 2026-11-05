using System.Text.RegularExpressions;

namespace Condor.Core.Evaluation;

/// <summary>Recognizes only bare, clearly underspecified commands; context-rich tasks pass through.</summary>
public static class TaskAmbiguityClassifier
{
    private static readonly Regex BareVagueCommand = new(
        @"^(?:por favor\s+)?(?:(?:arregla|arreglalo|arréglalo|corrige|corrigelo|corrígelo|mejora|mejoralo|mejóralo|cambia|modifica|actualiza)(?:\s+(?:esto|eso))?|fix(?:\s+(?:it|this|that))?|improve(?:\s+(?:it|this|that))?|change\s+(?:it|this|that)|hazlo|do it)(?:\s+por favor)?[.!?\s]*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string? FindQuestion(string? intention)
    {
        if (string.IsNullOrWhiteSpace(intention))
            return "¿Qué resultado esperas y qué parte del proyecto debo cambiar?";

        return BareVagueCommand.IsMatch(intention.Trim())
            ? "¿Qué parte del proyecto quieres que cambie y cuál es el resultado esperado?"
            : null;
    }
}
