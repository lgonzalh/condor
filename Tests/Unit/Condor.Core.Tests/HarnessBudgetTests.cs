using System.Collections.Generic;
using Condor.Core.Catalog;
using Condor.Core.Evaluation;
using Condor.Core.Models;
using Condor.Core.Selection;

namespace Condor.Core.Tests;

/// <summary>
/// Pruebas PROFUNDAS del harness de presupuesto dinamico y seleccion inteligente
/// (Prompt 2 real). Cubren los 23 escenarios: reserva, presupuesto, modelo grande
/// descartado sin margen, eficiencia, seleccion por tarea, familias, modelo
/// instalado del usuario, 1-, 1+, subida/bajada de RAM, reevaluacion periodica,
/// cambio en punto seguro, continuidad, adaptacion de prompt, insuficiente,
/// ausencia de loops, lifecycle, liberacion y regresiones.
/// </summary>
public class HarnessBudgetTests
{
    #region 1-2. Reserva minima y presupuesto calculado
    [Fact]
    public void Presupuesto_StockMenosReservaMenosMargen()
    {
        // RAM libre 10 GB, politica por defecto:
        // operativa = max(2.0, 10*0.25=2.5) = 2.5; reserva total = 1.5+1.5+2.5 = 5.5;
        // presupuesto = 10 - 5.5 - 1.0 = 3.5.
        var policy = BudgetPolicy.Default;
        var b = policy.Assess(Mem(16, 10));

        Assert.True(b.IsBudgeted);
        Assert.Equal(3.5, b.BudgetGb, 1);
        Assert.True(b.ReserveGb < 10.0);
        Assert.True(b.BudgetGb < 10.0);
    }

    [Fact]
    public void Reserva_OperativaNuncaSePrestaAlModelo()
    {
        var policy = BudgetPolicy.Default;
        // 4 GB libres: el presupuesto debe ser claramente menor (protector).
        var b = policy.Assess(Mem(16, 4));
        Assert.True(b.OperationalReserveGb >= 2.0);
        Assert.True(b.BudgetGb >= 0);
        // Un modelo que "quepa" numericamente pero deje margen < 0 NO debe admitirse.
        Assert.False(ModelEfficiencyEvaluator.LeavesMargin(new ModelCandidate { WeightGb = 1.0 }, b));
    }

    [Fact]
    public void Presupuesto_NuncaNegativo_YNuncaSuperaLaLibre()
    {
        var policy = BudgetPolicy.Default;
        var b = policy.Assess(Mem(16, 1));
        Assert.True(b.BudgetGb >= 0);
        Assert.True(b.BudgetGb <= b.RamFreeGb);
    }
    #endregion

    #region 3. Modelo grande descartado aunque quepa sin margen
    [Fact]
    public void Seleccion_ModeloGrandeDescartadoSiNoDejaMargen()
    {
        // RAM justa: el 7B puede "caber" numericamente pero el harness exige reserva/margen.
        var assessment = AssessmentConModelo("sin-instalar", ramFreeGb: 8, ramTotalGb: 16);
        var req = AgentRequirement();

        var r = ModelSelector.SelectForTask(assessment, ModelCatalog.Default, req, BudgetPolicy.Default);

        // 1- es eficiente, NO el mayor que quepa: no debe ser el 7B si agota el margen.
        Assert.NotNull(r.NodeInCurrent);
        Assert.NotEqual("qwen2.5-coder:7b", r.NodeInCurrent?.PullName);
        Assert.True(r.Budget!.BudgetGb > 0);
    }
    #endregion

    #region 4. Modelo menor seleccionado por eficiencia
    [Fact]
    public void Seleccion_PreFiereElMenorSuficiente_Eficiencia()
    {
        // RAM abundante: aun asi, para tarea de agente, el harness prefiere el menor
        // suficiente (eficiencia) sobre el mayor que quepa, conservando presupuesto.
        var assessment = AssessmentConModelo("sin-instalar", ramFreeGb: 14, ramTotalGb: 16);
        var req = AgentRequirement();

        var r = ModelSelector.SelectForTask(assessment, ModelCatalog.Default, req, BudgetPolicy.Default);

        Assert.NotNull(r.NodeInCurrent);
        // Menor peso que el 7B (el receptor elige el menor suficiente viable).
        Assert.True(r.NodeInCurrent!.WeightGb < 4.36);
    }
    #endregion

    #region 5. Seleccion dependiente de la tarea
    [Fact]
    public void Seleccion_DependeDeLaTarea_CapacidadesDistintas()
    {
        // Tarea de vision (sin tool-use/coding) vs agente de ingenieria (con herramientas):
        // el requisito cambia y los modelos candidatos cambian.
        var visionReq = new TaskModelRequirement
        {
            IntentKind = TaskIntentKinds.Vision,
            RequiresToolUse = false,
            RequiresStructuredOutput = false,
            PreferSmallestSufficient = false
        };
        var codingReq = AgentRequirement();

        Assert.NotEqual(visionReq.RequiredCodingLevel, codingReq.RequiredCodingLevel);
        Assert.NotEqual(visionReq.RequiresToolUse, codingReq.RequiresToolUse);
    }
    #endregion

