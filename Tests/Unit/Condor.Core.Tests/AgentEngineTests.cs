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
    public void AssessObservation_MismaObservacion_EsRedundante()
    {
        var prior = new List<AgentStep>
        {
            new() { Action = AgentAction.ActionListDir, Path = ".", ResultPreview = "dir", Success = true },
        };
        var current = new AgentStep { Action = AgentAction.ActionListDir, Path = ".", ResultPreview = "dir", Success = true };

        Assert.Equal(ObservationSignal.Redundant, AgentEngine.AssessObservation(current, prior));
    }

    [Fact]
    public void AssessObservation_RutaNueva_EsInformacionNueva()
    {
        var prior = new List<AgentStep>
        {
            new() { Action = AgentAction.ActionListDir, Path = ".", ResultPreview = "dir", Success = true },
        };
        var current = new AgentStep { Action = AgentAction.ActionListDir, Path = "src", ResultPreview = "dir2", Success = true };

        Assert.Equal(ObservationSignal.NewInformation, AgentEngine.AssessObservation(current, prior));
    }

    [Fact]
    public void AssessObservation_ResultadoCambiado_EsInformacionNueva()
    {
        var prior = new List<AgentStep>
        {
            new() { Action = AgentAction.ActionReadFile, Path = "a.cs", ResultPreview = "v1", Success = true },
        };
        // El modelo volvio a leer el mismo archivo pero ahora trae contenido distinto
        // (el archivo cambio), es una observacion nueva, no redundante.
        var current = new AgentStep { Action = AgentAction.ActionReadFile, Path = "a.cs", ResultPreview = "v2", Success = true };

        Assert.Equal(ObservationSignal.NewInformation, AgentEngine.AssessObservation(current, prior));
    }

    [Fact]
    public void AssessObservation_AccionNoObservacion_NoEsRedundante()
    {
        var prior = new List<AgentStep>
        {
            new() { Action = AgentAction.ActionBuild, Path = null, ResultPreview = "ok", Success = true },
        };
        var current = new AgentStep { Action = AgentAction.ActionBuild, Path = null, ResultPreview = "ok", Success = true };

        Assert.Equal(ObservationSignal.NewInformation, AgentEngine.AssessObservation(current, prior));
    }

    [Theory]
    [InlineData("me ayudas a revisar parece que hay un error?", IntentFlavor.Diagnostic)]
    [InlineData("hay un error en el descuento, corrigelo", IntentFlavor.Diagnostic)]
    [InlineData("revisa y cuentame que tenemos aqui", IntentFlavor.Descriptive)]
    [InlineData("crea una web sencilla para este proyecto", IntentFlavor.Build)]
    public void ClassifyIntent_DetectaMatizDeLaIntencion(string intention, IntentFlavor expected)
    {
        Assert.Equal(expected, AgentEngine.ClassifyIntent(intention));
    }

    [Fact]
    public void HasSufficientEvidence_DiagnosticoAbiertoIniciadoConMayuscula_NoSeConfundeConObjetivo()
    {
        var steps = new List<AgentStep>
        {
            new() { Action = AgentAction.ActionListDir, Path = ".", Success = true, ResultPreview = "index.html, app.js, estilos.css" },
            new() { Action = AgentAction.ActionReadFile, Path = "app.js", Success = true, ResultPreview = "codigo js" },
        };

        // "Hay ... directorio" no nombra un archivo concreto: sigue siendo una
        // revision abierta, por lo que un solo archivo leido es insuficiente.
        var (sufficient, _) = AgentEngine.HasSufficientEvidenceForDone("Hay un error, revisa este directorio", steps);

        Assert.False(sufficient);
    }

    [Fact]
    public void HasSufficientEvidence_DiagnosticoConObjetivoConcreto_Nombrado_EsSuficiente()
    {
        var steps = new List<AgentStep>
        {
            new() { Action = AgentAction.ActionListDir, Path = ".", Success = true, ResultPreview = "src/Calc.cs" },
            new() { Action = AgentAction.ActionReadFile, Path = "src/Calc.cs", Success = true, ResultPreview = "codigo" },
        };

        // Nombrar un modulo concreto ("en Calc") acota el objetivo: leerlo basta.
        var (sufficient, _) = AgentEngine.HasSufficientEvidenceForDone("hay un error en Calc, revisalo", steps);

        Assert.True(sufficient);
    }

    [Fact]
    public void HasSufficientEvidence_DiagnosticoSinLeerFuente_EsInsuficiente()
    {
        var steps = new List<AgentStep>
        {
            new() { Action = AgentAction.ActionListDir, Path = ".", Success = true, ResultPreview = "dir" },
        };

        var (sufficient, _) = AgentEngine.HasSufficientEvidenceForDone("hay un error, revisalo", steps);

        Assert.False(sufficient);
    }

    [Fact]
    public void HasSufficientEvidence_DiagnosticoHabiendoLeidoFuente_EsSuficiente()
    {
        var steps = new List<AgentStep>
        {
            new() { Action = AgentAction.ActionListDir, Path = ".", Success = true, ResultPreview = "dir" },
            new() { Action = AgentAction.ActionReadFile, Path = "src/Calc.cs", Success = true, ResultPreview = "codigo" },
        };

        var (sufficient, _) = AgentEngine.HasSufficientEvidenceForDone("hay un error en Calc, revisalo", steps);

        Assert.True(sufficient);
    }

    [Fact]
    public void HasSufficientEvidence_DiagnosticoAbiertoConUnSoloArchivo_EsInsuficiente()
    {
        var steps = new List<AgentStep>
        {
            new() { Action = AgentAction.ActionListDir, Path = ".", Success = true, ResultPreview = "index.html, app.js, estilos.css" },
            new() { Action = AgentAction.ActionReadFile, Path = "app.js", Success = true, ResultPreview = "codigo js" },
        };

        // Intencion abierta de detectar errores: leer un solo archivo de un
        // proyecto con varios archivos relacionados no basta para concluir.
        var (sufficient, hint) = AgentEngine.HasSufficientEvidenceForDone("hay un error, revisa este directorio", steps);

        Assert.False(sufficient);
        Assert.False(string.IsNullOrWhiteSpace(hint));
    }

    [Fact]
    public void HasSufficientEvidence_DiagnosticoAbiertoConDosArchivos_EsSuficiente()
    {
        var steps = new List<AgentStep>
        {
            new() { Action = AgentAction.ActionListDir, Path = ".", Success = true, ResultPreview = "index.html, app.js, estilos.css" },
            new() { Action = AgentAction.ActionReadFile, Path = "app.js", Success = true, ResultPreview = "codigo js" },
            new() { Action = AgentAction.ActionReadFile, Path = "index.html", Success = true, ResultPreview = "html" },
        };

        // Dos archivos distintos (relacion entre ellos) dan alcance suficiente.
        var (sufficient, _) = AgentEngine.HasSufficientEvidenceForDone("hay un error, revisa este directorio", steps);

        Assert.True(sufficient);
    }

    [Fact]
    public void HasSufficientEvidence_DiagnosticoAbiertoConSearchSinLeer_EsInsuficiente()
    {
        var steps = new List<AgentStep>
        {
            new() { Action = AgentAction.ActionListDir, Path = ".", Success = true, ResultPreview = "index.html, app.js, estilos.css" },
            new() { Action = AgentAction.ActionSearch, Path = null, Success = true, ResultPreview = "error" },
        };

        // Un search solo, sin haber leido contenido real, no basta para concluir
        // en una revision abierta de errores.
        var (sufficient, _) = AgentEngine.HasSufficientEvidenceForDone("hay un error, revisa este directorio", steps);

        Assert.False(sufficient);
    }

    [Fact]
    public void HasSufficientEvidence_DiagnosticoAbiertoConSearchYUnaLectura_EsSuficiente()
    {
        var steps = new List<AgentStep>
        {
            new() { Action = AgentAction.ActionListDir, Path = ".", Success = true, ResultPreview = "index.html, app.js, estilos.css" },
            new() { Action = AgentAction.ActionReadFile, Path = "app.js", Success = true, ResultPreview = "codigo js" },
            new() { Action = AgentAction.ActionSearch, Path = null, Success = true, ResultPreview = "error" },
        };

        var (sufficient, _) = AgentEngine.HasSufficientEvidenceForDone("hay un error, revisa este directorio", steps);

        Assert.True(sufficient);
    }

    [Fact]
    public void HasSufficientEvidence_DescribirSinLeerNada_EsInsuficiente()
    {
        var steps = new List<AgentStep>
        {
            new() { Action = AgentAction.ActionListDir, Path = ".", Success = true, ResultPreview = "dir" },
        };

        var (sufficient, _) = AgentEngine.HasSufficientEvidenceForDone("revisa y cuentame que tenemos aqui", steps);

        Assert.False(sufficient);
    }

    [Fact]
    public void HasSufficientEvidence_BuildSinModificar_EsInsuficiente()
    {
        var steps = new List<AgentStep>
        {
            new() { Action = AgentAction.ActionListDir, Path = ".", Success = true, ResultPreview = "dir" },
        };

        var (sufficient, _) = AgentEngine.HasSufficientEvidenceForDone("crea una web para este proyecto", steps);

        Assert.False(sufficient);
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
