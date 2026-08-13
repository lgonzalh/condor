using System;
using System.Linq;

namespace Condor.Core.Planning
{
    public static class PlanIntent
    {
        public const string Nueva = "nueva";
        public const string Continuar = "continuar";
        public const string Modificar = "modificar";
        public const string Indefinida = "indefinida";

        private static readonly string[] NuevaTerms =
        {
            "nueva", "nuevo", "crear", "crea", "inicia un nuevo",
            "genera un proyecto", "desde cero", "prototipo"
        };

        private static readonly string[] ContinuarTerms =
        {
            "continuar", "continua", "sigue", "retoma",
            "siguiente tarea", "prosigue", "avanza"
        };

        private static readonly string[] ModificarTerms =
        {
            "modificar", "modifica", "agregar", "agrega", "anade",
            "anadir", "implementar", "implementa", "cambia", "mejora",
            "corrige", "extiende", "refactoriza"
        };

        public static string Classify(string request)
        {
            if (string.IsNullOrWhiteSpace(request))
            {
                return Indefinida;
            }

            var normalized = Normalize(request);

            if (Matches(normalized, NuevaTerms))
            {
                return Nueva;
            }

            if (Matches(normalized, ContinuarTerms))
            {
                return Continuar;
            }

            if (Matches(normalized, ModificarTerms))
            {
                return Modificar;
            }

            return Indefinida;
        }

        private static bool Matches(string normalized, string[] terms)
        {
            foreach (var term in terms)
            {
                if (normalized.Contains(term, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string Normalize(string value)
        {
            return value
                .ToLowerInvariant()
                .Trim()
                .Replace("á", "a")
                .Replace("é", "e")
                .Replace("í", "i")
                .Replace("ó", "o")
                .Replace("ú", "u")
                .Replace("ñ", "n");
        }
    }
}
