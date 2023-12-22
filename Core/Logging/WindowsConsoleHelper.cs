using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DevkitServer.Core.Logging;
internal static class WindowsConsoleHelper
{
    internal const int StdOutputHandle = -11;
    internal const int StdInputHandle = -10;

    internal static event Func<uint, bool> OnConsoleControlPressed
    {
        add
        {
            SetConsoleCtrlHandler(value, true);
            PrintLastWin32Error("SetConsoleCtrlHandler", 0x7F);
        }
        remove
        {
            SetConsoleCtrlHandler(value, false);
            PrintLastWin32Error("SetConsoleCtrlHandler", 0x7F);
        }
    }

    internal static void SetUTF8CodePage()
    {
        const uint utf8 = 65001U;

        if (!SetConsoleOutputCP(utf8))
        {
            PrintLastWin32Error("SetConsoleOutputCP");
        }
        if (!SetConsoleCP(utf8))
        {
            PrintLastWin32Error("SetConsoleCP");
        }
    }

    internal static unsafe void ConfigureConsoleO(Func<StandardConsoleOutputFlags, StandardConsoleOutputFlags> configure)
    {
        void* handle = GetStdHandle(StdOutputHandle);
        if (handle == null)
            PrintLastWin32Error("GetStdHandle", 0x7F);

        if (!GetConsoleMode(handle, out uint oldOutputMode))
        {
            PrintLastWin32Error("GetConsoleMode", 0x7F);
            oldOutputMode = (uint)StandardConsoleOutputFlags.DEFAULT_WINDOWS;
        }

        StandardConsoleOutputFlags outputMode = configure((StandardConsoleOutputFlags)oldOutputMode);

        if (oldOutputMode != (uint)outputMode)
        {
            if (!SetConsoleMode(handle, (uint)outputMode))
                PrintLastWin32Error("SetConsoleMode", 0x7F);
        }
#if DEBUG
        CommandWindow.Log($"Using console output mode: {outputMode}.");
#endif
    }
    internal static unsafe void ConfigureConsoleI(Func<StandardConsoleInputFlags, StandardConsoleInputFlags> configure)
    {
        void* handle = GetStdHandle(StdInputHandle);
        if (handle == null)
            PrintLastWin32Error("GetStdHandle", 0x7F);

        if (!GetConsoleMode(handle, out uint oldInputMode))
        {
            PrintLastWin32Error("GetConsoleMode", 0x7F);
            oldInputMode = (uint)StandardConsoleInputFlags.DEFAULT_WINDOWS;
        }
        PrintLastWin32Error("GetConsoleMode", 0x7F);

        StandardConsoleInputFlags inputMode = configure((StandardConsoleInputFlags)oldInputMode);

        if (oldInputMode != (uint)inputMode)
        {
            if (!SetConsoleMode(handle, (uint)inputMode))
                PrintLastWin32Error("SetConsoleMode", 0x7F);
        }
#if DEBUG
        CommandWindow.Log($"Using console input mode: {inputMode}.");
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ThrowLastWin32Error(string source, int ignore = 0)
    {
        int err = Marshal.GetLastWin32Error();
        if (err == 0x00)
            return;

        if (err == ignore)
        {
#if DEBUG
            CommandWindow.Log($"Win32 error ignored: {err:X8} from [kernel32.dll]::{source}.");
#endif
            return;
        }

        CommandWindow.LogError($"Win32 error {err:X8} thrown from [kernel32.dll]::{source}.");

        throw new Win32Exception(err);
    }

    internal static void PrintLastWin32Error(string source, int ignore = 0)
    {
        int err = Marshal.GetLastWin32Error();
        if (err != 0x00 && err != ignore)
        {
            CommandWindow.Log($"Win32 error ignored: {err:X8} from [kernel32.dll]::{source}.");
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern unsafe void* GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern unsafe bool SetConsoleMode(void* hConsoleHandle, uint dwMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern unsafe bool GetConsoleMode(void* hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool FreeConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetConsoleOutputCP(uint wCodePageID);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetConsoleCP(uint wCodePageID);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetConsoleCtrlHandler(Func<uint, bool> handler, bool add);
}

// ReSharper disable InconsistentNaming

// https://learn.microsoft.com/en-us/windows/console/setconsolemode#parameters
[Flags]
internal enum StandardConsoleInputFlags : uint
{
    DEFAULT_WINDOWS = ENABLE_PROCESSED_INPUT | ENABLE_LINE_INPUT | ENABLE_ECHO_INPUT | ENABLE_MOUSE_INPUT | ENABLE_EXTENDED_FLAGS | ENABLE_AUTO_POSITION,

    ENABLE_PROCESSED_INPUT = 0x0001,
    ENABLE_LINE_INPUT = 0x0002,
    ENABLE_ECHO_INPUT = 0x0004,
    ENABLE_WINDOW_INPUT = 0x0008,
    ENABLE_MOUSE_INPUT = 0x0010,
    ENABLE_INSERT_MODE = 0x0020,
    ENABLE_QUICK_EDIT_MODE = 0x0040,
    ENABLE_EXTENDED_FLAGS = 0x0080,
    ENABLE_AUTO_POSITION = 0x0100,
    ENABLE_VIRTUAL_TERMINAL_INPUT = 0x0200
}

[Flags]
internal enum StandardConsoleOutputFlags : uint
{
    DEFAULT_WINDOWS = ENABLE_PROCESSED_OUTPUT | ENABLE_WRAP_AT_EOL_OUTPUT,

    ENABLE_PROCESSED_OUTPUT = 0x0001,
    ENABLE_WRAP_AT_EOL_OUTPUT = 0x0002,
    ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004,
    DISABLE_NEWLINE_AUTO_RETURN = 0x0008,
    ENABLE_LVB_GRID_WORLDWIDE = 0x0010
}
// ReSharper restore InconsistentNaming