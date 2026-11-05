using Condor.Cli.Commands;
using Condor.Cli.Routing;
using Condor.Cli.Tui;
using Condor.Core.Evaluation;
using Condor.Core.Models;
using Condor.Infrastructure.State;

namespace Condor.Cli.Tests;

public sealed class DeliveryMetricsCommandTests
{
    [Fact]
    public void Route_MetricasConArgumentos_SeReconoceComoComando()
    {
        var route = Assert.IsType<SlashRoute>(IntentionRouter.Route("/metricas iniciar cambio-123"));

        Assert.Equal(SlashCommandKind.Metricas, route.Kind);
        Assert.Equal(new[] { "iniciar", "cambio-123" }, route.Arguments);
        Assert.Contains("/metricas", TuiInput.CommandPaletteCommands);
        Assert.Equal(SlashCommandKind.EvaluarRevisor, Assert.IsType<SlashRoute>(IntentionRouter.Route("/evaluar-revisor")).Kind);
        Assert.Contains("/evaluar-revisor", TuiInput.CommandPaletteCommands);
    }

    [Fact]
    public async Task ExecuteAsync_RegistraCicloManual_YCalculaEstadoPersistidoPorProyecto()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "condor-metricas-" + Guid.NewGuid().ToString("N")));
        var stateRoot = Path.Combine(root, "state");
        var project = Path.Combine(root, "proyecto-a");
        var otherProject = Path.Combine(root, "proyecto-b");
        Directory.CreateDirectory(project);
        Directory.CreateDirectory(otherProject);
        var store = new LocalStateStore(stateRoot);

        try
        {
            Assert.Equal(0, await DeliveryMetricsCommand.ExecuteAsync(store, new[] { "iniciar", "cambio-123", "2026-10-30T10:00:00Z" }, workingDirectory: project));
            Assert.Equal(0, await DeliveryMetricsCommand.ExecuteAsync(store, new[] { "entregar", "cambio-123", "2026-10-30T12:00:00Z" }, workingDirectory: project));
            Assert.Equal(0, await DeliveryMetricsCommand.ExecuteAsync(store, new[] { "fallo", "cambio-123", "2026-10-30T13:00:00Z", "error", "externo" }, workingDirectory: project));
            Assert.Equal(0, await DeliveryMetricsCommand.ExecuteAsync(store, new[] { "recuperar", "cambio-123", "2026-10-30T15:00:00Z" }, workingDirectory: project));
            Assert.Equal(0, await DeliveryMetricsCommand.ExecuteAsync(store, new[] { "retrabajo", "cambio-123", "ajuste", "del", "defecto" }, workingDirectory: project));
            Assert.Equal(0, await DeliveryMetricsCommand.ExecuteAsync(store, Array.Empty<string>(), workingDirectory: project));

            var saved = await store.LoadDeliveryMetricsAsync(project);
            var change = Assert.Single(saved.Changes);
            Assert.Equal("cambio-123", change.ChangeId);
            Assert.Equal(DateTimeOffset.Parse("2026-10-30T10:00:00Z"), change.StartedAtUtc);
            Assert.Equal(DateTimeOffset.Parse("2026-10-30T12:00:00Z"), change.DeliveredAtUtc);
            Assert.Equal(DateTimeOffset.Parse("2026-10-30T13:00:00Z"), change.FailedAtUtc);
            Assert.Equal(DateTimeOffset.Parse("2026-10-30T15:00:00Z"), change.RecoveredAtUtc);
            Assert.Equal("error externo", change.FailureReason);
            Assert.Equal("ajuste del defecto", change.ReworkReason);
            Assert.Empty((await store.LoadDeliveryMetricsAsync(otherProject)).Changes);

            var summary = DeliveryMetricsCalculator.Summarize(saved, DateTimeOffset.Parse("2026-11-01T00:00:00Z"));
            Assert.Equal(1, summary.DeliveriesInLast30Days);
            Assert.Equal(0.23, summary.WeeklyDeploymentFrequency, 2);
            Assert.Equal(TimeSpan.FromHours(2), summary.AverageChangeLeadTime);
            Assert.Equal(100, summary.ChangeFailureRate);
            Assert.Equal(TimeSpan.FromHours(2), summary.AverageRecoveryTime);
            Assert.Equal(0, summary.UnrecoveredIncidentCount);
            Assert.Equal(1, summary.ReworkedChangeCount);
            Assert.Equal(100, summary.DeploymentReworkRate);
            Assert.Equal(new[] { "cambio-123: ajuste del defecto" }, summary.ReworkReasons);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_RechazaEntregarSinInicio()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "condor-metricas-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        var store = new LocalStateStore(Path.Combine(root, "state"));

        try
        {
            Assert.Equal(2, await DeliveryMetricsCommand.ExecuteAsync(store, new[] { "entregar", "sin-inicio" }, workingDirectory: root));
            Assert.Empty((await store.LoadDeliveryMetricsAsync(root)).Changes);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Summarize_SinDatos_NoInventaTasasNiDuraciones()
    {
        var summary = DeliveryMetricsCalculator.Summarize(new DeliveryMetricsState(), DateTimeOffset.UtcNow);

        Assert.Equal(0, summary.DeliveriesInLast30Days);
        Assert.Null(summary.ChangeFailureRate);
        Assert.Null(summary.AverageChangeLeadTime);
        Assert.Null(summary.AverageRecoveryTime);
        Assert.Null(summary.DeploymentReworkRate);
    }
}
