using Condor.Core.Models;
using Condor.Infrastructure.Agent;
using Condor.Infrastructure.State;

namespace Condor.Infrastructure.Tests;

public sealed class DeliveryMetricsTaskLinkerTests
{
    [Fact]
    public async Task LinkAsync_VinculaEjecucionACambioEIncidenteAbiertos()
    {
        var root = CreateProjectRoot();
        var project = Path.Combine(root, "proyecto");
        Directory.CreateDirectory(project);
        var store = new LocalStateStore(Path.Combine(root, "state"));
        var state = new DeliveryMetricsState();
        state.Changes.Add(new DeliveryChangeRecord
        {
            ChangeId = "CHG-12",
            StartedAtUtc = DateTimeOffset.Parse("2026-11-01T10:00:00Z")
        });
        state.Changes.Add(new DeliveryChangeRecord
        {
            ChangeId = "INC-8",
            StartedAtUtc = DateTimeOffset.Parse("2026-10-31T10:00:00Z"),
            DeliveredAtUtc = DateTimeOffset.Parse("2026-10-31T11:00:00Z"),
            FailedAtUtc = DateTimeOffset.Parse("2026-11-01T12:00:00Z")
        });
        await store.SaveDeliveryMetricsAsync(project, state);

        try
        {
            var result = await DeliveryMetricsTaskLinker.LinkAsync(
                store, project,
                "Implementar ajuste [[condor:cambio:CHG-12]] y atender [[condor:incidente:INC-8]]",
                "run-123", CancellationToken.None);

            Assert.Equal(new[] { "cambio:CHG-12", "incidente:INC-8" }, result.LinkedRecords);
            Assert.Null(result.Warning);
            var saved = await store.LoadDeliveryMetricsAsync(project);
            Assert.Equal(new[] { "run-123" }, Assert.Single(saved.Changes, change => change.ChangeId == "CHG-12").RelatedTaskExecutionIds);
            Assert.Equal(new[] { "run-123" }, Assert.Single(saved.Changes, change => change.ChangeId == "INC-8").IncidentTaskExecutionIds);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task LinkAsync_NoEnlazaCambiosEntregadosNiIncidentesRecuperados()
    {
        var root = CreateProjectRoot();
        var project = Path.Combine(root, "proyecto");
        Directory.CreateDirectory(project);
        var store = new LocalStateStore(Path.Combine(root, "state"));
        var state = new DeliveryMetricsState();
        state.Changes.Add(new DeliveryChangeRecord
        {
            ChangeId = "CHG-13",
            StartedAtUtc = DateTimeOffset.Parse("2026-11-01T10:00:00Z"),
            DeliveredAtUtc = DateTimeOffset.Parse("2026-11-01T11:00:00Z")
        });
        state.Changes.Add(new DeliveryChangeRecord
        {
            ChangeId = "INC-9",
            FailedAtUtc = DateTimeOffset.Parse("2026-11-01T12:00:00Z"),
            RecoveredAtUtc = DateTimeOffset.Parse("2026-11-01T13:00:00Z")
        });
        await store.SaveDeliveryMetricsAsync(project, state);

        try
        {
            var result = await DeliveryMetricsTaskLinker.LinkAsync(
                store, project,
                "Tarea [[condor:cambio:CHG-13]] y [[condor:incidente:INC-9]]",
                "run-124", CancellationToken.None);

            Assert.Empty(result.LinkedRecords);
            Assert.Contains("no está en curso", result.Warning);
            Assert.Contains("no está abierto", result.Warning);
            var saved = await store.LoadDeliveryMetricsAsync(project);
            Assert.All(saved.Changes, change =>
            {
                Assert.Empty(change.RelatedTaskExecutionIds);
                Assert.Empty(change.IncidentTaskExecutionIds);
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task LinkAsync_ReferenciaInexistenteAdvierteSinCrearRegistro()
    {
        var root = CreateProjectRoot();
        var project = Path.Combine(root, "proyecto");
        Directory.CreateDirectory(project);
        var store = new LocalStateStore(Path.Combine(root, "state"));

        try
        {
            var result = await DeliveryMetricsTaskLinker.LinkAsync(
                store, project, "Tarea [[condor:cambio:CHG-404]]", "run-125", CancellationToken.None);

            Assert.Empty(result.LinkedRecords);
            Assert.Contains("No se encontró un único registro", result.Warning);
            Assert.Empty((await store.LoadDeliveryMetricsAsync(project)).Changes);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateProjectRoot() => Path.Combine(Path.GetTempPath(), "condor-metric-link-" + Guid.NewGuid().ToString("N"));
}
