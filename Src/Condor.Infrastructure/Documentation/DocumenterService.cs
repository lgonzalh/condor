using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Condor.Core.Contracts;
using Condor.Core.Models;

namespace Condor.Infrastructure.Documentation;

public sealed class DocumenterService : IDocumenter
{
    private const int MaxEntriesPerSection = 250;
    private const int MaxEntryLength = 1000;
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);
    private static readonly Regex SecretTokenPattern = new(
        @"(?<![A-Za-z0-9])(?:sk-[A-Za-z0-9_-]{8,}|ghp_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}|xox[baprs]-[A-Za-z0-9-]{8,})(?![A-Za-z0-9])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CredentialAssignmentPattern = new(
        @"\b(api[_-]?key|secret|password|token)\s*[:=]\s*[^\s,;]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex BearerPattern = new(
        @"\bBearer\s+[A-Za-z0-9._~+/=-]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public async Task<DocumentationResult> WriteAsync(
        DocumentationRequest request,
        CancellationToken cancellationToken = default)
    {
        var generatedAtUtc = DateTime.UtcNow;

        try
        {
            ArgumentNullException.ThrowIfNull(request);

            if (string.IsNullOrWhiteSpace(request.WorkingDirectory))
            {
                return Failure(generatedAtUtc, "No se indico un directorio de trabajo.");
            }

            var root = Path.GetFullPath(request.WorkingDirectory);
            if (!Directory.Exists(root))
            {
                return Failure(generatedAtUtc, "El directorio de trabajo no existe.");
            }

            var reportDirectory = Path.GetFullPath(Path.Combine(root, "docs", "condor", "tareas"));
            var rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (!reportDirectory.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return Failure(generatedAtUtc, "La ruta del informe queda fuera del directorio de trabajo.");
            }

            if (ContainsReparsePoint(root, "docs", "condor", "tareas"))
            {
                return Failure(generatedAtUtc, "La ruta del informe contiene un enlace a otro directorio.");
            }

            Directory.CreateDirectory(reportDirectory);

            if (ContainsReparsePoint(root, "docs", "condor", "tareas"))
            {
                return Failure(generatedAtUtc, "La ruta del informe contiene un enlace a otro directorio.");
            }

            var slug = BuildSlug(request.TaskType);
            var fileName = $"{generatedAtUtc:yyyyMMdd-HHmmssfff}-{slug}-{Guid.NewGuid():N}.md";
            var reportPath = Path.Combine(reportDirectory, fileName);
            var content = BuildReport(request, generatedAtUtc);

            await using (var stream = new FileStream(
                reportPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true))
            await using (var writer = new StreamWriter(stream, Utf8WithoutBom))
            {
                await writer.WriteAsync(content.AsMemory(), cancellationToken).ConfigureAwait(false);
            }

            return new DocumentationResult
            {
                Success = true,
                RelativePath = Path.GetRelativePath(root, reportPath).Replace('\\', '/'),
                GeneratedAtUtc = generatedAtUtc
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Failure(generatedAtUtc, $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    private static string BuildReport(DocumentationRequest request, DateTime generatedAtUtc)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Informe de tarea de Condor");
        builder.AppendLine();
        builder.AppendLine($"- Tipo: {Escape(request.TaskType)}");
        builder.AppendLine($"- Resultado: {Escape(request.Outcome)}");
        builder.AppendLine($"- Generado UTC: {generatedAtUtc:O}");
        builder.AppendLine();

        AppendSection(builder, "Antes", request.Before);
        AppendSection(builder, "Durante", request.During);
        AppendSection(builder, "Despues", request.After);

        if (!string.IsNullOrWhiteSpace(request.Reason))
        {
            builder.AppendLine("## Motivo o limitacion");
            builder.AppendLine();
            AppendEntries(builder, new[] { request.Reason });
        }

        return builder.ToString();
    }

    private static void AppendSection(StringBuilder builder, string heading, System.Collections.Generic.IReadOnlyCollection<string> entries)
    {
        builder.AppendLine($"## {heading}");
        builder.AppendLine();

        if (entries.Count == 0)
        {
            builder.AppendLine("- Sin datos registrados.");
            builder.AppendLine();
            return;
        }

        AppendEntries(builder, entries);
    }

    private static void AppendEntries(StringBuilder builder, System.Collections.Generic.IEnumerable<string> entries)
    {
        foreach (var entry in entries.Take(MaxEntriesPerSection))
        {
            var normalized = (entry ?? "").Replace("\r\n", "\n").Replace('\r', '\n');
            foreach (var line in normalized.Split('\n'))
            {
                var bounded = line.Length > MaxEntryLength ? line[..MaxEntryLength] + "..." : line;
                builder.Append("- ").AppendLine(Escape(bounded));
            }
        }

        builder.AppendLine();
    }

    private static string Escape(string value)
    {
        var redacted = SecretTokenPattern.Replace(value, "[REDACTADO]");
        redacted = CredentialAssignmentPattern.Replace(redacted, "$1=[REDACTADO]");
        redacted = BearerPattern.Replace(redacted, "Bearer [REDACTADO]");

        return redacted.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("`", "\\`", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }

    private static string BuildSlug(string? value)
    {
        var slug = new string((value ?? "").ToLowerInvariant()
            .Select(character => char.IsAsciiLetterOrDigit(character) ? character : '-')
            .Take(48)
            .ToArray())
            .Trim('-');

        return string.IsNullOrWhiteSpace(slug) ? "tarea" : slug;
    }

    private static bool ContainsReparsePoint(string root, params string[] segments)
    {
        var current = root;
        foreach (var segment in segments)
        {
            current = Path.Combine(current, segment);
            if (Directory.Exists(current) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                return true;
            }
        }

        return false;
    }

    private static DocumentationResult Failure(DateTime generatedAtUtc, string reason)
    {
        return new DocumentationResult
        {
            Success = false,
            Reason = reason,
            GeneratedAtUtc = generatedAtUtc
        };
    }
}
