using System.Text.Json;
using Condor.Core.Models;

namespace Condor.Core.Agent;

public static class AgentActionParser
{
    private static readonly JsonSerializerOptions _options = new() { PropertyNameCaseInsensitive = true };

    public static AgentAction? Parse(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;
        var json = ExtractJson(content);
        if (json is null) return null;

        try
        {
            return JsonSerializer.Deserialize<AgentAction>(json, _options);
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractJson(string text)
    {
        var start = text.IndexOf('{');
        if (start < 0) return null;

        var depth = 0;
        var inString = false;
        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '"' && (i == 0 || text[i - 1] != '\\')) inString = !inString;
            if (inString) continue;
            if (c == '{') depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0) return text.Substring(start, i - start + 1);
            }
        }

        return start < text.Length ? text.Substring(start) : null;
    }
}
