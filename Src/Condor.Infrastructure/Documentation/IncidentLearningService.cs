using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Condor.Core.Evaluation;
using Condor.Core.Models;

namespace Condor.Infrastructure.Documentation;

public sealed record IncidentLearningResult(IReadOnlyList<string> ActivatedRuleIds);

/// <summary>
/// Guarda solo fingerprints allowlisted y activa instrucciones cuando el mismo patrón
/// aparece en más de un incidente persistido del proyecto.
/// </summary>
public sealed class IncidentLearningService
{
    private const int MinimumOccurrences = 2;
    private const int MaximumStateBytes = 16 * 1024;
    private const int MaximumOccurrences = 1_000_000;
    private const string StateFileName = "incident-learning.json";
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> ProjectLocks = new(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    public async Task<IncidentLearningResult> RecordAsync(
        string workingDirectory,
        string? reason,
        IEnumerable<string> failedActions,
        CancellationToken cancellationToken = default)
    {
        var patternIds = IncidentPatternCatalog.Classify(reason, failedActions);
        if (patternIds.Count == 0) return new IncidentLearningResult(Array.Empty<string>());

        var paths = ResolvePaths(workingDirectory, createDirectory: true);
        if (paths is null) return new IncidentLearningResult(Array.Empty<string>());

        var statePath = paths.Value.StatePath;
        var gate = ProjectLocks.GetOrAdd(statePath, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var state = await ReadStateAsync(statePath, cancellationToken).ConfigureAwait(false);
            var now = DateTimeOffset.UtcNow;
            var activated = new List<string>();
            foreach (var patternId in patternIds)
            {
                if (IncidentPatternCatalog.Find(patternId) is null) continue;
                if (!state.Entries.TryGetValue(patternId, out var entry))
                {
                    entry = new IncidentLearningEntry { FirstObservedAtUtc = now };
                    state.Entries.Add(patternId, entry);
                }

                var wasActive = entry.Occurrences >= MinimumOccurrences;
                entry.Occurrences = Math.Min(MaximumOccurrences, entry.Occurrences + 1);
                entry.LastObservedAtUtc = now;
                if (!wasActive && entry.Occurrences >= MinimumOccurrences) activated.Add(patternId);
            }

            state.SchemaVersion = 1;
            state.UpdatedAtUtc = now;
            await WriteStateAsync(statePath, state, cancellationToken).ConfigureAwait(false);
            return new IncidentLearningResult(activated);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return new IncidentLearningResult(Array.Empty<string>());
        }
        finally
        {
            gate.Release();
        }
    }

    public static IReadOnlyList<string> LoadActiveInstructions(string workingDirectory)
    {
        var paths = ResolvePaths(workingDirectory, createDirectory: false);
        if (paths is null || !File.Exists(paths.Value.StatePath)) return Array.Empty<string>();

        try
        {
            var statePath = paths.Value.StatePath;
            var info = new FileInfo(statePath);
            if (info.Length > MaximumStateBytes || DocumentationFileSafety.IsReparsePoint(statePath)) return Array.Empty<string>();
            var state = JsonSerializer.Deserialize<IncidentLearningState>(File.ReadAllText(statePath));
            if (state?.Entries is null) return Array.Empty<string>();

            return IncidentPatternCatalog.Definitions
                .Where(definition => state.Entries.TryGetValue(definition.Id, out var entry) && entry.Occurrences >= MinimumOccurrences)
                .Select(definition => definition.Instruction)
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static (string StatePath, string DirectoryPath)? ResolvePaths(string workingDirectory, bool createDirectory)
    {
        try
        {
            var root = Path.GetFullPath(workingDirectory);
            if (!Directory.Exists(root)) return null;
            var stateDirectory = Path.Combine(root, ".condor");
            if (File.Exists(stateDirectory)) return null;
            if (!Directory.Exists(stateDirectory) && createDirectory) Directory.CreateDirectory(stateDirectory);
            if (!Directory.Exists(stateDirectory) || DocumentationFileSafety.IsReparsePoint(stateDirectory)) return null;

            return (Path.Combine(stateDirectory, StateFileName), stateDirectory);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<IncidentLearningState> ReadStateAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(path) || DocumentationFileSafety.IsReparsePoint(path)) return new IncidentLearningState();
            var info = new FileInfo(path);
            if (info.Length > MaximumStateBytes) return new IncidentLearningState();
            var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            var state = JsonSerializer.Deserialize<IncidentLearningState>(json);
            if (state?.Entries is null || state.SchemaVersion != 1) return new IncidentLearningState();

            foreach (var unknownId in state.Entries.Keys.Where(id => IncidentPatternCatalog.Find(id) is null).ToList())
                state.Entries.Remove(unknownId);
            foreach (var entry in state.Entries.Values)
                entry.Occurrences = Math.Clamp(entry.Occurrences, 0, MaximumOccurrences);
            return state;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return new IncidentLearningState();
        }
    }

    private static async Task WriteStateAsync(string path, IncidentLearningState state, CancellationToken cancellationToken)
    {
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            if (Encoding.UTF8.GetByteCount(json) > MaximumStateBytes) return;
            await File.WriteAllTextAsync(temporaryPath, json, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

}
