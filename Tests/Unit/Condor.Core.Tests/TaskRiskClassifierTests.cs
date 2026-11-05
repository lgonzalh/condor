using Condor.Core.Evaluation;

namespace Condor.Core.Tests;

public sealed class TaskRiskClassifierTests
{
    [Theory]
    [InlineData("actualiza README.md", null, TaskRiskLevel.Low)]
    [InlineData("refactoriza la clase del carrito", "src/Cart.cs", TaskRiskLevel.Medium)]
    [InlineData("actualiza una dependencia de red", null, TaskRiskLevel.High)]
    [InlineData("despliega cambios a producción", null, TaskRiskLevel.Critical)]
    [InlineData("actualiza el texto", ".github/workflows/ci.yml", TaskRiskLevel.Critical)]
    [InlineData("Crea una prueba xUnit; no edites producción", null, TaskRiskLevel.Medium)]
    [InlineData("Crea exactamente src/Marker.cs. No modifiques src/Production.cs, que está excluido.", null, TaskRiskLevel.Medium)]
    [InlineData("No modifiques src/Production.cs; actualiza el objetivo indicado.", "src/Production.cs", TaskRiskLevel.Critical)]
    [InlineData("No despliegues a producción; documenta el proceso en README.md", null, TaskRiskLevel.Low)]
    public void Assess_ClassifiesImpactAndSensitiveTargetsProportionally(
        string intention,
        string? targetPath,
        TaskRiskLevel expected)
    {
        var assessment = TaskRiskClassifier.Assess(intention, targetPath);

        Assert.Equal(expected, assessment.Level);
        Assert.False(string.IsNullOrWhiteSpace(assessment.Rationale));
    }
}
