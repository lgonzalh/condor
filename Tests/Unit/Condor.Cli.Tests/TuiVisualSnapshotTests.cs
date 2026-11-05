using System.Text;
using Condor.Cli.Tui;

namespace Condor.Cli.Tests;

public sealed class TuiVisualSnapshotTests
{
    [Fact]
    public void Bienvenida_ConservaLaComposicionVisualAprobada()
    {
        using var host = CreateHost();
        host.ShowWelcome();
        host.SetWorkspace("C:\\Condor\\Demo");
        host.SetEstado("Preparando dependencias");
        host.SetProgreso("Verificando RAM y modelos locales");
        host.SetModelStatus("Ollama · Verificando RAM y modelos locales", working: true);
        host.SetBusy(true);

        TuiVisualSnapshot.AssertMatches("tui-welcome-110x34", TuiVisualSnapshot.Capture(host.SnapshotFullFrame()));
    }

    [Fact]
    public void Sesion_ConservaLaComposicionVisualAprobada()
    {
        using var host = CreateHost();
        host.ShowSession("qwen2.5-coder:3b");
        host.SetWorkspace("C:\\Condor\\Demo");
        host.SetRuntimeMetadata("Construir", "Ollama (Local)", "qwen2.5-coder:3b");
        host.SetEstado("Construyendo index.html", ActivityKind.CodeEdit);
        host.SetProgreso("Iteración 1");
        host.SetModelStatus("qwen2.5-coder:3b · creando archivo", working: true);
        host.SetBusy(true);
        host.AddActivity("Creando index.html", ActivityKind.CodeEdit);
        host.AddActivity("El archivo quedó guardado", ActivityKind.Success);

        TuiVisualSnapshot.AssertMatches("tui-session-110x34", TuiVisualSnapshot.Capture(host.SnapshotFullFrame()));
    }

    private static TuiHost CreateHost()
    {
        var host = new TuiHost(forceInteractive: true);
        host.Enter();
        return host;
    }
}

internal static class TuiVisualSnapshot
{
    private const int Columns = 110;
    private const int Rows = 34;
    private const string UpdateVariable = "CONDOR_UPDATE_TUI_SNAPSHOTS";

    public static string[] Capture(string frame)
    {
        var cells = Enumerable.Range(0, Rows)
            .Select(_ => new string(' ', Columns).ToCharArray())
            .ToArray();
        var row = 0;
        var column = 0;

        for (var index = 0; index < frame.Length; index++)
        {
            if (frame[index] == '\u001b' && index + 1 < frame.Length && frame[index + 1] == '[')
            {
                var end = index + 2;
                while (end < frame.Length && !char.IsLetter(frame[end])) end++;
                if (end >= frame.Length) break;

                var command = frame[end];
                var parameters = frame.Substring(index + 2, end - index - 2);
                if (command == 'H')
                {
                    var position = parameters.Split(';');
                    row = Math.Max(0, int.Parse(position[0], System.Globalization.CultureInfo.InvariantCulture) - 1);
                    column = Math.Max(0, (position.Length > 1 && position[1].Length > 0
                        ? int.Parse(position[1], System.Globalization.CultureInfo.InvariantCulture)
                        : 1) - 1);
                }
                else if (command == 'J')
                {
                    for (var clearRow = 0; clearRow < Rows; clearRow++)
                        cells[clearRow] = new string(' ', Columns).ToCharArray();
                }
                else if (command == 'K' && row < Rows)
                {
                    for (var clearColumn = column; clearColumn < Columns; clearColumn++) cells[row][clearColumn] = ' ';
                }

                index = end;
                continue;
            }

            if (!char.IsControl(frame[index]) && row < Rows && column < Columns)
                cells[row][column++] = frame[index];
        }

        return cells
            .Select(line => NormalizeVolatileState(Ansi.StripSgr(new string(line)).TrimEnd()))
            .ToArray();
    }

    private static string NormalizeVolatileState(string row)
    {
        const string marker = "RAM Condor ";
        var start = row.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0) return row;

        var end = row.IndexOf("MB", start + marker.Length, StringComparison.Ordinal);
        if (end < 0) return row;

        return row[..start] + "RAM Condor <RAM>" + row[(end + 2)..];
    }

    public static void AssertMatches(string name, IReadOnlyList<string> actual)
    {
        var configuredDirectory = Environment.GetEnvironmentVariable("CONDOR_TUI_SNAPSHOT_DIRECTORY");
        var snapshotDirectory = string.IsNullOrWhiteSpace(configuredDirectory)
            ? Path.Combine(AppContext.BaseDirectory, "VisualSnapshots")
            : Path.GetFullPath(configuredDirectory);
        Directory.CreateDirectory(snapshotDirectory);
        var path = Path.Combine(snapshotDirectory, name + ".snap");

        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            File.WriteAllLines(path, actual, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return;
        }

        Assert.True(File.Exists(path), "Falta el baseline visual " + path + ". Ejecuta operacion/scripts/Update-TuiVisualSnapshots.ps1 tras revisar el diseño.");
        var expected = File.ReadAllLines(path);
        var difference = DescribeDifference(expected, actual);
        Assert.True(difference is null, difference);
    }

    private static string? DescribeDifference(IReadOnlyList<string> expected, IReadOnlyList<string> actual)
    {
        if (expected.SequenceEqual(actual, StringComparer.Ordinal)) return null;

        var lines = new List<string> { "El estado visual de la TUI difiere de su baseline:" };
        var differenceCount = 0;
        for (var index = 0; index < Math.Max(expected.Count, actual.Count); index++)
        {
            var expectedLine = index < expected.Count ? expected[index] : "<sin fila>";
            var actualLine = index < actual.Count ? actual[index] : "<sin fila>";
            if (string.Equals(expectedLine, actualLine, StringComparison.Ordinal)) continue;

            lines.Add("Fila " + (index + 1) + ":");
            lines.Add("  esperado: " + expectedLine);
            lines.Add("  actual:   " + actualLine);
            if (++differenceCount == 12)
            {
                lines.Add("... diferencias adicionales omitidas");
                break;
            }
        }

        return string.Join(Environment.NewLine, lines);
    }
}
