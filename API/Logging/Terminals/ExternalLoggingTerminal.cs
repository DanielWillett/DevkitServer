namespace DevkitServer.API.Logging.Terminals;
internal sealed class ExternalLoggingTerminal : ITerminal
{
#pragma warning disable CS0067
    public event TerminalPreReadDelegate? OnInput;
    public event TerminalPreWriteDelegate? OnOutput;
    public bool IsComittingToUnturnedLog => false;
#pragma warning restore CS0067
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
