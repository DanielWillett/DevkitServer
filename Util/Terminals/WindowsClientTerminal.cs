#if CLIENT
using System.Runtime.InteropServices;
using System.Text;
using ThreadPriority = System.Threading.ThreadPriority;

namespace DevkitServer.Util.Terminals;
internal sealed class WindowsClientTerminal : MonoBehaviour, ITerminal
{
    private const uint UTF8_CODEPAGE = 65001U;
    private const int STD_OUTPUT_HANDLE = -11;

    public event TerminalReadDelegate? OnInput;
    public event TerminalWriteDelegate? OnOutput;
    private readonly Queue<LogMessage> _messageQueue = new Queue<LogMessage>();
    private readonly Queue<string> _inputQueue = new Queue<string>();
    private volatile bool _cancellationRequested;
    private Thread _keyMonitor;
    private bool setup;
    private bool inText;
    public void Init()
    {
        FreeConsole();
        AllocConsole();
        SetConsoleOutputCP(UTF8_CODEPAGE);
        SetConsoleCP(UTF8_CODEPAGE);
        IntPtr handle = GetStdHandle(STD_OUTPUT_HANDLE);
        GetConsoleMode(handle, out uint mode1);
        SetConsoleMode(handle, mode1 | 0x0004); // enable extended ANSI support for older terminals
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
        setup = true;
    }
    void Update()
    {
        if (setup)
        {
            while (_inputQueue.Count > 0)
            {
                string deq = _inputQueue.Dequeue();
                bool shouldHandle = true;
                OnInput?.Invoke(deq, ref shouldHandle);
            }
            if (_messageQueue.Count > 0)
            {
                ConsoleColor temp = Console.ForegroundColor;
                if (inText)
                {
                    int x = Console.CursorLeft;
                    int y = Console.CursorTop;
                    Console.MoveBufferArea(0, y, Console.WindowWidth, 1, 0, y + 1);
                    Console.SetCursorPosition(0, y);
                    while (_messageQueue.Count > 0)
                    {
                        _messageQueue.Dequeue().Write();
                    }
                    Console.SetCursorPosition(x, y + 1);
                }
                else
                {
                    while (_messageQueue.Count > 0)
                    {
                        _messageQueue.Dequeue().Write();
                    }
                }
                Console.ForegroundColor = temp;
            }
        }
    }
    public void Write(string input, ConsoleColor color)
    {
        OnOutput?.Invoke(ref input, ref color);
        _messageQueue.Enqueue(new LogMessage(input, color));
    }
    public void Close()
    {
        _cancellationRequested = true;
        _keyMonitor?.Abort();
        inText = false;
        Console.SetCursorPosition(0, Console.CursorTop);
        if (_messageQueue.Count > 0)
        {
            ConsoleColor temp = Console.ForegroundColor;
            while (_messageQueue.Count > 0)
            {
                _messageQueue.Dequeue().Write();
            }
            Console.ForegroundColor = temp;
        }
        setup = false;
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
    
    private void MonitorKeypress()
    {
        while (!_cancellationRequested)
        {
            string current = string.Empty;
            int oldlength = 0;
            while (true)
            {
                ConsoleKeyInfo key = Console.ReadKey();
                if (current.Length == 0)
                {
                    if (key.Key is (< ConsoleKey.NumPad0 or > ConsoleKey.NumPad9) and (< ConsoleKey.A or > ConsoleKey.Z) and (< ConsoleKey.Oem1 or > ConsoleKey.Oem8))
                    {
                        Console.SetCursorPosition(0, Console.CursorTop);
                        break;
                    }

                    inText = true;
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
                        inText = false;
                    }
                }
                else if (key.Key == ConsoleKey.Enter)
                {
                    Console.SetCursorPosition(0, Console.CursorTop);
                    Console.Write(new string(' ', Console.BufferWidth));
                    break;
                }
                else if (key.KeyChar == 0 || key.Key == ConsoleKey.OemClear || (key.Key >= ConsoleKey.LeftArrow && key.Key <= ConsoleKey.DownArrow))
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
            if (current.Length > 0)
            {
                _inputQueue.Enqueue(current);
            }
            inText = false;
        }
    }
    internal readonly struct LogMessage
    {
        public readonly string Message;
        public readonly ConsoleColor Color;
        public LogMessage(string message, ConsoleColor color)
        {
            Message = message;
            Color = color;
        }
        public void Write()
        {
            Console.ForegroundColor = Color;
            Console.WriteLine(Message);
        }
    }
}
#endif