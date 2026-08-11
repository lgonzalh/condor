using Condor.Core.Contracts;
using Condor.Core.Evaluation;
using Condor.Core.Models;
using Condor.Infrastructure.Detection;
using Condor.Infrastructure.Project;

namespace Condor.Infrastructure;

public class AssessmentService : IAssessmentService
{
    public async Task<AssessmentResult> ExecuteAsync(
        AssessmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var environment = new EnvironmentProfile
        {
            Os = await RunDetectorAsync(
                () => new OsDetector().DetectAsync(cancellationToken),
                () => new OperatingSystemInfo
                {
                    Status = DetectionStatus.Error,
                    Reason = "No fue posible detectar el sistema operativo"
                }),
            Cpu = await RunDetectorAsync(
                () => new CpuDetector().DetectAsync(cancellationToken),
                () => new ProcessorInfo
                {
                    Status = DetectionStatus.Error,
                    Reason = "No fue posible detectar la CPU"
                }),
            Memory = await RunDetectorAsync(
                () => new MemoryDetector().DetectAsync(cancellationToken),
                () => new MemoryInfo
                {
                    Status = DetectionStatus.Error,
                    Reason = "No fue posible detectar la memoria"
                })
        };

        var gpu = await RunDetectorAsync(
            () => new GpuDetector().DetectAsync(cancellationToken),
            () => new GpuDetectionResult
            {
                Status = DetectionStatus.Error,
                Reason = "No fue posible detectar la GPU"
            });

        environment.GpuList = gpu.Gpus;
        environment.GpuStatus = gpu.Status;
        environment.GpuReason = gpu.Reason;

        var storage = await RunDetectorAsync(
            () => new StorageDetector().DetectAsync(cancellationToken),
            () => new StorageDetectionResult
            {
                Status = DetectionStatus.Error,
                Reason = "No fue posible detectar el almacenamiento"
            });

        environment.StorageList = storage.Disks;
        environment.StorageStatus = storage.Status;
        environment.StorageReason = storage.Reason;

        var toolDetector = new ToolDetector();
        var tools = new ToolsProfile
        {
            DetectedTools = toolDetector.DetectAll(),
            Git = await RunDetectorAsync(
                () => new GitDetector().DetectAsync(cancellationToken),
                () => new ToolInfo
                {
                    Name = "git",
                    Status = DetectionStatus.Error,
                    Reason = "No fue posible verificar git"
                }),
            Ollama = await RunDetectorAsync(
                () => new OllamaDetector().DetectAsync(cancellationToken),
                () => new OllamaStatus { Note = "No fue posible verificar Ollama" })
        };

        var project = await RunDetectorAsync<ProjectProfile?>(
            async () => await new ProjectDetector().DiscoverAsync(request.WorkingDirectory, tools.Git, cancellationToken),
            () => null);

        return new AssessmentResult
        {
            SchemaVersion = "1.0.0",
            GeneratedAtUtc = DateTime.UtcNow,
            WorkingDirectory = request.WorkingDirectory,
            Environment = environment,
            Tools = tools,
            Capabilities = CapabilityEvaluator.Evaluate(environment, tools),
            Project = project
        };
    }

    private static async Task<T> RunDetectorAsync<T>(
        Func<Task<T>> detector,
        Func<T> fallback)
    {
        try
        {
            return await detector();
        }
        catch
        {
            return fallback();
        }
    }
}