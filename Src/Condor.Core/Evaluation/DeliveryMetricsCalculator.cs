using Condor.Core.Models;

namespace Condor.Core.Evaluation;

public sealed record DeliveryMetricsSummary(
    int DeliveredChangeCount,
    int DeliveriesInLast30Days,
    double WeeklyDeploymentFrequency,
    TimeSpan? AverageChangeLeadTime,
    int FailedChangeCount,
    double? ChangeFailureRate,
    TimeSpan? AverageRecoveryTime,
    int UnrecoveredIncidentCount,
    int ReworkedChangeCount,
    double? DeploymentReworkRate,
    IReadOnlyList<string> ReworkReasons);

public static class DeliveryMetricsCalculator
{
    private static readonly TimeSpan ReportingWindow = TimeSpan.FromDays(30);

    public static DeliveryMetricsSummary Summarize(DeliveryMetricsState state, DateTimeOffset now)
    {
        var delivered = state.Changes
            .Where(change => change.DeliveredAtUtc is not null)
            .ToList();
        var recentDeliveries = delivered.Count(change =>
            change.DeliveredAtUtc >= now - ReportingWindow && change.DeliveredAtUtc <= now);
        var leadTimes = delivered
            .Where(change => change.StartedAtUtc is not null && change.DeliveredAtUtc >= change.StartedAtUtc)
            .Select(change => change.DeliveredAtUtc!.Value - change.StartedAtUtc!.Value)
            .ToList();
        var failures = delivered
            .Where(change => change.FailedAtUtc is not null && change.FailedAtUtc >= change.DeliveredAtUtc)
            .ToList();
        var recovered = failures
            .Where(change => change.RecoveredAtUtc is not null && change.RecoveredAtUtc >= change.FailedAtUtc)
            .ToList();
        var recoveryTimes = recovered
            .Select(change => change.RecoveredAtUtc!.Value - change.FailedAtUtc!.Value)
            .ToList();
        var reworked = delivered.Where(change => change.ReworkAtUtc is not null).ToList();

        return new DeliveryMetricsSummary(
            delivered.Count,
            recentDeliveries,
            recentDeliveries * 7d / 30d,
            Average(leadTimes),
            failures.Count,
            Rate(failures.Count, delivered.Count),
            Average(recoveryTimes),
            failures.Count - recovered.Count,
            reworked.Count,
            Rate(reworked.Count, delivered.Count),
            reworked.Select(change => $"{change.ChangeId}: {change.ReworkReason}").ToList());
    }

    private static double? Rate(int numerator, int denominator) =>
        denominator == 0 ? null : numerator * 100d / denominator;

    private static TimeSpan? Average(IReadOnlyCollection<TimeSpan> values) =>
        values.Count == 0 ? null : TimeSpan.FromTicks((long)values.Average(value => value.Ticks));
}
