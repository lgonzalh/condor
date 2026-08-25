using System.Text;
using Condor.Cli.Commands;
using Condor.Cli.Presentation;
using Condor.Cli.Routing;
using Condor.Core.Contracts;
using Condor.Core.Models;
using Condor.Infrastructure;
using Condor.Infrastructure.Agent;
using Condor.Infrastructure.DependencyBootstrap;
using Condor.Infrastructure.Llm;
using Condor.Infrastructure.Setup;

namespace Condor.Cli.Tui;

/// <summary>
/// Sesion interactiva de Condor sobre la TUI persistente. Sustituye el bucle
/// clasico de consola cuando hay terminal interactiva con soporte VT; en otro
/// caso Condor usa automaticamente la experiencia CLI establecida (E2E,
/// pipelines y terminales sin VT no cambian).
///
/// Flujo del mockup oficial: Condor Grande da la bienvenida mientras se prepara
/// el entorno real; al estar listo, la sesion pasa a Condor Ave con la identidad
/// institucional permanente, zona de Conversacion/Actividad, Estado/Progreso con
/// datos reales del sistema y entrada de intenciones.
/// </summary>
public static class CondorTui
{
    /// <summary>La TUI requiere terminal interactiva con VT y tamano suficiente.</summary>
    public static bool CanRun()
    {
        return CanRun(out _, out _);
    }

