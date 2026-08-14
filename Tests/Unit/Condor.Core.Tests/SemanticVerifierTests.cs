using System.Collections.Generic;
using Condor.Core.Models;
using Condor.Core.Semantic;

namespace Condor.Core.Tests;

public class SemanticVerifierTests
{
    [Fact]
    public void IsDotNetAvailable_ConDotnetDetectado_Verdadero()
    {
        var a = new AssessmentResult
        {
            Tools = new ToolsProfile
            {
                DetectedTools = new List<ToolInfo>
                {
                    new() { Name = "dotnet", Status = DetectionStatus.Detected }
                }
            }
        };

        Assert.True(SemanticVerifier.IsDotNetAvailable(a));
    }

    [Fact]
    public void IsDotNetAvailable_SinDotnet_Falso()
    {
        var a = new AssessmentResult { Tools = new ToolsProfile { DetectedTools = new List<ToolInfo>() } };

        Assert.False(SemanticVerifier.IsDotNetAvailable(a));
    }

    [Fact]
    public void ResolveManifest_PrefiereSln()
    {
        var result = SemanticVerifier.ResolveDotNetManifest(new List<string>
        {
            "C:\\proyecto\\App.csproj",
            "C:\\proyecto\\MiApp.sln"
        });

        Assert.Equal("C:\\proyecto\\MiApp.sln", result);
    }

    [Fact]
    public void ResolveManifest_SinSln_UsaCsproj()
    {
        var result = SemanticVerifier.ResolveDotNetManifest(new List<string>
        {
            "B.csproj",
            "A.csproj"
        });

        Assert.Equal("A.csproj", result);
    }

    [Fact]
    public void ResolveManifest_Vacio_Null()
    {
        Assert.Null(SemanticVerifier.ResolveDotNetManifest(new List<string>()));
    }

    [Fact]
    public void BuildArguments_Compilar_IncluyeNoRestore()
    {
        var args = SemanticVerifier.BuildArguments("App.csproj", SemanticCheck.KindCompile);

        Assert.Contains("--no-restore", args);
        Assert.Contains("App.csproj", args);
    }

    [Fact]
    public void Classify_ExitCodeCero_Correcta()
    {
        Assert.Equal(SemanticCheck.StatusCorrect, SemanticVerifier.Classify(
            SemanticCheck.KindCompile, 0, false, false, true, false));
    }

    [Fact]
    public void Classify_ExitCodeNoCero_Fallida()
    {
        Assert.Equal(SemanticCheck.StatusFailed, SemanticVerifier.Classify(
            SemanticCheck.KindCompile, 1, false, false, true, false));
    }

    [Fact]
    public void Classify_Timeout_Timeout()
    {
        Assert.Equal(SemanticCheck.StatusTimeout, SemanticVerifier.Classify(
            SemanticCheck.KindCompile, null, true, false, true, false));
    }

    [Fact]
    public void Classify_Cancelacion_Cancelada()
    {
        Assert.Equal(SemanticCheck.StatusCancelled, SemanticVerifier.Classify(
            SemanticCheck.KindCompile, null, false, true, true, false));
    }

    [Fact]
    public void Classify_NoEjecutable_NoEjecutable()
    {
        Assert.Equal(SemanticCheck.StatusNotExecutable, SemanticVerifier.Classify(
            SemanticCheck.KindCompile, null, false, false, false, false));
    }

    [Fact]
    public void Classify_NoRestaurado_NoDisponible()
    {
        Assert.Equal(SemanticCheck.StatusNotAvailable, SemanticVerifier.Classify(
            SemanticCheck.KindCompile, 1, false, false, true, true));
    }

    [Fact]
    public void Truncate_SuperaLimite_Trunca()
    {
        var value = new string('x', 100);
        Assert.Equal(50, SemanticVerifier.Truncate(value, 50).Length);
    }

    [Fact]
    public void Determinismo_ClasificacionEstable()
    {
        var a = SemanticVerifier.Classify(SemanticCheck.KindCompile, 0, false, false, true, false);
        var b = SemanticVerifier.Classify(SemanticCheck.KindCompile, 0, false, false, true, false);

        Assert.Equal(a, b);
    }
}
