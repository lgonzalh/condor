using Condor.Core.Evaluation;
using Condor.Core.Models;

namespace Condor.Core.Tests;

public sealed class TaskIntentClassifierTests
{
    [Fact]
    public void Saludo_EsConsultaDirectaSinProtocoloDeHerramientas()
    {
        var requirement = TaskIntentClassifier.Classify("hola");

        Assert.Equal(TaskIntentKinds.Consult, requirement.IntentKind);
        Assert.False(requirement.RequiresToolUse);
        Assert.False(requirement.RequiresStructuredOutput);
        Assert.True(requirement.PreferSmallestSufficient);
    }

    [Fact]
    public void CambioDeCodigo_MantieneRequisitosDeAgente()
    {
        var requirement = TaskIntentClassifier.Classify("corrige este archivo y compila");

        Assert.Equal(TaskIntentKinds.Agent, requirement.IntentKind);
        Assert.True(requirement.RequiresToolUse);
        Assert.True(requirement.RequiresStructuredOutput);
        Assert.True(requirement.RequiredCodingLevel >= 3);
    }
}
