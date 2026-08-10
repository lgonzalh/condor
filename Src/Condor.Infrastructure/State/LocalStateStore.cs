using System.Text;
using Condor.Core.Contracts;
using Condor.Core.Models;
using Condor.Core.Serialization;

namespace Condor.Infrastructure.State;

public class LocalStateStore : IStateStore
{
    private readonly string _stateDirectory;

    public LocalStateStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Condor",
            "state"))
    {
    }

    public LocalStateStore(string stateDirectory)
    {
        _stateDirectory = stateDirectory;
    }

    public async Task SaveAssessmentAsync(
        AssessmentResult result,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_stateDirectory);
        var filePath = Path.Combine(_stateDirectory, "assessment.json");
        var json = AssessmentJson.Serialize(result);
        await File.WriteAllTextAsync(filePath, json, new UTF8Encoding(false), cancellationToken);
    }
}
