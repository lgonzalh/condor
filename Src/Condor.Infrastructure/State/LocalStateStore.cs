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
                var json = ContextJson.Serialize(context);
                await File.WriteAllTextAsync(filePath, json, new UTF8Encoding(false), cancellationToken);
            }
            catch
            {
            }
        }
    }
}
