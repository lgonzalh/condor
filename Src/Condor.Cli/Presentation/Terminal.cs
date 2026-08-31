namespace Condor.Cli.Presentation;

public static class Terminal
{
    private const string Reset = "\u001b[0m";
    private const string Cyan = "\u001b[36m";
    private const string Green = "\u001b[32m";
    private const string Yellow = "\u001b[33m";
    private const string Red = "\u001b[31m";
    private const string Dim = "\u001b[2m";
    private const string Bold = "\u001b[1m";
    private const string White = "\u001b[97m";

    public static bool UseColor { get; } =
        !Console.IsOutputRedirected &&
        string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));

    public static void WriteLine()
    {
        Console.WriteLine();
    }

    public static void WriteLine(string text)
    {
        Console.WriteLine(text);
    }

    public static void WriteInfo(string text)
    {
        Console.WriteLine(UseColor ? Cyan + text + Reset : text);
    }

    /// <summary>Cyan: identifica el contenido producido por Condor.</summary>
    public static void WriteCyan(string text)
    {
        Console.WriteLine(UseColor ? Cyan + text + Reset : text);
    }

    /// <summary>White: identifica la marca de Condor (Condor, ©Condor).</summary>
    public static void WriteWhite(string text)
    {
        Console.WriteLine(UseColor ? White + text + Reset : text);
    }

    public static void WriteSuccess(string text)
    {
        Console.WriteLine(UseColor ? Green + text + Reset : text);
    }

    public static void WriteWarning(string text)
    {
        Console.WriteLine(UseColor ? Yellow + text + Reset : text);
    }

    public static void WriteError(string text)
    {
        Console.WriteLine(UseColor ? Red + text + Reset : text);
    }

    public static void WriteDim(string text)
    {
        Console.WriteLine(UseColor ? Dim + text + Reset : text);
    }

    public static void WriteBold(string text)
    {
        Console.WriteLine(UseColor ? Bold + text + Reset : text);
    }

    public static void WriteHeader(string text)
    {
        Console.WriteLine();
        Console.WriteLine(UseColor ? Cyan + text + Reset : text);
    }
}
