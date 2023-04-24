namespace DevkitServer.Util.Terminals;
internal sealed class BackgroundLoggingTerminal : MonoBehaviour, ITerminal
{
    private bool _writing;
    public event TerminalPreReadDelegate? OnInput;
    public event TerminalPreWriteDelegate? OnOutput;
    public bool IsCommitingToUnturnedLog => _writing;
    
    public void Write(string input, ConsoleColor color, bool save, Severity severity)
    {
        OnOutput?.Invoke(ref input, ref color);
        if (save)
        {
            _writing = true;
            switch (severity)
            {
                case Severity.Warning:
                    UnturnedLog.warn(input);
                    break;
                case Severity.Error:
                case Severity.Fatal:
                    UnturnedLog.error(input);
                    break;
                default:
                    UnturnedLog.info(input);
                    break;
            }
            _writing = false;
        }
    }
    public void Init() { }
    public void Close() { }
}
