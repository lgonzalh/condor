using Condor.Cli.Commands;
using Condor.Cli.Presentation;
using Condor.Cli.Routing;
using Condor.Core.Contracts;
using Condor.Infrastructure;
using Condor.Infrastructure.Agent;
using Condor.Infrastructure.Context;
using Condor.Infrastructure.Building;
using Condor.Infrastructure.Cycle;
using Condor.Infrastructure.Llm;
using Condor.Infrastructure.Planning;
using Condor.Infrastructure.State;
using Condor.Infrastructure.Verification;
using Condor.Infrastructure.Vision;
using Condor.Infrastructure.Setup;
using Condor.Infrastructure.SemanticVerification;

namespace Condor.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var assessmentService = new AssessmentService();
        var stateStore = new LocalStateStore();
        var llmClient = new OllamaClient();

        // Comandos triviales no requieren preparacion.
        if (IsVersion(args))
        {
            Console.WriteLine(VersionInfo.Product + " " + VersionInfo.Version);
            return 0;
        }

        if (IsHelp(args))
        {
            RenderHelp();
            return 0;
        }

        if (args.Length == 0)
        {
            return await RunInteractiveAsync(assessmentService, stateStore, llmClient);
        }

        // Entrada con parametros (one-shot).
        var first = string.Join(" ", args);
        var route = IntentionRouter.Route(first);

        if (route is SlashRoute slash)
        {
            if (slash.Kind != SlashCommandKind.Ayuda)
            {
                var prep = await PrepareOnceAsync(assessmentService, stateStore);
                if (prep.NeedsIntervention)
                {
                    Terminal.WriteWarning(prep.Reason ?? "Preparacion pendiente.");
                }
            }

            return await HandleSlashAsync(slash, assessmentService, stateStore, llmClient);
        }

        if (route is FreeIntentionRoute free && !string.IsNullOrWhiteSpace(free.Intention))
        {
            // Intencion natural en una sola linea: se entrega al motor agente,
            // que ya ejecuta su propia preparacion interna y actua con herramientas.
            return await AgentCommand.ExecuteAsync(
                new AgentService(stateStore, assessmentService),
                args,
                CancellationToken.None);
        }

        // Texto vacio: presentar el flujo interactivo.
        return await RunInteractiveAsync(assessmentService, stateStore, llmClient);
    }

    private static async Task<int> RunInteractiveAsync(
        IAssessmentService assessmentService,
        IStateStore stateStore,
        ILlmClient llmClient)
    {
        // Preparacion automatica: evalua hardware, RAM, almacenamiento, GPU,
        // Ollama y modelos; selecciona y prepara el modelo adecuado. Silenciosa
        // salvo informacion relevante, errores o decisiones de intervencion.
        var prep = await PrepareOnceAsync(assessmentService, stateStore);

        RenderWelcome(prep);
        Terminal.WriteLine();

        if (Console.IsInputRedirected)
        {
            // Modo no interactivo: solo se deja el entorno preparado.
            return 0;
        }

        var interpreter = new Interpreter(
            slash => HandleSlashAsync(slash, assessmentService, stateStore, llmClient),
            free => AgentCommand.ExecuteAsync(
                new AgentService(stateStore, assessmentService),
                free.Intention.Split(' ', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries),
                CancellationToken.None));

        Terminal.WriteDim("Escribe '/ayuda' para los comandos de control o '/salir' para terminar.");
        Terminal.WriteLine();

        return await interpreter.RunAsync();
    }

    private static async Task<StartupPrepResult> PrepareOnceAsync(
        IAssessmentService assessmentService,
        IStateStore stateStore)
    {
        return await new StartupPreparer(
            assessmentService,
            stateStore,
            modelAutoSetup: new ModelAutoSetupService(stateStore, assessmentService)).RunAsync();
    }

    private static async Task<int> HandleSlashAsync(
        SlashRoute route,
        IAssessmentService assessmentService,
        IStateStore stateStore,
        ILlmClient llmClient)
    {
        var args = route.Arguments;

        return route.Kind switch
        {
            SlashCommandKind.Analizar => await AssessCommand.ExecuteAsync(assessmentService, stateStore, args, CancellationToken.None),
            SlashCommandKind.Contexto => await ContextCommand.ExecuteAsync(
                new ContextService(stateStore), stateStore, args, CancellationToken.None),
            SlashCommandKind.Planear => await PlanCommand.ExecuteAsync(
                new PlanService(stateStore), stateStore, args, CancellationToken.None),
            SlashCommandKind.Construir => await BuildCommand.ExecuteAsync(
                new BuildService(stateStore), stateStore, args, CancellationToken.None),
            SlashCommandKind.Verificar => await VerifyCommand.ExecuteAsync(
                new VerificationService(stateStore), stateStore, args, CancellationToken.None),
            SlashCommandKind.Examinar => await ExamineCommand.ExecuteAsync(
                new VisionService(stateStore), stateStore, args, CancellationToken.None),
            SlashCommandKind.Recomendar => await RecommendCommand.ExecuteAsync(stateStore, args, CancellationToken.None),
            SlashCommandKind.Consultar => await AskCommand.ExecuteAsync(llmClient, stateStore, args, CancellationToken.None),
            SlashCommandKind.VerificarSemantico => await CheckCommand.ExecuteAsync(
                new SemanticVerificationService(stateStore), stateStore, args, CancellationToken.None),
            SlashCommandKind.Preparar => await PrepareCommand.ExecuteAsync(
                new SetupService(stateStore, assessmentService),
                new ModelAutoSetupService(stateStore, assessmentService),
                args,
                CancellationToken.None),
            SlashCommandKind.Avanzar => await AdvanceCommand.ExecuteAsync(
                new CycleService(
                    new PlanService(stateStore),
                    new BuildService(stateStore),
                    new VerificationService(stateStore),
                    stateStore,
                    semanticService: new SemanticVerificationService(stateStore)),
                stateStore,
                args,
                CancellationToken.None),
            SlashCommandKind.Ayuda => await RenderHelpAndReturn(),
            _ => await RenderHelpAndReturn()
        };
    }

    private static Task<int> RenderHelpAndReturn()
    {
        RenderHelp();
        return Task.FromResult(0);
    }

    private static bool IsVersion(string[] args)
    {
        return args.Length == 1 &&
               (args[0].Equals("version", StringComparison.OrdinalIgnoreCase) ||
                args[0].Equals("--version", StringComparison.OrdinalIgnoreCase) ||
                args[0].Equals("-v", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsHelp(string[] args)
    {
        return args.Length == 1 &&
               (args[0].Equals("ayuda", StringComparison.OrdinalIgnoreCase) ||
                args[0].Equals("/ayuda", StringComparison.OrdinalIgnoreCase) ||
                args[0].Equals("--help", StringComparison.OrdinalIgnoreCase) ||
                args[0].Equals("-h", StringComparison.OrdinalIgnoreCase) ||
                args[0].Equals("/help", StringComparison.OrdinalIgnoreCase));
    }

    private static void RenderWelcome(StartupPrepResult prep)
    {
        Terminal.WriteLine();
        Terminal.WriteInfo("C O N D O R");
        Terminal.WriteDim(VersionInfo.Tagline);
        Terminal.WriteLine();
        Terminal.WriteLine("Condor dejo el entorno preparado y esta listo.");
        if (!string.IsNullOrWhiteSpace(prep.Model))
        {
            Terminal.WriteDim("  Modelo local listo: " + prep.Model);
        }
        else if (!string.IsNullOrWhiteSpace(prep.Reason))
        {
            Terminal.WriteDim("  " + prep.Reason);
        }

        if (prep.NeedsIntervention && !string.IsNullOrWhiteSpace(prep.Reason))
        {
            Terminal.WriteWarning("  " + prep.Reason);
        }

        Terminal.WriteLine();
        Terminal.WriteDim("Escribe libremente lo que necesitas, por ejemplo:");
        Terminal.WriteDim("  'revisa por que no compila este proyecto'");
        Terminal.WriteDim("  'crea una pagina web sencilla para este proyecto'");
        Terminal.WriteDim("  'continua el desarrollo de esta aplicacion'");
    }

    private static void RenderHelp()
    {
        Terminal.WriteLine();
        Terminal.WriteInfo("C O N D O R");
        Terminal.WriteDim(VersionInfo.Tagline);
        Terminal.WriteLine();
        Terminal.WriteLine("Condor es un agente de ingenieria. Escribe con palabras la intencion");
        Terminal.WriteLine("y Condor comprende, analiza, selecciona estrategia y modelo, actua con");
        Terminal.WriteLine("herramientas reales, verifica y entrega el resultado.");
        Terminal.WriteLine();
        Terminal.WriteLine("Uso:");
        Terminal.WriteLine("  condor                       Prepara el entorno y abre la sesion interactiva.");
        Terminal.WriteLine("  condor <tu intencion>        Ejecuta la intencion con el motor agente.");
        Terminal.WriteLine();
        Terminal.WriteLine("Comandos de control (con /):");
        Terminal.WriteLine("  /analizar                    Analiza el proyecto o directorio actual.");
        Terminal.WriteLine("  /contexto                    Reconstruye el contexto del proyecto.");
        Terminal.WriteLine("  /planear \"<solicitud>\"       Genera un plan de trabajo.");
        Terminal.WriteLine("  /construir                   Aplica los cambios del plan.");
        Terminal.WriteLine("  /verificar                   Comprueba los cambios aplicados.");
        Terminal.WriteLine("  /avanzar \"<solicitud>\"        Ejecuta el ciclo de ingenieria parcial.");
        Terminal.WriteLine("  /examinar \"<imagen>\"         Analiza una imagen localmente.");
        Terminal.WriteLine("  /recomendar \"<tipo>\"         Recomienda un modelo para el equipo.");
        Terminal.WriteLine("  /consultar \"<mensaje>\"       Consulta al modelo local.");
        Terminal.WriteLine("  /verificar-semantico         Compila y ejecuta las pruebas del proyecto.");
        Terminal.WriteLine("  /preparar                    Refresca la preparacion del entorno.");
        Terminal.WriteLine("  /ayuda                       Muestra esta ayuda.");
        Terminal.WriteLine("  /salir                       Termina la sesion interactiva.");
        Terminal.WriteLine();
        Terminal.WriteLine("Contracciones:");
        Terminal.WriteLine("  -v, --version                Muestra la version.");
        Terminal.WriteLine("  -h, --help                   Muestra esta ayuda.");
        Terminal.WriteLine();
        Terminal.WriteDim("No necesitas conocer modelos, herramientas, fases internas ni rutas.");
        Terminal.WriteDim("Escribe lo que necesitas y Condor se encarga del resto.");
    }
}
