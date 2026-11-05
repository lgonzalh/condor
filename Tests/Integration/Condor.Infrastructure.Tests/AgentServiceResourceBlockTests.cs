using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Condor.Cli.Routing;
using Condor.Cli.Tui;
using Condor.Core.Contracts;
using Condor.Core.Models;
using Condor.Infrastructure.Agent;
using Condor.Infrastructure.Detection;
using Condor.Infrastructure.Llm;
using Condor.Infrastructure.Setup;
using Condor.Infrastructure.State;

namespace Condor.Infrastructure.Tests;

public class AgentServiceResourceBlockTests
{
    [Fact]
    public async Task RunAsync_RamBaja_FallaRapido_ConPantallaModeloNoEjecutable()
    {
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 2, installedModels: new[] { "qwen2.5-coder:3b" });
        var service = new AgentService(
            store,
            assessment,
            modelAutoSetup: CreateModelAutoSetup(store, assessment, "qwen2.5-coder:3b"));

        var result = await service.RunAsync("analiza que contiene este proyecto", cancellationToken: CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("MODELO NO EJECUTABLE", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RAM disponible", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RAM requerida estimada", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Motivo:", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Accion:", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("No hay un modelo compatible disponible", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("at Condor.", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("liberar memoria", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("analiza que contiene este proyecto", result.Objective);
        Assert.Equal("analiza que contiene este proyecto", result.Checkpoint?.Task);
    }

    [Fact]
    public async Task RunAsync_RamBaja_FalloInmediato_SinReevaluacionesEnBucle()
    {
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 2, installedModels: new[] { "qwen2.5-coder:3b" });
        var service = new AgentService(
            store,
            assessment,
            modelAutoSetup: CreateModelAutoSetup(store, assessment, "qwen2.5-coder:3b"));

        var result = await service.RunAsync("analiza", cancellationToken: CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(assessment.ExecuteCount <= 4, "El bloqueo por RAM debe fallar rapido, sin bucle de recuperacion.");
    }

    [Fact]
    public async Task RunAsync_SinModelosNiPresupuesto_ReportaCompatibleNoDisponible()
    {
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 2, installedModels: Array.Empty<string>());
        var service = new AgentService(
            store,
            assessment,
            modelAutoSetup: CreateModelAutoSetup(store, assessment));

        var result = await service.RunAsync("analiza", cancellationToken: CancellationToken.None);

        Assert.False(result.Success);
        var reason = result.Reason ?? "";
        Assert.True(
            reason.Contains("MODELO NO EJECUTABLE", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("MODELO COMPATIBLE NO DISPONIBLE", StringComparison.OrdinalIgnoreCase),
            "Debe informar el bloqueo con una pantalla clara.");
        Assert.Equal("analiza", result.Objective);
    }

    [Fact]
    public async Task RunAsync_ActiveModel0_5b_AgentTask_RamInsuficiente_FalloSuave_ActiveModelConservado()
    {
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 2, installedModels: new[] { "qwen2.5-coder:0.5b", "qwen2.5-coder:1.5b", "qwen2.5-coder:3b" });

        using var session = new LocalModelSession(new HttpClient(new StubOllamaHandler()));
        await session.EnsureAvailableAsync("qwen2.5-coder:0.5b", CancellationToken.None);

        var service = new AgentService(
            store,
            assessment,
            session: session,
            modelAutoSetup: CreateModelAutoSetup(store, assessment, "qwen2.5-coder:0.5b", "qwen2.5-coder:1.5b", "qwen2.5-coder:3b"));

        var result = await service.RunAsync("analiza que contiene este proyecto", cancellationToken: CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Memoria insuficiente para continuar con esta tarea. Libera memoria y vuelve a intentarlo.", result.Reason);
        Assert.Equal("qwen2.5-coder:0.5b", result.Model);
        Assert.Equal("memoria_insuficiente", result.Checkpoint?.LastDecision);
        Assert.Equal("qwen2.5-coder:0.5b", session.ActiveModel);
    }

    [Fact]
    public async Task EnsureModelForRequirement_ActiveModel3b_AgentTask_ModeloSuficiente_Selecciona3b()
    {
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 8, installedModels: new[] { "qwen2.5-coder:0.5b", "qwen2.5-coder:1.5b", "qwen2.5-coder:3b" });

        using var session = new LocalModelSession(new HttpClient(new StubOllamaHandler()));
        await session.EnsureAvailableAsync("qwen2.5-coder:3b", CancellationToken.None);

        var modelOperator = new OllamaModelOperator(
            new HttpClient(new StubOllamaHandler()),
            new StubOllamaDetector("qwen2.5-coder:0.5b", "qwen2.5-coder:1.5b", "qwen2.5-coder:3b"));
        var modelSetup = new ModelAutoSetupService(store, assessment, modelOperator: modelOperator);
        var requirement = new TaskModelRequirement
        {
            IntentKind = TaskIntentKinds.Agent,
            RequiredCodingLevel = 3,
            RequiredMultiFileLevel = 2,
            RequiresToolUse = true,
            RequiresStructuredOutput = true,
            PreferSmallestSufficient = true,
            Label = "agente de ingenieria"
        };

        var selection = await modelSetup.EnsureModelForRequirementAsync(requirement, CancellationToken.None, currentModelName: session.ActiveModel);

        Assert.NotNull(selection.Desired);
        Assert.Equal("qwen2.5-coder:3b", selection.Desired.PullName);
        Assert.False(selection.BlockedByResources);
        Assert.Equal("qwen2.5-coder:3b", session.ActiveModel);
    }


    [Fact]
    public async Task RunAsync_ActiveModel0_5b_RamSuficiente_PromocionAlModeloSuficiente_3b()
    {
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 8, installedModels: new[] { "qwen2.5-coder:0.5b", "qwen2.5-coder:1.5b", "qwen2.5-coder:3b" });

        using var session = new LocalModelSession(new HttpClient(new StubOllamaHandler()));
        await session.EnsureAvailableAsync("qwen2.5-coder:0.5b", CancellationToken.None);

        var service = new AgentService(
            store,
            assessment,
            llm: new StubLlmClient(),
            session: session,
            modelAutoSetup: CreateModelAutoSetup(store, assessment, "qwen2.5-coder:0.5b", "qwen2.5-coder:1.5b", "qwen2.5-coder:3b"));

        var result = await service.RunAsync("analiza que contiene este proyecto", cancellationToken: CancellationToken.None);

        Assert.Contains("qwen2.5-coder:3b", result.Model);
    }

    [Fact]
    public async Task RunAsync_ActiveModelNull_RamInsuficiente_FalloDuro_ModeloNoEjecutable()
    {
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 2, installedModels: new[] { "qwen2.5-coder:3b" });

        using var session = new LocalModelSession(new HttpClient(new StubOllamaHandler()));
        var service = new AgentService(
            store,
            assessment,
            session: session,
            modelAutoSetup: CreateModelAutoSetup(store, assessment, "qwen2.5-coder:3b"));

        var result = await service.RunAsync("analiza que contiene este proyecto", cancellationToken: CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("MODELO NO EJECUTABLE", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RAM disponible", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RAM requerida estimada", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_FalloSuave_NoModificaActiveModel()
    {
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 2, installedModels: new[] { "qwen2.5-coder:0.5b", "qwen2.5-coder:1.5b", "qwen2.5-coder:3b" });

        var session = new LocalModelSession();
        await session.EnsureAvailableAsync("qwen2.5-coder:0.5b", CancellationToken.None);

        var service = new AgentService(
            store,
            assessment,
            session: session,
            modelAutoSetup: CreateModelAutoSetup(store, assessment, "qwen2.5-coder:0.5b", "qwen2.5-coder:1.5b", "qwen2.5-coder:3b"));

        var result = await service.RunAsync("analiza que contiene este proyecto", cancellationToken: CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("qwen2.5-coder:0.5b", session.ActiveModel);
    }

    [Fact]
    public async Task RunDirectConsultAsync_ActiveModel0_5b_Suficiente_EjecutaConExito()
    {
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 2, installedModels: new[] { "qwen2.5-coder:0.5b", "qwen2.5-coder:1.5b", "qwen2.5-coder:3b" });

        // La prueba no depende de un Ollama real: el cliente LLM y el probe de
        // disponibilidad se sustituyen por stubs deterministas.
        using var http = new HttpClient(new StubOllamaHandler());
        using var session = new LocalModelSession(http);
        await session.EnsureAvailableAsync("qwen2.5-coder:0.5b", CancellationToken.None);

        var service = new AgentService(
            store,
            assessment,
            llm: new StubLlmClient(),
            provider: new StubLlmDiagnostics(),
            session: session);

        var result = await service.RunAsync("hola, que tal", cancellationToken: CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("qwen2.5-coder:0.5b", result.Model);
    }

    [Fact]
    public async Task RunAsync_FalloInicial_ActivaPlanB_AnalizaCorrigeYExponeElEstado()
    {
        var projectRoot = TempDir();
        Directory.CreateDirectory(projectRoot);
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 8, installedModels: new[] { "qwen2.5-coder:3b" });
        using var http = new HttpClient(new StubOllamaHandler());
        using var session = new LocalModelSession(http);
        await session.EnsureAvailableAsync("qwen2.5-coder:3b", CancellationToken.None);

        var llm = new SequenceLlmClient(
            "{\"action\":\"read_file\",\"path\":\"index.html\"}",
            "{\"action\":\"create_file\",\"path\":\"index.html\",\"content\":\"index.html\"}",
            "{\"action\":\"create_file\",\"path\":\"index.html\",\"content\":\"<!doctype html><html lang=\\\"es\\\"><body>Condor</body></html>\"}");
        var service = new AgentService(
            store,
            assessment,
            new AgentLimits { MaxIterations = 1, MaxPlanBIterations = 2 },
            llm,
            new StubLlmDiagnostics(),
            session,
            () => projectRoot,
            CreateModelAutoSetup(store, assessment, "qwen2.5-coder:3b"));
        var progress = new CollectingAgentProgressObserver();

        var result = await service.RunAsync("Crea una pagina web sencilla en index.html", progress, CancellationToken.None);

        Assert.True(result.Success, result.Reason);
        Assert.True(File.Exists(Path.Combine(projectRoot, "index.html")));
        Assert.False(result.Steps[0].Success);
        Assert.Contains(result.Steps, step => step.Action == AgentAction.ActionCreateFile && !step.Success &&
            step.ResultPreview?.Contains("content contiene solo la ruta", StringComparison.OrdinalIgnoreCase) == true);
        Assert.Contains(result.Steps, step => step.Action == AgentAction.ActionCreateFile && step.Success);
        Assert.Equal("no_aplicable", result.Checkpoint?.HarnessState);
        Assert.Contains("Plan B", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no aplican", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, llm.CallCount);
        Assert.Contains(llm.Prompts[1], message => message.Contains("PLAN B ACTIVADO", StringComparison.Ordinal));
        Assert.Contains(llm.Prompts[2], message => message.Contains("No repitas la ruta ni el nombre del archivo como contenido.", StringComparison.Ordinal));
        Assert.Contains(progress.Updates, update => update.Message?.Contains("Plan B activado", StringComparison.OrdinalIgnoreCase) == true);
        Assert.DoesNotContain(result.Steps, step =>
            step.Action is AgentAction.ActionBuild or AgentAction.ActionTest);
    }

    [Fact]
    public async Task RunAsync_AccionCorrectivaInvalida_PlanBReanalizaYEmiteAccionPermitida()
    {
        var projectRoot = TempDir();
        Directory.CreateDirectory(projectRoot);
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 8, installedModels: new[] { "qwen2.5-coder:3b" });
        using var http = new HttpClient(new StubOllamaHandler());
        using var session = new LocalModelSession(http);
        await session.EnsureAvailableAsync("qwen2.5-coder:3b", CancellationToken.None);

        var llm = new SequenceLlmClient(
            "{\"action\":\"read_file\",\"path\":\"missing.html\"}",
            "{\"action\":\"replace_document\",\"path\":\"index.html\",\"content\":\"<html>Condor</html>\"}",
            "{\"action\":\"create_file\",\"path\":\"index.html\",\"content\":\"<!doctype html><html lang=\\\"es\\\"><body>Condor</body></html>\"}");
        var service = new AgentService(
            store,
            assessment,
            new AgentLimits { MaxIterations = 1, MaxPlanBIterations = 2 },
            llm,
            new StubLlmDiagnostics(),
            session,
            () => projectRoot,
            CreateModelAutoSetup(store, assessment, "qwen2.5-coder:3b"));

        var result = await service.RunAsync("Crea una pagina web sencilla en index.html", cancellationToken: CancellationToken.None);

        Assert.True(result.Success, result.Reason);
        Assert.True(File.Exists(Path.Combine(projectRoot, "index.html")));
        Assert.Equal(3, llm.CallCount);
        Assert.Contains(result.Steps, step => step.Action == "replace_document" && !step.Success);
        Assert.Contains(result.Steps, step => step.Action == AgentAction.ActionCreateFile && step.Success);
        Assert.Contains(llm.Prompts[2], message =>
            message.Contains("nombres permitidos", StringComparison.Ordinal) &&
            message.Contains("create_file", StringComparison.Ordinal) &&
            message.Contains("Accion no permitida: replace_document", StringComparison.Ordinal));
        Assert.Equal(2, result.Checkpoint?.PlanBIterationsUsed);
        Assert.Equal("no_aplicable", result.Checkpoint?.HarnessState);
    }

    [Fact]
    public async Task RunAsync_PlanBIntentaTerminarSinAccion_ReintentaYCorrige()
    {
        var projectRoot = TempDir();
        Directory.CreateDirectory(projectRoot);
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 8, installedModels: new[] { "qwen2.5-coder:3b" });
        using var http = new HttpClient(new StubOllamaHandler());
        using var session = new LocalModelSession(http);
        await session.EnsureAvailableAsync("qwen2.5-coder:3b", CancellationToken.None);

        var llm = new SequenceLlmClient(
            "{\"action\":\"read_file\",\"path\":\"missing.html\"}",
            "{\"action\":\"done\",\"reason\":\"No hacen falta cambios\"}",
            "{\"action\":\"create_file\",\"path\":\"index.html\",\"content\":\"<!doctype html><html lang=\\\"es\\\"><body>Condor</body></html>\"}");
        var service = new AgentService(
            store,
            assessment,
            new AgentLimits { MaxIterations = 1, MaxPlanBIterations = 2 },
            llm,
            new StubLlmDiagnostics(),
            session,
            () => projectRoot,
            CreateModelAutoSetup(store, assessment, "qwen2.5-coder:3b"));

        var result = await service.RunAsync("Crea una pagina web sencilla en index.html", cancellationToken: CancellationToken.None);

        Assert.True(result.Success, result.Reason);
        Assert.True(File.Exists(Path.Combine(projectRoot, "index.html")));
        Assert.Equal(3, llm.CallCount);
        Assert.Contains(result.Steps, step => step.Action == AgentAction.ActionDone && !step.Success);
        Assert.Contains(result.Steps, step => step.Action == AgentAction.ActionCreateFile && step.Success);
        Assert.Equal("plan_b_corregido", result.Checkpoint?.LastDecision);
        Assert.Equal(2, result.Checkpoint?.PlanBIterationsUsed);
    }

    [Fact]
    public async Task RunAsync_PlanBObservaAntesDeCorregir_NoDaPorRecuperadoUnReadFile()
    {
        var projectRoot = TempDir();
        Directory.CreateDirectory(projectRoot);
        await File.WriteAllTextAsync(Path.Combine(projectRoot, "index.html"), "<html><body>Anterior</body></html>");
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 8, installedModels: new[] { "qwen2.5-coder:3b" });
        using var http = new HttpClient(new StubOllamaHandler());
        using var session = new LocalModelSession(http);
        await session.EnsureAvailableAsync("qwen2.5-coder:3b", CancellationToken.None);

        var updated = "<!doctype html><html lang=\"es\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><body>Adaptable</body></html>";
        var llm = new SequenceLlmClient(
            "{\"action\":\"read_file\",\"path\":\"missing.html\"}",
            "{\"action\":\"read_file\",\"path\":\"index.html\"}",
            "{\"action\":\"edit_file\",\"path\":\"index.html\",\"content\":" +
            System.Text.Json.JsonSerializer.Serialize(updated) + "}");
        var service = new AgentService(
            store,
            assessment,
            new AgentLimits { MaxIterations = 1, MaxPlanBIterations = 2 },
            llm,
            new StubLlmDiagnostics(),
            session,
            () => projectRoot,
            CreateModelAutoSetup(store, assessment, "qwen2.5-coder:3b"));

        var result = await service.RunAsync("Actualiza index.html para que sea adaptable", cancellationToken: CancellationToken.None);

        Assert.True(result.Success, result.Reason);
        Assert.Equal(updated, await File.ReadAllTextAsync(Path.Combine(projectRoot, "index.html")));
        Assert.Equal(3, llm.CallCount);
        Assert.Contains(result.Steps, step => step.Action == AgentAction.ActionReadFile && step.Success);
        Assert.Contains(result.Steps, step => step.Action == AgentAction.ActionEditFile && step.Success);
        Assert.Equal(2, result.Checkpoint?.PlanBIterationsUsed);
        Assert.Equal("plan_b_corregido", result.Checkpoint?.LastDecision);
    }

    [Fact]
    public async Task RunAsync_PruebaDeRegresionConFalloEsperado_NoReportaExitoSinCrearLaPrueba()
    {
        var projectRoot = TempDir();
        Directory.CreateDirectory(Path.Combine(projectRoot, "Sample"));
        Directory.CreateDirectory(Path.Combine(projectRoot, "Sample.Tests"));
        await File.WriteAllTextAsync(Path.Combine(projectRoot, "Sample", "Calculator.cs"),
            "namespace Sample;\npublic static class Calculator { public static int Add(int left, int right) => left - right; }\n");

        var intention = "perfil: pruebas. Crea una prueba xUnit en Sample.Tests/CalculatorTests.cs para Add(-3, 5) == 2. La base contiene un defecto y el fallo esperado debe informarse; no edites producción.";
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 8, installedModels: new[] { "qwen2.5-coder:3b" });
        using var http = new HttpClient(new StubOllamaHandler());
        using var session = new LocalModelSession(http);
        await session.EnsureAvailableAsync("qwen2.5-coder:3b", CancellationToken.None);

        var llm = new SequenceLlmClient(
            "{\"action\":\"list_dir\",\"path\":\".\"}",
            "{\"action\":\"list_dir\",\"path\":\"Sample\"}",
            "{\"action\":\"list_dir\",\"path\":\"Sample.Tests\"}",
            "{\"action\":\"read_file\",\"path\":\"Sample/Calculator.cs\"}",
            "{\"action\":\"plan\",\"objective\":\"Agregar una prueba de regresión\",\"scopePaths\":[\"Sample.Tests/CalculatorTests.cs\"],\"excludedPaths\":[\"Sample/Calculator.cs\"],\"planSteps\":[\"Crear el caso xUnit\",\"Ejecutar la prueba enfocada\"],\"expectedResults\":[\"Existe el caso Add_NegativeNumberAndPositiveNumber_ReturnsSum\",\"La prueba observa el fallo de la suma\"],\"verificationSteps\":[\"Leer el archivo de prueba\",\"Ejecutar dotnet test con el filtro del caso\"],\"riskLevel\":\"medium\"}",
            "{\"action\":\"done\",\"reason\":\"El fallo esperado confirma la regresión.\"}",
            "{\"action\":\"done\",\"reason\":\"El fallo esperado confirma la regresión.\"}");
        var service = new AgentService(
            store,
            assessment,
            new AgentLimits { MaxIterations = 7, MaxPlanBIterations = 1 },
            llm,
            new StubLlmDiagnostics(),
            session,
            () => projectRoot,
            CreateModelAutoSetup(store, assessment, "qwen2.5-coder:3b"));

        var result = await service.RunAsync(intention, cancellationToken: CancellationToken.None);

        Assert.False(result.Success, result.Reason);
        Assert.False(File.Exists(Path.Combine(projectRoot, "Sample.Tests", "CalculatorTests.cs")));
        Assert.DoesNotContain(result.Steps, step => step.Success && step.Action == AgentAction.ActionCreateFile);
    }

