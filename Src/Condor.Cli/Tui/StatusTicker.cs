using System.IO;
using System.Threading;
using System.Threading.Channels;

namespace Condor.Cli.Tui;

/// <summary>Publica estados breves en una sola fila o como lineas simples.</summary>
public sealed class StatusTicker : IDisposable
{
    public const int DefaultTickerMinDwellMs = 120;
    public const int MaxPendingMessages = 5;

    private readonly Channel<TickerMessage> _channel = Channel.CreateUnbounded<TickerMessage>(
        new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });
    private readonly Action<string?, bool> _renderRegion;
    private readonly TimeProvider _timeProvider;
    private readonly TextWriter _output;
    private readonly Func<int> _widthProvider;
    private readonly bool _plainOutput;
    private readonly object _writeGate = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _worker;
    private string? _current;
    private bool _completed;
    private bool _disposed;

    public StatusTicker(
        Action<string?, bool> renderRegion,
        TimeProvider? timeProvider = null,
        int tickerMinDwellMs = DefaultTickerMinDwellMs,
        bool sinAnimacion = false,
        TextWriter? output = null,
        bool? outputRedirected = null,
        Func<int>? widthProvider = null)
    {
        _renderRegion = renderRegion ?? throw new ArgumentNullException(nameof(renderRegion));
        if (tickerMinDwellMs < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tickerMinDwellMs));
        }

        TickerMinDwellMs = tickerMinDwellMs;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _output = output ?? Console.Out;
        _widthProvider = widthProvider ?? ReadConsoleWidth;
        _plainOutput = sinAnimacion || (outputRedirected ?? Console.IsOutputRedirected);
        _worker = ConsumeAsync();
    }

    public int TickerMinDwellMs { get; }

    internal Task CompletionTask => _worker;

    public void Publish(string mensaje)
    {
        ArgumentNullException.ThrowIfNull(mensaje);
        var message = Normalize(mensaje);
        if (message.Length == 0)
        {
            return;
        }

        lock (_writeGate)
        {
            if (_completed || _disposed)
            {
                return;
            }

            if (!_channel.Writer.TryWrite(new TickerMessage(message, false, true)))
            {
                return;
            }

            if (_channel.Reader.CanCount && _channel.Reader.Count > MaxPendingMessages)
            {
                TickerMessage? latest = null;
                while (_channel.Reader.TryRead(out var pending))
                {
                    latest = pending;
                }

                if (latest is not null)
                {
                    _channel.Writer.TryWrite(latest);
                }
            }
        }
    }

    public void Complete(string? mensajeFinal = null, bool conservar = true)
    {
        lock (_writeGate)
        {
            if (_completed || _disposed)
            {
                return;
            }

            _completed = true;
            while (_channel.Reader.TryRead(out _))
            {
            }

            var final = conservar && !string.IsNullOrWhiteSpace(mensajeFinal)
                ? Normalize(mensajeFinal)
                : null;
            _channel.Writer.TryWrite(new TickerMessage(final, true, conservar));
            _channel.Writer.TryComplete();
        }
    }

    internal static string TruncateVisible(string mensaje, int maxWidth)
    {
        if (maxWidth <= 0)
        {
            return "";
        }

        var text = Normalize(mensaje);
        if (Ansi.VisibleWidth(text) <= maxWidth)
        {
            return text;
        }

        if (maxWidth <= 3)
        {
            return new string('.', maxWidth);
        }

        return Ansi.ClipVisible(text, maxWidth - 3) + "...";
    }

    private async Task ConsumeAsync()
    {
        try
        {
            await foreach (var message in _channel.Reader.ReadAllAsync(_stop.Token).ConfigureAwait(false))
            {
                if (message.IsCompletion)
                {
                    if (message.Conservar)
                    {
                        if (message.Text is not null)
                        {
                            Present(message.Text, final: true);
                            await WaitDwellAsync().ConfigureAwait(false);
                        }
                        else if (_current is not null && !_plainOutput)
                        {
                            Present(_current, final: true);
                        }
                    }
                    else
                    {
                        ClearRegion();
                    }

                    return;
                }

                if (message.Text is null)
                {
                    continue;
                }

                Present(message.Text, final: false);
                await WaitDwellAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
        }
    }

    private void Present(string message, bool final)
    {
        _current = message;
        if (_plainOutput)
        {
            var width = _widthProvider();
            var visibleMessage = width > 0 ? TruncateVisible(message, Math.Max(0, width - 1)) : message;
            _output.WriteLine(visibleMessage);
            _output.Flush();
            return;
        }

        _renderRegion(message, final);
    }

    private void ClearRegion()
    {
        _current = null;
        if (!_plainOutput)
        {
            _renderRegion(null, false);
        }
    }

    private Task WaitDwellAsync()
        => TickerMinDwellMs == 0
            ? Task.CompletedTask
            : Task.Delay(TimeSpan.FromMilliseconds(TickerMinDwellMs), _timeProvider, _stop.Token);

    private static string Normalize(string text)
        => Ansi.StripSgr(text)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace('\t', ' ')
            .Trim();

    private static int ReadConsoleWidth()
    {
        try
        {
            return Console.WindowWidth;
        }
        catch
        {
            return 0;
        }
    }

    public void Dispose()
    {
        lock (_writeGate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _completed = true;
            _channel.Writer.TryComplete();
            _stop.Cancel();
        }

        try
        {
            _worker.GetAwaiter().GetResult();
        }
        finally
        {
            ClearRegion();
            _stop.Dispose();
        }
    }

    private sealed record TickerMessage(string? Text, bool IsCompletion, bool Conservar);
}
