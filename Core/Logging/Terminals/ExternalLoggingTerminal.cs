using DevkitServer.API.Logging;

namespace DevkitServer.Core.Logging.Terminals;
internal sealed class ExternalLoggingTerminal : ITerminal
{
#pragma warning disable CS0067
    public event TerminalPreReadDelegate? OnInput;
    public event TerminalPreWriteDelegate? OnOutput;
    public bool IsComittingToUnturnedLog => false;
#pragma warning restore CS0067
    public void Write(ReadOnlySpan<char> input, ConsoleColor? color, bool saveToUnturnedLog, Severity severity)
    {
        OnOutput?.Invoke(ref input, ref color, severity);
        lock (this)
        {
            ConsoleColor foregroundOld = Console.ForegroundColor;
            if (color.HasValue)
                Console.ForegroundColor = color.Value;

            Console.WriteLine(input.ToString());

            if (color.HasValue)
                Console.ForegroundColor = foregroundOld;
        }
    }
    public void Init() { }
    public void Close() { }
}
