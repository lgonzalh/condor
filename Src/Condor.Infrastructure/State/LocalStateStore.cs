using System;
using System.Text;
using System.Threading.Tasks;
using Condor.Core.Contracts;
using Condor.Core.Models;
using Condor.Core.Serialization;

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
    }
}
