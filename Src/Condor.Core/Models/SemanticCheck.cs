namespace Condor.Core.Models;

public class SemanticCheck
{
    public const string KindCompile = "compilar";
    public const string KindTest = "probar";

    public const string StatusCorrect = "correcta";
    public const string StatusFailed = "fallida";
    public const string StatusNotAvailable = "no_disponible";
    public const string StatusNotSupported = "no_soportado";
    public const string StatusTimeout = "timeout";
    public const string StatusNotExecutable = "no_ejecutable";
    public const string StatusIncomplete = "incompleta";
    public const string StatusCancelled = "cancelada";

    public string Kind { get; set; } = "";
    public string Tool { get; set; } = "";
    public string Command { get; set; } = "";
    public int? ExitCode { get; set; }
    public bool TimeoutExpired { get; set; }
    public string Status { get; set; } = "";
    public string Output { get; set; } = "";
    public string? Reason { get; set; }
}
