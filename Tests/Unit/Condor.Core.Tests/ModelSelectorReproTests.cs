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
/// - una tarea de ingenieria conserva el protocolo estructurado y sus requisitos
///   de capacidades;
/// - una consulta conversacional simple como "hola" puede usar el modelo mas
///   pequeno, sin forzarle el protocolo de acciones del agente.
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

    // La recomendacion clasica de arranque no depende del texto de la tarea:
    // ambos inventarios identicos producen el mismo resultado.
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

    [Fact]
    public void HolaConRamLimitada_SeleccionaElModeloInstaladoMasPequenoSuficiente()
    {
        var assessment = Assessment("qwen2.5-coder:0.5b", ramTotalGb: 16, ramFreeGb: 5.6);
        var requirement = new TaskModelRequirement
        {
            IntentKind = TaskIntentKinds.Agent,
            RequiredCodingLevel = 0,
            RequiredMultiFileLevel = 0,
            RequiresToolUse = true,
            RequiresStructuredOutput = true,
            PreferSmallestSufficient = true,
            Label = "agente de comprension/analisis"
        };

        var r = ModelSelector.SelectForTask(
            assessment,
            ModelCatalog.Default,
            requirement,
            BudgetPolicy.Default);

        Assert.NotNull(r.Desired);
        Assert.Equal("qwen2.5-coder:0.5b", r.Desired.PullName);
        Assert.True(r.AlreadyInstalled);
        Assert.False(r.BlockedByResources);
    }

    [Fact]
    public void HolaConTresGBLibres_UsaPresupuestoMinimoSinBloquearse()
    {
        var assessment = Assessment("qwen2.5-coder:0.5b", ramTotalGb: 16, ramFreeGb: 3.4);
        var requirement = TaskIntentClassifier.Classify("hola");

        var r = ModelSelector.SelectForTask(
            assessment,
            ModelCatalog.Default,
            requirement,
            BudgetPolicy.Default);

        Assert.Equal(TaskIntentKinds.Consult, requirement.IntentKind);
        Assert.NotNull(r.Desired);
        Assert.Equal("qwen2.5-coder:0.5b", r.Desired.PullName);
        Assert.True(r.Budget!.BudgetGb > ModelEfficiencyEvaluator.PeakGb(r.Desired));
        Assert.True(r.AlreadyInstalled);
        Assert.False(r.BlockedByResources);
    }

    [Fact]
    public void SesionEn05BConMemoriaLibre_PromocionaAlModeloSuficienteParaMultiFile()
    {
        var assessment = Assessment("qwen2.5-coder:0.5b", ramTotalGb: 16, ramFreeGb: 8);
        var requirement = TaskIntentClassifier.Classify("analiza el proyecto");

        var r = ModelSelector.SelectForTask(
            assessment,
            ModelCatalog.Default,
            requirement,
            BudgetPolicy.Default,
            currentModelName: "qwen2.5-coder:0.5b");

        Assert.NotNull(r.Desired);
        Assert.Equal("qwen2.5-coder:3b", r.Desired.PullName);
        Assert.Contains("3b", r.Desired.PullName, System.StringComparison.OrdinalIgnoreCase);
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

    /// <summary>
    /// Escenario obligatorio del usuario:
    /// Estado A: ActiveModel = 0.5b, 1.5b no cabe, presupuesto = Y, RAM = X
    ///   → permanecer en 0.5b (conditionsImproved = false, lastSelectionBudgetGb = Y)
    /// Nueva tarea con las mismas condiciones:
    ///   → permanecer en 0.5b, NO intentar 1.5b
    /// Luego el usuario libera RAM:
    ///   → detectar que las condiciones cambiaron (RAM aumentó)
    ///   → reevaluar
    ///   → si 1.5b cabe, promocionar a 1.5b
    /// </summary>
    [Fact]
    public void EscenarioObligatorio_EstabilidadYSuperaConMejoraDeRAM()
    {
        // Estado A: 0.5b activo, RAM insuficiente para 1.5b
        // 1.5b pico ~1.1 GB, headroom = 4 - 1.5 - 1.5 - 1.5 = -0.5 → no cabe
        var requirement = TaskIntentClassifier.Classify("hola");
        var assessmentA = FullAssessment(
            installedModels: new[] { "qwen2.5-coder:0.5b", "qwen2.5-coder:1.5b" },
            ramTotalGb: 16, ramFreeGb: 4.0);

        var rA = ModelSelector.SelectForTask(
            assessmentA,
            ModelCatalog.Default,
            requirement,
            BudgetPolicy.Default,
            currentModelName: "qwen2.5-coder:0.5b",
            lastSelectionBudgetGb: 0); // Sin historial previo = primera seleccion

        // Primera seleccion: 0.5b es suficiente (Consult, no requiere coding), y está instalado
        Assert.NotNull(rA.Desired);
        Assert.Equal("qwen2.5-coder:0.5b", rA.Desired.PullName);

        var initialBudgetGb = rA.Budget?.BudgetGb ?? 0;
        var initialFreeRamGb = rA.Resources?.FreeGb ?? 0;

        // Nueva tarea con las mismas condiciones (misma RAM, mismo presupuesto)
        var rB = ModelSelector.SelectForTask(
            assessmentA,
            ModelCatalog.Default,
            requirement,
            BudgetPolicy.Default,
            currentModelName: "qwen2.5-coder:0.5b",
            lastSelectionBudgetGb: initialBudgetGb,
            lastSelectionFreeRamGb: initialFreeRamGb);

        // MISMA condición: no hay mejora → permanecer en 0.5b, NO promocionar a 1.5b
        Assert.NotNull(rB.Desired);
        Assert.Equal("qwen2.5-coder:0.5b", rB.Desired.PullName);
        Assert.Contains("presupuesto y RAM sin mejora", rB.Reason, System.StringComparison.OrdinalIgnoreCase);

        // Ahora el usuario libera RAM. El presupuesto y la RAM disponible aumentan.
        var assessmentC = FullAssessment(
            installedModels: new[] { "qwen2.5-coder:0.5b", "qwen2.5-coder:1.5b" },
            ramTotalGb: 16, ramFreeGb: 8.0); // RAM libre aumentó de 4.0 a 8.0

        var rC = ModelSelector.SelectForTask(
            assessmentC,
            ModelCatalog.Default,
            requirement,
            BudgetPolicy.Default,
            currentModelName: "qwen2.5-coder:0.5b",
            lastSelectionBudgetGb: initialBudgetGb,
            lastSelectionFreeRamGb: initialFreeRamGb);

        // Condiciones mejoraron (RAM aumentó de 4.0 a 8.0 GB) → promocionar a 1.5b (siguiente nivel)
        Assert.NotNull(rC.Desired);
        Assert.Equal("qwen2.5-coder:1.5b", rC.Desired.PullName);
        Assert.Contains("mejora", rC.Reason, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Escenario: promoción debe ser solo al siguiente nivel (0.5b → 1.5b, no 0.5b → 3b)
    /// aunque 3b también quepa con la nueva RAM.
    /// </summary>
    [Fact]
    public void PromocionSiempreAlSiguienteNivel_NoSaltaNiveles()
    {
        var requirement = new TaskModelRequirement
        {
            IntentKind = TaskIntentKinds.Agent,
            RequiredCodingLevel = 0,
            RequiredMultiFileLevel = 0,
            RequiresToolUse = true,
            RequiresStructuredOutput = true,
            PreferSmallestSufficient = true,
            Label = "consulta simple"
        };

        // Estado A: 0.5b activo, RAM baja
        var assessmentA = FullAssessment(
            installedModels: new[] { "qwen2.5-coder:0.5b", "qwen2.5-coder:1.5b", "qwen2.5-coder:3b" },
            ramTotalGb: 16, ramFreeGb: 4.0);

        var rA = ModelSelector.SelectForTask(
            assessmentA, ModelCatalog.Default, requirement, BudgetPolicy.Default,
            currentModelName: "qwen2.5-coder:0.5b");

        Assert.Equal("qwen2.5-coder:0.5b", rA.Desired?.PullName);

        var initialBudget = rA.Budget?.BudgetGb ?? 0;
        var initialFreeRam = rA.Resources?.FreeGb ?? 0;

        // Estado B: RAM liberada, ahora 3b también cabría, pero solo debe promocionar a 1.5b
        var assessmentB = FullAssessment(
            installedModels: new[] { "qwen2.5-coder:0.5b", "qwen2.5-coder:1.5b", "qwen2.5-coder:3b" },
            ramTotalGb: 16, ramFreeGb: 8.0);

        var rB = ModelSelector.SelectForTask(
            assessmentB, ModelCatalog.Default, requirement, BudgetPolicy.Default,
            currentModelName: "qwen2.5-coder:0.5b",
            lastSelectionBudgetGb: initialBudget,
            lastSelectionFreeRamGb: initialFreeRam);

        // Debe promocionar solo a 1.5b (el siguiente nivel), NUNCA directamente a 3b
        Assert.Equal("qwen2.5-coder:1.5b", rB.Desired?.PullName);
    }

    /// <summary>
    /// Escenario: cuando el presupuesto NO cambia pero la RAM libre aumenta
    /// (caso edge con OperatingMargin variable), se detecta la mejora por la
    /// RAM disponible y se permite la promoción.
    /// </summary>
    [Fact]
    public void MejoraDeRAMDetectadaInclusoCuandoPresupuestoIgual()
    {
        var requirement = TaskIntentClassifier.Classify("hola");

        // Estado A: 16GB total, 4GB libre
        // OperatingMargin = max(1.5, min(3.0, 16*0.08=1.28)) = 1.5
        // budget = 4 - 1.5 - 1.5 - 1.5 = -0.5 → no budgeted
        // Pero para Consult se usa AssessForSmallModel:
        // budget = 4 - 1.0 - 0.5 - 0.5 = 2.0 GB
        var assessmentA = FullAssessment(
            installedModels: new[] { "qwen2.5-coder:0.5b", "qwen2.5-coder:1.5b" },
            ramTotalGb: 16, ramFreeGb: 4.0);

        var rA = ModelSelector.SelectForTask(
            assessmentA, ModelCatalog.Default, requirement, BudgetPolicy.Default,
            currentModelName: "qwen2.5-coder:0.5b");

        Assert.Equal("qwen2.5-coder:0.5b", rA.Desired?.PullName);
        var initialBudget = rA.Budget?.BudgetGb ?? 0;
        var initialFreeRam = rA.Resources?.FreeGb ?? 0;

        // Estado B: RAM libre aumenta a 8GB (budget también aumenta de 2.0 a 6.0)
        var assessmentB = FullAssessment(
            installedModels: new[] { "qwen2.5-coder:0.5b", "qwen2.5-coder:1.5b" },
            ramTotalGb: 16, ramFreeGb: 8.0);

        var rB = ModelSelector.SelectForTask(
            assessmentB, ModelCatalog.Default, requirement, BudgetPolicy.Default,
            currentModelName: "qwen2.5-coder:0.5b",
            lastSelectionBudgetGb: initialBudget,
            lastSelectionFreeRamGb: initialFreeRam);

        // Tanto budget como RAM libre aumentaron → promocionar a 1.5b
        Assert.Equal("qwen2.5-coder:1.5b", rB.Desired?.PullName);
    }

    /// <summary>
    /// Escenario: ActiveModel insuficiente para la tarea. El modelo activo no
    /// es suficiente, por lo que no se hace early return; se procede a la selección
    /// normal. MinimumViable nunca debe convertirse automáticamente en ActiveModel.
    /// </summary>
    [Fact]
    public void ActiveModelInsuficiente_NoEarlyReturn_SeleccionNormal()
    {
        // 0.5b instado (ActiveModel) pero tarea requiere CodingLevel=3
        // 0.5b tiene CodingLevel=1 → NO suficiente → no hay early return
        var requirement = TaskIntentClassifier.Classify("analiza el proyecto");
        Assert.Equal(TaskIntentKinds.Agent, requirement.IntentKind);

        var assessment = FullAssessment(
            installedModels: new[] { "qwen2.5-coder:0.5b", "qwen2.5-coder:1.5b", "qwen2.5-coder:3b" },
            ramTotalGb: 16, ramFreeGb: 8.0);

        var r = ModelSelector.SelectForTask(
            assessment, ModelCatalog.Default, requirement, BudgetPolicy.Default,
            currentModelName: "qwen2.5-coder:0.5b");

        // El 0.5b no es suficiente para task agente (CodingLevel 1 < 3)
        // Normal selection should pick 3b (smallest sufficient that fits)
        Assert.NotNull(r.Desired);
        Assert.Equal("qwen2.5-coder:3b", r.Desired.PullName);
    }

    private static AssessmentResult FullAssessment(string[] installedModels, double ramTotalGb, double ramFreeGb)
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
                    Models = new List<ModelInfo>(
                        installedModels.Select(name => new ModelInfo
                        {
                            Name = name,
                            SizeBytes = GetModelSizeBytes(name),
                            Capabilities = new List<string> { "completion", "tool-use", "structured-output", "coding" }
                        }))
                }
            },
            Capabilities = new CapabilitiesSummary { ModelsCount = installedModels.Length, OllamaReady = true }
        };
    }

    private static long GetModelSizeBytes(string name)
    {
        return name switch
        {
            "qwen2.5-coder:0.5b" => 397L * 1024 * 1024,
            "qwen2.5-coder:1.5b" => 986L * 1024 * 1024,
            "qwen2.5-coder:3b" => 1848L * 1024 * 1024,
            "qwen2.5-coder:7b" => 4706L * 1024 * 1024,
            _ => 1848L * 1024 * 1024
        };
    }
}
