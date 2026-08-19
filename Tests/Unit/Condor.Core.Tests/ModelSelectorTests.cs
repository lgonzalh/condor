using System.Collections.Generic;
using Condor.Core.Catalog;
using Condor.Core.Evaluation;
using Condor.Core.Models;
using Condor.Core.Selection;

namespace Condor.Core.Tests;

public class ModelSelectorTests
{
    [Fact]
    public void Recommend_SinAssessment_NoSelecciona()
    {
        var r = ModelSelector.RecommendFromCatalog(null, ModelCatalog.Default);

        Assert.Null(r.Desired);
        Assert.Contains(r.Limitations, l => l.Contains("Assessment"));
    }

    [Fact]
    public void Recommend_ModeloDeseadoInstalado_ReutilizaSinDescargar()
    {
        var assessment = AssessmentConModelo("qwen2.5-coder:7b", ramFreeGb: 10, ramTotalGb: 16);

        var r = ModelSelector.RecommendFromCatalog(assessment, ModelCatalog.Default);

        Assert.NotNull(r.Desired);
        Assert.True(r.AlreadyInstalled);
        Assert.Equal("qwen2.5-coder:7b", r.InstalledName);
        Assert.Contains("reutiliza", r.Reason, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Recommend_ModeloNoInstalado_RequiereObtencion()
    {
        var assessment = AssessmentConModelo("otro-modelo", ramFreeGb: 10, ramTotalGb: 16);

        var r = ModelSelector.RecommendFromCatalog(assessment, ModelCatalog.Default);

        Assert.NotNull(r.Desired);
        Assert.False(r.AlreadyInstalled);
    }

    [Fact]
    public void Recommend_AlternativaMenosCapazNoPisaAlDeseadoMasCapaz()
    {
        // La alternativa instalada (llama3.2:3b, general) es MENOS capaz en
        // ingenieria que el deseado de mayor capacidad viable. No debe
        // reutilizarse la menos capaz si el deseado es viable y obtenible.
        var assessment = AssessmentConModelo("llama3.2:3b", ramFreeGb: 10, ramTotalGb: 16);

        var r = ModelSelector.RecommendFromCatalog(assessment, ModelCatalog.Default);

        Assert.NotNull(r.Desired);
        Assert.False(r.AlreadyInstalled);
        Assert.Equal("qwen2.5-coder:7b", r.Desired.PullName);
    }

    [Fact]
    public void Recommend_RamaInsuficiente_NoSeleccionaModeloGrande()
    {
        // Solo 1 GB libre -> ningun modelo del catalogo cabe (bloqueo por recursos).
        var assessment = AssessmentConModelo("vacio", ramFreeGb: 1, ramTotalGb: 2);

        var r = ModelSelector.RecommendFromCatalog(assessment, ModelCatalog.Default);

        Assert.Null(r.Desired);
        Assert.True(r.BlockedByResources);
        Assert.Contains(r.Limitations, l => l.Contains("no se intenta cargar", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Recommend_Determinista_MismaEntradaMismoResultado()
    {
        var a = AssessmentConModelo("llama3.2:3b", ramFreeGb: 8, ramTotalGb: 16);

        var r1 = ModelSelector.RecommendFromCatalog(a, ModelCatalog.Default);
        var r2 = ModelSelector.RecommendFromCatalog(a, ModelCatalog.Default);

        Assert.Equal(r1.Desired?.PullName, r2.Desired?.PullName);
        Assert.Equal(r1.AlreadyInstalled, r2.AlreadyInstalled);
    }

    [Fact]
    public void Recommend_ModeloAjustadoConMargenSuficiente_MuestraAdvertencia()
    {
        // 16 GB totales, 10 libres: el 7B (pico ~5.23, 32.7% de la RAM) esta en
        // estado Ajustado PERO el presupuesto seguro (headroom 5.5) lo admite.
        // Se permite con advertencia explicita.
        var assessment = AssessmentConModelo("sin-instalar", ramFreeGb: 10, ramTotalGb: 16);

        var r = ModelSelector.RecommendFromCatalog(assessment, ModelCatalog.Default);

        Assert.NotNull(r.Desired);
        Assert.Equal("qwen2.5-coder:7b", r.Desired.PullName);
        Assert.Equal(ResourcePressure.Adjusted, r.Resources?.Pressure);
        Assert.Contains(r.Limitations, l => l.Contains("Ajustado", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Recommend_ModeloEnPresion_RecomiendaDegradarYCerrarConsumidores()
    {
        // 14 GB totales, 10 libres: el 7B (pico ~5.23, ~37% de la RAM) cae en
        // estado Presion y aun cumple el presupuesto seguro. La carga se degrada
        // y se sugiere cerrar procesos de alto consumo (Condor no los cierra).
        var assessment = AssessmentConModelo("sin-instalar", ramFreeGb: 10, ramTotalGb: 14);

        var r = ModelSelector.RecommendFromCatalog(assessment, ModelCatalog.Default);

        Assert.NotNull(r.Desired);
        Assert.Equal("qwen2.5-coder:7b", r.Desired.PullName);
        Assert.Equal(ResourcePressure.Pressure, r.Resources?.Pressure);
        Assert.Contains(r.Limitations, l => l.Contains("Presion", System.StringComparison.OrdinalIgnoreCase));
        Assert.Contains(r.Limitations, l => l.Contains("degradara", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Recommend_ModeloMenorViable_Normal()
    {
        // El 7B no cabe -> se elige el 3B (13.5% de la RAM) en estado Normal.
        var assessment = AssessmentConModelo("sin-instalar", ramFreeGb: 7, ramTotalGb: 16);

        var r = ModelSelector.RecommendFromCatalog(assessment, ModelCatalog.Default);

        Assert.Equal("qwen2.5-coder:3b", r.Desired?.PullName);
        Assert.Equal(ResourcePressure.Normal, r.Resources?.Pressure);
    }

    [Fact]
    public void Recommend_Insuficiente_BloqueaYSinReintentos()
    {
        // Nada cabe: estado Insuficiente, no se selecciona y el mensaje deja
        // claro que no se reintenta en bucle.
        var assessment = AssessmentConModelo("sin-instalar", ramFreeGb: 1.0, ramTotalGb: 16);

        var r = ModelSelector.RecommendFromCatalog(assessment, ModelCatalog.Default);

        Assert.Null(r.Desired);
        Assert.Equal(ResourcePressure.Insufficient, r.Resources?.Pressure);
        Assert.Contains(r.Limitations, l => l.Contains("no se intenta cargar", System.StringComparison.OrdinalIgnoreCase));
        Assert.Contains(r.Limitations, l => l.Contains("presupuesto seguro", System.StringComparison.OrdinalIgnoreCase));
    }

    private static AssessmentResult AssessmentConModelo(string installedName, double ramFreeGb, double ramTotalGb)
    {
        return new AssessmentResult
        {
            Environment = new EnvironmentProfile
            {
                Memory = new MemoryInfo
                {
                    Status = DetectionStatus.Detected,
                    TotalBytes = (long)(ramTotalGb * 1024 * 1024 * 1024),
                    FreeBytes = (long)(ramFreeGb * 1024 * 1024 * 1024)
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
                        new() { Name = installedName, SizeBytes = 1024 * 1024 * 1024, Capabilities = new List<string> { "completion" } }
                    }
                }
            }
        };
    }

    [Fact]
    public void Recommend_CuandoSieteBNoCabe_SeleccionaElMenorViable()
    {
        // Con 7 GB libres de 16, el 7B (pico ~5.23) NO cumple el presupuesto
        // seguro (headroom 2.5) aunque su porcentaje (32.7%) sea Ajustado.
        // Se descarta y Condor elige automaticamente el menor viable: el 3B.
        var assessment = AssessmentConModelo("sin-instalar", ramFreeGb: 7, ramTotalGb: 16);

        var r = ModelSelector.RecommendFromCatalog(assessment, ModelCatalog.Default);

        Assert.NotNull(r.Desired);
        Assert.Equal("qwen2.5-coder:3b", r.Desired.PullName);
        Assert.False(r.BlockedByResources);
    }

    [Fact]
    public void Recommend_RamLibreBaja_PrefiereElMenorViableDentroDelPresupuesto()
    {
        // Caso concreto: el 7B no cabe -> seleccionar el modelo menor viable.
        var assessment = AssessmentConModelo("sin-instalar", ramFreeGb: 7.3, ramTotalGb: 16);

        var r = ModelSelector.RecommendFromCatalog(assessment, ModelCatalog.Default);

        Assert.NotNull(r.Desired);
        Assert.Equal("qwen2.5-coder:3b", r.Desired.PullName);
        Assert.False(r.BlockedByResources);
    }

    [Fact]
    public void Recommend_RamaInsuficienteParaTodo_MarcaBloqueoPorRecursos()
    {
        // Margen de RAM casi nulo: ningun modelo cabe de forma segura.
        var assessment = AssessmentConModelo("sin-instalar", ramFreeGb: 1.0, ramTotalGb: 16);

        var r = ModelSelector.RecommendFromCatalog(assessment, ModelCatalog.Default);

        Assert.Null(r.Desired);
        Assert.True(r.BlockedByResources);
        Assert.NotNull(r.Resources);
        Assert.Equal(ResourcePressure.Insufficient, r.Resources.Pressure);
        Assert.Contains(r.Limitations, l => l.Contains("no se intenta cargar", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Recommend_InstaladosNoCabenPeroAlternativaMenor_delCatalogoSiCabe()
    {
        // CASO C (promesa): los modelos instalados (3B/7B) no caben por RAM, pero
        // el catalogo de Condor contiene una alternativa menor (qwen2.5-coder:1.5b)
        // que SI cabe. El selector debe devolverla como deseada (aunque no este
        // instalada) para que el auto-setup la descargue y la use.
        // El inventario de Ollama no debe ser el limite del universo de modelos.
        var assessment = AssessmentConModelo("qwen2.5-coder:3b", ramFreeGb: 6.0, ramTotalGb: 16);

        var r = ModelSelector.RecommendFromCatalog(assessment, ModelCatalog.Default);

        Assert.NotNull(r.Desired);
        Assert.Equal("qwen2.5-coder:1.5b", r.Desired.PullName);
        Assert.False(r.AlreadyInstalled); // se obtendra (descarga) por el auto-setup
        Assert.False(r.BlockedByResources);
        // El 3B instalado no cabe, pero NO se abandona: Condor eligio la alternativa
        // menor viable del catalogo (1.5B) para descargar y usar.
    }

    [Fact]
    public void Recommend_MasRamaBaja_CaeAlModeloAunMenor()
    {
        // Con RAM menos holgada ni el 1.5B cabe, pero el 0.5B (ultimo recurso)
        // aun si. Condor debe seguir bajando dentro del catalogo respetando el
        // presupuesto, sin detenerse tras descartar los instalados.
        var assessment = AssessmentConModelo("qwen2.5-coder:3b", ramFreeGb: 5.1, ramTotalGb: 16);

        var r = ModelSelector.RecommendFromCatalog(assessment, ModelCatalog.Default);

        Assert.NotNull(r.Desired);
        Assert.Equal("qwen2.5-coder:0.5b", r.Desired.PullName);
        Assert.False(r.BlockedByResources);
    }

    [Fact]
    public void Recommend_RamaExtrema_SoloCuandoElCatalogoSeAgotaBloquea()
    {
        // F: solo se bloquea cuando de verdad NO existe ninguna alternativa viable
        // en el catalogo, no por el mero hecho de que los instalados no quepan.
        // Con RAM tan baja que ni el 0.5B cabe (headroom < pico 0.5B) -> bloqueo.
        var assessment = AssessmentConModelo("qwen2.5-coder:3b", ramFreeGb: 3.0, ramTotalGb: 16);

        var r = ModelSelector.RecommendFromCatalog(assessment, ModelCatalog.Default);

        Assert.Null(r.Desired);
        Assert.True(r.BlockedByResources);
    }
}
