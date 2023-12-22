#if CLIENT
using DevkitServer.API.Logging;
using DevkitServer.Core.Commands.Subsystem;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using ThreadPriority = System.Threading.ThreadPriority;
using ThreadState = System.Threading.ThreadState;

namespace DevkitServer.Core.Logging.Terminals;
internal sealed class WindowsClientTerminal : MonoBehaviour, ITerminal
{
    public event TerminalPreReadDelegate? OnInput;
    public event TerminalPreWriteDelegate? OnOutput;
    private readonly ConcurrentQueue<LogMessage> _outputQueue = new ConcurrentQueue<LogMessage>();
    private readonly ConcurrentQueue<string> _inputQueue = new ConcurrentQueue<string>();
    private volatile bool _cancellationRequested;
    private Thread? _keyMonitor;
    private volatile uint _closeHandler;
    private volatile bool _closeRequested;
    private volatile bool _inText;
    private volatile bool _writing;
    private bool _setup;
    private bool _fallback;
    public bool IsComittingToUnturnedLog => _writing;
    public void Init()
    {
        try
        {
            if (!WindowsConsoleHelper.FreeConsole())
                WindowsConsoleHelper.PrintLastWin32Error("FreeConsole", 0x7F);

            if (!WindowsConsoleHelper.AllocConsole())
                WindowsConsoleHelper.ThrowLastWin32Error("AllocConsole");

            WindowsConsoleHelper.SetUTF8CodePage();

            // enable extended virtual terminal sequences support for older terminals
            WindowsConsoleHelper.ConfigureConsoleO(x => x | StandardConsoleOutputFlags.ENABLE_VIRTUAL_TERMINAL_PROCESSING);

            // allow scrolling and selecting text
            WindowsConsoleHelper.ConfigureConsoleI(x => (x & ~StandardConsoleInputFlags.ENABLE_MOUSE_INPUT) | StandardConsoleInputFlags.ENABLE_QUICK_EDIT_MODE);

            WindowsConsoleHelper.OnConsoleControlPressed += OnExitRequested;

            Console.OutputEncoding = new UTF8Encoding(false);
            Console.InputEncoding = new UTF8Encoding(false);
            Console.Title = "Devkit Server Client";

            if (_keyMonitor is { ThreadState: ThreadState.Running })
                _keyMonitor.Abort();
            Thread curThread = Thread.CurrentThread;
            _keyMonitor = new Thread(MonitorKeypress)
            {
                Priority = ThreadPriority.Lowest,
                CurrentCulture = curThread.CurrentCulture,
                CurrentUICulture = curThread.CurrentUICulture,
                IsBackground = true,
                Name = curThread.Name + " Terminal Reader"
            };
            _keyMonitor.Start();
            _setup = true;
            _fallback = false;
        }
        catch (Win32Exception ex)
        {
            CommandWindow.LogError($"Falling back to basic file logging after WIN32 error: {ex.NativeErrorCode:X8}.");
            CommandWindow.LogError(ex);
            _fallback = true;
        }
    }


    [UsedImplicitly]
    void Update()
    {
        if (_setup)
        {
            while (_inputQueue.TryDequeue(out string deq))
            {
                bool shouldHandle = true;
                OnInput?.Invoke(deq, ref shouldHandle);
            }
            OutputTick();
        }
        else if (_fallback)
            OutputTick();
    }
    private void OutputTick()
    {
        if (_outputQueue.Count > 0)
        {
            lock (this)
            {
                if (_inText && !_fallback)
                {
                    int x = Console.CursorLeft;
                    int y = Console.CursorTop;
                    while (_outputQueue.TryDequeue(out LogMessage msg))
                    {
                        int h = 1;
                        string text = msg.Message;
                        for (int i = 0; i < text.Length; ++i)
                        {
                            if (text[i] == '\n') ++h;
                        }
                        Console.MoveBufferArea(0, y, x, 1, 0, y + h);
                        Console.SetCursorPosition(0, y);
                        Console.Write(new string(' ', x));
                        Console.SetCursorPosition(0, y);

                        if (msg.Color.HasValue)
                        {
                            ConsoleColor old = Console.ForegroundColor;
                            Console.ForegroundColor = msg.Color.Value;
                            Console.WriteLine(text);
                            Console.ForegroundColor = old;
                        }
                        else
                            Console.WriteLine(text);

                        y += h;

                        if (!msg.SaveToUnturnedLog)
                            continue;

                        ReadOnlySpan<char> noDate = LoggerExtensions.RemoveDateFromLine(text);
                        text = FormattingUtil.RemoveVirtualTerminalSequences(noDate);
                        _writing = true;
                        CommandHandler.IsLoggingFromDevkitServer = true;
                        try
                        {
                            Logs.printLine(text);
                        }
                        finally
                        {
                            CommandHandler.IsLoggingFromDevkitServer = false;
                            _writing = false;
                        }
                    }
                    Console.SetCursorPosition(x, y);
                }
                else
                {
                    while (_outputQueue.TryDequeue(out LogMessage msg))
                    {
                        WriteMessage(ref msg);
                    }
                }
            }
        }

        if (!_fallback && _closeRequested)
        {
            string reason = "Closing from control signal \"" +
                            _closeHandler switch
                            {
                                0 => "Ctrl + C",
                                1 => "Ctrl + Break",
                                2 => "Window Closed",
                                5 => "Logging Off",
                                6 => "Shutting Down",
                                _ => "Unknown: " + _closeHandler.ToString("X2")
                            } + "\".";
            Write(reason, ConsoleColor.Red, true, Severity.Info);

            Provider.QuitGame(reason);
            _closeHandler = uint.MaxValue;
        }
    }

