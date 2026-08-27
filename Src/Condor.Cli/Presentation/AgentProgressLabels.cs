using System;
using System.Collections.Generic;
using Condor.Core.Models;

namespace Condor.Cli.Presentation;

/// <summary>
/// Origen Único de la verdad para las etiquetas de progreso de Condor (T-019/T-020 P1).
///
/// Antes de esta unificación la TUI (TuiStartupView/TuiAgentProgressView) y la CLI
/// clásica (StartupProgressPresenter/AgentProgressPresenter) mantenían DOS copias de
/// las mismas etiquetas con textos DIVERGENTES (p. ej. CLI "Preparando entorno" vs
/// TUI "Preparando el entorno local"). Esa duplicación es la raíz de la deuda de
/// T-019. Aquí viven las etiquetas canónicas (texto rico y honesto, con objeto real de
/// la fase); cada presentador delega al origen único para que los modos no diverge.
/// Las superficies siguen siendo responsabilidad de cada presentador. Regla: NUNCA
/// una fase genérica sin detalle.
/// </summary>
internal static class AgentProgressLabels
{
    public static string PhaseTag(AgentPhase phase) => phase switch
    {
        AgentPhase.Understanding => "SOLICITUD",
        AgentPhase.Observing => "AGENTE",
        AgentPhase.Analyzing => "AGENTE",
        AgentPhase.Building => "AGENTE",
        AgentPhase.Verifying => "VERIFICACION",
        AgentPhase.Finalizing => "RESPUESTA",
        _ => "AGENTE",
    };

    public static string PhaseEstado(AgentProgress p)
    {
        if (p.Flag == ProgressFlag.ProviderError)
            return "El proveedor local no esta disponible ahora";

        if (p.Flag == ProgressFlag.Recovering)
            return "Recuperando el proveedor local" + (Subject(p).Length > 0 ? " — " + Subject(p) : "");

        var estado = p.Phase switch
        {
            AgentPhase.Understanding => "Comprendiendo la solicitud",
            AgentPhase.Observing => "Observando el proyecto",
            AgentPhase.Analyzing => "Analizando lo observado",
            AgentPhase.Building => "Aplicando cambios",
            AgentPhase.Verifying => "Verificando resultado de los cambios",
            AgentPhase.Finalizing => "Redactando la respuesta final",
            _ => "Trabajando",
        };

        if (p.Phase is not (AgentPhase.Verifying or AgentPhase.Finalizing or AgentPhase.Understanding))
            estado += Subject(p);

        return estado;
    }

    internal static string Subject(AgentProgress p)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(p.Action))
            parts.Add(p.Action!);
        if (!string.IsNullOrWhiteSpace(p.Path) && p.Path is not ("." or "./"))
            parts.Add(p.Path!);
        return parts.Count == 0 ? "" : " (" + string.Join(" ", parts) + ")";
    }

    public static string BuildProgreso(AgentProgress p, TimeSpan elapsed)
    {
        var segments = new List<string>();
        if (p.Iteration is { } it)
            segments.Add("Iteracion " + it);
        if (!string.IsNullOrWhiteSpace(p.ResourceState) && p.AvailableGb is { } free)
        {
            var budget = p.SafeBudgetGb is { } safe
                ? ", presupuesto " + safe.ToString("0.0") + " GB"
                : "";
            segments.Add("RAM libre " + free.ToString("0.0") + " GB" + budget + " (" + p.ResourceState + ")");
        }

        if (!string.IsNullOrWhiteSpace(p.Message))
            segments.Add(p.Message!);

        segments.Add(FormatElapsed(elapsed));
        return string.Join(" · ", segments);
    }

    public static string StageTag(StartupStage stage) => stage switch
    {
        StartupStage.PreparingEnvironment => "ENTORNO",
        StartupStage.ReviewingResources => "MEMORIA",
        StartupStage.DetectingOllama => "OLLAMA",
        StartupStage.BootstrappingDependencies => "ENTORNO",
        StartupStage.InstallingOllama => "OLLAMA",
        StartupStage.StartingOllamaServer => "OLLAMA",
        StartupStage.VerifyingOllamaServer => "OLLAMA",
        StartupStage.EvaluatingModels => "MODELO",
        StartupStage.SelectingModel => "MODELO",
        StartupStage.DownloadingModel => "MODELO",
        StartupStage.VerifyingModel => "VERIFICACION",
        StartupStage.Ready => "DECISION",
        _ => "ENTORNO",
    };

    public static string StageLabel(StartupStage stage) => stage switch
    {
        StartupStage.PreparingEnvironment => "Preparando el entorno local",
        StartupStage.ReviewingResources => "Revisando recursos del equipo",
        StartupStage.DetectingOllama => "Detectando Ollama en este equipo",
        StartupStage.BootstrappingDependencies => "Preparando dependencias locales",
        StartupStage.InstallingOllama => "Instalando Ollama",
        StartupStage.StartingOllamaServer => "Iniciando Ollama Server",
        StartupStage.VerifyingOllamaServer => "Verificando disponibilidad de Ollama Server",
        StartupStage.EvaluatingModels => "Evaluando modelos instalados",
        StartupStage.SelectingModel => "Seleccionando modelo adecuado para el equipo",
        StartupStage.DownloadingModel => "Descargando modelo",
        StartupStage.VerifyingModel => "Verificando modelo obtenido",
        StartupStage.Ready => "Entorno listo",
        _ => "Preparando el entorno",
    };

    internal static string StageEstado(StartupStage stage) => StageLabel(stage);

    public static string StageCompleted(StartupStage stage) => stage switch
    {
        StartupStage.PreparingEnvironment => "Entorno preparado",
        StartupStage.ReviewingResources => "Recursos del equipo detectados",
        StartupStage.DetectingOllama => "Ollama detectado",
        StartupStage.BootstrappingDependencies => "Dependencias locales preparadas",
        StartupStage.InstallingOllama => "Ollama instalado",
        StartupStage.StartingOllamaServer => "Ollama Server iniciado",
        StartupStage.VerifyingOllamaServer => "Ollama Server verificado y disponible",
        StartupStage.EvaluatingModels => "Modelos instalados evaluados",
        StartupStage.SelectingModel => "Modelo seleccionado",
        StartupStage.DownloadingModel => "Modelo descargado",
        StartupStage.VerifyingModel => "Modelo verificado",
        StartupStage.Ready => "Entorno listo para trabajar",
        _ => "Etapa completada",
    };

    internal static string StageCompletedLabel(StartupStage stage) => StageCompleted(stage);

    public static string BuildBar(double percent)
    {
        const int width = 14;
        var filled = (int)Math.Floor(Math.Clamp(percent, 0, 100) / 100.0 * width);
        return new string('█', filled) + new string('░', width - filled);
    }

    public static string FormatPercent(double percent) => Math.Round(percent) + "%";

    public static string FormatElapsed(TimeSpan el)
    {
        return el.TotalHours >= 1
            ? string.Format("{0:00}:{1:00}:{2:00}", (int)el.TotalHours, el.Minutes, el.Seconds)
            : string.Format("{0:00}:{1:00}", el.Minutes, el.Seconds);
    }
}
