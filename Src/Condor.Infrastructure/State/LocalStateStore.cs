using System;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using System.Linq;
using System.Threading.Tasks;
using Condor.Core.Contracts;
using Condor.Core.Models;
using Condor.Core.Serialization;
using Condor.Infrastructure.Security;

namespace Condor.Infrastructure.State
{
    public sealed class LocalStateStore : IStateStore
    {
        private const string AssessmentFileName = "assessment.json";
        private const string ContextFileName = "context.json";
        private const string PlanFileName = "plan.json";
        private const string BuildFileName = "build.json";
        private const string VerificationFileName = "verification.json";
        private const string CycleFileName = "cycle.json";
        private const string VisionFileName = "vision.json";
        private const string SemanticVerificationFileName = "verificacion_semantica.json";
        private const string DeliveryMetricsFileName = "metricas-entrega.json";
        private readonly string _stateDirectory;

        public LocalStateStore()
        {
            _stateDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Condor", "state");

            if (!Directory.Exists(_stateDirectory))
            {
                Directory.CreateDirectory(_stateDirectory);
            }
        }

        public LocalStateStore(string stateDirectory)
        {
            _stateDirectory = stateDirectory;
        }

        public async Task<AssessmentResult?> LoadAssessmentAsync(CancellationToken cancellationToken = default)
        {
            var filePath = Path.Combine(_stateDirectory, AssessmentFileName);

            if (!File.Exists(filePath))
            {
                return null;
            }

            try
            {
                var json = await File.ReadAllTextAsync(filePath, cancellationToken);
                return AssessmentJson.Deserialize(json);
            }
            catch
            {
                return null;
            }
        }

        public async Task SaveAssessmentAsync(AssessmentResult result, CancellationToken cancellationToken = default)
        {
            var filePath = Path.Combine(_stateDirectory, AssessmentFileName);

            try
            {
                Directory.CreateDirectory(_stateDirectory);
                var json = AssessmentJson.Serialize(result);
                await File.WriteAllTextAsync(filePath, json, new UTF8Encoding(false), cancellationToken);
            }
            catch
            {
            }
        }

        public async Task<ProjectContext?> LoadContextAsync(CancellationToken cancellationToken = default)
        {
            var filePath = Path.Combine(_stateDirectory, ContextFileName);

            if (!File.Exists(filePath))
            {
                return null;
            }

            try
            {
                var json = await File.ReadAllTextAsync(filePath, cancellationToken);
                return ContextJson.Deserialize(json);
            }
            catch
            {
                return null;
            }
        }

        public async Task SaveContextAsync(ProjectContext context, CancellationToken cancellationToken = default)
        {
            var filePath = Path.Combine(_stateDirectory, ContextFileName);

            try
            {
                Directory.CreateDirectory(_stateDirectory);
                var json = ContextJson.Serialize(context);
                await File.WriteAllTextAsync(filePath, json, new UTF8Encoding(false), cancellationToken);
            }
            catch
            {
            }
        }

        public async Task<WorkPlan?> LoadPlanAsync(CancellationToken cancellationToken = default)
        {
            var filePath = Path.Combine(_stateDirectory, PlanFileName);

            if (!File.Exists(filePath))
            {
                return null;
            }

            try
            {
                var json = await File.ReadAllTextAsync(filePath, cancellationToken);
                return PlanJson.Deserialize(json);
            }
            catch
            {
                return null;
            }
        }

        public async Task SavePlanAsync(WorkPlan plan, CancellationToken cancellationToken = default)
        {
            var filePath = Path.Combine(_stateDirectory, PlanFileName);

            try
            {
                Directory.CreateDirectory(_stateDirectory);
                var json = PlanJson.Serialize(plan);
                await File.WriteAllTextAsync(filePath, json, new UTF8Encoding(false), cancellationToken);
            }
            catch
            {
            }
        }

        public async Task<BuildResult?> LoadBuildAsync(CancellationToken cancellationToken = default)
        {
            var filePath = Path.Combine(_stateDirectory, BuildFileName);

            if (!File.Exists(filePath))
            {
                return null;
            }

            try
            {
                var json = await File.ReadAllTextAsync(filePath, cancellationToken);
                return BuildJson.Deserialize(json);
            }
            catch
            {
                return null;
            }
        }

        public async Task SaveBuildAsync(BuildResult result, CancellationToken cancellationToken = default)
        {
            var filePath = Path.Combine(_stateDirectory, BuildFileName);

            try
            {
                Directory.CreateDirectory(_stateDirectory);
                var json = BuildJson.Serialize(result);
                await File.WriteAllTextAsync(filePath, json, new UTF8Encoding(false), cancellationToken);
            }
            catch
            {
            }
        }