    /// <summary>
    /// Verifica compatibilidad y devuelve las dimensiones reales de la terminal
    /// para que RunAsync() no relea P/Invoke redundantes.
    /// </summary>
    public static bool CanRun(out int width, out int height)
    {
        width = 0;
        height = 0;
        if (Console.IsOutputRedirected || Console.IsInputRedirected)
        {
            return false;
        }

        if (!Ansi.TryEnableVirtualTerminal())
        {
            return false;
        }

        try
        {
            width = Console.WindowWidth;
            height = Console.WindowHeight;
            return width >= TuiHost.MinWidth &&
                   height >= TuiHost.MinHeight;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<int> RunAsync(
        IAssessmentService assessmentService,
        IStateStore stateStore,
        LocalModelSession session,
        CancellationToken shutdownToken,
        int? tuiWidth = null,
        int? tuiHeight = null)
    {
        using var host = tuiWidth.HasValue && tuiHeight.HasValue
            ? new TuiHost(tuiWidth.Value, tuiHeight.Value)
            : new TuiHost();
        host.Enter();
        host.ShowWelcome();
        host.Repaint(); // primera imagen inmediata: la TUI aparece sin esperar la preparacion

        // ---- Preparacion real del entorno (bootstrap Ollama + modelo) ----------
        // Corre en segundo plano MIENTRAS el bucle de render repinta la pantalla:
        // el progreso es real (TuiStartupView publica estados reales) y el usuario
        // ve la TUI de inmediato, sin bloqueo previo.
        var startup = new TuiStartupView(host);
        var startupBridge = new StartupProgressObserverBridge(startup);
        startup.Start();

        var arranque = System.Threading.Tasks.Task.Run(async () =>
        {
            host.SetEstado("Preparando dependencias locales");
            var boot = await new DependencyBootstrapper(assessmentService: assessmentService).RunAsync(startupBridge, shutdownToken).ConfigureAwait(false);
            if (!boot.Ready)
            {
                return (Boot: boot, Prep: (StartupPrepResult?)null);
            }

            host.SetEstado("Seleccionando modelo adecuado para el equipo");
            var prep = await new StartupPreparer(
                assessmentService,
                stateStore,
                modelAutoSetup: new ModelAutoSetupService(
                    stateStore, assessmentService, httpClient: session.SharedHttpClient)).RunAsync(startupBridge, shutdownToken, cachedAssessment: boot.Assessment).ConfigureAwait(false);

            return (Boot: boot, Prep: (StartupPrepResult?)prep);
        });

        // Bucle de render minimo mientras arranca: mantiene la pantalla viva.
        while (!arranque.IsCompleted)
        {
            host.HandleResizeIfNeeded();
            host.Tick();
            host.Repaint();
            Thread.Sleep(40);
        }

        DependencyBootstrapResult bootstrap;
        StartupPrepResult? prepNullable;
        try
        {
            (bootstrap, prepNullable) = await arranque.ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return 130;
        }

        if (prepNullable is null)
        {
            return await FatalExitAsync(
                host,
                "No se pudo dejar el entorno listo automaticamente.",
                DescribeBootstrapFailure(bootstrap));
        }

        var prep = prepNullable;
        if (!prep.Ready)
        {
            startup.Stop(false);
            return await FatalExitAsync(
                host,
                "Condor no puede iniciar: no hay un modelo utilizable ahora.",
                new[] { prep.Reason ?? "Se intento preparar un modelo compatible sin exito." });
        }

        startup.Stop(true);

        // ---- Sesion de trabajo: Condor Grande -> Condor Ave --------------------
        host.ShowSession(prep.Model);
        host.SetModel(prep.Model);
        host.SetEstado("En espera de tu intencion", ActivityKind.Success);
        host.SetProgreso("—");

        host.AddActivity("Entorno listo. Modo Local 100% activo.", ActivityKind.Success);
        if (!string.IsNullOrWhiteSpace(prep.Model))
        {
            host.AddActivity("Modelo local listo: " + prep.Model, ActivityKind.System);
        }

        if (prep.NeedsIntervention && !string.IsNullOrWhiteSpace(prep.Reason))
        {
            host.AddActivity(prep.Reason!.Trim(), ActivityKind.Warning);
        }

        host.AddActivity("Escribe lo que necesitas; Condor comprende, actua con herramientas reales y verifica.", ActivityKind.System);

        // ---- Bucle principal: teclado + repintado por regiones ------------------
        Console.TreatControlCAsInput = true;
        var input = new TuiInput(host);
        input.Render();

        Task<AgentResult>? running = null;
        CancellationTokenSource? runningCts = null;
        var runningWatch = System.Diagnostics.Stopwatch.StartNew();
        var exitCode = 0;
        string? lastHeaderModel = null;

        while (true)
        {
            host.HandleResizeIfNeeded();
            host.Tick();

            // Actualizar modelo en cabecera si hay tarea activa y el modelo de sesion cambio.
            if (running is not null)
            {
                var activeModel = session.ActiveModel;
                if (!string.IsNullOrWhiteSpace(activeModel) && activeModel != lastHeaderModel)
                {
                    host.SetModel(activeModel);
                    lastHeaderModel = activeModel;
                }
            }
            else if (lastHeaderModel is not null)
            {
                // Tarea terminada: restaurar modelo de inicio si es diferente.
                if (!string.IsNullOrWhiteSpace(prep.Model) && prep.Model != lastHeaderModel)
                {
                    host.SetModel(prep.Model);
                }
                lastHeaderModel = null;
            }

            host.Repaint();

            // Finalizacion de la tarea del agente (marshal al hilo de interfaz).
            if (running is { IsCompleted: true })
            {
                var finished = running;
                running = null;
                runningWatch.Stop();

                AgentResult? result = null;
                string? failure = null;
                try
                {
                    result = await finished.ConfigureAwait(true);
                }
                catch (OperationCanceledException)
                {
                    failure = null;
                }
                catch (Exception ex)
                {
                    failure = ShortReason(ex);
                }

                runningCts?.Dispose();
                runningCts = null;

                if (result is not null)
                {
                    foreach (var line in SplitLines(AgentRenderer.BuildResultText(result, runningWatch.Elapsed)))
                    {
                        host.AddActivity(line, result.Success ? ActivityKind.Condor : ActivityKind.Error);
                    }

                    host.SetEstado(result.Success ? "Tarea completada" : "No se pudo completar la tarea",
                        result.Success ? ActivityKind.Success : ActivityKind.Error);
                }
                else if (failure is not null)
                {
                    host.AddActivity("No se pudo completar la tarea: " + failure, ActivityKind.Error);
                    host.SetEstado("Error durante la tarea", ActivityKind.Error);
                }
                else
                {
                    host.AddActivity("Tarea cancelada por el usuario.", ActivityKind.Warning);
                    host.SetEstado("Tarea cancelada", ActivityKind.Warning);
                }

                host.SetProgreso(FormatoTiempo(runningWatch.Elapsed));
                host.SetBusy(false);
                input.Render();
            }

            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(intercept: true);

                if ((key.Modifiers & ConsoleModifiers.Control) != 0 && key.Key == ConsoleKey.C)
                {
                    // Ctrl+C solo sale de la sesion cuando no hay tarea en curso
                    if (running is null)
                    {
                        break;
                    }
                    // Con tarea en curso, Ctrl+C no hace nada (se usa Esc+Esc)
                    continue;
                }

                if (running is not null)
                {
                    var interruptAction = input.Handle(key);
                    if (interruptAction == InputAction.Interrupt)
                    {
                        // Interrupcion cooperativa de la tarea en curso (Esc + Esc).
                        runningCts?.Cancel();
                        continue;
                    }
                    continue; // teclado reservado mientras Condor trabaja
                }

                var submitAction = input.Handle(key);
                if (submitAction == InputAction.Exit)
                {
                    break;
                }

                if (submitAction == InputAction.Submit)
                {
                    var text = input.Buffer.Trim();
                    input.Clear();
                    if (text.Length == 0)
                    {
                        continue;
                    }

                    if (IsExit(text))
                    {
                        break;
                    }

                    // Comentario del usuario (-texto-): se registra tal cual y NUNCA
                    // se interpreta como instruccion, comando, tarea o accion.
                    if (EsComentarioUsuario(text))
                    {
                        host.AddActivity(text, ActivityKind.User);
                        host.SetEstado("Comentario registrado", ActivityKind.System);
                        host.SetProgreso("—");
                        input.Render();
                        continue;
                    }

                    var route = IntentionRouter.Route(text);
                    switch (route)
                    {
                        case SlashRoute slash when slash.Kind == SlashCommandKind.Ayuda:
                            // /ayuda dentro de la TUI: mostrar ayuda en la zona de actividad sin suspender la sesion.
                            host.AddActivity(text, ActivityKind.User);
                            RenderHelpInTui(host);
                            host.SetEstado("Listo", ActivityKind.Success);
                            host.SetProgreso("—");
                            input.Render();
                            break;

                        case SlashRoute slash:
                            // Los demas comandos "/" conservan su presentacion establecida:
                            // la pantalla TUI se suspende y retoma intacta.
                            host.AddActivity(text, ActivityKind.User);
                            host.SetEstado("Ejecutando " + FirstToken(text));
                            await host.SuspendAsync(async () =>
                                exitCode = await Program.HandleSlashAsync(slash, assessmentService, stateStore, session.Llm, session)).ConfigureAwait(true);
                            host.SetEstado("Listo", ActivityKind.Success);
                            host.SetProgreso("—");
                            input.Render();
                            break;

                        case FreeIntentionRoute free when !string.IsNullOrWhiteSpace(free.Intention):
                            host.SetBusy(true);
                            runningCts = CancellationTokenSource.CreateLinkedTokenSource(shutdownToken);
                            var agentBridge = new AgentProgressObserverBridge(new TuiAgentProgressView(host));
                            var agent = new AgentService(stateStore, assessmentService, session: session);
                            runningWatch.Restart();
                            var token = runningCts.Token;
                            running = Task.Run(() => agent.RunAsync(free.Intention, agentBridge, token));
                            break;
                    }
                }
            }
            else
            {
                Thread.Sleep(40);
            }
        }

        runningCts?.Cancel();
        Console.TreatControlCAsInput = false;
        host.Dispose();
        Console.WriteLine("Hasta pronto.");
        return exitCode;
    }

    private static async Task<int> FatalExitAsync(TuiHost host, string headline, string[] details)
    {
        host.SetEstado(headline, ActivityKind.Error);
        host.SetProgreso("—");
        foreach (var detail in details.Where(d => !string.IsNullOrWhiteSpace(d)))
        {
            host.AddActivity(detail!, ActivityKind.Error);
        }

        host.AddActivity("Cierra esta ventana o reintenta con 'condor /preparar' cuando el entorno permita continuar.", ActivityKind.System);
        host.Repaint();

        try
        {
            Console.TreatControlCAsInput = true;
            Console.ReadKey(intercept: true);
        }
        catch
        {
            await Task.Delay(1500);
        }
        finally
        {
            Console.TreatControlCAsInput = false;
        }

        host.Dispose();
        return 1;
    }

    private static string[] DescribeBootstrapFailure(DependencyBootstrapResult bootstrap)
    {
        var lines = new List<string>();
        if (bootstrap.Ollama is not null)
        {
            lines.Add("Ollama instalado: " + (bootstrap.Ollama.Health == OllamaHealth.NotInstalled ? "no" : "si") +
                      " · Ollama Server: " + (bootstrap.Ollama.Health == OllamaHealth.ServerAvailable ? "disponible" : "no disponible"));
        }

        if (!string.IsNullOrWhiteSpace(bootstrap.Reason))
        {
            lines.Add("Motivo: " + bootstrap.Reason);
        }

        return lines.ToArray();
    }

    private static bool IsExit(string text)
    {
        return text.Equals("/salir", StringComparison.OrdinalIgnoreCase) ||
               text.Equals("salir", StringComparison.OrdinalIgnoreCase) ||
               text.Equals("/exit", StringComparison.OrdinalIgnoreCase) ||
               text.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
               text.Equals("/quit", StringComparison.OrdinalIgnoreCase) ||
               text.Equals("quit", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Comentario del usuario: texto entre guiones, p. ej. "-asi de esta manera-".
    /// Se distingue de instrucciones y comandos; nunca dispara ejecucion.
    /// </summary>
    internal static bool EsComentarioUsuario(string text)
    {
        var s = (text ?? "").Trim();
        if (s.Length < 3 || !s.StartsWith('-') || !s.EndsWith('-'))
        {
            return false;
        }

        // El interior debe contener al menos un caracter que no sea guion.
        return s[1..^1].Trim('-').Trim().Length > 0;
    }

    private static string FirstToken(string text)
    {
        var i = text.IndexOf(' ');
        return i < 0 ? text : text[..i];
    }

    internal static IEnumerable<string> SplitLines(string text)
    {
        return text.Replace("\r\n", "\n").Split('\n');
    }

    /// <summary>Motivo corto y legible de una excepcion: nunca stack traces.</summary>
    private static string ShortReason(Exception ex)
    {
        var message = ex.Message.Split('\n')[0].Trim();
        return message.Length == 0 ? ex.GetType().Name : message;
    }

    private static string FormatoTiempo(TimeSpan e)
    {
        return e.TotalHours >= 1
            ? string.Format("{0:00}:{1:00}:{2:00}", (int)e.TotalHours, e.Minutes, e.Seconds)
            : string.Format("{0:00}:{1:00}", e.Minutes, e.Seconds);
    }

    /// <summary>Renderiza la ayuda integrada dentro de la TUI (zona de actividad).</summary>
    private static void RenderHelpInTui(TuiHost host)
    {
        host.AddActivity("", ActivityKind.System);
        host.AddActivity("C O N D O R", ActivityKind.System);
        host.AddActivity("Observa · Comprende · Planifica · Construye · Verifica", ActivityKind.System);
        host.AddActivity("v1.0 · build interno α.01", ActivityKind.System);
        host.AddActivity("", ActivityKind.System);
        host.AddActivity("Condor es un agente de ingenieria. Escribe con palabras la intencion", ActivityKind.System);
        host.AddActivity("y Condor comprende, analiza, selecciona estrategia y modelo, actua con", ActivityKind.System);
        host.AddActivity("herramientas reales, verifica y entrega el resultado.", ActivityKind.System);
        host.AddActivity("", ActivityKind.System);
        host.AddActivity("Uso:", ActivityKind.System);
        host.AddActivity("  condor                       Prepara el entorno y abre la sesion interactiva.", ActivityKind.System);
        host.AddActivity("  condor <tu intencion>        Ejecuta la intencion con el motor agente.", ActivityKind.System);
        host.AddActivity("", ActivityKind.System);
        host.AddActivity("Comandos de control (con /):", ActivityKind.System);
        host.AddActivity("  /analizar                    Analiza el proyecto o directorio actual.", ActivityKind.System);
        host.AddActivity("  /contexto                    Reconstruye el contexto del proyecto.", ActivityKind.System);
        host.AddActivity("  /planear \"<solicitud>\"       Genera un plan de trabajo.", ActivityKind.System);
        host.AddActivity("  /construir                   Aplica los cambios del plan.", ActivityKind.System);
        host.AddActivity("  /verificar                   Comprueba los cambios aplicados.", ActivityKind.System);
        host.AddActivity("  /avanzar \"<solicitud>\"        Ejecuta el ciclo de ingenieria parcial.", ActivityKind.System);
        host.AddActivity("  /examinar \"<imagen>\"         Analiza una imagen localmente.", ActivityKind.System);
        host.AddActivity("  /recomendar \"<tipo>\"         Recomienda un modelo para el equipo.", ActivityKind.System);
        host.AddActivity("  /consultar \"<mensaje>\"       Consulta al modelo local.", ActivityKind.System);
        host.AddActivity("  /verificar-semantico         Compila y ejecuta las pruebas del proyecto.", ActivityKind.System);
        host.AddActivity("  /preparar                    Refresca la preparacion del entorno.", ActivityKind.System);
        host.AddActivity("  /ayuda                       Muestra esta ayuda.", ActivityKind.System);
        host.AddActivity("  /version                     Muestra la version.", ActivityKind.System);
        host.AddActivity("  /salir                       Termina la sesion interactiva.", ActivityKind.System);
        host.AddActivity("", ActivityKind.System);
        host.AddActivity("Contracciones:", ActivityKind.System);
        host.AddActivity("  -v, --version                Muestra la version.", ActivityKind.System);
        host.AddActivity("  -h, --help                   Muestra esta ayuda.", ActivityKind.System);
        host.AddActivity("", ActivityKind.System);
        host.AddActivity("No necesitas conocer modelos, herramientas, fases internas ni rutas.", ActivityKind.System);
        host.AddActivity("Escribe lo que necesitas y Condor se encarga del resto.", ActivityKind.System);
    }
}
