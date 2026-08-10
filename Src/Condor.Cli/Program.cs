using Condor.Cli.Commands;
using Condor.Cli.Presentation;
using Condor.Core.Contracts;
using Condor.Infrastructure;
using Condor.Infrastructure.State;

namespace Condor.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        IAssessmentService assessmentService = new AssessmentService();
        IStateStore stateStore = new LocalStateStore();

        if (args.Length == 0)
        {
            RenderInitialState();
            return 0;
        }

        var command = args[0].ToLowerInvariant();

        switch (command)
        {
            case "help":
            case "--help":
            case "-h":
                RenderHelp();
                return 0;

            case "version":
            case "--version":
            case "-v":
                Console.WriteLine(VersionInfo.Product + " " + VersionInfo.Version);
                return 0;

            case "assess":
                return await AssessCommand.ExecuteAsync(
                    assessmentService,
                    stateStore,
                    args.Skip(1).ToArray(),
                    CancellationToken.None);

            default:
                Terminal.WriteError("Comando desconocido: " + args[0]);
                RenderHelp();
                return 1;
        }
    }

    private static void RenderInitialState()
    {
        Terminal.WriteLine();
        Terminal.WriteInfo("C O N D O R");
        Terminal.WriteDim(VersionInfo.Tagline);
        Terminal.WriteLine();
        Terminal.WriteLine("Que quieres construir?");
        Terminal.WriteLine();
        Terminal.WriteDim("Usa 'condor assess' para analizar el entorno.");
        Terminal.WriteDim("Usa 'condor help' para ver los comandos disponibles.");
    }

    private static void RenderHelp()
    {
        Terminal.WriteLine();
        Terminal.WriteInfo("C O N D O R");
        Terminal.WriteDim(VersionInfo.Tagline);
        Terminal.WriteLine();
        Terminal.WriteLine("Uso:");
        Terminal.WriteLine("  condor                     Muestra el estado inicial.");
        Terminal.WriteLine("  condor assess              Analiza el entorno y muestra el resumen.");
        Terminal.WriteLine("  condor assess --json       Genera el resultado en formato JSON.");
        Terminal.WriteLine("  condor version             Muestra la version.");
        Terminal.WriteLine("  condor help                Muestra esta ayuda.");
    }
}