        public async Task<VerificationResult?> LoadVerificationAsync(CancellationToken cancellationToken = default)
        {
            var filePath = Path.Combine(_stateDirectory, VerificationFileName);

            if (!File.Exists(filePath))
            {
                return null;
            }

            try
            {
                var json = await File.ReadAllTextAsync(filePath, cancellationToken);
                return VerificationJson.Deserialize(json);
            }
            catch
            {
                return null;
            }
        }

        public async Task SaveVerificationAsync(VerificationResult result, CancellationToken cancellationToken = default)
        {
            var filePath = Path.Combine(_stateDirectory, VerificationFileName);

            try
            {
                Directory.CreateDirectory(_stateDirectory);
                var json = VerificationJson.Serialize(result);
                await File.WriteAllTextAsync(filePath, json, new UTF8Encoding(false), cancellationToken);
            }
            catch
            {
            }
        }

        public async Task<CycleResult?> LoadCycleAsync(CancellationToken cancellationToken = default)
        {
            var filePath = Path.Combine(_stateDirectory, CycleFileName);

            if (!File.Exists(filePath))
            {
                return null;
            }

            try
            {
                var json = await File.ReadAllTextAsync(filePath, cancellationToken);
                return CycleJson.Deserialize(json);
            }
            catch
            {
                return null;
            }
        }

        public async Task SaveCycleAsync(CycleResult result, CancellationToken cancellationToken = default)
        {
            var filePath = Path.Combine(_stateDirectory, CycleFileName);

            try
            {
                Directory.CreateDirectory(_stateDirectory);
                var json = CycleJson.Serialize(result);
                await File.WriteAllTextAsync(filePath, json, new UTF8Encoding(false), cancellationToken);
            }
            catch
            {
            }
        }

        public async Task<VisionResult?> LoadVisionAsync(CancellationToken cancellationToken = default)
        {
            var filePath = Path.Combine(_stateDirectory, VisionFileName);

            if (!File.Exists(filePath))
            {
                return null;
            }

            try
            {
                var json = await File.ReadAllTextAsync(filePath, cancellationToken);
                return VisionJson.Deserialize(json);
            }
            catch
            {
                return null;
            }
        }

        public async Task SaveVisionAsync(VisionResult result, CancellationToken cancellationToken = default)
        {
            var filePath = Path.Combine(_stateDirectory, VisionFileName);

            try
            {
                Directory.CreateDirectory(_stateDirectory);
                var json = VisionJson.Serialize(result);
                await File.WriteAllTextAsync(filePath, json, new UTF8Encoding(false), cancellationToken);
            }
            catch
            {
            }
        }

        public async Task<SemanticVerificationResult?> LoadSemanticVerificationAsync(CancellationToken cancellationToken = default)
        {
            var filePath = Path.Combine(_stateDirectory, SemanticVerificationFileName);

            if (!File.Exists(filePath))
            {
                return null;
            }

            try
            {
                var json = await File.ReadAllTextAsync(filePath, cancellationToken);
                return SemanticVerificationJson.Deserialize(json);
            }
            catch
            {
                return null;
            }
        }

        public async Task SaveSemanticVerificationAsync(SemanticVerificationResult result, CancellationToken cancellationToken = default)
        {
            var filePath = Path.Combine(_stateDirectory, SemanticVerificationFileName);

            try
            {
                Directory.CreateDirectory(_stateDirectory);
                var json = SemanticVerificationJson.Serialize(result);
                await File.WriteAllTextAsync(filePath, json, new UTF8Encoding(false), cancellationToken);
            }
            catch
            {
            }
        }

