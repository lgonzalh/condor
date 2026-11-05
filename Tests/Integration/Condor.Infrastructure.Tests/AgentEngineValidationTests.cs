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

    [Fact]
    public void ValidateAction_PlanVinculaCadaTareaConResultadoYComprobacion()
    {
        var validation = AgentEngine.ValidateAction(PlanCompleto());

        Assert.True(validation.Valid, validation.Reason);
    }

    [Fact]
    public void ValidateAction_PlanSinResultadoPorTarea_EsInvalido()
    {
        var plan = PlanCompleto();
        plan.ExpectedResults!.RemoveAt(1);

        var validation = AgentEngine.ValidateAction(plan);

        Assert.False(validation.Valid);
        Assert.Contains("resultado esperado", validation.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateAction_PlanSinComprobacionPorTarea_EsInvalido()
    {
        var plan = PlanCompleto();
        plan.VerificationSteps!.RemoveAt(1);

        var validation = AgentEngine.ValidateAction(plan);

        Assert.False(validation.Valid);
        Assert.Contains("comprobación propia", validation.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateAction_PlanConMasDeDoceTareas_EsInvalido()
    {
        var plan = PlanCompleto();
        for (var index = 0; index < 11; index++)
        {
            plan.PlanSteps!.Add("Tarea adicional " + index);
            plan.ExpectedResults!.Add("Resultado adicional " + index);
            plan.VerificationSteps!.Add("Comprobación adicional " + index);
        }

        var validation = AgentEngine.ValidateAction(plan);

        Assert.False(validation.Valid);
        Assert.Contains("12 tareas", validation.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateAction_GitShow_ExigeUnHashHexadecimal()
    {
        var invalid = AgentEngine.ValidateAction(new AgentAction
        {
            Action = AgentAction.ActionGitShow,
            Path = "HEAD;--help"
        });
        var valid = AgentEngine.ValidateAction(new AgentAction
        {
            Action = AgentAction.ActionGitShow,
            Path = "0123456789abcdef0123456789abcdef01234567"
        });

        Assert.False(invalid.Valid);
        Assert.Contains("hash de commit", invalid.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.True(valid.Valid, valid.Reason);
    }

    private static AgentAction PlanCompleto() => new()
    {
        Action = AgentAction.ActionPlan,
        Objective = "Actualizar la suma",
        ScopePaths = new List<string> { "src/Calculator.cs" },
        ExcludedPaths = new List<string> { "README.md" },
        PlanSteps = new List<string> { "Actualizar el cálculo", "Revisar el resultado" },
        ExpectedResults = new List<string> { "La suma usa los operandos", "La salida coincide con el contrato" },
        VerificationSteps = new List<string> { "Leer la implementación", "Ejecutar el caso de suma" }
    };
}
