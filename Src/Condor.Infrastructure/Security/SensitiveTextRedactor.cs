using System.Text.RegularExpressions;

namespace Condor.Infrastructure.Security;

internal static class SensitiveTextRedactor
{
    private static readonly Regex SecretTokenPattern = new(
        @"(?<![A-Za-z0-9])(?:sk-[A-Za-z0-9_-]{8,}|ghp_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}|xox[baprs]-[A-Za-z0-9-]{8,})(?![A-Za-z0-9])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CredentialAssignmentPattern = new(
        @"\b(api[_-]?key|secret|password|token)\s*[:=]\s*[^\s,;]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex BearerPattern = new(
        @"\bBearer\s+[A-Za-z0-9._~+/=-]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static string Redact(string value, string placeholder = "[redactado]")
    {
        var redacted = SecretTokenPattern.Replace(value, placeholder);
        redacted = CredentialAssignmentPattern.Replace(redacted, "$1=" + placeholder);
        return BearerPattern.Replace(redacted, "Bearer " + placeholder);
    }
}
