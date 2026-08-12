namespace Condor.Core.Context;

public static class OperativeArtifactCatalog
{
    public static IReadOnlyList<OperativeArtifactKind> Order { get; } = new[]
    {
        OperativeArtifactKind.EstadoDesarrollo,
        OperativeArtifactKind.Releve,
        OperativeArtifactKind.Backlog,
        OperativeArtifactKind.Kanban,
        OperativeArtifactKind.RegistroCambios
    };

    public static string FileName(OperativeArtifactKind kind)
    {
        return kind switch
        {
            OperativeArtifactKind.EstadoDesarrollo => "ESTADO_DESARROLLO.md",
            OperativeArtifactKind.Releve => "RELEVO.md",
            OperativeArtifactKind.Backlog => "BACKLOG.md",
            OperativeArtifactKind.Kanban => "KANBAN.md",
            OperativeArtifactKind.RegistroCambios => "REGISTRO_CAMBIOS.md",
            _ => ""
        };
    }
}