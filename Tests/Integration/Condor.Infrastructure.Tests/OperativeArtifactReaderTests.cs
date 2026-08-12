using Condor.Core.Context;
using Condor.Core.Models;
using Condor.Infrastructure.Context;

namespace Condor.Infrastructure.Tests;

public class OperativeArtifactReaderTests
{
    [Fact]
    public async Task ReadAsync_DirectorioVacio_DevuelveListaVacia()
    {
        var reader = new OperativeArtifactReader();

        var artifacts = await reader.ReadAsync("", CancellationToken.None);

        Assert.Empty(artifacts);
    }

    [Fact]
    public async Task ReadAsync_SinDirectorioOperacion_NoDetectaArtefactos()
    {
        var directory = DirectorioTemporal();
        Directory.CreateDirectory(directory);
        var reader = new OperativeArtifactReader();

        var artifacts = await reader.ReadAsync(directory, CancellationToken.None);

        Assert.Empty(artifacts);
    }

    [Fact]
    public async Task ReadAsync_ConArtefactos_LeeTodosLosOficialesEnOrdenFijo()
    {
        var directory = DirectorioTemporal();
        Directory.CreateDirectory(Path.Combine(directory, "operacion"));
        foreach (var n in new[] { "ESTADO_DESARROLLO.md", "RELEVO.md", "BACKLOG.md", "KANBAN.md", "REGISTRO_CAMBIOS.md" })
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "operacion", n), "contenido\n");
        }
        var reader = new OperativeArtifactReader();

        var artifacts = await reader.ReadAsync(directory, CancellationToken.None);

        Assert.Equal(5, artifacts.Count);
        Assert.All(artifacts, artifact => Assert.Equal(DetectionStatus.Detected, artifact.Status));
        Assert.Equal(OperativeArtifactKind.EstadoDesarrollo, artifacts[0].Kind);
        Assert.Equal(OperativeArtifactKind.Releve, artifacts[1].Kind);
        Assert.Equal(OperativeArtifactKind.Backlog, artifacts[2].Kind);
        Assert.Equal(OperativeArtifactKind.Kanban, artifacts[3].Kind);
        Assert.Equal(OperativeArtifactKind.RegistroCambios, artifacts[4].Kind);
    }

    [Fact]
    public async Task ReadAsync_ArtefactoExcesivo_SeOmitComoLimitedConLimitanteTamano()
    {
        var directory = DirectorioTemporal();
        Directory.CreateDirectory(Path.Combine(directory, "operacion"));
        var oversized = new string('a', (ContextLimits.Default.MaxArtifactBytes + 1));
        await File.WriteAllTextAsync(Path.Combine(directory, "operacion", "BACKLOG.md"), oversized);
        var reader = new OperativeArtifactReader();

        var artifacts = await reader.ReadAsync(directory, CancellationToken.None);

        var oversizedArtifact = Assert.Single(artifacts, artifact => artifact.Kind == OperativeArtifactKind.Backlog);
        Assert.Equal(DetectionStatus.Limited, oversizedArtifact.Status);
        Assert.Equal("supera el limite de tamano", oversizedArtifact.Reason);
    }

    [Fact]
    public async Task ReadAsync_SoloArtefactosDetectados_ExcluyeInexistentes()
    {
        var directory = DirectorioTemporal();
        Directory.CreateDirectory(Path.Combine(directory, "operacion"));
        await File.WriteAllTextAsync(Path.Combine(directory, "operacion", "KANBAN.md"), "siguiente\nT-006\n");
        var reader = new OperativeArtifactReader();

        var artifacts = await reader.ReadAsync(directory, CancellationToken.None);

        Assert.Single(artifacts);
        Assert.Equal(OperativeArtifactKind.Kanban, artifacts[0].Kind);
    }

    private static string DirectorioTemporal()
    {
        return Path.Combine(Path.GetTempPath(), "condor-artifact-" + Guid.NewGuid().ToString("N"));
    }
}
