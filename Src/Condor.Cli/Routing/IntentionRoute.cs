namespace Condor.Cli.Routing;

public enum SlashCommandKind
{
    Analizar,
    Contexto,
    Planear,
    Construir,
    Verificar,
    Examinar,
    Recomendar,
    Ayuda,
    Version,
    Consultar,
    VerificarSemantico,
    Preparar,
    Avanzar
}

public abstract record IntentionRoute;

public sealed record SlashRoute(SlashCommandKind Kind, string[] Arguments) : IntentionRoute;

public sealed record FreeIntentionRoute(string Intention) : IntentionRoute;

public static class IntentionRouter
{
    private static readonly Dictionary<string, SlashCommandKind> Commands =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["/analizar"] = SlashCommandKind.Analizar,
            ["/contexto"] = SlashCommandKind.Contexto,
            ["/planear"] = SlashCommandKind.Planear,
            ["/construir"] = SlashCommandKind.Construir,
            ["/verificar"] = SlashCommandKind.Verificar,
            ["/examinar"] = SlashCommandKind.Examinar,
            ["/recomendar"] = SlashCommandKind.Recomendar,
            ["/ayuda"] = SlashCommandKind.Ayuda,
            ["/help"] = SlashCommandKind.Ayuda,
            ["/version"] = SlashCommandKind.Version,
            ["/v"] = SlashCommandKind.Version,
            ["/consultar"] = SlashCommandKind.Consultar,
            ["/verificar-semantico"] = SlashCommandKind.VerificarSemantico,
            ["/preparar"] = SlashCommandKind.Preparar,
            ["/avanzar"] = SlashCommandKind.Avanzar
        };

    public static bool IsSlashCommand(string? input)
    {
        return !string.IsNullOrWhiteSpace(input) && input[0] == '/';
    }

    public static IntentionRoute Route(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new FreeIntentionRoute("");
        }

        var trimmed = input.TrimStart();
        if (!isCommandToken(trimmed))
        {
            return new FreeIntentionRoute(trimmed);
        }

        var firstToken = NextToken(trimmed);
        var token = firstToken.TrimEnd();

        if (Commands.TryGetValue(token, out var kind))
        {
            var rest = trimmed.Substring(firstToken.Length).Trim();
            return new SlashRoute(kind, string.IsNullOrWhiteSpace(rest) ? Array.Empty<string>() : SplitArguments(rest));
        }

        // Un reconocimiento que no inicia con "/" no es un slash; es intencion libre.
        return new FreeIntentionRoute(trimmed);
    }

    private static bool isCommandToken(string input)
    {
        return input.StartsWith('/') &&
               (ContainsLetter(input, 1) || (input.Length > 1 && input[0] == '/'));
    }

    private static bool ContainsLetter(string input, int start)
    {
        for (var i = start; i < input.Length; i++)
        {
            if (char.IsLetter(input[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static string NextToken(string input)
    {
        var i = 0;
        while (i < input.Length && !char.IsWhiteSpace(input[i]))
        {
            i++;
        }

        return input.Substring(0, i);
    }

    private static string[] SplitArguments(string input)
    {
        // Divide en piezas respetando comillas simples o dobles simples para frases.
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        char? quote = null;

        foreach (var c in input)
        {
            if (quote is not null)
            {
                if (c == quote)
                {
                    quote = null;
                }
                else
                {
                    current.Append(c);
                }

                continue;
            }

            if (c == '"' || c == '\'')
            {
                quote = c;
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                if (current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }

                continue;
            }

            current.Append(c);
        }

        if (current.Length > 0)
        {
            result.Add(current.ToString());
        }

        return result.ToArray();
    }
}
