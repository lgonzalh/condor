using System;
using System.Collections.Generic;
using Condor.Core.Models;
using Condor.Core.Serialization;
using Condor.Core.Verification;

namespace Condor.Core.Tests;

public class VerifierTests
{
    [Fact]
    public void Verifica_SinBuild_DevuelveNotDetectedConMotivoInstructivo()
    {
        var result = Verifier.Verify(null, "C:\\proyecto", new Dictionary<string, ProbedFile?>(), VerificationLimits.Default);

        Assert.Equal(DetectionStatus.NotDetected, result.Status);
        Assert.Contains("condor construir", result.Reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Verifica_BuildNotDetected_DegradaANotDetected()
    {
        var build = BuildConEstado(DetectionStatus.NotDetected, AccionAplicada("B0", "A.cs"));

        var result = Verifier.Verify(build, "C:\\proyecto", new Dictionary<string, ProbedFile?>(), VerificationLimits.Default);

        Assert.Equal(DetectionStatus.NotDetected, result.Status);
    }

    [Fact]
    public void Verifica_BuildLimited_DegradaALimited()
    {
        var build = BuildConEstado(DetectionStatus.Limited, AccionAplicada("B0", "A.cs"));

        var result = Verifier.Verify(build, "C:\\proyecto", new Dictionary<string, ProbedFile?>(), VerificationLimits.Default);

        Assert.Equal(DetectionStatus.Limited, result.Status);
    }

    [Fact]
    public void Verifica_BuildSinAcciones_DegradaALimited()
    {
        var build = BuildConEstado(DetectionStatus.Detected);

        var result = Verifier.Verify(build, "C:\\proyecto", new Dictionary<string, ProbedFile?>(), VerificationLimits.Default);

        Assert.Equal(DetectionStatus.Limited, result.Status);
    }

    [Fact]
    public void Verifica_SinWorkingDirectory_DegradaALimited()
    {
        var build = BuildConEstado(DetectionStatus.Detected, AccionAplicada("B0", "A.cs"));

        var result = Verifier.Verify(build, "", new Dictionary<string, ProbedFile?>(), VerificationLimits.Default);

        Assert.Equal(DetectionStatus.Limited, result.Status);
    }

    [Fact]
    public void Verifica_AccionAplicada_ArchivoExisteYContenidoCoincide_Pasa()
    {
        var build = BuildConEstado(DetectionStatus.Detected, AccionAplicada("B0", "Models/Perfil.cs"));
        var probed = new Dictionary<string, ProbedFile?>
        {
            ["Models/Perfil.cs"] = new() { Content = "crear modelo" }
        };

        var result = Verifier.Verify(build, "C:\\proyecto", probed, VerificationLimits.Default);

        Assert.Equal(DetectionStatus.Detected, result.Status);
        Assert.Equal(1, result.Passed);
        Assert.Equal(0, result.Failed);
        Assert.Equal(VerificationCheck.StatusPassed, result.Checks[0].Status);
    }

    [Fact]
    public void Verifica_AccionAplicada_ArchivoInexistente_Falla()
    {
        var build = BuildConEstado(DetectionStatus.Detected, AccionAplicada("B0", "NoExiste.cs"));
        var probed = new Dictionary<string, ProbedFile?>();

        var result = Verifier.Verify(build, "C:\\proyecto", probed, VerificationLimits.Default);

        Assert.Equal(1, result.Failed);
        Assert.Equal(VerificationCheck.StatusFailed, result.Checks[0].Status);
    }

    [Fact]
    public void Verifica_AccionAplicada_ContenidoDistinto_Falla()
    {
        var build = BuildConEstado(DetectionStatus.Detected, AccionAplicada("B0", "A.cs"));
        var probed = new Dictionary<string, ProbedFile?>
        {
            ["A.cs"] = new() { Content = "contenido diferente" }
        };

        var result = Verifier.Verify(build, "C:\\proyecto", probed, VerificationLimits.Default);

        Assert.Equal(1, result.Failed);
        Assert.Equal(VerificationCheck.StatusFailed, result.Checks[0].Status);
    }

    [Fact]
    public void Verifica_AccionOmitida_RegistroInformativo()
    {
        var build = BuildConEstado(DetectionStatus.Detected, AccionOmitida("B0", "A.cs"));

        var result = Verifier.Verify(build, "C:\\proyecto", new Dictionary<string, ProbedFile?>(), VerificationLimits.Default);

        Assert.Equal(1, result.Informative);
        Assert.Equal(0, result.Failed);
        Assert.Equal(VerificationCheck.StatusInformative, result.Checks[0].Status);
    }

    [Fact]
    public void Verifica_AccionFallida_RegistroInformativo()
    {
        var build = BuildConEstado(DetectionStatus.Detected, AccionFallida("B0", "A.cs"));

        var result = Verifier.Verify(build, "C:\\proyecto", new Dictionary<string, ProbedFile?>(), VerificationLimits.Default);

        Assert.Equal(1, result.Informative);
        Assert.Equal(0, result.Failed);
    }

    [Theory]
    [InlineData("../fuera.cs")]
    [InlineData("/absoluta.cs")]
    [InlineData("C:\\windows\\x.cs")]
    public void Verifica_RutaFueraDelObjetivo_CheckFallida(string path)
    {
        var build = BuildConEstado(DetectionStatus.Detected, AccionAplicada("B0", path));

        var result = Verifier.Verify(build, "C:\\proyecto", new Dictionary<string, ProbedFile?>(), VerificationLimits.Default);

        Assert.Equal(VerificationCheck.StatusFailed, result.Checks[0].Status);
    }

    [Fact]
    public void Verifica_LimiteDeChecks_RespetaMaxChecks()
    {
        var actions = new List<BuildAction>();
        for (var i = 0; i < 40; i++)
        {
            actions.Add(AccionOmitida("B" + i, "src/A" + i + ".cs"));
        }

        var build = BuildConEstado(DetectionStatus.Detected, actions.ToArray());

        var result = Verifier.Verify(build, "C:\\proyecto", new Dictionary<string, ProbedFile?>(), VerificationLimits.Default);

        Assert.True(result.Checks.Count <= VerificationLimits.Default.MaxChecks);
        Assert.Contains(VerificationLimits.LimitChecks, result.LimitsApplied);
    }

    [Fact]
    public void Determinismo_DosVerificaciones_ProducenElMismoResultado()
    {
        var build = BuildConEstado(DetectionStatus.Detected, AccionAplicada("B0", "Models/Perfil.cs"));
        var probed = new Dictionary<string, ProbedFile?>
        {
            ["Models/Perfil.cs"] = new() { Content = "crear modelo" }
        };

        var first = Verifier.Verify(build, "C:\\proyecto", probed, VerificationLimits.Default);
        first.GeneratedAtUtc = DateTime.MinValue;

        var second = Verifier.Verify(build, "C:\\proyecto", probed, VerificationLimits.Default);
        second.GeneratedAtUtc = DateTime.MinValue;

        Assert.Equal(VerificationJson.Serialize(first), VerificationJson.Serialize(second));
    }

    private static BuildResult BuildConEstado(DetectionStatus status, params BuildAction[] actions)
    {
        return new BuildResult
        {
            SchemaVersion = "1.0.0",
            Status = status,
            WorkingDirectory = "C:\\proyecto",
            RootName = "condor",
            Objective = "Verificar cambios",
            Actions = new List<BuildAction>(actions),
            Applied = 1,
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    private static BuildAction AccionAplicada(string id, string path)
    {
        return new BuildAction
        {
            Id = id,
            Kind = BuildActionKind.Crear,
            RelativePath = path,
            Content = "crear modelo",
            Status = BuildAction.StatusApplied,
            Evidence = "e2e"
        };
    }

    private static BuildAction AccionOmitida(string id, string path)
    {
        return new BuildAction
        {
            Id = id,
            Kind = BuildActionKind.Crear,
            RelativePath = path,
            Content = "x",
            Status = BuildAction.StatusOmitted,
            StatusReason = "El archivo ya existe",
            Evidence = "e2e"
        };
    }

    private static BuildAction AccionFallida(string id, string path)
    {
        return new BuildAction
        {
            Id = id,
            Kind = BuildActionKind.Actualizar,
            RelativePath = path,
            Content = "x",
            Status = BuildAction.StatusFailed,
            StatusReason = "No fue posible escribir",
            Evidence = "e2e"
        };
    }
}
