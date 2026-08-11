using Condor.Core.Evaluation;
using Condor.Core.Models;

namespace Condor.Core.Tests;

public class ModelRoleClassifierTests
{
    [Fact]
    public void DevelopmentScore_ModeloCoder_PuntuaAlto()
    {
        var model = new ModelInfo
        {
            Name = "qwen2.5-coder:7b",
            Family = "qwen2",
            Capabilities = new List<string> { "completion" }
        };

        var score = ModelRoleClassifier.DevelopmentScore(model);

        Assert.True(score >= 0.6);
    }

    [Fact]
    public void DevelopmentScore_ModeloConCapacidadTools_Contribuye()
    {
        var model = new ModelInfo
        {
            Name = "modelo-generico",
            Capabilities = new List<string> { "completion", "tools" }
        };

        var score = ModelRoleClassifier.DevelopmentScore(model);

        Assert.True(score >= 0.25);
    }

    [Fact]
    public void DevelopmentScore_DeepseekR1_PuntuaPorRazonamiento()
    {
        var model = new ModelInfo
        {
            Name = "deepseek-r1:7b",
            Family = "qwen2",
            Capabilities = new List<string> { "completion" }
        };

        var score = ModelRoleClassifier.DevelopmentScore(model);

        Assert.True(score >= 0.15);
    }

    [Fact]
    public void HasVision_ConCapacidadVision_DevuelveVerdadero()
    {
        var model = new ModelInfo { Capabilities = new List<string> { "completion", "vision" } };

        Assert.True(ModelRoleClassifier.HasVision(model));
        Assert.False(ModelRoleClassifier.HasVision(new ModelInfo()));
    }
}
