using System;
using System.IO;
using System.Threading.Tasks;
using Condor.Core.Contracts;
using Condor.Core.Models;
using Condor.Core.Setup;

namespace Condor.Infrastructure.Setup;

public sealed class SetupService : ISetupService
{
    private const string ReasonTimeout = "Tiempo excedido al preparar el entorno.";

    private readonly IStateStore _stateStore;
    private readonly IAssessmentService? _assessmentService;
    private readonly SetupLimits _limits;
    private readonly StateDirectoryProbe _probe;
    private readonly string _stateDirectory;

    public SetupService(
        IStateStore stateStore,
        IAssessmentService? assessmentService = null,
        SetupLimits? limits = null,
        string? stateDirectory = null)
    {
        _stateStore = stateStore;
        _assessmentService = assessmentService;
        _limits = limits ?? SetupLimits.Default;
        _probe = new StateDirectoryProbe();
        _stateDirectory = stateDirectory ?? DefaultStateDirectory();
    }

    public async Task<SetupResult> PrepareAsync(
        bool refreshAssessment = false,
        CancellationToken cancellationToken = default)
    {
        var timeout = TimeSpan.FromMilliseconds(_limits.SetupTimeoutMilliseconds);

        try
        {
            AssessmentResult? assessment = await _stateStore
                .LoadAssessmentAsync(cancellationToken)
                .WaitAsync(timeout, cancellationToken);

            if ((assessment is null || refreshAssessment) && _assessmentService is not null)
            {
                assessment = await _assessmentService
                    .ExecuteAsync(new AssessmentRequest(), cancellationToken)
                    .WaitAsync(timeout, cancellationToken);
                await _stateStore.SaveAssessmentAsync(assessment, cancellationToken);
            }

            var stateDirectory = _stateDirectory;
            var state = _probe.Probe(stateDirectory);

            return SetupEvaluator.Evaluate(
                assessment,
                stateDirectory,
                state.Exists,
                state.Reason is null,
                state.Reason,
                _limits);
        }
        catch (TimeoutException)
        {
            return SetupResultTimeout();
        }
    }

    private static string DefaultStateDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Condor", "state");
    }

    private static SetupResult SetupResultTimeout()
    {
        return new SetupResult
        {
            SchemaVersion = "1.0.0",
            Status = DetectionStatus.Limited,
            Reason = ReasonTimeout,
            Platform = "windows",
            GeneratedAtUtc = DateTime.UtcNow
        };
    }
}
