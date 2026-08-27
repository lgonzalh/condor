using Condor.Cli.Commands;
using Condor.Cli.Presentation;
using Condor.Cli.Routing;
using Condor.Core.Contracts;
using Condor.Core.Models;
using Condor.Infrastructure;
using Condor.Infrastructure.Agent;
using Condor.Infrastructure.Context;
using Condor.Infrastructure.Building;
using Condor.Infrastructure.Cycle;
using Condor.Infrastructure.DependencyBootstrap;
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
        // UTF-8 para que caracteres como "·" y "α" se muestren fielmente en
        // consolas modernas independientemente de la pagina de codigos activa.
        try
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
        }
        catch
        {
            // Si el entorno no permite cambiarlo, seguir con la codificacion activa.
        }

        var assessmentService = new AssessmentService();
        var stateStore = new LocalStateStore();

        // Sesion unica y reutilizable del proveedor local para toda la ejecucion:
        // un solo HttpClient y un unico modelo activo. Al terminar (normal, error,
        // cancelacion o /salir) se libera el modelo mediante el mecanismo oficial
        // de Ollama (keep_alive=0). Condor nunca gestiona procesos llama-server.
        using var session = new LocalModelSession();
        var llmClient = session.Llm;

        // Token compartido de cancelacion cooperativa (Ctrl+C): al pulsar Ctrl+C
        // se cancela cualquier operacion pendiente del agente en curso y luego se
        // libera la sesion del proveedor en el cierre de consola.
        using var shutdownCts = new System.Threading.CancellationTokenSource();

        // Ctrl+C: ruta unica de shutdown. Antes de que el proceso termine se
        // cancela de forma cooperativa la operacion pendiente y se libera la
        // sesion del proveedor (keep_alive=0), evitando que el modelo quede
        // retenido en RAM. Es un evento de consola, no un proceso a matar.
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            shutdownCts.Cancel();
            session.ReleaseAsync().GetAwaiter().GetResult();
        };

        // Rutas de terminacion unica: en finally se libera el modelo retenido en
        // RAM. Esto garantiza que Condor no deja la sesion del proveedor ocupando
        // memoria cuando termina, sin matar infraestructura externa.
        try
        {
            // Comandos triviales no requieren preparacion.
            if (IsVersion(args))
            {
                Console.WriteLine(VersionInfo.Product + " " + VersionInfo.DisplayName);
                return 0;
            }

            if (IsHelp(args))
            {
                RenderHelp();
                return 0;
            }

            if (args.Length == 0)
            {
                // Experiencia TUI persistente cuando hay terminal interactiva con
                // VT y tamano suficiente; en otro caso, la CLI clasica establecida
                // (redireccion de E/S para E2E/pipelines queda intacta).
                if (Tui.CondorTui.CanRun(out var tuiWidth, out var tuiHeight))
                {
                    return await Tui.CondorTui.RunAsync(assessmentService, stateStore, session, shutdownCts.Token, tuiWidth, tuiHeight);
                }

                return await RunInteractiveAsync(assessmentService, stateStore, llmClient, session, shutdownCts.Token);
            }

            // Entrada con parametros (one-shot).
            var first = string.Join(" ", args);
            var route = IntentionRouter.Route(first);

            if (route is SlashRoute slash)
            {
                if (slash.Kind != SlashCommandKind.Ayuda)
                {
                    // Bootstrap de dependencias (Ollama) antes de preparar el modelo.
                    var bootstrap = await RunBootstrapAsync(progress: null, shutdownCts.Token);
                    if (!bootstrap.Ready)
                    {
                        RenderBootstrapFailure(bootstrap);
                        return 1;
                    }

                    var prep = await PrepareOnceAsync(assessmentService, stateStore, session);
                    if (prep.NeedsIntervention)
                    {
                        Terminal.WriteWarning(prep.Reason ?? "Preparacion pendiente.");
                    }
                }

                return await HandleSlashAsync(slash, assessmentService, stateStore, llmClient, session);
            }

            if (route is FreeIntentionRoute free && !string.IsNullOrWhiteSpace(free.Intention))
            {
                // Bootstrap de dependencias (Ollama) antes de ejecutar el agente.
                var bootstrap = await RunBootstrapAsync(progress: null, shutdownCts.Token);
                if (!bootstrap.Ready)
                {
                    RenderBootstrapFailure(bootstrap);
                    return 1;
                }

                // Intencion natural en una sola linea: se entrega al motor agente,
                // que ya ejecuta su propia preparacion interna y actua con herramientas.
                return await AgentCommand.ExecuteAsync(
                    new AgentService(stateStore, assessmentService, session: session),
                    args,
                    shutdownCts.Token);
            }

            // Texto vacio: presentar el flujo interactivo.
            return await RunInteractiveAsync(assessmentService, stateStore, llmClient, session, shutdownCts.Token);
        }
        finally
        {
            // Shutdown unico: libera la sesion del proveedor (keep_alive=0) y
            // asegura que el modelo no queda retenido en RAM. Tolerante a errores.
            await session.ReleaseAsync();
        }
    }

    private static async Task<int> RunInteractiveAsync(
        IAssessmentService assessmentService,
        IStateStore stateStore,
        ILlmClient llmClient,
        LocalModelSession session,
        System.Threading.CancellationToken shutdownToken)
    {
        // Preparacion automatica con feedback visual continuo y honesto: desde
        // el arranque hasta el prompt muestra las etapas reales (recursos,
        // Ollama, modelos, descarga, verificacion). Independiente del progreso
        // de tareas del agente.
        using var presenter = new StartupProgressPresenter();
        presenter.Start();
        var bridge = new StartupProgressObserverBridge(presenter);

        // Bootstrap de dependencias: antes del flujo normal se detecta/prepara el
        // entorno necesario (Ollama). El usuario no debe administrar dependencias
        // manualmente: si falta, Condor lo instala/arranca y verifica el endpoint.
        var bootstrap = await RunBootstrapAsync(bridge, shutdownToken, assessmentService);
        if (!bootstrap.Ready)
        {
            presenter.Stop(false);
            RenderBootstrapFailure(bootstrap);
            return 1;
        }

        var prep = await PrepareOnceAsync(assessmentService, stateStore, session, bridge, bootstrap.Assessment);

        presenter.Stop(prep.Ready);

        // Sin un modelo utilizable no se muestra el prompt ni se arranca la
        // sesion: Cóndor no puede operar. Se informa el motivo y se sale.
        if (!prep.Ready)
        {
            RenderStartupFailure(prep.Reason);
            return 1;
        }

        RenderWelcome(prep);
        Terminal.WriteLine();

        if (Console.IsInputRedirected)
        {
            // Modo no interactivo: solo se deja el entorno preparado.
            return 0;
        }

        var interpreter = new Interpreter(
            slash => HandleSlashAsync(slash, assessmentService, stateStore, llmClient, session),
            free => AgentCommand.ExecuteAsync(
                new AgentService(stateStore, assessmentService, session: session),
                free.Intention.Split(' ', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries),
                shutdownToken),
            onBeforePrompt: () =>
            {
                // Redibuja la identidad (superior e inferior) en cada punto de
                // espera de entrada para que no desaparezca por el desplazamiento.
                Presentation.IdentityHeader.Render(prep.Model, Environment.CurrentDirectory);
                Presentation.IdentityHeader.RenderFooter(prep.Model);
            });

        Terminal.WriteDim("Escribe '/ayuda' para los comandos de control o '/salir' para terminar.");
        Terminal.WriteLine();

        return await interpreter.RunAsync();
    }

    private static async Task<StartupPrepResult> PrepareOnceAsync(
        IAssessmentService assessmentService,
        IStateStore stateStore,
        LocalModelSession session,
        IStartupProgressObserver? progress = null,
        AssessmentResult? cachedAssessment = null)
    {
        return await new StartupPreparer(
            assessmentService,
            stateStore,
            modelAutoSetup: new ModelAutoSetupService(stateStore, assessmentService, httpClient: session.SharedHttpClient)).RunAsync(progress, cachedAssessment: cachedAssessment);
    }

    /// <summary>
    /// Bootstrap de dependencias (Ollama) con feedback visible y honesto. Condor
    /// detecta, instala/arranca y verifica el server real por si solo; el usuario
    /// no gestiona dependencias manualmente. Devuelve el resultado sin lanzar
    /// excepciones al usuario final.
    /// </summary>
    private static async Task<DependencyBootstrapResult> RunBootstrapAsync(
        IStartupProgressObserver? progress,
        System.Threading.CancellationToken cancellationToken,
        IAssessmentService? assessmentService = null)
    {
        return await new DependencyBootstrapper(assessmentService: assessmentService).RunAsync(progress, cancellationToken);
    }

    /// <summary>
    /// Falla de bootstrap controlada y sin stack traces: explica el estado para
    /// que el usuario sepa que ocurrio y como proseguir, con opcion de reintento.
    /// </summary>
    private static void RenderBootstrapFailure(DependencyBootstrapResult bootstrap)
    {
        Terminal.WriteLine();
        Terminal.WriteWarning("⚠ No se pudo dejar el entorno listo automaticamente.");
        Terminal.WriteLine();
        if (bootstrap.Ollama is not null)
        {
            var installed = bootstrap.Ollama.Health switch
            {
                OllamaHealth.NotInstalled => "NOK",
                OllamaHealth.ServerAvailable => "OK",
                _ => "OK"
            };
            var server = bootstrap.Ollama.Health switch
            {
                OllamaHealth.ServerAvailable => "OK",
                _ => "ERROR"
            };
            Terminal.WriteDim("  Ollama instalado: [" + installed + "]");
            Terminal.WriteDim("  Ollama Server:    [" + server + "]");
        }

        if (!string.IsNullOrWhiteSpace(bootstrap.Reason))
        {
            Terminal.WriteDim("  Motivo: " + bootstrap.Reason);
        }

        Terminal.WriteLine();
        Terminal.WriteDim("  Puedes [Reintentar] ejecutando 'condor /preparar' o revisar");
        Terminal.WriteDim("  la instalacion de Ollama y volver a intentarlo.");
        Terminal.WriteLine();
    }

    /// <summary>Enrutado de comandos "/" compartido por la CLI clasica y la TUI.</summary>
    internal static async Task<int> HandleSlashAsync(
        SlashRoute route,
        IAssessmentService assessmentService,
        IStateStore stateStore,
        ILlmClient llmClient,
        LocalModelSession session)
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
                new VisionService(stateStore, session: session), stateStore, args, CancellationToken.None),
            SlashCommandKind.Recomendar => await RecommendCommand.ExecuteAsync(stateStore, args, CancellationToken.None),
            SlashCommandKind.Consultar => await AskCommand.ExecuteAsync(llmClient, stateStore, args, CancellationToken.None),
            SlashCommandKind.VerificarSemantico => await CheckCommand.ExecuteAsync(
                new SemanticVerificationService(stateStore), stateStore, args, CancellationToken.None),
            SlashCommandKind.Preparar => await PrepareCommand.ExecuteAsync(
                new SetupService(stateStore, assessmentService),
                new ModelAutoSetupService(stateStore, assessmentService, httpClient: session.SharedHttpClient),
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
            SlashCommandKind.Version => await RenderVersionAndReturn(),
            _ => await RenderHelpAndReturn()
        };
    }

    private static Task<int> RenderHelpAndReturn()
    {
        RenderHelp();
        return Task.FromResult(0);
    }

    private static Task<int> RenderVersionAndReturn()
    {
        Console.WriteLine(VersionInfo.Product + " " + VersionInfo.DisplayName);
        return Task.FromResult(0);
    }

    private static bool IsVersion(string[] args)
    {
        return args.Length == 1 &&
               (args[0].Equals("version", StringComparison.OrdinalIgnoreCase) ||
                args[0].Equals("/version", StringComparison.OrdinalIgnoreCase) ||
                args[0].Equals("/v", StringComparison.OrdinalIgnoreCase) ||
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

    /// <summary>
    /// Bloque honesto de fallo al arrancar: sin un modelo utilizable Condor no
    /// puede iniciar. Se explica el motivo y se indica la salida, sin dejar que
    /// aparezca el prompt &gt; como si todo estuviera funcionando.
    /// </summary>
    private static void RenderStartupFailure(string? reason)
    {
        Terminal.WriteLine();
        Terminal.WriteWarning("Condor no puede iniciar.");
        Terminal.WriteLine();
        Terminal.WriteDim("  No hay modelos locales disponibles.");
        Terminal.WriteDim("  Se intento preparar un modelo compatible, pero no fue posible.");
        Terminal.WriteLine();
        if (!string.IsNullOrWhiteSpace(reason))
        {
            Terminal.WriteDim("  Motivo: " + reason);
        }
        Terminal.WriteLine();
        Terminal.WriteDim("  Puedes intentarlo de nuevo con 'condor /preparar' una vez que haya");
        Terminal.WriteDim("  un modelo capaz o recursos disponibles.");
        Terminal.WriteLine();
    }

    private static void RenderWelcome(StartupPrepResult prep)
    {
        // La interfaz normal es minimalista: modelo, directorio, una instruccion
        // y la barra de identidad inferior (que se dibuja junto al prompt).
        if (!string.IsNullOrWhiteSpace(prep.Model))
        {
            Terminal.WriteDim("Modelo local listo: " + prep.Model);
        }
        else if (!string.IsNullOrWhiteSpace(prep.Reason))
        {
            // Nota gris y breve (p. ej. RAM baja) cuando la sesion arranca igual.
            Terminal.WriteDim(prep.Reason);
        }

        Terminal.WriteDim("Directorio de trabajo: " + Environment.CurrentDirectory);
        Terminal.WriteLine();
        Terminal.WriteDim("Escribe lo que necesitas y Condor se encarga del resto.");
        Terminal.WriteLine();
    }

    private static void RenderHelp()
    {
        Terminal.WriteLine();
        Terminal.WriteInfo("C O N D O R");
        Terminal.WriteDim(VersionInfo.Tagline);
        Terminal.WriteDim(VersionInfo.DisplayName);
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
        WriteHelpCommand("/analizar", "Analiza el proyecto o directorio actual.");
        WriteHelpCommand("/contexto", "Reconstruye el contexto del proyecto.");
        WriteHelpCommand("/planear \"<solicitud>\"", "Genera un plan de trabajo.");
        WriteHelpCommand("/construir", "Aplica los cambios del plan.");
        WriteHelpCommand("/verificar", "Comprueba los cambios aplicados.");
        WriteHelpCommand("/avanzar \"<solicitud>\"", "Ejecuta el ciclo de ingenieria parcial.");
        WriteHelpCommand("/examinar \"<imagen>\"", "Analiza una imagen localmente.");
        WriteHelpCommand("/recomendar \"<tipo>\"", "Recomienda un modelo para el equipo.");
        WriteHelpCommand("/consultar \"<mensaje>\"", "Consulta al modelo local.");
        WriteHelpCommand("/verificar-semantico", "Compila y ejecuta las pruebas del proyecto.");
        WriteHelpCommand("/preparar", "Refresca la preparacion del entorno.");
        WriteHelpCommand("/ayuda", "Muestra esta ayuda.");
        WriteHelpCommand("/version", "Muestra la version.");
        WriteHelpCommand("/salir", "Termina la sesion interactiva.");
        Terminal.WriteLine();
        Terminal.WriteLine("Contracciones:");
        WriteHelpCommand("-v, --version", "Muestra la version.");
        WriteHelpCommand("-h, --help", "Muestra esta ayuda.");
        Terminal.WriteLine();
        Terminal.WriteDim("No necesitas conocer modelos, herramientas, fases internas ni rutas.");
        Terminal.WriteDim("Escribe lo que necesitas y Condor se encarga del resto.");
    }

    private static void WriteHelpCommand(string command, string description)
    {
        var useColor = Terminal.UseColor;
        var reset = "\u001b[0m";
        var bold = "\u001b[1m";
        var dim = "\u001b[2m";
        var cmd = useColor ? bold + command + reset : command;
        var desc = useColor ? dim + description + reset : description;
        Console.WriteLine("  " + cmd.PadRight(useColor ? command.Length + 10 : 28) + " " + desc);
    }
}