    public void Write(ReadOnlySpan<char> input, ConsoleColor? color, bool saveToUnturnedLog, Severity severity)
    {
        OnOutput?.Invoke(ref input, ref color, severity);
        _outputQueue.Enqueue(new LogMessage(input.ToString(), color, saveToUnturnedLog, severity));
        if (DevkitServerModule.IsMainThread)
            OutputTick();
    }

    [return: MarshalAs(UnmanagedType.Bool)]
    private bool OnExitRequested(uint val)
    {
        _closeHandler = val;
        _closeRequested = true;
        Thread.Sleep(TimeSpan.FromSeconds(4.9d));
        return true;
    }
    public void Close()
    {
        _cancellationRequested = true;
        _keyMonitor?.Abort();
        _inText = false;
        if (_setup)
        {
            Console.SetCursorPosition(0, Console.CursorTop);
            WindowsConsoleHelper.OnConsoleControlPressed -= OnExitRequested;

            _setup = false;
        }
        while (_outputQueue.TryDequeue(out LogMessage msg))
        {
            WriteMessage(ref msg);
        }

        WindowsConsoleHelper.FreeConsole();
    }
    private void WriteMessage(ref LogMessage message)
    {
        string text = message.Message;
        if (!_fallback)
        {
            if (message.Color.HasValue)
            {
                ConsoleColor old = Console.ForegroundColor;
                Console.ForegroundColor = message.Color.Value;
                Console.WriteLine(text);
                Console.ForegroundColor = old;
            }
            else
                Console.WriteLine(text);
        }

        if (!message.SaveToUnturnedLog)
            return;

        ReadOnlySpan<char> noDate = LoggerExtensions.RemoveDateFromLine(text);
        text = FormattingUtil.RemoveVirtualTerminalSequences(noDate);
        _writing = true;
        CommandHandler.IsLoggingFromDevkitServer = true;
        try
        {
            Logs.printLine(text);
        }
        finally
        {
            CommandHandler.IsLoggingFromDevkitServer = false;
            _writing = false;
        }
    }
    private void MonitorKeypress()
    {
        CommandWindow.Log("Console keypress monitor thread starting.");
        try
        {
            while (!_cancellationRequested)
            {
                string current = string.Empty;
                int oldlength = 0;
                while (true)
                {
                    ConsoleKeyInfo key = Console.ReadKey();
                    lock (this)
                    {
                        if (current.Length == 0)
                        {
                            if (key.Key is (< ConsoleKey.NumPad0 or > ConsoleKey.NumPad9) and (< ConsoleKey.A or > ConsoleKey.Z) and (< ConsoleKey.Oem1 or > ConsoleKey.Oem8))
                            {
                                Console.SetCursorPosition(0, Console.CursorTop);
                                break;
                            }

                            _inText = true;
                        }
                        if (key.Key == ConsoleKey.Escape)
                        {
                            current = string.Empty;
                            break;
                        }
                        if (key.Key == ConsoleKey.Backspace)
                        {
                            if ((key.Modifiers & ConsoleModifiers.Control) != 0)
                            {
                                string[] n = current.Split(' ');
                                if (n.Length < 2) current = string.Empty;
                                else current = string.Join(" ", n, 0, n.Length - 1);
                            }
                            else
                            {
                                current = current.Substring(0, current.Length - 1);
                            }
                            if (current.Length == 0)
                            {
                                _inText = false;
                            }
                        }
                        else if (key.Key == ConsoleKey.Enter)
                        {
                            Console.SetCursorPosition(0, Console.CursorTop);
                            Console.Write(new string(' ', Console.BufferWidth));
                            break;
                        }
                        else if (key.KeyChar == 0 || key.Key == ConsoleKey.OemClear || key.Key is >= ConsoleKey.LeftArrow and <= ConsoleKey.DownArrow)
                        {
                            Console.SetCursorPosition(current.Length, Console.CursorTop);
                            continue;
                        }
                        else
                        {
                            current += (key.Modifiers & ConsoleModifiers.Shift) != 0 ? char.ToUpper(key.KeyChar) : char.ToLower(key.KeyChar);
                        }
                        if (oldlength > current.Length)
                            Console.Write("\r" + current + new string(' ', oldlength - current.Length));
                        else
                            Console.Write("\r" + current);
                        oldlength = current.Length;
                        Console.SetCursorPosition(current.Length, Console.CursorTop);
                    }
                }
                if (current.Length > 0)
                {
                    _inputQueue.Enqueue(current);
                }
                _inText = false;
            }
        }
        catch (Exception ex)
        {
            CommandWindow.LogError("Error in key monitor thread.");
            CommandWindow.LogError(ex);
        }
    }
}
#endif