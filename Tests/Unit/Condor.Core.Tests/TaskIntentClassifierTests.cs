using Condor.Core.Evaluation;
using Condor.Core.Models;

namespace Condor.Core.Tests;

public sealed class TaskIntentClassifierTests
{
    [Fact]
    public void Saludo_EsConsultaDirectaSinProtocoloDeHerramientas()
    {
        var requirement = TaskIntentClassifier.Classify("hola");

        Assert.Equal(TaskIntentKinds.Consult, requirement.IntentKind);
        Assert.False(requirement.RequiresToolUse);
        Assert.False(requirement.RequiresStructuredOutput);
        Assert.True(requirement.PreferSmallestSufficient);
    }

    [Fact]
    public void CambioDeCodigo_MantieneRequisitosDeAgente()
    {
        var requirement = TaskIntentClassifier.Classify("corrige este archivo y compila");

        Assert.Equal(TaskIntentKinds.Agent, requirement.IntentKind);
        Assert.True(requirement.RequiresToolUse);
        Assert.True(requirement.RequiresStructuredOutput);
        Assert.True(requirement.RequiredCodingLevel >= 3);
        Assert.False(requirement.PreferSmallestSufficient);
    }

    [Fact]
    public void PaginaWebEstaticaEnUnArchivo_NoExigeCapacidadMultifile()
    {
        var requirement = TaskIntentClassifier.Classify(
            "Crea una pagina web estatica y sencilla en index.html dentro del mismo proyecto.");

        Assert.Equal(TaskIntentKinds.Agent, requirement.IntentKind);
        Assert.Equal(2, requirement.RequiredCodingLevel);
        Assert.Equal(0, requirement.RequiredMultiFileLevel);
        Assert.True(requirement.RequiresToolUse);
        Assert.True(requirement.RequiresStructuredOutput);
        Assert.False(requirement.PreferSmallestSufficient);
    }

    [Fact]
    public void AnalisisDelProyectoCompleto_MantieneRequisitoMultiFile()
    {
        var requirement = TaskIntentClassifier.Classify("analiza el proyecto completo");

        Assert.Equal(2, requirement.RequiredMultiFileLevel);
        Assert.True(requirement.RequiredCodingLevel >= 2);
    }

    [Fact]
    public void AnalizaQueContieneEsteProyecto_MantieneRequisitoMultiFile()
    {
        var requirement = TaskIntentClassifier.Classify("analiza que contiene este proyecto");

        Assert.Equal(2, requirement.RequiredMultiFileLevel);
        Assert.True(TaskIntentClassifier.IsInformational("analiza que contiene este proyecto"));
    }

    [Theory]
    [InlineData("Actualiza el contenido existente de index.html para que sea adaptable.")]
    [InlineData("Actualizar la pagina web y conservar la documentacion existente.")]
    public void SolicitudDeActualizacionQueMencionaContenido_NoEsInformativa(string intention)
    {
        var requirement = TaskIntentClassifier.Classify(intention);

        Assert.Equal(TaskIntentKinds.Agent, requirement.IntentKind);
        Assert.False(TaskIntentClassifier.IsInformational(intention));
    }

    [Fact]
    public void SolicitudRealDePaginaHtmlConInformeAutomatico_NoEsInformativa()
    {
        const string intention = "Actualiza la pagina existente en la ruta exacta index.html (raiz del proyecto, no docs/index.html) para que sea estatica y adaptable a movil y escritorio. Primero lee index.html y luego actualiza esa misma ruta usando edit_file con el contenido completo. Conserva el enlace relativo exacto docs/tareas/indice-condor.html. Incluye doctype HTML, lang es, meta charset UTF-8, meta viewport y CSS en la pagina con contenedor centrado, ancho maximo, espaciado fluido, colores legibles y una regla @media para pantallas de hasta 600px. Sin dependencias ni recursos externos. No alteres docs/tareas; Condor guardara su informe automatico alli.";

        Assert.False(TaskIntentClassifier.IsInformational(intention));
    }
}
