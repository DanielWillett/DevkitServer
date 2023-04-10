#if SERVER
using System;

namespace DevkitServer.Util.Terminals;
internal class ServerTerminal : MonoBehaviour, ITerminal
{
    private bool _writing;
    public event TerminalReadDelegate? OnInput;
    public event TerminalWriteDelegate? OnOutput;
    public Action<CommandWindow, string>? LogInfoIntl = Accessor.GenerateInstanceCaller<CommandWindow, Action<CommandWindow, string>>("internalLogInformation");
    public Action<CommandWindow, string>? LogWarnIntl = Accessor.GenerateInstanceCaller<CommandWindow, Action<CommandWindow, string>>("internalLogWarning");
    public Action<CommandWindow, string>? LogErrorIntl = Accessor.GenerateInstanceCaller<CommandWindow, Action<CommandWindow, string>>("internalLogError");

    public bool IsCommitingToUnturnedLog => _writing;

    public void Write(string input, ConsoleColor color, bool save, Severity severity)
    {
        OnOutput?.Invoke(ref input, ref color);
        string str = Logger.GetANSIForegroundString(color) + input + Logger.ANSIReset;
        _writing = true;
        switch (severity)
        {
            case Severity.Warning:
                if (LogWarnIntl == null)
                {
                    CommandWindow.LogWarning(str);
                    return;
                }

                LogWarnIntl(Dedicator.commandWindow, str);
                break;

            case Severity.Error:
                if (LogErrorIntl == null)
                {
                    CommandWindow.LogError(str);
                    return;
                }

                LogErrorIntl(Dedicator.commandWindow, str);
                break;

            default:
                if (LogInfoIntl == null)
                {
                    CommandWindow.Log(str);
                    return;
                }

                LogInfoIntl(Dedicator.commandWindow, str);
                break;
        }
        if (save)
        {
            Logger.TryRemoveDateFromLine(ref str);
            str = Logger.RemoveANSIFormatting(str);
            Logs.printLine(str);
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