using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Condor.Core.Contracts;
using Condor.Core.Cycle;
using Condor.Core.Models;

namespace Condor.Infrastructure.Cycle;

public sealed class CycleService : ICycleService
{
    private const string ReasonTimeout = "Tiempo excedido en el ciclo de ingenieria.";

    private readonly IPlanService _planService;
    private readonly IBuildService _buildService;
    private readonly IVerificationService _verificationService;
    private readonly ISemanticVerificationService? _semanticService;
    private readonly IStateStore _stateStore;
    private readonly CycleLimits _limits;

    public CycleService(
        IPlanService planService,
        IBuildService buildService,
        IVerificationService verificationService,
        IStateStore stateStore,
        CycleLimits? limits = null,
        ISemanticVerificationService? semanticService = null)
    {
        _planService = planService;
        _buildService = buildService;
        _verificationService = verificationService;
        _stateStore = stateStore;
        _limits = limits ?? CycleLimits.Default;
        _semanticService = semanticService;
    }

    public async Task<CycleResult> AdvanceAsync(string userRequest, CancellationToken cancellationToken = default)
    {
        var timeout = TimeSpan.FromMilliseconds(_limits.CycleTimeoutMilliseconds);

        try
        {
            var cycleId = BuildCycleId(userRequest);
            var iteration = 1;
            var limitsApplied = new List<string>();
            WorkPlan? plan = null;
            BuildResult? build = null;
            VerificationResult? verification = null;
            SemanticVerificationResult? semantic = null;

            while (true)
            {
                plan = await _planService
                    .BuildPlanAsync(userRequest, cancellationToken)
                    .WaitAsync(timeout, cancellationToken);

                await _stateStore.SavePlanAsync(plan, cancellationToken);

                build = await _buildService
                    .ApplyPlanAsync(cancellationToken)
                    .WaitAsync(timeout, cancellationToken);

                await _stateStore.SaveBuildAsync(build, cancellationToken);

                verification = await _verificationService
                    .VerifyAsync(cancellationToken)
                    .WaitAsync(timeout, cancellationToken);

                await _stateStore.SaveVerificationAsync(verification, cancellationToken);

                semantic = await RunSemanticAsync(cancellationToken);

                var semanticInfo = ClassifySemantic(semantic);

                var decision = CycleEngine.EvaluateDecision(
                    plan, build, verification, iteration, _limits);

                var blocked = semanticInfo.Status == "fallida";

                if (blocked && decision.Complete)
                {
                    // La integridad estaba completa pero la semantica fallo: no puede ser Completado.
                    if (iteration < _limits.MaxIterations)
                    {
                        limitsApplied.Add(CycleLimits.LimitIterations);
                        iteration++;
                        continue;
                    }

                    return Result(plan, build, verification, semanticInfo, cycleId, iteration,
                        CycleStage.Detenido, "La verificacion semantica del proyecto fallo de forma no recuperable.", limitsApplied);
                }

                if (decision.Complete)
                {
                    if (semanticInfo.Status == "no_disponible" || semanticInfo.Status == "incompleta")
                    {
                        return Result(plan, build, verification, semanticInfo, cycleId, iteration,
                            CycleStage.Degradado, semanticInfo.Reason, limitsApplied);
                    }

                    return Result(plan, build, verification, semanticInfo, cycleId, iteration,
                        CycleStage.Completado, null, limitsApplied);
                }

                if (decision.Stopped)
                {
                    return Result(plan, build, verification, semanticInfo, cycleId, iteration,
                        decision.Stage, decision.Reason, limitsApplied);
                }

                if (decision.Regenerate)
                {
                    limitsApplied.Add(CycleLimits.LimitIterations);
                    iteration++;
                }
            }
        }
        catch (TimeoutException)
        {
            return new CycleResult
            {
                SchemaVersion = "1.0.0",
                Status = DetectionStatus.Limited,
                Reason = ReasonTimeout,
                GeneratedAtUtc = DateTime.UtcNow
            };
        }
    }

    private async Task<SemanticVerificationResult?> RunSemanticAsync(CancellationToken cancellationToken)
    {
        if (_semanticService is null)
        {
            return null;
        }

        return await _semanticService
            .VerifySemanticAsync(true, true, cancellationToken)
            .WaitAsync(TimeSpan.FromMilliseconds(_limits.CycleTimeoutMilliseconds), cancellationToken);
    }

