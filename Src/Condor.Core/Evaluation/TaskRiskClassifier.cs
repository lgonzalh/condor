namespace Condor.Core.Evaluation;

public enum TaskRiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

public sealed record TaskRiskAssessment(TaskRiskLevel Level, string Rationale);

/// <summary>Conservative, deterministic floor for the controls requested in the task plan.</summary>
public static class TaskRiskClassifier
{
    private const string NegatedSignalPattern =
        @"(?:\bno\b|\bnunca\b|\bsin\b|\bevitar?\b|\bavoid\b|\bdon't\b|\bdo\s+not\b|\bnever\b|\bwithout\b)\s+(?:(?!\b(?:pero|but|sino|excepto)\b)[\p{L}\p{N}_./\\-]+\s+){0,3}[\p{L}\p{N}_./\\-]*$";

    private static readonly string[] CriticalSignals =
    {
        "production", "produccion", "producción", "deploy", "deployment", "despliegue",
        "publish", "publicar", "delete", "eliminar", "borrar", "drop table", "migration",
        "migracion", "migración", "payment", "pago", "credential", "credencial", "secret",
        "secreto", "private key", "clave privada", "llave privada", "authentication",
        "autenticacion", "autenticación", "authorization", "autorizacion",
        "autorización", "encryption", "cifrado", "security", "seguridad", "personal data",
        "datos personales", "customer data", "datos de clientes", "datos del cliente",
        ".env", ".pem", ".pfx", ".key", ".github/workflows", "workflow"
    };

    private static readonly string[] HighSignals =
    {
        "database", "base de datos", "schema", "esquema", "dependency", "dependencia",
        "package upgrade", "actualizar paquete", "actualizacion de paquetes", "actualización de paquetes",
        "configuration", "configuracion", "configuración", "permission", "permiso",
        "access control", "control de acceso", "network", "external service", "servicio externo",
        "release", "lanzamiento"
    };

    public static TaskRiskAssessment Assess(string? intention)
        => Assess(intention, null);

    public static TaskRiskAssessment Assess(string? intention, string? targetPath)
    {
        var request = (intention ?? string.Empty).Trim();
        var text = (request + " " + (targetPath ?? string.Empty)).Trim().ToLowerInvariant();
        if (text.Length == 0)
            return new TaskRiskAssessment(TaskRiskLevel.High, "La solicitud está vacía y no permite delimitar su impacto.");

        var extension = System.IO.Path.GetExtension(request);
        var documentationOnly = extension is ".md" or ".txt" or ".rst" or ".adoc" ||
            request.Contains("documentación", System.StringComparison.OrdinalIgnoreCase) ||
            request.Contains("documentation", System.StringComparison.OrdinalIgnoreCase);

        var hasCriticalIntent = CriticalSignals.Any(signal => ContainsUnnegatedSignal(request, signal));
        var hasCriticalTarget = CriticalSignals.Any(signal =>
            (targetPath ?? string.Empty).Contains(signal, System.StringComparison.OrdinalIgnoreCase));
        if (hasCriticalIntent || hasCriticalTarget)
            return new TaskRiskAssessment(TaskRiskLevel.Critical, "La solicitud puede afectar datos, seguridad, publicación o un entorno operativo.");
        if (HighSignals.Any(signal => text.Contains(signal, System.StringComparison.Ordinal)))
            return new TaskRiskAssessment(TaskRiskLevel.High, "La solicitud puede cambiar configuración, dependencias o interfaces compartidas.");
        if (documentationOnly)
            return new TaskRiskAssessment(TaskRiskLevel.Low, "El alcance declarado es documental y reversible.");
        return new TaskRiskAssessment(TaskRiskLevel.Medium, "La solicitud puede modificar el comportamiento del proyecto y requiere verificación.");
    }

    private static bool ContainsUnnegatedSignal(string text, string signal)
    {
        var searchFrom = 0;
        while (searchFrom < text.Length)
        {
            var index = text.IndexOf(signal, searchFrom, System.StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                return false;

            var prefix = text[..index];
            if (!System.Text.RegularExpressions.Regex.IsMatch(
                    prefix,
                    NegatedSignalPattern,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase |
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant))
            {
                return true;
            }

            searchFrom = index + signal.Length;
        }

        return false;
    }
}
