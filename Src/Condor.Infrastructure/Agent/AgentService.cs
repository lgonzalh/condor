using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Condor.Core.Agent;
using Condor.Core.Contracts;
using Condor.Core.Evaluation;
using Condor.Core.Models;
using Condor.Infrastructure.Detection;
using Condor.Infrastructure.Llm;
using Condor.Infrastructure.Setup;

namespace Condor.Infrastructure.Agent;

public sealed class AgentService : IAgentService
{
    private const string OutputTokenBudgetExhausted = "__condor_output_token_budget_exhausted__";
    private readonly IStateStore _stateStore;
    private readonly IAssessmentService? _assessmentService;
    private readonly ILlmClient _llm;
    private readonly ILlmProviderDiagnostics _provider;
    private readonly AgentLimits _limits;
    private readonly LocalModelSession? _session;
    private readonly Func<string> _workingDirectoryProvider;
    private readonly ModelAutoSetupService? _modelAutoSetup;
    private readonly IAgentSteeringChannel? _steeringChannel;
    private readonly IAgentDelegationService? _delegationService;
    private int _remainingOutputTokens;

    public AgentService(
        IStateStore stateStore,
        IAssessmentService? assessmentService = null,
        AgentLimits? limits = null,
        ILlmClient? llm = null,
        ILlmProviderDiagnostics? provider = null,
        LocalModelSession? session = null,
        Func<string>? workingDirectoryProvider = null,
        ModelAutoSetupService? modelAutoSetup = null,
        IAgentSteeringChannel? steeringChannel = null,
        IAgentDelegationService? delegationService = null,
        bool allowDelegation = true,
        int? maxOutputTokens = null)
    {
        _stateStore = stateStore;
        _assessmentService = assessmentService;
        _session = session;
        _workingDirectoryProvider = workingDirectoryProvider ?? (() => Environment.CurrentDirectory);
        _modelAutoSetup = modelAutoSetup;
        _steeringChannel = steeringChannel;
        _delegationService = allowDelegation
            ? delegationService ?? new AgentDelegationService(RunSubagentAsync)
            : null;
        _remainingOutputTokens = maxOutputTokens is { } outputBudget ? Math.Max(0, outputBudget) : -1;
        if (session is not null)
        {
            // Sesion compartida: un unico proveedor/HttpClient para toda la ejecucion.
            _llm = llm ?? session.Llm;
            _provider = provider ?? session.Diagnostics;
        }
        else
        {
            _llm = llm ?? new OllamaClient();
            _provider = provider ?? (_llm as ILlmProviderDiagnostics) ?? new OllamaClient();
        }
        _limits = limits ?? AgentLimits.Default;
    }

    /// <summary>
    /// Crea el auto-setup de modelos reutilizando el HttpClient de la sesion
    /// compartida cuando existe, para no duplicar conectores por tarea.
    /// </summary>
    private ModelAutoSetupService CreateModelSetup()
        => _modelAutoSetup ?? new(_stateStore, _assessmentService, httpClient: _session?.SharedHttpClient);

    private async Task<AgentResult> RunSubagentAsync(
        string workingDirectory,
        AgentSubtask subtask,
        IAgentProgressObserver? progress,
        CancellationToken cancellationToken)
    {
        var childLimits = new AgentLimits
        {
            MaxIterations = subtask.MaxIterations,
            TimeoutMilliseconds = checked(subtask.MaxDurationSeconds * 1000),
            MaxTokensPerRequest = _limits.MaxTokensPerRequest,
            MaxModifications = _limits.MaxModifications,
            MaxRepeatedAction = _limits.MaxRepeatedAction,
            MaxRepeatedFailedAction = _limits.MaxRepeatedFailedAction,
            MaxPlanBIterations = _limits.MaxPlanBIterations,
            MaxRedundantObservations = _limits.MaxRedundantObservations,
            MaxInvalidOutputs = _limits.MaxInvalidOutputs,
            MaxContentLength = _limits.MaxContentLength
        };
        var childService = new AgentService(
            _stateStore,
            _assessmentService,
            childLimits,
            llm: _llm,
            provider: _provider,
            workingDirectoryProvider: () => workingDirectory,
            allowDelegation: false,
            maxOutputTokens: subtask.MaxOutputTokens);
        return await childService.RunAsync(subtask.Task, progress, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ApprovalResult> RequestCriticalWriteApprovalAsync(
        AgentAction action,
        int iteration,
        string workingDirectory,
        AgentCheckpoint checkpoint,
        List<AgentStep> steps,
        IAgentProgressObserver? progress,
        CancellationToken cancellationToken)
    {
        var commitHash = await ReadCurrentGitCommitAsync(workingDirectory, cancellationToken).ConfigureAwait(false);
        var digest = ComputeApprovalDigest(action, commitHash);
        var confirmationCode = digest.Substring(0, 8).ToUpperInvariant();
        var description = action.Action + (string.IsNullOrWhiteSpace(action.Path) ? "" : " " + action.Path);
        var versionDescription = commitHash is null
            ? "el contenido exacto de la acción (sin HEAD Git verificable)"
            : "HEAD " + commitHash[..12];
        var question = "Riesgo crítico: Condor se detuvo antes de ejecutar " + description + ", vinculada a " + versionDescription + ". " +
                       "Para aprobar esta acción exacta responde APROBAR " + confirmationCode + "; para rechazarla responde RECHAZAR " + confirmationCode + ".";

        checkpoint.NextAction = "esperar_aprobacion";
        checkpoint.LastDecision = "esperando_aprobacion_humana";
        checkpoint.PendingApprovalDigest = digest;
        checkpoint.PendingApprovalDescription = description;
        checkpoint.LastResult = question;
        if (!await _stateStore.SaveAgentCheckpointAsync(workingDirectory, checkpoint, cancellationToken).ConfigureAwait(false))
        {
            return new ApprovalResult(ApprovalDecision.Unavailable, digest,
                "Condor no pudo guardar la solicitud de aprobación; la acción crítica quedó bloqueada.");
        }

        progress?.Report(AgentProgress.Of(
            AgentPhase.Understanding,
            action: "aprobación humana",
            path: action.Path,
            iteration: iteration,
            message: question,
            flag: ProgressFlag.Recovering));

        if (_steeringChannel is null)
            return new ApprovalResult(ApprovalDecision.WaitingForUser, digest, question);

        while (true)
        {
            var answer = await _steeringChannel.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            if (string.Equals(answer.Trim(), "APROBAR " + confirmationCode, StringComparison.OrdinalIgnoreCase))
            {
                checkpoint.PendingApprovalDigest = null;
                checkpoint.PendingApprovalDescription = null;
                checkpoint.NextAction = "ejecutar_accion_aprobada";
                checkpoint.LastDecision = "aprobacion_humana_otorgada";
                checkpoint.LastResult = "Acción crítica aprobada por el usuario: " + description;
                if (!await _stateStore.SaveAgentCheckpointAsync(workingDirectory, checkpoint, cancellationToken).ConfigureAwait(false))
                {
                    return new ApprovalResult(ApprovalDecision.Unavailable, digest,
                        "Condor no pudo guardar la aprobación; la acción crítica quedó bloqueada.");
                }

                steps.Add(new AgentStep
                {
                    Iteration = iteration,
                    Action = "human_approval",
                    Path = action.Path,
                    Success = true,
                    ResultPreview = "El usuario aprobó la acción crítica identificada por " + confirmationCode + ".",
                    AtUtc = DateTime.UtcNow
                });
                return new ApprovalResult(ApprovalDecision.Approved, digest, null);
            }

            if (string.Equals(answer.Trim(), "RECHAZAR " + confirmationCode, StringComparison.OrdinalIgnoreCase))
            {
                checkpoint.PendingApprovalDigest = null;
                checkpoint.PendingApprovalDescription = null;
                checkpoint.NextAction = "detener";
                checkpoint.LastDecision = "aprobacion_humana_rechazada";
                checkpoint.LastResult = "El usuario rechazó la acción crítica: " + description;
                await _stateStore.SaveAgentCheckpointAsync(workingDirectory, checkpoint, cancellationToken).ConfigureAwait(false);
                steps.Add(new AgentStep
                {
                    Iteration = iteration,
                    Action = "human_approval",
                    Path = action.Path,
                    Success = false,
                    ResultPreview = "El usuario rechazó la acción crítica.",
                    AtUtc = DateTime.UtcNow
                });
                return new ApprovalResult(ApprovalDecision.Rejected, digest, "El usuario rechazó la acción crítica.");
            }

            progress?.Report(AgentProgress.Of(
                AgentPhase.Understanding,
                action: "aprobación humana",
                path: action.Path,
                iteration: iteration,
                message: "La acción sigue bloqueada. Responde exactamente APROBAR " + confirmationCode + " o RECHAZAR " + confirmationCode + ".",
                flag: ProgressFlag.Recovering));
        }
    }

    private static string ComputeApprovalDigest(AgentAction action, string? commitHash)
    {
        var material = string.Join("\n", commitHash ?? "sin-head-git", action.Action, action.Path ?? "", action.Content ?? "", action.Original ?? "", action.Replacement ?? "");
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(material)));
    }

    private static async Task<string?> ReadCurrentGitCommitAsync(string workingDirectory, CancellationToken cancellationToken)
    {
        try
        {
            var start = new ProcessStartInfo("git")
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            start.ArgumentList.Add("rev-parse");
            start.ArgumentList.Add("--verify");
            start.ArgumentList.Add("HEAD^{commit}");
            using var process = Process.Start(start);
            if (process is null)
                return null;

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var commitHash = (await outputTask.ConfigureAwait(false)).Trim();
            _ = await errorTask.ConfigureAwait(false);
            if (process.ExitCode != 0 || (commitHash.Length is not (40 or 64)) || !commitHash.All(Uri.IsHexDigit))
                return null;

            return commitHash.ToLowerInvariant();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private enum ApprovalDecision
    {
        Approved,
        Rejected,
        WaitingForUser,
        Unavailable
    }

    private readonly record struct ApprovalResult(ApprovalDecision Decision, string Digest, string? Reason);

    private sealed class AgentAdmittedActionState
    {
        public AgentProfile Profile = null!;
        public string Model = "";
        public string Intention = "";
        public string WorkingDirectory = "";
        public List<AgentStep> Steps = null!;
        public AgentCheckpoint Checkpoint = null!;
        public List<LlmMessage> Messages = null!;
        public IAgentProgressObserver? Progress;
        public bool PlanBUsed;
        public string? PlanBInitialFailure;
        public bool AwaitingPlanBAction;
        public int EffectiveMaxIterations;
        public Func<string?, int, Task<AgentResult?>> TryPlanBHtmlLinkRepairAsync = null!;
        public TaskRiskAssessment RiskAssessment = null!;
        public int ScopeObservationStartIndex;
        public List<string>? AuthorizedScope;
        public List<string>? ExcludedScope;
        public int ReviewReplanStepStart;
        public AgentToolset Toolset = null!;
        public CancellationToken CancellationToken;
        public AgentInventory? Inventory;
        public int Modifications;
        public bool RequiresIndependentReview;
        public bool RequiresMultiAgentReview;
        public AgentHarness Harness = null!;
        public RepoSnapshot OriginalSnapshot = null!;
        public bool ResourcesWarned;
        public int RedundantObservations;
        public string? LastDoneReason;
        public Func<int, bool> ApplyPendingSteering = null!;
        public AgentPlanRecoveryState PlanRecoveryState = null!;
    }

    private sealed class AgentPlanRecoveryState
    {
        private readonly HashSet<string> _invalidPlanFingerprints = new(StringComparer.Ordinal);
        private int _invalidStructuredResponseCount;
        private string? _acceptedPlanFingerprint;
        private int _acceptedPlanRepeatCount;

        public bool TryRegisterInvalidPlan(string fingerprint) => _invalidPlanFingerprints.Add(fingerprint);
        public int RegisterInvalidStructuredResponse() => ++_invalidStructuredResponseCount;

        public void RegisterAcceptedPlan(string fingerprint)
        {
            ResetInvalidAttempts();
            _acceptedPlanFingerprint = fingerprint;
            _acceptedPlanRepeatCount = 0;
        }

        public void ResetInvalidAttempts()
        {
            _invalidPlanFingerprints.Clear();
            _invalidStructuredResponseCount = 0;
        }

        public void ResetAcceptedPlan()
        {
            _acceptedPlanFingerprint = null;
            _acceptedPlanRepeatCount = 0;
        }

        public bool TryRegisterRepeatedAcceptedPlan(string fingerprint, out int repeatCount)
        {
            if (_acceptedPlanFingerprint is null)
            {
                repeatCount = 0;
                return false;
            }

            // Keep the plan guarded until execution makes progress. A model may
            // paraphrase its objective or steps while still avoiding the work.
            _acceptedPlanFingerprint = fingerprint;
            repeatCount = ++_acceptedPlanRepeatCount;
            return true;
        }

        public void Reset()
        {
            ResetInvalidAttempts();
            ResetAcceptedPlan();
        }
    }

    private async Task<(AgentResult? Result, bool ContinueIteration)> ProcessAdmittedActionAsync(
        AgentAdmittedActionState state,
        AgentAction action,
        int currentIteration)
    {
        var requestedActionName = action.Action;
        var requestedActionPath = action.Path;
        var normalizedRequestedRootHtmlPath = TryNormalizeRequestedRootHtmlPath(action, state.Intention, state.WorkingDirectory);
        var normalizedCombinedFileAction = state.AwaitingPlanBAction &&
            TryNormalizeCombinedFileAction(action, state.WorkingDirectory);

        var admission = await AdmitAgentActionAsync(
            action,
            currentIteration,
            state.Profile,
            state.Model,
            state.Intention,
            state.RiskAssessment.Level,
            state.Steps,
            state.Checkpoint,
            state.Messages,
            state.Progress,
            state.PlanBUsed,
            state.PlanBInitialFailure,
            state.AwaitingPlanBAction,
            state.EffectiveMaxIterations,
            state.PlanRecoveryState,
            state.TryPlanBHtmlLinkRepairAsync).ConfigureAwait(true);
        state.PlanBUsed = admission.PlanBUsed;
        state.PlanBInitialFailure = admission.PlanBInitialFailure;
        state.AwaitingPlanBAction = admission.AwaitingPlanBAction;
        state.EffectiveMaxIterations = admission.EffectiveMaxIterations;
        if (admission.Result is not null)
            return (admission.Result, false);
        if (admission.ContinueIteration)
            return (null, true);

        var preparation = await PrepareAdmittedAgentActionAsync(
            action, currentIteration, state.Intention, state.Model, state.RiskAssessment,
            state.WorkingDirectory, state.ScopeObservationStartIndex, state.AuthorizedScope, state.ExcludedScope,
            state.ReviewReplanStepStart, state.Steps, state.Toolset, state.Messages, state.Progress,
            state.CancellationToken, state.Checkpoint, state.PlanRecoveryState, state.Inventory).ConfigureAwait(true);
        state.AuthorizedScope = preparation.AuthorizedScope;
        state.ExcludedScope = preparation.ExcludedScope;
        state.ReviewReplanStepStart = preparation.ReviewReplanStepStart;
        if (preparation.Result is not null)
            return (preparation.Result, false);
        if (preparation.ContinueIteration)
            return (null, true);

        var execution = await ExecuteApprovedAgentActionAsync(
            action, currentIteration, state.Profile.Id, state.Intention, state.Model, state.WorkingDirectory,
            state.Toolset, state.Progress, requestedActionName, requestedActionPath,
            normalizedCombinedFileAction, normalizedRequestedRootHtmlPath,
            state.AwaitingPlanBAction, state.RiskAssessment, state.RequiresIndependentReview,
            state.RequiresMultiAgentReview, state.Checkpoint, state.Steps, state.Messages,
            state.CancellationToken, state.Inventory, state.Modifications, state.ReviewReplanStepStart,
            state.AuthorizedScope, state.ExcludedScope, state.Harness, state.OriginalSnapshot,
            state.PlanBUsed, state.PlanBInitialFailure, state.EffectiveMaxIterations).ConfigureAwait(true);
        if (execution.Result is not null)
            return (execution.Result, false);
        action = execution.Action!;
        var step = execution.Step!;
        if (IsPlanProgressAction(action.Action, step))
            state.PlanRecoveryState.ResetAcceptedPlan();
        state.Modifications = execution.Modifications;
        state.ReviewReplanStepStart = execution.ReviewReplanStepStart;
        state.RequiresIndependentReview = execution.RequiresIndependentReview;
        state.PlanBUsed = execution.PlanBUsed;
        state.PlanBInitialFailure = execution.PlanBInitialFailure;
        state.AwaitingPlanBAction = execution.AwaitingPlanBAction;
        state.EffectiveMaxIterations = execution.EffectiveMaxIterations;

        EvaluateResourcesAndWarn(state.Checkpoint, state.Progress, ref state.ResourcesWarned, currentIteration);

        var observationOutcome = await HandleObservationOutcomeAsync(
            action,
            step,
            currentIteration,
            state.RedundantObservations,
            state.Modifications,
            state.Intention,
            state.Model,
            state.WorkingDirectory,
            state.ScopeObservationStartIndex,
            state.Steps,
            state.Toolset,
            state.Messages,
            state.Progress,
            state.Checkpoint,
            state.CancellationToken,
            state.PlanBUsed,
            state.PlanBInitialFailure,
            state.AwaitingPlanBAction,
            state.EffectiveMaxIterations,
            state.TryPlanBHtmlLinkRepairAsync).ConfigureAwait(true);
        state.RedundantObservations = observationOutcome.RedundantObservations;
        state.PlanBUsed = observationOutcome.PlanBUsed;
        state.PlanBInitialFailure = observationOutcome.PlanBInitialFailure;
        state.AwaitingPlanBAction = observationOutcome.AwaitingPlanBAction;
        state.EffectiveMaxIterations = observationOutcome.EffectiveMaxIterations;
        if (observationOutcome.Result is not null)
            return (observationOutcome.Result, false);
        if (observationOutcome.ContinueIteration)
            return (null, true);

        if (IsModification(action.Action) && step.Success)
        {
            var modifiedVerification = await VerifyModifiedActionAsync(
                currentIteration, state.Intention, state.Model, state.WorkingDirectory, state.Checkpoint,
                state.Steps, state.Messages, state.Progress, state.CancellationToken, state.OriginalSnapshot,
                state.Harness, state.RiskAssessment, state.AuthorizedScope, state.ExcludedScope, state.Modifications,
                state.LastDoneReason, state.PlanBUsed, state.PlanBInitialFailure, state.AwaitingPlanBAction,
                state.EffectiveMaxIterations, state.RequiresIndependentReview, state.RequiresMultiAgentReview,
                state.ApplyPendingSteering).ConfigureAwait(true);
            state.LastDoneReason = modifiedVerification.LastDoneReason;
            state.Modifications = modifiedVerification.Modifications;
            state.PlanBUsed = modifiedVerification.PlanBUsed;
            state.PlanBInitialFailure = modifiedVerification.PlanBInitialFailure;
            state.AwaitingPlanBAction = modifiedVerification.AwaitingPlanBAction;
            state.EffectiveMaxIterations = modifiedVerification.EffectiveMaxIterations;
            if (modifiedVerification.Result is not null)
                return (modifiedVerification.Result, false);
            if (modifiedVerification.ContinueIteration)
                return (null, true);
        }

        if (action.Action == AgentAction.ActionDone)
        {
            var doneOutcome = await HandleDoneActionAsync(
                action,
                currentIteration,
                state.Profile,
                state.Modifications,
                state.Intention,
                state.Model,
                state.WorkingDirectory,
                state.Inventory,
                state.RiskAssessment,
                state.AuthorizedScope,
                state.ExcludedScope,
                state.Steps,
                state.Checkpoint,
                state.Messages,
                state.Progress,
                state.LastDoneReason,
                state.PlanBUsed,
                state.PlanBInitialFailure,
                state.AwaitingPlanBAction,
                state.EffectiveMaxIterations,
                state.RequiresIndependentReview,
                state.RequiresMultiAgentReview,
                state.Harness,
                state.CancellationToken,
                state.OriginalSnapshot,
                state.ScopeObservationStartIndex,
                state.ApplyPendingSteering).ConfigureAwait(true);
            state.LastDoneReason = doneOutcome.LastDoneReason;
            state.Modifications = doneOutcome.Modifications;
            state.PlanBUsed = doneOutcome.PlanBUsed;
            state.PlanBInitialFailure = doneOutcome.PlanBInitialFailure;
            state.AwaitingPlanBAction = doneOutcome.AwaitingPlanBAction;
            state.EffectiveMaxIterations = doneOutcome.EffectiveMaxIterations;
            if (doneOutcome.Result is not null)
                return (doneOutcome.Result, false);
            if (doneOutcome.ContinueIteration)
                return (null, true);
        }

        var progressOutcome = await CheckAgentIterationProgressAsync(
            currentIteration, state.Steps, _limits, state.EffectiveMaxIterations,
            state.Intention, state.WorkingDirectory, state.Model, state.Progress, state.LastDoneReason,
            state.Inventory, state.Checkpoint, state.PlanBUsed, state.PlanBInitialFailure,
            state.AwaitingPlanBAction, state.Modifications, state.Messages, state.RiskAssessment,
            state.AuthorizedScope, state.ExcludedScope, state.Harness, state.OriginalSnapshot,
            state.CancellationToken).ConfigureAwait(true);
        state.LastDoneReason = progressOutcome.LastDoneReason;
        state.Modifications = progressOutcome.Modifications;
        state.PlanBUsed = progressOutcome.PlanBUsed;
        state.PlanBInitialFailure = progressOutcome.PlanBInitialFailure;
        state.AwaitingPlanBAction = progressOutcome.AwaitingPlanBAction;
        state.EffectiveMaxIterations = progressOutcome.EffectiveMaxIterations;
        if (progressOutcome.Result is not null)
            return (progressOutcome.Result, false);
        if (progressOutcome.ContinueIteration)
            return (null, true);

        state.Checkpoint.NextAction = "siguiente";
        return (null, false);
    }

    private async Task<(AgentResult? Result, bool ContinueIteration)> ProcessAgentIterationAsync(
        AgentAdmittedActionState state,
        int currentIteration)
    {
        state.Checkpoint.Iteration = currentIteration;
        state.ApplyPendingSteering(currentIteration);
        if (state.AwaitingPlanBAction)
        {
            var maxPlanBIterations = Math.Max(1, _limits.MaxPlanBIterations);
            if (state.Checkpoint.PlanBIterationsUsed >= maxPlanBIterations)
            {
                var limitFailure = StartPlanB(
                    null,
                    null,
                    "Plan B alcanzo el limite de " + maxPlanBIterations + " iteraciones sin completar una accion correctiva.",
                    ref state.PlanBUsed,
                    ref state.PlanBInitialFailure,
                    ref state.AwaitingPlanBAction,
                    ref state.EffectiveMaxIterations,
                    currentIteration,
                    state.Messages,
                    state.Progress,
                    state.Checkpoint);
                return (Fail(limitFailure ?? "Plan B no pudo completar una accion correctiva.", state.Model, state.Intention, state.Steps, state.Checkpoint), false);
            }
            state.Checkpoint.PlanBIterationsUsed++;
        }

        var modelOutcome = await RequestAgentActionForIterationAsync(
            state.Model,
            state.Messages,
            currentIteration,
            state.Checkpoint,
            state.Steps,
            state.Intention,
            state.Progress,
            state.CancellationToken,
            state.ApplyPendingSteering).ConfigureAwait(true);
        if (modelOutcome.Result is not null)
            return (modelOutcome.Result, false);
        if (modelOutcome.ContinueIteration)
            return (null, true);

        var call = modelOutcome.Call;
        if (call.Action is null)
        {
            var protocolOutcome = await HandleNullActionAsync(
                call,
                currentIteration,
                state.Model,
                state.Intention,
                state.Inventory,
                state.Toolset,
                state.WorkingDirectory,
                state.Steps,
                state.Checkpoint,
                state.Messages,
                state.Progress,
                state.CancellationToken,
                state.PlanBUsed,
                state.PlanBInitialFailure,
                state.AwaitingPlanBAction,
                state.EffectiveMaxIterations,
                state.PlanRecoveryState,
                state.TryPlanBHtmlLinkRepairAsync).ConfigureAwait(true);
            state.PlanBUsed = protocolOutcome.PlanBUsed;
            state.PlanBInitialFailure = protocolOutcome.PlanBInitialFailure;
            state.AwaitingPlanBAction = protocolOutcome.AwaitingPlanBAction;
            state.EffectiveMaxIterations = protocolOutcome.EffectiveMaxIterations;
            if (protocolOutcome.Result is not null)
                return (protocolOutcome.Result, false);
            return (null, true);
        }

        var action = call.Action;
        if (state.ApplyPendingSteering(currentIteration))
            return (null, true);

        return await ProcessAdmittedActionAsync(state, action, currentIteration).ConfigureAwait(true);
    }

    public async Task<AgentResult> RunAsync(string intention, IAgentProgressObserver? progress = null, CancellationToken cancellationToken = default)
    {
        var executionId = Guid.NewGuid().ToString("N");
        var result = await RunCoreAsync(intention, progress, cancellationToken).ConfigureAwait(false);
        var correlationTask = string.IsNullOrWhiteSpace(result.Checkpoint?.Task)
            ? intention
            : result.Checkpoint.Task;
        var workingDirectory = result.Checkpoint?.WorkingDirectory;
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            try
            {
                workingDirectory = _workingDirectoryProvider();
            }
            catch
            {
                // The task result remains authoritative if the directory provider fails after completion.
            }
        }

        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            try
            {
                var link = await DeliveryMetricsTaskLinker.LinkAsync(
                    _stateStore, workingDirectory, correlationTask, executionId, CancellationToken.None).ConfigureAwait(false);
                if (link.LinkedRecords.Count > 0)
                {
                    result.TaskExecutionId = executionId;
                    result.LinkedDeliveryMetricRecords = link.LinkedRecords.ToList();
                }
                result.DeliveryMetricsLinkWarning = link.Warning;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                result.DeliveryMetricsLinkWarning = "No se pudo guardar el vínculo con las métricas locales.";
            }
        }

        return result;
    }

