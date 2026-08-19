using System.Collections.Generic;

namespace Condor.Core.Models;

// Accion estructurada que el modelo decide y Condor valida+ejecuta.
public class AgentAction
{
    public const string ActionListDir = "list_dir";
    public const string ActionReadFile = "read_file";
    public const string ActionEditFile = "edit_file";
    public const string ActionCreateFile = "create_file";
    public const string ActionBuild = "build";
    public const string ActionTest = "test";
    public const string ActionGitStatus = "git_status";
    public const string ActionSearch = "search";
    public const string ActionDone = "done";

    public string Action { get; set; } = "";
    public string? Path { get; set; }
    public string? Content { get; set; }
    public string? Reason { get; set; }
    public List<string>? AllowedActions { get; set; }
}
