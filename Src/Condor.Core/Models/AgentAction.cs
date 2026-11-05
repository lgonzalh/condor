using System.Collections.Generic;

namespace Condor.Core.Models;

// Accion estructurada que el modelo decide y Condor valida+ejecuta.
public class AgentAction
{
    public const string ActionListDir = "list_dir";
    public const string ActionReadFile = "read_file";
    public const string ActionPatch = "patch";
    public const string ActionEditFile = "edit_file";
    public const string ActionCreateFile = "create_file";
    public const string ActionBuild = "build";
    public const string ActionTest = "test";
    public const string ActionRestore = "restore";
    public const string ActionGitStatus = "git_status";
    public const string ActionGitDiff = "git_diff";
    public const string ActionGitLog = "git_log";
    public const string ActionGitShow = "git_show";
    public const string ActionSearch = "search";
    public const string ActionUndoFile = "undo_file";
    public const string ActionPlan = "plan";
    public const string ActionClarify = "clarify";
    public const string ActionDelegate = "delegate";
    public const string ActionDone = "done";

    public string Action { get; set; } = "";
    public string? Path { get; set; }
    public string? Content { get; set; }

    // Para patch: el fragmento exacto/parcial a localizar y reemplazar.
    public string? Original { get; set; }
    public string? Replacement { get; set; }

    public string? Reason { get; set; }
    public List<string>? AllowedActions { get; set; }

    // El plan se declara antes de editar. Las rutas son relativas al proyecto;
    // los directorios completos se indican con el sufijo /**.
    public string? Objective { get; set; }
    public List<string>? ScopePaths { get; set; }
    public List<string>? ExcludedPaths { get; set; }
    public List<string>? PlanSteps { get; set; }
    public List<string>? ExpectedResults { get; set; }
    public List<string>? VerificationSteps { get; set; }
    public List<AgentPlanItem>? PlanItems { get; set; }
    public string? RiskLevel { get; set; }

    // Tareas independientes que el agente principal puede encargar a agentes aislados.
    public List<AgentSubtask>? Subtasks { get; set; }

    // Hallazgos estructurados que los perfiles de revisión entregan al agente principal.
    public List<AgentReviewFinding>? ReviewFindings { get; set; }
}
