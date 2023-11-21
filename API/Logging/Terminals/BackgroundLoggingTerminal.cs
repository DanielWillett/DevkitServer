using DevkitServer.Commands.Subsystem;

namespace DevkitServer.API.Logging.Terminals;
internal sealed class BackgroundLoggingTerminal : MonoBehaviour, ITerminal
{
    private bool _writing;
    [UsedImplicitly]
    public event TerminalPreReadDelegate? OnInput;
    public event TerminalPreWriteDelegate? OnOutput;
    public bool IsComittingToUnturnedLog => _writing;

    public void Write(string input, ConsoleColor color, bool save, Severity severity)
    {
        OnOutput?.Invoke(ref input, ref color);
        if (save)
        {
            DevkitServerUtility.QueueOnMainThread(() =>
            {
                _writing = true;
                CommandHandler.IsLoggingFromDevkitServer = true;
                try
                {
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
                }
                finally
                {
                    CommandHandler.IsLoggingFromDevkitServer = false;
                    _writing = false;
                }
            });
        }
    }
    public void Init() { }
    public void Close() { }
}
