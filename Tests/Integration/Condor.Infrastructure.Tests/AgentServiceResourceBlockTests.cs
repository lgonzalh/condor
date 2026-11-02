using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
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
            "{\"action\":\"create_file\",\"path\":\"index.html\",\"content\":\"<!doctype html><html lang=\\\"es\\\"><body>Condor</body></html>\"}");
        var service = new AgentService(
            store,
            assessment,
            new AgentLimits { MaxIterations = 1, MaxPlanBIterations = 1 },
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
        Assert.Contains(result.Steps, step => step.Action == AgentAction.ActionCreateFile && step.Success);
        Assert.Equal("no_aplicable", result.Checkpoint?.HarnessState);
        Assert.Contains("Plan B", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no aplican", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, llm.CallCount);
        Assert.Contains(llm.Prompts[1], message => message.Contains("PLAN B ACTIVADO", StringComparison.Ordinal));
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
        Assert.Contains(llm.Prompts.SelectMany(prompt => prompt), prompt =>
            prompt.Contains("Observacion repetida sin informacion nueva", StringComparison.OrdinalIgnoreCase) &&
            prompt.Contains("no la repitas", StringComparison.OrdinalIgnoreCase));
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
            "{\"action\":\"edit_file\",\"path\":\"index.html\",\"content\":\"" + withoutLink.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"}");
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
            "Actualiza la pagina existente en la ruta exacta index.html (raiz del proyecto, no docs/index.html). Conserva el enlace relativo exacto docs/indice-condor.html.",
            cancellationToken: CancellationToken.None);

        var updated = await File.ReadAllTextAsync(Path.Combine(projectRoot, "index.html"));
        Assert.True(result.Success, result.Reason);
        Assert.Contains("<a href=\"docs/indice-condor.html\">Documentacion generada</a>", updated);
        Assert.Equal("plan_b_corregido", result.Checkpoint?.LastDecision);
        Assert.Equal(1, llm.CallCount);
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
        Assert.True(result.Success, result.Reason);
        Assert.Contains("<a href=\"docs/indice-condor.html\">Documentacion generada</a>", updated);
        Assert.DoesNotContain("<link rel=\"stylesheet\"", updated, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("plan_b_corregido", result.Checkpoint?.LastDecision);
        Assert.Equal(1, llm.CallCount);
        Assert.Contains(result.Steps, step =>
            step.Action == AgentAction.ActionEditFile &&
            step.Success &&
            step.ResultPreview?.Contains("Plan B inserto el enlace HTML exacto", StringComparison.OrdinalIgnoreCase) == true);
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
            "{\"action\":\"patch/edit_file\",\"path\":\"index.html\",\"content\":" +
            System.Text.Json.JsonSerializer.Serialize(updated) + "}");
        var service = new AgentService(
            store,
            assessment,
            new AgentLimits { MaxIterations = 1, MaxPlanBIterations = 1 },
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
        Assert.Equal(2, llm.CallCount);
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
        Assert.Equal(1, result.Checkpoint?.PlanBIterationsUsed);
    }

    [Fact]
    public async Task RunAsync_RespuestaMalformadaEnActualizacion_ActivaPlanBCorrectivo()
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
        Assert.Contains(llm.Prompts[1], message => message.Contains("PLAN B ACTIVADO", StringComparison.Ordinal));
        Assert.Contains(llm.Prompts[1], message =>
            message.Contains("nombre tomado de esta lista", StringComparison.Ordinal) &&
            message.Contains("create_file", StringComparison.Ordinal) &&
            message.Contains("Nunca combines nombres con '/'", StringComparison.Ordinal));
        Assert.Contains(result.Steps, step => step.Action == AgentAction.ActionCreateFile && step.Success);
        Assert.Equal("plan_b_corregido", result.Checkpoint?.LastDecision);
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

    private static string TempDir()
    {
        return Path.Combine(Path.GetTempPath(), "condor-agent-resource-" + Guid.NewGuid().ToString("N"));
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

        public SequenceLlmClient(params string[] responses)
        {
            _responses = new Queue<string>(responses);
        }

        public List<string[]> Prompts { get; } = new();
        public int CallCount => Prompts.Count;

        public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
        {
            Prompts.Add(request.Messages?.Select(message => message.Content ?? "").ToArray() ?? Array.Empty<string>());
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
