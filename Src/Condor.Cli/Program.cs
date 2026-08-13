using Condor.Cli.Commands;
using Condor.Cli.Presentation;
using Condor.Core.Contracts;
using Condor.Infrastructure;
using Condor.Infrastructure.Context;
using Condor.Infrastructure.Llm;
using Condor.Infrastructure.Planning;
using Condor.Infrastructure.State;

namespace Condor.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        IAssessmentService assessmentService = new AssessmentService();
        IStateStore stateStore = new LocalStateStore();
        ILlmClient llmClient = new OllamaClient();

        if (args.Length == 0)
        {
            RenderInitialState();
            return 0;
        }

        var command = args[0].ToLowerInvariant();

        switch (command)
        {
            case "ayuda":
            case "--help":
            case "-h":
                RenderHelp();
                return 0;

            case "version":
            case "--version":
            case "-v":
                Console.WriteLine(VersionInfo.Product + " " + VersionInfo.Version);
                return 0;

            case "analizar":
                return await AssessCommand.ExecuteAsync(
                    assessmentService,
                    stateStore,
                    args.Skip(1).ToArray(),
                    CancellationToken.None);

            case "consultar":
                return await AskCommand.ExecuteAsync(
                    llmClient,
                    stateStore,
                    args.Skip(1).ToArray(),
                    CancellationToken.None);

            case "recomendar":
                return await RecommendCommand.ExecuteAsync(
                    stateStore,
                    args.Skip(1).ToArray(),
                    CancellationToken.None);

            case "contexto":
                return await ContextCommand.ExecuteAsync(
                    new ContextService(stateStore),
                    stateStore,
                    args.Skip(1).ToArray(),
                    CancellationToken.None);

            case "planear":
                return await PlanCommand.ExecuteAsync(
                    new PlanService(stateStore),
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
        Terminal.WriteDim("Usa 'condor analizar' para analizar el entorno.");
        Terminal.WriteDim("Usa 'condor contexto' para reconstruir el contexto del proyecto.");
        Terminal.WriteDim("Usa 'condor planear \"<solicitud>\"' para generar un plan de trabajo.");
        Terminal.WriteDim("Usa 'condor recomendar' para elegir un modelo local.");
        Terminal.WriteDim("Usa 'condor consultar' para consultar al modelo local.");
        Terminal.WriteDim("Usa 'condor ayuda' para ver los comandos disponibles.");
    }

    private static void RenderHelp()
    {
        Terminal.WriteLine();
        Terminal.WriteInfo("C O N D O R");
        Terminal.WriteDim(VersionInfo.Tagline);
        Terminal.WriteLine();
        Terminal.WriteLine("Uso:");
        Terminal.WriteLine("  condor                     Muestra el estado inicial.");
        Terminal.WriteLine("  condor analizar            Analiza el entorno y muestra el resumen.");
        Terminal.WriteLine("  condor analizar --json     Genera el resultado en formato JSON.");
        Terminal.WriteLine("  condor contexto            Reconstruye el contexto del proyecto.");
        Terminal.WriteLine("  condor contexto --json     Genera el contexto en formato JSON.");
        Terminal.WriteLine("  condor planear \"<solicitud>\" Genera un plan de trabajo.");
        Terminal.WriteLine("  condor planear \"<solicitud>\" --json");
        Terminal.WriteLine("                             Genera el plan en formato JSON.");
        Terminal.WriteLine("  condor recomendar          Recomienda un modelo para el equipo.");
        Terminal.WriteLine("  condor recomendar --proposito <tipo>");
        Terminal.WriteLine("                             tipo: desarrollo, general o vision.");
        Terminal.WriteLine("  condor consultar \"<mensaje>\"  Consulta al modelo local.");
        Terminal.WriteLine("  condor consultar \"<mensaje>\" --modelo <modelo>");
        Terminal.WriteLine("                             Consulta usando un modelo especifico.");
        Terminal.WriteLine("  condor version             Muestra la version.");
        Terminal.WriteLine("  condor ayuda               Muestra esta ayuda.");
        Terminal.WriteLine();
        Terminal.WriteLine("Alias:");
        Terminal.WriteLine("  condor -h, --help          Muestra esta ayuda.");
        Terminal.WriteLine("  condor -v, --version       Muestra la version.");
    }
}