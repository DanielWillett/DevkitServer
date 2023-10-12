namespace DevkitServer.API.Logging.Terminals;
internal sealed class ExternalLoggingTerminal : ITerminal
{
    public event TerminalPreReadDelegate? OnInput;
    public event TerminalPreWriteDelegate? OnOutput;
    public bool IsComittingToUnturnedLog => false;
    public void Write(string input, ConsoleColor color, bool save, Severity severity)
    {
        OnOutput?.Invoke(ref input, ref color);
        ConsoleColor foregroundOld = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(input);
        Console.ForegroundColor = foregroundOld;
    }
    public void Init() { }
    public void Close() { }
}
