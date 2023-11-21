#if SERVER
using DevkitServer.Commands.Subsystem;

namespace DevkitServer.API.Logging.Terminals;
internal class ServerTerminal : MonoBehaviour, ITerminal
{
    private bool _writing;
    public event TerminalPreReadDelegate? OnInput;
    public event TerminalPreWriteDelegate? OnOutput;
    public Action<CommandWindow, string>? LogInfoIntl;
    public Action<CommandWindow, string>? LogWarnIntl;
    public Action<CommandWindow, string>? LogErrorIntl;
    private bool _init;
    public bool IsComittingToUnturnedLog => _writing;
    private void CheckInit()
    {
        if (_init) return;
        _init = true;
        LogInfoIntl = Accessor.GenerateInstanceCaller<CommandWindow, Action<CommandWindow, string>>("internalLogInformation");
        LogWarnIntl = Accessor.GenerateInstanceCaller<CommandWindow, Action<CommandWindow, string>>("internalLogWarning");
        LogErrorIntl = Accessor.GenerateInstanceCaller<CommandWindow, Action<CommandWindow, string>>("internalLogError");
    }
    public void Write(string input, ConsoleColor color, bool save, Severity severity)
    {
        CheckInit();
        OnOutput?.Invoke(ref input, ref color);
        string str = FormattingUtil.GetANSIString(color, false) + input + FormattingUtil.ANSIForegroundReset;
        _writing = true;
        switch (severity)
        {
            case Severity.Warning:
                if (LogWarnIntl == null)
                {
                    DevkitServerUtility.QueueOnMainThread(() => CommandWindow.LogWarning(str));
                    save = false;
                    return;
                }

                LogWarnIntl(Dedicator.commandWindow, str);
                break;

            case Severity.Error:
                if (LogErrorIntl == null)
                {
                    DevkitServerUtility.QueueOnMainThread(() => CommandWindow.LogError(str));
                    save = false;
                    return;
                }

                LogErrorIntl(Dedicator.commandWindow, str);
                break;

            default:
                if (LogInfoIntl == null)
                {
                    DevkitServerUtility.QueueOnMainThread(() => CommandWindow.Log(str));
                    save = false;
                    return;
                }

                LogInfoIntl(Dedicator.commandWindow, str);
                break;
        }
        if (save)
        {
            Logger.TryRemoveDateFromLine(ref str);
            str = FormattingUtil.RemoveANSIFormatting(str);
            DevkitServerUtility.QueueOnMainThread(() =>
            {
                CommandHandler.IsLoggingFromDevkitServer = true;
                try
                {
                    Logs.printLine(str);
                }
                finally
                {
                    CommandHandler.IsLoggingFromDevkitServer = false;
                }
            });
        }
        _writing = false;
    }

    public void Init()
    {
        CommandWindow.onCommandWindowInputted += OnInputted;
    }

    public void Close()
    {
        CommandWindow.onCommandWindowInputted -= OnInputted;
    }

    private void OnInputted(string text, ref bool shouldexecutecommand)
    {
        OnInput?.Invoke(text, ref shouldexecutecommand);
    }
}
#endif