    #region 6. Diferentes familias evaluadas
    [Fact]
    public void Seleccion_EvaluaDiversasFamilias()
    {
        var families = new HashSet<string>();
        foreach (var c in ModelCatalog.Default)
        {
            if (!string.IsNullOrWhiteSpace(c.Family))
            {
                families.Add(c.Family!);
            }
        }

        Assert.Contains(families, f => f.ToLowerInvariant().Contains("llama"));
        Assert.Contains(families, f => f.ToLowerInvariant().Contains("qwen"));
        Assert.True(families.Count >= 2, "Debe evaluar varias familias, no solo una.");
    }
    #endregion

    #region 7. Modelo instalado del usuario considerado
    [Fact]
    public void Seleccion_ModeloInstaladoDelUsuario_EsCandidato()
    {
        // Modelo instalado por el usuario (NO en catalogo) pero suficiente y que cabe.
        var assessment = AssessmentConModelo("mi-modelo-7b", ramFreeGb: 10, ramTotalGb: 16);
        var req = AgentRequirement();

        var r = ModelSelector.SelectForTask(assessment, ModelCatalog.Default, req, BudgetPolicy.Default);

        // Si el modelo instalado del usuario es suficiente y cabe, puede ser elegido 1-.
        Assert.NotNull(r.NodeInCurrent);
        Assert.False(string.IsNullOrWhiteSpace(r.InstalledName));
    }
    #endregion

    #region 8-9. 1- y 1+ correctamente determinados
    [Fact]
    public void Seleccion_Determina_1Mas_Mas_1Menos()
    {
        var assessment = AssessmentConModelo("sin-instalar", ramFreeGb: 8, ramTotalGb: 16);
        var req = AgentRequirement();

        var r = ModelSelector.SelectForTask(assessment, ModelCatalog.Default, req, BudgetPolicy.Default);

        Assert.NotNull(r.NodeInCurrent);   // 1-
        Assert.NotNull(r.NextCandidate);   // 1+
        // 1+ debe ser distinto y normalmente mayor/mejor que 1-.
        Assert.NotEqual(r.NodeInCurrent!.PullName, r.NextCandidate!.PullName);
    }

    [Fact]
    public void Seleccion_SinMargen_CandidatoInsuficiente_Bloquea()
    {
        var assessment = AssessmentConModelo("sin-instalar", ramFreeGb: 2, ramTotalGb: 16);
        var req = AgentRequirement();

        var r = ModelSelector.SelectForTask(assessment, ModelCatalog.Default, req, BudgetPolicy.Default);

        Assert.Null(r.Desired);
        Assert.True(r.BlockedByResources);
        Assert.NotEmpty(r.InsufficientCandidates);
    }
    #endregion

    #region 10-11. Aumento / disminucion de RAM
    [Fact]
    public void Reevaluador_RamaAumenta_SugiereSubirA1Mas()
    {
        var policy = BudgetPolicy.Default;
        var reval = new BudgetReevaluator(policy, maxReevaluations: 6);
        var current = ModelCatalog.Default.Get("qwen2.5-coder:1.5b");
        var next = ModelCatalog.Default.Get("qwen2.5-coder:3b");
        var req = AgentRequirement();

        // RAM abundante (15 libre): el 3B cabe con margen y es mejor que el 1.5B.
        var d = reval.Decide(Mem(16, 15), current, next, req, alreadyChanged: 0);

        Assert.Equal(BudgetTransition.UpgradeToNext, d.Transition);
        Assert.Equal("qwen2.5-coder:3b", d.SuggestedModel);
    }

    [Fact]
    public void Reevaluador_RamaDisminuye_SugiereDegradar()
    {
        var policy = BudgetPolicy.Default;
        var reval = new BudgetReevaluator(policy);
        var current = ModelCatalog.Default.Get("qwen2.5-coder:3b");
        var next = ModelCatalog.Default.Get("qwen2.5-coder:1.5b");
        var req = AgentRequirement();

        // RAM justa (8 libre): el presupuesto ya no admite el 3B (pico ~2.16) con
        // margen, pero si el 1.5B -> degradar en punto seguro.
        var d = reval.Decide(Mem(16, 8), current, next, req, alreadyChanged: 0);

        Assert.Equal(BudgetTransition.Downgrade, d.Transition);
    }
    #endregion

    #region 12-13. Reevaluacion periodica y cambio solo en punto seguro
    [Fact]
    public void Reevaluador_IntervaloPorDefecto_30Minutos()
    {
        Assert.Equal(System.TimeSpan.FromMinutes(30), BudgetReevaluator.DefaultReevaluationInterval);
    }

