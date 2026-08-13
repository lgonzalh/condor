using System.Collections.Generic;

namespace Condor.Core.Models
{
    public class PlanTask
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string? Detail { get; set; }
        public List<string> DependsOn { get; set; } = new();
        public string Priority { get; set; } = "media";
        public string Evidence { get; set; } = "";
    }
}
