using System.Xml.Linq;

namespace Condor.Architecture.Tests;

public class ProjectReferenceRulesTests
{
    [Fact]
    public void CondorCore_NoTieneReferenciasAOtrosProyectos()
    {
        var references = GetProjectReferences(RepoRoot("Src/Condor.Core/Condor.Core.csproj"));

        Assert.Empty(references);
    }

    [Fact]
    public void CondorInfrastructure_ReferenciaSoloCondorCore()
    {
        var references = GetProjectReferences(RepoRoot("Src/Condor.Infrastructure/Condor.Infrastructure.csproj"));

        Assert.Equal(new[] { "Condor.Core" }, references);
    }

    [Fact]
    public void CondorCli_ReferenciaCondorCoreYCondorInfrastructure()
    {
        var references = GetProjectReferences(RepoRoot("Src/Condor.Cli/Condor.Cli.csproj"));

        Assert.Contains("Condor.Core", references);
        Assert.Contains("Condor.Infrastructure", references);
    }

    [Fact]
    public void CondorCli_NoReferenciaProyectosDePruebas()
    {
        var references = GetProjectReferences(RepoRoot("Src/Condor.Cli/Condor.Cli.csproj"));

        Assert.DoesNotContain(references, reference => reference.EndsWith(".Tests"));
    }

    [Fact]
    public void PruebasUnitarias_ReferenciaSoloCondorCore()
    {
        var references = GetProjectReferences(RepoRoot("Tests/Unit/Condor.Core.Tests/Condor.Core.Tests.csproj"));

        Assert.Contains("Condor.Core", references);
        Assert.DoesNotContain(references, reference => reference != "Condor.Core");
    }

    [Fact]
    public void PruebasDeIntegracion_ReferenciaCondorCoreYCondorInfrastructure()
    {
        var references = GetProjectReferences(RepoRoot("Tests/Integration/Condor.Infrastructure.Tests/Condor.Infrastructure.Tests.csproj"));

        Assert.Contains("Condor.Core", references);
        Assert.Contains("Condor.Infrastructure", references);
    }

    private static List<string> GetProjectReferences(string csprojPath)
    {
        var document = XDocument.Load(csprojPath);
        return document.Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value ?? "")
            .Where(value => !string.IsNullOrEmpty(value))
            .Select(value => Path.GetFileNameWithoutExtension(value) ?? "")
            .ToList();
    }

    private static string RepoRoot(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Condor.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, relativePath);
    }
}
