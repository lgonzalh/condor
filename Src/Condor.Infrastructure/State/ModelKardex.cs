using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Condor.Infrastructure.State;

public enum ModelKardexStatus
{
    /// <summary>Modelo instalado y verificado en Ollama.</summary>
    Instalado,

    /// <summary>El presupuesto de RAM determino que el modelo no es ejecutable ahora.</summary>
    RechazadoPorPresupuesto,

    /// <summary>Intento de obtencion fallido tras reintentos acotados.</summary>
    FalloObtencion
}

/// <summary>Entrada del kardex local de modelos: un movimiento con fecha y motivo.</summary>
public sealed class ModelKardexEntry
{
    public string Model { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Reason { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

/// <summary>
/// Kardex local de modelos: registro historico de decisiones sobre modelos
/// (instalado, rechazado por presupuesto, fallo de obtencion) persistido junto
/// al estado local. El inventario VIVO siempre manda (Ollama /api/tags); el
/// kardex solo aporta historia para diagnosticos y para no repetir intentos
/// que ya se sabe que no proceden. Nunca inventa modelos ni sustituye al server.
/// </summary>
public sealed class ModelKardex
{
    private const string FileName = "kardex_modelos.json";
    private readonly string _filePath;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public ModelKardex(string? stateDirectory = null)
    {
        var dir = stateDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Condor", "state");
        _filePath = Path.Combine(dir, FileName);
    }

    /// <summary>Registra (o actualiza) el estado de un modelo en el kardex.</summary>
    public async Task RecordAsync(string model, ModelKardexStatus status, string? reason = null)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return;
        }

        try
        {
            var entries = await LoadEntriesAsync();
            var normalized = model.Trim();
            entries.RemoveAll(e => string.Equals(e.Model, normalized, StringComparison.OrdinalIgnoreCase));
            entries.Add(new ModelKardexEntry
            {
                Model = normalized,
                Status = status.ToString(),
                Reason = reason,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
            entries.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Model, b.Model));

            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            await File.WriteAllTextAsync(_filePath, JsonSerializer.Serialize(entries, JsonOptions), new UTF8Encoding(false));
        }
        catch
        {
            // El kardex es auxiliar: cualquier error de E/S no debe afectar la tarea.
        }
    }

    /// <summary>Devuelve la entrada mas reciente del modelo, o null si no hay registro.</summary>
    public async Task<ModelKardexEntry?> GetAsync(string model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return null;
        }

        try
        {
            var entries = await LoadEntriesAsync();
            return entries
                .Where(e => string.Equals(e.Model, model.Trim(), StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(e => e.UpdatedAtUtc)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private async Task<List<ModelKardexEntry>> LoadEntriesAsync()
    {
        if (!File.Exists(_filePath))
        {
            return new List<ModelKardexEntry>();
        }

        try
        {
            var json = await File.ReadAllTextAsync(_filePath);
            var entries = JsonSerializer.Deserialize<List<ModelKardexEntry>>(json);
            return entries ?? new List<ModelKardexEntry>();
        }
        catch
        {
            // Archivo corrupto: se reinicia el kardex sin propagar el error.
            return new List<ModelKardexEntry>();
        }
    }
}