    [Fact]
    public async Task RunAsync_PlanParaArchivoNuevo_ExploraSuDirectorioPadre_AceptaPlan()
    {
        var projectRoot = TempDir();
        Directory.CreateDirectory(Path.Combine(projectRoot, "Sample.Tests"));
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 8, installedModels: new[] { "qwen2.5-coder:3b" });
        using var http = new HttpClient(new StubOllamaHandler());
        using var session = new LocalModelSession(http);
        await session.EnsureAvailableAsync("qwen2.5-coder:3b", CancellationToken.None);

        var llm = new SequenceLlmClient(
            "{\"action\":\"plan\",\"objective\":\"Crear el caso de regresión\",\"scopePaths\":[\"Sample.Tests/CalculatorTests.cs\"],\"excludedPaths\":[\"Sample/Calculator.cs\"],\"planItems\":[{\"task\":\"Crear el caso xUnit\",\"expectedResult\":\"Existe el archivo de prueba\",\"verificationStep\":\"Revisar el archivo creado\"}],\"riskLevel\":\"low\"}",
            "{\"action\":\"done\",\"reason\":\"Plan declarado\"}");
        var service = new AgentService(
            store,
            assessment,
            new AgentLimits { MaxIterations = 2, MaxPlanBIterations = 1 },
            llm,
            new StubLlmDiagnostics(),
            session,
            () => projectRoot,
            CreateModelAutoSetup(store, assessment, "qwen2.5-coder:3b"));

        var result = await service.RunAsync(
            "Crea una prueba xUnit en Sample.Tests/CalculatorTests.cs; no edites producción.",
            cancellationToken: CancellationToken.None);

