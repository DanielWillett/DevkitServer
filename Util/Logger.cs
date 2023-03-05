using DevkitServer.Util.Terminals;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using StackCleaner;

namespace DevkitServer.Util;
internal static class Logger
{
    public static ITerminal Terminal;
    private const string TimeFormat = "yyyy-MM-dd hh:mm:ss";
    internal const string ANSIReset = "\u001b[39m";
    private static readonly StackTraceCleaner Cleaner;
    private static readonly char[] TrimChars = { '\n', '\r' };
    private static readonly char[] SplitChars = { '\n' };
    static Logger()
    {
        UnturnedLog.info("test");
        StackCleanerConfiguration config = new StackCleanerConfiguration
        {
            ColorFormatting = StackColorFormatType.ExtendedANSIColor,
            Colors = UnityColor32Config.Default,
            IncludeNamespaces = false,
#if DEBUG
            IncludeFileData = true
#else
            IncludeFileData = false
#endif
        };
#if !SERVER
        if (Application.platform is RuntimePlatform.WindowsPlayer or RuntimePlatform.WindowsEditor)
        {
            Terminal = DevkitServerModule.GameObjectHost.AddComponent<WindowsClientTerminal>();
        }
        else
        {
            Terminal = DevkitServerModule.GameObjectHost.AddComponent<BackgroundLoggingTerminal>();
            config.ColorFormatting = StackColorFormatType.ANSIColor;
        }
#else
        if (Application.platform is not RuntimePlatform.WindowsPlayer and not RuntimePlatform.WindowsEditor)
            config.ColorFormatting = StackColorFormatType.ANSIColor;
        Terminal = DevkitServerModule.GameObjectHost.AddComponent<ServerTerminal>();
#endif
        Cleaner = new StackTraceCleaner(config);
        UnturnedLog.info("test2");
    }
    internal static void InitLogger()
    {
        UnturnedLog.info("test3");
        Terminal.Init();
        Terminal.OnInput += OnTerminalInput;
    }
    internal static void CloseLogger()
    {
        Terminal.Close();
        Terminal.OnInput -= OnTerminalInput;
        if (Terminal is Object obj)
            Object.Destroy(obj);
    }
    public static void LogInfo(string message, ConsoleColor color = ConsoleColor.Gray)
    {
        Terminal.Write("[" + DateTime.UtcNow.ToString(TimeFormat) + "] [DEVKIT SERVER] [INFO]  " + message, color);
    }
    public static void LogWarning(string message, ConsoleColor color = ConsoleColor.Yellow, [CallerMemberName] string method = "")
    {
        Terminal.Write("[" + DateTime.UtcNow.ToString(TimeFormat) + "] [DEVKIT SERVER] [WARN]  " + "[" + method.ToUpperInvariant() + "] " + message, color);
    }
    public static void LogError(string message, ConsoleColor color = ConsoleColor.Red, [CallerMemberName] string method = "")
    {
        Terminal.Write("[" + DateTime.UtcNow.ToString(TimeFormat) + "] [DEVKIT SERVER] [ERROR] " + "[" + method.ToUpperInvariant() + "] " + message, color);
    }
    public static void LogError(Exception ex, bool cleanStack = true, [CallerMemberName] string method = "") => WriteExceptionIntl(ex, cleanStack, 0, method);
    private static void WriteExceptionIntl(Exception ex, bool cleanStack, int indent, string? method = null)
    {
        string ind = indent == 0 ? string.Empty : new string(' ', indent);
        bool inner = false;
        while (ex != null)
        {
            if (inner)
            {
                Terminal.Write(string.Empty, ConsoleColor.Red);
            }
            Terminal.Write(ind + (inner ? "Inner Exception: " : ((string.IsNullOrEmpty(method) ? string.Empty : ("[" + method!.ToUpper() + "] ")) + "Exception: ")) + ex.GetType().Name, ConsoleColor.Red);
            Terminal.Write(ind + (ex.Message ?? "No message"), ConsoleColor.DarkRed);
            if (ex is TypeLoadException t)
            {
                Terminal.Write(ind + "Type: " + t.TypeName, ConsoleColor.DarkRed);
            }
            else if (ex is ReflectionTypeLoadException t2)
            {
                Terminal.Write(ind + "Type load exceptions:", ConsoleColor.DarkRed);
                foreach (Exception ex2 in t2.LoaderExceptions)
                {
                    WriteExceptionIntl(ex2, cleanStack, indent + 1);
                }
            }
            else if (ex is AggregateException t3)
            {
                Terminal.Write(ind + "Inner exceptions:", ConsoleColor.DarkRed);
                foreach (Exception ex2 in t3.InnerExceptions)
                {
                    WriteExceptionIntl(ex2, cleanStack, indent + 1);
                }
            }
            if (ex.StackTrace != null)
            {
                if (cleanStack)
                {
                    string str = Cleaner.GetString(ex);
                    Terminal.Write(str, ConsoleColor.DarkGray);
                }
                /*
                Terminal.Write(indent != 0
                    ? string.Join(Environment.NewLine, ex.StackTrace.Split(SplitChars).Select(x => ind + x.Trim(TrimChars)))
                    : ex.StackTrace, ConsoleColor.DarkGray);*/
            }
            if (ex is AggregateException) break;
            ex = ex.InnerException!;
            inner = true;
        }
    }
    internal static unsafe string GetANSIForegroundString(ConsoleColor color)
    {
        int num = color switch
        {
            ConsoleColor.Black => 30,
            ConsoleColor.DarkRed => 31,
            ConsoleColor.DarkGreen => 32,
            ConsoleColor.DarkYellow => 33,
            ConsoleColor.DarkBlue => 34,
            ConsoleColor.DarkMagenta => 35,
            ConsoleColor.DarkCyan => 36,
            ConsoleColor.Gray => 37,
            ConsoleColor.DarkGray => 90,
            ConsoleColor.Red => 91,
            ConsoleColor.Green => 92,
            ConsoleColor.Yellow => 93,
            ConsoleColor.Blue => 94,
            ConsoleColor.Magenta => 95,
            ConsoleColor.Cyan => 96,
            ConsoleColor.White => 97,
            _ => 39
        };
        char* chrs = stackalloc char[5];
        chrs[0] = '\u001b';
        chrs[1] = '[';
        chrs[2] = (char)(num / 10 + 48);
        chrs[3] = (char)(num % 10 + 48);
        chrs[4] = 'm';

        return new string(chrs, 0, 5);
    }
    private static void OnTerminalInput(string input, ref bool shouldhandle)
    {
        if (!shouldhandle) return;
        LogInfo(input);
    }
}
