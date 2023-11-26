#if CLIENT
using System.ComponentModel;
using System.Runtime.InteropServices;
using SDG.Framework.Utilities;
using System.Text;
using ThreadPriority = System.Threading.ThreadPriority;
using ThreadState = System.Threading.ThreadState;
using DevkitServer.Core.Commands.Subsystem;

namespace DevkitServer.API.Logging.Terminals;
internal sealed class WindowsClientTerminal : MonoBehaviour, ITerminal
{
    public event TerminalPreReadDelegate? OnInput;
    public event TerminalPreWriteDelegate? OnOutput;
    private readonly List<LogMessage> _messageQueue = new List<LogMessage>();
    private int _msgIndex;
    private readonly List<string> _inputQueue = new List<string>();
    private int _inputIndex;
    private volatile bool _cancellationRequested;
    private Thread? _keyMonitor;
    private volatile uint _closeHandler;
    private volatile bool _closeRequested;
    private bool _setup;
    private bool _inText;
    private bool _writing;
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

            // enable extended ANSI support for older terminals
            WindowsConsoleHelper.ConfigureConsoleO(x => x | StandardConsoleOutputFlags.ENABLE_VIRTUAL_TERMINAL_PROCESSING);

            // allow scrolling and selecting text
            WindowsConsoleHelper.ConfigureConsoleI(x => (x & ~StandardConsoleInputFlags.ENABLE_MOUSE_INPUT) | StandardConsoleInputFlags.ENABLE_QUICK_EDIT_MODE);

            WindowsConsoleHelper.OnConsoleControlPressed += OnExitRequested;

            Console.OutputEncoding = new UTF8Encoding(false);
            Console.InputEncoding = new UTF8Encoding(false);
            Console.Title = "Devkit Server Client";

            if (_keyMonitor != null && _keyMonitor.ThreadState == ThreadState.Running)
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
            while (_inputQueue.Count > 0)
            {
                string deq = _inputQueue[_inputIndex];
                ++_inputIndex;

                if (_inputQueue.Count <= _inputIndex)
                {
                    _inputQueue.Clear();
                    _inputIndex = 0;
                }
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
        lock (this)
        {
            if (_messageQueue.Count > 0)
            {
                if (_inText && !_fallback)
                {
                    int x = Console.CursorLeft;
                    int y = Console.CursorTop;
                    while (_messageQueue.Count > 0)
                    {
                        LogMessage msg = _messageQueue[_msgIndex];
                        ++_msgIndex;

                        if (_messageQueue.Count <= _msgIndex)
                        {
                            _messageQueue.Clear();
                            _msgIndex = 0;
                        }
                        int h = 1;
                        for (int i = 0; i < msg.Message.Length; ++i)
                            if (msg.Message[i] == '\n') ++h;
                        Console.MoveBufferArea(0, y, x, 1, 0, y + h);
                        Console.SetCursorPosition(0, y);
                        Console.Write(new string(' ', x));
                        Console.SetCursorPosition(0, y);
                        msg.Write();
                        y += h;

                        if (msg.Save)
                        {
                            string message = msg.Message;
                            Logger.TryRemoveDateFromLine(ref message);
                            _writing = true;
                            CommandHandler.IsLoggingFromDevkitServer = true;
                            try
                            {
                                Logs.printLine(FormattingUtil.RemoveANSIFormatting(message));
                            }
                            finally
                            {
                                CommandHandler.IsLoggingFromDevkitServer = false;
                                _writing = false;
                            }
                        }
                    }
                    Console.SetCursorPosition(x, y);
                }
                else
                {
                    while (_messageQueue.Count > 0)
                    {
                        LogMessage msg = _messageQueue[_msgIndex];
                        ++_msgIndex;

                        if (_messageQueue.Count <= _msgIndex)
                        {
                            _messageQueue.Clear();
                            _msgIndex = 0;
                        }
                        if (!_fallback)
                            msg.Write();
                        if (msg.Save)
                        {
                            string message = msg.Message;
                            Logger.TryRemoveDateFromLine(ref message);
                            _writing = true;
                            CommandHandler.IsLoggingFromDevkitServer = true;
                            try
                            {
                                Logs.printLine(FormattingUtil.RemoveANSIFormatting(message));
                            }
                            finally
                            {
                                CommandHandler.IsLoggingFromDevkitServer = false;
                                _writing = false;
                            }
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
    }

    public void Write(string input, ConsoleColor color, bool save, Severity severity)
    {
        OnOutput?.Invoke(ref input, ref color);
        lock (this)
            _messageQueue.Add(new LogMessage(input, color, save));
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
        lock (this)
        {
            if (_messageQueue.Count > 0)
            {
                while (_messageQueue.Count > 0)
                {
                    LogMessage msg = _messageQueue[_msgIndex];
                    ++_msgIndex;

                    if (_messageQueue.Count <= _msgIndex)
                    {
                        _messageQueue.Clear();
                        _msgIndex = 0;
                    }

                    if (!_fallback)
                        msg.Write();
                    if (msg.Save)
                    {
                        string message = msg.Message;
                        Logger.TryRemoveDateFromLine(ref message);
                        _writing = true;
                        CommandHandler.IsLoggingFromDevkitServer = true;
                        try
                        {
                            Logs.printLine(FormattingUtil.RemoveANSIFormatting(message));
                        }
                        finally
                        {
                            CommandHandler.IsLoggingFromDevkitServer = false;
                            _writing = false;
                        }
                    }
                }
            }
        }


        WindowsConsoleHelper.FreeConsole();
    }
    private void MonitorKeypress()
    {
        Logger.LogDebug("Console keypress monitor thread starting.");
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
                        else if (key.KeyChar == 0 || key.Key == ConsoleKey.OemClear || key.Key >= ConsoleKey.LeftArrow && key.Key <= ConsoleKey.DownArrow)
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
                    _inputQueue.Add(current);
                }
                _inText = false;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Error in key monitor thread.");
            Logger.LogError(ex);
        }
    }
    internal readonly struct LogMessage
    {
        public readonly string Message;
        public readonly bool Save;
        public LogMessage(string message, ConsoleColor color, bool save)
        {
            Message = FormattingUtil.GetANSIString(color, false) + message + FormattingUtil.ANSIForegroundReset;
            Save = save;
        }
        public void Write()
        {
            Console.WriteLine(Message);
        }
    }
}
#endif