        Assert.Contains(result.Steps, step =>
            step.Success && step.Action == AgentAction.ActionListDir && step.Path == "Sample.Tests");
        Assert.Contains(result.Steps, step => step.Success && step.Action == AgentAction.ActionPlan);
        Assert.DoesNotContain(result.Steps, step => step.Action == AgentAction.ActionPlan && !step.Success);
        Assert.Single(result.Steps, step => step.Action == AgentAction.ActionPlan);
        Assert.Equal("Medium", result.Checkpoint?.RiskLevel);
        Assert.False(File.Exists(Path.Combine(projectRoot, "Sample.Tests", "CalculatorTests.cs")));
    }

    [Fact]
    public async Task RunAsync_RutaUnicaExplicita_CondorRegistraPlanAcotadoYContinua()
    {
        var projectRoot = TempDir();
        Directory.CreateDirectory(Path.Combine(projectRoot, "src"));
        await File.WriteAllTextAsync(Path.Combine(projectRoot, "Sample.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup></Project>");
        await File.WriteAllTextAsync(Path.Combine(projectRoot, "src", "Production.cs"),
            "namespace Sample; public static class Production { public static string Value => \"stable\"; }");
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 8, installedModels: new[] { "qwen2.5-coder:3b" });
        using var http = new HttpClient(new StubOllamaHandler());
        using var session = new LocalModelSession(http);
        await session.EnsureAvailableAsync("qwen2.5-coder:3b", CancellationToken.None);

        var llm = new SequenceLlmClient(
            "{\"action\":\"create_file\",\"path\":\"src/Marker.cs\",\"content\":\"namespace Sample; public static class Marker { public static string Value => \\\"Condor\\\"; }\"}",
            "{\"action\":\"git_diff\"}",
            "{\"action\":\"done\",\"reason\":\"Se creó el archivo solicitado.\"}");
        var service = new AgentService(
            store,
            assessment,
            new AgentLimits { MaxIterations = 4, MaxPlanBIterations = 1 },
            llm,
            new StubLlmDiagnostics(),
            session,
            () => projectRoot,
            CreateModelAutoSetup(store, assessment, "qwen2.5-coder:3b"));

        var result = await service.RunAsync(
            "Crea exactamente src/Marker.cs con una clase estática Sample.Marker. No modifiques src/Production.cs, que está excluido.",
            cancellationToken: CancellationToken.None);

        Assert.True(result.Success, "Riesgo: " + result.Checkpoint?.RiskLevel + Environment.NewLine + result.Reason + Environment.NewLine + string.Join(Environment.NewLine, result.Steps.Select(step =>
            step.Action + " " + step.Path + " · " + step.Success + " · " + step.ResultPreview)));
        Assert.Contains("static class Marker", await File.ReadAllTextAsync(Path.Combine(projectRoot, "src", "Marker.cs")));
        Assert.Equal("src/Marker.cs", Assert.Single(result.Checkpoint!.DeclaredScope!));
        Assert.Equal("src/Production.cs", Assert.Single(result.Checkpoint.ExcludedScope!));
        Assert.Single(result.Checkpoint.PlanSteps!);
        Assert.Single(result.Checkpoint.ExpectedResults!);
        Assert.Single(result.Checkpoint.VerificationSteps!);
        Assert.Single(result.Steps, step => step.Action == AgentAction.ActionPlan && step.Success);
        Assert.Contains(result.Steps, step => step.Action == AgentAction.ActionCreateFile && step.Path == "src/Marker.cs" && step.Success);
        Assert.Equal(3, llm.CallCount);
    }

    [Fact]
    public async Task RunAsync_PlanRechazado_RutaUnicaExplicita_RecuperaConPlanDeterminista()
    {
        var projectRoot = TempDir();
        Directory.CreateDirectory(Path.Combine(projectRoot, "src"));
        await File.WriteAllTextAsync(Path.Combine(projectRoot, "Sample.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup></Project>");
        await File.WriteAllTextAsync(Path.Combine(projectRoot, "src", "Production.cs"),
            "namespace Sample; public static class Production { public static string Value => \"stable\"; }");
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 8, installedModels: new[] { "qwen2.5-coder:3b" });
        using var http = new HttpClient(new StubOllamaHandler());
        using var session = new LocalModelSession(http);
        await session.EnsureAvailableAsync("qwen2.5-coder:3b", CancellationToken.None);

        var llm = new SequenceLlmClient(
            "{\"action\":\"plan\",\"objective\":\"Crear la clase solicitada\",\"scopePaths\":[\"src/Marker.cs\"],\"excludedPaths\":[\"src/Production.cs\"],\"planItems\":[{\"task\":\"Crear Marker\",\"expectedResult\":\"\",\"verificationStep\":\"Compilar\"}]}",
            "{\"action\":\"create_file\",\"path\":\"src/Marker.cs\",\"content\":\"namespace Sample; public static class Marker { public static string Value => \\\"Condor\\\"; }\"}",
            "{\"action\":\"git_diff\"}",
            "{\"action\":\"done\",\"reason\":\"La clase solicitada compila.\"}");
        var service = new AgentService(
            store,
            assessment,
            new AgentLimits { MaxIterations = 7, MaxPlanBIterations = 1 },
            llm,
            new StubLlmDiagnostics(),
            session,
            () => projectRoot,
            CreateModelAutoSetup(store, assessment, "qwen2.5-coder:3b"));

        var result = await service.RunAsync(
            "Crea exactamente src/Marker.cs con una clase estática Sample.Marker. No modifiques src/Production.cs, que está excluido.",
            cancellationToken: CancellationToken.None);

        Assert.True(result.Success, result.Reason + Environment.NewLine + string.Join(Environment.NewLine, result.Steps.Select(step =>
            step.Action + " " + step.Path + " · " + step.Success + " · " + step.ResultPreview)));
        Assert.Contains(result.Steps, step => step.Action == AgentAction.ActionPlan && !step.Success);
        Assert.Contains(result.Steps, step => step.Action == AgentAction.ActionPlan && step.Success &&
            step.ResultPreview!.Contains("src/Production.cs", StringComparison.Ordinal));
        Assert.Contains(result.Steps, step => step.Action == AgentAction.ActionCreateFile && step.Path == "src/Marker.cs" && step.Success);
        Assert.Equal("src/Production.cs", Assert.Single(result.Checkpoint!.ExcludedScope!));
        Assert.Contains("static class Production", await File.ReadAllTextAsync(Path.Combine(projectRoot, "src", "Production.cs")));
        Assert.Equal(4, llm.CallCount);
    }

    [Fact]
    public async Task RunAsync_PlanInvalido_EnviaEsquemaJsonYEsperaCorreccionAntesDeEscribir()
    {
        var projectRoot = TempDir();
        Directory.CreateDirectory(Path.Combine(projectRoot, "Sample"));
        Directory.CreateDirectory(Path.Combine(projectRoot, "Sample.Tests"));
        await File.WriteAllTextAsync(Path.Combine(projectRoot, "Sample", "Calculator.cs"),
            "namespace Sample;\npublic static class Calculator { public static int Add(int left, int right) => left - right; }\n");

        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 8, installedModels: new[] { "qwen2.5-coder:3b" });
        using var http = new HttpClient(new StubOllamaHandler());
        using var session = new LocalModelSession(http);
        await session.EnsureAvailableAsync("qwen2.5-coder:3b", CancellationToken.None);

        var llm = new SequenceLlmClient(
            "{\"action\":\"list_dir\",\"path\":\".\"}",
            "{\"action\":\"list_dir\",\"path\":\"Sample.Tests\"}",
            "{\"action\":\"read_file\",\"path\":\"Sample/Calculator.cs\"}",
            "{\"action\":\"plan\",\"objective\":\"Crear la prueba solicitada\",\"scopePaths\":[\"Sample.Tests/CalculatorTests.cs\"],\"excludedPaths\":[\"Sample/Calculator.cs\"],\"planSteps\":[\"Crear el caso xUnit\",\"Ejecutar la prueba filtrada\",\"Informar el resultado\"],\"expectedResults\":[\"Existe el caso solicitado\",\"La prueba termina con un resultado observado\"],\"verificationSteps\":[\"Leer el archivo de prueba\",\"Ejecutar dotnet test con el filtro del caso\"]}",
            "{\"action\":\"plan\",\"objective\":\"Crear la prueba solicitada\",\"scopePaths\":[\"Sample.Tests/CalculatorTests.cs\"],\"excludedPaths\":[\"Sample/Calculator.cs\"],\"planItems\":[{\"task\":\"Crear el caso xUnit\",\"expectedResult\":\"Existe el caso solicitado\",\"verificationStep\":\"Leer el archivo de prueba\"},{\"task\":\"Ejecutar la prueba filtrada\",\"expectedResult\":\"La prueba termina con un resultado observado\",\"verificationStep\":\"Ejecutar dotnet test con el filtro del caso\"},{\"task\":\"Informar el resultado\",\"expectedResult\":\"El informe indica el resultado real\",\"verificationStep\":\"Revisar la salida del comando\"}]}",
            "{\"action\":\"clarify\",\"reason\":\"Plan registrado para continuar la prueba.\"}");
        var service = new AgentService(
            store,
            assessment,
            new AgentLimits { MaxIterations = 7, MaxPlanBIterations = 1 },
            llm,
            new StubLlmDiagnostics(),
            session,
            () => projectRoot,
            CreateModelAutoSetup(store, assessment, "qwen2.5-coder:3b"));

        var result = await service.RunAsync(
            "perfil: pruebas. Crea exactamente Sample.Tests/CalculatorTests.cs con una prueba xUnit para Calculator.Add(-3, 5) == 2. No edites producción.",
            cancellationToken: CancellationToken.None);

        Assert.True(result.AwaitingUserInput);
        Assert.Contains(result.Steps, step => step.Action == AgentAction.ActionPlan && !step.Success &&
            step.ResultPreview!.Contains("Cada tarea del plan requiere exactamente un resultado esperado", StringComparison.Ordinal));
        Assert.Contains(result.Steps, step => step.Action == AgentAction.ActionPlan && step.Success);
        Assert.Contains(llm.Prompts[4], message => message.Contains("El plan rechazado contiene 3 tareas, 2 resultados esperados y 2 comprobaciones.", StringComparison.Ordinal));
        Assert.Contains(llm.Prompts[4], message => message.Contains("exactamente 3 objetos planItems", StringComparison.Ordinal));
        Assert.Contains(llm.Prompts[4], message => message.Contains("\"action\":\"plan\"", StringComparison.Ordinal) &&
            message.Contains("\"planItems\"", StringComparison.Ordinal) &&
            message.Contains("\"expectedResult\"", StringComparison.Ordinal) &&
            message.Contains("\"verificationStep\"", StringComparison.Ordinal));
        Assert.False(File.Exists(Path.Combine(projectRoot, "Sample.Tests", "CalculatorTests.cs")));
    }

    [Fact]
    public async Task RunAsync_PlanSinScopePaths_RutaUnicaSolicitadaYExplorada_RecuperaAlcanceSeguro()
    {
        var projectRoot = TempDir();
        Directory.CreateDirectory(projectRoot);
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 8, installedModels: new[] { "qwen2.5-coder:3b" });
        using var http = new HttpClient(new StubOllamaHandler());
        using var session = new LocalModelSession(http);
        await session.EnsureAvailableAsync("qwen2.5-coder:3b", CancellationToken.None);

        var llm = new SequenceLlmClient(
            "{\"action\":\"list_dir\",\"path\":\".\"}",
            "{\"action\":\"plan\",\"objective\":\"Crear saludo.txt\",\"planItems\":[{\"task\":\"Crear saludo.txt\",\"expectedResult\":\"saludo.txt contiene Condor funciona\",\"verificationStep\":\"Leer saludo.txt y comprobar el contenido\"}],\"riskLevel\":\"low\"}",
            "{\"action\":\"create_file\",\"path\":\"saludo.txt\",\"content\":\"Condor funciona\"}",
            "{\"action\":\"read_file\",\"path\":\"saludo.txt\"}",
            "{\"action\":\"git_diff\"}",
            "{\"action\":\"done\",\"reason\":\"El archivo fue creado y comprobado.\"}");
        var service = new AgentService(
            store,
            assessment,
            new AgentLimits { MaxIterations = 8, MaxPlanBIterations = 1 },
            llm,
            new StubLlmDiagnostics(),
            session,
            () => projectRoot,
            CreateModelAutoSetup(store, assessment, "qwen2.5-coder:3b"));

        var result = await service.RunAsync(
            "Crea exactamente el archivo saludo.txt con el texto exacto Condor funciona. No modifiques otros archivos.",
            cancellationToken: CancellationToken.None);

        Assert.True(result.Success, result.Reason);
        Assert.Equal("Condor funciona", await File.ReadAllTextAsync(Path.Combine(projectRoot, "saludo.txt")));
        Assert.Equal("saludo.txt", Assert.Single(result.Checkpoint!.DeclaredScope!));
        Assert.Contains(result.Steps, step => step.Action == "plan_scope" && step.Success && step.Path == "saludo.txt");
        Assert.Contains(result.Steps, step => step.Action == AgentAction.ActionPlan && step.Success);
        Assert.Equal(6, llm.CallCount);
    }

    [Fact]
    public async Task RunAsync_PlanConRutaDePerfilErronea_LaReduceAlArchivoSolicitado()
    {
        var projectRoot = TempDir();
        Directory.CreateDirectory(projectRoot);
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 8, installedModels: new[] { "qwen2.5-coder:3b" });
        using var http = new HttpClient(new StubOllamaHandler());
        using var session = new LocalModelSession(http);
        await session.EnsureAvailableAsync("qwen2.5-coder:3b", CancellationToken.None);

        var llm = new SequenceLlmClient(
            "{\"action\":\"list_dir\",\"path\":\".\"}",
            "{\"action\":\"plan\",\"objective\":\"Crear saludo.txt\",\"scopePaths\":[\"localappdata/temp/otro-proyecto/saludo.txt\"],\"planItems\":[{\"task\":\"Crear saludo.txt\",\"expectedResult\":\"saludo.txt contiene Condor funciona\",\"verificationStep\":\"Leer saludo.txt y comprobar el contenido\"}],\"riskLevel\":\"low\"}",
            "{\"action\":\"create_file\",\"path\":\"saludo.txt\",\"content\":\"Condor funciona\"}",
            "{\"action\":\"read_file\",\"path\":\"saludo.txt\"}",
            "{\"action\":\"git_diff\"}",
            "{\"action\":\"done\",\"reason\":\"El archivo fue creado y comprobado.\"}");
        var service = new AgentService(
            store,
            assessment,
            new AgentLimits { MaxIterations = 8, MaxPlanBIterations = 1 },
            llm,
            new StubLlmDiagnostics(),
            session,
            () => projectRoot,
            CreateModelAutoSetup(store, assessment, "qwen2.5-coder:3b"));

        var result = await service.RunAsync(
            "Crea exactamente el archivo saludo.txt con el texto exacto Condor funciona. No modifiques otros archivos.",
            cancellationToken: CancellationToken.None);

        Assert.True(result.Success, result.Reason);
        Assert.Equal("Condor funciona", await File.ReadAllTextAsync(Path.Combine(projectRoot, "saludo.txt")));
        Assert.Equal("saludo.txt", Assert.Single(result.Checkpoint!.DeclaredScope!));
        Assert.Contains(result.Steps, step => step.Action == "plan_scope" && step.Success && step.Path == "saludo.txt");
        Assert.Equal(6, llm.CallCount);
    }

    [Fact]
    public async Task RunAsync_PlanRepetidoTrasListarDirectorio_ConservaLaGuardiaYEjecutaLaTarea()
    {
        var projectRoot = TempDir();
        Directory.CreateDirectory(projectRoot);
        Directory.CreateDirectory(Path.Combine(projectRoot, "src"));
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 8, installedModels: new[] { "qwen2.5-coder:3b" });
        using var http = new HttpClient(new StubOllamaHandler());
        using var session = new LocalModelSession(http);
        await session.EnsureAvailableAsync("qwen2.5-coder:3b", CancellationToken.None);

        const string plan = "{\"action\":\"plan\",\"objective\":\"Crear saludo.txt\",\"scopePaths\":[\"saludo.txt\"],\"planItems\":[{\"task\":\"Crear saludo.txt\",\"expectedResult\":\"saludo.txt contiene Condor funciona\",\"verificationStep\":\"Leer saludo.txt y comprobar el contenido\"}],\"riskLevel\":\"low\"}";
        var llm = new SequenceLlmClient(
            "{\"action\":\"list_dir\",\"path\":\".\"}",
            plan,
            "{\"action\":\"list_dir\",\"path\":\"src\"}",
            plan.Replace("Crear saludo.txt", "Añadir saludo.txt", StringComparison.Ordinal),
            "{\"action\":\"create_file\",\"path\":\"saludo.txt\",\"content\":\"Condor funciona\"}",
            "{\"action\":\"read_file\",\"path\":\"saludo.txt\"}",
            "{\"action\":\"git_diff\"}",
            "{\"action\":\"done\",\"reason\":\"El archivo fue creado y comprobado.\"}");
        var service = new AgentService(
            store,
            assessment,
            new AgentLimits { MaxIterations = 10, MaxPlanBIterations = 1 },
            llm,
            new StubLlmDiagnostics(),
            session,
            () => projectRoot,
            CreateModelAutoSetup(store, assessment, "qwen2.5-coder:3b"));

        var result = await service.RunAsync(
            "Crea exactamente el archivo saludo.txt con el texto exacto Condor funciona.",
            cancellationToken: CancellationToken.None);

        Assert.True(result.Success, result.Reason + Environment.NewLine + string.Join(Environment.NewLine, result.Steps.Select(step =>
            step.Action + " " + step.Path + " · " + step.Success + " · " + step.ResultPreview)));
        Assert.Equal("Condor funciona", await File.ReadAllTextAsync(Path.Combine(projectRoot, "saludo.txt")));
        Assert.Contains(result.Steps, step => step.Action == AgentAction.ActionPlan && !step.Success &&
            step.ResultPreview!.Contains("plan ya registrado", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(8, llm.CallCount);
    }

    [Fact]
    public async Task RunAsync_PlanSinScopePaths_RutaSoloProhibida_NoAutorizaElArchivo()
    {
        var projectRoot = TempDir();
        Directory.CreateDirectory(Path.Combine(projectRoot, "src"));
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 8, installedModels: new[] { "qwen2.5-coder:3b" });
        using var http = new HttpClient(new StubOllamaHandler());
        using var session = new LocalModelSession(http);
        await session.EnsureAvailableAsync("qwen2.5-coder:3b", CancellationToken.None);

        const string invalidPlan = "{\"action\":\"plan\",\"objective\":\"Crear una nota\",\"planItems\":[{\"task\":\"Crear la nota solicitada\",\"expectedResult\":\"Existe la nota\",\"verificationStep\":\"Leer la nota creada\"}]}";
        var llm = new SequenceLlmClient(
            "{\"action\":\"list_dir\",\"path\":\".\"}",
            "{\"action\":\"list_dir\",\"path\":\"src\"}",
            invalidPlan,
            invalidPlan);
        var service = new AgentService(
            store,
            assessment,
            new AgentLimits { MaxIterations = 6, MaxPlanBIterations = 1 },
            llm,
            new StubLlmDiagnostics(),
            session,
            () => projectRoot,
            CreateModelAutoSetup(store, assessment, "qwen2.5-coder:3b"));

        var result = await service.RunAsync(
            "Crea una nota en la raíz y no modifiques src/Production.cs.",
            cancellationToken: CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Steps, step => step.Action == AgentAction.ActionPlan && !step.Success);
        Assert.DoesNotContain(result.Steps, step => step.Action == "plan_scope");
        Assert.Contains(llm.Prompts[3], message =>
            message.Contains("scopePaths", StringComparison.Ordinal) &&
            message.Contains("no indicó una ruta concreta", StringComparison.Ordinal));
        Assert.False(File.Exists(Path.Combine(projectRoot, "src", "Production.cs")));
        Assert.Equal(4, llm.CallCount);
    }

    [Fact]
    public async Task RunAsync_RespuestaEstructuradaInvalida_SolicitaUnaCorreccionYExplicaElProtocolo()
    {
        var projectRoot = TempDir();
        Directory.CreateDirectory(projectRoot);
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 8, installedModels: new[] { "qwen2.5-coder:3b" });
        using var http = new HttpClient(new StubOllamaHandler());
        using var session = new LocalModelSession(http);
        await session.EnsureAvailableAsync("qwen2.5-coder:3b", CancellationToken.None);

        var llm = new SequenceLlmClient(
            "respuesta sin JSON",
            "{\"action\":\"clarify\",\"reason\":\"Necesito confirmar una ruta.\"}");
        var service = new AgentService(
            store,
            assessment,
            new AgentLimits { MaxIterations = 1, MaxPlanBIterations = 1 },
            llm,
            new StubLlmDiagnostics(),
            session,
            () => projectRoot,
            CreateModelAutoSetup(store, assessment, "qwen2.5-coder:3b"));

        var result = await service.RunAsync(
            "Crea la prueba solicitada en Sample.Tests/CalculatorTests.cs.",
            cancellationToken: CancellationToken.None);

        Assert.True(result.AwaitingUserInput, result.Reason + "; calls=" + llm.CallCount + "; decision=" + result.Checkpoint?.LastDecision);
        Assert.Equal(2, llm.CallCount);
        Assert.Contains(llm.Prompts[1], message =>
            message.Contains("La respuesta anterior no produjo una acción JSON válida", StringComparison.Ordinal));
        Assert.Contains(llm.Prompts[1], message => message.Contains("objeto JSON válido", StringComparison.Ordinal));
        Assert.Contains(llm.Prompts[1], message => message.Contains("planItems", StringComparison.Ordinal));
        Assert.Contains(llm.Prompts[1], message => message.Contains("sin Markdown", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RunAsync_TestFallaContraCodigoExcluido_ConservaLaPruebaYReportaResultado()
    {
        var projectRoot = TempDir();
        Directory.CreateDirectory(Path.Combine(projectRoot, "Sample"));
        Directory.CreateDirectory(Path.Combine(projectRoot, "Sample.Tests"));
        await File.WriteAllTextAsync(Path.Combine(projectRoot, "Cap028Validation.slnx"), """
            <Solution>
              <Project Path="Sample.Tests/Sample.Tests.csproj" />
              <Project Path="Sample/Sample.csproj" />
            </Solution>
            """);
        await File.WriteAllTextAsync(Path.Combine(projectRoot, "Sample", "Sample.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup>
            </Project>
            """);
        const string production = "namespace Sample;\npublic static class Calculator { public static int Add(int left, int right) => left - right; }\n";
        await File.WriteAllTextAsync(Path.Combine(projectRoot, "Sample", "Calculator.cs"), production);
        await File.WriteAllTextAsync(Path.Combine(projectRoot, "Sample.Tests", "Sample.Tests.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><IsPackable>false</IsPackable></PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
                <PackageReference Include="xunit" Version="2.9.3" />
                <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
              </ItemGroup>
              <ItemGroup><ProjectReference Include="..\Sample\Sample.csproj" /></ItemGroup>
            </Project>
            """);

        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 8, installedModels: new[] { "qwen2.5-coder:3b" });
        using var http = new HttpClient(new StubOllamaHandler());
        using var session = new LocalModelSession(http);
        await session.EnsureAvailableAsync("qwen2.5-coder:3b", CancellationToken.None);

        const string generatedTest = """
            using Xunit;
            namespace Sample.Tests;
            public sealed class CalculatorTests
            {
                [Fact]
                public void Add_NegativeNumberAndPositiveNumber_ReturnsSum()
                {
                    Assert.Equal(2, Calculator.Add(-3, 5));
                }
            }
            """;
        const string userRequest = "Crea exactamente Sample.Tests/CalculatorTests.cs con un test xUnit que verifique Calculator.Add(-3, 5) == 2. No edites producción. Alcance Sample.Tests/CalculatorTests.cs; excluye Sample/Calculator.cs.";
        using var tuiHost = new TuiHost(110, 34);
        tuiHost.Enter();
        tuiHost.ShowSession("qwen2.5-coder:3b");
        var tuiInput = new TuiInput(tuiHost);
        tuiInput.SetText(userRequest);
        tuiInput.BeginAgentPicker(new[]
        {
            new SelectItem("plan", "Plan", "Crea y guarda un plan", Array.Empty<string>(), false, null, "Enter"),
            new SelectItem("codificar", "Codificar", "Ejecuta tareas con herramientas", Array.Empty<string>(), false, null, "Enter")
        }, activeAgentId: "plan");
        foreach (var character in "Codificar")
        {
            tuiInput.Handle(new ConsoleKeyInfo(character, ConsoleKey.A, false, false, false));
        }

        Assert.Contains("Codificar", Ansi.StripSgr(tuiHost.SnapshotFullFrame()));
        Assert.Equal("codificar", tuiInput.SelectedAgentId);
        Assert.Equal(InputAction.AgentSelected,
            tuiInput.Handle(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false)));
        Assert.Equal(userRequest, tuiInput.Buffer);
        Assert.Equal(InputAction.Submit,
            tuiInput.Handle(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false)));
        var submittedText = tuiInput.Buffer.Trim();
        var agentRoute = Assert.IsType<FreeIntentionRoute>(
            CondorTui.RouteAgentMode(tuiInput.SelectedAgentId!, submittedText, session.ActiveModel));

        var llm = new SequenceLlmClient(
            "{\"action\":\"list_dir\",\"path\":\"Sample.Tests\"}",
            "{\"action\":\"read_file\",\"path\":\"Sample/Calculator.cs\"}",
            "{\"action\":\"plan\",\"objective\":\"Agregar el test solicitado\",\"scopePaths\":[\"Sample.Tests/CalculatorTests.cs\"],\"excludedPaths\":[\"Sample/Calculator.cs\"],\"planItems\":[{\"task\":\"Crear el test xUnit\",\"expectedResult\":\"El archivo afirma que Add(-3, 5) devuelve 2\",\"verificationStep\":\"Ejecutar el test y observar el resultado\"}],\"riskLevel\":\"medium\"}",
            "{\"action\":\"create_file\",\"path\":\"Sample.Tests/CalculatorTests.cs\",\"content\":" + System.Text.Json.JsonSerializer.Serialize(generatedTest) + "}");
        var service = new AgentService(
            store,
            assessment,
            new AgentLimits { MaxIterations = 8 },
            llm,
            new StubLlmDiagnostics(),
            session,
            () => projectRoot,
            CreateModelAutoSetup(store, assessment, "qwen2.5-coder:3b"));

        var result = await CondorTui.ExecuteAgentRouteAsync(
            service, agentRoute, progress: null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("test_fallo_con_alcance_excluido", result.Checkpoint?.LastDecision);
        Assert.Contains("La prueba solicitada se ejecutó y falló", result.Reason, StringComparison.Ordinal);
        Assert.Contains("Expected: 2", result.Reason, StringComparison.Ordinal);
        Assert.Contains("Actual:   -8", result.Reason, StringComparison.Ordinal);
        Assert.Equal(4, llm.CallCount);
        Assert.Equal(generatedTest, await File.ReadAllTextAsync(Path.Combine(projectRoot, "Sample.Tests", "CalculatorTests.cs")));
        Assert.Equal(production, await File.ReadAllTextAsync(Path.Combine(projectRoot, "Sample", "Calculator.cs")));
        Assert.Contains(result.Steps, step => step.Action == AgentAction.ActionTest && !step.Success);
    }

    [Fact]
    public async Task RunAsync_ObservacionRepetidaTrasPlanRechazado_IncluyeMotivoEnPlanB()
    {
        var projectRoot = TempDir();
        Directory.CreateDirectory(Path.Combine(projectRoot, "Sample.Tests"));
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 8, installedModels: new[] { "qwen2.5-coder:3b" });
        using var http = new HttpClient(new StubOllamaHandler());
        using var session = new LocalModelSession(http);
        await session.EnsureAvailableAsync("qwen2.5-coder:3b", CancellationToken.None);

        var llm = new SequenceLlmClient(
            "{\"action\":\"list_dir\",\"path\":\".\"}",
            "{\"action\":\"plan\",\"objective\":\"Crear la prueba solicitada\",\"scopePaths\":[\"Sample.Tests/CalculatorTests.cs\"],\"excludedPaths\":[\"Sample/Calculator.cs\"],\"planItems\":[{\"task\":\"Crear el test\",\"expectedResult\":\"\",\"verificationStep\":\"Ejecutar dotnet test\"}],\"riskLevel\":\"medium\"}",
            "{\"action\":\"list_dir\",\"path\":\".\"}",
            "{\"action\":\"clarify\",\"reason\":\"Necesito confirmar un dato antes de cambiar archivos.\"}");
        var service = new AgentService(
            store,
            assessment,
            new AgentLimits { MaxIterations = 5, MaxPlanBIterations = 1 },
            llm,
            new StubLlmDiagnostics(),
            session,
            () => projectRoot,
            CreateModelAutoSetup(store, assessment, "qwen2.5-coder:3b"));

        var result = await service.RunAsync(
            "Crea exactamente Sample.Tests/CalculatorTests.cs con una prueba. No edites producción.",
            cancellationToken: CancellationToken.None);

        Assert.True(result.AwaitingUserInput, result.Reason);
        Assert.Contains(result.Steps, step => step.Action == AgentAction.ActionPlan && !step.Success);
        Assert.Contains(llm.Prompts[3], message =>
            message.Contains("Ultimo plan rechazado", StringComparison.OrdinalIgnoreCase) &&
            message.Contains("resultado esperado", StringComparison.OrdinalIgnoreCase) &&
            message.Contains("no supongas que hay un plan aceptado", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RunAsync_PlanDeRiesgoCritico_ExploraDirectorioYExplicaMinimoAntesDeReplanificar()
    {
        var projectRoot = TempDir();
        Directory.CreateDirectory(Path.Combine(projectRoot, "Sample.Tests"));
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 8, installedModels: new[] { "qwen2.5-coder:3b" });
        using var http = new HttpClient(new StubOllamaHandler());
        using var session = new LocalModelSession(http);
        await session.EnsureAvailableAsync("qwen2.5-coder:3b", CancellationToken.None);

        const string scope = "Sample.Tests";
        var llm = new SequenceLlmClient(
            "{\"action\":\"plan\",\"objective\":\"Crear la prueba de regresión\",\"scopePaths\":[\"Sample.Tests\"],\"excludedPaths\":[],\"planSteps\":[\"Crear la prueba\"],\"expectedResults\":[\"Existe la prueba solicitada\"],\"verificationSteps\":[\"Leer la prueba\"],\"riskLevel\":\"critical\"}",
            "{\"action\":\"plan\",\"objective\":\"Crear y verificar la prueba de regresión\",\"scopePaths\":[\"Sample.Tests\"],\"excludedPaths\":[],\"planSteps\":[\"Crear la prueba solicitada\",\"Ejecutar la prueba filtrada\",\"Registrar el resultado observado\"],\"expectedResults\":[\"Existe el archivo de prueba\",\"El caso termina con resultado real\",\"El informe describe el resultado\"],\"verificationSteps\":[\"Leer el archivo de prueba\",\"Ejecutar dotnet test con el filtro\",\"Revisar la salida del comando\"],\"riskLevel\":\"critical\"}",
            "{\"action\":\"clarify\",\"reason\":\"Plan válido; continuaré con los pasos autorizados.\"}");
        var service = new AgentService(
            store,
            assessment,
            new AgentLimits { MaxIterations = 5, MaxPlanBIterations = 1 },
            llm,
            new StubLlmDiagnostics(),
            session,
            () => projectRoot,
            CreateModelAutoSetup(store, assessment, "qwen2.5-coder:3b"));

        var result = await service.RunAsync(
            "perfil: pruebas. Crea una prueba de regresión en Sample.Tests/CalculatorTests.cs para Calculator.Add(-3, 5) == 2. No edites producción ni despliegues a producción; ejecuta el caso.",
            cancellationToken: CancellationToken.None);

        Assert.True(result.AwaitingUserInput);
        Assert.Contains(result.Steps, step => step.Success && step.Action == AgentAction.ActionListDir && step.Path == scope);
        Assert.Contains(result.Steps, step => step.Action == AgentAction.ActionPlan && !step.Success &&
            step.ResultPreview!.Contains("requiere al menos 3 pasos y 3 comprobaciones distintas", StringComparison.Ordinal));
        Assert.Contains(result.Steps, step => step.Success && step.Action == AgentAction.ActionPlan);
        Assert.Contains(llm.Prompts[1], message => message.Contains("Incluye al menos 3 objetos planItems distintos", StringComparison.Ordinal));
        Assert.Contains(llm.Prompts[1], message => message.Contains("\"expectedResult\"", StringComparison.Ordinal));
        Assert.False(File.Exists(Path.Combine(projectRoot, "Sample.Tests", "CalculatorTests.cs")));
    }

    [Fact]
    public async Task RunAsync_ObservacionRaizRepetida_ExploraDirectorioSolicitadoAntesDePlanificar()
    {
        var projectRoot = TempDir();
        Directory.CreateDirectory(Path.Combine(projectRoot, "Sample"));
        Directory.CreateDirectory(Path.Combine(projectRoot, "Sample.Tests"));
        await File.WriteAllTextAsync(Path.Combine(projectRoot, "Sample", "Calculator.cs"),
            "namespace Sample;\npublic static class Calculator { public static int Add(int left, int right) => left - right; }\n");

        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 8, installedModels: new[] { "qwen2.5-coder:3b" });
        using var http = new HttpClient(new StubOllamaHandler());
        using var session = new LocalModelSession(http);
        await session.EnsureAvailableAsync("qwen2.5-coder:3b", CancellationToken.None);

        var llm = new SequenceLlmClient(
            "{\"action\":\"list_dir\",\"path\":\".\"}",
            "{\"action\":\"list_dir\",\"path\":\".\"}",
            "{\"action\":\"plan\",\"objective\":\"Crear y comprobar la prueba solicitada\",\"scopePaths\":[\"Sample.Tests/CalculatorTests.cs\"],\"excludedPaths\":[\"Sample/Calculator.cs\"],\"planSteps\":[\"Crear la prueba solicitada\",\"Ejecutar el caso filtrado\",\"Informar el resultado observado\"],\"expectedResults\":[\"Existe el archivo de prueba\",\"El caso termina con un resultado real\",\"El informe registra la salida observada\"],\"verificationSteps\":[\"Leer el archivo de prueba\",\"Ejecutar dotnet test con el filtro\",\"Revisar la salida del comando\"]}",
            "{\"action\":\"clarify\",\"reason\":\"El plan está listo para ejecutarse.\"}");
        var service = new AgentService(
            store,
            assessment,
            new AgentLimits { MaxIterations = 6, MaxPlanBIterations = 1, MaxRedundantObservations = 1 },
            llm,
            new StubLlmDiagnostics(),
            session,
            () => projectRoot,
            CreateModelAutoSetup(store, assessment, "qwen2.5-coder:3b"));

        var result = await service.RunAsync(
            "perfil: pruebas. Crea exactamente Sample.Tests/CalculatorTests.cs para la regresión solicitada. Antes del plan lista Sample.Tests y lee Sample/Calculator.cs. No edites producción.",
            cancellationToken: CancellationToken.None);

        Assert.True(result.AwaitingUserInput);
        Assert.Equal(4, llm.CallCount);
        Assert.Equal(2, result.Steps.Count(step => step.Success && step.Action == AgentAction.ActionListDir && step.Path == "."));
        Assert.Contains(result.Steps, step => step.Success && step.Action == AgentAction.ActionListDir && step.Path == "Sample.Tests");
        Assert.Contains(result.Steps, step => step.Success && step.Action == AgentAction.ActionPlan);
        Assert.DoesNotContain(result.Steps, step => step.Action == AgentAction.ActionPlan && !step.Success);
        Assert.False(File.Exists(Path.Combine(projectRoot, "Sample.Tests", "CalculatorTests.cs")));
    }

    [Fact]
    public async Task RunAsync_ObservacionesRepetidasEnTareaDeEdicion_NoReportaExitoSinCambios()
    {
        var projectRoot = TempDir();
        Directory.CreateDirectory(projectRoot);
        await File.WriteAllTextAsync(Path.Combine(projectRoot, "index.html"), "<html><body>Anterior</body></html>");
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 8, installedModels: new[] { "qwen2.5-coder:3b" });
        using var http = new HttpClient(new StubOllamaHandler());
        using var session = new LocalModelSession(http);
        await session.EnsureAvailableAsync("qwen2.5-coder:3b", CancellationToken.None);

        var llm = new SequenceLlmClient(
            "{\"action\":\"read_file\",\"path\":\"index.html\"}",
            "{\"action\":\"read_file\",\"path\":\"index.html\"}",
            "{\"action\":\"read_file\",\"path\":\"index.html\"}");
        var service = new AgentService(
            store,
            assessment,
            new AgentLimits { MaxIterations = 1, MaxPlanBIterations = 2 },
            llm,
            new StubLlmDiagnostics(),
            session,
            () => projectRoot,
            CreateModelAutoSetup(store, assessment, "qwen2.5-coder:3b"));

        var result = await service.RunAsync("Actualiza index.html para que sea adaptable", cancellationToken: CancellationToken.None);

        Assert.False(result.Success, (result.Reason ?? "sin motivo") + " · pasos: " +
            string.Join(" | ", result.Steps.Select(step => step.Action + ":" + step.Success + ":" + step.ResultPreview)));
        Assert.Equal("<html><body>Anterior</body></html>", await File.ReadAllTextAsync(Path.Combine(projectRoot, "index.html")));
        Assert.Equal(3, llm.CallCount); // una iteracion inicial y dos iteraciones acotadas de Plan B
        Assert.All(llm.Requests, request => Assert.True(request.RequireJson));
        Assert.Contains(llm.Prompts.SelectMany(prompt => prompt), prompt =>
            prompt.Contains("Observacion repetida sin informacion nueva", StringComparison.OrdinalIgnoreCase) &&
            prompt.Contains("no repitas esa accion", StringComparison.OrdinalIgnoreCase) &&
            prompt.Contains("ejecuta el siguiente paso pendiente", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("plan_b_fallido", result.Checkpoint?.LastDecision);
        Assert.Equal(2, result.Checkpoint?.PlanBIterationsUsed);
    }

    [Fact]
    public async Task RunAsync_RutaRaizSolicitada_CorrigePrefijoDocsSinModificarOtraRuta()
    {
        var projectRoot = TempDir();
        Directory.CreateDirectory(projectRoot);
        Directory.CreateDirectory(Path.Combine(projectRoot, "docs"));
        var replacement = "<!doctype html><html lang=\"es\"><meta charset=\"UTF-8\"><body><h1>Listo</h1></body></html>";
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 8, installedModels: new[] { "qwen2.5-coder:3b" });
        using var http = new HttpClient(new StubOllamaHandler());
        using var session = new LocalModelSession(http);
        await session.EnsureAvailableAsync("qwen2.5-coder:3b", CancellationToken.None);

        var llm = new SequenceLlmClient(
            "{\"action\":\"create_file\",\"path\":\"docs/index.html\",\"content\":\"" + replacement.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"}");
        var service = new AgentService(
            store,
            assessment,
            new AgentLimits { MaxIterations = 2 },
            llm,
            new StubLlmDiagnostics(),
            session,
            () => projectRoot,
            CreateModelAutoSetup(store, assessment, "qwen2.5-coder:3b"));

        var result = await service.RunAsync(
            "Crea la pagina en la ruta exacta index.html (raiz del proyecto, no docs/index.html).",
            cancellationToken: CancellationToken.None);

        Assert.True(result.Success, result.Reason);
        Assert.Equal(replacement, await File.ReadAllTextAsync(Path.Combine(projectRoot, "index.html")));
        Assert.False(File.Exists(Path.Combine(projectRoot, "docs", "index.html")));
        Assert.Contains(result.Steps, step => step.Success && step.Path == "index.html" &&
            (step.ResultPreview ?? "").Contains("corrigio la ruta", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RunAsync_FaltaEnlaceSolicitado_HarnessReparaConPlanB()
    {
        var projectRoot = TempDir();
        Directory.CreateDirectory(projectRoot);
        var withoutLink = "<!doctype html><html lang=\"es\"><body><h1>Condor</h1></body></html>";
        await File.WriteAllTextAsync(Path.Combine(projectRoot, "index.html"), withoutLink);
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 8, installedModels: new[] { "qwen2.5-coder:3b" });
        using var http = new HttpClient(new StubOllamaHandler());
        using var session = new LocalModelSession(http);
        await session.EnsureAvailableAsync("qwen2.5-coder:3b", CancellationToken.None);

        var llm = new SequenceLlmClient(
            "{\"action\":\"read_file\",\"path\":\"index.html\"}",
            "{\"action\":\"edit_file\",\"path\":\"index.html\",\"content\":\"" + withoutLink.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"}");
        var service = new AgentService(
            store,
            assessment,
            new AgentLimits { MaxIterations = 2, MaxPlanBIterations = 2 },
            llm,
            new StubLlmDiagnostics(),
            session,
            () => projectRoot,
            CreateModelAutoSetup(store, assessment, "qwen2.5-coder:3b"));

        var result = await service.RunAsync(
            "Actualiza la pagina existente en la ruta exacta index.html (raiz del proyecto, no docs/index.html). Conserva el enlace relativo exacto docs/indice-condor.html.",
            cancellationToken: CancellationToken.None);

        var updated = await File.ReadAllTextAsync(Path.Combine(projectRoot, "index.html"));
        Assert.True(result.Success, result.Reason);
        Assert.Contains("<a href=\"docs/indice-condor.html\">", updated);
        Assert.Equal("plan_b_corregido", result.Checkpoint?.LastDecision);
        Assert.Equal(2, llm.CallCount);
    }

    [Fact]
    public async Task RunAsync_EnlaceExplicitoFaltante_HarnessReparaConPlanBDeterminista()
    {
        var projectRoot = TempDir();
        Directory.CreateDirectory(projectRoot);
        var withoutAnchor = "<!doctype html><html lang=\"es\"><head><link rel=\"stylesheet\" href=\"docs/indice-condor.html\"></head><body><h1>Condor</h1></body></html>";
        await File.WriteAllTextAsync(Path.Combine(projectRoot, "index.html"), withoutAnchor);
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 8, installedModels: new[] { "qwen2.5-coder:3b" });
        using var http = new HttpClient(new StubOllamaHandler());
        using var session = new LocalModelSession(http);
        await session.EnsureAvailableAsync("qwen2.5-coder:3b", CancellationToken.None);

        var llm = new SequenceLlmClient("{\"action\":\"invalid\"}");
        var service = new AgentService(
            store,
            assessment,
            new AgentLimits { MaxIterations = 1, MaxPlanBIterations = 2 },
            llm,
            new StubLlmDiagnostics(),
            session,
            () => projectRoot,
            CreateModelAutoSetup(store, assessment, "qwen2.5-coder:3b"));

        var result = await service.RunAsync(
            "Actualiza index.html. Conserva el enlace relativo exacto docs/indice-condor.html con esta etiqueta: <a href=\"docs/indice-condor.html\">Documentacion generada</a>.",
            cancellationToken: CancellationToken.None);

        var updated = await File.ReadAllTextAsync(Path.Combine(projectRoot, "index.html"));
        Assert.True(result.Success, result.Reason + "; decision=" + result.Checkpoint?.LastDecision +
            "; risk=" + result.Checkpoint?.RiskLevel + "; scope=" + string.Join(",", result.Checkpoint?.DeclaredScope ?? new List<string>()) +
            "; planB=" + result.Checkpoint?.PlanBIterationsUsed +
            "; steps=" + string.Join(" | ", result.Steps.Select(step => step.Action + ":" + step.Success + ":" + step.ResultPreview)));
        Assert.Contains("<a href=\"docs/indice-condor.html\">Documentacion generada</a>", updated);
        Assert.DoesNotContain("<link rel=\"stylesheet\"", updated, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("plan_b_corregido", result.Checkpoint?.LastDecision);
        Assert.Equal(1, llm.CallCount);
        Assert.Contains(result.Steps, step =>
            step.Action == AgentAction.ActionEditFile &&
            step.Success &&
            step.ResultPreview?.Contains("Plan B insertó el enlace HTML solicitado", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task RunAsync_AccionCompuestaPatchEdit_CondorLaNormalizaConservandoElProyecto()
    {
        var projectRoot = TempDir();
        Directory.CreateDirectory(projectRoot);
        var original = "<html><body>Anterior</body></html>";
        await File.WriteAllTextAsync(Path.Combine(projectRoot, "index.html"), original);
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 8, installedModels: new[] { "qwen2.5-coder:3b" });
        using var http = new HttpClient(new StubOllamaHandler());
        using var session = new LocalModelSession(http);
        await session.EnsureAvailableAsync("qwen2.5-coder:3b", CancellationToken.None);

        var updated = "<!doctype html><html lang=\"es\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><body>Actualizado</body></html>";
        var llm = new SequenceLlmClient(
            "{\"action\":\"read_file\",\"path\":\"missing.html\"}",
            "{\"action\":\"read_file\",\"path\":\"index.html\"}",
            "{\"action\":\"patch/edit_file\",\"path\":\"index.html\",\"content\":" +
            System.Text.Json.JsonSerializer.Serialize(updated) + "}");
        var service = new AgentService(
            store,
            assessment,
            new AgentLimits { MaxIterations = 1, MaxPlanBIterations = 2 },
            llm,
            new StubLlmDiagnostics(),
            session,
            () => projectRoot,
            CreateModelAutoSetup(store, assessment, "qwen2.5-coder:3b"));

        var result = await service.RunAsync("Actualiza index.html", cancellationToken: CancellationToken.None);

        Assert.True(result.Success, result.Reason);
        Assert.Equal(updated, await File.ReadAllTextAsync(Path.Combine(projectRoot, "index.html")));
        Assert.Contains(result.Steps, step =>
            step.Action == AgentAction.ActionEditFile && step.Success &&
            step.ResultPreview!.Contains("interpreto la accion compuesta 'patch/edit_file'", StringComparison.Ordinal));
        Assert.Equal(3, llm.CallCount);
    }

    [Fact]
    public async Task RunAsync_PatchIncorrectoConHtmlCompleto_PlanBUsaEditFileSeguro()
    {
        var projectRoot = TempDir();
        Directory.CreateDirectory(projectRoot);
        await File.WriteAllTextAsync(Path.Combine(projectRoot, "index.html"), "<html><body>Anterior</body></html>");
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 8, installedModels: new[] { "qwen2.5-coder:3b" });
        using var http = new HttpClient(new StubOllamaHandler());
        using var session = new LocalModelSession(http);
        await session.EnsureAvailableAsync("qwen2.5-coder:3b", CancellationToken.None);

        var updated = "<!doctype html><html lang=\"es\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><body>Adaptable</body></html>";
        var llm = new SequenceLlmClient(
            "{\"action\":\"read_file\",\"path\":\"missing.html\"}",
            "{\"action\":\"read_file\",\"path\":\"index.html\"}",
            "{\"action\":\"patch\",\"path\":\"index.html\",\"original\":\"texto que no existe\",\"replacement\":" +
            System.Text.Json.JsonSerializer.Serialize(updated) + "}");
        var service = new AgentService(
            store,
            assessment,
            new AgentLimits { MaxIterations = 1, MaxPlanBIterations = 2 },
            llm,
            new StubLlmDiagnostics(),
            session,
            () => projectRoot,
            CreateModelAutoSetup(store, assessment, "qwen2.5-coder:3b"));

        var result = await service.RunAsync("Actualiza index.html para que sea adaptable", cancellationToken: CancellationToken.None);

        Assert.True(result.Success, result.Reason);
        Assert.Equal(updated, await File.ReadAllTextAsync(Path.Combine(projectRoot, "index.html")));
        Assert.Contains(result.Steps, step => step.Action == AgentAction.ActionPatch && !step.Success);
        Assert.Contains(result.Steps, step =>
            step.Action == AgentAction.ActionEditFile && step.Success &&
            step.ResultPreview!.Contains("replacement contenia un documento HTML completo", StringComparison.Ordinal));
        Assert.Equal(2, result.Checkpoint?.PlanBIterationsUsed);
    }

    [Fact]
    public async Task RunAsync_RespuestaMalformadaEnActualizacion_PermiteCorreccionEstructurada()
    {
        var projectRoot = TempDir();
        Directory.CreateDirectory(projectRoot);
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 8, installedModels: new[] { "qwen2.5-coder:3b" });
        using var http = new HttpClient(new StubOllamaHandler());
        using var session = new LocalModelSession(http);
        await session.EnsureAvailableAsync("qwen2.5-coder:3b", CancellationToken.None);

        var llm = new SequenceLlmClient(
            "respuesta que no es JSON",
            "{\"action\":\"create_file\",\"path\":\"index.html\",\"content\":\"<!doctype html><html lang=\\\"es\\\"><body>Adaptable</body></html>\"}");
        var service = new AgentService(
            store,
            assessment,
            new AgentLimits { MaxIterations = 1, MaxPlanBIterations = 1 },
            llm,
            new StubLlmDiagnostics(),
            session,
            () => projectRoot,
            CreateModelAutoSetup(store, assessment, "qwen2.5-coder:3b"));

        var result = await service.RunAsync(
            "Actualiza el contenido existente de index.html para que sea una pagina web adaptable.",
            cancellationToken: CancellationToken.None);

        Assert.True(result.Success, result.Reason);
        Assert.True(File.Exists(Path.Combine(projectRoot, "index.html")));
        Assert.Equal(2, llm.CallCount);
        Assert.Contains(llm.Prompts[1], message =>
            message.Contains("La respuesta anterior no produjo una acción JSON válida", StringComparison.Ordinal));
        Assert.Contains(result.Steps, step => step.Action == AgentAction.ActionCreateFile && step.Success);
    }

    [Fact]
    public async Task RunAsync_FalloTrasPlanB_DetieneSinConsumirElLimiteCompleto()
    {
        var projectRoot = TempDir();
        Directory.CreateDirectory(projectRoot);
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 8, installedModels: new[] { "qwen2.5-coder:3b" });
        using var http = new HttpClient(new StubOllamaHandler());
        using var session = new LocalModelSession(http);
        await session.EnsureAvailableAsync("qwen2.5-coder:3b", CancellationToken.None);

        var llm = new SequenceLlmClient(
            "{\"action\":\"read_file\",\"path\":\"missing.html\"}",
            "{\"action\":\"read_file\",\"path\":\"missing.html\"}");
        var service = new AgentService(
            store,
            assessment,
            new AgentLimits { MaxIterations = 8, MaxPlanBIterations = 1 },
            llm,
            new StubLlmDiagnostics(),
            session,
            () => projectRoot,
            CreateModelAutoSetup(store, assessment, "qwen2.5-coder:3b"));

        var result = await service.RunAsync("Crea una pagina web sencilla en index.html", cancellationToken: CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(2, llm.CallCount);
        Assert.Contains("Plan B no pudo corregir", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("plan_b_fallido", result.Checkpoint?.LastDecision);
    }

    [Fact]
    public async Task RunAsync_ObservacionRepetida_PlanBCreaPaginaRaizConEnlaceALaDocumentacion()
    {
        var projectRoot = TempDir();
        var docsRoot = Path.Combine(projectRoot, "docs");
        Directory.CreateDirectory(docsRoot);
        await File.WriteAllTextAsync(
            Path.Combine(docsRoot, "indice-condor.html"),
            "<!doctype html><html lang=\"es\"><body><h1>Documentación</h1></body></html>");

        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 8, installedModels: new[] { "qwen2.5-coder:3b" });
        using var http = new HttpClient(new StubOllamaHandler());
        using var session = new LocalModelSession(http);
        await session.EnsureAvailableAsync("qwen2.5-coder:3b", CancellationToken.None);

        var llm = new SequenceLlmClient(
            "{\"action\":\"list_dir\",\"path\":\".\"}",
            "{\"action\":\"list_dir\",\"path\":\".\"}");
        var service = new AgentService(
            store,
            assessment,
            new AgentLimits { MaxIterations = 8, MaxPlanBIterations = 1 },
            llm,
            new StubLlmDiagnostics(),
            session,
            () => projectRoot,
            CreateModelAutoSetup(store, assessment, "qwen2.5-coder:3b"));

        var result = await service.RunAsync(
            "Crea una página web sencilla en index.html en la raíz de este proyecto. " +
            "Debe mostrar enlaces relativos a los documentos generados. Guarda la documentación directamente en docs/.",
            cancellationToken: CancellationToken.None);

        var rootHtml = Path.Combine(projectRoot, "index.html");
        Assert.True(result.Success, result.Reason);
        Assert.True(File.Exists(rootHtml));
        Assert.Contains("<a href=\"docs/indice-condor.html\">Documentación generada</a>",
            System.Net.WebUtility.HtmlDecode(await File.ReadAllTextAsync(rootHtml)));
        Assert.DoesNotContain(result.Steps, step => step.Path == "docs/index.html");
        Assert.Contains(result.Steps, step =>
            step.Action == AgentAction.ActionCreateFile && step.Success && step.Path == "index.html");
        Assert.Equal(2, llm.CallCount);
        Assert.Equal("plan_b_corregido", result.Checkpoint?.LastDecision);
    }

    [Fact]
    public async Task RunAsync_TerceraRondaDeRevision_ExigeReplanificarYGuardaElLimite()
    {
        var projectRoot = TempDir();
        Directory.CreateDirectory(projectRoot);
        await File.WriteAllTextAsync(Path.Combine(projectRoot, "README.md"), "Contenido inicial\n");
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        await store.SaveAgentCheckpointAsync(projectRoot, new AgentCheckpoint
        {
            Task = "Implementa un cambio en README.md",
            WorkingDirectory = projectRoot,
            NextAction = "revisar",
            ReviewRoundsUsed = 3,
            DeclaredScope = new List<string> { "README.md" },
            PlanSteps = new List<string> { "Revisar README.md" },
            VerificationSteps = new List<string> { "Leer el archivo" }
        });

        var assessment = new StubAssessmentService(16, 8, new[] { "qwen2.5-coder:3b" });
        using var http = new HttpClient(new StubOllamaHandler());
        using var session = new LocalModelSession(http);
        await session.EnsureAvailableAsync("qwen2.5-coder:3b", CancellationToken.None);
        var llm = new SequenceLlmClient(
            "{\"action\":\"delegate\",\"subtasks\":[{\"id\":\"review-4\",\"task\":\"Revisa README.md\",\"profileId\":\"reviewer\",\"maxDurationSeconds\":60,\"maxIterations\":1,\"maxOutputTokens\":128}]}");
        var delegation = new RecordingDelegationService();
        var service = new AgentService(
            store,
            assessment,
            new AgentLimits { MaxIterations = 1 },
            llm,
            new StubLlmDiagnostics(),
            session,
            () => projectRoot,
            CreateModelAutoSetup(store, assessment, "qwen2.5-coder:3b"),
            delegationService: delegation);

        var result = await service.RunAsync("continuar", cancellationToken: CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(0, delegation.ExecuteCount);
        Assert.True(result.Checkpoint?.ReviewReplanRequired);
        Assert.Equal(3, result.Checkpoint?.ReviewRoundsUsed);
        Assert.Contains(result.Steps, step => step.Action == AgentAction.ActionDelegate &&
            step.ResultPreview?.Contains("plan", StringComparison.OrdinalIgnoreCase) == true);
        var saved = await store.LoadAgentCheckpointAsync(projectRoot);
        Assert.True(saved?.ReviewReplanRequired);
        Assert.Equal(3, saved?.ReviewRoundsUsed);
    }

    [Fact]
    public async Task RunAsync_QuintaRondaDeRevision_SeDetieneSinDelegar()
    {
        var projectRoot = TempDir();
        Directory.CreateDirectory(projectRoot);
        await File.WriteAllTextAsync(Path.Combine(projectRoot, "README.md"), "Contenido inicial\n");
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        await store.SaveAgentCheckpointAsync(projectRoot, new AgentCheckpoint
        {
            Task = "Implementa un cambio en README.md",
            WorkingDirectory = projectRoot,
            NextAction = "revisar",
            ReviewRoundsUsed = 4
        });

        var assessment = new StubAssessmentService(16, 8, new[] { "qwen2.5-coder:3b" });
        using var http = new HttpClient(new StubOllamaHandler());
        using var session = new LocalModelSession(http);
        await session.EnsureAvailableAsync("qwen2.5-coder:3b", CancellationToken.None);
        var llm = new SequenceLlmClient(
            "{\"action\":\"delegate\",\"subtasks\":[{\"id\":\"review-5\",\"task\":\"Revisa README.md\",\"profileId\":\"reviewer\",\"maxDurationSeconds\":60,\"maxIterations\":1,\"maxOutputTokens\":128}]}");
        var delegation = new RecordingDelegationService();
        var service = new AgentService(
            store,
            assessment,
            new AgentLimits { MaxIterations = 1 },
            llm,
            new StubLlmDiagnostics(),
            session,
            () => projectRoot,
            CreateModelAutoSetup(store, assessment, "qwen2.5-coder:3b"),
            delegationService: delegation);

        var result = await service.RunAsync("continuar", cancellationToken: CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(0, delegation.ExecuteCount);
        Assert.Contains("cuatro rondas", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.Checkpoint?.ReviewRoundsUsed == 4,
            $"Rondas={result.Checkpoint?.ReviewRoundsUsed}; llamadas={llm.CallCount}; delegado={delegation.LastReviewTask ?? "ninguno"}; " +
            string.Join(" | ", result.Steps.Select(step => $"{step.Action}:{step.Success}:{step.ResultPreview}")));
    }

    [Fact]
    public async Task RunAsync_RevisionMultiagente_NoPermiteCerrarConUnSoloPerfilRevisor()
    {
        var projectRoot = TempDir();
        var stateRoot = TempDir();
        Directory.CreateDirectory(projectRoot);
        await File.WriteAllTextAsync(Path.Combine(projectRoot, "README.md"), "Contenido inicial\n");
        try
        {
            var store = new LocalStateStore(stateRoot);
            await store.SaveAgentCheckpointAsync(projectRoot, new AgentCheckpoint
            {
                Task = "Implementa un cambio en README.md y solicita una revisión multiagente",
                WorkingDirectory = projectRoot,
                NextAction = "revisar",
                UnreviewedModifications = 1
            });

            var assessment = new StubAssessmentService(16, 8, new[] { "qwen2.5-coder:3b" });
            using var http = new HttpClient(new StubOllamaHandler());
            using var session = new LocalModelSession(http);
            await session.EnsureAvailableAsync("qwen2.5-coder:3b", CancellationToken.None);
            var llm = new SequenceLlmClient(
                "{\"action\":\"delegate\",\"subtasks\":[{\"id\":\"review-only\",\"task\":\"Revisa README.md\",\"profileId\":\"reviewer\",\"maxDurationSeconds\":60,\"maxIterations\":1,\"maxOutputTokens\":128}]}");
            var delegation = new RecordingDelegationService();
            var service = new AgentService(
                store,
                assessment,
                new AgentLimits { MaxIterations = 1 },
                llm,
                new StubLlmDiagnostics(),
                session,
                () => projectRoot,
                CreateModelAutoSetup(store, assessment, "qwen2.5-coder:3b"),
                delegationService: delegation);

            var result = await service.RunAsync("continuar", cancellationToken: CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(0, delegation.ExecuteCount);
            Assert.Equal(1, result.Checkpoint?.UnreviewedModifications);
            Assert.Contains(result.Steps, step =>
                step.Action == AgentAction.ActionDelegate &&
                !step.Success &&
                step.ResultPreview!.Contains("reviewer y security-reviewer", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(projectRoot, recursive: true);
            Directory.Delete(stateRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_RevisionDeltaFinal_SoloIncluyeRutasModificadasTrasElNuevoPlan()
    {
        var projectRoot = TempDir();
        Directory.CreateDirectory(projectRoot);
        await File.WriteAllTextAsync(Path.Combine(projectRoot, "README.md"), "Contenido inicial\n");
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        await store.SaveAgentCheckpointAsync(projectRoot, new AgentCheckpoint
        {
            Task = "Implementa un cambio en README.md y solicita una revisión multiagente independiente",
            WorkingDirectory = projectRoot,
            NextAction = "revisar",
            ReviewRoundsUsed = 3,
            ReviewReplanRequired = true
        });

        var assessment = new StubAssessmentService(16, 8, new[] { "qwen2.5-coder:3b" });
        using var http = new HttpClient(new StubOllamaHandler());
        using var session = new LocalModelSession(http);
        await session.EnsureAvailableAsync("qwen2.5-coder:3b", CancellationToken.None);
        var llm = new SequenceLlmClient(
            "{\"action\":\"read_file\",\"path\":\"README.md\"}",
            "{\"action\":\"plan\",\"objective\":\"Actualizar README.md\",\"scopePaths\":[\"README.md\"],\"excludedPaths\":[],\"planItems\":[{\"task\":\"Actualizar el contenido\",\"expectedResult\":\"README.md contiene el texto solicitado.\",\"verificationStep\":\"Leer el contenido actualizado\"}]}",
            "{\"action\":\"edit_file\",\"path\":\"README.md\",\"content\":\"Contenido actualizado\\n\"}",
            "{\"action\":\"read_file\",\"path\":\"README.md\"}",
            "{\"action\":\"delegate\",\"subtasks\":[{\"id\":\"review-delta\",\"task\":\"Comprueba el resultado\",\"profileId\":\"reviewer\",\"maxDurationSeconds\":60,\"maxIterations\":1,\"maxOutputTokens\":128},{\"id\":\"security-delta\",\"task\":\"Comprueba el mismo resultado desde seguridad\",\"profileId\":\"security-reviewer\",\"maxDurationSeconds\":60,\"maxIterations\":1,\"maxOutputTokens\":128}]}",
            "{\"action\":\"git_diff\"}",
            "{\"action\":\"done\",\"reason\":\"El archivo quedó leído, el diff revisado y la revisión multiagente está completa.\"}");
        var delegation = new RecordingDelegationService();
        var service = new AgentService(
            store,
            assessment,
            new AgentLimits { MaxIterations = 10 },
            llm,
            new StubLlmDiagnostics(),
            session,
            () => projectRoot,
            CreateModelAutoSetup(store, assessment, "qwen2.5-coder:3b"),
            delegationService: delegation);

        var result = await service.RunAsync("continuar", cancellationToken: CancellationToken.None);

        Assert.True(result.Success, result.Reason);
        Assert.Equal(4, result.Checkpoint?.ReviewRoundsUsed);
        Assert.False(result.Checkpoint?.ReviewReplanRequired);
        Assert.NotNull(delegation.LastReviewTask);
        Assert.Contains("revisa únicamente los cambios nuevos", delegation.LastReviewTask, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("README.md", delegation.LastReviewTask, StringComparison.Ordinal);
        Assert.Contains("Revisión delta final", delegation.LastReviewTask, StringComparison.Ordinal);
        Assert.Contains("reviewer", delegation.LastReviewProfileIds);
        Assert.Contains("security-reviewer", delegation.LastReviewProfileIds);
    }

    [Fact]
    public async Task RunAsync_ContextoExtenso_CompactaYConservaObjetivoYObservaciones()
    {
        var projectRoot = TempDir();
        Directory.CreateDirectory(projectRoot);
        File.WriteAllText(Path.Combine(projectRoot, "a.txt"), new string('a', 12_000));
        File.WriteAllText(Path.Combine(projectRoot, "b.txt"), new string('b', 12_000));
        File.WriteAllText(Path.Combine(projectRoot, "c.txt"), new string('c', 12_000));

        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(16, 8, new[] { "qwen2.5-coder:3b" });
        using var http = new HttpClient(new StubOllamaHandler());
        using var session = new LocalModelSession(http);
        await session.EnsureAvailableAsync("qwen2.5-coder:3b", CancellationToken.None);

        var llm = new SequenceLlmClient(
            "{\"action\":\"read_file\",\"path\":\"a.txt\"}",
            "{\"action\":\"read_file\",\"path\":\"b.txt\"}",
            "{\"action\":\"read_file\",\"path\":\"c.txt\"}",
            "{\"action\":\"done\",\"reason\":\"Analicé los archivos y conservé la señal AZUL-742.\"}");
        var service = new AgentService(
            store,
            assessment,
            new AgentLimits { MaxIterations = 4 },
            llm,
            new StubLlmDiagnostics(),
            session,
            () => projectRoot,
            CreateModelAutoSetup(store, assessment, "qwen2.5-coder:3b"));

        var result = await service.RunAsync(
            "Analiza estos archivos y conserva la señal AZUL-742 en la síntesis final.",
            cancellationToken: CancellationToken.None);

        Assert.True(result.Success, result.Reason);
        var compactedPrompts = llm.Prompts.Where(prompt =>
            prompt.Any(message => message.Contains("[MEMORIA DE EJECUCION CONDOR]", StringComparison.Ordinal))).ToList();
        Assert.NotEmpty(compactedPrompts);
        var compactedPrompt = compactedPrompts[^1];
        var compactedText = string.Join("\n", compactedPrompt);
        Assert.Contains("AZUL-742", compactedText, StringComparison.Ordinal);
        Assert.Contains("Resultados observados más recientes", compactedText, StringComparison.Ordinal);
        Assert.Contains("a.txt", compactedText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_RutaSensible_ExigeAprobacionYLaInvalidaSiCambiaHead()
    {
        var projectRoot = TempDir();
        Directory.CreateDirectory(projectRoot);
        await RunGitAsync(projectRoot, "init");
        await RunGitAsync(projectRoot, "config", "user.name", "Condor Tests");
        await RunGitAsync(projectRoot, "config", "user.email", "condor-tests@example.invalid");
        await File.WriteAllTextAsync(Path.Combine(projectRoot, "README.md"), "Proyecto de prueba.\n");
        await RunGitAsync(projectRoot, "add", "README.md");
        await RunGitAsync(projectRoot, "commit", "-m", "initial test commit");
        var initialHead = (await RunGitAsync(projectRoot, "rev-parse", "HEAD")).Trim();

        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(16, 8, new[] { "qwen2.5-coder:3b" });
        using var http = new HttpClient(new StubOllamaHandler());
        using var session = new LocalModelSession(http);
        await session.EnsureAvailableAsync("qwen2.5-coder:3b", CancellationToken.None);
        var llm = new SequenceLlmClient(
            "{\"action\":\"list_dir\",\"path\":\".\"}",
            "{\"action\":\"plan\",\"objective\":\"crear configuración local\",\"scopePaths\":[\".\"],\"excludedPaths\":[],\"planSteps\":[\"crear el archivo de configuración\",\"revisar el contenido antes de escribir\"],\"expectedResults\":[\"El archivo .env contiene la configuración local.\",\"El archivo no contiene credenciales reales.\"],\"verificationSteps\":[\"leer el archivo y comprobar su configuración\",\"buscar valores que parezcan credenciales\"],\"riskLevel\":\"high\"}",
            "{\"action\":\"create_file\",\"path\":\".env\",\"content\":\"LOCAL_MODE=1\"}");
        var progress = new CollectingAgentProgressObserver();
        var steering = new CommitChangingApprovalChannel(projectRoot, progress);
        var service = new AgentService(
            store,
            assessment,
            new AgentLimits { MaxIterations = 3 },
            llm,
            new StubLlmDiagnostics(),
            session,
            () => projectRoot,
            CreateModelAutoSetup(store, assessment, "qwen2.5-coder:3b"),
            steeringChannel: steering);

        var result = await service.RunAsync("Crea un archivo de configuración local para este proyecto.", progress);
        var finalHead = (await RunGitAsync(projectRoot, "rev-parse", "HEAD")).Trim();

        Assert.True(initialHead != finalHead,
            $"La prueba no alcanzó el canal de aprobación. Razón={result.Reason}; pasos={string.Join(" | ", result.Steps.Select(step => $"{step.Action}:{step.Success}:{step.ResultPreview}"))}");
        Assert.False(result.Success);
        Assert.Contains("commit cambiaron después de la aprobación", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.Steps, step => step.Action == "human_approval" && step.Success);
        Assert.False(File.Exists(Path.Combine(projectRoot, ".env")));
        Assert.Contains(progress.Updates, update => update.Action == "aprobación humana" &&
            (update.Message ?? "").Contains(initialHead[..12], StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RunAsync_DocumentacionDeBajoRiesgo_UsaPlanMinimoYNoPideAprobacion()
    {
        var projectRoot = TempDir();
        var stateRoot = TempDir();
        Directory.CreateDirectory(projectRoot);
        Directory.CreateDirectory(stateRoot);
        await File.WriteAllTextAsync(Path.Combine(projectRoot, "README.md"), "# Proyecto\n\nTexto anterior.\n");

        try
        {
            var store = new LocalStateStore(stateRoot);
            var assessment = new StubAssessmentService(16, 8, new[] { "qwen2.5-coder:3b" });
            using var http = new HttpClient(new StubOllamaHandler());
            using var session = new LocalModelSession(http);
            await session.EnsureAvailableAsync("qwen2.5-coder:3b", CancellationToken.None);
            var llm = new SequenceLlmClient(
                "{\"action\":\"list_dir\",\"path\":\".\"}",
                "{\"action\":\"read_file\",\"path\":\"README.md\"}",
                "{\"action\":\"plan\",\"objective\":\"actualizar documentación\",\"scopePaths\":[\"README.md\"],\"excludedPaths\":[],\"planSteps\":[\"actualizar el texto de README.md\"],\"expectedResults\":[\"README.md muestra el texto solicitado.\"],\"verificationSteps\":[\"leer README.md y comprobar el texto actualizado\"],\"riskLevel\":\"low\"}",
                "{\"action\":\"edit_file\",\"path\":\"README.md\",\"content\":\"# Proyecto\\n\\nTexto actualizado.\\n\"}",
                "{\"action\":\"read_file\",\"path\":\"README.md\"}",
                "{\"action\":\"git_diff\"}",
                "{\"action\":\"done\",\"reason\":\"README.md fue actualizado y releído para comprobar el contenido.\"}");
            var service = new AgentService(
                store,
                assessment,
                new AgentLimits { MaxIterations = 8 },
                llm,
                new StubLlmDiagnostics(),
                session,
                () => projectRoot,
                CreateModelAutoSetup(store, assessment, "qwen2.5-coder:3b"));

            var result = await service.RunAsync("actualiza la documentación README.md");

            Assert.True(result.Success, result.Reason);
            Assert.Contains(result.Steps, step => step.Action == "plan" && step.Success);
            Assert.Contains(result.Steps, step => step.Action == "edit_file" && step.Success);
            Assert.DoesNotContain(result.Steps, step => step.Action == "human_approval");
            Assert.Equal("# Proyecto\n\nTexto actualizado.\n", await File.ReadAllTextAsync(Path.Combine(projectRoot, "README.md")));
        }
        finally
        {
            Directory.Delete(projectRoot, recursive: true);
            Directory.Delete(stateRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_ConsultaSobreCodigo_BloqueaModificacionesYConservaArchivos()
    {
        var projectRoot = TempDir();
        var stateRoot = TempDir();
        Directory.CreateDirectory(projectRoot);
        Directory.CreateDirectory(stateRoot);
        var sourcePath = Path.Combine(projectRoot, "Calculator.cs");
        const string originalContent = "public static int Sum(int a, int b) => a + b;\n";
        await File.WriteAllTextAsync(sourcePath, originalContent);

        try
        {
            var store = new LocalStateStore(stateRoot);
            var assessment = new StubAssessmentService(16, 8, new[] { "qwen2.5-coder:3b" });
            using var http = new HttpClient(new StubOllamaHandler());
            using var session = new LocalModelSession(http);
            await session.EnsureAvailableAsync("qwen2.5-coder:3b", CancellationToken.None);
            var llm = new SequenceLlmClient(
                "{\"action\":\"list_dir\",\"path\":\".\"}",
                "{\"action\":\"read_file\",\"path\":\"Calculator.cs\"}",
                "{\"action\":\"edit_file\",\"path\":\"Calculator.cs\",\"content\":\"public static int Sum(int a, int b) => a - b;\\n\"}",
                "{\"action\":\"done\",\"reason\":\"Sum retorna la suma de los dos parámetros.\"}");
            var service = new AgentService(
                store,
                assessment,
                new AgentLimits { MaxIterations = 6 },
                llm,
                new StubLlmDiagnostics(),
                session,
                () => projectRoot,
                CreateModelAutoSetup(store, assessment, "qwen2.5-coder:3b"));

            var result = await service.RunAsync("¿Qué hace esta clase?");

            Assert.True(result.Success, result.Reason);
            var blockedWrite = Assert.Single(result.Steps, step => step.Action == "edit_file");
            Assert.False(blockedWrite.Success);
            Assert.Contains("consulta informativa", blockedWrite.ResultPreview, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(originalContent, await File.ReadAllTextAsync(sourcePath));
        }
        finally
        {
            Directory.Delete(projectRoot, recursive: true);
            Directory.Delete(stateRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_AlcanceDeclarado_BloqueaEdicionExcluidaYConservaDiff()
    {
        var projectRoot = TempDir();
        var stateRoot = TempDir();
        Directory.CreateDirectory(projectRoot);
        Directory.CreateDirectory(stateRoot);
        var sourcePath = Path.Combine(projectRoot, "Calculator.cs");
        var contractPath = Path.Combine(projectRoot, "IArithmetic.cs");
        var documentationPath = Path.Combine(projectRoot, "README.md");
        const string originalSource = "public static int Sum(int a, int b) => a + b;\n";
        const string originalContract = "public interface IArithmetic { int Sum(int a, int b); }\n";
        const string originalDocumentation = "# Proyecto\n\nSin cambios.\n";
        await File.WriteAllTextAsync(sourcePath, originalSource);
        await File.WriteAllTextAsync(contractPath, originalContract);
        await File.WriteAllTextAsync(documentationPath, originalDocumentation);

        try
        {
            var store = new LocalStateStore(stateRoot);
            var assessment = new StubAssessmentService(16, 8, new[] { "qwen2.5-coder:3b" });
            using var http = new HttpClient(new StubOllamaHandler());
            using var session = new LocalModelSession(http);
            await session.EnsureAvailableAsync("qwen2.5-coder:3b", CancellationToken.None);
            var llm = new SequenceLlmClient(
                "{\"action\":\"list_dir\",\"path\":\".\"}",
                "{\"action\":\"read_file\",\"path\":\"Calculator.cs\"}",
                "{\"action\":\"read_file\",\"path\":\"IArithmetic.cs\"}",
                "{\"action\":\"read_file\",\"path\":\"README.md\"}",
                "{\"action\":\"plan\",\"objective\":\"corregir la suma\",\"scopePaths\":[\"Calculator.cs\",\"IArithmetic.cs\"],\"excludedPaths\":[\"README.md\"],\"planSteps\":[\"revisar el contrato IArithmetic\",\"corregir la implementación de Sum en Calculator.cs\"],\"expectedResults\":[\"El contrato de suma queda comprendido.\"],\"verificationSteps\":[\"leer la firma de IArithmetic.Sum\",\"comprobar el resultado de Sum en Calculator.cs\"],\"riskLevel\":\"medium\"}",
                "{\"action\":\"plan\",\"objective\":\"corregir la suma\",\"scopePaths\":[\"Calculator.cs\",\"IArithmetic.cs\"],\"excludedPaths\":[\"README.md\"],\"planSteps\":[\"revisar el contrato IArithmetic y corregir la implementación de Sum en Calculator.cs\"],\"expectedResults\":[\"Calculator.Sum cumple el contrato IArithmetic y suma los valores recibidos.\"],\"verificationSteps\":[\"releer Calculator.cs y comprobar la suma respecto a IArithmetic\"],\"riskLevel\":\"medium\"}",
                "{\"action\":\"edit_file\",\"path\":\"README.md\",\"content\":\"# Alterado fuera de alcance\\n\"}",
                "{\"action\":\"edit_file\",\"path\":\"Calculator.cs\",\"content\":\"public static int Sum(int a, int b) => a + b + 1;\\n\"}",
                "{\"action\":\"git_diff\"}",
                "{\"action\":\"done\",\"reason\":\"La suma fue corregida y la documentación quedó fuera del alcance.\"}");
            var service = new AgentService(
                store,
                assessment,
                new AgentLimits { MaxIterations = 9 },
                llm,
                new StubLlmDiagnostics(),
                session,
                () => projectRoot,
                CreateModelAutoSetup(store, assessment, "qwen2.5-coder:3b"));

            var result = await service.RunAsync("corrige la suma en Calculator.cs");

            Assert.True(result.Success, result.Reason);
            var rejectedPlan = Assert.Single(result.Steps, step => step.Action == "plan" && !step.Success);
            Assert.Contains("resultado esperado", rejectedPlan.ResultPreview, StringComparison.OrdinalIgnoreCase);
            var plan = Assert.Single(result.Steps, step => step.Action == "plan" && step.Success);
            Assert.Contains("IArithmetic.cs", plan.ResultPreview, StringComparison.Ordinal);
            Assert.Contains("Calculator.cs", plan.ResultPreview, StringComparison.Ordinal);
            Assert.Contains("resultado esperado: Calculator.Sum cumple el contrato IArithmetic", plan.ResultPreview, StringComparison.Ordinal);
            var planIndex = result.Steps.IndexOf(plan);
            Assert.True(planIndex >= 0, "Condor debe declarar el alcance después de explorar los archivos relacionados.");
            foreach (var relatedPath in new[] { "Calculator.cs", "IArithmetic.cs" })
            {
                var readStep = Assert.Single(result.Steps, step => step.Action == "read_file" && step.Success && step.Path == relatedPath);
                Assert.True(result.Steps.IndexOf(readStep) < planIndex, relatedPath + " debe leerse antes de registrar el plan.");
            }
            var allowedWrite = Assert.Single(result.Steps, step => step.Action == "edit_file" && step.Success);
            Assert.Equal("Calculator.cs", allowedWrite.Path);
            Assert.True(result.Steps.IndexOf(plan) < result.Steps.IndexOf(allowedWrite), "Condor debe registrar el objetivo, alcance y verificación antes de editar.");
            Assert.Contains("objetivo: corregir la suma", plan.ResultPreview, StringComparison.Ordinal);
            Assert.Contains("comprobar: releer Calculator.cs", plan.ResultPreview, StringComparison.Ordinal);
            var blockedWrite = Assert.Single(result.Steps, step => step.Action == "edit_file" && !step.Success);
            Assert.Equal("README.md", blockedWrite.Path);
            Assert.Contains("fuera del alcance", blockedWrite.ResultPreview, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("public static int Sum(int a, int b) => a + b + 1;\n", await File.ReadAllTextAsync(sourcePath));
            Assert.Equal(originalDocumentation, await File.ReadAllTextAsync(documentationPath));
        }
        finally
        {
            Directory.Delete(projectRoot, recursive: true);
            Directory.Delete(stateRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_ContinuidadEntreSesiones_RetomaPlanYCompletaLaTarea()
    {
        var projectRoot = TempDir();
        var stateRoot = TempDir();
        Directory.CreateDirectory(projectRoot);
        Directory.CreateDirectory(stateRoot);
        var filePath = Path.Combine(projectRoot, "input.txt");
        await File.WriteAllTextAsync(filePath, "base\n");

        try
        {
            var firstStore = new LocalStateStore(stateRoot);
            var assessment = new StubAssessmentService(16, 8, new[] { "qwen2.5-coder:3b" });
            using var firstHttp = new HttpClient(new StubOllamaHandler());
            using var firstSession = new LocalModelSession(firstHttp);
            await firstSession.EnsureAvailableAsync("qwen2.5-coder:3b", CancellationToken.None);
            var firstLlm = new SequenceLlmClient(
                "{\"action\":\"list_dir\",\"path\":\".\"}",
                "{\"action\":\"read_file\",\"path\":\"input.txt\"}",
                "{\"action\":\"plan\",\"objective\":\"actualizar input.txt con AZUL-742 actualizado\",\"scopePaths\":[\"input.txt\"],\"excludedPaths\":[\"README.md\"],\"planSteps\":[\"actualizar input.txt para incluir AZUL-742 actualizado\"],\"expectedResults\":[\"input.txt contiene AZUL-742 actualizado\"],\"verificationSteps\":[\"leer input.txt y confirmar la frase exacta\"],\"riskLevel\":\"medium\"}");
            var firstService = new AgentService(
                firstStore,
                assessment,
                new AgentLimits { MaxIterations = 3 },
                firstLlm,
                new StubLlmDiagnostics(),
                firstSession,
                () => projectRoot,
                CreateModelAutoSetup(firstStore, assessment, "qwen2.5-coder:3b"));

            var interrupted = await firstService.RunAsync(
                "Actualiza input.txt para que contenga AZUL-742 actualizado.",
                cancellationToken: CancellationToken.None);

            Assert.False(interrupted.Success);
            Assert.NotNull(interrupted.Checkpoint);
            Assert.Contains("actualizar input.txt para incluir AZUL-742 actualizado", interrupted.Checkpoint!.PlanSteps!);
            var persisted = await new LocalStateStore(stateRoot).LoadAgentCheckpointAsync(projectRoot);
            Assert.NotNull(persisted);
            Assert.Contains(persisted!.CompletedSteps, step => step.Contains("read_file input.txt", StringComparison.Ordinal));
            Assert.Equal("input.txt", Assert.Single(persisted.DeclaredScope!));

            var resumedStore = new LocalStateStore(stateRoot);
            using var resumedHttp = new HttpClient(new StubOllamaHandler());
            using var resumedSession = new LocalModelSession(resumedHttp);
            await resumedSession.EnsureAvailableAsync("qwen2.5-coder:3b", CancellationToken.None);
            var resumedLlm = new SequenceLlmClient(
                "{\"action\":\"list_dir\",\"path\":\".\"}",
                "{\"action\":\"read_file\",\"path\":\"input.txt\"}",
                "{\"action\":\"edit_file\",\"path\":\"input.txt\",\"content\":\"AZUL-742 actualizado\\n\"}",
                "{\"action\":\"read_file\",\"path\":\"input.txt\"}",
                "{\"action\":\"git_diff\"}",
                "{\"action\":\"done\",\"reason\":\"input.txt contiene AZUL-742 actualizado y la lectura confirma el resultado.\"}");
            var resumedService = new AgentService(
                resumedStore,
                assessment,
                new AgentLimits { MaxIterations = 7 },
                resumedLlm,
                new StubLlmDiagnostics(),
                resumedSession,
                () => projectRoot,
                CreateModelAutoSetup(resumedStore, assessment, "qwen2.5-coder:3b"));

            var resumed = await resumedService.RunAsync("continúa", cancellationToken: CancellationToken.None);

            Assert.True(resumed.Success, resumed.Reason);
            Assert.Equal("AZUL-742 actualizado\n", await File.ReadAllTextAsync(filePath));
            var resumedPrompt = string.Join("\n", resumedLlm.Prompts[0]);
            Assert.Contains("Tarea original: Actualiza input.txt para que contenga AZUL-742 actualizado.", resumedPrompt, StringComparison.Ordinal);
            Assert.Contains("Alcance: input.txt", resumedPrompt, StringComparison.Ordinal);
            Assert.Contains("resultado esperado: input.txt contiene AZUL-742 actualizado", resumedPrompt, StringComparison.Ordinal);
            Assert.Contains("comprobar: leer input.txt y confirmar la frase exacta", resumedPrompt, StringComparison.Ordinal);
            Assert.Contains("Acciones completadas:", resumedPrompt, StringComparison.Ordinal);
            Assert.NotNull(resumed.Checkpoint);
            Assert.Contains(resumed.Checkpoint!.CompletedSteps, step => step.Contains("read_file input.txt", StringComparison.Ordinal));
            Assert.Equal("Actualiza input.txt para que contenga AZUL-742 actualizado.", resumed.Checkpoint.Task);
            Assert.Null(await resumedStore.LoadAgentCheckpointAsync(projectRoot));
        }
        finally
        {
            Directory.Delete(projectRoot, recursive: true);
            Directory.Delete(stateRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_RequisitoDuranteEjecucion_ReplanificaAlcanceYPruebasAntesDeEscribir()
    {
        var projectRoot = TempDir();
        var stateRoot = TempDir();
        Directory.CreateDirectory(projectRoot);
        Directory.CreateDirectory(stateRoot);
        var calculatorPath = Path.Combine(projectRoot, "Calculator.cs");
        var testsPath = Path.Combine(projectRoot, "CalculatorTests.cs");
        var readmePath = Path.Combine(projectRoot, "README.md");
        await File.WriteAllTextAsync(calculatorPath, "public static class Calculator { public static int Sum(int a, int b) => a + b; }\n");
        await File.WriteAllTextAsync(testsPath, "public static class CalculatorTests { public static bool SumCase() => Calculator.Sum(1, 2) == 3; }\n");
        await File.WriteAllTextAsync(readmePath, "# Calculator\nSum adds two numbers.\n");

        try
        {
            var store = new LocalStateStore(stateRoot);
            var assessment = new StubAssessmentService(16, 8, new[] { "qwen2.5-coder:3b" });
            using var http = new HttpClient(new StubOllamaHandler());
            using var session = new LocalModelSession(http);
            await session.EnsureAvailableAsync("qwen2.5-coder:3b", CancellationToken.None);
            var steering = new AgentSteeringChannel();
            var llm = new SequenceLlmClient(
                call =>
                {
                    if (call == 4)
                        steering.Submit("Agrega una prueba en CalculatorTests.cs que verifique Sum(1, 2) == -1 y actualiza README.md para explicar que Sum ahora resta.");
                },
                "{\"action\":\"list_dir\",\"path\":\".\"}",
                "{\"action\":\"read_file\",\"path\":\"Calculator.cs\"}",
                "{\"action\":\"plan\",\"objective\":\"cambiar Sum para restar\",\"scopePaths\":[\"Calculator.cs\"],\"excludedPaths\":[\"CalculatorTests.cs\",\"README.md\"],\"planSteps\":[\"cambiar Sum para restar\"],\"expectedResults\":[\"Sum devuelve la resta de sus argumentos\"],\"verificationSteps\":[\"leer Calculator.cs y comprobar la resta\"],\"riskLevel\":\"medium\"}",
                "{\"action\":\"edit_file\",\"path\":\"Calculator.cs\",\"content\":\"public static class Calculator { public static int Sum(int a, int b) => a - b; }\\n\"}",
                "{\"action\":\"list_dir\",\"path\":\".\"}",
                "{\"action\":\"read_file\",\"path\":\"Calculator.cs\"}",
                "{\"action\":\"read_file\",\"path\":\"CalculatorTests.cs\"}",
                "{\"action\":\"read_file\",\"path\":\"README.md\"}",
                "{\"action\":\"plan\",\"objective\":\"cambiar Sum a resta, actualizar su prueba y documentarlo\",\"scopePaths\":[\"Calculator.cs\",\"CalculatorTests.cs\",\"README.md\"],\"excludedPaths\":[],\"planSteps\":[\"cambiar Sum para restar\",\"actualizar la prueba de Sum(1, 2)\",\"documentar que Sum ahora resta\"],\"expectedResults\":[\"Sum devuelve la resta de sus argumentos\",\"CalculatorTests confirma que Sum(1, 2) devuelve -1\",\"README explica que Sum resta\"],\"verificationSteps\":[\"leer Calculator.cs y comprobar la resta\",\"leer CalculatorTests.cs y comprobar el caso de regresión\",\"leer README.md y comprobar la descripción\"],\"riskLevel\":\"medium\"}",
                "{\"action\":\"edit_file\",\"path\":\"Calculator.cs\",\"content\":\"public static class Calculator { public static int Sum(int a, int b) => a - b; }\\n\"}",
                "{\"action\":\"edit_file\",\"path\":\"CalculatorTests.cs\",\"content\":\"public static class CalculatorTests { public static bool SumCase() => Calculator.Sum(1, 2) == -1; }\\n\"}",
                "{\"action\":\"edit_file\",\"path\":\"README.md\",\"content\":\"# Calculator\\nSum subtracts the second number from the first.\\n\"}",
                "{\"action\":\"read_file\",\"path\":\"Calculator.cs\"}",
                "{\"action\":\"read_file\",\"path\":\"CalculatorTests.cs\"}",
                "{\"action\":\"read_file\",\"path\":\"README.md\"}",
                "{\"action\":\"git_diff\"}",
                "{\"action\":\"done\",\"reason\":\"Sum resta; la prueba confirma el caso 1, 2 y README explica el cambio.\"}");
            var service = new AgentService(
                store,
                assessment,
                new AgentLimits { MaxIterations = 18 },
                llm,
                new StubLlmDiagnostics(),
                session,
                () => projectRoot,
                CreateModelAutoSetup(store, assessment, "qwen2.5-coder:3b"),
                steeringChannel: steering);

            var result = await service.RunAsync("Cambia Calculator.Sum para que reste los dos argumentos.");

            Assert.True(result.Success, result.Reason + " | llamadas=" + llm.CallCount + " | pasos=" +
                string.Join("; ", result.Steps.Select(step => step.Action + " " + step.Path + " " + step.Success + " " + step.ResultPreview)));
            var plans = result.Steps.Where(step => step.Action == AgentAction.ActionPlan && step.Success).ToList();
            Assert.Equal(2, plans.Count);
            var oldPlanIndex = result.Steps.IndexOf(plans[0]);
            var updatedPlanIndex = result.Steps.IndexOf(plans[1]);
            Assert.Contains("CalculatorTests.cs", plans[1].ResultPreview, StringComparison.Ordinal);
            Assert.Contains("README.md", plans[1].ResultPreview, StringComparison.Ordinal);
            Assert.Contains("resultado esperado: CalculatorTests confirma que Sum(1, 2) devuelve -1", plans[1].ResultPreview, StringComparison.Ordinal);
            foreach (var path in new[] { "Calculator.cs", "CalculatorTests.cs", "README.md" })
            {
                var read = Assert.Single(result.Steps, step => step.Action == "read_file" && step.Success && step.Path == path &&
                    step.Iteration > plans[0].Iteration && step.Iteration < plans[1].Iteration);
                Assert.True(result.Steps.IndexOf(read) < updatedPlanIndex, path + " debe leerse antes del plan actualizado.");
            }
            var edits = result.Steps.Where(step => step.Action == "edit_file" && step.Success).ToList();
            Assert.True(edits.Count == 3, "resultado=" + result.Reason + "; decisión=" + result.Checkpoint?.LastDecision +
                "; siguiente=" + result.Checkpoint?.NextAction + "; llamadas=" + llm.CallCount + "; pasos=" +
                string.Join("; ", result.Steps.Select(step => step.Action + " " + step.Path + " " + step.Success + " " + step.ResultPreview)));
            Assert.All(edits, edit => Assert.True(result.Steps.IndexOf(edit) > updatedPlanIndex, "No se debe escribir con el plan anterior."));
            Assert.Contains(edits, edit => edit.Path == "CalculatorTests.cs");
            Assert.Contains(edits, edit => edit.Path == "README.md");
            Assert.True(oldPlanIndex < updatedPlanIndex);
            Assert.Contains("SumCase() => Calculator.Sum(1, 2) == -1", await File.ReadAllTextAsync(testsPath), StringComparison.Ordinal);
            Assert.Contains("Sum subtracts", await File.ReadAllTextAsync(readmePath), StringComparison.Ordinal);
            var updatedPlanPrompt = string.Join("\n", llm.Prompts[plans[1].Iteration - 1]);
            Assert.Contains("public static int Sum(int a, int b) => a + b;", updatedPlanPrompt, StringComparison.Ordinal);
            Assert.Contains("SumCase() => Calculator.Sum(1, 2) == 3", updatedPlanPrompt, StringComparison.Ordinal);
            Assert.Contains("Sum adds two numbers.", updatedPlanPrompt, StringComparison.Ordinal);
            Assert.Contains("El usuario agregó un requisito durante la tarea: Agrega una prueba en CalculatorTests.cs",
                string.Join("\n", llm.Prompts.SelectMany(prompt => prompt)), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(projectRoot, recursive: true);
            Directory.Delete(stateRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_SolicitudAmbigua_PreguntaAntesDeConsultarModeloOModificar()
    {
        var projectRoot = TempDir();
        var stateRoot = TempDir();
        Directory.CreateDirectory(projectRoot);
        Directory.CreateDirectory(stateRoot);
        var sourcePath = Path.Combine(projectRoot, "Calculator.cs");
        const string originalContent = "public static int Sum(int a, int b) => a + b;\n";
        await File.WriteAllTextAsync(sourcePath, originalContent);

        try
        {
            var store = new LocalStateStore(stateRoot);
            var assessment = new StubAssessmentService(16, 8, new[] { "qwen2.5-coder:3b" });
            using var http = new HttpClient(new StubOllamaHandler());
            using var session = new LocalModelSession(http);
            await session.EnsureAvailableAsync("qwen2.5-coder:3b", CancellationToken.None);
            var llm = new SequenceLlmClient(
                "{\"action\":\"edit_file\",\"path\":\"Calculator.cs\",\"content\":\"public static int Sum(int a, int b) => a - b;\\n\"}");
            var service = new AgentService(
                store,
                assessment,
                new AgentLimits { MaxIterations = 4 },
                llm,
                new StubLlmDiagnostics(),
                session,
                () => projectRoot,
                CreateModelAutoSetup(store, assessment, "qwen2.5-coder:3b"));

            var result = await service.RunAsync("mejora esto");

            Assert.False(result.Success);
            Assert.True(result.AwaitingUserInput, result.Reason);
            Assert.Contains("¿Qué parte del proyecto quieres que cambie", result.Reason, StringComparison.Ordinal);
            Assert.Equal("solicitud_ambigua", result.Checkpoint?.LastDecision);
            Assert.Equal(0, llm.CallCount);
            Assert.Empty(result.Steps);
            Assert.Equal(originalContent, await File.ReadAllTextAsync(sourcePath));
        }
        finally
        {
            Directory.Delete(projectRoot, recursive: true);
            Directory.Delete(stateRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_PerfilReviewer_RespetaSoloLecturaYRechazaEdicion()
    {
        var projectRoot = TempDir();
        var stateRoot = TempDir();
        Directory.CreateDirectory(projectRoot);
        Directory.CreateDirectory(stateRoot);
        var sourcePath = Path.Combine(projectRoot, "Calculator.cs");
        const string originalContent = "public static int Sum(int a, int b) => a + b;\n";
        await File.WriteAllTextAsync(sourcePath, originalContent);

        try
        {
            var store = new LocalStateStore(stateRoot);
            var assessment = new StubAssessmentService(16, 8, new[] { "qwen2.5-coder:3b" });
            using var http = new HttpClient(new StubOllamaHandler());
            using var session = new LocalModelSession(http);
            await session.EnsureAvailableAsync("qwen2.5-coder:3b", CancellationToken.None);
            var llm = new SequenceLlmClient(
                "{\"action\":\"list_dir\",\"path\":\".\"}",
                "{\"action\":\"read_file\",\"path\":\"Calculator.cs\"}",
                "{\"action\":\"edit_file\",\"path\":\"Calculator.cs\",\"content\":\"public static int Sum(int a, int b) => a - b;\\n\"}",
                "{\"action\":\"done\",\"reason\":\"Revisión terminada sin hallazgos.\",\"reviewFindings\":[]}");
            var service = new AgentService(
                store,
                assessment,
                new AgentLimits { MaxIterations = 6 },
                llm,
                new StubLlmDiagnostics(),
                session,
                () => projectRoot,
                CreateModelAutoSetup(store, assessment, "qwen2.5-coder:3b"));

            var result = await service.RunAsync("@reviewer Revisa Calculator.cs y reporta defectos concretos.");

            Assert.False(result.Success);
            Assert.Contains("no autoriza la acción 'edit_file'", result.Reason, StringComparison.Ordinal);
            Assert.Equal(originalContent, await File.ReadAllTextAsync(sourcePath));
            Assert.Contains(result.Steps, step => step.Action == "read_file" && step.Success && step.Path == "Calculator.cs");
            Assert.Contains(result.Steps, step => step.Action == "edit_file" && !step.Success);
            var reviewPrompt = string.Join("\n", llm.Prompts[0]);
            Assert.Contains("Perfil activo: Revisión (reviewer)", reviewPrompt, StringComparison.Ordinal);
            Assert.Contains("Este perfil es de solo lectura", reviewPrompt, StringComparison.Ordinal);
            Assert.Equal(3, llm.CallCount);
        }
        finally
        {
            Directory.Delete(projectRoot, recursive: true);
            Directory.Delete(stateRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_RevisionPreexistente_NoLaAtribuyeAlCambioDeOtroArchivo()
    {
        var projectRoot = TempDir();
        Directory.CreateDirectory(projectRoot);
        var calculatorPath = Path.Combine(projectRoot, "Calculator.cs");
        await File.WriteAllTextAsync(calculatorPath, "public static int Subtract(int a, int b) => a - b;\n");
        await File.WriteAllTextAsync(Path.Combine(projectRoot, "README.md"), "base\n");
        try
        {
            await RunGitAsync(projectRoot, "init");
            await RunGitAsync(projectRoot, "config", "user.name", "Condor Tests");
            await RunGitAsync(projectRoot, "config", "user.email", "condor-tests@example.invalid");
            await RunGitAsync(projectRoot, "add", "Calculator.cs", "README.md");
            await RunGitAsync(projectRoot, "commit", "-m", "base con defecto preexistente");
            await File.WriteAllTextAsync(Path.Combine(projectRoot, "README.md"), "cambio independiente\n");

            var stateRoot = TempDir();
            try
            {
                var store = new LocalStateStore(stateRoot);
                var assessment = new StubAssessmentService(16, 8, new[] { "qwen2.5-coder:3b" });
                using var http = new HttpClient(new StubOllamaHandler());
                using var session = new LocalModelSession(http);
                await session.EnsureAvailableAsync("qwen2.5-coder:3b", CancellationToken.None);
                var llm = new SequenceLlmClient(
                    "{\"action\":\"list_dir\",\"path\":\".\"}",
                    "{\"action\":\"read_file\",\"path\":\"Calculator.cs\"}",
                    "{\"action\":\"done\",\"reason\":\"Hallazgo anterior al cambio actual.\",\"reviewFindings\":[{\"path\":\"Calculator.cs\",\"line\":1,\"severity\":\"high\",\"explanation\":\"La operación implementa una resta y el contrato documentado exige sumar.\",\"evidence\":\"a - b\",\"reproduction\":\"Invoca Subtract(4, 2) y compara el resultado con 6.\",\"origin\":\"pre-existing\",\"status\":\"open\"}]}");
                var service = new AgentService(
                    store,
                    assessment,
                    new AgentLimits { MaxIterations = 5 },
                    llm,
                    new StubLlmDiagnostics(),
                    session,
                    () => projectRoot,
                    CreateModelAutoSetup(store, assessment, "qwen2.5-coder:3b"));

                var result = await service.RunAsync(
                    "@reviewer Revisa el cambio y separa defectos preexistentes de los introducidos.",
                    cancellationToken: CancellationToken.None);

                Assert.False(result.Success);
                Assert.Contains("hallazgos abiertos", result.Reason, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("La evidencia literal no coincide", result.Reason, StringComparison.Ordinal);
                Assert.DoesNotContain("pre-existing, pero su línea sí cambió", result.Reason, StringComparison.Ordinal);
            }
            finally
            {
                Directory.Delete(stateRoot, recursive: true);
            }
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(projectRoot, "*", SearchOption.AllDirectories))
            {
                try { File.SetAttributes(file, FileAttributes.Normal); } catch { }
            }
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_RevisionConHallazgoAbierto_NoPermiteCerrarYConservaLaEvidencia()
    {
        var projectRoot = TempDir();
        var stateRoot = TempDir();
        Directory.CreateDirectory(projectRoot);
        Directory.CreateDirectory(stateRoot);
        var sourcePath = Path.Combine(projectRoot, "Calculator.cs");
        const string originalContent = "public static int Sum(int a, int b) => a + b;\n";
        await File.WriteAllTextAsync(sourcePath, originalContent);

        try
        {
            var store = new LocalStateStore(stateRoot);
            var assessment = new StubAssessmentService(16, 8, new[] { "qwen2.5-coder:3b" });
            using var http = new HttpClient(new StubOllamaHandler());
            using var session = new LocalModelSession(http);
            await session.EnsureAvailableAsync("qwen2.5-coder:3b", CancellationToken.None);
            var llm = new SequenceLlmClient(
                "{\"action\":\"list_dir\",\"path\":\".\"}",
                "{\"action\":\"read_file\",\"path\":\"Calculator.cs\"}",
                "{\"action\":\"done\",\"reason\":\"Hay un defecto verificable.\",\"reviewFindings\":[{\"path\":\"Calculator.cs\",\"line\":1,\"severity\":\"high\",\"explanation\":\"El resultado no coincide con el comportamiento solicitado.\",\"evidence\":\"public static int Sum(int a, int b) => a + b;\",\"reproduction\":\"Llama Sum(1, 2).\",\"origin\":\"uncertain\",\"status\":\"open\"}]}");
            var service = new AgentService(
                store,
                assessment,
                new AgentLimits { MaxIterations = 5 },
                llm,
                new StubLlmDiagnostics(),
                session,
                () => projectRoot,
                CreateModelAutoSetup(store, assessment, "qwen2.5-coder:3b"));

            var result = await service.RunAsync("@reviewer Revisa Calculator.cs y reporta defectos concretos.");

            Assert.False(result.Success);
            Assert.Contains("hallazgos abiertos", result.Reason, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Calculator.cs", result.Reason, StringComparison.Ordinal);
            Assert.Contains("El resultado no coincide con el comportamiento solicitado.", result.Reason, StringComparison.Ordinal);
            Assert.Contains("Llama Sum(1, 2).", result.Reason, StringComparison.Ordinal);
            Assert.Equal(originalContent, await File.ReadAllTextAsync(sourcePath));
        }
        finally
        {
            Directory.Delete(projectRoot, recursive: true);
            Directory.Delete(stateRoot, recursive: true);
        }
    }

    private static string TempDir()
    {
        return Path.Combine(Path.GetTempPath(), "condor-agent-resource-" + Guid.NewGuid().ToString("N"));
    }

    private static async Task<string> RunGitAsync(string workingDirectory, params string[] arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);

        using var process = Process.Start(start) ?? throw new InvalidOperationException("No se pudo iniciar git.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = await outputTask;
        var error = await errorTask;
        if (process.ExitCode != 0)
            throw new InvalidOperationException("git " + string.Join(" ", arguments) + " falló: " + error);
        return output;
    }

    private static ModelAutoSetupService CreateModelAutoSetup(
        LocalStateStore store,
        IAssessmentService assessment,
        params string[] installedModels)
    {
        var detector = new StubOllamaDetector(installedModels);
        var modelOperator = new OllamaModelOperator(new HttpClient(new StubOllamaHandler()), detector);
        return new ModelAutoSetupService(store, assessment, modelOperator: modelOperator);
    }

    private sealed class StubAssessmentService : IAssessmentService
    {
        private readonly double _ramTotalGb;
        private readonly double _ramFreeGb;
        private readonly string[] _installedModels;
        public int ExecuteCount { get; private set; }

        public StubAssessmentService(double ramTotalGb, double ramFreeGb, string[] installedModels)
        {
            _ramTotalGb = ramTotalGb;
            _ramFreeGb = ramFreeGb;
            _installedModels = installedModels;
        }

        public Task<AssessmentResult> ExecuteAsync(AssessmentRequest request, CancellationToken cancellationToken = default)
        {
            ExecuteCount++;
            return Task.FromResult(new AssessmentResult
            {
                Environment = new EnvironmentProfile
                {
                    Memory = new MemoryInfo
                    {
                        Status = DetectionStatus.Detected,
                        TotalBytes = (long)(_ramTotalGb * 1024d * 1024 * 1024),
                        FreeBytes = (long)(_ramFreeGb * 1024d * 1024 * 1024),
                        AvailableBytes = (long)(_ramFreeGb * 1024d * 1024 * 1024)
                    }
                },
                Tools = new ToolsProfile
                {
                    Ollama = new OllamaStatus
                    {
                        Installed = true,
                        ServerRunning = true,
                        Models = new List<ModelInfo>(
                            _installedModels.Select(name => new ModelInfo
                            {
                                Name = name,
                                SizeBytes = GetModelSizeBytes(name),
                                Capabilities = new List<string> { "completion", "tool-use", "structured-output", "coding" }
                            }))
                    }
                },
                Capabilities = new CapabilitiesSummary { ModelsCount = _installedModels.Length, OllamaReady = true }
            });
        }

        private static long GetModelSizeBytes(string name)
        {
            return name switch
            {
                "qwen2.5-coder:0.5b" => 397L * 1024 * 1024,
                "qwen2.5-coder:1.5b" => 986L * 1024 * 1024,
                "qwen2.5-coder:3b" => 1848L * 1024 * 1024,
                _ => 1848L * 1024 * 1024
            };
        }
    }

    private sealed class StubLlmClient : ILlmClient
    {
        public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new LlmResponse
            {
                Success = true,
                Content = "Hola. Estoy listo para ayudarte.",
                Model = request.Model,
                Outcome = LlmOutcome.Ok
            });
    }

    private sealed class SequenceLlmClient : ILlmClient
    {
        private readonly Queue<string> _responses;
        private readonly Action<int>? _beforeResponse;

        public SequenceLlmClient(params string[] responses)
        {
            _responses = new Queue<string>(responses);
        }

        public SequenceLlmClient(Action<int> beforeResponse, params string[] responses)
        {
            _beforeResponse = beforeResponse;
            _responses = new Queue<string>(responses);
        }

        public List<string[]> Prompts { get; } = new();
        public List<LlmRequest> Requests { get; } = new();
        public int CallCount => Prompts.Count;

        public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            Prompts.Add(request.Messages?.Select(message => message.Content ?? "").ToArray() ?? Array.Empty<string>());
            _beforeResponse?.Invoke(Prompts.Count);
            var content = _responses.Count > 0 ? _responses.Dequeue() : "";
            return Task.FromResult(new LlmResponse
            {
                Success = true,
                Content = content,
                Model = request.Model,
                Outcome = LlmOutcome.Ok
            });
        }
    }

    private sealed class CollectingAgentProgressObserver : IAgentProgressObserver
    {
        public List<AgentProgress> Updates { get; } = new();

        public void Report(AgentProgress progress) => Updates.Add(progress);
    }

    private sealed class CommitChangingApprovalChannel(
        string workingDirectory,
        CollectingAgentProgressObserver progress) : IAgentSteeringChannel
    {
        public bool TryDequeue(out string? instruction)
        {
            instruction = null;
            return false;
        }

        public async ValueTask<string> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var question = progress.Updates.LastOrDefault(update => update.Action == "aprobación humana")?.Message
                ?? throw new InvalidOperationException("No se recibió una solicitud explícita de aprobación.");
            const string marker = "APROBAR ";
            var markerIndex = question.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
                throw new InvalidOperationException("La solicitud no incluyó el código de aprobación.");
            var code = question[(markerIndex + marker.Length)..].Split(new[] { ' ', ';', '.' }, StringSplitOptions.RemoveEmptyEntries)[0];
            await RunGitAsync(workingDirectory, "commit", "--allow-empty", "-m", "change while approval waits");
            return "APROBAR " + code;
        }
    }

    private sealed class RecordingDelegationService : IAgentDelegationService
    {
        public int ExecuteCount { get; private set; }
        public string? LastReviewTask { get; private set; }
        public IReadOnlyList<string> LastReviewProfileIds { get; private set; } = Array.Empty<string>();

        public Task<AgentStep> ExecuteAsync(
            AgentAction action,
            string workingDirectory,
            int iteration,
            IAgentProgressObserver? progress,
            CancellationToken cancellationToken = default)
        {
            ExecuteCount++;
            var reviewSubtasks = action.Subtasks?.Where(subtask =>
                subtask.ProfileId is "reviewer" or "security-reviewer").ToList() ?? new List<AgentSubtask>();
            LastReviewTask = reviewSubtasks.FirstOrDefault()?.Task;
            LastReviewProfileIds = reviewSubtasks.Select(subtask => subtask.ProfileId!).ToList();
            return Task.FromResult(new AgentStep
            {
                Iteration = iteration,
                Action = AgentAction.ActionDelegate,
                Success = true,
                ResultPreview = "Revisión simulada completada."
            });
        }
    }

    private sealed class StubLlmDiagnostics : ILlmProviderDiagnostics
    {
        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public string ProviderName => "Stub";
    }

    private sealed class StubOllamaHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"version\":\"stub\"}", System.Text.Encoding.UTF8, "application/json")
            });
    }

    private sealed class StubOllamaDetector(params string[] installedModels) : OllamaDetector
    {
        public override Task<OllamaStatus> DetectAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new OllamaStatus
            {
                Installed = true,
                ServerRunning = true,
                Models = installedModels.Select(name => new ModelInfo { Name = name }).ToList()
            });
    }
}
