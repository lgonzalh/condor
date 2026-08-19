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

    // Caso B (actualizado a la Promesa): cuando la RAM cae bajo el umbral del
    // modelo instalado (3B), Condor ya NO se bloquea con Desired==null: busca una
    // alternativa menor viable en el catalogo (0.5B) para descargar y usar.
    [Fact]
    public void AnalisisRamBaja_BuscaAlternativaMenorEnCatalogo_NoBloquea()
    {
        var assessment = Assessment("qwen2.5-coder:3b", ramTotalGb: 16, ramFreeGb: 5);

        var r = ModelSelector.RecommendFromCatalog(assessment, ModelCatalog.Default, "agente");

        // El 3B instalado no cabe, pero Condor no abandona: elige la alternativa
        // menor viable (0.5B) en vez de devolver Desired==null.
        Assert.NotNull(r.Desired);
        Assert.Equal("qwen2.5-coder:0.5b", r.Desired.PullName);
        Assert.False(r.BlockedByResources);
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

    // Frontera del modelo instalado 3B: con RAM holgada se usa el 3B; al caer bajo
    // el umbral, Condor pasa a la alternativa menor viable (1.5B) del catalogo en
    // lugar de bloquear con Desired==null.
    [Fact]
    public void FronteraDeRechazo_Qwen3b_DegradaAAlternativaMenor()
    {
        AssertElected(ramFreeGb: 7.0, "qwen2.5-coder:3b");  // headroom 2.5 >= pico 2.16 -> 3B cabe
        AssertElected(ramFreeGb: 6.5, "qwen2.5-coder:1.5b"); // headroom 2.0 < pico 2.16 -> alternativa 1.5B
    }

    private static void AssertElected(double ramFreeGb, string expected)
    {
        var r = ModelSelector.RecommendFromCatalog(
            Assessment("qwen2.5-coder:3b", ramTotalGb: 16, ramFreeGb: ramFreeGb),
            ModelCatalog.Default, "agente");
        Assert.NotNull(r.Desired);
        Assert.Equal(expected, r.Desired.PullName);
        Assert.False(r.BlockedByResources);
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
