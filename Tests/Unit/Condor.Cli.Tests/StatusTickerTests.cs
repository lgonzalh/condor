using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using Condor.Cli.Tui;

namespace Condor.Cli.Tests;

public sealed class StatusTickerTests
{
    [Fact]
    public async Task PublicarTresMensajes_ConservaSoloElUltimoEnLaFila()
    {
        using var host = new TuiHost(110, 34);
        host.Enter();
        host.ShowWelcome();
        var lastMessage = NewSignal();
        using var ticker = new StatusTicker((text, final) =>
        {
            host.SetTickerMessage(text, final);
            if (text == "Dependencias locales preparadas")
            {
                lastMessage.TrySetResult(true);
            }
        }, tickerMinDwellMs: 0, outputRedirected: false);

        ticker.Publish("Detectando Ollama");
        ticker.Publish("Servidor Ollama arriba");
        ticker.Publish("Dependencias locales preparadas");
        await lastMessage.Task.WaitAsync(TimeSpan.FromSeconds(2));
        ticker.Complete("Dependencias locales preparadas", conservar: true);
        await ticker.CompletionTask.WaitAsync(TimeSpan.FromSeconds(2));

        var snapshot = Ansi.StripSgr(host.SnapshotFullFrame());
        Assert.Equal("Dependencias locales preparadas", host.SnapshotTickerLine());
        Assert.Contains("Dependencias locales preparadas", snapshot);
        Assert.DoesNotContain("Detectando Ollama", snapshot);
        Assert.DoesNotContain("Servidor Ollama arriba", snapshot);
        Assert.DoesNotContain('\n', snapshot);
    }

    [Fact]
    public async Task MensajeLargo_SeTruncaConPuntosSinHacerWrap()
    {
        using var host = new TuiHost(80, 24);
        host.Enter();
        host.ShowWelcome();
        var published = NewSignal();
        var longMessage = new string('x', 120);
        using var ticker = new StatusTicker((text, final) =>
        {
            host.SetTickerMessage(text, final);
            published.TrySetResult(true);
        }, tickerMinDwellMs: 0, outputRedirected: false);

        ticker.Publish(longMessage);
        await published.Task.WaitAsync(TimeSpan.FromSeconds(2));
        ticker.Complete(longMessage, conservar: true);
        await ticker.CompletionTask.WaitAsync(TimeSpan.FromSeconds(2));

        var line = host.SnapshotTickerLine();
        Assert.Equal(79, Ansi.VisibleWidth(line));
        Assert.EndsWith("...", line, StringComparison.Ordinal);
        Assert.Equal(new string('x', 76) + "...", line);

        var wideText = StatusTicker.TruncateVisible(Ansi.FgRojo + new string('漢', 50) + Ansi.Reset, 79);
        Assert.Equal(79, Ansi.VisibleWidth(wideText));
        Assert.EndsWith("...", wideText, StringComparison.Ordinal);
        Assert.DoesNotContain(Ansi.Esc, wideText);
    }

    [Fact]
    public async Task ColaRespetaLaPermanenciaMinimaYDescartaIntermediosAlSuperarCinco()
    {
        var clock = new ManualTimeProvider();
        var rendered = new ConcurrentQueue<(string Text, bool Final)>();
        var firstMessage = NewSignal();
        var lastMessage = NewSignal();
        using var ticker = new StatusTicker((text, final) =>
        {
            if (text is null)
            {
                return;
            }

            rendered.Enqueue((text, final));
            if (text == "inicio") firstMessage.TrySetResult(true);
            if (text == "ultimo") lastMessage.TrySetResult(true);
        }, timeProvider: clock, outputRedirected: false);

        Assert.Equal(120, ticker.TickerMinDwellMs);
        ticker.Publish("inicio");
        await firstMessage.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await clock.WaitForTimerCountAsync(1).WaitAsync(TimeSpan.FromSeconds(2));

        for (var index = 1; index <= 5; index++)
        {
            ticker.Publish("intermedio-" + index);
        }
        ticker.Publish("ultimo");

        clock.Advance(TimeSpan.FromMilliseconds(119));
        Assert.DoesNotContain(rendered, item => item.Text == "ultimo");
        clock.Advance(TimeSpan.FromMilliseconds(1));
        await lastMessage.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await clock.WaitForTimerCountAsync(2).WaitAsync(TimeSpan.FromSeconds(2));

        var visible = rendered.Where(item => !item.Final).Select(item => item.Text).ToArray();
        Assert.Equal(new[] { "inicio", "ultimo" }, visible);

        ticker.Complete(conservar: true);
        clock.Advance(TimeSpan.FromMilliseconds(120));
        await ticker.CompletionTask.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(rendered.Last().Final);
    }

