using Condor.Core.Agent;
using Condor.Core.Models;
using Condor.Infrastructure.Agent;

namespace Condor.Infrastructure.Tests;

public class AgentEngineTests2
{
    [Fact]
    public void ValidateAction_PatchSinOriginal_EsInvalida()
    {
        var validation = AgentEngine.ValidateAction(new AgentAction
        {
            Action = AgentAction.ActionPatch,
            Path = "X.cs",
            Replacement = "new"
        });

        Assert.False(validation.Valid);
    }

    [Fact]
    public void ValidateAction_PatchCompleto_EsValida()
    {
        var validation = AgentEngine.ValidateAction(new AgentAction
        {
            Action = AgentAction.ActionPatch,
            Path = "X.cs",
            Original = "old",
            Replacement = "new"
        });

        Assert.True(validation.Valid);
    }

    [Fact]
    public void ValidateAction_Restore_EsValida()
    {
        var validation = AgentEngine.ValidateAction(new AgentAction { Action = AgentAction.ActionRestore });

        Assert.True(validation.Valid);
    }

    [Fact]
    public void ValidateAction_UndoFile_EsValida()
    {
        var validation = AgentEngine.ValidateAction(new AgentAction { Action = AgentAction.ActionUndoFile, Path = "Calc.cs" });

        Assert.True(validation.Valid);
    }

    [Fact]
    public void ValidateAction_PatchAdmiteOriginalOContent()
    {
        var viaContent = AgentEngine.ValidateAction(new AgentAction { Action = AgentAction.ActionPatch, Path = "X.cs", Content = "old", Replacement = "new" });
        Assert.True(viaContent.Valid);

        var viaOriginal = AgentEngine.ValidateAction(new AgentAction { Action = AgentAction.ActionPatch, Path = "X.cs", Original = "old", Replacement = "new" });
        Assert.True(viaOriginal.Valid);
    }
}
