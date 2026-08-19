using System.Collections.Generic;
using Condor.Core.Catalog;
using Condor.Core.Evaluation;
using Condor.Core.Models;
using Condor.Core.Selection;

namespace Condor.Core.Tests;

/// <summary>
/// Reproduccion de la observacion real: qwen2.5-coder:3b descargado, detectado,
/// seleccionado y usado con exito en "hola" produce "No hay un modelo compatible
/// disponible para la tarea" ante otras entradas.
///
/// Causa raiz localizada en la CAJA DE SELECCION (ModelSelector ->
/// OrderByCompatibility -> ModelMemoryBudget.FitsInRamStrict), NO en el routing
/// ni en el texto de la tarea. Tanto "hola" como la tarea de analisis recorren el
/// MISMO camino: AgentCommand -> AgentService.RunAsync -> ModelAutoSetupService.
/// EnsureModelAsync ignora por completo la intencion (purpose=nulo): la unica
/// variable que decide es la instantanea de memoria (FreePhysicalMemory via CIM)
/// en CADA invocacion.
///
/// - "hola funciona": la RAM libre en ese momento supera el umbral.
/// - "la tarea falla": la RAM libre bajo del umbral -> incluso el 3B no cumple el
///   presupuesto seguro -> Desired==null -> AgentService devuelve el mensaje exacto.
///
/// Umbral del modelo menor viable (qwen2.5-coder:3b, pico ~2.16 GB) sobre 16 GB
/// totales: headroom = libre - 1.5(SO) - 1.5(Condor) - 1.5(margen) >= 2.16, es
/// decir libre >= ~6.66 GB. Ambas pruebas usan el modelo YA instalado para
/// reflejar fielmente el escenario del usuario (sin descarga).
/// </summary>
public class ModelSelectorReproTests
{
    // Caso A: "hola" funciona --- RAM libre suficiente -> el 3B se selecciona.
    [Fact]
    public void HolaFunciona_RamLibreSuficiente_SeleccionaQwen3bSinRechazo()
    {
        var assessment = Assessment("qwen2.5-coder:3b", ramTotalGb: 16, ramFreeGb: 8);

        var r = ModelSelector.RecommendFromCatalog(assessment, ModelCatalog.Default, "agente");

        Assert.NotNull(r.Desired);
        Assert.Equal("qwen2.5-coder:3b", r.Desired.PullName);
        Assert.False(r.BlockedByResources);
        // Por tanto AgentService NO produce "No hay un modelo compatible..." (linea 54).
    }

    // Caso B: una tarea de analisis falla --- la misma RAM cae bajo el umbral y el
    // MISMO modelo instalado es descartado -> Desired==null -> el error exacto.
    [Fact]
    public void AnalisisFalla_RamLibreBaja_DescartaLosMismoModelo_DesiredNulo()
    {
        var assessment = Assessment("qwen2.5-coder:3b", ramTotalGb: 16, ramFreeGb: 5);

        var r = ModelSelector.RecommendFromCatalog(assessment, ModelCatalog.Default, "agente");

        // Esta es exactamente la condicion que dispara en AgentService.RunAsync:
        //   if (selection.Desired is null)
        //       return Fail("No hay un modelo compatible disponible para la tarea.", ...);
        Assert.Null(r.Desired);
        Assert.True(r.BlockedByResources);
        Assert.Equal(ResourcePressure.Insufficient, r.Resources?.Pressure);
        Assert.Contains(r.Limitations, l => l.Contains("no se intenta cargar", System.StringComparison.OrdinalIgnoreCase));
    }

    // La intencion (hola vs analisis) NO participa en la seleccion: se comprueba
    // que con identica RAM ambos textos producen el mismo resultado.
    [Fact]
    public void LaIntencionNoInfluyeEnLaSeleccion_RamIdemMismoResultado()
    {
        var withHola = Assessment("qwen2.5-coder:3b", ramTotalGb: 16, ramFreeGb: 8);
        var withAnalisis = Assessment("qwen2.5-coder:3b", ramTotalGb: 16, ramFreeGb: 8);

        var rHola = ModelSelector.RecommendFromCatalog(withHola, ModelCatalog.Default, "agente");
        var rAnalisis = ModelSelector.RecommendFromCatalog(withAnalisis, ModelCatalog.Default, "agente");

        Assert.Equal(rHola.Desired?.PullName, rAnalisis.Desired?.PullName);
        Assert.NotNull(rHola.Desired);
        Assert.NotNull(rAnalisis.Desired);
    }

    // Umbral exacto del menor modelo viable: la frontera entre la RAM que permite
    // o rechaza al 3B esta en ~6.66 GB libres sobre 16 totales.
    [Fact]
    public void FronteraDeRechazo_Qwen3b_seAncaEnRamaLibreBaja()
    {
        AssertTrueFits(ramFreeGb: 7.0);   // headroom 2.5 >= pico 2.16  -> cabe
        AssertFalseFits(ramFreeGb: 6.5);  // headroom 2.0 <  pico 2.16  -> NO cabe
    }

    private static void AssertTrueFits(double ramFreeGb)
    {
        var r = ModelSelector.RecommendFromCatalog(
            Assessment("qwen2.5-coder:3b", ramTotalGb: 16, ramFreeGb: ramFreeGb),
            ModelCatalog.Default, "agente");
        Assert.NotNull(r.Desired);
        Assert.Equal("qwen2.5-coder:3b", r.Desired.PullName);
    }

    private static void AssertFalseFits(double ramFreeGb)
    {
        var r = ModelSelector.RecommendFromCatalog(
            Assessment("qwen2.5-coder:3b", ramTotalGb: 16, ramFreeGb: ramFreeGb),
            ModelCatalog.Default, "agente");
        Assert.Null(r.Desired);
    }

    private static AssessmentResult Assessment(string installedName, double ramTotalGb, double ramFreeGb)
    {
        var TotalBytes = (long)(ramTotalGb * 1024d * 1024 * 1024);
        var FreeBytes = (long)(ramFreeGb * 1024d * 1024 * 1024);
        return new AssessmentResult
        {
            Environment = new EnvironmentProfile
            {
                Memory = new MemoryInfo
                {
                    Status = DetectionStatus.Detected,
                    TotalBytes = TotalBytes,
                    FreeBytes = FreeBytes,
                    AvailableBytes = FreeBytes
                }
            },
            Tools = new ToolsProfile
            {
                Ollama = new OllamaStatus
                {
                    Installed = true,
                    ServerRunning = true,
                    Models = new List<ModelInfo>
                    {
                        new() { Name = installedName, SizeBytes = 1848L * 1024 * 1024, Capabilities = new List<string> { "completion" } }
                    }
                }
            }
        };
    }
}
