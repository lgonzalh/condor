using System;
using System.Collections.Generic;

namespace Condor.Core.Models
{
    public class ContinuationPoint
    {
        public DetectionStatus Status { get; set; } = DetectionStatus.NotDetected;
        public List<string> Evidence { get; set; } = new();
        public string? LastActivity { get; set; }
        public List<string> PendingWork { get; set; } = new();
        public string? SuggestedNext { get; set; }
        public string? Reason { get; set; }
    }
}
