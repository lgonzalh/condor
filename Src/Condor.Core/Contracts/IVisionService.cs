using Condor.Core.Models;

namespace Condor.Core.Contracts;

public interface IVisionService
{
    Task<VisionResult> ExamineAsync(string imagePath, CancellationToken cancellationToken = default);
}
