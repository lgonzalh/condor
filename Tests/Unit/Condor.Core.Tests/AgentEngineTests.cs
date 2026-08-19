using System.Collections.Generic;
using Condor.Core.Agent;
using Condor.Core.Models;

namespace Condor.Core.Tests;

public class AgentEngineTests
{
    [Fact]
    public void ValidateAction_AccionPermitida_Valida()
    {
        var v = AgentEngine.ValidateAction(new AgentAction { Action = AgentAction.ActionReadFile, Path = "a.cs" });
        Assert.True(v.Valid);
    }

    [Fact]
    public void ValidateAction_AccionNoPermitida_Rechaza()
    {
        var v = AgentEngine.ValidateAction(new AgentAction { Action = "borrar" });
        Assert.False(v.Valid);
    }

    [Fact]
    public void ValidateAction_EditSinContenido_Rechaza()
    {
        var v = AgentEngine.ValidateAction(new AgentAction { Action = AgentAction.ActionEditFile, Path = "a.cs" });
        Assert.False(v.Valid);
    }

    [Fact]
    public void EvaluateHarness_BuildYTestOk_Exito()
    {
        var d = AgentEngine.EvaluateHarness(true, true, null, null);
        Assert.True(d.Done);
    }

    [Fact]
    public void EvaluateHarness_BuildFalla_NoExito()
    {
        var d = AgentEngine.EvaluateHarness(false, true, "error", null);
        Assert.False(d.Done);
        Assert.Contains("build", d.Reason, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CheckProgress_IteracionesLimite_Agota()
    {
        var d = AgentEngine.CheckProgress(8, new List<AgentStep>(), new AgentLimits { MaxIterations = 8 });
        Assert.True(d.Fail);
    }

    [Fact]
    public void CheckProgress_RepeticionSinProgreso_Detecta()
    {
        var steps = new List<AgentStep>();
        for (var i = 0; i < 3; i++)
        {
            steps.Add(new AgentStep { Action = AgentAction.ActionReadFile, Path = "a.cs", ResultPreview = "" });
        }

        var d = AgentEngine.CheckProgress(1, steps, new AgentLimits { MaxRepeatedAction = 3 });
        Assert.True(d.Fail);
    }

    [Fact]
    public void ParseAction_JsonValido_DevuelveAccion()
    {
        var action = AgentActionParser.Parse("{\"action\":\"edit_file\",\"path\":\"Calculator.cs\",\"content\":\"return a*b;\",\"reason\":\"fix\"}");
        Assert.NotNull(action);
        Assert.Equal(AgentAction.ActionEditFile, action.Action);
        Assert.Equal("Calculator.cs", action.Path);
    }

    [Fact]
    public void ParseAction_JsonEntreTexto_Extrae()
    {
        var action = AgentActionParser.Parse("Accion:\n{\"action\":\"read_file\",\"path\":\"Program.cs\"}\nfin.");
        Assert.NotNull(action);
        Assert.Equal(AgentAction.ActionReadFile, action.Action);
    }

    [Fact]
    public void ParseAction_NoJson_Null()
    {
        Assert.Null(AgentActionParser.Parse("no hay json"));
    }
}
