using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace Condor.Cli.Tui;

public enum TuiMouseEventKind
{
    LeftClick,
    Wheel
}

public readonly record struct TuiMouseEvent(
    TuiMouseEventKind Kind,
    int Row,
    int Column,
    int WheelDelta = 0);

public readonly record struct TuiConsoleInputEvent(
    ConsoleKeyInfo Key,
    TuiMouseEvent? Mouse = null);

/// <summary>Activa seguimiento de mouse y restaura el modo previo al suspender la TUI.</summary>
internal sealed class TuiMouseTrackingMode : IDisposable
{
    private const int StdInputHandle = -10;
    private const uint EnableMouseInput = 0x0010;
    private const uint EnableQuickEditMode = 0x0040;
    private const uint EnableExtendedFlags = 0x0080;
    private const uint MouseModeMask = EnableMouseInput | EnableQuickEditMode | EnableExtendedFlags;

    private IntPtr _inputHandle;
    private uint _originalMouseMode;
    private bool _hasOriginalMode;
    private bool _enabled;
    private bool _posixMode;

    public void Enable()
    {
        if (_enabled || Console.IsInputRedirected || Console.IsOutputRedirected)
        {
            return;
        }

        if (!OperatingSystem.IsWindows())
        {
            try
            {
                Console.Write(Ansi.EnableMouseTracking);
                _posixMode = true;
                _enabled = true;
            }
            catch
            {
                _posixMode = false;
            }

            return;
        }

        try
        {
            _inputHandle = GetStdHandle(StdInputHandle);
            if (_inputHandle == IntPtr.Zero || !GetConsoleMode(_inputHandle, out var currentMode))
            {
                return;
            }

            if (!_hasOriginalMode)
            {
                _originalMouseMode = currentMode & MouseModeMask;
                _hasOriginalMode = true;
            }

            var mouseMode = (currentMode | EnableMouseInput | EnableExtendedFlags) & ~EnableQuickEditMode;
            if (SetConsoleMode(_inputHandle, mouseMode))
            {
                _enabled = true;
            }
        }
        catch
        {
            _enabled = false;
        }
    }

    public void Disable()
    {
        if (!_enabled)
        {
            return;
        }

        if (_posixMode)
        {
            try
            {
                Console.Write(Ansi.DisableMouseTracking);
            }
            catch
            {
                // La restauracion de la terminal es de mejor esfuerzo al cerrar una sesion.
            }

            _posixMode = false;
            _enabled = false;
            return;
        }

        try
        {
            if (_inputHandle != IntPtr.Zero && _hasOriginalMode && GetConsoleMode(_inputHandle, out var currentMode))
            {
                var restoredMode = (currentMode & ~MouseModeMask) | _originalMouseMode;
                SetConsoleMode(_inputHandle, restoredMode);
            }
        }
        catch
        {
            // La consola puede haber cerrado mientras Condor libera el modo de mouse.
        }

        _enabled = false;
    }

    public void Dispose() => Disable();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);
}

/// <summary>Lee teclas y eventos de mouse del mismo flujo de entrada de la TUI.</summary>
internal sealed class TuiConsoleInputReader
{
    private const int StdInputHandle = -10;
    private const ushort KeyEvent = 0x0001;
    private const ushort MouseEvent = 0x0002;
    private const uint ShiftPressed = 0x0010;
    private const uint LeftAltPressed = 0x0002;
    private const uint RightAltPressed = 0x0001;
    private const uint LeftCtrlPressed = 0x0008;
    private const uint RightCtrlPressed = 0x0004;
    private const uint FromLeftButtonPressed = 0x0001;
    private const uint MouseMoved = 0x0001;
    private const uint DoubleClick = 0x0002;
    private const uint MouseWheeled = 0x0004;

    private readonly Queue<TuiConsoleInputEvent> _pending = new();
    private readonly bool _useNativeWindowsInput;
    private readonly IntPtr _inputHandle;
    private bool _leftButtonPressed;

    public TuiConsoleInputReader()
    {
        _useNativeWindowsInput = OperatingSystem.IsWindows() && !Console.IsInputRedirected;
        _inputHandle = _useNativeWindowsInput ? GetStdHandle(StdInputHandle) : IntPtr.Zero;
    }