    [Fact]
    public async Task Complete_ConservaElMensajeFinalOClearLaFila()
    {
        using var host = new TuiHost(110, 34);
        host.Enter();
        host.ShowWelcome();
        var temporaryMessage = NewSignal();
        var finalMessage = NewSignal();
        using var ticker = new StatusTicker((text, final) =>
        {
            host.SetTickerMessage(text, final);
            if (text == "estado temporal") temporaryMessage.TrySetResult(true);
            if (text == "Listo para continuar" && final) finalMessage.TrySetResult(true);
        }, tickerMinDwellMs: 0, outputRedirected: false);

        ticker.Publish("estado temporal");
        await temporaryMessage.Task.WaitAsync(TimeSpan.FromSeconds(2));
        ticker.Complete("Listo para continuar", conservar: true);
        await finalMessage.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await ticker.CompletionTask.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal("Listo para continuar", host.SnapshotTickerLine());

        var cleared = NewSignal();
        using var clearingTicker = new StatusTicker((text, final) =>
        {
            host.SetTickerMessage(text, final);
            if (text == "borrar") cleared.TrySetResult(true);
        }, tickerMinDwellMs: 0, outputRedirected: false);
        clearingTicker.Publish("borrar");
        await cleared.Task.WaitAsync(TimeSpan.FromSeconds(2));
        clearingTicker.Complete(conservar: false);
        await clearingTicker.CompletionTask.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal("", host.SnapshotTickerLine());
    }

    [Fact]
    public void Fila_UsaTextoAtenuadoYTextoNormalParaElMensajeFinal()
    {
        using var host = new TuiHost(110, 34);
        host.Enter();
        host.ShowWelcome();
        var theme = new TuiTheme("", "<texto>", "<atenuado>", "", "", "", "");
        host.SetTuiTheme(theme);

        host.SetTickerMessage("En curso", final: false);
        var pendingFrame = host.SnapshotFullFrame();
        host.SetTickerMessage("Terminado", final: true);
        var finalFrame = host.SnapshotFullFrame();

        Assert.Contains("<atenuado>En curso" + Ansi.Reset, pendingFrame);
        Assert.Contains("<texto>Terminado" + Ansi.Reset, finalFrame);
    }

    [Fact]
    public async Task SalidaRedirigida_ImprimeLineasNormalesSinAnsi()
    {
        var output = new SignalingWriter(2);
        using var ticker = new StatusTicker(
            (_, _) => throw new InvalidOperationException("No debe renderizar una region en modo texto."),
            tickerMinDwellMs: 0,
            output: output,
            outputRedirected: true);

        ticker.Publish("Detectando Ollama");
        ticker.Publish("Servidor Ollama\narriba");
        await output.ExpectedLines.Task.WaitAsync(TimeSpan.FromSeconds(2));
        ticker.Complete(conservar: true);
        await ticker.CompletionTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal("Detectando Ollama" + output.NewLine + "Servidor Ollama arriba" + output.NewLine, output.ToString());
        Assert.DoesNotContain(Ansi.Esc, output.ToString());
    }

