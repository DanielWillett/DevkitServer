using DevkitServer.Core.Commands.Subsystem;
using System.Collections.Concurrent;

namespace DevkitServer.Core.Logging.Terminals;
internal sealed class BackgroundLoggingTerminal : MonoBehaviour, ITerminal
{
    private bool _writing;
    private readonly ConcurrentQueue<LogMessage> _msgQueue = new ConcurrentQueue<LogMessage>();
#pragma warning disable CS0067
    [UsedImplicitly]
    public event TerminalPreReadDelegate? OnInput;
    public event TerminalPreWriteDelegate? OnOutput;
    public bool IsComittingToUnturnedLog => _writing;

#pragma warning restore CS0067
    public void Write(ReadOnlySpan<char> input, ConsoleColor? color, bool saveToUnturnedLog, Severity severity)
    {
        if (!saveToUnturnedLog)
            return;

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
            LogIntl(msg.Message, msg.Color, msg.Severity);
        }
    }
    private void LogIntl(string input, ConsoleColor? color, Severity severity)
    {
        ReadOnlySpan<char> span = input;
        OnOutput?.Invoke(ref span, ref color, severity);

        string str = FormattingUtil.RemoveVirtualTerminalSequences(LoggerExtensions.RemoveDateFromLine(span));

        _writing = true;
        CommandHandler.IsLoggingFromDevkitServer = true;
        try
        {
            switch (severity)
            {
                case Severity.Warning:
                    UnturnedLog.warn(str);
                    break;
                case Severity.Error:
                case Severity.Fatal:
                    UnturnedLog.error(str);
                    break;
                default:
                    UnturnedLog.info(str);
                    break;
            }
        }
        finally
        {
            CommandHandler.IsLoggingFromDevkitServer = false;
            _writing = false;
        }
    }
    public void Init() { }
    public void Close() { }
}
