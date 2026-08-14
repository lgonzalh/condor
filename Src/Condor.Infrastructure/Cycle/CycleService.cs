using System;
using System.Collections.Generic;
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
    private readonly IStateStore _stateStore;
    private readonly CycleLimits _limits;

    public CycleService(
        IPlanService planService,
        IBuildService buildService,
        IVerificationService verificationService,
        IStateStore stateStore,
        CycleLimits? limits = null)
    {
        _planService = planService;
        _buildService = buildService;
        _verificationService = verificationService;
        _stateStore = stateStore;
        _limits = limits ?? CycleLimits.Default;
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

                var decision = CycleEngine.EvaluateDecision(
                    plan, build, verification, iteration, _limits);

                if (decision.Complete)
                {
                    return Result(plan, build, verification, cycleId, iteration,
                        CycleStage.Completado, null, limitsApplied);
                }

                if (decision.Stopped)
                {
                    return Result(plan, build, verification, cycleId, iteration,
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

    private static CycleResult Result(
        WorkPlan plan,
        BuildResult build,
        VerificationResult verification,
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
        var id = "C" + StableHash(normalized).ToString("X8");
        return id;
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
}
