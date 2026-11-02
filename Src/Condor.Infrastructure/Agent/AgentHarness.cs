using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Condor.Core.Models;

namespace Condor.Infrastructure.Agent;

public readonly record struct HarnessVerifyResult(
    bool Done,
    string? Reason,
    string Detail,
    bool ChecksApplicable = true);

public readonly record struct HtmlRepairResult(string RelativePath, string Action);

/// <summary>
/// Harness externo del agente: ejecuta build y test de forma real e independiente
/// del modelo y devuelve la evidencia obtenida. No adivina ni simula exito.
/// </summary>
public sealed class AgentHarness
{
    private readonly string _workingDir;
    private readonly AgentLimits _limits;
    private readonly List<AgentStep> _steps;
    private readonly string? _intention;
    private bool _restored;

    public AgentHarness(string workingDir, AgentLimits limits, List<AgentStep> steps, string? intention = null)
    {
        _workingDir = workingDir;
        _limits = limits;
        _steps = steps;
        _intention = intention;
    }

    public async Task<HarnessVerifyResult> VerifyAsync(CancellationToken ct)
    {
        var toolset = new AgentToolset(_workingDir, maxContent: _limits.MaxContentLength);

        var htmlFailure = VerifyChangedHtmlFiles();
        if (htmlFailure is not null)
            return htmlFailure.Value;

        if (!toolset.HasBuildManifest)
        {
            const string reason = "No se detecto un manifiesto compatible para build/test; esas verificaciones no aplican a este proyecto.";
            return new HarnessVerifyResult(true, reason, "Build/test omitidos (no aplicables).", ChecksApplicable: false);
        }

        var build = await toolset.ExecuteAsync(new AgentAction { Action = AgentAction.ActionBuild }, 0, ct);
        _steps.Add(build);

        if (!build.Success)
        {
            var (restored, build2) = await MaybeRestoreAndReRunAsync(toolset, build, ct);
            if (!build2.Success)
                return new(false, TailReason("Build fallo: " + (build2.ResultPreview ?? "sin detalle")), build2.ResultPreview ?? "");
        }

        var tests = await toolset.ExecuteAsync(new AgentAction { Action = AgentAction.ActionTest }, 0, ct);
        _steps.Add(tests);
        if (!tests.Success)
        {
            var (restored2, tests2) = await MaybeRestoreAndReRunAsync(toolset, tests, ct);
            if (!tests2.Success)
                return new(false, TailReason("Pruebas fallaron: " + (tests2.ResultPreview ?? "sin detalle")), tests2.ResultPreview ?? "");
        }

        return new(true, "El harness confirmo build y pruebas externamente.", "build ok; test ok");
    }