        public async Task<bool> SaveAgentCheckpointAsync(string workingDirectory, AgentCheckpoint checkpoint, CancellationToken cancellationToken = default)
        {
            try
            {
                Directory.CreateDirectory(_stateDirectory);
                var filePath = AgentCheckpointPath(workingDirectory);
                var temporaryPath = filePath + ".tmp";
                var safeCheckpoint = new AgentCheckpoint
                {
                    SchemaVersion = checkpoint.SchemaVersion,
                    Task = Redact(checkpoint.Task) ?? string.Empty,
                    WorkingDirectory = Path.GetFullPath(workingDirectory),
                    Iteration = checkpoint.Iteration,
                    Model = checkpoint.Model,
                    Strategy = checkpoint.Strategy,
                    LastDecision = Redact(checkpoint.LastDecision),
                    LastAction = Redact(checkpoint.LastAction),
                    LastResult = string.IsNullOrWhiteSpace(checkpoint.LastResult) ? null : "Resultado detallado omitido del punto de continuidad.",
                    HarnessState = checkpoint.HarnessState,
                    LastError = Redact(checkpoint.LastError),
                    NextAction = Redact(checkpoint.NextAction),
                    RiskLevel = checkpoint.RiskLevel,
                    DeclaredScope = checkpoint.DeclaredScope?.ToList(),
                    ExcludedScope = checkpoint.ExcludedScope?.ToList(),
                    PlanSteps = checkpoint.PlanSteps?.Select(value => Redact(value) ?? string.Empty).ToList(),
                    ExpectedResults = checkpoint.ExpectedResults?.Select(value => Redact(value) ?? string.Empty).ToList(),
                    VerificationSteps = checkpoint.VerificationSteps?.Select(value => Redact(value) ?? string.Empty).ToList(),
                    CompletedSteps = checkpoint.CompletedSteps.Select(value => Redact(value) ?? string.Empty).ToList(),
                    PlanBIterationsUsed = checkpoint.PlanBIterationsUsed,
                    ReviewRoundsUsed = checkpoint.ReviewRoundsUsed,
                    UnreviewedModifications = checkpoint.UnreviewedModifications,
                    ReviewReplanRequired = checkpoint.ReviewReplanRequired,
                    ReviewReplanPrepared = checkpoint.ReviewReplanPrepared,
                    ResourcesPressure = checkpoint.ResourcesPressure,
                    HeadroomGb = checkpoint.HeadroomGb,
                    GeneratedAtUtc = DateTime.UtcNow
                };
                var json = JsonSerializer.Serialize(safeCheckpoint, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(temporaryPath, json, new UTF8Encoding(false), cancellationToken);
                File.Move(temporaryPath, filePath, overwrite: true);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<AgentCheckpoint?> LoadAgentCheckpointAsync(string workingDirectory, CancellationToken cancellationToken = default)
        {
            try
            {
                var filePath = AgentCheckpointPath(workingDirectory);
                if (!File.Exists(filePath)) return null;
                var json = await File.ReadAllTextAsync(filePath, cancellationToken);
                var checkpoint = JsonSerializer.Deserialize<AgentCheckpoint>(json);
                return checkpoint is null || checkpoint.NextAction == "entregar" ? null : checkpoint;
            }
            catch
            {
                return null;
            }
        }

        public async Task<DeliveryMetricsState> LoadDeliveryMetricsAsync(string workingDirectory, CancellationToken cancellationToken = default)
        {
            var filePath = DeliveryMetricsPath(workingDirectory);
            if (!File.Exists(filePath)) return new DeliveryMetricsState();

            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var state = JsonSerializer.Deserialize<DeliveryMetricsState>(json);
            return state is null || state.Changes is null
                ? new DeliveryMetricsState()
                : state;
        }

        public async Task SaveDeliveryMetricsAsync(string workingDirectory, DeliveryMetricsState state, CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(_stateDirectory);
            var safeState = new DeliveryMetricsState
            {
                Changes = state.Changes.Select(change => new DeliveryChangeRecord
                {
                    ChangeId = change.ChangeId,
                    StartedAtUtc = change.StartedAtUtc,
                    DeliveredAtUtc = change.DeliveredAtUtc,
                    FailedAtUtc = change.FailedAtUtc,
                    FailureReason = Redact(change.FailureReason),
                    RecoveredAtUtc = change.RecoveredAtUtc,
                    ReworkAtUtc = change.ReworkAtUtc,
                    ReworkReason = Redact(change.ReworkReason),
                    RelatedTaskExecutionIds = (change.RelatedTaskExecutionIds ?? new List<string>()).Take(256).ToList(),
                    IncidentTaskExecutionIds = (change.IncidentTaskExecutionIds ?? new List<string>()).Take(256).ToList()
                }).ToList()
            };
            var filePath = DeliveryMetricsPath(workingDirectory);
            var temporaryPath = filePath + ".tmp";
            var json = JsonSerializer.Serialize(safeState, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(temporaryPath, json, new UTF8Encoding(false), cancellationToken);
            File.Move(temporaryPath, filePath, overwrite: true);
        }

        private string AgentCheckpointPath(string workingDirectory)
        {
            var root = Path.GetFullPath(workingDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var identity = OperatingSystem.IsWindows() ? root.ToUpperInvariant() : root;
            var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
            return Path.Combine(_stateDirectory, "agent-checkpoint-" + key + ".json");
        }

        private string DeliveryMetricsPath(string workingDirectory)
        {
            var root = Path.GetFullPath(workingDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var identity = OperatingSystem.IsWindows() ? root.ToUpperInvariant() : root;
            var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
            return Path.Combine(_stateDirectory, "metricas-entrega-" + key + ".json");
        }

        private static string? Redact(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;
            return SensitiveTextRedactor.Redact(value);
        }
    }
}