    [Fact]
    public async Task RedimensionarDuranteElArranque_RecalculaLaFilaSinResiduos()
    {
        using var host = new TuiHost(110, 34);
        host.Enter();
        host.ShowWelcome();
        var published = NewSignal();
        var longMessage = new string('z', 140);
        using var ticker = new StatusTicker((text, final) =>
        {
            host.SetTickerMessage(text, final);
            published.TrySetResult(true);
        }, tickerMinDwellMs: 0, outputRedirected: false);

        ticker.Publish(longMessage);
        await published.Task.WaitAsync(TimeSpan.FromSeconds(2));
        ticker.Complete(longMessage, conservar: true);
        await ticker.CompletionTask.WaitAsync(TimeSpan.FromSeconds(2));
        var oldLine = host.SnapshotTickerLine();
        Assert.Equal(109, Ansi.VisibleWidth(oldLine));

        Assert.True(host.ResizeTo(80, 24));
        var newLine = host.SnapshotTickerLine();
        var snapshot = Ansi.StripSgr(host.SnapshotFullFrame());

        Assert.Equal(79, Ansi.VisibleWidth(newLine));
        Assert.EndsWith("...", newLine, StringComparison.Ordinal);
        Assert.Contains(newLine, snapshot);
        Assert.DoesNotContain(oldLine, snapshot);
    }

    private static TaskCompletionSource<bool> NewSignal()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class SignalingWriter(int expectedLines) : StringWriter
    {
        private int _lineCount;
        public TaskCompletionSource<bool> ExpectedLines { get; } = NewSignal();

        public override void WriteLine(string? value)
        {
            base.WriteLine(value);
            if (Interlocked.Increment(ref _lineCount) >= expectedLines)
            {
                ExpectedLines.TrySetResult(true);
            }
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private readonly object _gate = new();
        private readonly List<ManualTimer> _timers = new();
        private readonly Dictionary<int, TaskCompletionSource<bool>> _timerWaiters = new();
        private long _timestamp;
        private int _timerCount;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref _timestamp);

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state);
            timer.Change(dueTime, period);
            lock (_gate)
            {
                _timers.Add(timer);
                _timerCount++;
                foreach (var waiter in _timerWaiters.Where(pair => pair.Key <= _timerCount).Select(pair => pair.Value))
                {
                    waiter.TrySetResult(true);
                }
            }

            return timer;
        }

        public Task WaitForTimerCountAsync(int count)
        {
            lock (_gate)
            {
                if (_timerCount >= count)
                {
                    return Task.CompletedTask;
                }

                if (!_timerWaiters.TryGetValue(count, out var waiter))
                {
                    waiter = NewSignal();
                    _timerWaiters.Add(count, waiter);
                }

                return waiter.Task;
            }
        }

        public void Advance(TimeSpan amount)
        {
            ManualTimer[] timers;
            long now;
            lock (_gate)
            {
                _timestamp += amount.Ticks;
                now = _timestamp;
                timers = _timers.ToArray();
            }

            foreach (var timer in timers)
            {
                timer.FireIfDue(now);
            }
        }

        private sealed class ManualTimer(
            ManualTimeProvider clock,
            TimerCallback callback,
            object? state) : ITimer
        {
            private readonly object _gate = new();
            private long? _dueTimestamp;
            private TimeSpan _period;
            private bool _disposed;

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                lock (_gate)
                {
                    if (_disposed)
                    {
                        return false;
                    }

                    _period = period;
                    _dueTimestamp = dueTime == Timeout.InfiniteTimeSpan
                        ? null
                        : clock.GetTimestamp() + dueTime.Ticks;
                    return true;
                }
            }

            public void FireIfDue(long now)
            {
                lock (_gate)
                {
                    if (_disposed || _dueTimestamp is not { } due || due > now)
                    {
                        return;
                    }

                    _dueTimestamp = _period > TimeSpan.Zero ? now + _period.Ticks : null;
                }

                callback(state);
            }

            public void Dispose()
            {
                lock (_gate)
                {
                    _disposed = true;
                    _dueTimestamp = null;
                }
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}