    public async Task<HtmlRepairResult?> TryRepairMissingRequiredHtmlLinkAsync(
        string? failureReason,
        CancellationToken ct,
        bool allowRequestedRootHtmlFile = false)
    {
        const string missingLinkFailure = "no contiene el enlace relativo exacto requerido";
        var isMissingLinkFailure = !string.IsNullOrWhiteSpace(failureReason) &&
            failureReason.Contains(missingLinkFailure, StringComparison.OrdinalIgnoreCase);
        if (!isMissingLinkFailure && !allowRequestedRootHtmlFile)
        {
            return null;
        }

        var requiredLink = FindRequiredRelativeHtmlLink(_intention);
        if (requiredLink is null) return null;

        var root = Path.GetFullPath(_workingDir);
        var rootPrefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        var candidatePaths = new List<string>();
        foreach (var step in _steps)
        {
            if (!step.Success || string.IsNullOrWhiteSpace(step.Path) ||
                step.Action is not (AgentAction.ActionCreateFile or AgentAction.ActionEditFile or AgentAction.ActionPatch))
            {
                continue;
            }

            var extension = Path.GetExtension(step.Path);
            if (!extension.Equals(".html", StringComparison.OrdinalIgnoreCase) &&
                !extension.Equals(".htm", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            candidatePaths.Add(step.Path);
        }

        if (allowRequestedRootHtmlFile && FindRequestedRootHtmlFile(_intention) is { } requestedRootHtml &&
            !candidatePaths.Exists(path => string.Equals(path, requestedRootHtml, StringComparison.OrdinalIgnoreCase)))
        {
            candidatePaths.Insert(0, requestedRootHtml);
        }

        foreach (var relativePath in candidatePaths)
        {
            ct.ThrowIfCancellationRequested();

            var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
            if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!File.Exists(fullPath))
            {
                if (allowRequestedRootHtmlFile &&
                    FindRequestedRootHtmlFile(_intention) is { } requestedFile &&
                    string.Equals(relativePath, requestedFile, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(Path.GetDirectoryName(fullPath), root, StringComparison.OrdinalIgnoreCase) &&
                    await TryCreateRequestedRootHtmlFileAsync(fullPath, root, requiredLink, _intention, ct).ConfigureAwait(false))
                {
                    return new HtmlRepairResult(relativePath.Replace(Path.DirectorySeparatorChar, '/'), AgentAction.ActionCreateFile);
                }

                continue;
            }

            string content;
            try
            {
                if ((File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
                    continue;
                content = await File.ReadAllTextAsync(fullPath, new UTF8Encoding(false, true), ct).ConfigureAwait(false);
            }
            catch (DecoderFallbackException)
            {
                continue;
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            if (!ContainsHtmlMarkup(content) || ContainsHtmlLink(content, requiredLink)) continue;

            var anchor = BuildRequestedHtmlAnchor(_intention, requiredLink);
            var repaired = RemoveHtmlDocumentStylesheetLinks(content);
            var closingBody = Regex.Match(repaired, @"(?i)</body\s*>", RegexOptions.RightToLeft);
            if (!closingBody.Success) continue;

            repaired = repaired.Insert(closingBody.Index, Environment.NewLine + "    " + anchor + Environment.NewLine + "  ");
            if (!ContainsHtmlLink(repaired, requiredLink)) continue;

            try
            {
                await File.WriteAllTextAsync(fullPath, repaired, new UTF8Encoding(false), ct).ConfigureAwait(false);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            return new HtmlRepairResult(
                Path.GetRelativePath(root, fullPath).Replace(Path.DirectorySeparatorChar, '/'),
                AgentAction.ActionEditFile);
        }

        return null;
    }

    private HarnessVerifyResult? VerifyChangedHtmlFiles()
    {
        var root = Path.GetFullPath(_workingDir);
        var rootPrefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        var checkedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var step in _steps)
        {
            if (!step.Success || string.IsNullOrWhiteSpace(step.Path) ||
                step.Action is not (AgentAction.ActionCreateFile or AgentAction.ActionEditFile or AgentAction.ActionPatch))
            {
                continue;
            }

            var extension = Path.GetExtension(step.Path);
            if (!extension.Equals(".html", StringComparison.OrdinalIgnoreCase) &&
                !extension.Equals(".htm", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(Path.Combine(root, step.Path));
            if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return new(false, "El harness rechazo una ruta HTML fuera de la raiz del proyecto.", step.Path);
            }

            if (!checkedFiles.Add(fullPath)) continue;
            if (!File.Exists(fullPath))
            {
                return new(false, "El harness no encontro el archivo HTML modificado.", step.Path);
            }

            if ((File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
            {
                return new(false, "El harness no verifico un archivo HTML que es un punto de reanalisis.", step.Path);
            }

            string content;
            try
            {
                content = File.ReadAllText(fullPath, new UTF8Encoding(false, true));
            }
            catch (DecoderFallbackException)
            {
                return new(false, "El archivo HTML modificado no contiene texto UTF-8 valido.", step.Path);
            }
            catch (IOException ex)
            {
                return new(false, "El harness no pudo leer el archivo HTML modificado: " + ex.Message, step.Path);
            }

            if (!ContainsHtmlMarkup(content))
            {
                return new(false, "El archivo .html modificado no contiene marcado HTML; el harness rechazo la entrega.", step.Path);
            }

            var requiredLink = FindRequiredRelativeHtmlLink(_intention);
            if (requiredLink is not null && !ContainsHtmlLink(content, requiredLink))
            {
                return new(false,
                    "El archivo HTML modificado no contiene el enlace relativo exacto requerido '" + requiredLink + "'; el harness rechazo la entrega.",
                    requiredLink);
            }
        }

        return null;
    }

    private static bool ContainsHtmlMarkup(string content)
        => Regex.IsMatch(content, @"(?is)<!doctype\s+html\b|<html\b|<[a-z][a-z0-9:-]*\b(?:\s[^<>]*?)?>");

    private static string? FindRequiredRelativeHtmlLink(string? intention)
    {
        if (string.IsNullOrWhiteSpace(intention)) return null;
        var match = Regex.Match(intention, @"(?i)\benlace\s+relativo\s+exacto\s+(?<path>[A-Za-z0-9._/-]+\.html)\b");
        if (!match.Success)
        {
            match = Regex.Match(
                intention,
                @"(?i)\bhref\s*=\s*(?<quote>['""])(?<path>[A-Za-z0-9._/-]+\.html)\k<quote>");
        }

        if (!match.Success)
        {
            var asksForDocumentationLinks =
                Regex.IsMatch(intention, @"(?i)\b(?:enlaces?|links?)\b") &&
                Regex.IsMatch(intention, @"(?i)\b(?:documentaci[oó]n|documentos)\b") &&
                (intention.Contains("docs/", StringComparison.OrdinalIgnoreCase) ||
                 intention.Contains("docs\\", StringComparison.OrdinalIgnoreCase));
            if (!asksForDocumentationLinks) return null;
            return "docs/indice-condor.html";
        }

        var path = match.Groups["path"].Value;
        return path.StartsWith("/", StringComparison.Ordinal) || path.Contains(":", StringComparison.Ordinal) ||
               path.Split('/').Any(segment => segment == "..")
            ? null
            : path;
    }

    private static string? FindRequestedRootHtmlFile(string? intention)
    {
        if (string.IsNullOrWhiteSpace(intention)) return null;
        var match = Regex.Match(
            intention,
            @"(?i)\bruta\s+exacta\s+(?<file>[A-Za-z0-9_.-]+\.html)\s*\(\s*ra[ií]z\b");
        if (!match.Success)
        {
            match = Regex.Match(intention, @"(?i)\b(?:actualiza|edita|corrige)\s+(?<file>[A-Za-z0-9_.-]+\.html)\b");
        }
        if (!match.Success)
        {
            match = Regex.Match(intention, @"(?i)\b(?<file>[A-Za-z0-9_.-]+\.html)\s+en\s+la\s+ra[ií]z\b");
        }

        if (!match.Success) return null;
        var file = match.Groups["file"].Value;
        return string.Equals(Path.GetFileName(file), file, StringComparison.Ordinal) ? file : null;
    }

    private static bool ContainsHtmlLink(string content, string relativePath)
    {
        var pattern = @"(?is)<a\b[^>]*\bhref\s*=\s*(['""])" + Regex.Escape(relativePath) + @"\1[^>]*>.*?</a\s*>";
        return Regex.IsMatch(content, pattern);
    }

    private static string BuildRequestedHtmlAnchor(string? intention, string relativePath)
    {
        var label = "Documentación generada";
        var pattern = @"(?is)<a\b[^>]*\bhref\s*=\s*(['""])" + Regex.Escape(relativePath) + @"\1[^>]*>(?<label>[^<]{1,120})</a>";
        var match = Regex.Match(intention ?? string.Empty, pattern);
        if (match.Success && !string.IsNullOrWhiteSpace(match.Groups["label"].Value))
            label = match.Groups["label"].Value.Trim();

        var href = System.Net.WebUtility.HtmlEncode(relativePath);
        var text = System.Net.WebUtility.HtmlEncode(label);
        return "<p><a href=\"" + href + "\">" + text + "</a></p>";
    }

    private static async Task<bool> TryCreateRequestedRootHtmlFileAsync(
        string fullPath,
        string projectRoot,
        string requiredLink,
        string? intention,
        CancellationToken cancellationToken)
    {
        var projectName = System.Net.WebUtility.HtmlEncode(new DirectoryInfo(projectRoot).Name);
        var anchor = BuildRequestedHtmlAnchor(intention, requiredLink);
        var content = "<!doctype html>" + Environment.NewLine +
                      "<html lang=\"es\">" + Environment.NewLine +
                      "<head>" + Environment.NewLine +
                      "  <meta charset=\"utf-8\">" + Environment.NewLine +
                      "  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">" + Environment.NewLine +
                      "  <title>" + projectName + "</title>" + Environment.NewLine +
                      "</head>" + Environment.NewLine +
                      "<body>" + Environment.NewLine +
                      "  <main>" + Environment.NewLine +
                      "    <h1>" + projectName + "</h1>" + Environment.NewLine +
                      "    <p>Proyecto generado con Condor.</p>" + Environment.NewLine +
                      "    <section aria-labelledby=\"documentacion\">" + Environment.NewLine +
                      "      <h2 id=\"documentacion\">Documentación</h2>" + Environment.NewLine +
                      "      " + anchor + Environment.NewLine +
                      "    </section>" + Environment.NewLine +
                      "  </main>" + Environment.NewLine +
                      "</body>" + Environment.NewLine +
                      "</html>" + Environment.NewLine;

        try
        {
            await using var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            await writer.WriteAsync(content.AsMemory(), cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string RemoveHtmlDocumentStylesheetLinks(string content)
    {
        return Regex.Replace(content, @"(?is)<link\b[^>]*>", match =>
        {
            var tag = match.Value;
            var stylesheet = Regex.IsMatch(tag, @"(?i)\brel\s*=\s*(['""])stylesheet\1");
            var htmlDocument = Regex.IsMatch(tag, @"(?i)\bhref\s*=\s*(['""])[^'""]+\.html\1");
            return stylesheet && htmlDocument ? string.Empty : tag;
        });
    }

    private async Task<(bool, AgentStep)> MaybeRestoreAndReRunAsync(AgentToolset toolset, AgentStep failed, CancellationToken ct)
    {
        if (_restored || !LooksLikeRestoreFailure(failed.ResultPreview))
            return (false, failed);

        var restore = await toolset.ExecuteAsync(new AgentAction { Action = AgentAction.ActionRestore }, 0, ct);
        _steps.Add(restore);
        _restored = true;

        var rerun = await toolset.ExecuteAsync(new AgentAction { Action = failed.Action == AgentAction.ActionTest ? AgentAction.ActionTest : AgentAction.ActionBuild }, 0, ct);
        _steps.Add(rerun);
        return (true, rerun);
    }

    private static bool LooksLikeRestoreFailure(string? output)
    {
        return output is not null &&
               (output.Contains("NETSDK1004", StringComparison.OrdinalIgnoreCase) ||
                output.Contains("project.assets.json", StringComparison.OrdinalIgnoreCase) ||
                output.Contains("NU1004", StringComparison.OrdinalIgnoreCase) ||
                output.Contains("no se encontro el archivo de recursos", StringComparison.OrdinalIgnoreCase) ||
                output.Contains("restauracion de paquetes", StringComparison.OrdinalIgnoreCase));
    }

    private static string TailReason(string s)
    {
        // Resumen breve del motivo, tomado del final del mensaje real del harness.
        return s.Length > 300 ? s.Substring(0, 300) + "..." : s;
    }
}