    private async Task<AgentResult> RunCoreAsync(string intention, IAgentProgressObserver? progress, CancellationToken cancellationToken)
    {
        var checkpoint = new AgentCheckpoint { Task = intention, GeneratedAtUtc = DateTime.UtcNow };
        var steps = new List<AgentStep>();
        var modifications = 0;
        var reviewReplanStepStart = 0;
        List<string>? authorizedScope = null;
        List<string>? excludedScope = null;
        var scopeObservationStartIndex = 0;
        var effectiveMaxIterations = _limits.MaxIterations;
        var planBUsed = false;
        string? planBInitialFailure = null;
        var awaitingPlanBAction = false;
        var planRecoveryState = new AgentPlanRecoveryState();

        var workingDir = _workingDirectoryProvider();
        if (string.IsNullOrWhiteSpace(workingDir) || !Directory.Exists(workingDir))
        {
            return Fail("No hay un directorio de trabajo util. Ejecuta desde el proyecto.", "", intention, steps, checkpoint);
        }

        var continuation = await RestoreContinuationIfRequestedAsync(
            intention, workingDir, checkpoint, steps, cancellationToken).ConfigureAwait(false);
        if (continuation.Result is not null)
            return continuation.Result;
        checkpoint = continuation.Checkpoint;
        intention = continuation.Intention;
        checkpoint.WorkingDirectory = workingDir;

        var intentPreparation = await PrepareAgentIntentAsync(
            intention, workingDir, checkpoint, steps, progress, cancellationToken).ConfigureAwait(true);
        if (intentPreparation.Result is not null)
            return intentPreparation.Result;
        var requirement = intentPreparation.Requirement;
        var riskAssessment = intentPreparation.RiskAssessment;
        authorizedScope = intentPreparation.AuthorizedScope;
        excludedScope = intentPreparation.ExcludedScope;

        var profile = Condor.Core.Evaluation.AgentProfileCatalog.Resolve(intention);
        var requiresMultiAgentReview = MultiAgentReviewPolicy.IsRequested(intention);
        var requiresIndependentReview = profile.Id == "engineer" && _delegationService is not null &&
            (RequestsIndependentReview(intention) || requiresMultiAgentReview);

        var modelPreparation = await PrepareAgentModelAsync(
            requirement, intention, steps, checkpoint, progress, cancellationToken).ConfigureAwait(true);
        if (modelPreparation.Result is not null)
            return modelPreparation.Result;
        var selection = modelPreparation.Selection!;
        var model = modelPreparation.Model!;
        var inventory = modelPreparation.Inventory;

        var toolset = new AgentToolset(workingDir, maxContent: _limits.MaxContentLength);
        var harness = new AgentHarness(workingDir, _limits, steps, intention);
        var originalSnapshot = RepoSnapshot.Capture(workingDir);

        // Observar el directorio real primero. El manifest .NET es OPCIONAL:
        // solo informa de la capacidad de build/test disponible; NO es un
        // requisito de entrada. Para intenciones abiertas (analisis, ""de que
        // va"") el agente razona sobre lo que realmente existe en el directorio.
        var snapshot = await ListRootSnapshotAsync(toolset, workingDir, cancellationToken);
        var manifest = FindManifest(workingDir);

        // Verificar disponibilidad de la sesion y ajustar el prompt al modelo
        // seleccionado (adaptacion por capacidades; no un prompt unico estatico).
        var activeModel = selection.Desired;
        var systemPrompt = ModelPromptBuilder.BuildSystemPrompt(
            workingDir, manifest, activeModel, riskAssessment.Level, profile, delegationEnabled: _delegationService is not null);

        var messages = new List<LlmMessage>
        {
            new() { Role = "system", Content = systemPrompt },
            new() { Role = "user", Content = "Contexto inicial del repositorio (estructura de la raiz):\n" + snapshot + "\n\nTarea: " + intention + "\n\nCONTEXTO ESTRICTO: si el modelo soporta salida estructurada, emite SIEMPRE una accion JSON (ver system). Para una solicitud de comprension (\"que tenemos aqui\"), observa con list_dir/read_file y cuando tengas suficiente evidencia responde usando done, colocando tu sintesis en 'reason'. No respondas fuera de lo indicado en system." }
        };

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_limits.TimeoutMilliseconds);

        string? lastDoneReason = null;

        try
        {
            var redundantObservations = 0;
            var resourcesWarned = false;

            async Task<AgentResult?> TryPlanBHtmlLinkRepairAsync(string? failureReason, int currentIteration)
            {
                var outcome = await TryRepairRequestedHtmlLinkAsync(
                    failureReason,
                    currentIteration,
                    riskAssessment,
                    authorizedScope,
                    excludedScope,
                    intention,
                    workingDir,
                    steps,
                    timeoutCts.Token,
                    planBUsed,
                    planBInitialFailure,
                    awaitingPlanBAction,
                    modifications,
                    checkpoint,
                    progress,
                    messages,
                    harness,
                    originalSnapshot,
                    model).ConfigureAwait(false);
                planBUsed = outcome.PlanBUsed;
                planBInitialFailure = outcome.PlanBInitialFailure;
                awaitingPlanBAction = outcome.AwaitingPlanBAction;
                modifications = outcome.Modifications;
                return outcome.Result;
            }
            var initialResources = EvaluateResources();
            progress?.Report(AgentProgress.Of(AgentPhase.Understanding,
                message: "Comprendiendo la solicitud",
                resourceState: initialResources?.PressureLabel,
                availableGb: initialResources?.FreeGb,
                safeBudgetGb: initialResources?.SafeBudgetGb));

            bool ApplyPendingSteering(int currentIteration)
            {
                var outcome = ApplySteeringChanges(
                    currentIteration,
                    intention,
                    riskAssessment,
                    authorizedScope,
                    excludedScope,
                    scopeObservationStartIndex,
                    steps,
                    checkpoint,
                    messages,
                    progress,
                    _steeringChannel);
                intention = outcome.Intention;
                riskAssessment = outcome.RiskAssessment;
                authorizedScope = outcome.AuthorizedScope;
                excludedScope = outcome.ExcludedScope;
                scopeObservationStartIndex = outcome.ScopeObservationStartIndex;
                return outcome.Changed;
            }



            for (var iteration = 0; iteration < effectiveMaxIterations; iteration++)
            {
                var actionState = new AgentAdmittedActionState
                {
                    Profile = profile,
                    Model = model,
                    Intention = intention,
                    WorkingDirectory = workingDir,
                    Steps = steps,
                    Checkpoint = checkpoint,
                    Messages = messages,
                    Progress = progress,
                    PlanBUsed = planBUsed,
                    PlanBInitialFailure = planBInitialFailure,
                    AwaitingPlanBAction = awaitingPlanBAction,
                    EffectiveMaxIterations = effectiveMaxIterations,
                    RiskAssessment = riskAssessment,
                    ScopeObservationStartIndex = scopeObservationStartIndex,
                    AuthorizedScope = authorizedScope,
                    ExcludedScope = excludedScope,
                    ReviewReplanStepStart = reviewReplanStepStart,
                    Toolset = toolset,
                    CancellationToken = timeoutCts.Token,
                    Inventory = inventory,
                    Modifications = modifications,
                    RequiresIndependentReview = requiresIndependentReview,
                    RequiresMultiAgentReview = requiresMultiAgentReview,
                    Harness = harness,
                    OriginalSnapshot = originalSnapshot,
                    ResourcesWarned = resourcesWarned,
                    RedundantObservations = redundantObservations,
                    LastDoneReason = lastDoneReason,
                    PlanRecoveryState = planRecoveryState
                };
                actionState.TryPlanBHtmlLinkRepairAsync = async (failureReason, currentIteration) =>
                {
                    var result = await TryPlanBHtmlLinkRepairAsync(failureReason, currentIteration).ConfigureAwait(true);
                    actionState.PlanBUsed = planBUsed;
                    actionState.PlanBInitialFailure = planBInitialFailure;
                    actionState.AwaitingPlanBAction = awaitingPlanBAction;
                    actionState.EffectiveMaxIterations = effectiveMaxIterations;
                    actionState.Modifications = modifications;
                    return result;
                };
                actionState.ApplyPendingSteering = currentIteration =>
                {
                    var changed = ApplyPendingSteering(currentIteration);
                    actionState.Intention = intention;
                    actionState.RiskAssessment = riskAssessment;
                    actionState.ScopeObservationStartIndex = scopeObservationStartIndex;
                    actionState.AuthorizedScope = authorizedScope;
                    actionState.ExcludedScope = excludedScope;
                    if (changed)
                        planRecoveryState.Reset();
                    return changed;
                };

                var iterationOutcome = await ProcessAgentIterationAsync(actionState, iteration + 1).ConfigureAwait(true);
                intention = actionState.Intention;
                riskAssessment = actionState.RiskAssessment;
                scopeObservationStartIndex = actionState.ScopeObservationStartIndex;
                authorizedScope = actionState.AuthorizedScope;
                excludedScope = actionState.ExcludedScope;
                planBUsed = actionState.PlanBUsed;
                planBInitialFailure = actionState.PlanBInitialFailure;
                awaitingPlanBAction = actionState.AwaitingPlanBAction;
                effectiveMaxIterations = actionState.EffectiveMaxIterations;
                reviewReplanStepStart = actionState.ReviewReplanStepStart;
                modifications = actionState.Modifications;
                requiresIndependentReview = actionState.RequiresIndependentReview;
                resourcesWarned = actionState.ResourcesWarned;
                redundantObservations = actionState.RedundantObservations;
                lastDoneReason = actionState.LastDoneReason;
                if (iterationOutcome.Result is not null)
                    return iterationOutcome.Result;
                if (iterationOutcome.ContinueIteration)
                    continue;
            }
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            return Fail("Tiempo total del agente agotado.", model, intention, steps, checkpoint);
        }
        catch (Exception ex)
        {
            return Fail("Error interno del agente: " + ex.Message, model, intention, steps, checkpoint);
        }
        finally
        {
            checkpoint.CompletedSteps.AddRange(steps
                .Select(step => step.Action + (string.IsNullOrWhiteSpace(step.Path) ? "" : " " + step.Path) + (step.Success ? " · completado" : " · fallido"))
                .ToList());
            checkpoint.WorkingDirectory = workingDir;
            var checkpointSaved = await _stateStore.SaveAgentCheckpointAsync(workingDir, checkpoint, CancellationToken.None);
            if (!checkpointSaved)
            {
                checkpoint.LastError = "No se pudo guardar el punto de continuidad local.";
                progress?.Report(AgentProgress.Of(AgentPhase.Verifying, message: checkpoint.LastError, flag: ProgressFlag.Recovering));
            }
        }

