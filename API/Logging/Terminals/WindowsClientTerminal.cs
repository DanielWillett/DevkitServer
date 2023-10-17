﻿#if CLIENT
using DevkitServer.Commands.Subsystem;
using System.Runtime.InteropServices;
using System.Text;
using SDG.Framework.Utilities;
using ThreadPriority = System.Threading.ThreadPriority;
using ThreadState = System.Threading.ThreadState;

namespace DevkitServer.API.Logging.Terminals;
internal sealed class WindowsClientTerminal : MonoBehaviour, ITerminal
{
    private const uint CodepageUTF8 = 65001U;
    private const int StdOutputHandle = -11;

    public event TerminalPreReadDelegate? OnInput;
    public event TerminalPreWriteDelegate? OnOutput;
    private readonly List<LogMessage> _messageQueue = new List<LogMessage>();
    private int _msgIndex;
    private readonly List<string> _inputQueue = new List<string>();
    private int _inputIndex;
    private volatile bool _cancellationRequested;
    private Thread? _keyMonitor;
    private uint? _closeHandler;
    private bool _setup;
    private bool _inText;
    private bool _writing;
    public bool IsComittingToUnturnedLog => _writing;
    public void Init()
    {
        FreeConsole();
        AllocConsole();
        Thread.Sleep(200);
        SetConsoleOutputCP(CodepageUTF8);
        SetConsoleCP(CodepageUTF8);
        Thread.Sleep(200);
        IntPtr handle = GetStdHandle(StdOutputHandle);
        GetConsoleMode(handle, out uint mode1);
        SetConsoleMode(handle, mode1 | 0x0004); // enable extended ANSI support for older terminals
        SetConsoleCtrlHandler(OnExitRequested, true);
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
    }
    private void OutputTick()
    {
        if (_messageQueue.Count > 0)
        {
            _writing = true;
            if (_inText)
            {
                lock (this)
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
                            CommandHandler.IsLoggingFromDevkitServer = true;
                            try
                            {
                                Logs.printLine(FormattingUtil.RemoveANSIFormatting(message));
                            }
                            finally
                            {
                                CommandHandler.IsLoggingFromDevkitServer = false;
                            }
                        }
                    }
                    Console.SetCursorPosition(x, y);
                }
            }
            else
            {
                lock (this)
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
                        msg.Write();
                        if (msg.Save)
                        {
                            string message = msg.Message;
                            Logger.TryRemoveDateFromLine(ref message);
                            CommandHandler.IsLoggingFromDevkitServer = true;
                            try
                            {
                                Logs.printLine(FormattingUtil.RemoveANSIFormatting(message));
                            }
                            finally
                            {
                                CommandHandler.IsLoggingFromDevkitServer = false;
                            }
                        }
                    }
                }
            }
            _writing = false;
        }
        if (_closeHandler.HasValue && _closeHandler.Value != uint.MaxValue)
        {
            Write("Closing from control signal \"" +
                  _closeHandler.Value switch
                  {
                      0 => "Ctrl + C",
                      1 => "Ctrl + Break",
                      2 => "Window Closed",
                      5 => "Logging Off",
                      6 => "Shutting Down",
                      _ => "Unknown: " + _closeHandler.Value
                  } + "\".", ConsoleColor.Red, true, Severity.Info);

            TimeUtility.InvokeAfterDelay(() => Application.Quit(0), 0.5f);
            _closeHandler = uint.MaxValue;
        }
    }

    public void Write(string input, ConsoleColor color, bool save, Severity severity)
    {
        OnOutput?.Invoke(ref input, ref color);
        _messageQueue.Add(new LogMessage(input, color, save));
        if (DevkitServerModule.IsMainThread)
            OutputTick();
    }
    private bool OnExitRequested(uint val)
    {
        _closeHandler = val;
        return true;
    }
    public void Close()
    {
        _cancellationRequested = true;
        _keyMonitor?.Abort();
        _inText = false;
        Console.SetCursorPosition(0, Console.CursorTop);
        SetConsoleCtrlHandler(OnExitRequested, false);
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

                msg.Write();
            }
        }
        _setup = false;
        FreeConsole();
    }


    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleOutputCP(uint wCodePageID);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleCP(uint wCodePageID);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleCtrlHandler(Func<uint, bool> handler, bool add);

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