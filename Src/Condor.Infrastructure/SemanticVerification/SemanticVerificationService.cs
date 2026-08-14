using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Condor.Core.Contracts;
using Condor.Core.Models;
using Condor.Core.Semantic;

namespace Condor.Infrastructure.SemanticVerification;

public sealed class SemanticVerificationService : ISemanticVerificationService
{
    private const string ReasonTimeout = "Tiempo excedido al verificar semanticamente.";

    private static readonly string[] RestoreMarkers =
    {
        "NU1301", "error MSB3539", "assets file", ".csproj.assets.json", "NETSDK1004", "restore"
    };

    private readonly IStateStore _stateStore;
    private readonly SemanticVerificationLimits _limits;
    private readonly ProcessRunner _runner;
    private readonly DotNetManifestProbe _manifestProbe;
    private readonly ToolProbe _toolProbe;

    public SemanticVerificationService(
        IStateStore stateStore,
        SemanticVerificationLimits? limits = null)
    {
        _stateStore = stateStore;
        _limits = limits ?? SemanticVerificationLimits.Default;
        _runner = new ProcessRunner();
        _manifestProbe = new DotNetManifestProbe();
        _toolProbe = new ToolProbe();
    }

    public async Task<SemanticVerificationResult> VerifySemanticAsync(
        bool compile,
        bool test,
        CancellationToken cancellationToken = default)
    {
        var timeout = TimeSpan.FromMilliseconds(_limits.ProcessTimeoutMilliseconds);

        try
        {
            var context = await _stateStore
                .LoadContextAsync(cancellationToken)
                .WaitAsync(timeout, cancellationToken);

            var assessment = await _stateStore
                .LoadAssessmentAsync(cancellationToken)
                .WaitAsync(timeout, cancellationToken);

            if (context is null || context.Status == DetectionStatus.NotDetected)
            {
                return NotDetected("No hay contexto de proyecto. Ejecuta 'condor contexto' primero.");
            }

            if (!string.IsNullOrWhiteSpace(context.WorkingDirectory) &&
                !System.IO.Directory.Exists(context.WorkingDirectory))
            {
                return NotDetected("El WorkingDirectory del proyecto no existe.");
            }

            if (!SemanticVerifier.IsDotNetAvailable(assessment))
            {
                return Result(context, Single(SemanticCheck.KindCompile, SemanticCheck.StatusNotAvailable,
                    SemanticVerifier.ReasonDotNetMissing), _limits);
            }

            var manifests = _manifestProbe.Find(context.WorkingDirectory);
            var manifest = SemanticVerifier.ResolveDotNetManifest(manifests);

            if (manifest is null)
            {
                return Result(context, Single(SemanticCheck.KindCompile, SemanticCheck.StatusNotSupported,
                    SemanticVerifier.ReasonNoManifest), _limits);
            }

            var checks = new List<SemanticCheck>();
            var limitsApplied = new List<string>();

            if (compile)
            {
                checks.Add(await RunCheckAsync(context, manifest, SemanticCheck.KindCompile, cancellationToken));
            }

            if (test)
            {
                checks.Add(await RunCheckAsync(context, manifest, SemanticCheck.KindTest, cancellationToken));
            }

            if (checks.Count > _limits.MaxChecks)
            {
                limitsApplied.Add(SemanticVerificationLimits.LimitChecks);
            }

            return Result(context, checks, _limits, limitsApplied);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return NotDetected("La verificacion semantica fue cancelada.");
        }
        catch (TimeoutException)
        {
            return new SemanticVerificationResult
            {
                SchemaVersion = "1.0.0",
                Status = DetectionStatus.Limited,
                Reason = ReasonTimeout,
                LimitsApplied = new List<string> { SemanticVerificationLimits.LimitTimeout },
                GeneratedAtUtc = DateTime.UtcNow
            };
        }
    }

