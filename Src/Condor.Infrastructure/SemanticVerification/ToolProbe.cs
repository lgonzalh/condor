using System.Linq;
using Condor.Core.Models;

namespace Condor.Infrastructure.SemanticVerification;

public sealed class ToolProbe
{
    public bool HasDotNet(AssessmentResult? assessment)
    {
        return (assessment?.Tools?.DetectedTools ?? new System.Collections.Generic.List<ToolInfo>())
            .Any(t => string.Equals(t.Name, "dotnet", System.StringComparison.OrdinalIgnoreCase) &&
                      t.Status == DetectionStatus.Detected);
    }
}
