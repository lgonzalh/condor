using System.Globalization;
using System.Text.Json;
using Condor.Core.Contracts;
using Condor.Core.Evaluation;
using Condor.Core.Models;

namespace Condor.Cli.Commands;

public static class DeliveryMetricsCommand
{
    public static async Task<int> ExecuteAsync(
        IStateStore stateStore,
        string[] args,
        CancellationToken cancellationToken = default,
        string? workingDirectory = null)
    {
        workingDirectory ??= Environment.CurrentDirectory;
        DeliveryMetricsState state;
        try
        {
            state = await stateStore.LoadDeliveryMetricsAsync(workingDirectory, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (IsMetricsStorageError(exception))
        {
            return Invalid("No se pudieron leer las métricas locales. Revisa que el archivo de métricas exista y sea válido.");
        }
        if (args.Length == 0 || Is(args[0], "resumen", "ver"))
        {
            RenderSummary(state, DateTimeOffset.UtcNow);
            return 0;
        }

        if (Is(args[0], "ayuda", "help"))
        {
            RenderUsage();
            return 0;
        }

        var action = args[0].Trim().ToLowerInvariant();
        var values = args.Skip(1).ToArray();
        var now = DateTimeOffset.UtcNow;
        if (!TryApplyAction(state, action, values, now, out var message, out var error))
        {
            return Invalid(error);
        }

        try
        {
            await stateStore.SaveDeliveryMetricsAsync(workingDirectory, state, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (IsMetricsStorageError(exception))
        {
            return Invalid("No se pudo guardar la métrica local; el registro no se confirmó.");
        }
        Console.WriteLine(message);
        RenderSummary(state, now);
        return 0;
    }

    private static bool TryApplyAction(
        DeliveryMetricsState state,
        string action,
        string[] values,
        DateTimeOffset now,
        out string message,
        out string error)
    {
        message = string.Empty;
        error = string.Empty;
        return action switch
        {
            "iniciar" => TryStartChange(state, values, now, out message, out error),
            "entregar" => TryRecordDelivery(state, values, now, out message, out error),
            "fallo" => TryRecordFailure(state, values, now, out message, out error),
            "recuperar" => TryRecordRecovery(state, values, now, out message, out error),
            "retrabajo" => TryRecordRework(state, values, now, out message, out error),
            _ => SetError("Acción desconocida. Usa /metricas para ver el resumen o /metricas ayuda para consultar la sintaxis.", out error)
        };
    }

    private static bool TryStartChange(DeliveryMetricsState state, string[] values, DateTimeOffset now, out string message, out string error)
    {
        if (!TryReadIdAndTime(values, now, out var id, out var startedAt, out error))
        {
            message = string.Empty;
            return false;
        }
        if (Find(state, id) is not null)
        {
            message = string.Empty;
            error = $"El cambio '{id}' ya está registrado.";
            return false;
        }

        state.Changes.Add(new DeliveryChangeRecord { ChangeId = id, StartedAtUtc = startedAt });
        message = $"Inicio manual del cambio '{id}' registrado.";
        error = string.Empty;
        return true;
    }

    private static bool TryRecordDelivery(DeliveryMetricsState state, string[] values, DateTimeOffset now, out string message, out string error)
    {
        if (!TryReadIdAndTime(values, now, out var id, out var deliveredAt, out error))
        {
            message = string.Empty;
            return false;
        }
        var delivery = Find(state, id);
        if (delivery?.StartedAtUtc is null) return SetError("Registra primero /metricas iniciar <cambio> para calcular Change Lead Time.", out message, out error);
        if (delivery.DeliveredAtUtc is not null) return SetError($"El cambio '{id}' ya tiene una entrega registrada.", out message, out error);
        if (deliveredAt < delivery.StartedAtUtc) return SetError("La fecha de entrega no puede ser anterior al inicio.", out message, out error);

        delivery.DeliveredAtUtc = deliveredAt;
        message = $"Entrega ya realizada del cambio '{id}' registrada manualmente; Condor no ejecutó ningún despliegue.";
        error = string.Empty;
        return true;
    }

    private static bool TryRecordFailure(DeliveryMetricsState state, string[] values, DateTimeOffset now, out string message, out string error)
    {
        if (!TryReadIdAndOptionalReason(values, now, out var id, out var failedAt, out var reason, out error))
        {
            message = string.Empty;
            return false;
        }
        var failed = Find(state, id);
        if (failed?.DeliveredAtUtc is null) return SetError("Solo puedes registrar un fallo para un cambio con entrega registrada.", out message, out error);
        if (failed.FailedAtUtc is not null) return SetError($"El cambio '{id}' ya tiene un fallo registrado.", out message, out error);
        if (failedAt < failed.DeliveredAtUtc) return SetError("La fecha del fallo no puede ser anterior a la entrega.", out message, out error);
        if (string.IsNullOrWhiteSpace(reason)) return SetError("Indica el motivo del fallo: /metricas fallo <cambio> [fecha ISO-8601] <motivo>.", out message, out error);

        failed.FailedAtUtc = failedAt;
        failed.FailureReason = Limit(reason, 500);
        message = $"Fallo posterior a la entrega del cambio '{id}' registrado.";
        error = string.Empty;
        return true;
    }

    private static bool TryRecordRecovery(DeliveryMetricsState state, string[] values, DateTimeOffset now, out string message, out string error)
    {
        if (!TryReadIdAndTime(values, now, out var id, out var recoveredAt, out error))
        {
            message = string.Empty;
            return false;
        }
        var recovery = Find(state, id);
        if (recovery?.FailedAtUtc is null) return SetError("Registra primero /metricas fallo <cambio> <motivo>.", out message, out error);
        if (recovery.RecoveredAtUtc is not null) return SetError($"El incidente del cambio '{id}' ya tiene recuperación registrada.", out message, out error);
        if (recoveredAt < recovery.FailedAtUtc) return SetError("La fecha de recuperación no puede ser anterior al fallo.", out message, out error);

        recovery.RecoveredAtUtc = recoveredAt;
        message = $"Recuperación manual del cambio '{id}' registrada.";
        error = string.Empty;
        return true;
    }

    private static bool TryRecordRework(DeliveryMetricsState state, string[] values, DateTimeOffset now, out string message, out string error)
    {
        if (values.Length < 2) return SetError("Uso: /metricas retrabajo <cambio> <motivo>.", out message, out error);
        var id = NormalizeId(values[0]);
        var rework = Find(state, id);
        if (rework?.DeliveredAtUtc is null) return SetError("Solo puedes registrar retrabajo para un cambio con entrega registrada.", out message, out error);
        if (rework.ReworkAtUtc is not null) return SetError($"El cambio '{id}' ya tiene retrabajo registrado.", out message, out error);
        var reason = Limit(string.Join(' ', values.Skip(1)).Trim(), 500);
        if (string.IsNullOrWhiteSpace(reason)) return SetError("Indica el motivo del retrabajo.", out message, out error);

        rework.ReworkAtUtc = now;
        rework.ReworkReason = reason;
        message = $"Retrabajo del cambio '{id}' registrado con su motivo.";
        error = string.Empty;
        return true;
    }

    private static bool SetError(string errorText, out string error)
    {
        error = errorText;
        return false;
    }

    private static bool SetError(string errorText, out string message, out string error)
    {
        message = string.Empty;
        error = errorText;
        return false;
    }

    private static void RenderSummary(DeliveryMetricsState state, DateTimeOffset now)
    {
        var summary = DeliveryMetricsCalculator.Summarize(state, now);

        Console.WriteLine("Métricas de entrega (datos registrados manualmente)");
        Console.WriteLine($"Entregas: {summary.DeliveriesInLast30Days} en los últimos 30 días ({summary.WeeklyDeploymentFrequency:0.##} por semana)");
        Console.WriteLine($"Change Lead Time medio: {FormatDuration(summary.AverageChangeLeadTime)} ({summary.DeliveredChangeCount} entregas)");
        Console.WriteLine($"Change Failure Rate: {FormatRate(summary.ChangeFailureRate)} ({summary.FailedChangeCount}/{summary.DeliveredChangeCount})");
        Console.WriteLine($"Tiempo medio de recuperación: {FormatDuration(summary.AverageRecoveryTime)} ({summary.FailedChangeCount - summary.UnrecoveredIncidentCount} incidentes recuperados; {summary.UnrecoveredIncidentCount} pendientes)");
        Console.WriteLine($"Deployment Rework Rate: {FormatRate(summary.DeploymentReworkRate)} ({summary.ReworkedChangeCount}/{summary.DeliveredChangeCount})");
        var linkedTasks = state.Changes.Sum(change => change.RelatedTaskExecutionIds?.Count ?? 0);
        var linkedIncidentTasks = state.Changes.Sum(change => change.IncidentTaskExecutionIds?.Count ?? 0);
        Console.WriteLine($"Ejecuciones Condor vinculadas: {linkedTasks} a cambios; {linkedIncidentTasks} a incidentes.");
        foreach (var reason in summary.ReworkReasons)
        {
            Console.WriteLine($"  Retrabajo {reason}");
        }
        Console.WriteLine("Estos registros no consultan plataformas ni ejecutan publicaciones o despliegues.");
    }

    private static bool TryReadIdAndTime(string[] values, DateTimeOffset now, out string id, out DateTimeOffset time, out string error)
    {
        id = string.Empty;
        time = now;
        error = "";
        if (values.Length is < 1 or > 2)
        {
            error = "Uso: /metricas iniciar|entregar|recuperar <cambio> [fecha ISO-8601].";
            return false;
        }

        id = NormalizeId(values[0]);
        if (id.Length is 0 or > 120)
        {
            error = "El identificador del cambio debe tener entre 1 y 120 caracteres.";
            return false;
        }

        if (values.Length == 2 && !TryParseTime(values[1], out time))
        {
            error = "La fecha debe usar el formato ISO-8601, por ejemplo 2026-11-04T15:30:00Z.";
            return false;
        }

        return true;
    }

    private static bool TryReadIdAndOptionalReason(
        string[] values,
        DateTimeOffset now,
        out string id,
        out DateTimeOffset time,
        out string reason,
        out string error)
    {
        id = string.Empty;
        time = now;
        reason = string.Empty;
        error = "";
        if (values.Length < 2)
        {
            error = "Uso: /metricas fallo <cambio> [fecha ISO-8601] <motivo>.";
            return false;
        }

        id = NormalizeId(values[0]);
        if (id.Length is 0 or > 120)
        {
            error = "El identificador del cambio debe tener entre 1 y 120 caracteres.";
            return false;
        }

        var reasonStart = 1;
        if (TryParseTime(values[1], out var parsed))
        {
            time = parsed;
            reasonStart = 2;
        }
        reason = string.Join(' ', values.Skip(reasonStart)).Trim();
        return true;
    }

    private static bool TryParseTime(string value, out DateTimeOffset parsed)
    {
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out parsed))
        {
            parsed = parsed.ToUniversalTime();
            return true;
        }
        return false;
    }

    private static DeliveryChangeRecord? Find(DeliveryMetricsState state, string id) =>
        state.Changes.FirstOrDefault(change => string.Equals(change.ChangeId, id, StringComparison.OrdinalIgnoreCase));

    private static string NormalizeId(string value) => value.Trim();

    private static bool Is(string value, params string[] candidates) => candidates.Contains(value, StringComparer.OrdinalIgnoreCase);

    private static string Limit(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];

    private static int Invalid(string message)
    {
        Console.Error.WriteLine(message);
        return 2;
    }

    private static bool IsMetricsStorageError(Exception exception) => exception is
        IOException or
        UnauthorizedAccessException or
        JsonException or
        ArgumentException or
        NotSupportedException or
        System.Security.SecurityException;

    private static string FormatRate(double? rate) => rate is null ? "sin datos" : $"{rate.Value:0.#}%";

    private static string FormatDuration(TimeSpan? duration) => duration is null
        ? "sin datos"
        : $"{duration.Value.TotalHours:0.##} h";

    private static void RenderUsage()
    {
        Console.WriteLine("/metricas                                  Muestra el resumen actual.");
        Console.WriteLine("/metricas iniciar <cambio> [fecha ISO-8601]");
        Console.WriteLine("/metricas entregar <cambio> [fecha ISO-8601]  Registra una entrega ya realizada.");
        Console.WriteLine("/metricas fallo <cambio> [fecha ISO-8601] <motivo>");
        Console.WriteLine("/metricas recuperar <cambio> [fecha ISO-8601]");
        Console.WriteLine("/metricas retrabajo <cambio> <motivo>");
        Console.WriteLine("Para enlazar una tarea usa [[condor:cambio:<id>]]; para una recuperación, [[condor:incidente:<id>]].");
        Console.WriteLine("Las fechas omitidas usan la hora actual. Las métricas dependen de registros manuales.");
    }
}