    public bool TryRead(out TuiConsoleInputEvent inputEvent)
    {
        if (_pending.Count > 0)
        {
            inputEvent = _pending.Dequeue();
            return true;
        }

        if (_useNativeWindowsInput && _inputHandle != IntPtr.Zero && TryReadWindows(out inputEvent))
        {
            return true;
        }

        return TryReadPosix(out inputEvent);
    }

    internal static bool TryParseSgrMouse(string sequence, out TuiMouseEvent mouseEvent)
    {
        mouseEvent = default;
        if (string.IsNullOrEmpty(sequence) || !sequence.StartsWith("\u001b[<", StringComparison.Ordinal) ||
            sequence.Length < 8 || sequence[^1] is not ('M' or 'm'))
        {
            return false;
        }

        var payload = sequence.AsSpan(3, sequence.Length - 4);
        var firstSeparator = payload.IndexOf(';');
        if (firstSeparator <= 0)
        {
            return false;
        }

        var remaining = payload[(firstSeparator + 1)..];
        var secondSeparator = remaining.IndexOf(';');
        if (secondSeparator <= 0 || remaining[(secondSeparator + 1)..].IndexOf(';') >= 0)
        {
            return false;
        }

        if (!int.TryParse(payload[..firstSeparator], NumberStyles.None, CultureInfo.InvariantCulture, out var button) ||
            !int.TryParse(remaining[..secondSeparator], NumberStyles.None, CultureInfo.InvariantCulture, out var column) ||
            !int.TryParse(remaining[(secondSeparator + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var row) ||
            row < 1 || column < 1)
        {
            return false;
        }

        if ((button & 64) != 0)
        {
            mouseEvent = new TuiMouseEvent(
                TuiMouseEventKind.Wheel,
                row,
                column,
                (button & 1) == 0 ? 1 : -1);
            return true;
        }

        if ((button & 32) == 0 && (button & 3) == 0 && sequence[^1] == 'M')
        {
            mouseEvent = new TuiMouseEvent(TuiMouseEventKind.LeftClick, row, column);
            return true;
        }

        return false;
    }

    private bool TryReadWindows(out TuiConsoleInputEvent inputEvent)
    {
        inputEvent = default;
        try
        {
            while (GetNumberOfConsoleInputEvents(_inputHandle, out var available) && available > 0)
            {
                var records = new InputRecord[1];
                if (!ReadConsoleInputW(_inputHandle, records, 1, out var recordsRead) || recordsRead == 0)
                {
                    return false;
                }

                var record = records[0];
                if (record.EventType == KeyEvent && record.Event.Key.KeyDown != 0)
                {
                    var key = record.Event.Key;
                    var modifiers = key.ControlKeyState;
                    var keyInfo = new ConsoleKeyInfo(
                        key.UnicodeChar,
                        (ConsoleKey)key.VirtualKeyCode,
                        (modifiers & ShiftPressed) != 0,
                        (modifiers & (LeftAltPressed | RightAltPressed)) != 0,
                        (modifiers & (LeftCtrlPressed | RightCtrlPressed)) != 0);
                    var repeats = Math.Clamp((int)key.RepeatCount, 1, 64);
                    for (var index = 1; index < repeats; index++)
                    {
                        _pending.Enqueue(new TuiConsoleInputEvent(keyInfo));
                    }

                    inputEvent = new TuiConsoleInputEvent(keyInfo);
                    return true;
                }

                if (record.EventType == MouseEvent && TryConvertMouse(record.Event.Mouse, out var mouse))
                {
                    inputEvent = new TuiConsoleInputEvent(default, mouse);
                    return true;
                }
            }
        }
        catch
        {
            // Si el host no expone registros Win32, la lectura de teclas conserva su ruta habitual.
        }

        return false;
    }

    private bool TryReadPosix(out TuiConsoleInputEvent inputEvent)
    {
        inputEvent = default;
        if (!Console.KeyAvailable)
        {
            return false;
        }

        var key = Console.ReadKey(intercept: true);
        if (key.KeyChar == '\u001b' && key.Key == ConsoleKey.Escape && TryReadSgrMouse(out var mouse))
        {
            inputEvent = new TuiConsoleInputEvent(default, mouse);
            return true;
        }

        inputEvent = new TuiConsoleInputEvent(key);
        return true;
    }

    private bool TryReadSgrMouse(out TuiMouseEvent mouseEvent)
    {
        mouseEvent = default;
        var consumed = new List<ConsoleKeyInfo>();
        var sequence = new StringBuilder("\u001b");
        if (!TryReadKeyWithin(12, out var key))
        {
            return false;
        }

        consumed.Add(key);
        sequence.Append(key.KeyChar);
        if (key.KeyChar != '[' || !TryReadKeyWithin(12, out key))
        {
            Replay(consumed);
            return false;
        }

        consumed.Add(key);
        sequence.Append(key.KeyChar);
        if (key.KeyChar != '<')
        {
            Replay(consumed);
            return false;
        }

        while (sequence.Length < 48 && TryReadKeyWithin(12, out key))
        {
            consumed.Add(key);
            sequence.Append(key.KeyChar);
            if (key.KeyChar is 'M' or 'm')
            {
                if (TryParseSgrMouse(sequence.ToString(), out mouseEvent))
                {
                    return true;
                }

                break;
            }
        }

        Replay(consumed);
        return false;
    }

    private static bool TryReadKeyWithin(int timeoutMilliseconds, out ConsoleKeyInfo key)
    {
        var timer = Stopwatch.StartNew();
        while (timer.ElapsedMilliseconds <= timeoutMilliseconds)
        {
            if (Console.KeyAvailable)
            {
                key = Console.ReadKey(intercept: true);
                return true;
            }

            Thread.Sleep(1);
        }

        key = default;
        return false;
    }

    private void Replay(IEnumerable<ConsoleKeyInfo> keys)
    {
        foreach (var key in keys)
        {
            _pending.Enqueue(new TuiConsoleInputEvent(key));
        }
    }

    private bool TryConvertMouse(MouseEventRecord mouseRecord, out TuiMouseEvent mouseEvent)
    {
        mouseEvent = default;
        var row = mouseRecord.MousePosition.Y + 1;
        var column = mouseRecord.MousePosition.X + 1;
        var flags = mouseRecord.EventFlags;

        if (flags == MouseWheeled)
        {
            var delta = unchecked((short)(mouseRecord.ButtonState >> 16));
            if (delta == 0)
            {
                return false;
            }

            mouseEvent = new TuiMouseEvent(TuiMouseEventKind.Wheel, row, column, Math.Sign(delta));
            return true;
        }

        var leftPressed = (mouseRecord.ButtonState & FromLeftButtonPressed) != 0;
        var isClickRecord = flags == 0 || flags == DoubleClick;
        var pressedNow = leftPressed && (!_leftButtonPressed || flags == DoubleClick) && isClickRecord;
        _leftButtonPressed = leftPressed;
        if (!pressedNow)
        {
            return false;
        }

        mouseEvent = new TuiMouseEvent(TuiMouseEventKind.LeftClick, row, column);
        return true;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct InputRecord
    {
        public ushort EventType;
        public InputRecordUnion Event;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputRecordUnion
    {
        [FieldOffset(0)] public KeyEventRecord Key;
        [FieldOffset(0)] public MouseEventRecord Mouse;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct KeyEventRecord
    {
        public int KeyDown;
        public ushort RepeatCount;
        public ushort VirtualKeyCode;
        public ushort VirtualScanCode;
        public char UnicodeChar;
        public uint ControlKeyState;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseEventRecord
    {
        public ConsoleCoord MousePosition;
        public uint ButtonState;
        public uint ControlKeyState;
        public uint EventFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ConsoleCoord
    {
        public short X;
        public short Y;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNumberOfConsoleInputEvents(IntPtr hConsoleInput, out uint lpcNumberOfEvents);

    [DllImport("kernel32.dll", EntryPoint = "ReadConsoleInputW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadConsoleInputW(
        IntPtr hConsoleInput,
        [Out] InputRecord[] lpBuffer,
        uint nLength,
        out uint lpNumberOfEventsRead);
}