    private async Task<SemanticCheck> RunCheckAsync(
        ProjectContext context,
        string manifest,
        string kind,
        CancellationToken cancellationToken)
    {
        var run = await _runner.RunAsync(
            context.WorkingDirectory,
            manifest,
            kind,
            _limits.ProcessTimeoutMilliseconds,
            cancellationToken);

        if (run.ValidationReason is not null)
        {
            return NewCheck(kind, manifest, SemanticCheck.StatusNotSupported, run.ValidationReason);
        }

        if (run.Cancelled)
        {
            return NewCheck(kind, manifest, SemanticCheck.StatusCancelled, "La verificacion fue cancelada.");
        }

        if (run.TimedOut)
        {
            return NewCheck(kind, manifest, SemanticCheck.StatusTimeout, "La verificacion supero el tiempo maximo.");
        }

        if (run.NotExecutable)
        {
            return NewCheck(kind, manifest, SemanticCheck.StatusNotExecutable, "No fue posible ejecutar la herramienta.");
        }

        if (run.Incomplete)
        {
            return NewCheck(kind, manifest, SemanticCheck.StatusIncomplete, "El proceso termino de forma incompleta.");
        }

        var notRestored = run.ExitCode != 0 && ContainsRestoreMarker(run.Output);
        var status = notRestored
            ? SemanticCheck.StatusNotAvailable
            : SemanticVerifier.Classify(kind, run.ExitCode, false, false, true, false);

        var reason = notRestored ? SemanticVerifier.ReasonNotRestored : null;

        return new SemanticCheck
        {
            Kind = kind,
            Tool = "dotnet",
            Command = "dotnet " + (kind == SemanticCheck.KindTest ? "test" : "build") + " " + manifest + " --no-restore",
            ExitCode = run.ExitCode,
            TimeoutExpired = false,
            Status = status,
            Output = SemanticVerifier.Truncate(run.Output, _limits.MaxOutputLength),
            Reason = reason
        };
    }

    private static SemanticCheck NewCheck(string kind, string manifest, string status, string reason)
    {
        return new SemanticCheck
        {
            Kind = kind,
            Tool = "dotnet",
            Command = "dotnet " + (kind == SemanticCheck.KindTest ? "test" : "build") + " " + manifest + " --no-restore",
            Status = status,
            Reason = reason
        };
    }

    private static bool ContainsRestoreMarker(string output)
    {
        foreach (var marker in RestoreMarkers)
        {
            if (output.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static List<SemanticCheck> Single(string kind, string status, string reason)
    {
        return new List<SemanticCheck> { NewCheck(kind, "", status, reason) };
    }

    private static SemanticVerificationResult Result(
        ProjectContext context,
        List<SemanticCheck> checks,
        SemanticVerificationLimits limits,
        List<string>? limitsApplied = null)
    {
        var anyFailure = checks.Exists(c =>
            c.Status == SemanticCheck.StatusFailed ||
            c.Status == SemanticCheck.StatusTimeout ||
            c.Status == SemanticCheck.StatusNotAvailable ||
            c.Status == SemanticCheck.StatusNotSupported ||
            c.Status == SemanticCheck.StatusNotExecutable ||
            c.Status == SemanticCheck.StatusIncomplete ||
            c.Status == SemanticCheck.StatusCancelled);

        var status = anyFailure ? DetectionStatus.Limited : DetectionStatus.Detected;

        return new SemanticVerificationResult
        {
            SchemaVersion = "1.0.0",
            Status = status,
            RootName = context.RootName,
            WorkingDirectory = context.WorkingDirectory,
            Checks = checks,
            LimitsApplied = limitsApplied ?? new List<string>(),
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    private static SemanticVerificationResult NotDetected(string reason)
    {
        return new SemanticVerificationResult
        {
            SchemaVersion = "1.0.0",
            Status = DetectionStatus.NotDetected,
            Reason = reason,
            GeneratedAtUtc = DateTime.UtcNow
        };
    }
}