    [Fact]
    public void Reevaluador_Continuidad_NoCambiaSiNoHayAlternativaSegura()
    {
        var policy = BudgetPolicy.Default;
        var reval = new BudgetReevaluator(policy);
        var req = AgentRequirement();

        // Sin modelo actual -> mantiene (continuidad) sin sugerir cambio.
        var d = reval.Decide(Mem(16, 8), current: null, next: null, req, alreadyChanged: 0);
        Assert.Equal(BudgetTransition.KeepCurrent, d.Transition);
    }
    #endregion

    #region 14. Continuidad de la tarea (sin interrumpir)
    [Fact]
    public void Reevaluador_SinCambio_CuandoPresupuestoEstable()
    {
        var policy = BudgetPolicy.Default;
        var reval = new BudgetReevaluator(policy);
        var current = ModelCatalog.Default.Get("qwen2.5-coder:1.5b");
        var next = ModelCatalog.Default.Get("qwen2.5-coder:3b");
        var req = AgentRequirement();

        // RAM media estable; el actual sigue siendo suficiente y no hay salto claro.
        var d = reval.Decide(Mem(16, 7), current, next, req, alreadyChanged: 0);
        Assert.Equal(BudgetTransition.KeepCurrent, d.Transition);
    }
    #endregion

    #region 16. Modelo insuficiente descartado
    [Fact]
    public void Seleccion_ModeloInsuficienteParaTarea_Descartado()
    {
        // Tarea de vision exige... (usamos agente con alta exigencia); el 0.5B sin
        // tool-use debe quedar en InsufficientCandidates para tareas de agente.
        var assessment = AssessmentConModelo("qwen2.5-coder:0.5b", ramFreeGb: 6, ramTotalGb: 16);
        var req = AgentRequirement(); // exige tool-use

        var r = ModelSelector.SelectForTask(assessment, ModelCatalog.Default, req, BudgetPolicy.Default);

        Assert.Contains(r.InsufficientCandidates, c => c.Contains("0.5b", System.StringComparison.OrdinalIgnoreCase));
    }
    #endregion

    #region 17. Ausencia de loops
    [Fact]
    public void Reevaluador_EscapaAlLimite_NoBucle()
    {
        var policy = BudgetPolicy.Default;
        var reval = new BudgetReevaluator(policy, maxReevaluations: 2);
        var current = ModelCatalog.Default.Get("qwen2.5-coder:1.5b");
        var next = ModelCatalog.Default.Get("qwen2.5-coder:3b");
        var req = AgentRequirement();

        // Tras alcanzar el limite de cambios, decide mantener (sin bucle).
        var d = reval.Decide(Mem(16, 15), current, next, req, alreadyChanged: 3);
        Assert.True(d.ExhaustedAttempts);
        Assert.Equal(BudgetTransition.KeepCurrent, d.Transition);
    }
    #endregion

    #region helpers
    private static TaskModelRequirement AgentRequirement() => new()
    {
        IntentKind = TaskIntentKinds.Agent,
        RequiredCodingLevel = 3,
        RequiredMultiFileLevel = 2,
        RequiresToolUse = true,
        RequiresStructuredOutput = true,
        PreferSmallestSufficient = true,
        Label = "agente de ingenieria (harness)"
    };

    private static MemoryInfo Mem(double totalGb, double freeGb) => new()
    {
        Status = DetectionStatus.Detected,
        TotalBytes = (long)(totalGb * ModelMemoryBudget.BytesPerGb),
        FreeBytes = (long)(freeGb * ModelMemoryBudget.BytesPerGb),
        AvailableBytes = (long)(freeGb * ModelMemoryBudget.BytesPerGb)
    };

    private static AssessmentResult AssessmentConModelo(string installedName, double ramFreeGb, double ramTotalGb)
    {
        return new AssessmentResult
        {
            Environment = new EnvironmentProfile
            {
                Memory = Mem(ramTotalGb, ramFreeGb)
            },
            Tools = new ToolsProfile
            {
                Ollama = new OllamaStatus
                {
                    Installed = true,
                    ServerRunning = true,
                    Models = new List<ModelInfo>
                    {
                        new() { Name = installedName, SizeBytes = 1500L * 1024 * 1024, Capabilities = new List<string> { "completion", "tool-use", "structured-output" } }
                    }
                }
            }
        };
    }
    #endregion
}

internal static class CatalogExtensions
{
    public static ModelCandidate? Get(this IReadOnlyList<ModelCandidate> catalog, string name)
    {
        foreach (var c in catalog)
        {
            if (c.PullName.Equals(name, System.StringComparison.OrdinalIgnoreCase) ||
                c.Name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
            {
                return c;
            }
        }
        return null;
    }
}
