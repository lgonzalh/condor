using Condor.Core.Evaluation;
using Condor.Core.Models;

namespace Condor.Core.Tests;

public class LlmModelSelectorTests
{
    [Fact]
    public void Select_ConModeloExplicito_LoPrefiere()
    {
        var assessment = AssessmentConModelo("primero", "segundo");

        var selected = LlmModelSelector.Select(assessment, "explicito");

        Assert.Equal("explicito", selected);
    }

    [Fact]
    public void Select_SinModeloExplicito_UsaPrimeroDisponible()
    {
        var assessment = AssessmentConModelo("primero", "segundo");

        var selected = LlmModelSelector.Select(assessment, null);

        Assert.Equal("primero", selected);
    }

    [Fact]
    public void Select_ModeloExplicitoEnBlanco_UsaPrimeroDisponible()
    {
        var assessment = AssessmentConModelo("primero");

        var selected = LlmModelSelector.Select(assessment, "   ");

        Assert.Equal("primero", selected);
    }

    [Fact]
    public void Select_SinModelosDisponibles_DevuelveNull()
    {
        var assessment = new AssessmentResult();

        var selected = LlmModelSelector.Select(assessment, null);

        Assert.Null(selected);
    }

    [Fact]
    public void Select_SinAssessment_DevuelveNull()
    {
        var selected = LlmModelSelector.Select(null, null);

        Assert.Null(selected);
    }

    [Fact]
    public void Select_CuandoAssessmentNoTieneTools_OllamaOModels_DevuelveNullSinExcepcion()
    {
        var sinTools = new AssessmentResult();
        var sinOllama = new AssessmentResult { Tools = new ToolsProfile() };
        var sinModels = new AssessmentResult
        {
            Tools = new ToolsProfile { Ollama = new OllamaStatus() }
        };

        var seleccionSinTools = LlmModelSelector.Select(sinTools, null);
        var seleccionSinOllama = LlmModelSelector.Select(sinOllama, null);
        var seleccionSinModels = LlmModelSelector.Select(sinModels, null);

        Assert.Null(seleccionSinTools);
        Assert.Null(seleccionSinOllama);
        Assert.Null(seleccionSinModels);
    }

    private static AssessmentResult AssessmentConModelo(params string[] modelos)
    {
        return new AssessmentResult
        {
            Tools = new ToolsProfile
            {
                Ollama = new OllamaStatus
                {
                    Models = modelos
                        .Select(modelo => new ModelInfo { Name = modelo })
                        .ToList()
                }
            }
        };
    }
}