        return await CompleteAgentRunAsync(
            workingDir, model, intention, steps, checkpoint, progress, lastDoneReason, inventory, planBUsed).ConfigureAwait(true);
    }

    private async Task<AgentResult> CompleteAgentRunAsync(
        string workingDirectory,
        string model,
        string intention,
        List<AgentStep> steps,
        AgentCheckpoint checkpoint,
        IAgentProgressObserver? progress,
        string? lastDoneReason,
        AgentInventory? inventory,
        bool planBUsed)
    {
        // Si se agotaron las iteraciones, entrega respuestas informativas solo cuando haya evidencia observada.
        var finalDelivery = TryDeliverInformational(checkpoint, steps, workingDirectory, model, intention, progress, lastDoneReason, inventory);
        if (finalDelivery is not null)
        {
            var completed = ApplyPlanBCompletion(finalDelivery, planBUsed);
            if (completed.Success) checkpoint.NextAction = "entregar";
            if (!await _stateStore.SaveAgentCheckpointAsync(workingDirectory, checkpoint, CancellationToken.None).ConfigureAwait(false) && completed.Success)
            {
                checkpoint.LastError = "No se pudo guardar el punto de continuidad local.";
                progress?.Report(AgentProgress.Of(AgentPhase.Verifying, message: checkpoint.LastError, flag: ProgressFlag.Recovering));
            }
            return completed;
        }

        var failureResult = Fail(
            planBUsed ? "Plan B no pudo completar la tarea dentro de los limites." : "El agente no pudo completar la tarea dentro de los limites.",
            model,
            intention,
            steps,
            checkpoint);
        checkpoint.NextAction = "revisar";
        await _stateStore.SaveAgentCheckpointAsync(workingDirectory, checkpoint, CancellationToken.None).ConfigureAwait(false);
        return failureResult;
    }

    private static AgentDoneOutcome? ValidateDoneReviewGate(
        AgentAction action,
        int iteration,
        Condor.Core.Models.AgentProfile profile,
        string intention,
        string model,
        List<AgentStep> steps,
        AgentCheckpoint checkpoint,
        List<LlmMessage> messages,
        IAgentProgressObserver? progress,
        string? lastDoneReason,
        int modifications,
        bool planBUsed,
        string? planBInitialFailure,
        bool awaitingPlanBAction,
        int effectiveMaxIterations,
        bool requiresIndependentReview,
        bool requiresMultiAgentReview)
    {
        AgentDoneOutcome Continue() => new(
            null, true, modifications, lastDoneReason, planBUsed,
            planBInitialFailure, awaitingPlanBAction, effectiveMaxIterations);

        if (profile.Id is "reviewer" or "security-reviewer")
        {
            var reviewError = ValidateReviewFindings(action.ReviewFindings, workingDirectory: checkpoint.WorkingDirectory ?? "", steps);
            if (reviewError is not null)
            {
                messages.Add(new LlmMessage { Role = "user", Content = reviewError });
                steps.Add(new AgentStep
                {
                    Iteration = iteration,
                    Action = AgentAction.ActionDone,
                    Success = false,
                    ResultPreview = reviewError,
                    AtUtc = DateTime.UtcNow
                });
                return Continue();
            }

            var openFindings = action.ReviewFindings!.Where(finding =>
                finding.Status.Equals("open", StringComparison.OrdinalIgnoreCase)).ToList();
            if (openFindings.Count > 0)
            {
                var criticalCount = openFindings.Count(finding =>
                    finding.Severity.Equals("critical", StringComparison.OrdinalIgnoreCase));
                var reason = criticalCount > 0
                    ? $"La revisión no se puede cerrar: quedan {criticalCount} hallazgos críticos abiertos."
                    : $"La revisión no se puede cerrar: quedan {openFindings.Count} hallazgos abiertos sin corregir o justificar.";
                return new AgentDoneOutcome(
                    Fail(reason + "\n" + FormatReviewFindings(openFindings), model, intention, steps, checkpoint),
                    false, modifications, lastDoneReason, planBUsed, planBInitialFailure,
                    awaitingPlanBAction, effectiveMaxIterations);
            }
        }

        var lastChange = steps.LastOrDefault(step => step.Success && IsModification(step.Action));
        if (lastChange is not null && !steps.Any(step => step.Success &&
            step.Action == AgentAction.ActionGitDiff && step.Iteration > lastChange.Iteration))
        {
            const string reviewRequired = "Antes de terminar, revisa con git_diff el último cambio realizado.";
            checkpoint.LastDecision = "revision_diff_pendiente";
            messages.Add(new LlmMessage { Role = "user", Content = reviewRequired });
            progress?.Report(AgentProgress.Of(
                AgentPhase.Verifying,
                action: AgentAction.ActionGitDiff,
                iteration: iteration,
                message: reviewRequired,
                flag: ProgressFlag.Recovering));
            return Continue();
        }

        if (requiresIndependentReview && checkpoint.UnreviewedModifications > 0)
        {
            var reviewGate = requiresMultiAgentReview
                ? "El cambio aún no tiene las dos revisiones independientes solicitadas. Delega el mismo diff a @reviewer y @security-reviewer en modo de solo lectura; contrasta hallazgos, verifica evidencia y vuelve a revisar antes de cerrar."
                : "El cambio aún no tiene una revisión independiente aprobada. Delega un revisor de solo lectura sobre el diff actual; corrige los hallazgos abiertos y vuelve a revisar antes de cerrar.";
            checkpoint.LastDecision = "revision_independiente_pendiente";
            messages.Add(new LlmMessage { Role = "user", Content = reviewGate });
            progress?.Report(AgentProgress.Of(
                AgentPhase.Verifying,
                action: AgentAction.ActionDelegate,
                iteration: iteration,
                message: reviewGate,
                flag: ProgressFlag.Recovering));
            return Continue();
        }

        return null;
    }

    private sealed record AgentDoneOutcome(
        AgentResult? Result,
        bool ContinueIteration,
        int Modifications,
        string? LastDoneReason,
        bool PlanBUsed,
        string? PlanBInitialFailure,
        bool AwaitingPlanBAction,
        int EffectiveMaxIterations);

    private async Task<AgentDoneOutcome> HandleDoneActionAsync(
        AgentAction action,
        int iteration,
        Condor.Core.Models.AgentProfile profile,
        int modifications,
        string intention,
        string model,
        string workingDir,
        AgentInventory? inventory,
        Condor.Core.Evaluation.TaskRiskAssessment riskAssessment,
        List<string>? authorizedScope,
        List<string>? excludedScope,
        List<AgentStep> steps,
        AgentCheckpoint checkpoint,
        List<LlmMessage> messages,
        IAgentProgressObserver? progress,
        string? lastDoneReason,
        bool planBUsed,
        string? planBInitialFailure,
        bool awaitingPlanBAction,
        int effectiveMaxIterations,
        bool requiresIndependentReview,
        bool requiresMultiAgentReview,
        AgentHarness harness,
        CancellationToken cancellationToken,
        RepoSnapshot originalSnapshot,
        int scopeObservationStartIndex,
        Func<int, bool> applyPendingSteering)
    {
        AgentDoneOutcome Outcome(AgentResult? result = null, bool continueIteration = false)
            => new(result, continueIteration, modifications, lastDoneReason,
                planBUsed, planBInitialFailure, awaitingPlanBAction, effectiveMaxIterations);

        var reviewGateOutcome = ValidateDoneReviewGate(
            action,
            iteration,
            profile,
            intention,
            model,
            steps,
            checkpoint,
            messages,
            progress,
            lastDoneReason,
            modifications,
            planBUsed,
            planBInitialFailure,
            awaitingPlanBAction,
            effectiveMaxIterations,
            requiresIndependentReview,
            requiresMultiAgentReview);
        if (reviewGateOutcome is not null)
        {
            return reviewGateOutcome;
        }
        if (modifications == 0)
        {
            return CompleteDoneWithoutModifications(
                action, profile, inventory, model, intention, steps, checkpoint,
                progress, messages, lastDoneReason, modifications, planBUsed,
                planBInitialFailure, awaitingPlanBAction, effectiveMaxIterations);
        }

        progress?.Report(AgentProgress.Of(AgentPhase.Verifying, action: "build/test", iteration: iteration));
        var verification = await harness.VerifyAsync(cancellationToken);
        var htmlRepair = planBUsed && riskAssessment.Level != Condor.Core.Evaluation.TaskRiskLevel.Critical
            ? await harness.TryRepairMissingRequiredHtmlLinkAsync(verification.Reason, cancellationToken)
            : null;
        if (htmlRepair is not null)
        {
            var repairedHtmlPath = htmlRepair.Value.RelativePath;
            var repairAction = htmlRepair.Value.Action;
            planBUsed = true;
            planBInitialFailure ??= verification.Reason;
            awaitingPlanBAction = false;
            checkpoint.LastDecision = "plan_b_html_link_repaired";
            checkpoint.LastAction = repairAction + " " + repairedHtmlPath;
            checkpoint.LastResult = repairAction == AgentAction.ActionCreateFile
                ? "Plan B creó el archivo HTML solicitado; el harness repetirá la verificación."
                : "Plan B insertó el enlace HTML solicitado; el harness repetirá la verificación.";
            steps.Add(new AgentStep
            {
                Action = repairAction,
                Path = repairedHtmlPath,
                Success = true,
                ResultPreview = checkpoint.LastResult
            });
            progress?.Report(AgentProgress.Of(
                AgentPhase.Verifying,
                message: checkpoint.LastResult,
                action: repairAction,
                path: repairedHtmlPath,
                iteration: iteration));
            verification = await harness.VerifyAsync(cancellationToken);
        }

        checkpoint.HarnessState = !verification.ChecksApplicable
            ? "no_aplicable"
            : verification.Done ? "exito" : verification.Reason;
        checkpoint.LastError = verification.Done ? null : verification.Reason;
        if (verification.Done)
        {
            return HandleSuccessfulDoneVerification(
                verification, iteration, workingDir, originalSnapshot, checkpoint,
                planBUsed, planBInitialFailure, awaitingPlanBAction, effectiveMaxIterations,
                messages, progress, model, intention, steps, modifications, lastDoneReason,
                requiresIndependentReview, requiresMultiAgentReview, applyPendingSteering,
                finalizationRequested: true);
        }

        var failedHarnessStep = steps.LastOrDefault(s => !s.Success &&
            (s.Action.Equals(AgentAction.ActionBuild, StringComparison.OrdinalIgnoreCase) ||
             s.Action.Equals(AgentAction.ActionTest, StringComparison.OrdinalIgnoreCase)))
            ?? new AgentStep { Action = "harness", Success = false, ResultPreview = verification.Detail };
        var testFailureEvidence = (verification.Reason ?? "") + Environment.NewLine + verification.Detail;
        if (failedHarnessStep.Action == AgentAction.ActionTest &&
            ShouldReportExcludedBehaviorMismatch(intention, checkpoint, testFailureEvidence))
        {
            checkpoint.LastDecision = "test_fallo_con_alcance_excluido";
            checkpoint.NextAction = "informar_resultado";
            var testFailureTail = verification.Detail.Length > 2000
                ? "…" + verification.Detail[^2000..]
                : verification.Detail;
            var reason = "La prueba solicitada se ejecutó y falló al comparar el resultado observado con la expectativa. " +
                         "Condor conserva la prueba y no cambia archivos excluidos. " + (verification.Reason ?? "") + Environment.NewLine + testFailureTail;
            progress?.Report(AgentProgress.Of(
                AgentPhase.Finalizing,
                action: AgentAction.ActionTest,
                iteration: iteration,
                message: "La prueba falló contra el comportamiento observado; se conserva el test y no se modifica el código excluido."));
            return Outcome(Fail(reason, model, intention, steps, checkpoint));
        }

        var planBFailure = StartPlanB(
            failedHarnessStep.Action,
            failedHarnessStep.Path,
            (verification.Reason ?? "Build/test fallo.") + " " + verification.Detail,
            ref planBUsed,
            ref planBInitialFailure,
            ref awaitingPlanBAction,
            ref effectiveMaxIterations,
            iteration,
            messages,
            progress,
            checkpoint);
        if (planBFailure is not null)
        {
            return Outcome(Fail(planBFailure, model, intention, steps, checkpoint));
        }

        var repairOutcome = await TryRepairRequestedHtmlLinkAsync(
            (verification.Reason ?? "Build/test fallo.") + " " + verification.Detail,
            iteration,
            riskAssessment,
            authorizedScope,
            excludedScope,
            intention,
            workingDir,
            steps,
            cancellationToken,
            planBUsed,
            planBInitialFailure,
            awaitingPlanBAction,
            modifications,
            checkpoint,
            progress,
            messages,
            harness,
            originalSnapshot,
            model);
        planBUsed = repairOutcome.PlanBUsed;
        planBInitialFailure = repairOutcome.PlanBInitialFailure;
        awaitingPlanBAction = repairOutcome.AwaitingPlanBAction;
        modifications = repairOutcome.Modifications;
        return Outcome(repairOutcome.Result);
    }

    private AgentDoneOutcome HandleSuccessfulDoneVerification(
        HarnessVerifyResult verification,
        int iteration,
        string workingDir,
        RepoSnapshot originalSnapshot,
        AgentCheckpoint checkpoint,
        bool planBUsed,
        string? planBInitialFailure,
        bool awaitingPlanBAction,
        int effectiveMaxIterations,
        List<LlmMessage> messages,
        IAgentProgressObserver? progress,
        string model,
        string intention,
        List<AgentStep> steps,
        int modifications,
        string? lastDoneReason,
        bool requiresIndependentReview,
        bool requiresMultiAgentReview,
        Func<int, bool> applyPendingSteering,
        bool finalizationRequested,
        bool recordIntegrityError = false)
    {
        AgentDoneOutcome Outcome(AgentResult? result = null, bool continueIteration = false)
            => new(result, continueIteration, modifications, lastDoneReason,
                planBUsed, planBInitialFailure, awaitingPlanBAction, effectiveMaxIterations);

        if (applyPendingSteering(iteration))
        {
            return Outcome(continueIteration: true);
        }

        var integrity = VerifyTestIntegrity(
            workingDir, originalSnapshot, checkpoint.DeclaredScope, checkpoint.ExcludedScope);
        if (integrity is not null)
        {
            if (recordIntegrityError)
                checkpoint.LastError = integrity;
            checkpoint.NextAction = "detener";
            checkpoint.LastDecision = "revisar";
            var integrityPlanBFailure = StartPlanB(
                "integridad_harness",
                null,
                integrity,
                ref planBUsed,
                ref planBInitialFailure,
                ref awaitingPlanBAction,
                ref effectiveMaxIterations,
                iteration,
                messages,
                progress,
                checkpoint);
            if (integrityPlanBFailure is not null)
            {
                return Outcome(Fail(integrityPlanBFailure, model, intention, steps, checkpoint));
            }
            return Outcome(continueIteration: true);
        }

        if (requiresIndependentReview && checkpoint.UnreviewedModifications > 0)
        {
            var reviewPrompt = requiresMultiAgentReview
                ? "Build/test pasó y la integridad de las pruebas está confirmada. Antes de finalizar, delega el mismo diff a @reviewer y @security-reviewer en revisiones independientes de solo lectura; compara y verifica cada hallazgo, descarta los no reproducibles con evidencia y vuelve a revisar el diff."
                : "Build/test pasó y la integridad de las pruebas está confirmada. Antes de finalizar, solicita una revisión independiente de solo lectura mediante delegate; atiende los hallazgos abiertos y vuelve a verificar el diff.";
            checkpoint.NextAction = "revision_independiente";
            checkpoint.LastDecision = "harness_verificado_revision_pendiente";
            messages.Add(new LlmMessage { Role = "user", Content = reviewPrompt });
            progress?.Report(AgentProgress.Of(
                AgentPhase.Verifying,
                action: AgentAction.ActionDelegate,
                iteration: iteration,
                message: reviewPrompt));
            return Outcome(continueIteration: true);
        }

        if (checkpoint.PlanSteps is not { Count: > 0 } || finalizationRequested)
        {
            checkpoint.NextAction = "entregar";
            checkpoint.LastDecision = planBUsed
                ? "plan_b_corregido"
                : verification.ChecksApplicable ? "verificar" : "verificacion_no_aplicable";
            var completionReason = HarnessCompletionReason(verification, planBUsed);
            progress?.Report(AgentProgress.Of(AgentPhase.Finalizing, message: completionReason));
            return Outcome(new AgentResult
            {
                Success = true,
                Reason = completionReason,
                Model = model,
                Objective = intention,
                Steps = steps,
                Checkpoint = checkpoint
            });
        }

        checkpoint.NextAction = "continuar_plan";
        checkpoint.LastDecision = verification.ChecksApplicable
            ? "cambio_verificado_continuar_plan"
            : "verificacion_no_aplicable_continuar_plan";
        var verificationStatus = verification.ChecksApplicable
            ? "Build/test pasó para el cambio recién aplicado."
            : "Build/test no aplica a este proyecto y no confirma la finalización.";
        const string continuePlan = " Continúa con los pasos pendientes del plan y sus comprobaciones; revisa el diff y usa done cuando la tarea completa esté resuelta.";
        messages.Add(new LlmMessage { Role = "user", Content = verificationStatus + continuePlan });
        progress?.Report(AgentProgress.Of(
            AgentPhase.Analyzing,
            action: AgentAction.ActionPlan,
            iteration: iteration,
            message: verificationStatus + " Se continuará con el plan vigente."));
        return Outcome(continueIteration: true);
    }

    private sealed record AgentIntentPreparationOutcome(
        AgentResult? Result,
        Condor.Core.Models.TaskModelRequirement Requirement,
        Condor.Core.Evaluation.TaskRiskAssessment RiskAssessment,
        List<string>? AuthorizedScope,
        List<string>? ExcludedScope);

    private async Task<AgentIntentPreparationOutcome> PrepareAgentIntentAsync(
        string intention,
        string workingDir,
        AgentCheckpoint checkpoint,
        List<AgentStep> steps,
        IAgentProgressObserver? progress,
        CancellationToken cancellationToken)
    {
        var requirement = TaskIntentClassifier.Classify(intention);
        var riskAssessment = Condor.Core.Evaluation.TaskRiskClassifier.Assess(intention);
        checkpoint.RiskLevel = riskAssessment.Level.ToString();
        List<string>? authorizedScope = null;
        List<string>? excludedScope = null;

        var ambiguityResult = await HandleAmbiguousIntentionAsync(
            intention,
            requirement.IntentKind == TaskIntentKinds.Consult,
            workingDir,
            checkpoint,
            steps,
            progress,
            cancellationToken).ConfigureAwait(false);
        if (ambiguityResult is not null)
        {
            return new AgentIntentPreparationOutcome(
                ambiguityResult, requirement, riskAssessment, authorizedScope, excludedScope);
        }

        var explicitRootHtml = AgentHarness.FindRequestedRootHtmlFile(intention);
        if (explicitRootHtml is not null &&
            TryAuthorizeExplicitSingleFileScope(intention, explicitRootHtml, riskAssessment.Level, out var rootHtmlScope))
        {
            authorizedScope = new List<string> { rootHtmlScope };
            excludedScope = new List<string>();
            checkpoint.DeclaredScope = authorizedScope.ToList();
            checkpoint.ExcludedScope = new List<string>();
            checkpoint.LastDecision = "alcance_implicito_archivo_explicito";
            checkpoint.NextAction = "ejecutar_cambio_acotado";
        }

        if (requirement.IntentKind == TaskIntentKinds.Consult)
        {
            var consultation = await RunDirectConsultAsync(
                intention, requirement, progress, cancellationToken).ConfigureAwait(true);
            return new AgentIntentPreparationOutcome(
                consultation, requirement, riskAssessment, authorizedScope, excludedScope);
        }

        return new AgentIntentPreparationOutcome(
            null, requirement, riskAssessment, authorizedScope, excludedScope);
    }

    private sealed record AgentModelPreparationOutcome(
        AgentResult? Result,
        Condor.Core.Models.ModelSelectionResult? Selection,
        string? Model,
        AgentInventory? Inventory);

    private async Task<AgentModelPreparationOutcome> PrepareAgentModelAsync(
        Condor.Core.Models.TaskModelRequirement requirement,
        string intention,
        List<AgentStep> steps,
        AgentCheckpoint checkpoint,
        IAgentProgressObserver? progress,
        CancellationToken cancellationToken)
    {
        var modelSetup = CreateModelSetup();
        var selection = await modelSetup.EnsureModelForRequirementAsync(
            requirement,
            cancellationToken: cancellationToken,
            currentModelName: _session?.ActiveModel,
            lastSelectionBudgetGb: _session?.LastSelectionBudgetGb ?? 0,
            lastSelectionFreeRamGb: _session?.LastSelectionFreeRamGb ?? 0);

        if (_session is not null && selection.Budget is not null)
        {
            _session.LastSelectionBudgetGb = selection.Budget.BudgetGb;
            _session.LastSelectionFreeRamGb = selection.Resources?.FreeGb ?? 0;
        }

        if (selection.Desired is null)
        {
            var unavailable = await HandleUnavailableModelSelectionAsync(
                selection, intention, steps, checkpoint, progress, cancellationToken).ConfigureAwait(false);
            return new AgentModelPreparationOutcome(unavailable, selection, null, null);
        }

        var model = selection.InstalledName ?? selection.Desired.PullName;
        checkpoint.Model = model;
        checkpoint.Strategy = "structured-action";
        checkpoint.LastDecision = "comprender";
        if (_session is not null)
            await _session.EnsureAvailableAsync(model, cancellationToken);

        var inventory = await BuildInventoryAsync(selection, cancellationToken);
        return new AgentModelPreparationOutcome(null, selection, model, inventory);
    }

    private sealed record ContinuationRestoreOutcome(
        AgentResult? Result,
        AgentCheckpoint Checkpoint,
        string Intention);

    private async Task<ContinuationRestoreOutcome> RestoreContinuationIfRequestedAsync(
        string intention,
        string workingDir,
        AgentCheckpoint checkpoint,
        List<AgentStep> steps,
        CancellationToken cancellationToken)
    {
        if (!IsContinuationRequest(intention))
            return new ContinuationRestoreOutcome(null, checkpoint, intention);

        var previous = await _stateStore.LoadAgentCheckpointAsync(workingDir, cancellationToken);
        if (previous is null)
        {
            return new ContinuationRestoreOutcome(
                Fail("No hay una tarea pendiente guardada para este proyecto.", "", intention, steps, checkpoint),
                checkpoint,
                intention);
        }

        return new ContinuationRestoreOutcome(
            null,
            RestoreContinuationCheckpoint(previous),
            BuildContinuationIntention(previous, intention));
    }

    private async Task<AgentResult?> HandleAmbiguousIntentionAsync(
        string intention,
        bool isConsult,
        string workingDir,
        AgentCheckpoint checkpoint,
        List<AgentStep> steps,
        IAgentProgressObserver? progress,
        CancellationToken cancellationToken)
    {
        var ambiguityQuestion = Condor.Core.Evaluation.TaskAmbiguityClassifier.FindQuestion(intention);
        if (ambiguityQuestion is null || isConsult)
            return null;

        checkpoint.NextAction = "esperar_aclaracion";
        checkpoint.LastDecision = "solicitud_ambigua";
        checkpoint.LastResult = ambiguityQuestion;
        if (!await _stateStore.SaveAgentCheckpointAsync(workingDir, checkpoint, cancellationToken))
        {
            progress?.Report(AgentProgress.Of(
                AgentPhase.Verifying,
                message: "No se pudo guardar el punto de continuidad local.",
                flag: ProgressFlag.Recovering));
        }
        progress?.Report(AgentProgress.Of(
            AgentPhase.Understanding,
            action: AgentAction.ActionClarify,
            message: ambiguityQuestion));
        return new AgentResult
        {
            Success = false,
            AwaitingUserInput = true,
            Reason = ambiguityQuestion,
            Objective = intention,
            Steps = steps,
            Checkpoint = checkpoint
        };
    }

    private async Task<AgentDoneOutcome> CheckAgentIterationProgressAsync(
        int iteration,
        List<AgentStep> steps,
        Condor.Core.Models.AgentLimits limits,
        int effectiveMaxIterations,
        string intention,
        string workingDir,
        string model,
        IAgentProgressObserver? progress,
        string? lastDoneReason,
        AgentInventory? inventory,
        AgentCheckpoint checkpoint,
        bool planBUsed,
        string? planBInitialFailure,
        bool awaitingPlanBAction,
        int modifications,
        List<LlmMessage> messages,
        Condor.Core.Evaluation.TaskRiskAssessment riskAssessment,
        List<string>? authorizedScope,
        List<string>? excludedScope,
        AgentHarness harness,
        RepoSnapshot originalSnapshot,
        CancellationToken cancellationToken)
    {
        AgentDoneOutcome Outcome(AgentResult? result = null, bool continueIteration = false)
            => new(result, continueIteration, modifications, lastDoneReason,
                planBUsed, planBInitialFailure, awaitingPlanBAction, effectiveMaxIterations);

        var progressCheck = AgentEngine.CheckProgress(iteration, steps, limits, effectiveMaxIterations);
        if (!progressCheck.Fail)
            return Outcome();

        var progressReason = progressCheck.Reason ?? "Limite sin progreso.";
        if (planBUsed && progressReason.Contains("limite de iteraciones", StringComparison.OrdinalIgnoreCase))
            progressReason = "Plan B no completo la tarea dentro del presupuesto de iteraciones.";
        checkpoint.LastError = progressReason;

        var delivered = TryDeliverInformational(
            checkpoint, steps, workingDir, model, intention, progress, lastDoneReason, inventory);
        if (delivered is not null)
            return Outcome(ApplyPlanBCompletion(delivered, planBUsed));

        var lastStep = steps.LastOrDefault();
        var planBFailure = StartPlanB(
            null,
            lastStep?.Path,
            progressReason + (lastStep is null
                ? ""
                : " Ultima accion: " + lastStep.Action + ". Resultado: " + (lastStep.ResultPreview ?? "sin detalle")),
            ref planBUsed,
            ref planBInitialFailure,
            ref awaitingPlanBAction,
            ref effectiveMaxIterations,
            iteration,
            messages,
            progress,
            checkpoint);
        if (planBFailure is not null)
            return Outcome(Fail(planBFailure, model, intention, steps, checkpoint));

        var repairOutcome = await TryRepairRequestedHtmlLinkAsync(
            progressReason,
            iteration,
            riskAssessment,
            authorizedScope,
            excludedScope,
            intention,
            workingDir,
            steps,
            cancellationToken,
            planBUsed,
            planBInitialFailure,
            awaitingPlanBAction,
            modifications,
            checkpoint,
            progress,
            messages,
            harness,
            originalSnapshot,
            model);
        planBUsed = repairOutcome.PlanBUsed;
        planBInitialFailure = repairOutcome.PlanBInitialFailure;
        awaitingPlanBAction = repairOutcome.AwaitingPlanBAction;
        modifications = repairOutcome.Modifications;
        return Outcome(repairOutcome.Result, continueIteration: true);
    }

    private sealed record AgentActionPreparationOutcome(
        AgentResult? Result,
        bool ContinueIteration,
        List<string>? AuthorizedScope,
        List<string>? ExcludedScope,
        int ReviewReplanStepStart);

    private async Task<AgentActionPreparationOutcome> PrepareAdmittedAgentActionAsync(
        AgentAction action,
        int iteration,
        string intention,
        string model,
        Condor.Core.Evaluation.TaskRiskAssessment riskAssessment,
        string workingDir,
        int scopeObservationStartIndex,
        List<string>? authorizedScope,
        List<string>? excludedScope,
        int reviewReplanStepStart,
        List<AgentStep> steps,
        AgentToolset toolset,
        List<LlmMessage> messages,
        IAgentProgressObserver? progress,
        CancellationToken cancellationToken,
        AgentCheckpoint checkpoint,
        AgentPlanRecoveryState planRecoveryState,
        AgentInventory? inventory)
    {
        AgentActionPreparationOutcome Outcome(
            AgentResult? result = null,
            bool continueIteration = false)
            => new(result, continueIteration, authorizedScope, excludedScope, reviewReplanStepStart);

        if (action.Action == AgentAction.ActionClarify)
        {
            var question = action.Reason ?? action.Content ?? "¿Qué resultado o alcance esperas?";
            checkpoint.NextAction = "esperar_aclaracion";
            checkpoint.LastDecision = "awaiting_user_input";
            checkpoint.LastAction = AgentAction.ActionClarify;
            checkpoint.LastResult = question;
            steps.Add(new AgentStep
            {
                Iteration = iteration,
                Action = AgentAction.ActionClarify,
                Success = true,
                ResultPreview = question,
                AtUtc = DateTime.UtcNow
            });
            progress?.Report(AgentProgress.Of(
                AgentPhase.Understanding,
                action: AgentAction.ActionClarify,
                iteration: iteration,
                message: question));
            return Outcome(new AgentResult
            {
                Success = false,
                AwaitingUserInput = true,
                Reason = question,
                Model = model,
                Objective = intention,
                Steps = steps,
                Checkpoint = checkpoint,
                Inventory = inventory
            });
        }

        if (action.Action == AgentAction.ActionPlan)
        {
            var planOutcome = await HandlePlanActionAsync(
                action,
                iteration,
                riskAssessment,
                workingDir,
                scopeObservationStartIndex,
                authorizedScope,
                excludedScope,
                steps,
                toolset,
                messages,
                progress,
                cancellationToken,
                checkpoint,
                reviewReplanStepStart,
                planRecoveryState).ConfigureAwait(true);
            authorizedScope = planOutcome.AuthorizedScope;
            excludedScope = planOutcome.ExcludedScope;
            reviewReplanStepStart = planOutcome.ReviewReplanStepStart;
            return Outcome(continueIteration: true);
        }

        var scopeGate = await ValidateModificationScopeAsync(
            action,
            iteration,
            intention,
            riskAssessment,
            workingDir,
            authorizedScope,
            excludedScope,
            steps,
            checkpoint,
            messages,
            progress,
            toolset,
            cancellationToken).ConfigureAwait(true);
        authorizedScope = scopeGate.AuthorizedScope;
        excludedScope = scopeGate.ExcludedScope;
        return Outcome(continueIteration: scopeGate.ContinueIteration);
    }

    private sealed record ReviewedAgentActionOutcome(
        AgentResult? Result,
        AgentAction? Action,
        AgentStep? Step,
        int Modifications,
        int ReviewReplanStepStart,
        bool RequiresIndependentReview,
        bool PlanBUsed,
        string? PlanBInitialFailure,
        bool AwaitingPlanBAction,
        int EffectiveMaxIterations);

    private async Task<ReviewedAgentActionOutcome> ExecuteApprovedAgentActionAsync(
        AgentAction action,
        int iteration,
        string profileId,
        string intention,
        string model,
        string workingDir,
        AgentToolset toolset,
        IAgentProgressObserver? progress,
        string requestedActionName,
        string? requestedActionPath,
        bool normalizedCombinedFileAction,
        bool normalizedRequestedRootHtmlPath,
        bool awaitingPlanBAction,
        Condor.Core.Evaluation.TaskRiskAssessment riskAssessment,
        bool requiresIndependentReview,
        bool requiresMultiAgentReview,
        AgentCheckpoint checkpoint,
        List<AgentStep> steps,
        List<LlmMessage> messages,
        CancellationToken cancellationToken,
        AgentInventory? inventory,
        int modifications,
        int reviewReplanStepStart,
        List<string>? authorizedScope,
        List<string>? excludedScope,
        AgentHarness harness,
        RepoSnapshot originalSnapshot,
        bool planBUsed,
        string? planBInitialFailure,
        int effectiveMaxIterations)
    {
        AgentStep? step = null;
        ReviewedAgentActionOutcome Outcome(AgentResult? result = null)
            => new(result, action, step, modifications, reviewReplanStepStart,
                requiresIndependentReview, planBUsed, planBInitialFailure,
                awaitingPlanBAction, effectiveMaxIterations);

        var approvalResult = await ValidateCriticalActionApprovalAsync(
            action, iteration, riskAssessment, intention, model, workingDir,
            checkpoint, steps, progress, inventory, cancellationToken).ConfigureAwait(true);
        if (approvalResult is not null)
            return Outcome(approvalResult);

        var reviewDecision = PrepareReviewDelegation(
            action, profileId, requiresIndependentReview, _delegationService is not null,
            requiresMultiAgentReview, checkpoint, steps, reviewReplanStepStart);
        if (reviewDecision.TerminalFailure is not null)
            return Outcome(Fail(reviewDecision.TerminalFailure, model, intention, steps, checkpoint));

        requiresIndependentReview = reviewDecision.RequiresIndependentReview;
        var execution = await ExecuteReviewedAgentActionAsync(
            action,
            iteration,
            workingDir,
            toolset,
            progress,
            reviewDecision.Failure,
            requestedActionName,
            requestedActionPath,
            normalizedCombinedFileAction,
            normalizedRequestedRootHtmlPath,
            awaitingPlanBAction,
            riskAssessment,
            reviewDecision.ReviewerDelegation,
            reviewDecision.Ready,
            checkpoint,
            steps,
            messages,
            cancellationToken,
            modifications,
            reviewReplanStepStart).ConfigureAwait(true);
        action = execution.Action;
        step = execution.Step;
        modifications = execution.Modifications;
        reviewReplanStepStart = execution.ReviewReplanStepStart;

        if (!step.Success && action.Action != AgentAction.ActionDelegate)
        {
            var planBFailure = StartPlanB(
                action.Action,
                action.Path,
                step.ResultPreview ?? "La herramienta no pudo completar la accion.",
                ref planBUsed,
                ref planBInitialFailure,
                ref awaitingPlanBAction,
                ref effectiveMaxIterations,
                iteration,
                messages,
                progress,
                checkpoint);
            if (planBFailure is not null)
                return Outcome(Fail(planBFailure, model, intention, steps, checkpoint));

            var htmlRecovery = await TryRepairRequestedHtmlLinkAsync(
                step.ResultPreview ?? "La herramienta fallo al ejecutar la accion.",
                iteration,
                riskAssessment,
                authorizedScope,
                excludedScope,
                intention,
                workingDir,
                steps,
                cancellationToken,
                planBUsed,
                planBInitialFailure,
                awaitingPlanBAction,
                modifications,
                checkpoint,
                progress,
                messages,
                harness,
                originalSnapshot,
                model);
            planBUsed = htmlRecovery.PlanBUsed;
            planBInitialFailure = htmlRecovery.PlanBInitialFailure;
            awaitingPlanBAction = htmlRecovery.AwaitingPlanBAction;
            modifications = htmlRecovery.Modifications;
            if (htmlRecovery.Result is not null)
                return Outcome(htmlRecovery.Result);
        }
        else if (awaitingPlanBAction && IsPlanBCorrectionAction(action.Action))
        {
            awaitingPlanBAction = false;
        }

        return Outcome();
    }

    private async Task<AgentDoneOutcome> VerifyModifiedActionAsync(
        int iteration,
        string intention,
        string model,
        string workingDir,
        AgentCheckpoint checkpoint,
        List<AgentStep> steps,
        List<LlmMessage> messages,
        IAgentProgressObserver? progress,
        CancellationToken cancellationToken,
        RepoSnapshot originalSnapshot,
        AgentHarness harness,
        Condor.Core.Evaluation.TaskRiskAssessment riskAssessment,
        List<string>? authorizedScope,
        List<string>? excludedScope,
        int modifications,
        string? lastDoneReason,
        bool planBUsed,
        string? planBInitialFailure,
        bool awaitingPlanBAction,
        int effectiveMaxIterations,
        bool requiresIndependentReview,
        bool requiresMultiAgentReview,
        Func<int, bool> applyPendingSteering)
    {
        AgentDoneOutcome Outcome(AgentResult? result = null, bool continueIteration = false)
            => new(result, continueIteration, modifications, lastDoneReason,
                planBUsed, planBInitialFailure, awaitingPlanBAction, effectiveMaxIterations);

        progress?.Report(AgentProgress.Of(AgentPhase.Verifying, action: "build/test", iteration: iteration));
        var verification = await harness.VerifyAsync(cancellationToken);
        var htmlRepair = planBUsed
            ? await harness.TryRepairMissingRequiredHtmlLinkAsync(verification.Reason, cancellationToken)
            : null;
        if (htmlRepair is not null)
        {
            var repairedHtmlPath = htmlRepair.Value.RelativePath;
            var repairAction = htmlRepair.Value.Action;
            planBUsed = true;
            planBInitialFailure ??= verification.Reason;
            awaitingPlanBAction = false;
            checkpoint.LastDecision = "plan_b_html_link_repaired";
            checkpoint.LastAction = repairAction + " " + repairedHtmlPath;
            checkpoint.LastResult = repairAction == AgentAction.ActionCreateFile
                ? "Plan B creó el archivo HTML solicitado; el harness repetirá la verificación."
                : "Plan B insertó el enlace HTML solicitado; el harness repetirá la verificación.";
            steps.Add(new AgentStep
            {
                Action = repairAction,
                Path = repairedHtmlPath,
                Success = true,
                ResultPreview = checkpoint.LastResult
            });
            progress?.Report(AgentProgress.Of(
                AgentPhase.Verifying,
                message: checkpoint.LastResult,
                action: repairAction,
                path: repairedHtmlPath,
                iteration: iteration));
            verification = await harness.VerifyAsync(cancellationToken);
        }

        checkpoint.HarnessState = !verification.ChecksApplicable
            ? "no_aplicable"
            : verification.Done ? "exito" : verification.Reason;
        checkpoint.LastError = verification.Done ? null : verification.Reason;
        if (verification.Done)
        {
            var successOutcome = HandleSuccessfulDoneVerification(
                verification, iteration, workingDir, originalSnapshot, checkpoint,
                planBUsed, planBInitialFailure, awaitingPlanBAction, effectiveMaxIterations,
                messages, progress, model, intention, steps, modifications, lastDoneReason,
                requiresIndependentReview, requiresMultiAgentReview, applyPendingSteering,
                finalizationRequested: false,
                recordIntegrityError: true);
            return successOutcome;
        }

        var failedHarnessStep = steps.LastOrDefault(s => !s.Success &&
            (s.Action.Equals(AgentAction.ActionBuild, StringComparison.OrdinalIgnoreCase) ||
             s.Action.Equals(AgentAction.ActionTest, StringComparison.OrdinalIgnoreCase)))
            ?? new AgentStep { Action = "harness", Success = false, ResultPreview = verification.Detail };
        var testFailureEvidence = (verification.Reason ?? "") + Environment.NewLine + verification.Detail;
        if (failedHarnessStep.Action == AgentAction.ActionTest &&
            ShouldReportExcludedBehaviorMismatch(intention, checkpoint, testFailureEvidence))
        {
            checkpoint.LastDecision = "test_fallo_con_alcance_excluido";
            checkpoint.NextAction = "informar_resultado";
            var testFailureTail = verification.Detail.Length > 2000
                ? "…" + verification.Detail[^2000..]
                : verification.Detail;
            var reason = "La prueba solicitada se ejecutó y falló al comparar el resultado observado con la expectativa. " +
                         "Condor conserva la prueba y no cambia archivos excluidos. " + (verification.Reason ?? "") + Environment.NewLine + testFailureTail;
            progress?.Report(AgentProgress.Of(
                AgentPhase.Finalizing,
                action: AgentAction.ActionTest,
                iteration: iteration,
                message: "La prueba falló contra el comportamiento observado; se conserva el test y no se modifica el código excluido."));
            return Outcome(Fail(reason, model, intention, steps, checkpoint));
        }

        var planBFailure = StartPlanB(
            failedHarnessStep.Action,
            failedHarnessStep.Path,
            (verification.Reason ?? "Build/test fallo.") + " " + verification.Detail,
            ref planBUsed,
            ref planBInitialFailure,
            ref awaitingPlanBAction,
            ref effectiveMaxIterations,
            iteration,
            messages,
            progress,
            checkpoint);
        if (planBFailure is not null)
            return Outcome(Fail(planBFailure, model, intention, steps, checkpoint));

        var repairOutcome = await TryRepairRequestedHtmlLinkAsync(
            (verification.Reason ?? "Build/test fallo.") + " " + verification.Detail,
            iteration,
            riskAssessment,
            authorizedScope,
            excludedScope,
            intention,
            workingDir,
            steps,
            cancellationToken,
            planBUsed,
            planBInitialFailure,
            awaitingPlanBAction,
            modifications,
            checkpoint,
            progress,
            messages,
            harness,
            originalSnapshot,
            model);
        planBUsed = repairOutcome.PlanBUsed;
        planBInitialFailure = repairOutcome.PlanBInitialFailure;
        awaitingPlanBAction = repairOutcome.AwaitingPlanBAction;
        modifications = repairOutcome.Modifications;
        return Outcome(repairOutcome.Result);
    }

    private static AgentDoneOutcome CompleteDoneWithoutModifications(
        AgentAction action,
        Condor.Core.Models.AgentProfile profile,
        AgentInventory? inventory,
        string model,
        string intention,
        List<AgentStep> steps,
        AgentCheckpoint checkpoint,
        IAgentProgressObserver? progress,
        List<LlmMessage> messages,
        string? lastDoneReason,
        int modifications,
        bool planBUsed,
        string? planBInitialFailure,
        bool awaitingPlanBAction,
        int effectiveMaxIterations)
    {
        AgentDoneOutcome Outcome(AgentResult? result = null, bool continueIteration = false)
            => new(result, continueIteration, modifications, lastDoneReason,
                planBUsed, planBInitialFailure, awaitingPlanBAction, effectiveMaxIterations);

        var (sufficient, hint) = AgentEngine.HasSufficientEvidenceForDone(intention, steps);
        if (!sufficient)
        {
            lastDoneReason = action.Reason ?? action.Content ?? lastDoneReason;
            messages.Add(new LlmMessage
            {
                Role = "user",
                Content = hint ?? "No tienes evidencia suficiente aun; observa o actua, y luego concluye."
            });
            return Outcome(continueIteration: true);
        }

        checkpoint.NextAction = "entregar";
        checkpoint.LastDecision = planBUsed ? "plan_b_corregido" : "describir";
        progress?.Report(AgentProgress.Of(AgentPhase.Finalizing, message: "Preparando respuesta"));
        var summary = action.Reason ?? action.Content ?? "";
        var isReviewProfile = profile.Id is "reviewer" or "security-reviewer";
        if (isReviewProfile)
        {
            summary += "\n\nHallazgos estructurados:\n" +
                (action.ReviewFindings!.Count == 0
                    ? "Sin hallazgos verificados."
                    : FormatReviewFindings(action.ReviewFindings));
        }

        var description = (planBUsed ? "Plan B recupero la tarea tras el fallo inicial. " : "") +
            (string.IsNullOrWhiteSpace(summary)
                ? "Condor observo el directorio y describio lo encontrado."
                : summary);
        return Outcome(new AgentResult
        {
            Success = true,
            Reason = description,
            Inventory = inventory,
            Model = model,
            Objective = intention,
            Steps = steps,
            Checkpoint = checkpoint,
            ReviewFindings = isReviewProfile ? action.ReviewFindings : null
        });
    }

    private sealed record ObservationOutcome(
        AgentResult? Result,
        bool ContinueIteration,
        int RedundantObservations,
        bool PlanBUsed,
        string? PlanBInitialFailure,
        bool AwaitingPlanBAction,
        int EffectiveMaxIterations);

    private async Task<ObservationOutcome> HandleObservationOutcomeAsync(
        AgentAction action,
        AgentStep step,
        int iteration,
        int redundantObservations,
        int modifications,
        string intention,
        string model,
        string workingDir,
        int scopeObservationStartIndex,
        List<AgentStep> steps,
        AgentToolset toolset,
        List<LlmMessage> messages,
        IAgentProgressObserver? progress,
        AgentCheckpoint checkpoint,
        CancellationToken cancellationToken,
        bool planBUsed,
        string? planBInitialFailure,
        bool awaitingPlanBAction,
        int effectiveMaxIterations,
        Func<string?, int, Task<AgentResult?>> repairHtml)
    {
        var observationAction = action.Action is AgentAction.ActionListDir or AgentAction.ActionReadFile or
            AgentAction.ActionSearch or AgentAction.ActionGitDiff or AgentAction.ActionGitLog or AgentAction.ActionGitShow;
        if (!step.Success || !observationAction)
        {
            return new ObservationOutcome(
                null, false, redundantObservations, planBUsed, planBInitialFailure,
                awaitingPlanBAction, effectiveMaxIterations);
        }

        var observationHistory = steps.Take(steps.Count - 1).Skip(scopeObservationStartIndex).ToList();
        var observationSignal = AgentEngine.AssessObservation(step, observationHistory);
        if (observationSignal != ObservationSignal.Redundant)
        {
            return new ObservationOutcome(null, false, 0, planBUsed, planBInitialFailure,
                awaitingPlanBAction, effectiveMaxIterations);
        }

        redundantObservations++;
        if (redundantObservations <= _limits.MaxRedundantObservations)
        {
            messages.Add(new LlmMessage
            {
                Role = "user",
                Content = "Ya observaste esto exactamente antes y el resultado no cambio; repetir la misma observacion no aporta informacion nueva. Si la solicitud es de comprension/analisis, sintetiza lo visto y responde con done. Si la tarea requiere un cambio, usa exactamente create_file para crear, edit_file para reemplazar un archivo existente o patch con original/replacement para un cambio quirurgico. No combines nombres con '/'; verifica con build o test cuando aplique."
            });
            return new ObservationOutcome(null, false, redundantObservations, planBUsed,
                planBInitialFailure, awaitingPlanBAction, effectiveMaxIterations);
        }

        if (modifications == 0 && IsInformationalRequest(intention))
        {
            checkpoint.NextAction = "entregar";
            checkpoint.LastDecision = planBUsed ? "plan_b_corregido" : "describir";
            var summary = BuildObservedSummary(steps, workingDir);
            var result = new AgentResult
            {
                Success = true,
                Reason = (planBUsed ? "Plan B recupero la tarea tras el fallo inicial. " : "") + summary,
                Model = model,
                Objective = intention,
                Steps = steps,
                Checkpoint = checkpoint
            };
            return new ObservationOutcome(result, false, redundantObservations, planBUsed,
                planBInitialFailure, awaitingPlanBAction, effectiveMaxIterations);
        }

        if (action.Action == AgentAction.ActionListDir &&
            await TryExploreExplicitDirectoryAfterRedundantListDirAsync(
                intention,
                workingDir,
                action.Path,
                steps,
                scopeObservationStartIndex,
                toolset,
                iteration,
                messages,
                progress,
                checkpoint,
                cancellationToken))
        {
            return new ObservationOutcome(null, true, 0, planBUsed, planBInitialFailure,
                awaitingPlanBAction, effectiveMaxIterations);
        }

        var lastRejectedPlan = steps.LastOrDefault(s =>
            s.Action.Equals(AgentAction.ActionPlan, StringComparison.OrdinalIgnoreCase) && !s.Success);
        checkpoint.LastError = "Observaciones redundantes sin informacion nueva y sin un cambio de codigo verificado." +
            (string.IsNullOrWhiteSpace(lastRejectedPlan?.ResultPreview)
                ? ""
                : " Ultimo plan rechazado: " + lastRejectedPlan.ResultPreview);
        var planBFailure = StartPlanB(
            action.Action,
            action.Path,
            checkpoint.LastError,
            ref planBUsed,
            ref planBInitialFailure,
            ref awaitingPlanBAction,
            ref effectiveMaxIterations,
            iteration,
            messages,
            progress,
            checkpoint);
        if (planBFailure is not null)
        {
            return new ObservationOutcome(
                Fail(planBFailure, model, intention, steps, checkpoint), false, redundantObservations,
                planBUsed, planBInitialFailure, awaitingPlanBAction, effectiveMaxIterations);
        }

        var htmlRecovery = await repairHtml(checkpoint.LastError, iteration);
        return new ObservationOutcome(
            htmlRecovery, htmlRecovery is null, redundantObservations,
            planBUsed, planBInitialFailure, awaitingPlanBAction, effectiveMaxIterations);
    }

    private sealed record RawAgentActionResult(AgentAction Action, AgentStep Step);

    private async Task<RawAgentActionResult> ExecuteAndRecoverHtmlActionAsync(
        AgentAction action,
        int iteration,
        string workingDir,
        AgentToolset toolset,
        IAgentProgressObserver? progress,
        string? reviewGateFailure,
        string requestedActionName,
        string? requestedActionPath,
        bool normalizedCombinedFileAction,
        bool normalizedRequestedRootHtmlPath,
        bool awaitingPlanBAction,
        Condor.Core.Evaluation.TaskRiskAssessment riskAssessment,
        List<AgentStep> steps,
        CancellationToken cancellationToken)
    {
        var step = reviewGateFailure is not null
            ? new AgentStep
            {
                Iteration = iteration,
                Action = AgentAction.ActionDelegate,
                Success = false,
                ResultPreview = reviewGateFailure,
                AtUtc = DateTime.UtcNow
            }
            : action.Action == AgentAction.ActionDone
            ? new AgentStep
            {
                Iteration = iteration,
                Action = action.Action,
                Success = true,
                ResultPreview = string.IsNullOrWhiteSpace(action.Reason)
                    ? "Condor recibió la solicitud de cierre y comprobará la evidencia."
                    : action.Reason,
                AtUtc = DateTime.UtcNow
            }
            : action.Action == AgentAction.ActionDelegate
            ? _delegationService is null
                ? new AgentStep
                {
                    Iteration = iteration,
                    Action = AgentAction.ActionDelegate,
                    Success = false,
                    ResultPreview = "La delegación no está habilitada en este agente.",
                    AtUtc = DateTime.UtcNow
                }
                : await _delegationService.ExecuteAsync(action, workingDir, iteration, progress, cancellationToken)
            : await toolset.ExecuteAsync(action, iteration, cancellationToken);

        if (normalizedCombinedFileAction || normalizedRequestedRootHtmlPath)
        {
            var normalization = normalizedRequestedRootHtmlPath
                ? "Condor corrigio la ruta '" + (requestedActionPath ?? "") + "' a la ruta raiz exacta '" + action.Path + "' solicitada. "
                : "Condor interpreto la accion compuesta '" + requestedActionName + "' como '" + action.Action + "'. ";
            step.ResultPreview = normalization + (step.ResultPreview ?? "");
        }
        else if (!step.Success && awaitingPlanBAction && action.Action != AgentAction.ActionDelegate)
        {
            var completeHtmlFallback = TryCreateCompleteHtmlEditFallback(action, workingDir, step.ResultPreview);
            if (completeHtmlFallback is not null && riskAssessment.Level != Condor.Core.Evaluation.TaskRiskLevel.Critical)
            {
                steps.Add(step);
                var fallbackStep = await toolset.ExecuteAsync(completeHtmlFallback, iteration, cancellationToken);
                fallbackStep.ResultPreview = "Plan B detecto que el original de patch no coincidia; como replacement contenia un documento HTML completo y la ruta era un archivo HTML de la raiz, aplico edit_file. " +
                                             (fallbackStep.ResultPreview ?? "");
                action = completeHtmlFallback;
                step = fallbackStep;
            }
        }

        return new RawAgentActionResult(action, step);
    }

    private sealed record AgentActionExecutionOutcome(
        AgentAction Action,
        AgentStep Step,
        int Modifications,
        int ReviewReplanStepStart);

    private async Task<AgentActionExecutionOutcome> ExecuteReviewedAgentActionAsync(
        AgentAction action,
        int iteration,
        string workingDir,
        AgentToolset toolset,
        IAgentProgressObserver? progress,
        string? reviewGateFailure,
        string requestedActionName,
        string? requestedActionPath,
        bool normalizedCombinedFileAction,
        bool normalizedRequestedRootHtmlPath,
        bool awaitingPlanBAction,
        Condor.Core.Evaluation.TaskRiskAssessment riskAssessment,
        bool reviewerDelegation,
        bool reviewDelegationReady,
        AgentCheckpoint checkpoint,
        List<AgentStep> steps,
        List<LlmMessage> messages,
        CancellationToken cancellationToken,
        int modifications,
        int reviewReplanStepStart)
    {
        progress?.Report(AgentProgress.Of(
            PhaseForAction(action.Action),
            action: action.Action == AgentAction.ActionDelegate ? "subagentes" : action.Action,
            path: action.Path,
            iteration: iteration));

        var execution = await ExecuteAndRecoverHtmlActionAsync(
            action,
            iteration,
            workingDir,
            toolset,
            progress,
            reviewGateFailure,
            requestedActionName,
            requestedActionPath,
            normalizedCombinedFileAction,
            normalizedRequestedRootHtmlPath,
            awaitingPlanBAction,
            riskAssessment,
            steps,
            cancellationToken);
        action = execution.Action;
        var step = execution.Step;

        steps.Add(step);
        if (reviewerDelegation && checkpoint.ReviewRoundsUsed == 3)
        {
            checkpoint.ReviewReplanRequired = true;
            checkpoint.ReviewReplanPrepared = false;
            reviewReplanStepStart = steps.Count;
            checkpoint.LastDecision = "limite_rondas_revision_replanificar";
            step.ResultPreview = (step.ResultPreview ?? "Ronda de revisión terminada.") +
                " Se alcanzaron tres rondas completas. Replantea el plan, realiza cambios nuevos y solicita como máximo una revisión delta limitada a esos cambios.";
        }
        checkpoint.LastAction = action.Action + " " + (action.Path ?? "");
        checkpoint.LastResult = Truncate(step.ResultPreview ?? "");
        if ((action.Action is AgentAction.ActionEditFile or AgentAction.ActionCreateFile or
            AgentAction.ActionPatch or AgentAction.ActionUndoFile) && step.Success)
        {
            modifications++;
            checkpoint.UnreviewedModifications++;
        }
        if (reviewDelegationReady && step.Success)
        {
            checkpoint.UnreviewedModifications = 0;
            checkpoint.LastDecision = "revision_independiente_completada";
        }

        messages.Add(new LlmMessage
        {
            Role = "user",
            Content = "Resultado de " + action.Action + "(" + (action.Path ?? "") + "):\n" +
                (step.Success ? (step.ResultPreview ?? "ok") : ("ERROR: " + (step.ResultPreview ?? "sin detalle")))
        });
        return new AgentActionExecutionOutcome(action, step, modifications, reviewReplanStepStart);
    }

    private async Task<AgentResult?> ValidateCriticalActionApprovalAsync(
        AgentAction action,
        int iteration,
        Condor.Core.Evaluation.TaskRiskAssessment riskAssessment,
        string intention,
        string model,
        string workingDir,
        AgentCheckpoint checkpoint,
        List<AgentStep> steps,
        IAgentProgressObserver? progress,
        AgentInventory? inventory,
        CancellationToken cancellationToken)
    {
        var actionRisk = Condor.Core.Evaluation.TaskRiskClassifier.Assess(intention, action.Path);
        if ((riskAssessment.Level != Condor.Core.Evaluation.TaskRiskLevel.Critical &&
             actionRisk.Level != Condor.Core.Evaluation.TaskRiskLevel.Critical) ||
            !IsModification(action.Action))
        {
            return null;
        }

        var approval = await RequestCriticalWriteApprovalAsync(
            action, iteration, workingDir, checkpoint, steps, progress, cancellationToken).ConfigureAwait(false);
        if (approval.Decision == ApprovalDecision.WaitingForUser)
        {
            return new AgentResult
            {
                Success = false,
                AwaitingUserInput = true,
                Reason = approval.Reason,
                Objective = intention,
                Model = model,
                Steps = steps,
                Checkpoint = checkpoint,
                Inventory = inventory
            };
        }

        if (approval.Decision != ApprovalDecision.Approved)
        {
            return Fail(
                approval.Reason ?? "Condor bloqueó la acción crítica por falta de aprobación humana.",
                model,
                intention,
                steps,
                checkpoint);
        }

        var currentCommit = await ReadCurrentGitCommitAsync(workingDir, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(approval.Digest, ComputeApprovalDigest(action, currentCommit), StringComparison.Ordinal))
        {
            return Fail(
                "La acción o el commit cambiaron después de la aprobación humana; Condor requiere una nueva aprobación.",
                model,
                intention,
                steps,
                checkpoint);
        }

        return null;
    }

    private sealed record ModificationScopeOutcome(
        bool ContinueIteration,
        List<string>? AuthorizedScope,
        List<string>? ExcludedScope);

    private static async Task<ModificationScopeOutcome> ValidateModificationScopeAsync(
        AgentAction action,
        int iteration,
        string intention,
        Condor.Core.Evaluation.TaskRiskAssessment riskAssessment,
        string workingDir,
        List<string>? authorizedScope,
        List<string>? excludedScope,
        List<AgentStep> steps,
        AgentCheckpoint checkpoint,
        List<LlmMessage> messages,
        IAgentProgressObserver? progress,
        AgentToolset toolset,
        CancellationToken cancellationToken)
    {
        if (action.Action is not (AgentAction.ActionCreateFile or AgentAction.ActionEditFile or
            AgentAction.ActionPatch or AgentAction.ActionUndoFile))
        {
            return new ModificationScopeOutcome(false, authorizedScope, excludedScope);
        }

        if (IsInformationalRequest(intention))
        {
            const string readOnlyQuery = "La solicitud es una consulta informativa; Condor permite solo lectura y bloqueó esta modificación.";
            steps.Add(new AgentStep
            {
                Iteration = iteration,
                Action = action.Action,
                Path = action.Path,
                Success = false,
                ResultPreview = readOnlyQuery,
                AtUtc = DateTime.UtcNow
            });
            checkpoint.NextAction = "responder_consulta";
            checkpoint.LastDecision = "escritura_bloqueada_consulta_informativa";
            checkpoint.LastAction = action.Action + " " + (action.Path ?? "");
            checkpoint.LastResult = readOnlyQuery;
            messages.Add(new LlmMessage
            {
                Role = "user",
                Content = readOnlyQuery + " Responde la pregunta con la evidencia ya observada, sin alterar archivos."
            });
            progress?.Report(AgentProgress.Of(
                AgentPhase.Analyzing,
                action: action.Action,
                path: action.Path,
                iteration: iteration,
                message: readOnlyQuery,
                flag: ProgressFlag.Recovering));
            return new ModificationScopeOutcome(true, authorizedScope, excludedScope);
        }

        if (authorizedScope is null)
        {
            if (TryAuthorizeExplicitSingleFileScope(intention, action.Path, riskAssessment.Level, out var explicitScope))
            {
                authorizedScope = new List<string> { explicitScope };
                excludedScope = new List<string>();
                checkpoint.DeclaredScope = authorizedScope.ToList();
                checkpoint.ExcludedScope = new List<string>();
                if (!checkpoint.ReviewReplanRequired && checkpoint.PlanSteps is not { Count: > 0 } &&
                    action.Action is AgentAction.ActionCreateFile or AgentAction.ActionEditFile or AgentAction.ActionPatch)
                {
                    excludedScope = FindExplicitlyExcludedFilePaths(intention, explicitScope);
                    checkpoint.ExcludedScope = excludedScope.ToList();
                    RegisterExplicitSingleFilePlan(
                        explicitScope,
                        excludedScope,
                        riskAssessment.Level,
                        iteration,
                        steps,
                        checkpoint,
                        progress);
                }
                else
                {
                    checkpoint.LastDecision = "alcance_implicito_archivo_explicito";
                    checkpoint.NextAction = "ejecutar_cambio_acotado";
                }
            }
            else
            {
                var noPlan = "Condor bloqueó la escritura: esta tarea requiere un plan explícito con objetivo, alcance y comprobaciones.";
                steps.Add(new AgentStep
                {
                    Iteration = iteration,
                    Action = action.Action,
                    Path = action.Path,
                    Success = false,
                    ResultPreview = noPlan,
                    AtUtc = DateTime.UtcNow
                });
                checkpoint.NextAction = "declarar_plan";
                checkpoint.LastDecision = "escritura_bloqueada_sin_plan";
                checkpoint.LastAction = action.Action + " " + action.Path;
                messages.Add(new LlmMessage
                {
                    Role = "user",
                    Content = noPlan + " Explora lo necesario y devuelve primero la acción plan."
                });
                progress?.Report(AgentProgress.Of(
                    AgentPhase.Analyzing,
                    action: action.Action,
                    path: action.Path,
                    iteration: iteration,
                    message: noPlan,
                    flag: ProgressFlag.Recovering));
                return new ModificationScopeOutcome(true, authorizedScope, excludedScope);
            }
        }

        if (action.Path is not null && !HasExploredScope(action.Path, workingDir, steps))
        {
            var fullPath = Path.GetFullPath(Path.Combine(
                workingDir,
                action.Path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar)));
            if (File.Exists(fullPath))
            {
                const string readBeforeEdit = "Antes de cambiar un archivo existente, léelo con read_file para preservar el contenido y confirmar el estado actual.";
                steps.Add(new AgentStep
                {
                    Iteration = iteration,
                    Action = action.Action,
                    Path = action.Path,
                    Success = false,
                    ResultPreview = readBeforeEdit,
                    AtUtc = DateTime.UtcNow
                });
                checkpoint.LastDecision = "lectura_previa_requerida";
                messages.Add(new LlmMessage { Role = "user", Content = readBeforeEdit });
                progress?.Report(AgentProgress.Of(
                    AgentPhase.Observing,
                    action: AgentAction.ActionReadFile,
                    path: action.Path,
                    iteration: iteration,
                    message: readBeforeEdit,
                    flag: ProgressFlag.Recovering));
                return new ModificationScopeOutcome(true, authorizedScope, excludedScope);
            }

            var parent = NormalizeRel(Path.GetDirectoryName(action.Path.Replace('\\', '/')) ?? ".");
            var directoryStep = await toolset.ExecuteAsync(
                new AgentAction
                {
                    Action = AgentAction.ActionListDir,
                    Path = string.IsNullOrWhiteSpace(parent) ? "." : parent
                },
                iteration,
                cancellationToken);
            steps.Add(directoryStep);
            messages.Add(new LlmMessage
            {
                Role = "user",
                Content = "Exploración previa del directorio " + (directoryStep.Path ?? parent) + ":\n" +
                    (directoryStep.Success ? directoryStep.ResultPreview : "ERROR: " + directoryStep.ResultPreview)
            });
            if (!directoryStep.Success)
            {
                checkpoint.LastDecision = "exploracion_previa_fallida";
                return new ModificationScopeOutcome(true, authorizedScope, excludedScope);
            }
        }

        if (!AgentEngine.IsPathWithinScope(action.Path, authorizedScope, excludedScope))
        {
            var outOfScope = "Condor bloqueó la escritura fuera del alcance declarado (" +
                (action.Path ?? "ruta vacía") + "). Actualiza el plan antes de modificar esa ruta.";
            steps.Add(new AgentStep
            {
                Iteration = iteration,
                Action = action.Action,
                Path = action.Path,
                Success = false,
                ResultPreview = outOfScope,
                AtUtc = DateTime.UtcNow
            });
            checkpoint.NextAction = "actualizar_plan";
            checkpoint.LastDecision = "escritura_bloqueada_fuera_de_alcance";
            checkpoint.LastAction = action.Action + " " + action.Path;
            messages.Add(new LlmMessage { Role = "user", Content = outOfScope });
            progress?.Report(AgentProgress.Of(
                AgentPhase.Analyzing,
                action: action.Action,
                path: action.Path,
                iteration: iteration,
                message: outOfScope,
                flag: ProgressFlag.Recovering));
            return new ModificationScopeOutcome(true, authorizedScope, excludedScope);
        }

        return new ModificationScopeOutcome(false, authorizedScope, excludedScope);
    }

    private sealed record AgentPlanActionOutcome(
        List<string>? AuthorizedScope,
        List<string>? ExcludedScope,
        int ReviewReplanStepStart);

    private static async Task<AgentPlanActionOutcome> HandlePlanActionAsync(
        AgentAction action,
        int iteration,
        Condor.Core.Evaluation.TaskRiskAssessment riskAssessment,
        string workingDir,
        int scopeObservationStartIndex,
        List<string>? currentAuthorizedScope,
        List<string>? currentExcludedScope,
        List<AgentStep> steps,
        AgentToolset toolset,
        List<LlmMessage> messages,
        IAgentProgressObserver? progress,
        CancellationToken cancellationToken,
        AgentCheckpoint checkpoint,
        int reviewReplanStepStart,
        AgentPlanRecoveryState planRecoveryState)
    {
        var planSteps = action.PlanSteps!;
        var expectedResults = action.ExpectedResults!;
        var verificationSteps = action.VerificationSteps!;
        var planScope = action.ScopePaths!;
        var requiredChecks = riskAssessment.Level switch
        {
            Condor.Core.Evaluation.TaskRiskLevel.Critical => 3,
            Condor.Core.Evaluation.TaskRiskLevel.High => 2,
            _ => 1
        };
        var requiredSteps = requiredChecks;
        var distinctCheckCount = verificationSteps.Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var distinctStepCount = planSteps.Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var broadCriticalScope = riskAssessment.Level == Condor.Core.Evaluation.TaskRiskLevel.Critical &&
            planScope.Any(path => path == "." || path.EndsWith("/**", StringComparison.Ordinal));
        var unexploredScope = await ExploreUnobservedPlanScopeAsync(
            planScope,
            workingDir,
            scopeObservationStartIndex,
            steps,
            toolset,
            messages,
            progress,
            iteration,
            cancellationToken);

        if (distinctCheckCount < requiredChecks || distinctStepCount < requiredSteps ||
            broadCriticalScope || unexploredScope is not null)
        {
            var planGap = broadCriticalScope
                ? "La tarea crítica necesita rutas concretas; no se permite autorizar la raíz o un comodín de directorio."
                : unexploredScope is not null
                    ? "El alcance " + unexploredScope + " aún no se exploró. Lee el archivo o lista su directorio antes de declarar el plan."
                    : "El nivel de riesgo " + riskAssessment.Level.ToString().ToLowerInvariant() + " requiere al menos " + requiredSteps + " pasos y " + requiredChecks + " comprobaciones distintas.";
            steps.Add(new AgentStep
            {
                Iteration = iteration,
                Action = AgentAction.ActionPlan,
                Success = false,
                ResultPreview = planGap,
                AtUtc = DateTime.UtcNow
            });
            checkpoint.LastDecision = "plan_insuficiente_para_riesgo";
            messages.Add(new LlmMessage
            {
                Role = "user",
                Content = BuildPlanGapCorrection(planGap, broadCriticalScope, unexploredScope, requiredSteps)
            });
            progress?.Report(AgentProgress.Of(
                AgentPhase.Analyzing,
                action: AgentAction.ActionPlan,
                iteration: iteration,
                message: planGap,
                flag: ProgressFlag.Recovering));
            return new AgentPlanActionOutcome(currentAuthorizedScope, currentExcludedScope, reviewReplanStepStart);
        }

        planRecoveryState.RegisterAcceptedPlan(BuildInvalidPlanFingerprint(action, "plan_aceptado"));
        var authorizedScope = action.ScopePaths!.ToList();
        var excludedScope = action.ExcludedPaths?.ToList() ?? new List<string>();
        checkpoint.DeclaredScope = authorizedScope.ToList();
        checkpoint.ExcludedScope = excludedScope.ToList();
        checkpoint.PlanSteps = planSteps.ToList();
        checkpoint.ExpectedResults = expectedResults.ToList();
        checkpoint.VerificationSteps = verificationSteps.ToList();
        checkpoint.LastDecision = "plan_declarado";
        checkpoint.LastAction = AgentAction.ActionPlan;
        checkpoint.LastResult = action.Objective;
        checkpoint.NextAction = "ejecutar_plan";
        var planSummary = "Plan previo · riesgo " + riskAssessment.Level.ToString().ToLowerInvariant() +
            " · objetivo: " + action.Objective +
            " · alcance: " + string.Join(", ", authorizedScope) +
            (excludedScope.Count == 0 ? "" : " · excluye: " + string.Join(", ", excludedScope)) +
            " · tareas, resultados y comprobaciones (en orden): " + string.Join(" | ", planSteps.Select((task, index) =>
                (index + 1) + ". " + task + " → resultado esperado: " + expectedResults[index] + "; comprobar: " + verificationSteps[index])) +
            (checkpoint.ReviewReplanRequired
                ? " · próxima revisión limitada a cambios nuevos posteriores a este plan"
                : "");
        steps.Add(new AgentStep
        {
            Iteration = iteration,
            Action = AgentAction.ActionPlan,
            Success = true,
            ResultPreview = planSummary,
            AtUtc = DateTime.UtcNow
        });
        if (checkpoint.ReviewReplanRequired)
        {
            checkpoint.ReviewReplanPrepared = true;
            reviewReplanStepStart = steps.Count;
            checkpoint.LastDecision = "plan_revision_delta";
        }

        progress?.Report(AgentProgress.Of(
            AgentPhase.Analyzing,
            action: AgentAction.ActionPlan,
            iteration: iteration,
            message: planSummary));
        messages.Add(new LlmMessage
        {
            Role = "user",
            Content = "Plan registrado por Condor. Riesgo mínimo: " + riskAssessment.Level.ToString().ToLowerInvariant() +
                ". Alcance autorizado: " + string.Join(", ", authorizedScope) +
                ". Excluidos: " + (excludedScope.Count == 0 ? "ninguno declarado" : string.Join(", ", excludedScope)) +
                ". Ejecuta las tareas en orden, una a la vez; alcanza el resultado esperado y completa la comprobación asociada antes de empezar la siguiente. Respeta el alcance y actualiza el plan si necesitas incluir otra ruta."
        });
        return new AgentPlanActionOutcome(authorizedScope, excludedScope, reviewReplanStepStart);
    }

    private sealed record AgentActionAdmissionOutcome(
        AgentResult? Result,
        bool ContinueIteration,
        bool PlanBUsed,
        string? PlanBInitialFailure,
        bool AwaitingPlanBAction,
        int EffectiveMaxIterations);

    private async Task<AgentActionAdmissionOutcome> AdmitAgentActionAsync(
        AgentAction action,
        int iteration,
        Condor.Core.Models.AgentProfile profile,
        string model,
        string intention,
        Condor.Core.Evaluation.TaskRiskLevel riskLevel,
        List<AgentStep> steps,
        AgentCheckpoint checkpoint,
        List<LlmMessage> messages,
        IAgentProgressObserver? progress,
        bool planBUsed,
        string? planBInitialFailure,
        bool awaitingPlanBAction,
        int effectiveMaxIterations,
        AgentPlanRecoveryState planRecoveryState,
        Func<string?, int, Task<AgentResult?>> repairHtml)
    {
        if (!string.Equals(action.Action, AgentAction.ActionPlan, StringComparison.OrdinalIgnoreCase))
            planRecoveryState.ResetInvalidAttempts();

        if ((AgentEngine.SupportedActions.Contains(action.Action) && !profile.AllowedActions.Contains(action.Action)) ||
            (action.Action == AgentAction.ActionDelegate && _delegationService is null))
        {
            var profileFailure = "El perfil '" + profile.Id + "' no autoriza la acción '" + action.Action + "'.";
            steps.Add(new AgentStep
            {
                Iteration = iteration,
                Action = action.Action,
                Path = action.Path,
                Success = false,
                ResultPreview = profileFailure,
                AtUtc = DateTime.UtcNow
            });
            return new AgentActionAdmissionOutcome(
                Fail(profileFailure, model, intention, steps, checkpoint), false,
                planBUsed, planBInitialFailure, awaitingPlanBAction, effectiveMaxIterations);
        }

        var recoveredExplicitScope = RecoverExplicitPlanScope(
            action, intention, checkpoint.WorkingDirectory, riskLevel, steps);

        var validation = AgentEngine.ValidateAction(action);
        if (!validation.Valid)
        {
            var validationFailure = validation.Reason ?? "La accion no cumple el contrato.";
            steps.Add(new AgentStep
            {
                Iteration = iteration,
                Action = action.Action,
                Path = action.Path,
                Success = false,
                ResultPreview = validationFailure,
                AtUtc = DateTime.UtcNow
            });
            if (action.Action == AgentAction.ActionPlan)
            {
                var fingerprint = BuildInvalidPlanFingerprint(action, validationFailure);
                if (!planRecoveryState.TryRegisterInvalidPlan(fingerprint))
                {
                    checkpoint.LastError = "El modelo repitió el mismo plan inválido después de recibir una corrección. Condor detuvo la tarea sin ejecutar cambios.";
                    checkpoint.LastDecision = "plan_rechazado_repetido";
                    checkpoint.NextAction = "detener";
                    progress?.Report(AgentProgress.Of(
                        AgentPhase.Finalizing,
                        action: AgentAction.ActionPlan,
                        iteration: iteration,
                        message: checkpoint.LastError));
                    return new AgentActionAdmissionOutcome(
                        Fail(checkpoint.LastError, model, intention, steps, checkpoint), false,
                        planBUsed, planBInitialFailure, awaitingPlanBAction, effectiveMaxIterations);
                }

                checkpoint.LastError = validationFailure;
                checkpoint.LastDecision = "plan_rechazado_por_validacion";
                checkpoint.NextAction = "corregir_plan";
                var planCorrection = BuildInvalidPlanCorrection(action, validationFailure);
                messages.Add(new LlmMessage { Role = "user", Content = validationFailure + " " + planCorrection });
                progress?.Report(AgentProgress.Of(
                    AgentPhase.Analyzing,
                    action: AgentAction.ActionPlan,
                    iteration: iteration,
                    message: validationFailure,
                    flag: ProgressFlag.Recovering));
                return new AgentActionAdmissionOutcome(
                    null, true, planBUsed, planBInitialFailure, awaitingPlanBAction, effectiveMaxIterations);
            }

            var planBFailure = StartPlanB(
                action.Action,
                action.Path,
                validationFailure,
                ref planBUsed,
                ref planBInitialFailure,
                ref awaitingPlanBAction,
                ref effectiveMaxIterations,
                iteration,
                messages,
                progress,
                checkpoint);
            if (planBFailure is not null)
            {
                return new AgentActionAdmissionOutcome(
                    Fail(planBFailure, model, intention, steps, checkpoint), false,
                    planBUsed, planBInitialFailure, awaitingPlanBAction, effectiveMaxIterations);
            }

            var htmlRecovery = await repairHtml(validationFailure, iteration);
            return new AgentActionAdmissionOutcome(
                htmlRecovery, htmlRecovery is null,
                planBUsed, planBInitialFailure, awaitingPlanBAction, effectiveMaxIterations);
        }

        RecordRecoveredPlanScope(recoveredExplicitScope, iteration, steps, progress);

        if (action.Action == AgentAction.ActionPlan)
        {
            var acceptedPlanFingerprint = BuildInvalidPlanFingerprint(action, "plan_aceptado");
            if (checkpoint.ReviewReplanRequired)
                planRecoveryState.ResetAcceptedPlan();

            if (planRecoveryState.TryRegisterRepeatedAcceptedPlan(acceptedPlanFingerprint, out var repeatCount))
            {
                var repeatedPlanFailure = "El modelo volvió a entregar un plan ya registrado sin ejecutar sus tareas.";
                if (repeatCount >= 2)
                {
                    checkpoint.LastError = repeatedPlanFailure + " Condor detuvo la tarea sin ejecutar cambios.";
                    checkpoint.LastDecision = "plan_repetido_detencion";
                    checkpoint.NextAction = "detener";
                    steps.Add(new AgentStep
                    {
                        Iteration = iteration,
                        Action = AgentAction.ActionPlan,
                        Success = false,
                        ResultPreview = checkpoint.LastError,
                        AtUtc = DateTime.UtcNow
                    });
                    progress?.Report(AgentProgress.Of(
                        AgentPhase.Finalizing,
                        action: AgentAction.ActionPlan,
                        iteration: iteration,
                        message: checkpoint.LastError));
                    return new AgentActionAdmissionOutcome(
                        Fail(checkpoint.LastError, model, intention, steps, checkpoint), false,
                        planBUsed, planBInitialFailure, awaitingPlanBAction, effectiveMaxIterations);
                }

                checkpoint.LastDecision = "plan_repetido_correccion";
                checkpoint.NextAction = "ejecutar_plan";
                steps.Add(new AgentStep
                {
                    Iteration = iteration,
                    Action = AgentAction.ActionPlan,
                    Success = false,
                    ResultPreview = repeatedPlanFailure + " Se solicitó ejecutar la primera tarea.",
                    AtUtc = DateTime.UtcNow
                });
                messages.Add(new LlmMessage
                {
                    Role = "user",
                    Content = "Ese plan ya está registrado por Condor. No lo vuelvas a devolver ni lo reformules. Ejecuta ahora la primera tarea del plan registrado y después realiza su comprobación."
                });
                progress?.Report(AgentProgress.Of(
                    AgentPhase.Analyzing,
                    action: AgentAction.ActionPlan,
                    iteration: iteration,
                    message: repeatedPlanFailure,
                    flag: ProgressFlag.Recovering));
                return new AgentActionAdmissionOutcome(
                    null, true, planBUsed, planBInitialFailure, awaitingPlanBAction, effectiveMaxIterations);
            }
        }

        if (action.Action == AgentAction.ActionDone && awaitingPlanBAction)
        {
            var agentRefusal = action.Reason ?? action.Content;
            var earlyDoneFailure = "El agente intento terminar sin ejecutar una accion de recuperacion tras el fallo." +
                (string.IsNullOrWhiteSpace(agentRefusal) ? "" : " Motivo del agente: " + agentRefusal);
            steps.Add(new AgentStep
            {
                Iteration = iteration,
                Action = action.Action,
                Success = false,
                ResultPreview = earlyDoneFailure,
                AtUtc = DateTime.UtcNow
            });
            var planBFailure = StartPlanB(
                action.Action,
                action.Path,
                earlyDoneFailure,
                ref planBUsed,
                ref planBInitialFailure,
                ref awaitingPlanBAction,
                ref effectiveMaxIterations,
                iteration,
                messages,
                progress,
                checkpoint);
            if (planBFailure is not null)
            {
                return new AgentActionAdmissionOutcome(
                    Fail(planBFailure, model, intention, steps, checkpoint), false,
                    planBUsed, planBInitialFailure, awaitingPlanBAction, effectiveMaxIterations);
            }

            var htmlRecovery = await repairHtml(earlyDoneFailure, iteration);
            return new AgentActionAdmissionOutcome(
                htmlRecovery, htmlRecovery is null,
                planBUsed, planBInitialFailure, awaitingPlanBAction, effectiveMaxIterations);
        }

        return new AgentActionAdmissionOutcome(
            null, false, planBUsed, planBInitialFailure, awaitingPlanBAction, effectiveMaxIterations);
    }

    private sealed record NullActionOutcome(
        AgentResult? Result,
        bool PlanBUsed,
        string? PlanBInitialFailure,
        bool AwaitingPlanBAction,
        int EffectiveMaxIterations);

    private async Task<NullActionOutcome> HandleNullActionAsync(
        AgentModelCall call,
        int iteration,
        string model,
        string intention,
        AgentInventory? inventory,
        AgentToolset toolset,
        string workingDir,
        List<AgentStep> steps,
        AgentCheckpoint checkpoint,
        List<LlmMessage> messages,
        IAgentProgressObserver? progress,
        CancellationToken cancellationToken,
        bool planBUsed,
        string? planBInitialFailure,
        bool awaitingPlanBAction,
        int effectiveMaxIterations,
        AgentPlanRecoveryState planRecoveryState,
        Func<string?, int, Task<AgentResult?>> repairHtml)
    {
        var protocolFailure = call.Error ?? "El modelo no produjo una accion JSON valida.";
        steps.Add(new AgentStep
        {
            Iteration = iteration,
            Action = "respuesta_estructurada",
            Success = false,
            ResultPreview = protocolFailure,
            AtUtc = DateTime.UtcNow
        });

        if (planRecoveryState.RegisterInvalidStructuredResponse() == 1)
        {
            effectiveMaxIterations = Math.Max(effectiveMaxIterations, iteration + 1);
            checkpoint.LastError = protocolFailure;
            checkpoint.LastDecision = "corregir_respuesta_estructurada";
            checkpoint.NextAction = "reintentar_json";
            messages.Add(new LlmMessage
            {
                Role = "user",
                Content = "La respuesta anterior no produjo una acción JSON válida. Reintenta una sola vez y responde únicamente con un objeto JSON válido de una acción permitida, sin Markdown, comentarios ni texto externo. Si vas a modificar archivos, primero registra el plan con el esquema indicado por el sistema; para continuar observando, usa read_file o list_dir."
            });
            progress?.Report(AgentProgress.Of(
                AgentPhase.Analyzing,
                action: "respuesta_estructurada",
                iteration: iteration,
                message: "La respuesta no cumplió el formato; Condor solicita una corrección estructurada.",
                flag: ProgressFlag.Recovering));
            return new NullActionOutcome(null, planBUsed, planBInitialFailure, awaitingPlanBAction, effectiveMaxIterations);
        }

        if (!IsInformationalRequest(intention))
        {
            checkpoint.LastError = "El modelo repitió una respuesta estructurada inválida después de una corrección. Condor detuvo la tarea sin ejecutar esa respuesta.";
            checkpoint.LastDecision = "respuesta_estructurada_repetida";
            checkpoint.NextAction = "detener";
            progress?.Report(AgentProgress.Of(
                AgentPhase.Finalizing,
                action: "respuesta_estructurada",
                iteration: iteration,
                message: checkpoint.LastError));
            return new NullActionOutcome(
                Fail(checkpoint.LastError, model, intention, steps, checkpoint),
                planBUsed,
                planBInitialFailure,
                awaitingPlanBAction,
                effectiveMaxIterations);
        }

        if (IsInformationalRequest(intention))
        {
            checkpoint.LastDecision = "plan_b_describir";
            checkpoint.LastError = protocolFailure;
            checkpoint.NextAction = "observar_y_describir";
            progress?.Report(AgentProgress.Of(
                AgentPhase.Analyzing,
                action: "Plan B",
                iteration: iteration,
                message: "Plan B: la respuesta no fue estructurada; Condor observara el proyecto y resumira evidencia real."));
            var grounded = await GroundInformationalAsync(toolset, workingDir, steps, cancellationToken);
            var result = grounded is null
                ? Fail("Plan B no pudo obtener evidencia suficiente para describir el proyecto.", model, intention, steps, checkpoint)
                : new AgentResult
                {
                    Success = true,
                    Reason = "Plan B entrego una descripcion basada en archivos observados por Condor. " + grounded,
                    Model = model,
                    Objective = intention,
                    Steps = steps,
                    Checkpoint = checkpoint,
                    Inventory = inventory
                };
            return new NullActionOutcome(result, planBUsed, planBInitialFailure, awaitingPlanBAction, effectiveMaxIterations);
        }

        var planBFailure = StartPlanB(
            "respuesta_estructurada",
            null,
            protocolFailure,
            ref planBUsed,
            ref planBInitialFailure,
            ref awaitingPlanBAction,
            ref effectiveMaxIterations,
            iteration,
            messages,
            progress,
            checkpoint);
        if (planBFailure is not null)
        {
            return new NullActionOutcome(
                Fail(planBFailure, model, intention, steps, checkpoint),
                planBUsed,
                planBInitialFailure,
                awaitingPlanBAction,
                effectiveMaxIterations);
        }

        var htmlRecovery = await repairHtml(protocolFailure, iteration);
        return new NullActionOutcome(
            htmlRecovery,
            planBUsed,
            planBInitialFailure,
            awaitingPlanBAction,
            effectiveMaxIterations);
    }

    private sealed record PlanBHtmlRepairOutcome(
        AgentResult? Result,
        bool PlanBUsed,
        string? PlanBInitialFailure,
        bool AwaitingPlanBAction,
        int Modifications);

    private async Task<PlanBHtmlRepairOutcome> TryRepairRequestedHtmlLinkAsync(
        string? failureReason,
        int currentIteration,
        Condor.Core.Evaluation.TaskRiskAssessment riskAssessment,
        List<string>? authorizedScope,
        List<string>? excludedScope,
        string intention,
        string workingDir,
        List<AgentStep> steps,
        CancellationToken cancellationToken,
        bool planBUsed,
        string? planBInitialFailure,
        bool awaitingPlanBAction,
        int modifications,
        AgentCheckpoint checkpoint,
        IAgentProgressObserver? progress,
        List<LlmMessage> messages,
        AgentHarness harness,
        RepoSnapshot originalSnapshot,
        string model)
    {
        if (riskAssessment.Level == Condor.Core.Evaluation.TaskRiskLevel.Critical || authorizedScope is null)
        {
            return new PlanBHtmlRepairOutcome(null, planBUsed, planBInitialFailure, awaitingPlanBAction, modifications);
        }

        var requestedRootHtml = AgentHarness.FindRequestedRootHtmlFile(intention);
        var scopedRepairSteps = steps.Where(step =>
            (step.Action is AgentAction.ActionCreateFile or AgentAction.ActionEditFile or AgentAction.ActionPatch) &&
            AgentEngine.IsPathWithinScope(step.Path, authorizedScope, excludedScope)).ToList();
        var scopedHarness = new AgentHarness(workingDir, _limits, scopedRepairSteps, intention);
        var repair = await scopedHarness.TryRepairMissingRequiredHtmlLinkAsync(
            failureReason,
            cancellationToken,
            allowRequestedRootHtmlFile: requestedRootHtml is not null &&
                AgentEngine.IsPathWithinScope(requestedRootHtml, authorizedScope, excludedScope));
        if (repair is null)
        {
            return new PlanBHtmlRepairOutcome(null, planBUsed, planBInitialFailure, awaitingPlanBAction, modifications);
        }

        var repairedPath = repair.Value.RelativePath;
        var repairAction = repair.Value.Action;
        planBUsed = true;
        planBInitialFailure ??= failureReason;
        awaitingPlanBAction = false;
        modifications++;
        var repairReason = repairAction == AgentAction.ActionCreateFile
            ? "Plan B creó el archivo HTML raíz solicitado con el enlace a documentación; el harness verificará el resultado."
            : "Plan B insertó el enlace HTML solicitado y el harness verificará el resultado.";
        steps.Add(new AgentStep
        {
            Iteration = currentIteration,
            Action = repairAction,
            Path = repairedPath,
            Success = true,
            ResultPreview = repairReason,
            AtUtc = DateTime.UtcNow
        });
        checkpoint.LastDecision = "plan_b_html_link_repaired";
        checkpoint.LastAction = AgentAction.ActionEditFile + " " + repairedPath;
        checkpoint.LastResult = repairReason;
        progress?.Report(AgentProgress.Of(
            AgentPhase.Verifying,
            message: repairReason,
            action: repairAction,
            path: repairedPath,
            iteration: currentIteration));

        var verification = await harness.VerifyAsync(cancellationToken);
        checkpoint.HarnessState = !verification.ChecksApplicable
            ? "no_aplicable"
            : verification.Done ? "exito" : verification.Reason;
        checkpoint.LastError = verification.Done ? null : verification.Reason;
        messages.Add(new LlmMessage
        {
            Role = "user",
            Content = repairReason + " Resultado del harness: " +
                      (verification.Done ? (verification.Detail ?? "verificado") : (verification.Reason ?? "fallo sin detalle"))
        });
        if (!verification.Done)
        {
            return new PlanBHtmlRepairOutcome(null, planBUsed, planBInitialFailure, awaitingPlanBAction, modifications);
        }

        var integrity = VerifyTestIntegrity(workingDir, originalSnapshot, checkpoint.DeclaredScope, checkpoint.ExcludedScope);
        if (integrity is not null)
        {
            checkpoint.LastDecision = "revisar";
            checkpoint.LastError = integrity;
            return new PlanBHtmlRepairOutcome(
                Fail(integrity, model, intention, steps, checkpoint), planBUsed, planBInitialFailure, awaitingPlanBAction, modifications);
        }

        checkpoint.NextAction = "entregar";
        checkpoint.LastDecision = "plan_b_corregido";
        var completionReason = HarnessCompletionReason(verification, planBUsed);
        progress?.Report(AgentProgress.Of(AgentPhase.Finalizing, message: completionReason));
        var result = new AgentResult
        {
            Success = true,
            Reason = completionReason,
            Model = model,
            Objective = intention,
            Steps = steps,
            Checkpoint = checkpoint
        };
        return new PlanBHtmlRepairOutcome(result, planBUsed, planBInitialFailure, awaitingPlanBAction, modifications);
    }

    private sealed record PendingSteeringOutcome(
        bool Changed,
        string Intention,
        Condor.Core.Evaluation.TaskRiskAssessment RiskAssessment,
        List<string>? AuthorizedScope,
        List<string>? ExcludedScope,
        int ScopeObservationStartIndex);

    private static PendingSteeringOutcome ApplySteeringChanges(
        int currentIteration,
        string intention,
        Condor.Core.Evaluation.TaskRiskAssessment riskAssessment,
        List<string>? authorizedScope,
        List<string>? excludedScope,
        int scopeObservationStartIndex,
        List<AgentStep> steps,
        AgentCheckpoint checkpoint,
        List<LlmMessage> messages,
        IAgentProgressObserver? progress,
        IAgentSteeringChannel? steeringChannel)
    {
        var changed = false;
        while (steeringChannel?.TryDequeue(out var steering) == true)
        {
            if (string.IsNullOrWhiteSpace(steering))
            {
                continue;
            }

            changed = true;
            intention += "\nRequisito agregado durante la ejecución: " + steering;
            checkpoint.Task = intention;
            authorizedScope = null;
            excludedScope = null;
            scopeObservationStartIndex = steps.Count;
            var updatedRisk = Condor.Core.Evaluation.TaskRiskClassifier.Assess(steering);
            if (updatedRisk.Level > riskAssessment.Level)
            {
                riskAssessment = updatedRisk;
            }

            checkpoint.RiskLevel = riskAssessment.Level.ToString();
            checkpoint.NextAction = "actualizar_plan";
            checkpoint.LastDecision = "requisito_actualizado";
            messages.Add(new LlmMessage
            {
                Role = "user",
                Content = "El usuario agregó un requisito durante la tarea: " + steering +
                    ". El plan y el alcance anteriores quedan pendientes de actualización. No escribas hasta emitir una nueva acción plan que incluya el requisito. El nivel de riesgo mínimo ahora es " + riskAssessment.Level.ToString().ToLowerInvariant() + "."
            });
            progress?.Report(AgentProgress.Of(
                AgentPhase.Analyzing,
                action: AgentAction.ActionPlan,
                iteration: currentIteration,
                message: "Requisito nuevo recibido; se actualizará el plan y el alcance."));
        }

        return new PendingSteeringOutcome(
            changed, intention, riskAssessment, authorizedScope, excludedScope, scopeObservationStartIndex);
    }

    private static (List<AgentSubtask> Subtasks, bool ReviewerDelegation, bool Ready, bool RequiresIndependentReview, string? Failure, string? TerminalFailure)
        PrepareReviewDelegation(
            AgentAction action,
            string profileId,
            bool requiresIndependentReview,
            bool hasDelegationService,
            bool requiresMultiAgentReview,
            AgentCheckpoint checkpoint,
            List<AgentStep> steps,
            int reviewReplanStepStart)
    {
        var reviewSubtasks = action.Action == AgentAction.ActionDelegate
            ? action.Subtasks?.Where(subtask => IsReviewProfile(subtask.ProfileId)).ToList() ?? new List<AgentSubtask>()
            : new List<AgentSubtask>();
        var reviewerDelegation = reviewSubtasks.Count > 0;
        if (reviewerDelegation && profileId == "engineer" && hasDelegationService)
            requiresIndependentReview = true;

        var ready = reviewerDelegation &&
            (!requiresMultiAgentReview || MultiAgentReviewPolicy.HasIndependentReviewers(
                reviewSubtasks.Select(subtask => subtask.ProfileId)));
        string? failure = reviewerDelegation && !ready
            ? "La revisión multiagente requiere delegar el mismo diff a dos perfiles distintos: reviewer y security-reviewer. Compara sus hallazgos y verifica la evidencia antes de cerrar."
            : null;
        string? terminalFailure = null;

        if (ready)
        {
            if (checkpoint.ReviewRoundsUsed >= 4)
            {
                terminalFailure = "Condor detuvo la revisión tras cuatro rondas. Replantea la tarea fuera del ciclo actual y limita la siguiente revisión a cambios nuevos.";
            }
            else if (checkpoint.ReviewRoundsUsed < 3)
            {
                checkpoint.ReviewRoundsUsed++;
            }
            else
            {
                checkpoint.ReviewReplanRequired = true;
                if (!checkpoint.ReviewReplanPrepared)
                {
                    failure = "Se alcanzó el límite de tres rondas completas. Primero replantea el plan con `plan`, realiza un cambio nuevo y luego solicita una única revisión delta de esos cambios.";
                }
                else
                {
                    var changedPaths = steps.Skip(reviewReplanStepStart)
                        .Where(step => step.Success && IsModification(step.Action) && !string.IsNullOrWhiteSpace(step.Path))
                        .Select(step => step.Path!.Replace('\\', '/'))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    var scope = string.Join(", ", changedPaths);
                    var deltaPrefix = "Revisión delta final: revisa únicamente los cambios nuevos en estas rutas: " + scope + ". No repitas la revisión completa ni reabras observaciones ya evaluadas. Instrucción: ";
                    if (changedPaths.Count == 0)
                        failure = "La revisión delta requiere al menos un archivo modificado después del nuevo plan.";
                    else if (changedPaths.Count > 30 || scope.Length > 3000 || reviewSubtasks.Any(subtask => deltaPrefix.Length + subtask.Task.Length > 6000))
                        failure = "La revisión delta excede el límite de alcance. Divide los cambios nuevos en un conjunto más pequeño y vuelve a planificar.";
                    else
                    {
                        foreach (var subtask in reviewSubtasks)
                            subtask.Task = deltaPrefix + subtask.Task;
                        checkpoint.ReviewRoundsUsed = 4;
                        checkpoint.ReviewReplanRequired = false;
                        checkpoint.ReviewReplanPrepared = false;
                        checkpoint.LastDecision = "revision_delta_final";
                    }
                }
            }
        }

        return (reviewSubtasks, reviewerDelegation, ready, requiresIndependentReview, failure, terminalFailure);
    }

    private static bool IsContinuationRequest(string intention)
    {
        var trimmed = intention.TrimStart();
        if (trimmed.StartsWith("Solicitud original:", StringComparison.OrdinalIgnoreCase))
            return trimmed.Contains("Continúa la tarea usando esta respuesta", StringComparison.OrdinalIgnoreCase);
        return trimmed.StartsWith("continúa", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("continua", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("continue", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("reanuda", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("retoma", StringComparison.OrdinalIgnoreCase);
    }

    private static AgentCheckpoint RestoreContinuationCheckpoint(AgentCheckpoint previous)
        => new()
        {
            SchemaVersion = previous.SchemaVersion,
            Task = previous.Task,
            WorkingDirectory = previous.WorkingDirectory,
            Model = previous.Model,
            Strategy = previous.Strategy,
            LastDecision = previous.LastDecision,
            LastAction = previous.LastAction,
            LastResult = previous.LastResult,
            HarnessState = previous.HarnessState,
            LastError = previous.LastError,
            NextAction = previous.NextAction,
            RiskLevel = previous.RiskLevel,
            DeclaredScope = previous.DeclaredScope?.ToList(),
            ExcludedScope = previous.ExcludedScope?.ToList(),
            PlanSteps = previous.PlanSteps?.ToList(),
            ExpectedResults = previous.ExpectedResults?.ToList(),
            VerificationSteps = previous.VerificationSteps?.ToList(),
            CompletedSteps = previous.CompletedSteps.ToList(),
            ReviewRoundsUsed = Math.Clamp(previous.ReviewRoundsUsed, 0, 4),
            UnreviewedModifications = Math.Max(0, previous.UnreviewedModifications),
            ReviewReplanRequired = previous.ReviewReplanRequired,
            ReviewReplanPrepared = previous.ReviewReplanPrepared,
            GeneratedAtUtc = DateTime.UtcNow
        };

    private static string BuildContinuationIntention(AgentCheckpoint checkpoint, string latestInstruction)
    {
        var scope = checkpoint.DeclaredScope is { Count: > 0 }
            ? string.Join(", ", checkpoint.DeclaredScope)
            : "sin alcance declarado todavía";
        var completed = checkpoint.CompletedSteps.Count == 0
            ? "ninguno registrado"
            : string.Join("; ", checkpoint.CompletedSteps);
        var plan = checkpoint.PlanSteps is { Count: > 0 }
            ? string.Join("; ", checkpoint.PlanSteps.Select((task, index) =>
                (index + 1) + ". " + task + " → resultado esperado: " +
                (checkpoint.ExpectedResults is { } expected && index < expected.Count ? expected[index] : "no registrado") + "; comprobar: " +
                (checkpoint.VerificationSteps is { } checks && index < checks.Count ? checks[index] : "no registrada")))
            : "pendiente de declarar";
        return "Continúa la tarea pendiente guardada para este proyecto. Tarea original: " + checkpoint.Task +
            "\nRiesgo calculado: " + (checkpoint.RiskLevel ?? "no registrado") +
            "\nAlcance: " + scope +
            "\nExclusiones: " + (checkpoint.ExcludedScope is { Count: > 0 } ? string.Join(", ", checkpoint.ExcludedScope) : "ninguna declarada") +
            "\nPlan: " + plan +
            "\nAcciones completadas: " + completed +
            "\nÚltima decisión: " + (checkpoint.LastDecision ?? "no registrada") +
            "\nÚltima acción: " + (checkpoint.LastAction ?? "ninguna") +
            "\nSiguiente acción pendiente: " + (checkpoint.NextAction ?? "revisar el estado") +
            "\nNueva instrucción del usuario: " + latestInstruction +
            "\nVuelve a explorar los archivos antes de editar, conserva el alcance salvo actualización explícita del plan y verifica el resultado.";
    }

    private static bool HasExploredScope(string scope, string workingDirectory, IReadOnlyList<AgentStep> steps)
    {
        var normalized = NormalizeRel(scope);
        if (normalized == ".")
            return steps.Any(step => step.Success && step.Action == AgentAction.ActionListDir && NormalizeRel(step.Path) == ".");

        if (normalized.EndsWith("/**", StringComparison.Ordinal))
        {
            var directory = normalized[..^3].TrimEnd('/');
            return steps.Any(step => step.Success && step.Action == AgentAction.ActionListDir &&
                NormalizeRel(step.Path).Equals(directory, StringComparison.OrdinalIgnoreCase));
        }

        if (steps.Any(step => step.Success && step.Action == AgentAction.ActionReadFile &&
            NormalizeRel(step.Path).Equals(normalized, StringComparison.OrdinalIgnoreCase)))
            return true;

        var fullPath = Path.Combine(workingDirectory, normalized.Replace('/', Path.DirectorySeparatorChar));
        if (Directory.Exists(fullPath))
            return steps.Any(step => step.Success && step.Action == AgentAction.ActionListDir &&
                NormalizeRel(step.Path).Equals(normalized, StringComparison.OrdinalIgnoreCase));
        if (File.Exists(fullPath)) return false;
        var parentPath = Path.GetDirectoryName(normalized);
        var parent = NormalizeRel(string.IsNullOrWhiteSpace(parentPath) ? "." : parentPath);
        return steps.Any(step => step.Success && step.Action == AgentAction.ActionListDir &&
            NormalizeRel(step.Path).Equals(parent, StringComparison.OrdinalIgnoreCase));
    }

    private static string? TryGetPlanExplorationDirectory(string scope, string workingDirectory)
    {
        var normalized = NormalizeRel(scope);
        if (normalized == ".") return ".";

        if (normalized.EndsWith("/**", StringComparison.Ordinal))
        {
            var directory = normalized[..^3].TrimEnd('/');
            var fullDirectory = Path.GetFullPath(Path.Combine(workingDirectory, directory.Replace('/', Path.DirectorySeparatorChar)));
            return Directory.Exists(fullDirectory) ? directory : null;
        }

        var fullPath = Path.GetFullPath(Path.Combine(workingDirectory, normalized.Replace('/', Path.DirectorySeparatorChar)));
        if (Directory.Exists(fullPath)) return normalized;
        if (File.Exists(fullPath)) return null;

        var parent = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(parent) || !Directory.Exists(parent)) return null;
        return NormalizeRel(Path.GetRelativePath(workingDirectory, parent));
    }

    private static string? TryGetExplicitDirectoryRequest(
        string intention,
        string workingDirectory,
        string? observedPath,
        IReadOnlyList<AgentStep> steps,
        int observationStartIndex)
    {
        var markers = new[] { "lista ", "listar ", "list_dir ", "list " };
        var marker = markers
            .Select(value => (Value: value, Index: intention.IndexOf(value, StringComparison.OrdinalIgnoreCase)))
            .Where(candidate => candidate.Index >= 0)
            .OrderBy(candidate => candidate.Index)
            .FirstOrDefault();
        if (marker.Value is null) return null;

        var requested = intention[(marker.Index + marker.Value.Length)..]
            .TrimStart()
            .Split(new[] { ' ', '\t', '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?
            .Trim('"', '\'', '`')
            .TrimEnd(':', '!', ')', '.');
        if (string.IsNullOrWhiteSpace(requested) || Path.IsPathRooted(requested) ||
            requested.Split('/', '\\').Any(segment => segment is "." or ".."))
            return null;

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(Path.Combine(workingDirectory, requested.Replace('/', Path.DirectorySeparatorChar)));
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }

        var relativePath = NormalizeRel(Path.GetRelativePath(workingDirectory, fullPath));
        if (relativePath == "." || relativePath == ".." || relativePath.StartsWith("../", StringComparison.Ordinal) ||
            relativePath.StartsWith("..\\", StringComparison.Ordinal) ||
            NormalizeRel(observedPath).Equals(relativePath, StringComparison.OrdinalIgnoreCase) ||
            !Directory.Exists(fullPath))
            return null;

        return steps.Skip(observationStartIndex).Any(step => step.Success && step.Action == AgentAction.ActionListDir &&
            NormalizeRel(step.Path).Equals(relativePath, StringComparison.OrdinalIgnoreCase))
            ? null
            : relativePath;
    }

    private static async Task<bool> TryExploreExplicitDirectoryAfterRedundantListDirAsync(
        string intention,
        string workingDirectory,
        string? observedPath,
        List<AgentStep> steps,
        int observationStartIndex,
        AgentToolset toolset,
        int iteration,
        List<LlmMessage> messages,
        IAgentProgressObserver? progress,
        AgentCheckpoint checkpoint,
        CancellationToken cancellationToken)
    {
        var requestedDirectory = TryGetExplicitDirectoryRequest(
            intention, workingDirectory, observedPath, steps, observationStartIndex);
        if (requestedDirectory is null) return false;

        var observation = await toolset.ExecuteAsync(
            new AgentAction { Action = AgentAction.ActionListDir, Path = requestedDirectory }, iteration, cancellationToken);
        steps.Add(observation);
        checkpoint.LastAction = AgentAction.ActionListDir;
        checkpoint.LastResult = observation.ResultPreview;
        checkpoint.NextAction = "continuar_exploracion";
        messages.Add(new LlmMessage
        {
            Role = "user",
            Content = "Condor completó la exploración del directorio solicitado " + requestedDirectory + ":\n" +
                (observation.Success ? observation.ResultPreview : "ERROR: " + observation.ResultPreview)
        });
        progress?.Report(AgentProgress.Of(AgentPhase.Observing, action: AgentAction.ActionListDir,
            path: requestedDirectory, iteration: iteration,
            message: "Condor explora el directorio solicitado en la tarea.", flag: ProgressFlag.Recovering));
        return observation.Success;
    }

    private static string BuildPlanGapCorrection(string planGap, bool broadCriticalScope, string? unexploredScope, int requiredSteps)
    {
        const string schema = "Presenta un único objeto JSON `plan` con esta forma: {\"action\":\"plan\",\"objective\":\"...\",\"scopePaths\":[\"ruta relativa concreta\"],\"excludedPaths\":[],\"planItems\":[{\"task\":\"tarea\",\"expectedResult\":\"resultado observable\",\"verificationStep\":\"comprobación propia\"}]}. Cada objeto agrupa su tarea, resultado y comprobación. Conserva las rutas exactas solicitadas; no reemplaces un archivo solicitado por su directorio padre.";
        var detail = !broadCriticalScope && unexploredScope is null
            ? " Incluye al menos " + requiredSteps + " objetos planItems distintos, cada uno con task, expectedResult y verificationStep diferentes; no repitas una tarea para alcanzar el mínimo."
            : " Incluye y explora el alcance solicitado antes de proponer el plan.";
        return planGap + " " + schema + detail + " No modifiques archivos hasta registrar el plan válido.";
    }

    private static string BuildInvalidPlanCorrection(AgentAction action, string validationFailure)
    {
        const string schema = "Devuelve un único objeto JSON con esta forma exacta: {\"action\":\"plan\",\"objective\":\"...\",\"scopePaths\":[\"ruta/relativa/observada\"],\"excludedPaths\":[],\"planItems\":[{\"task\":\"tarea\",\"expectedResult\":\"resultado observable\",\"verificationStep\":\"comprobación propia\"}]}. Usa las rutas relativas exactas solicitadas y observadas.";
        var planStepsCount = action.PlanSteps?.Count ?? 0;
        var expectedResultsCount = action.ExpectedResults?.Count ?? 0;
        var verificationStepsCount = action.VerificationSteps?.Count ?? 0;
        var needsArrayAlignment = planStepsCount > 0 &&
            (validationFailure.Contains("resultado esperado", StringComparison.OrdinalIgnoreCase) ||
             validationFailure.Contains("comprobación propia", StringComparison.OrdinalIgnoreCase) ||
             validationFailure.Contains("comprobaciones de salida", StringComparison.OrdinalIgnoreCase));
        var missingScope = validationFailure.Contains("scopePaths", StringComparison.OrdinalIgnoreCase);
        var detail = missingScope
            ? "Incluye scopePaths con la ruta relativa exacta del único archivo que el usuario pidió crear o modificar. Si el usuario no indicó una ruta concreta, solicita la ruta necesaria sin inventar alcance."
            : needsArrayAlignment
                ? $"El plan rechazado contiene {planStepsCount} tareas, {expectedResultsCount} resultados esperados y {verificationStepsCount} comprobaciones. Conserva exactamente las {planStepsCount} tareas propuestas y su orden; devuelve exactamente {planStepsCount} objetos planItems, cada uno con task, expectedResult y verificationStep concretos y distintos."
                : "Corrige la acción plan según la validación de Condor.";

        return validationFailure + " " + schema + " " + detail + " Cada tarea debe estar agrupada con su resultado y comprobación en un único objeto planItems. No modifiques archivos hasta registrar un plan válido.";
    }

    private static string BuildInvalidPlanFingerprint(AgentAction action, string validationFailure)
    {
        var content = System.Text.Json.JsonSerializer.Serialize(new
        {
            validationFailure = NormalizePlanFingerprintText(validationFailure),
            objective = NormalizePlanFingerprintText(action.Objective),
            scope = NormalizePlanFingerprintPaths(action.ScopePaths),
            excluded = NormalizePlanFingerprintPaths(action.ExcludedPaths),
            tasks = NormalizePlanFingerprintItems(action.PlanSteps),
            expectedResults = NormalizePlanFingerprintItems(action.ExpectedResults),
            verifications = NormalizePlanFingerprintItems(action.VerificationSteps)
        });
        var digest = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(digest);
    }

    private static string NormalizePlanFingerprintText(string? value) =>
        string.Join(" ", (value ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToUpperInvariant();

    private static string[] NormalizePlanFingerprintItems(IEnumerable<string>? items) =>
        (items ?? Array.Empty<string>())
            .Select(NormalizePlanFingerprintText)
            .ToArray();

    private static string[] NormalizePlanFingerprintPaths(IEnumerable<string>? paths) =>
        (paths ?? Array.Empty<string>())
            .Select(path => path.Trim().Replace('\\', '/'))
            .Where(path => path.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

    private static async Task<string?> ExploreUnobservedPlanScopeAsync(
        IReadOnlyList<string> planScope,
        string workingDirectory,
        int observationStartIndex,
        List<AgentStep> steps,
        AgentToolset toolset,
        List<LlmMessage> messages,
        IAgentProgressObserver? progress,
        int iteration,
        CancellationToken cancellationToken)
    {
        var observations = steps.Skip(observationStartIndex).ToList();
        var unexploredScope = planScope.FirstOrDefault(path => !HasExploredScope(path, workingDirectory, observations));
        if (unexploredScope is null)
            return unexploredScope;

        if (TryResolveReadableExistingPlanFile(unexploredScope, workingDirectory, out var existingFile))
        {
            var fileStep = await toolset.ExecuteAsync(
                new AgentAction { Action = AgentAction.ActionReadFile, Path = existingFile }, iteration, cancellationToken);
            steps.Add(fileStep);
            messages.Add(new LlmMessage
            {
                Role = "user",
                Content = "Condor leyó el archivo existente " + existingFile + " para validar la ruta incluida en el plan:\n" +
                    (fileStep.Success ? fileStep.ResultPreview : "ERROR: " + fileStep.ResultPreview)
            });
            progress?.Report(AgentProgress.Of(AgentPhase.Observing, action: AgentAction.ActionReadFile,
                path: existingFile, iteration: iteration,
                message: "Condor lee el archivo existente incluido en el plan.", flag: ProgressFlag.Recovering));
            if (!fileStep.Success) return unexploredScope;

            observations = steps.Skip(observationStartIndex).ToList();
            return planScope.FirstOrDefault(path => !HasExploredScope(path, workingDirectory, observations));
        }

        if (TryGetPlanExplorationDirectory(unexploredScope, workingDirectory) is not { } directoryToExplore)
            return unexploredScope;

        var directoryStep = await toolset.ExecuteAsync(
            new AgentAction { Action = AgentAction.ActionListDir, Path = directoryToExplore }, iteration, cancellationToken);
        steps.Add(directoryStep);
        messages.Add(new LlmMessage
        {
            Role = "user",
            Content = "Condor exploró el directorio " + directoryToExplore + " para validar la ruta incluida en el plan:\n" +
                (directoryStep.Success ? directoryStep.ResultPreview : "ERROR: " + directoryStep.ResultPreview)
        });
        progress?.Report(AgentProgress.Of(AgentPhase.Observing, action: AgentAction.ActionListDir,
            path: directoryToExplore, iteration: iteration,
            message: "Condor explora la ruta incluida en el plan.", flag: ProgressFlag.Recovering));
        if (!directoryStep.Success) return unexploredScope;

        observations = steps.Skip(observationStartIndex).ToList();
        return planScope.FirstOrDefault(path => !HasExploredScope(path, workingDirectory, observations));
    }

    private static bool TryResolveReadableExistingPlanFile(string scope, string workingDirectory, out string relativePath)
    {
        relativePath = NormalizeRel(scope);
        if (!AgentContentFileTypes.IsReadableExtension(Path.GetExtension(relativePath)))
            return false;

        try
        {
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(workingDirectory));
            var fullPath = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            var pathFromRoot = Path.GetRelativePath(root, fullPath);
            if (pathFromRoot == "." || pathFromRoot == ".." ||
                pathFromRoot.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                Path.IsPathRooted(pathFromRoot) ||
                !File.Exists(fullPath) || Directory.Exists(fullPath))
                return false;

            var current = fullPath;
            while (true)
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    return false;
                if (current.Equals(root, StringComparison.OrdinalIgnoreCase))
                    break;
                var parent = Path.GetDirectoryName(current);
                if (string.IsNullOrWhiteSpace(parent))
                    return false;
                current = parent;
            }

            relativePath = NormalizeRel(Path.GetRelativePath(root, fullPath));
            return true;
        }
        catch
        {
            relativePath = "";
            return false;
        }
    }

    private async Task<AgentResult> RunDirectConsultAsync(
        string intention,
        TaskModelRequirement requirement,
        IAgentProgressObserver? progress,
        CancellationToken cancellationToken)
    {
        var checkpoint = new AgentCheckpoint
        {
            Task = intention,
            GeneratedAtUtc = DateTime.UtcNow,
            Strategy = "direct-consult",
            LastDecision = "comprender"
        };
        var steps = new List<AgentStep>();

        var modelSetup = CreateModelSetup();
        var lastBudgetGb = _session?.LastSelectionBudgetGb ?? 0;
        var lastFreeRamGb = _session?.LastSelectionFreeRamGb ?? 0;
        var selection = await modelSetup.EnsureModelForRequirementAsync(
            requirement,
            cancellationToken: cancellationToken,
            currentModelName: _session?.ActiveModel,
            lastSelectionBudgetGb: lastBudgetGb,
            lastSelectionFreeRamGb: lastFreeRamGb);

        // Actualizar el presupuesto y RAM de la ultima seleccion en la sesion
        if (_session is not null && selection.Budget is not null)
        {
            _session.LastSelectionBudgetGb = selection.Budget.BudgetGb;
            _session.LastSelectionFreeRamGb = selection.Resources?.FreeGb ?? 0;
        }

        if (selection.Desired is null)
            return await BuildUnavailableDirectConsultResultAsync(
                selection, intention, steps, checkpoint, progress, cancellationToken);

        var model = selection.InstalledName ?? selection.Desired.PullName;
        checkpoint.Model = model;

        if (_session is not null)
        {
            var available = await _session.EnsureAvailableAsync(model, cancellationToken);
            if (!available)
            {
                checkpoint.LastDecision = "detener";
                checkpoint.LastError = "El proveedor local no esta disponible.";
                progress?.Report(AgentProgress.Of(
                    AgentPhase.Verifying,
                    message: checkpoint.LastError,
                    flag: ProgressFlag.ProviderError));
                return Fail(checkpoint.LastError, model, intention, steps, checkpoint);
            }
        }

        return await CompleteDirectConsultAsync(
            model, intention, selection, steps, checkpoint, progress, cancellationToken);
    }

    private async Task<AgentResult> BuildUnavailableDirectConsultResultAsync(
        ModelSelectionResult selection,
        string intention,
        List<AgentStep> steps,
        AgentCheckpoint checkpoint,
        IAgentProgressObserver? progress,
        CancellationToken cancellationToken)
    {
        // CASO NUEVO: hay modelo activo en sesión, pero la tarea requiere
        // un upgrade (modelo superior) que no cabe en el presupuesto actual.
        // No bloqueamos Condor: informamos al usuario y mantenemos la sesión viva.
        var currentActiveModel = _session?.ActiveModel;
        var needsUpgradeButNoMemory = currentActiveModel is not null && selection.BlockedByResources;
        if (needsUpgradeButNoMemory)
        {
            progress?.Report(AgentProgress.Of(
                AgentPhase.Verifying,
                message: "Memoria insuficiente para continuar con esta tarea.",
                resourceState: selection.Resources?.PressureLabel,
                availableGb: selection.Resources?.FreeGb,
                safeBudgetGb: selection.Budget?.BudgetGb,
                flag: ProgressFlag.Normal));

            checkpoint.LastDecision = "memoria_insuficiente";
            checkpoint.LastError = "Memoria insuficiente para el upgrade requerido.";

            return new AgentResult
            {
                Success = false,
                Reason = "Memoria insuficiente para continuar con esta tarea. Libera memoria y vuelve a intentarlo.",
                Model = currentActiveModel ?? string.Empty,
                Objective = intention,
                Steps = steps,
                Checkpoint = checkpoint,
                Inventory = await BuildInventoryAsync(selection, cancellationToken)
            };
        }

        // Sin modelo utilizable en absoluto: fallo controlado honesto.
        progress?.Report(AgentProgress.Of(
            AgentPhase.Verifying,
            message: "Modelo no ejecutable por presupuesto de memoria.",
            resourceState: selection.Resources?.PressureLabel,
            availableGb: selection.Resources?.FreeGb,
            safeBudgetGb: selection.Budget?.BudgetGb,
            flag: ProgressFlag.ProviderError));

        return Fail(BuildModelNotExecutableReason(selection), "", intention, steps, checkpoint);
    }

    private async Task<AgentResult> CompleteDirectConsultAsync(
        string model,
        string intention,
        ModelSelectionResult selection,
        List<AgentStep> steps,
        AgentCheckpoint checkpoint,
        IAgentProgressObserver? progress,
        CancellationToken cancellationToken)
    {
        var inventory = await BuildInventoryAsync(selection, cancellationToken);
        progress?.Report(AgentProgress.Of(
            AgentPhase.Understanding,
            message: "Consultando el modelo local",
            resourceState: selection.Resources?.PressureLabel,
            availableGb: selection.Resources?.FreeGb,
            safeBudgetGb: selection.Budget?.BudgetGb,
            modelActivity: model + " · redactando respuesta",
            modelWorking: true));

        LlmResponse response;
        try
        {
            response = await _llm.CompleteAsync(
                new LlmRequest
                {
                    Model = model,
                    Prompt = "Responde en español, de forma breve y natural, a esta consulta del usuario: " + intention,
                    Temperature = 0.2,
                    MaxTokens = 256
                },
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Fail("Tiempo total de la consulta agotado.", model, intention, steps, checkpoint);
        }

        progress?.Report(AgentProgress.Of(
            AgentPhase.Understanding,
            message: "Condor recibió la respuesta del modelo",
            modelActivity: model + " · respuesta recibida",
            modelWorking: false));

        if (!response.Success || string.IsNullOrWhiteSpace(response.Content))
        {
            checkpoint.LastDecision = "detener";
            checkpoint.LastError = BuildProviderFailureReason(
                response.Outcome,
                response.Error ?? "El modelo no devolvio una respuesta util.");
            progress?.Report(AgentProgress.Of(
                AgentPhase.Verifying,
                message: "Modelo local · " + checkpoint.LastError,
                flag: ProgressFlag.ProviderError));
            return Fail(checkpoint.LastError, model, intention, steps, checkpoint);
        }

        checkpoint.Iteration = 1;
        checkpoint.LastDecision = "entregar";
        checkpoint.NextAction = "entregar";
        progress?.Report(AgentProgress.Of(
            AgentPhase.Finalizing,
            message: "Preparando respuesta"));

        return new AgentResult
        {
            Success = true,
            Reason = response.Content.Trim(),
            Model = model,
            Objective = intention,
            Steps = steps,
            Checkpoint = checkpoint,
            Inventory = inventory
        };
    }

    private static AgentResult? TryDeliverInformational(
        AgentCheckpoint checkpoint,
        IReadOnlyList<AgentStep> steps,
        string workingDir,
        string model,
        string intention,
        IAgentProgressObserver? progress,
        string? lastDoneReason,
        AgentInventory? inventory)
    {
        var (hasSufficientEvidence, _) = AgentEngine.HasSufficientEvidenceForDone(intention, steps);
        if (!hasSufficientEvidence) return null;

        checkpoint.NextAction = "entregar";
        checkpoint.LastDecision = "describir";
        progress?.Report(AgentProgress.Of(AgentPhase.Finalizing, message: "Preparando respuesta"));

        var baseSummary = DescribeObserved(workingDir, steps);
        var summary = string.IsNullOrWhiteSpace(lastDoneReason)
            ? baseSummary
            : baseSummary + "\n" + lastDoneReason.Trim();
        if (string.Equals(checkpoint.HarnessState, "no_aplicable", StringComparison.OrdinalIgnoreCase) &&
            !summary.Contains("build/test no aplican", StringComparison.OrdinalIgnoreCase))
        {
            summary += "\nBuild/test no aplican al proyecto detectado; la entrega se basa en la evidencia observada.";
        }

        return new AgentResult
        {
            Success = true,
            Reason = summary,
            Model = model,
            Objective = intention,
            Steps = steps.ToList(),
            Checkpoint = checkpoint,
            Inventory = inventory
        };
    }

    private static bool IsInformationalRequest(string intention)
        => TaskIntentClassifier.IsInformational(intention);

    private static bool ShouldReportExcludedBehaviorMismatch(
        string intention,
        AgentCheckpoint checkpoint,
        string failureEvidence)
    {
        var addsTest = (intention.Contains("prueba", StringComparison.OrdinalIgnoreCase) ||
                        intention.Contains("test", StringComparison.OrdinalIgnoreCase)) &&
                       (intention.Contains("crear", StringComparison.OrdinalIgnoreCase) ||
                        intention.Contains("crea", StringComparison.OrdinalIgnoreCase) ||
                        intention.Contains("agregar", StringComparison.OrdinalIgnoreCase) ||
                        intention.Contains("agrega", StringComparison.OrdinalIgnoreCase) ||
                        intention.Contains("escribe", StringComparison.OrdinalIgnoreCase));
        var hasExcludedPaths = checkpoint.ExcludedScope is { Count: > 0 };
        var hasAssertionMismatch = failureEvidence.Contains("Assert.Equal() Failure", StringComparison.OrdinalIgnoreCase) ||
                                   (failureEvidence.Contains("Expected:", StringComparison.OrdinalIgnoreCase) &&
                                    failureEvidence.Contains("Actual:", StringComparison.OrdinalIgnoreCase));
        return addsTest && hasExcludedPaths && hasAssertionMismatch;
    }

    private static bool IsPlanBCorrectionAction(string action)
        => action is AgentAction.ActionPatch or
            AgentAction.ActionEditFile or
            AgentAction.ActionCreateFile or
            AgentAction.ActionUndoFile or
            AgentAction.ActionBuild or
            AgentAction.ActionTest;

    private static bool TryNormalizeCombinedFileAction(AgentAction action, string workingDir)
    {
        if (!string.Equals(action.Action, "patch/edit_file", StringComparison.OrdinalIgnoreCase))
            return false;

        var relativePath = action.Path?.Trim();
        if (string.IsNullOrWhiteSpace(relativePath) ||
            Path.IsPathRooted(relativePath) ||
            !string.Equals(Path.GetFileName(relativePath), relativePath, StringComparison.Ordinal) ||
            relativePath is "." or "..")
        {
            return false;
        }

        var target = Path.Combine(workingDir, relativePath);
        var exists = File.Exists(target);
        if (exists && !string.IsNullOrEmpty(action.Original) && action.Replacement is not null)
        {
            action.Action = AgentAction.ActionPatch;
            return true;
        }

        if (string.IsNullOrWhiteSpace(action.Content))
            return false;

        action.Action = exists ? AgentAction.ActionEditFile : AgentAction.ActionCreateFile;
        return true;
    }

    private static bool TryNormalizeRequestedRootHtmlPath(AgentAction action, string intention, string workingDir)
    {
        if (action.Action is not (AgentAction.ActionReadFile or AgentAction.ActionEditFile or
            AgentAction.ActionCreateFile or AgentAction.ActionPatch or "patch/edit_file"))
        {
            return false;
        }

        var match = System.Text.RegularExpressions.Regex.Match(
            intention ?? "",
            @"\bruta\s+exacta\s+(?<file>[A-Za-z0-9_.-]+\.html)\s*\(\s*ra[ií]z\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!match.Success) return false;

        var requestedFile = match.Groups["file"].Value;
        if (!string.Equals(Path.GetFileName(requestedFile), requestedFile, StringComparison.Ordinal))
            return false;

        var actionPath = NormalizeRel(action.Path?.Trim());
        while (actionPath.StartsWith("./", StringComparison.Ordinal)) actionPath = actionPath[2..];
        if (!string.Equals(actionPath, "docs/" + requestedFile, StringComparison.OrdinalIgnoreCase))
            return false;

        var root = Path.GetFullPath(workingDir);
        var target = Path.GetFullPath(Path.Combine(root, requestedFile));
        var rootPrefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        if (!target.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        action.Path = requestedFile;
        return true;
    }

    private static AgentAction? TryCreateCompleteHtmlEditFallback(
        AgentAction action,
        string workingDir,
        string? failureDetail)
    {
        if (!string.Equals(action.Action, AgentAction.ActionPatch, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(failureDetail) ||
            !failureDetail.Contains("No se encontro el texto a reemplazar", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var relativePath = action.Path?.Trim();
        if (string.IsNullOrWhiteSpace(relativePath) ||
            Path.IsPathRooted(relativePath) ||
            !string.Equals(Path.GetFileName(relativePath), relativePath, StringComparison.Ordinal) ||
            !string.Equals(Path.GetExtension(relativePath), ".html", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var document = action.Replacement ?? action.Content;
        if (string.IsNullOrWhiteSpace(document) ||
            !document.Contains("<!doctype html", StringComparison.OrdinalIgnoreCase) ||
            !document.Contains("<html", StringComparison.OrdinalIgnoreCase) ||
            !document.Contains("</html>", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var target = Path.Combine(workingDir, relativePath);
        try
        {
            if (!File.Exists(target) || (File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0)
                return null;
        }
        catch
        {
            return null;
        }

        return new AgentAction
        {
            Action = AgentAction.ActionEditFile,
            Path = relativePath,
            Content = document,
            Reason = "Aplicar replacement como documento HTML completo despues de que patch.original no coincidio."
        };
    }

    private static async Task<string?> GroundInformationalAsync(AgentToolset toolset, string workingDir, List<AgentStep> steps, CancellationToken ct)
    {
        try
        {
            AgentStep list = await toolset.ExecuteAsync(new AgentAction { Action = AgentAction.ActionListDir, Path = "" }, 0, ct);
            steps.Add(list);
            if (!list.Success) return null;

            var files = UnreadContentFiles(workingDir, steps).Take(2).ToList();
            foreach (var f in files)
            {
                var read = await toolset.ExecuteAsync(new AgentAction { Action = AgentAction.ActionReadFile, Path = f }, 0, ct);
                if (read.Success) steps.Add(read);
            }

            return DescribeObserved(workingDir, steps);
        }
        catch
        {
            return null;
        }
    }

    private static string DescribeObserved(string workingDir, IReadOnlyList<AgentStep> steps)
    {
        var dirs = steps.Where(s => s.Success && s.Action == AgentAction.ActionListDir)
            .Select(s => s.Path ?? ".")
            .Distinct(System.StringComparer.OrdinalIgnoreCase)
            .ToList();
        var files = steps.Where(s => s.Success && s.Action == AgentAction.ActionReadFile)
            .Select(s => s.Path ?? "")
            .Distinct(System.StringComparer.OrdinalIgnoreCase)
            .Where(p => p.Length > 0)
            .ToList();

        var sb = new System.Text.StringBuilder();
        sb.Append("Condor observo el directorio " + workingDir + ".");
        if (files.Count > 0)
            sb.Append(" Archivos revisados: " + string.Join(", ", files) + ".");
        else
            sb.Append(" No se leyo contenido de archivos.");

        if (dirs.Count > 0)
            sb.Append(" Estructura observada: " + string.Join(", ", dirs) + ".");
        return sb.ToString();
    }

    private static List<string> UnreadContentFiles(string workingDir, IReadOnlyList<AgentStep> steps)
    {
        var readPaths = steps
            .Where(s => s.Success && s.Action == AgentAction.ActionReadFile)
            .Select(s => NormalizeRel(s.Path))
            .ToHashSet(System.StringComparer.OrdinalIgnoreCase);

        var candidates = new List<string>();
        try
        {
            foreach (var file in Directory.EnumerateFiles(workingDir, "*", SearchOption.AllDirectories))
            {
                var name = Path.GetFileName(file);
                if (name.StartsWith(".", StringComparison.Ordinal)) continue;
                if (file.Contains("\\bin\\") || file.Contains("\\obj\\") || file.Contains("\\.git\\") ||
                    file.Contains("\\node_modules\\") || file.Contains("\\.vs\\") || file.Contains("\\.artifacts\\"))
                    continue;

                var ext = Path.GetExtension(file);
                if (!AgentContentFileTypes.IsReadableExtension(ext)) continue;

                var rel = Path.GetRelativePath(workingDir, file).Replace('\\', '/');
                if (readPaths.Contains(NormalizeRel(rel))) continue;

                candidates.Add(rel);
                if (candidates.Count >= 12) break;
            }
        }
        catch
        {
            // Si falla la enumeracion del sistema de archivos, no se bloquea.
        }

        return candidates;
    }

    // Extensiones de contenido/codigo que un agente debe poder inspeccionar.
    private static string NormalizeRel(string? p) => (p ?? "").Replace('\\', '/');

    private static string BuildObservedSummary(IReadOnlyList<AgentStep> steps, string workingDir)
    {
        var dirs = steps.Where(s => s.Success && s.Action == AgentAction.ActionListDir)
            .Select(s => s.Path ?? ".")
            .Distinct(System.StringComparer.OrdinalIgnoreCase)
            .ToList();
        var files = steps.Where(s => s.Success && s.Action == AgentAction.ActionReadFile)
            .Select(s => s.Path ?? "")
            .Distinct(System.StringComparer.OrdinalIgnoreCase)
            .Where(p => p.Length > 0)
            .ToList();

        var sb = new System.Text.StringBuilder();
        sb.Append("Condor observo el directorio " + workingDir);

        if (files.Count > 0)
            sb.Append(" y reviso " + files.Count + " archivo(s): " + string.Join(", ", files) + ".");
        else
            sb.Append(". No llego a leer archivos.");

        if (dirs.Count > 0)
            sb.Append(" Estructura explorada: " + string.Join(", ", dirs) + ".");

        sb.Append(" Describe lo encontrado segun la evidencia observada.");
        return sb.ToString();
    }

    private static string? ValidateReviewFindings(
        IReadOnlyList<AgentReviewFinding>? findings,
        string workingDirectory,
        IReadOnlyList<AgentStep> steps)
    {
        if (findings is null)
            return "La revisión debe devolver reviewFindings como lista explícita, aunque esté vacía.";
        if (findings.Count > 200)
            return "La revisión excedió el máximo de 200 hallazgos; prioriza los defectos verificables.";

        var root = Path.GetFullPath(workingDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var lineCache = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        var changedLineCache = new Dictionary<string, HashSet<int>?>(StringComparer.OrdinalIgnoreCase);
        long inspectedBytes = 0;
        var findingTextLength = 0;
        foreach (var finding in findings)
        {
            if (finding is null)
                return "La lista de revisión contiene un hallazgo nulo.";
            var contractError = ValidateReviewFindingFields(finding, ref findingTextLength);
            if (contractError is not null)
                return contractError;

            var evidence = ReadAndValidateReviewEvidence(
                finding, workingDirectory, root, steps, lineCache, ref inspectedBytes);
            if (evidence.Error is not null)
                return evidence.Error;

            var changeError = ValidateReviewFindingChangeStatus(
                finding, workingDirectory, evidence.RelativePath, evidence.Lines!, changedLineCache);
            if (changeError is not null)
                return changeError;
        }

        return null;
    }

    private static string? ValidateReviewFindingFields(AgentReviewFinding finding, ref int findingTextLength)
    {
        var pathError = ValidateReviewFindingPathAndPosition(finding);
        if (pathError is not null)
            return pathError;
        if (finding.Severity is not ("critical" or "high" or "medium" or "low"))
            return "La gravedad de cada hallazgo debe ser critical, high, medium o low.";
        if (string.IsNullOrWhiteSpace(finding.Explanation) || finding.Explanation.Length > 2000)
            return "Cada hallazgo requiere una explicación breve.";
        if (string.IsNullOrWhiteSpace(finding.Evidence) || finding.Evidence.Trim().Length < 4 ||
            finding.Evidence.Length > 500 || finding.Evidence.Contains('\n') || finding.Evidence.Contains('\r'))
            return "Cada hallazgo requiere evidencia literal y breve de una sola línea.";
        if (string.IsNullOrWhiteSpace(finding.Reproduction) || finding.Reproduction.Length > 2000)
            return "Cada hallazgo requiere pasos para reproducir o confirmar el problema.";
        if (finding.Origin is not ("current-change" or "pre-existing" or "uncertain"))
            return "El origen debe ser current-change, pre-existing o uncertain.";
        if (finding.Status is not ("open" or "resolved" or "accepted"))
            return "El estado debe ser open, resolved o accepted.";
        if (finding.Status != "open" && string.IsNullOrWhiteSpace(finding.ClosureReason))
            return "Un hallazgo resuelto o aceptado requiere una justificación de cierre.";
        if (finding.ClosureReason?.Length > 1000)
            return "La justificación de cierre no puede exceder 1000 caracteres.";

        findingTextLength += finding.Path.Length + finding.Explanation.Length + finding.Evidence.Length +
            finding.Reproduction.Length + (finding.ClosureReason?.Length ?? 0);
        return findingTextLength > 100_000
            ? "Los hallazgos exceden el máximo de texto acumulado de 100000 caracteres."
            : null;
    }

    private static string? ValidateReviewFindingPathAndPosition(AgentReviewFinding finding)
    {
        if (string.IsNullOrWhiteSpace(finding.Path) || finding.Path.Length > 500 ||
            finding.Path.StartsWith('/') || finding.Path.Contains(':') ||
            finding.Path.Replace('\\', '/').Split('/').Any(segment => segment is "" or "." or ".."))
            return "Cada hallazgo requiere una ruta relativa segura.";
        return finding.Line < 1
            ? "Cada hallazgo requiere un número de línea positivo."
            : null;
    }

    private static (string? Error, string RelativePath, string[]? Lines) ReadAndValidateReviewEvidence(
        AgentReviewFinding finding,
        string workingDirectory,
        string root,
        IReadOnlyList<AgentStep> steps,
        Dictionary<string, string[]> lineCache,
        ref long inspectedBytes)
    {
        var relative = finding.Path.Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(workingDirectory, relative));
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
            return ("El archivo del hallazgo no existe dentro del proyecto: " + finding.Path, relative, null);

        var observed = steps.Any(step => step.Success && step.Action == AgentAction.ActionReadFile &&
            string.Equals(NormalizeReviewPath(step.Path), NormalizeReviewPath(finding.Path), StringComparison.OrdinalIgnoreCase));
        if (!observed)
            return ("El hallazgo debe estar basado en un archivo que el revisor leyó: " + finding.Path, relative, null);

        if (!lineCache.TryGetValue(fullPath, out var lines))
        {
            try
            {
                var fileLength = new FileInfo(fullPath).Length;
                if (fileLength > 5 * 1024 * 1024 || inspectedBytes + fileLength > 20 * 1024 * 1024)
                    return ("Los archivos citados por la revisión exceden el límite de lectura segura.", relative, null);
                inspectedBytes += fileLength;
                lines = File.ReadAllLines(fullPath);
                lineCache[fullPath] = lines;
            }
            catch (Exception exception)
            {
                return ("No se pudo verificar la evidencia de " + finding.Path + ": " + exception.GetType().Name, relative, null);
            }
        }

        if (finding.Line > lines.Length || !lines[finding.Line - 1].Contains(finding.Evidence.Trim(), StringComparison.Ordinal))
            return ("La evidencia literal no coincide con la línea " + finding.Line + " de " + finding.Path + ".", relative, null);
        return (null, relative, lines);
    }

    private static string? ValidateReviewFindingChangeStatus(
        AgentReviewFinding finding,
        string workingDirectory,
        string relativePath,
        string[] lines,
        Dictionary<string, HashSet<int>?> changedLineCache)
    {
        if (finding.Status != "resolved" && !(finding.Status == "open" && finding.Origin != "uncertain"))
            return null;

        if (!changedLineCache.TryGetValue(relativePath, out var changedLines))
        {
            changedLines = ReadChangedLineNumbers(workingDirectory, finding.Path, lines.Length);
            changedLineCache[relativePath] = changedLines;
        }
        if (changedLines is null)
            return finding.Status == "resolved"
                ? "No se pudo verificar el cierre del hallazgo contra HEAD: " + finding.Path + "."
                : "No se pudo comparar con HEAD; usa origin=uncertain para " + finding.Path + ".";

        var changed = changedLines.Contains(finding.Line);
        if (finding.Status == "resolved" && !changed)
            return "El hallazgo se marcó resolved, pero la línea citada no forma parte del cambio actual: " + finding.Path + ":" + finding.Line;
        if (finding.Status == "open" && finding.Origin == "current-change" && !changed)
            return "El hallazgo se marcó current-change, pero su línea no cambió respecto de HEAD: " + finding.Path + ":" + finding.Line;
        if (finding.Status == "open" && finding.Origin == "pre-existing" && changed)
            return "El hallazgo se marcó pre-existing, pero su línea sí cambió respecto de HEAD: " + finding.Path + ":" + finding.Line;
        return null;
    }

    private static string NormalizeReviewPath(string? path)
        => (path ?? "").Replace('\\', '/').TrimStart('.', '/');

    private static HashSet<int>? ReadChangedLineNumbers(string workingDirectory, string relativePath, int lineCount)
    {
        try
        {
            var start = new ProcessStartInfo("git")
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            start.ArgumentList.Add("diff");
            start.ArgumentList.Add("--unified=0");
            start.ArgumentList.Add("--no-color");
            start.ArgumentList.Add("HEAD");
            start.ArgumentList.Add("--");
            start.ArgumentList.Add(relativePath);
            using var process = Process.Start(start);
            if (process is null)
                return null;
            var output = process.StandardOutput.ReadToEnd();
            _ = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
                return null;

            // `git diff HEAD` no incluye archivos nuevos sin seguimiento. Para
            // una ruta no rastreada, cada línea presente pertenece al cambio actual.
            if (output.Length == 0)
            {
                var tracked = new ProcessStartInfo("git")
                {
                    WorkingDirectory = workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                tracked.ArgumentList.Add("ls-files");
                tracked.ArgumentList.Add("--error-unmatch");
                tracked.ArgumentList.Add("--");
                tracked.ArgumentList.Add(relativePath);
                using var trackedProcess = Process.Start(tracked);
                if (trackedProcess is null)
                    return null;
                _ = trackedProcess.StandardOutput.ReadToEnd();
                _ = trackedProcess.StandardError.ReadToEnd();
                trackedProcess.WaitForExit();
                if (trackedProcess.ExitCode == 1)
                {
                    if (lineCount > 100_000)
                        return null;
                    return Enumerable.Range(1, lineCount).ToHashSet();
                }
                if (trackedProcess.ExitCode != 0)
                    return null;
            }

            var changed = new HashSet<int>();
            var hunks = System.Text.RegularExpressions.Regex.Matches(
                output,
                "^@@ -[0-9]+(?:,[0-9]+)? \\+([0-9]+)(?:,([0-9]+))? @@",
                System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            foreach (System.Text.RegularExpressions.Match hunk in hunks)
            {
                var first = int.Parse(hunk.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                var count = hunk.Groups[2].Success
                    ? int.Parse(hunk.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture)
                    : 1;
                if (count > 100_000 || changed.Count + count > 100_000)
                    return null;
                for (var line = first; line < first + count; line++)
                    changed.Add(line);
            }
            return changed;
        }
        catch
        {
            return null;
        }
    }

    private static string FormatReviewFindings(IEnumerable<AgentReviewFinding> findings)
    {
        return string.Join("\n", findings.Select(finding =>
            $"[{finding.Severity}] {finding.Path}:{finding.Line} ({finding.Origin}, {finding.Status}) — {finding.Explanation}\n" +
            $"Evidencia: {finding.Evidence}\nReproducción: {finding.Reproduction}" +
            (string.IsNullOrWhiteSpace(finding.ClosureReason) ? "" : "\nCierre: " + finding.ClosureReason)));
    }

    private static bool IsModification(string action)
        => action is AgentAction.ActionEditFile or AgentAction.ActionCreateFile or AgentAction.ActionPatch or AgentAction.ActionUndoFile;

    private static bool IsReviewProfile(string? profileId)
        => profileId is "reviewer" or "security-reviewer";

    private static bool TryAuthorizeExplicitSingleFileScope(
        string intention,
        string? actionPath,
        Condor.Core.Evaluation.TaskRiskLevel riskLevel,
        out string scope)
    {
        scope = "";
        if (riskLevel is Condor.Core.Evaluation.TaskRiskLevel.High or Condor.Core.Evaluation.TaskRiskLevel.Critical ||
            string.IsNullOrWhiteSpace(actionPath))
            return false;

        var target = NormalizeRel(actionPath);
        var mentions = System.Text.RegularExpressions.Regex.Matches(
                (intention ?? "").Replace('\\', '/'),
                @"(?<![\w./-])(?:[\w.-]+/)*[\w.-]+\.[A-Za-z0-9]{1,12}(?![\w-]|\.[A-Za-z0-9])",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant)
            .Select(match => NormalizeRel(match.Value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (mentions.Count == 0 || !string.Equals(mentions[0], target, StringComparison.OrdinalIgnoreCase))
            return false;

        scope = target;
        return true;
    }

    private static bool TryRecoverExplicitPlanScope(
        string intention,
        string? workingDirectory,
        Condor.Core.Evaluation.TaskRiskLevel riskLevel,
        IReadOnlyList<AgentStep> steps,
        out string scope)
    {
        scope = "";
        if (riskLevel is Condor.Core.Evaluation.TaskRiskLevel.High or Condor.Core.Evaluation.TaskRiskLevel.Critical ||
            string.IsNullOrWhiteSpace(workingDirectory))
            return false;

        var target = FindSingleExplicitWriteTarget(intention);
        if (target is null || !WasExplicitPlanTargetObserved(target, workingDirectory, steps))
            return false;

        scope = target;
        return true;
    }

    private static string? RecoverExplicitPlanScope(
        AgentAction action,
        string intention,
        string? workingDirectory,
        Condor.Core.Evaluation.TaskRiskLevel riskLevel,
        List<AgentStep> steps)
    {
        if (!string.Equals(action.Action, AgentAction.ActionPlan, StringComparison.OrdinalIgnoreCase) ||
            action.ScopePaths is { Count: > 1 } ||
            !TryRecoverExplicitPlanScope(intention, workingDirectory, riskLevel, steps, out var scope))
            return null;

        var proposedScope = action.ScopePaths?.SingleOrDefault()?.Replace('\\', '/');
        if (!string.IsNullOrWhiteSpace(proposedScope) &&
            !Path.GetFileName(proposedScope).Equals(Path.GetFileName(scope), StringComparison.OrdinalIgnoreCase))
            return null;
        if (string.Equals(proposedScope, scope, StringComparison.OrdinalIgnoreCase) ||
            action.ExcludedPaths?.Any(excluded => AgentEngine.IsPathWithinScope(scope, new[] { excluded })) == true)
            return null;

        action.ScopePaths = new List<string> { scope };
        return scope;
    }

    private static void RecordRecoveredPlanScope(
        string? scope,
        int iteration,
        List<AgentStep> steps,
        IAgentProgressObserver? progress)
    {
        if (scope is null)
            return;

        var recovery = "Condor fijó scopePaths a la única ruta de archivo solicitada y explorada: " + scope;
        steps.Add(new AgentStep
        {
            Iteration = iteration,
            Action = "plan_scope",
            Path = scope,
            Success = true,
            ResultPreview = recovery,
            AtUtc = DateTime.UtcNow
        });
        progress?.Report(AgentProgress.Of(
            AgentPhase.Analyzing,
            action: AgentAction.ActionPlan,
            iteration: iteration,
            path: scope,
            message: recovery,
            flag: ProgressFlag.Recovering));
    }

    private static bool IsPlanProgressAction(string action, AgentStep step)
    {
        if (IsModification(action))
            return step.Success;

        return (step.Success && (action is AgentAction.ActionReadFile or AgentAction.ActionSearch)) ||
               action is AgentAction.ActionBuild or AgentAction.ActionTest or AgentAction.ActionRestore;
    }

    private static string? FindSingleExplicitWriteTarget(string intention)
    {
        var text = (intention ?? "").Replace('\\', '/');
        const string pathPattern = @"(?<![\w./-])(?:[\w.-]+/)*[\w.-]+\.[A-Za-z0-9]{1,12}(?![\w-]|\.[A-Za-z0-9])";
        const string writeVerbPattern = @"\b(?:crea(?:r|s|n)?|creame|escrib(?:e|ir|as)|agreg(?:a|ar|as)|añad(?:e|ir|as)|edit(?:a|ar|es)|modific(?:a|ar|as|es)|actualiz(?:a|ar|es)|repar(?:a|ar)|genera(?:r)?|create|write|add|edit|modify|update|fix)\b";
        const string negationPattern = @"\b(?:no|don't|do not)\s+(?:[\w'-]+\s+){0,4}$";
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (System.Text.RegularExpressions.Match pathMatch in System.Text.RegularExpressions.Regex.Matches(
                     text, pathPattern,
                     System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant))
        {
            if (!Condor.Core.Agent.AgentContentFileTypes.IsReadableExtension(Path.GetExtension(pathMatch.Value)))
                continue;

            var prefix = text[..pathMatch.Index];
            var verbs = System.Text.RegularExpressions.Regex.Matches(
                prefix, writeVerbPattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            if (verbs.Count == 0)
                continue;

            var verb = verbs[^1];
            var between = prefix[(verb.Index + verb.Length)..];
            if (pathMatch.Index - verb.Index > 120 ||
                between.IndexOfAny(new[] { '.', '!', '?', ';', '\r', '\n' }) >= 0 ||
                System.Text.RegularExpressions.Regex.IsMatch(
                    prefix, negationPattern,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant))
                continue;

            var relative = pathMatch.Value.Replace('\\', '/');
            if (Path.IsPathRooted(relative) || relative.Contains(':') ||
                relative.Split('/').Any(segment => segment is "" or "." or ".."))
                continue;

            candidates.Add(relative);
        }

        return candidates.Count == 1 ? candidates.Single() : null;
    }

    private static bool WasExplicitPlanTargetObserved(
        string relativePath,
        string workingDirectory,
        IReadOnlyList<AgentStep> steps)
    {
        try
        {
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(workingDirectory));
            if (!Directory.Exists(root))
                return false;

            var fullPath = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            var relativeFromRoot = Path.GetRelativePath(root, fullPath);
            if (relativeFromRoot is "." or ".." ||
                relativeFromRoot.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                Path.IsPathRooted(relativeFromRoot))
                return false;

            var parent = Path.GetDirectoryName(fullPath);
            if (parent is null || !Directory.Exists(parent))
                return false;

            var current = File.Exists(fullPath) ? fullPath : parent;
            while (true)
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    return false;
                if (current.Equals(root, StringComparison.OrdinalIgnoreCase))
                    break;
                current = Path.GetDirectoryName(current)!;
                if (string.IsNullOrWhiteSpace(current))
                    return false;
            }

            return steps.Any(step => step.Success &&
                (step.Action == AgentAction.ActionReadFile && ResolvesTo(step.Path, fullPath) ||
                 step.Action == AgentAction.ActionListDir && ResolvesTo(step.Path, parent)));

            bool ResolvesTo(string? observedPath, string expectedPath)
            {
                var observed = Path.GetFullPath(Path.Combine(
                    root,
                    string.IsNullOrWhiteSpace(observedPath) ? "." : observedPath.Replace('/', Path.DirectorySeparatorChar)));
                return observed.Equals(expectedPath, StringComparison.OrdinalIgnoreCase);
            }
        }
        catch
        {
            return false;
        }
    }

    private static void RegisterExplicitSingleFilePlan(
        string scope,
        IReadOnlyList<string> excludedPaths,
        Condor.Core.Evaluation.TaskRiskLevel riskLevel,
        int iteration,
        List<AgentStep> steps,
        AgentCheckpoint checkpoint,
        IAgentProgressObserver? progress)
    {
        var task = "Realizar el cambio solicitado en " + scope + ".";
        var expectedResult = "El archivo " + scope + " refleja el resultado solicitado.";
        var verification = "Comprobar el archivo y ejecutar las verificaciones aplicables a la solicitud.";
        var objective = "Completar el cambio explícito solicitado para " + scope + ".";

        checkpoint.DeclaredScope = new List<string> { scope };
        checkpoint.ExcludedScope = excludedPaths.ToList();
        checkpoint.PlanSteps = new List<string> { task };
        checkpoint.ExpectedResults = new List<string> { expectedResult };
        checkpoint.VerificationSteps = new List<string> { verification };
        checkpoint.LastDecision = "plan_determinista_archivo_explicito";
        checkpoint.LastAction = AgentAction.ActionPlan;
        checkpoint.LastResult = objective;
        checkpoint.LastError = null;
        checkpoint.NextAction = "ejecutar_plan";

        var summary = "Plan de Condor para una ruta explícita · riesgo " + riskLevel.ToString().ToLowerInvariant() +
            " · objetivo: " + objective +
            " · alcance: " + scope +
            (excludedPaths.Count == 0 ? "" : " · excluye: " + string.Join(", ", excludedPaths)) +
            " · tarea: " + task +
            " · resultado esperado: " + expectedResult +
            " · comprobar: " + verification;
        steps.Add(new AgentStep
        {
            Iteration = iteration,
            Action = AgentAction.ActionPlan,
            Success = true,
            ResultPreview = summary,
            AtUtc = DateTime.UtcNow
        });
        progress?.Report(AgentProgress.Of(
            AgentPhase.Analyzing,
            action: AgentAction.ActionPlan,
            iteration: iteration,
            message: summary));
    }

    private static List<string> FindExplicitlyExcludedFilePaths(string intention, string scope)
    {
        var normalizedIntention = (intention ?? string.Empty).Replace('\\', '/');
        var matches = System.Text.RegularExpressions.Regex.Matches(
            normalizedIntention,
            @"(?<![\w./-])(?:[\w.-]+/)*[\w.-]+\.[A-Za-z0-9]{1,12}(?![\w-]|\.[A-Za-z0-9])",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        var excluded = new List<string>();
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var path = NormalizeRel(match.Value);
            if (string.Equals(path, scope, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!path.Contains('/') && !IsRecognizedFileExtension(System.IO.Path.GetExtension(path)))
                continue;

            var contextStart = Math.Max(0, match.Index - 100);
            var contextLength = Math.Min(normalizedIntention.Length - contextStart, match.Length + 100);
            var context = normalizedIntention.Substring(contextStart, contextLength);
            var explicitlyExcluded = System.Text.RegularExpressions.Regex.IsMatch(
                context,
                @"\b(excluid[oa]s?|fuera\s+del\s+alcance|no\s+(?:edites|modifiques|alteres|cambies)|do\s+not\s+(?:edit|modify)|don't\s+(?:edit|modify))\b",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            if (explicitlyExcluded && !excluded.Contains(path, StringComparer.OrdinalIgnoreCase))
                excluded.Add(path);
        }

        return excluded;
    }

    private static bool IsRecognizedFileExtension(string extension)
        => extension.ToLowerInvariant() is ".cs" or ".csx" or ".fs" or ".vb" or ".py" or ".js" or ".ts" or
            ".jsx" or ".tsx" or ".html" or ".htm" or ".css" or ".json" or ".xml" or ".yml" or ".yaml" or
            ".md" or ".txt" or ".sql" or ".sh" or ".ps1" or ".toml" or ".ini" or ".config" or ".props" or
            ".targets" or ".csproj" or ".sln" or ".slnx" or ".lock";

    private static bool RequestsIndependentReview(string intention)
    {
        var request = intention ?? string.Empty;
        return request.Contains("revisión independiente", StringComparison.OrdinalIgnoreCase) ||
            request.Contains("revision independiente", StringComparison.OrdinalIgnoreCase) ||
            request.Contains("revisor independiente", StringComparison.OrdinalIgnoreCase) ||
            MultiAgentReviewPolicy.IsRequested(request) ||
            request.Contains("review", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Mapa (puro) de accion a fase de progreso, para observabilidad.</summary>
    public static AgentPhase PhaseForAction(string action)
    {
        switch (action)
        {
            case AgentAction.ActionListDir:
            case AgentAction.ActionReadFile:
            case AgentAction.ActionSearch:
                return AgentPhase.Observing;

            case AgentAction.ActionPatch:
            case AgentAction.ActionEditFile:
            case AgentAction.ActionCreateFile:
            case AgentAction.ActionUndoFile:
                return AgentPhase.Building;

            case AgentAction.ActionBuild:
            case AgentAction.ActionTest:
            case AgentAction.ActionRestore:
                return AgentPhase.Verifying;

            case AgentAction.ActionDone:
                return AgentPhase.Finalizing;

            default:
                return AgentPhase.Analyzing;
        }
    }

    private string? StartPlanB(
        string? failedAction,
        string? failedPath,
        string failureDetail,
        ref bool planBUsed,
        ref string? planBInitialFailure,
        ref bool awaitingPlanBAction,
        ref int effectiveMaxIterations,
        int failedIteration,
        List<LlmMessage> messages,
        IAgentProgressObserver? progress,
        AgentCheckpoint checkpoint)
    {
        var detail = Truncate(string.IsNullOrWhiteSpace(failureDetail) ? "sin detalle" : failureDetail);
        var location = string.IsNullOrWhiteSpace(failedPath) ? "" : " en '" + failedPath + "'";
        var redundantObservation = detail.Contains("observaciones redundantes", StringComparison.OrdinalIgnoreCase);
        var rejectedPlanContext = detail.Contains("ultimo plan rechazado", StringComparison.OrdinalIgnoreCase);
        var failureContext = string.IsNullOrWhiteSpace(failedAction)
            ? "Bloqueo detectado: " + detail
            : redundantObservation
                ? "Observacion repetida sin informacion nueva: " + failedAction + location + ". Resultado real: " + detail
                : "Accion fallida: " + failedAction + location + ". Resultado real: " + detail;
        var failureLabel = redundantObservation
            ? "observacion redundante (" + failedAction + location + ")"
            : string.IsNullOrWhiteSpace(failedAction) ? "falta de progreso" : failedAction + location;
        var redundantObservationGuidance = redundantObservation
            ? " Esta observacion ya produjo el mismo resultado; no repitas esa accion ni una lectura equivalente. " +
              (rejectedPlanContext
                  ? "El ultimo plan fue rechazado; corrige el motivo de rechazo que sigue y no supongas que hay un plan aceptado. "
                  : "Revisa el objetivo original y el plan aceptado, y ejecuta el siguiente paso pendiente. ") +
              "Consulta otra ruta solo si la solicitud la exige o falta evidencia necesaria. Si el paso pendiente crea el archivo nuevo solicitado, usa create_file con la ruta exacta; no leas archivos del proyecto de pruebas sin necesidad."
            : "";
        var sourceContentRecoveryGuidance = detail.Contains("content contiene solo la ruta", StringComparison.OrdinalIgnoreCase)
            ? " CORRECCION DEL CONTENIDO: conserva la ruta exacta del archivo solicitado, pero el campo content debe contener codigo fuente completo y valido. No repitas la ruta ni el nombre del archivo como contenido."
            : "";
        var harnessFailureGuidance = failedAction is AgentAction.ActionBuild or AgentAction.ActionTest or "harness"
            ? "\nCORRECCION DEL HARNESS: build/test falló y el diagnóstico está en los mensajes anteriores. Trabaja únicamente sobre el archivo de tu alcance señalado por el primer error. Si todavía no tienes su contenido actual, léelo; después corrígelo con edit_file o patch. No repitas list_dir ni explores rutas ajenas al error; no vuelvas a crear una ruta existente. Conserva intactas las rutas excluidas y deja que Condor repita el harness. Sigue exactamente los diagnósticos del compilador; si indica que el tipo o miembro es estático, usa Tipo.Miembro y elimina cualquier instancia. No declares done mientras falle."
            : "";
        var structuredProtocolGuidance = string.IsNullOrWhiteSpace(failedAction) ||
                                         string.Equals(failedAction, "respuesta_estructurada", StringComparison.OrdinalIgnoreCase)
            ? " La respuesta anterior no cumplio el protocolo. Devuelve solo un objeto JSON valido, sin markdown, texto previo, arrays de acciones ni envolturas. La propiedad raiz action debe contener exactamente un nombre permitido. Para una lectura usa {\"action\":\"read_file\",\"path\":\"ruta/observada\"}; para crear usa {\"action\":\"create_file\",\"path\":\"ruta/exacta\",\"content\":\"contenido\"}; para un plan usa action=plan, objective, scopePaths, excludedPaths, planItems y riskLevel. Cada planItems agrupa task, expectedResult y verificationStep."
            : "";

        if (planBUsed)
        {
            var maxPlanBIterations = Math.Max(1, _limits.MaxPlanBIterations);
            if (awaitingPlanBAction && checkpoint.PlanBIterationsUsed < maxPlanBIterations)
            {
                var allowedActions = string.Join(", ", AgentEngine.SupportedActions);
                var remainingIterations = maxPlanBIterations - checkpoint.PlanBIterationsUsed;
                effectiveMaxIterations = Math.Max(effectiveMaxIterations, failedIteration + remainingIterations);
                checkpoint.LastDecision = "plan_b_reintento";
                checkpoint.LastError = detail;
                checkpoint.NextAction = "analizar_y_corregir";
                messages.Add(new LlmMessage
                {
                    Role = "user",
                    Content = "La correccion de Plan B no paso la validacion. Resultado real: " + failureContext + redundantObservationGuidance +
                              structuredProtocolGuidance + sourceContentRecoveryGuidance + "\nReanaliza y emite una nueva accion JSON usando exactamente uno de estos nombres permitidos: " + allowedActions +
                              ". Respeta la ruta exacta de la solicitud o una observada y resuelvela desde la raiz; no anadas prefijos no solicitados. Para crear un archivo nuevo usa create_file; para reemplazar el contenido de uno existente usa edit_file; usa patch solo con original y replacement. No inventes acciones combinadas ni repitas la accion rechazada. Quedan " + remainingIterations + " iteracion(es) de Plan B. Si no puedes corregirlo de forma segura, detente con el motivo en reason." + harnessFailureGuidance
                });
                progress?.Report(AgentProgress.Of(
                    AgentPhase.Analyzing,
                    action: "Plan B",
                    iteration: failedIteration,
                    message: "La correccion de Plan B no paso; Condor analiza el error y prepara otra accion valida (" +
                             checkpoint.PlanBIterationsUsed + "/" + maxPlanBIterations + ")."));
                return null;
            }

            awaitingPlanBAction = false;
            checkpoint.LastDecision = "plan_b_fallido";
            checkpoint.LastError = detail;
            checkpoint.NextAction = "detener";
            var message = "Plan B no pudo corregir el fallo. Condor detuvo la tarea para evitar repetir intentos.";
            progress?.Report(AgentProgress.Of(
                AgentPhase.Finalizing,
                action: "Plan B",
                iteration: failedIteration,
                message: message));
            return "Plan B no pudo corregir el fallo inicial. Fallo inicial: " +
                   (planBInitialFailure ?? "sin detalle") + " Recuperacion fallida: " + detail;
        }

        planBUsed = true;
        planBInitialFailure = detail;
        awaitingPlanBAction = true;
        checkpoint.PlanBIterationsUsed = 0;
        effectiveMaxIterations = Math.Max(
            effectiveMaxIterations,
            failedIteration + Math.Max(1, _limits.MaxPlanBIterations));
        checkpoint.LastDecision = "plan_b";
        checkpoint.LastError = detail;
        checkpoint.NextAction = "analizar_y_corregir";

        messages.Add(new LlmMessage
        {
            Role = "user",
            Content = "PLAN B ACTIVADO tras el fallo de la iteracion " + failedIteration +
                      ". " + failureContext + sourceContentRecoveryGuidance +
                      "\nAnaliza ese resultado y corrige la causa antes de continuar. No repitas la misma accion con los mismos datos si ya fallo; observa la ruta o adapta la accion usando la evidencia disponible. " + structuredProtocolGuidance + redundantObservationGuidance +
                      "Emite exactamente una accion JSON con un solo nombre tomado de esta lista: " + string.Join(", ", AgentEngine.SupportedActions) +
                      ". Usa la ruta exacta de la solicitud o una observada, siempre relativa a la raiz; no anadas prefijos ni subdirectorios no solicitados. Para archivo nuevo usa create_file; para existente usa edit_file o patch con original/replacement. Nunca combines nombres con '/'. " +
                      "Si la correccion tiene exito, continua la tarea y verifica el resultado. No declares done antes de ejecutar una accion de recuperacion exitosa; si no existe una correccion segura, detente con el motivo en reason." + harnessFailureGuidance
        });

        progress?.Report(AgentProgress.Of(
            AgentPhase.Analyzing,
            action: "Plan B",
            iteration: failedIteration,
                message: "Plan B activado tras detectar " + failureLabel + "; Condor analiza el resultado y prepara una correccion."));
        return null;
    }

    private static AgentResult ApplyPlanBCompletion(AgentResult result, bool planBUsed)
    {
        if (planBUsed)
        {
            result.Reason = "Plan B recupero la tarea tras el fallo inicial. " + result.Reason;
            if (result.Checkpoint is not null)
                result.Checkpoint.LastDecision = "plan_b_corregido";
        }

        return result;
    }

    private static string HarnessCompletionReason(HarnessVerifyResult result, bool planBUsed)
    {
        var recovery = planBUsed ? "Plan B recupero la tarea tras el fallo inicial. " : "";
        if (!result.ChecksApplicable)
            return recovery + "Tarea completada; build/test no aplican a este proyecto. " +
                   (result.Reason ?? result.Detail);

        return recovery + "El harness confirmo build y pruebas.";
    }

    private static async Task<string> ListRootSnapshotAsync(AgentToolset toolset, string workingDir, CancellationToken ct)
    {
        var step = await toolset.ExecuteAsync(new AgentAction { Action = AgentAction.ActionListDir, Path = "" }, 0, ct);
        return step.ResultPreview ?? "";
    }

    private async Task<AgentModelCall> RequestActionAsync(string model, List<LlmMessage> messages, CancellationToken ct)
    {
        int? maxTokens = null;
        if (_remainingOutputTokens >= 0)
        {
            while (true)
            {
                var remaining = Volatile.Read(ref _remainingOutputTokens);
                if (remaining <= 0)
                    return new AgentModelCall(null, null, OutputTokenBudgetExhausted);
                var reservation = Math.Min(remaining, Math.Max(1, _limits.MaxTokensPerRequest));
                if (Interlocked.CompareExchange(ref _remainingOutputTokens, remaining - reservation, remaining) == remaining)
                {
                    maxTokens = reservation;
                    break;
                }
            }
        }

        var resp = await _llm.CompleteAsync(new LlmRequest
        {
            Model = model,
            Messages = messages,
            MaxTokens = maxTokens,
            RequireJson = true
        }, ct);

        // Fallo a nivel del proveedor (proceso terminado, server no disponible,
        // timeout real). Se devuelve el estado para que el ciclo decida recuperar
        // o detenerse sin gastar mas iteraciones como si el modelo siguiera activo.
        if (!resp.Success && resp.Outcome is not LlmOutcome.Ok)
        {
            return new AgentModelCall(null, resp.Outcome, resp.Error);
        }

        if (!resp.Success)
        {
            return new AgentModelCall(null, null, resp.Error);
        }

        var parsed = AgentActionParser.Parse(resp.Content);
        if (parsed is null)
        {
            return new AgentModelCall(null, LlmOutcome.InvalidResponse, "El modelo devolvio un JSON que no es una accion valida");
        }

        return new AgentModelCall(parsed, null, null);
    }

    private const int ContextCompactionThresholdCharacters = 20_000;
    private const int ContextCompactionRecentCharacters = 8_000;
    private const string CompactedContextMarker = "[MEMORIA DE EJECUCION CONDOR]";

    private static bool CompactConversationContext(
        List<LlmMessage> messages,
        AgentCheckpoint checkpoint,
        IReadOnlyList<AgentStep> steps)
    {
        if (messages.Count <= 2) return false;
        var totalCharacters = messages.Sum(message => message.Content?.Length ?? 0);
        if (totalCharacters <= ContextCompactionThresholdCharacters && messages.Count < 24)
            return false;

        var system = messages[0];
        var initialContext = messages[1];
        var summary = BuildCompactedContextSummary(checkpoint, steps);
        var recentMessages = new List<LlmMessage>();
        var remainingCharacters = ContextCompactionRecentCharacters;

        for (var index = messages.Count - 1; index >= 2 && remainingCharacters > 0; index--)
        {
            var message = messages[index];
            if (message.Content?.StartsWith(CompactedContextMarker, StringComparison.Ordinal) == true)
                continue;

            var content = message.Content ?? "";
            if (content.Length > remainingCharacters)
                content = ClipContext(content, remainingCharacters);
            if (content.Length == 0) continue;

            recentMessages.Add(new LlmMessage { Role = message.Role, Content = content });
            remainingCharacters -= content.Length;
        }

        recentMessages.Reverse();
        messages.Clear();
        messages.Add(system);
        messages.Add(initialContext);
        messages.Add(new LlmMessage { Role = "user", Content = summary });
        messages.AddRange(recentMessages);
        return true;
    }

    private static string BuildCompactedContextSummary(AgentCheckpoint checkpoint, IReadOnlyList<AgentStep> steps)
    {
        var summary = new System.Text.StringBuilder();
        summary.AppendLine(CompactedContextMarker + ": se conserva el estado verificable y los datos recientes; usa esta memoria junto con el objetivo inicial.");
        AppendMemoryField(summary, "Objetivo vigente", checkpoint.Task, 4000);
        AppendMemoryField(summary, "Riesgo", checkpoint.RiskLevel, 200);
        AppendMemoryField(summary, "Alcance autorizado", JoinMemoryItems(checkpoint.DeclaredScope), 1800);
        AppendMemoryField(summary, "Alcance excluido", JoinMemoryItems(checkpoint.ExcludedScope), 1200);
        AppendMemoryField(summary, "Plan", JoinMemoryItems(checkpoint.PlanSteps), 2400);
        AppendMemoryField(summary, "Resultados esperados por tarea", JoinMemoryItems(checkpoint.ExpectedResults), 1800);
        AppendMemoryField(summary, "Verificación prevista", JoinMemoryItems(checkpoint.VerificationSteps), 1800);
        AppendMemoryField(summary, "Pasos completados", JoinMemoryItems(checkpoint.CompletedSteps), 1800);
        AppendMemoryField(summary, "Última decisión", checkpoint.LastDecision, 500);
        AppendMemoryField(summary, "Última acción", checkpoint.LastAction, 500);
        AppendMemoryField(summary, "Último resultado", checkpoint.LastResult, 1000);
        AppendMemoryField(summary, "Estado del harness", checkpoint.HarnessState, 500);
        AppendMemoryField(summary, "Siguiente acción", checkpoint.NextAction, 300);
        AppendMemoryField(summary, "Error pendiente", checkpoint.LastError, 800);
        AppendMemoryField(summary, "Revisiones", $"rondas={checkpoint.ReviewRoundsUsed}; modificaciones pendientes={checkpoint.UnreviewedModifications}; requiere replanteamiento={checkpoint.ReviewReplanRequired}; replanteamiento preparado={checkpoint.ReviewReplanPrepared}", 500);
        AppendMemoryField(summary, "Aprobación pendiente", checkpoint.PendingApprovalDescription, 500);

        summary.AppendLine("Resultados observados más recientes:");
        foreach (var step in steps.TakeLast(8))
        {
            var result = ClipContext(step.ResultPreview ?? "", 350);
            var path = string.IsNullOrWhiteSpace(step.Path) ? "" : " " + step.Path;
            summary.Append("- ").Append(step.Action).Append(path).Append(": ")
                .Append(step.Success ? "correcto" : "fallido");
            if (result.Length > 0) summary.Append(" — ").Append(result.Replace('\r', ' ').Replace('\n', ' '));
            summary.AppendLine();
        }

        return summary.ToString();
    }

    private static string JoinMemoryItems(IEnumerable<string>? values)
    {
        return values is null ? "" : string.Join("; ", values.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static void AppendMemoryField(System.Text.StringBuilder summary, string label, string? value, int limit)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        summary.Append(label).Append(": ").Append(ClipContext(value, limit)).AppendLine();
    }

    private static string ClipContext(string value, int maximumCharacters)
    {
        if (value.Length <= maximumCharacters) return value;
        if (maximumCharacters <= 64) return value[..maximumCharacters];
        var prefixLength = maximumCharacters / 3;
        var suffixLength = maximumCharacters - prefixLength - 24;
        return value[..prefixLength] + "\n...[contenido resumido por Condor]...\n" + value[^suffixLength..];
    }

    private readonly record struct AgentModelCall(AgentAction? Action, LlmOutcome? ProviderFailure, string? Error);

    private readonly record struct AgentModelIterationOutcome(
        AgentModelCall Call,
        AgentResult? Result,
        bool ContinueIteration);

    private async Task<AgentModelIterationOutcome> RequestAgentActionForIterationAsync(
        string model,
        List<LlmMessage> messages,
        int iteration,
        AgentCheckpoint checkpoint,
        List<AgentStep> steps,
        string intention,
        IAgentProgressObserver? progress,
        CancellationToken cancellationToken,
        Func<int, bool> applyPendingSteering)
    {
        if (CompactConversationContext(messages, checkpoint, steps))
        {
            progress?.Report(AgentProgress.Of(
                AgentPhase.Analyzing,
                iteration: iteration,
                message: "Contexto extenso resumido conservando tarea, plan, alcance y evidencia."));
        }

        progress?.Report(AgentProgress.Of(
            AgentPhase.Analyzing,
            iteration: iteration,
            message: "Esperando propuesta",
            modelActivity: model + " · generando propuesta",
            modelWorking: true));
        var call = await RequestActionAsync(model, messages, cancellationToken);
        if (call.Error == OutputTokenBudgetExhausted)
        {
            checkpoint.LastDecision = "limite_tokens_salida";
            checkpoint.NextAction = "detener";
            return new AgentModelIterationOutcome(
                call,
                Fail("El agente agotó su presupuesto de tokens de salida antes de completar la tarea.", model, intention, steps, checkpoint),
                false);
        }
        if (applyPendingSteering(iteration))
            return new AgentModelIterationOutcome(call, null, true);

        var modelActivity = call.Action is { } proposedAction
            ? model + " · propuso " + proposedAction.Action + (string.IsNullOrWhiteSpace(proposedAction.Path) ? "" : " " + proposedAction.Path)
            : model + " · no produjo una acción válida";
        progress?.Report(AgentProgress.Of(
            AgentPhase.Analyzing,
            iteration: iteration,
            message: "Respuesta estructurada recibida",
            modelActivity: modelActivity,
            modelWorking: false));

        // Una respuesta no estructurada es un fallo de protocolo, no una caída del proveedor.
        if (call.ProviderFailure == LlmOutcome.InvalidResponse)
            call = new AgentModelCall(null, null, call.Error);

        if (call.ProviderFailure is not null)
        {
            checkpoint.NextAction = "revisar";
            progress?.Report(AgentProgress.Of(
                AgentPhase.Verifying,
                message: "Modelo local · " + LlmOutcomeLabel(call.ProviderFailure.Value) + " · " + call.Error,
                flag: ProgressFlag.ProviderError,
                modelActivity: model + " · " + LlmOutcomeLabel(call.ProviderFailure.Value),
                modelWorking: false));

            var recovered = await TryRecoverProviderAsync(model, messages, progress, cancellationToken);
            if (recovered is null)
            {
                checkpoint.LastDecision = "detener";
                checkpoint.LastError = BuildProviderFailureReason(call.ProviderFailure.Value, call.Error);
                progress?.Report(AgentProgress.Of(
                    AgentPhase.Finalizing,
                    message: "Modelo local no disponible · Condor detuvo la tarea",
                    flag: ProgressFlag.ProviderError));
                return new AgentModelIterationOutcome(
                    call, Fail(checkpoint.LastError, model, intention, steps, checkpoint), false);
            }

            call = recovered.Value;
            if (call.Error == OutputTokenBudgetExhausted)
            {
                checkpoint.LastDecision = "limite_tokens_salida";
                checkpoint.NextAction = "detener";
                return new AgentModelIterationOutcome(
                    call,
                    Fail("El agente agotó su presupuesto de tokens de salida antes de completar la tarea.", model, intention, steps, checkpoint),
                    false);
            }
            if (call.ProviderFailure == LlmOutcome.InvalidResponse)
                call = new AgentModelCall(null, null, call.Error);

            // Un fallo persistente del proveedor se detiene sin gastar más iteraciones.
            if (call.ProviderFailure is not null && call.Action is null)
            {
                checkpoint.LastDecision = "detener";
                checkpoint.LastError = BuildProviderFailureReason(call.ProviderFailure.Value, call.Error);
                progress?.Report(AgentProgress.Of(
                    AgentPhase.Finalizing,
                    message: "Modelo local no disponible tras reintentos · Condor detuvo la tarea",
                    flag: ProgressFlag.ProviderError));
                return new AgentModelIterationOutcome(
                    call, Fail(checkpoint.LastError, model, intention, steps, checkpoint), false);
            }

            progress?.Report(AgentProgress.Of(
                AgentPhase.Analyzing,
                iteration: iteration,
                message: "Respuesta recuperada del proveedor",
                modelActivity: call.Action is { } recoveredAction
                    ? model + " · propuso " + recoveredAction.Action + (string.IsNullOrWhiteSpace(recoveredAction.Path) ? "" : " " + recoveredAction.Path)
                    : model + " · respuesta sin acción válida",
                modelWorking: false));
        }

        return new AgentModelIterationOutcome(call, null, false);
    }

    /// <summary>Recuperacion limitada del proveedor: comprueba health y reintenta UNA solicitud si el servidor volvio. Devuelve null si no se recupero.</summary>
    private async Task<AgentModelCall?> TryRecoverProviderAsync(string model, List<LlmMessage> messages, IAgentProgressObserver? progress, CancellationToken ct)
    {
        const int maxProbes = 3;

        for (var attempt = 0; attempt < maxProbes; attempt++)
        {
            progress?.Report(AgentProgress.Of(
                AgentPhase.Verifying,
                message: "Modelo local: intentando recuperacion (" + (attempt + 1) + "/" + maxProbes + ")",
                flag: ProgressFlag.Recovering,
                modelActivity: model + " · reintentando conexión (" + (attempt + 1) + "/" + maxProbes + ")",
                modelWorking: true));

            bool available;
            try
            {
                available = await _provider.IsAvailableAsync(ct);
            }
            catch
            {
                available = false;
            }

            if (available)
            {
                // El proveedor volvio: reintentar la solicitud actual una sola vez mas.
                progress?.Report(AgentProgress.Of(
                    AgentPhase.Verifying,
                    message: "Modelo local recuperado; reintentando la solicitud",
                    flag: ProgressFlag.Recovering));
                var retried = await RequestActionAsync(model, messages, ct);
                return retried;
            }

            // Pequena pausa antes de volver a comprobar.
            try
            {
                await Task.Delay(700, ct);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        return null;
    }

    private static string LlmOutcomeLabel(LlmOutcome outcome)
    {
        return outcome switch
        {
            LlmOutcome.Thinking => "modelo pensando (respuesta lenta)",
            LlmOutcome.ServerUnavailable => "servidor temporalmente no disponible",
            LlmOutcome.ProcessEnded => "proceso del modelo terminado inesperadamente",
            LlmOutcome.Timeout => "timeout real de la solicitud",
            LlmOutcome.InvalidResponse => "respuesta invalida del modelo",
            _ => outcome.ToString()
        };
    }

    private static string BuildProviderFailureReason(LlmOutcome outcome, string? detail)
    {
        var cause = LlmOutcomeLabel(outcome);
        var detailText = string.IsNullOrWhiteSpace(detail) ? "" : " (" + detail + ")";
        return "El proveedor del modelo fallo: " + cause + detailText + ". Condor detuvo la tarea para no consumir mas iteraciones. Revisa el diagnostico del servidor local.";
    }

    /// <summary>
    /// Construye el mensaje claro de "MODELO NO EJECUTABLE" por presupuesto de
    /// memoria. Incluye modelo (si se conoce), RAM requerida estimada, RAM
    /// disponible y el motivo exacto. Es la salida controlada ante RAM insuficiente
    /// (no se pide al usuario liberar memoria como mecanismo normal, no hay loop).
    /// </summary>
    private async Task<AgentResult> HandleUnavailableModelSelectionAsync(
        Condor.Core.Models.ModelSelectionResult selection,
        string intention,
        List<AgentStep> steps,
        AgentCheckpoint checkpoint,
        IAgentProgressObserver? progress,
        CancellationToken cancellationToken)
    {
        // Hay un modelo activo, pero la tarea requiere un upgrade que no cabe en el presupuesto.
        var currentActiveModel = _session?.ActiveModel;
        if (currentActiveModel is not null && selection.BlockedByResources)
        {
            progress?.Report(AgentProgress.Of(
                AgentPhase.Verifying,
                message: "Memoria insuficiente: ninguna alternativa compatible cabe en el presupuesto seguro.",
                resourceState: selection.Resources?.PressureLabel,
                availableGb: selection.Resources?.FreeGb,
                safeBudgetGb: selection.Budget?.BudgetGb,
                flag: ProgressFlag.Normal));

            checkpoint.LastDecision = "memoria_insuficiente";
            checkpoint.LastError = "Memoria insuficiente para el upgrade requerido.";
            return new AgentResult
            {
                Success = false,
                Reason = "Memoria insuficiente para continuar con esta tarea. Libera memoria y vuelve a intentarlo.",
                Model = currentActiveModel,
                Objective = intention,
                Steps = steps,
                Checkpoint = checkpoint,
                Inventory = await BuildInventoryAsync(selection, cancellationToken).ConfigureAwait(false)
            };
        }

        progress?.Report(AgentProgress.Of(
            AgentPhase.Analyzing,
            message: "No hay un modelo compatible que pueda cargarse con seguridad.",
            resourceState: selection.Resources?.PressureLabel,
            availableGb: selection.Resources?.FreeGb,
            safeBudgetGb: selection.Budget?.BudgetGb));
        return Fail(BuildModelNotExecutableReason(selection), "", intention, steps, checkpoint);
    }

    private static string BuildModelNotExecutableReason(Condor.Core.Models.ModelSelectionResult selection)
    {
        var budget = selection.Budget;
        var resources = selection.Resources;
        // Referencia del modelo: el minimo suficiente para la tarea que no cabe
        // (bloqueo por recursos) o el deseado si existe.
        var reference = selection.Desired ?? selection.MinimumViable;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("MODELO NO EJECUTABLE");
        var model = reference is not null ? reference.PullName : "ninguno (sin modelo compatible)";
        sb.AppendLine("Modelo: " + model);
        sb.AppendLine("RAM requerida estimada: " +
                      (reference is null ? "-" : ModelEfficiencyEvaluator.PeakGb(reference).ToString("0.0")) + " GB");
        sb.AppendLine("RAM disponible: " + (resources?.FreeGb.ToString("0.0") ?? "-") + " GB");
        sb.AppendLine("Motivo: presupuesto de memoria insuficiente.");
        if (budget is not null && budget.IsBudgeted)
        {
            sb.AppendLine("Presupuesto permitido: " + budget.BudgetGb.ToString("0.0") + " GB · reserva operativa: " + budget.ReserveGb.ToString("0.0") + " GB.");
        }
        else if (resources is not null)
        {
            sb.AppendLine("Estado de recursos: " + (resources.PressureLabel ?? "sin datos") + ".");
        }

        if (resources is not null && resources.TopConsumers.Count > 0)
        {
            var top = string.Join(", ", resources.TopConsumers.Select(c => c.ProcessName + " ~" + c.WorkingSetGb.ToString("0.0") + " GB"));
            sb.AppendLine("Consumidores actuales de RAM (solo informativo): " + top + ".");
        }

        sb.AppendLine("Accion: se ha detenido de forma controlada; no se descarga ni carga un modelo que no pueda ejecutarse de forma segura.");
        return sb.ToString();
    }

    private static string? VerifyTestIntegrity(
        string workingDir,
        RepoSnapshot snapshot,
        IReadOnlyList<string>? authorizedScope,
        IReadOnlyList<string>? excludedScope)
    {
        var changed = snapshot.ChangedTestFiles(workingDir)
            .Where(path => !AgentEngine.IsPathWithinScope(path, authorizedScope, excludedScope))
            .ToList();
        if (changed.Count == 0)
        {
            return null;
        }

        return "El agente modifico archivos de prueba (" + string.Join(", ", changed) +
               "). Condor no puede confirmar exito contra las pruebas originales; revierte los cambios de prueba (undo_file) y corrige el codigo de produccion en lugar de las pruebas.";
    }

    private static ResourceSnapshot? EvaluateResources()
    {
        try
        {
            var memory = new MemoryDetector().DetectAsync(CancellationToken.None).GetAwaiter().GetResult();
            var consumers = new ProcessRamDetector().DetectTopConsumers();
            return ModelMemoryBudget.Snapshot(memory, candidatePeakGb: null, consumers);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Recopila el inventario objetivo del entorno y de la decision de modelo
    /// (recursos, CPU, almacenamiento, modelos instalados, modelo seleccionado,
    /// motivo y capacidades verificadas del catalogo). Solo usa datos reales
    /// detectados o del catalogo; nunca inventa capacidades.
    /// </summary>
    private async Task<AgentInventory?> BuildInventoryAsync(
        Condor.Core.Models.ModelSelectionResult selection,
        CancellationToken ct)
    {
        try
        {
            var resources = selection.Resources;
            var desired = selection.Desired;
            var budget = selection.Budget;
            var inventory = CreateBaseInventory(selection, resources, desired, budget);

            await PopulateCpuInventoryAsync(inventory, ct);
            await PopulateInstalledModelsAsync(inventory, ct);

            var storage = await new StorageDetector().DetectAsync(ct);
            var disks = storage.Disks;
            inventory.FreeDiskGb = disks is { Count: > 0 }
                ? System.Math.Round(disks.Max(d => d.FreeBytes) / (double)ModelMemoryBudget.BytesPerGb, 1)
                : 0;

            SetInventorySelectionDetails(inventory, selection, desired);
            return inventory;
        }
        catch
        {
            return null;
        }
    }

    private static AgentInventory CreateBaseInventory(
        Condor.Core.Models.ModelSelectionResult selection,
        Condor.Core.Models.ResourceSnapshot? resources,
        Condor.Core.Models.ModelCandidate? desired,
        Condor.Core.Models.BudgetAssessment? budget)
        => new()
        {
            RamTotalGb = resources?.TotalGb ?? (budget?.RamTotalGb ?? 0),
            RamFreeGb = resources?.FreeGb ?? (budget?.RamFreeGb ?? 0),
            SafeBudgetGb = budget?.BudgetGb ?? (resources?.SafeBudgetGb ?? 0),
            EstimatedModelRamGb = desired is null ? 0 : System.Math.Round(ModelEfficiencyEvaluator.PeakGb(desired), 1),
            PressureLabel = resources?.PressureLabel,
            ReserveGb = budget?.ReserveGb ?? 0,
            OperationalReserveGb = budget?.OperationalReserveGb ?? 0,
            BudgetGb = budget?.BudgetGb ?? 0,
            NodeInCurrent = selection.NodeInCurrent?.PullName,
            NextCandidate = selection.NextCandidate?.PullName
        };

    private static async Task PopulateCpuInventoryAsync(AgentInventory inventory, CancellationToken ct)
    {
        try
        {
            var cpu = await new CpuDetector().DetectAsync(ct);
            if (cpu.Status != DetectionStatus.Detected)
                return;

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(cpu.Name)) parts.Add(cpu.Name);
            if (cpu.Cores > 0) parts.Add(cpu.Cores + " nucleos");
            if (cpu.LogicalProcessors > 0) parts.Add(cpu.LogicalProcessors + " hebras");
            inventory.Cpu = parts.Count > 0 ? string.Join(" · ", parts) : null;
        }
        catch { /* CPU opcional */ }
    }

    private async Task PopulateInstalledModelsAsync(AgentInventory inventory, CancellationToken ct)
    {
        try
        {
            if (_assessmentService is null)
                return;

            var live = await _assessmentService.ExecuteAsync(new AssessmentRequest(), ct);
            inventory.InstalledModels = live.Tools?.Ollama?.Models?.Select(m => m.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name)).ToList();
        }
        catch { /* modelos opcional */ }
    }

    private static void SetInventorySelectionDetails(
        AgentInventory inventory,
        Condor.Core.Models.ModelSelectionResult selection,
        Condor.Core.Models.ModelCandidate? desired)
    {
        inventory.SelectedModel = selection.InstalledName ?? desired?.PullName;
        inventory.SelectionReason = selection.Reason;
        inventory.ModelCapabilities = desired?.Capabilities?.Any() == true ? desired.Capabilities : null;
    }

    private static void EvaluateResourcesAndWarn(AgentCheckpoint checkpoint, IAgentProgressObserver? progress, ref bool warned, int iteration)
    {
        var snapshot = EvaluateResources();
        if (snapshot is null)
        {
            return;
        }

        checkpoint.ResourcesPressure = snapshot.PressureLabel;
        checkpoint.HeadroomGb = snapshot.HeadroomGb;

        if (snapshot.Pressure is ResourcePressure.Normal or ResourcePressure.Adjusted || warned)
        {
            return;
        }

        var builder = new System.Text.StringBuilder("Presion de memoria: " + snapshot.FreeGb + " GB libres · estado " + snapshot.PressureLabel + ".");
        builder.Append(" Condor reducira temporalmente su carga.");

        if (snapshot.TopConsumers.Count > 0)
        {
            builder.Append(" Consumidores relevantes: ");
            for (var i = 0; i < snapshot.TopConsumers.Count; i++)
            {
                var c = snapshot.TopConsumers[i];
                builder.Append(c.ProcessName + " ~" + c.WorkingSetGb + " GB" + (i < snapshot.TopConsumers.Count - 1 ? ", " : "."));
            }

            builder.Append(" Cerrar aplicaciones no necesarias podria liberar memoria (Condor no cierra procesos).");
        }

        warned = true;
        progress?.Report(AgentProgress.Of(
            AgentPhase.Verifying,
            message: builder.ToString(),
            iteration: iteration,
            flag: ProgressFlag.Recovering));
    }

    private static string? FindManifest(string workingDirectory)
    {
        try
        {
            string? f = Directory.EnumerateFiles(workingDirectory, "*.slnx", SearchOption.AllDirectories).FirstOrDefault()
                ?? Directory.EnumerateFiles(workingDirectory, "*.sln", SearchOption.AllDirectories).FirstOrDefault()
                ?? Directory.EnumerateFiles(workingDirectory, "*.csproj", SearchOption.AllDirectories)
                    .Where(p => !p.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) &&
                                !p.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
                    .FirstOrDefault();
            return f is null ? null : Path.GetFileName(f);
        }
        catch { return null; }
    }

    private static AgentResult Fail(string reason, string model, string objective, List<AgentStep> steps, AgentCheckpoint checkpoint)
        => new() { Success = false, Reason = reason, Model = model, Objective = objective, Steps = steps, Checkpoint = checkpoint };

    private static string Truncate(string s) => s is { Length: > 2000 } ? s.Substring(0, 2000) + " …" : (s ?? "");
}

