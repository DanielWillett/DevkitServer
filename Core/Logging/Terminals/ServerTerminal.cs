#if SERVER
using DevkitServer.API;
using DevkitServer.Core.Commands.Subsystem;
using System.Collections.Concurrent;

namespace DevkitServer.Core.Logging.Terminals;
internal class ServerTerminal : MonoBehaviour, ITerminal
{
    private bool _writing;
    private readonly ConcurrentQueue<LogMessage> _msgQueue = new ConcurrentQueue<LogMessage>();
    public event TerminalPreReadDelegate? OnInput;
    public event TerminalPreWriteDelegate? OnOutput;
    public static Action<CommandWindow, string>? LogInfoIntl;
    public static Action<CommandWindow, string>? LogWarnIntl;
    public static Action<CommandWindow, string>? LogErrorIntl;
    private static bool _init;
    public bool IsComittingToUnturnedLog => _writing;
    private static void CheckInit()
    {
        if (_init)
            return;

        _init = true;

        LogInfoIntl = Accessor.GenerateInstanceCaller<CommandWindow, Action<CommandWindow, string>>("internalLogInformation");
        LogWarnIntl = Accessor.GenerateInstanceCaller<CommandWindow, Action<CommandWindow, string>>("internalLogWarning");
        LogErrorIntl = Accessor.GenerateInstanceCaller<CommandWindow, Action<CommandWindow, string>>("internalLogError");
    }
    public void Write(ReadOnlySpan<char> input, ConsoleColor? color, bool saveToUnturnedLog, Severity severity)
    {
        string msg = color.HasValue ? FormattingUtil.WrapMessageWithColor(color.Value, input) : input.ToString();

        _msgQueue.Enqueue(new LogMessage(msg, color, saveToUnturnedLog, severity));

        if (DevkitServerModule.IsMainThread)
            Update();
    }
    [UsedImplicitly]
    private void Update()
    {
        while (_msgQueue.TryDequeue(out LogMessage msg))
        {
            LogIntl(msg.Message, msg.Color, msg.SaveToUnturnedLog, msg.Severity);
        }
    }
    private void LogIntl(string input, ConsoleColor? color, bool saveToUnturnedLog, Severity severity)
    {
        CheckInit();
        ReadOnlySpan<char> span = input;
        ReadOnlySpan<char> oldSpan = span;
        OnOutput?.Invoke(ref span, ref color, severity);

        if (oldSpan != span)
            input = span.ToString();

        _writing = true;
        try
        {
            switch (severity)
            {
                case Severity.Warning:
                    if (LogWarnIntl == null)
                    {
                        CommandWindow.LogWarning(LoggerExtensions.RemoveDateFromLine(input).ToString());
                        saveToUnturnedLog = false;
                        break;
                    }

                    LogWarnIntl(Dedicator.commandWindow, input);
                    break;

                case Severity.Error:
                case Severity.Fatal:
                    if (LogErrorIntl == null)
                    {
                        CommandWindow.LogError(LoggerExtensions.RemoveDateFromLine(input).ToString());
                        saveToUnturnedLog = false;
                        break;
                    }

                    LogErrorIntl(Dedicator.commandWindow, input);
                    break;

                default:
                    if (LogInfoIntl == null)
                    {
                        CommandWindow.Log(LoggerExtensions.RemoveDateFromLine(input).ToString());
                        saveToUnturnedLog = false;
                        break;
                    }

                    LogInfoIntl(Dedicator.commandWindow, input);
                    break;
            }

            if (saveToUnturnedLog)
            {
                ReadOnlySpan<char> noDate = LoggerExtensions.RemoveDateFromLine(input);
                input = FormattingUtil.RemoveVirtualTerminalSequences(noDate);
                CommandHandler.IsLoggingFromDevkitServer = true;
                try
                {
                    Logs.printLine(input);
                }
                finally
                {
                    CommandHandler.IsLoggingFromDevkitServer = false;
                }
            }
        }
        finally
        {
            _writing = false;
        }
    }
    public void Init()
    {
        CommandWindow.onCommandWindowInputted += OnInputted;
        if (Application.platform is RuntimePlatform.WindowsEditor or RuntimePlatform.WindowsServer or RuntimePlatform.WindowsPlayer)
        {
            // enable extended virtual terminal sequences support for older terminals
            WindowsConsoleHelper.ConfigureConsoleO(x => x | StandardConsoleOutputFlags.ENABLE_VIRTUAL_TERMINAL_PROCESSING);
        }
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