using System;

namespace Condor.Core.Models
{
    public class RelevantDependency
    {
        public string Name { get; set; } = "";
        public string Source { get; set; } = "";
        public string? Detail { get; set; }
    }
}