    private static SemanticInfo ClassifySemantic(SemanticVerificationResult? semantic)
    {
        if (semantic is null)
        {
            return SemanticInfo.Omitted();
        }

        if (semantic.Checks.Count == 0)
        {
            return SemanticInfo.Omitted();
        }

        var statuses = semantic.Checks.Select(c => c.Status).ToList();

        if (statuses.Contains(SemanticCheck.StatusFailed))
        {
            return SemanticInfo.Failed(BuildSummary(statuses));
        }

        if (statuses.Contains(SemanticCheck.StatusNotAvailable) ||
            statuses.Contains(SemanticCheck.StatusNotSupported) ||
            statuses.Contains(SemanticCheck.StatusNotExecutable))
        {
            return SemanticInfo.NotAvailable(BuildSummary(statuses));
        }

        if (statuses.Contains(SemanticCheck.StatusTimeout) ||
            statuses.Contains(SemanticCheck.StatusIncomplete))
        {
            return SemanticInfo.Incomplete(BuildSummary(statuses));
        }

        return SemanticInfo.Correct(BuildSummary(statuses));
    }

    private static string BuildSummary(List<string> statuses)
    {
        return string.Join(", ", statuses);
    }

    private static CycleResult Result(
        WorkPlan plan,
        BuildResult build,
        VerificationResult verification,
        SemanticInfo semantic,
        string cycleId,
        int iteration,
        CycleStage stage,
        string? reason,
        List<string> limitsApplied)
    {
        var notDetected = plan.Status == DetectionStatus.NotDetected ||
                          build.Status == DetectionStatus.NotDetected;
        var status = notDetected
            ? DetectionStatus.NotDetected
            : stage switch
            {
                CycleStage.Completado => DetectionStatus.Detected,
                _ => DetectionStatus.Limited
            };

        return new CycleResult
        {
            SchemaVersion = "1.0.0",
            Status = status,
            Reason = reason,
            RootName = plan.RootName,
            WorkingDirectory = plan.WorkingDirectory,
            Intention = plan.Intention,
            Objective = plan.Objective,
            Iterations = iteration,
            Stages = 3,
            Applied = build.Applied,
            Verified = verification.Passed,
            SemanticAvailable = semantic.Available,
            SemanticStatus = semantic.Status,
            SemanticSummary = semantic.Summary,
            SemanticReference = semantic.Reference,
            Checkpoint = new CycleCheckpoint
            {
                SchemaVersion = "1.0.0",
                CycleId = cycleId,
                Iteration = iteration,
                Stage = stage,
                StageResult = stage == CycleStage.Completado ? "correcto" : "no_valido",
                StatusCycle = status == DetectionStatus.NotDetected ? "no_detectado"
                    : stage == CycleStage.Completado ? "completado" : "detenido",
                RecoveryState = stage == CycleStage.Completado ? "sin_recuperacion" : (reason ?? "pendiente"),
                NextAction = stage == CycleStage.Completado ? "continuar" : "revisar",
                GeneratedAtUtc = DateTime.UtcNow
            },
            LimitsApplied = limitsApplied.Count > 0
                ? limitsApplied
                : new List<string>(),
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    private static string BuildCycleId(string userRequest)
    {
        var source = string.IsNullOrWhiteSpace(userRequest) ? "ciclo" : userRequest.Trim();
        var normalized = source
            .ToLowerInvariant()
            .Replace(" ", string.Empty)
            .Replace("á", "a").Replace("é", "e").Replace("í", "i")
            .Replace("ó", "o").Replace("ú", "u").Replace("ñ", "n");
        return "C" + StableHash(normalized).ToString("X8");
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = 17;
            foreach (var c in value)
            {
                hash = (hash * 31) + c;
            }

            return hash;
        }
    }

    private readonly record struct SemanticInfo(
        bool Available,
        string? Status,
        string? Summary,
        string? Reference,
        string? Reason)
    {
        public static SemanticInfo Omitted() => new(false, null, null, null, null);

        public static SemanticInfo Correct(string summary) =>
            new(true, "correcta", summary, "verificacion_semantica.json", null);

        public static SemanticInfo NotAvailable(string summary) =>
            new(true, "no_disponible", summary, "verificacion_semantica.json",
                "La verificacion semantica no estuvo disponible para el objetivo.");

        public static SemanticInfo Incomplete(string summary) =>
            new(true, "incompleta", summary, "verificacion_semantica.json",
                "La verificacion semantica quedo incompleta o degradada.");

        public static SemanticInfo Failed(string summary) =>
            new(true, "fallida", summary, "verificacion_semantica.json",
                "La verificacion semantica se ejecuto y produjo resultados negativos.");
    }
}
