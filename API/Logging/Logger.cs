using DevkitServer.Configuration;
using StackCleaner;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using DevkitServer.API.Logging.Terminals;
#if SERVER
using DevkitServer.Levels;
#endif
#if CLIENT
using DevkitServer.Patches;
using HarmonyLib;
using System.Text;
#endif

namespace DevkitServer.API.Logging;
public static class Logger
{
    private static readonly bool IsExternalFeatureset;
#nullable disable
    private static ITerminal _term;
#nullable restore
    public static readonly StackTraceCleaner StackCleaner;
    private static List<(DateTime Timestamp, Severity Severity, string Message, ConsoleColor Color, string Method)>? _loadingErrors = new List<(DateTime, Severity, string, ConsoleColor, string)>();
    private static List<(DateTime Timestamp, Exception Exception, string Method)>? _loadingExceptions = new List<(DateTime, Exception, string)>();
    public static event TerminalPreReadDelegate? OnInputting;
    public static event TerminalPostReadDelegate? OnInputted;
    public static event TerminalPreWriteDelegate? OnOutputting;
    public static event TerminalPostWriteDelegate? OnOutputed;
    public static ITerminal Terminal
    {
        get => _term;
        set
        {
            ITerminal old = Interlocked.Exchange(ref _term, value);
            Exception? disposeEx = null;
            Exception? destroyEx = null;
            if (old != null)
            {
                old.Close();
                old.OnInput -= OnTerminalInput;
                old.OnOutput -= OnTerminalOutput;
                try
                {
                    if (old is IDisposable disposable)
                        disposable.Dispose();
                }
                catch (Exception ex)
                {
                    disposeEx = ex;
                }

                if (DevkitServerModule.UnityLoaded)
                {
                    try
                    {
                        if (old is Object obj)
                            Object.Destroy(obj);
                    }
                    catch (Exception ex)
                    {
                        destroyEx = ex;
                    }
                }
            }
            if (value != null)
            {
                value.Init();
                value.OnInput += OnTerminalInput;
                value.OnOutput += OnTerminalOutput;
            }
            if (disposeEx != null && destroyEx != null)
            {
                LogError(new AggregateException($"Errors disposing of and destroying old terminal: {old!.GetType().Format()}", disposeEx, destroyEx), method: "SET TERMINAL");
            }
            else if (disposeEx != null)
            {
                LogError(new Exception($"Error disposing of old terminal: {old!.GetType().Format()}", disposeEx), method: "SET TERMINAL");
            }
            else if (destroyEx != null)
            {
                LogError(new Exception($"Error destroying old terminal: {old!.GetType().Format()}", destroyEx), method: "SET TERMINAL");
            }
        }
    }
    private const string TimeFormat = "yyyy-MM-dd hh:mm:ss";
    static Logger()
    {
        try
        {
            IsExternalFeatureset = !DevkitServerModule.UnturnedLoaded;
            StackCleanerConfiguration config = new StackCleanerConfiguration
            {
                ColorFormatting = StackColorFormatType.ExtendedANSIColor,
                Colors = Type.GetType("System.Drawing.Color, System.Drawing", false, false) == null ? UnityColor32Config.Default : Color32Config.Default,
                IncludeNamespaces = false,
#if DEBUG
                IncludeFileData = true,
                IncludeSourceData = true,
                IncludeILOffset = true
#else
                IncludeFileData = false,
                IncludeSourceData = false,
                IncludeILOffset = false
#endif
            };
            if (IsExternalFeatureset)
            {
                config.ColorFormatting = StackColorFormatType.ExtendedANSIColor;
            }
            else
            {
#if !SERVER
                if (Application.platform is RuntimePlatform.WindowsPlayer or RuntimePlatform.WindowsEditor)
                    config.ColorFormatting = StackColorFormatType.ExtendedANSIColor;
                else
                    config.ColorFormatting = StackColorFormatType.None;
#else
            if (Application.platform is not RuntimePlatform.WindowsPlayer and not RuntimePlatform.WindowsEditor)
                config.ColorFormatting = StackColorFormatType.ANSIColor;
#endif
                if (!DevkitServerConfig.Config.ConsoleVisualANSISupport)
                {
                    config.ColorFormatting = StackColorFormatType.None;
                    config.Colors = Color4Config.Default;
                }
                else if (!DevkitServerConfig.Config.ConsoleExtendedVisualANSISupport)
                {
                    config.ColorFormatting = StackColorFormatType.ANSIColor;
                    config.Colors = Color4Config.Default;
                }
            }

            StackCleaner = new StackTraceCleaner(config);
            if (!IsExternalFeatureset)
            {
#if !SERVER
                if (Application.platform is RuntimePlatform.WindowsPlayer or RuntimePlatform.WindowsEditor)
                {
                    Terminal = DevkitServerModule.GameObjectHost.AddComponent<WindowsClientTerminal>();
                    LogInfo("Initalized Windows terminal.", ConsoleColor.DarkCyan);
                }
                else
                {
                    Terminal = DevkitServerModule.GameObjectHost.AddComponent<BackgroundLoggingTerminal>();
                    LogInfo("Did not initialize a terminal.", ConsoleColor.DarkCyan);
                }
#else
                Terminal = DevkitServerModule.GameObjectHost.AddComponent<ServerTerminal>();
#endif
            }
            else
            {
                Terminal = new ExternalLoggingTerminal();
            }

            string log = "[DEVKIT SERVER] Loading logger with log type: " + Terminal.GetType().Format() + " colorized with: " + config.ColorFormatting.Format() + ".";
            if (IsExternalFeatureset)
                Console.WriteLine(log);
            else
                CommandWindow.Log(log);
        }
        catch (Exception ex)
        {
            Console.WriteLine("[DEVKIT SERVER] Error initializing logger.");
            Console.WriteLine("[DEVKIT SERVER] " + Environment.NewLine + ex);

            StackCleanerConfiguration config = new StackCleanerConfiguration
            {
                ColorFormatting = StackColorFormatType.None,
                Colors = Type.GetType("System.Drawing.Color, System.Drawing", false, false) == null ? UnityColor32Config.Default : Color32Config.Default,
                IncludeNamespaces = true,
                IncludeFileData = true,
                IncludeSourceData = true,
                IncludeILOffset = true
            };
            StackCleaner = new StackTraceCleaner(config);
        }
    }
    internal static void ClearLoadingErrors()
    {
        if (_loadingErrors != null)
        {
            if (_loadingErrors.Count > 0)
            {
                IEnumerable<(DateTime, Severity, string, ConsoleColor, string)> logs = _loadingErrors.OrderBy(x => x.Timestamp);
                _loadingErrors = null;
                Terminal.Write("---- LAUNCH " + "WARNINGS".Colorize(ConsoleColor.Yellow) + "/" + "ERRORS".Colorize(ConsoleColor.Red) + " ----", ConsoleColor.Cyan, false, Severity.Info);
                foreach ((DateTime timestamp, Severity severity, string message, ConsoleColor color, string method) in logs)
                {
                    Terminal.Write("[" + timestamp.ToString(TimeFormat) + "] [DEVKIT SERVER] [" + (severity == Severity.Warning ? "WARN] " : "ERROR]") +
                                   " [" + method.ToUpperInvariant() + "] " + message, color, false, severity);
                }
            }
            else
                _loadingErrors = null;
        }
        if (_loadingExceptions != null)
        {
            if (_loadingExceptions.Count > 0)
            {
                IEnumerable<(DateTime, Exception, string)> logs = _loadingExceptions.OrderBy(x => x.Timestamp);
                _loadingExceptions = null;
                Terminal.Write("---- LAUNCH " + "EXCEPTIONS".Colorize(ConsoleColor.DarkRed) + " ----", ConsoleColor.Cyan, false, Severity.Info);
                foreach ((DateTime timestamp, Exception ex, string method) in logs)
                    WriteExceptionIntl(ex, true, 0, timestamp, method);
            }
            else
                _loadingExceptions = null;
        }
    }
    internal static void InitLogger()
    {
#if CLIENT
        ReadInitialVanillaLogs();
#endif
    }
#if CLIENT
    internal static void ReadInitialVanillaLogs()
    {
        try
        {
            string log = Logs.getLogFilePath();
            if (File.Exists(log))
            {
                using FileStream str = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using StreamReader reader = new StreamReader(str, Encoding.GetEncoding(65001, new EncoderReplacementFallback(), new DecoderReplacementFallback()));
                while (reader.ReadLine() is { } line)
                {
                    OnLinePrinted(ref line);
                }
            }
        }
        catch (Exception ex)
        {
            LogWarning("Failed to fetch vanilla logs.");
            LogError(ex);
        }
    }
    internal static void PostPatcherSetupInitLogger()
    {
        if (IsExternalFeatureset) throw new InvalidOperationException("External access violation.");
        try
        {
            MethodInfo? method = typeof(LogFile).GetMethod(nameof(LogFile.writeLine), BindingFlags.Instance | BindingFlags.Public);
            if (method != null)
                PatchesMain.Patcher.Patch(method, prefix: new HarmonyMethod(typeof(Logger).GetMethod(nameof(OnLinePrinted), BindingFlags.Static | BindingFlags.NonPublic)));
        }
        catch (Exception ex)
        {
            LogWarning("Failed to patch vanilla logs.");
            LogError(ex);
        }
    }
    private static void OnLinePrinted(ref string line)
    {
        if (Terminal is not { IsComittingToUnturnedLog: false })
            return;
        line = FormattingUtil.RemoveANSIFormatting(line);
        string msg2 = line;
        TryRemoveDateFromLine(ref msg2);
        Terminal.Write("[" + DateTime.UtcNow.ToString(TimeFormat) + "] [UNTURNED]      [LOG]   " + msg2, ConsoleColor.DarkGray, false, Severity.Info);
    }
#endif
    internal static void TryRemoveDateFromLine(ref string message)
    {
        int ind = message.Length > 22 ? message.IndexOf(']', 0, 22) : -1;
        if (ind != -1)
            message = message.Substring(ind + (message.Length > ind + 2 && message[ind + 1] == ' ' ? 2 : message.Length > ind + 1 ? 1 : 0));
    }
    internal static void CloseLogger()
    {
        Terminal = null!;
    }
    public static void Log(Severity severity, string message, ConsoleColor? color = null)
    {
        switch (severity)
        {
            case Severity.Error:
            case Severity.Fatal:
                LogError(message, color ?? ConsoleColor.Red);
                break;
            case Severity.Warning:
                LogWarning(message, color ?? ConsoleColor.Yellow);
                break;
            case Severity.Debug:
                LogDebug(message, color ?? ConsoleColor.DarkGray);
                break;
            default: // Info
                LogInfo(message, color ?? ConsoleColor.DarkCyan);
                break;
        }
    }
    internal static void CoreLog(string message, string core, string? type = null, ConsoleColor? color = null, Severity severity = Severity.Info)
    {
        color ??= severity switch
        {
            Severity.Debug => ConsoleColor.DarkGray,
            Severity.Info => ConsoleColor.DarkCyan,
            Severity.Warning => ConsoleColor.Yellow,
            Severity.Error or Severity.Fatal => ConsoleColor.Red,
            _ => ConsoleColor.Gray
        };
        if (string.IsNullOrEmpty(type))
        {
            type = severity switch
            {
                Severity.Debug => "DEBUG",
                Severity.Warning => "WARN",
                Severity.Error => "ERROR",
                Severity.Fatal => "FATAL",
                _ => "INFO"
            };
        }
        ReplaceResetsWithConsoleColor(ref message, color.Value);
        Terminal.Write("[" + DateTime.UtcNow.ToString(TimeFormat) + "] " +
                       "[" + core.ToUpperInvariant() + "]"
                       + new string(' ', Math.Max(1, 14 - core.Length))
                       + "[" + type!.ToUpperInvariant() + "]" + new string(' ', Math.Max(1, 6 - type.Length)) + message,
            color.Value, true, severity);
    }
    /// <summary>
    /// Replaces any ANSI reset strings with the ANSI string for the provided console color.
    /// </summary>
    public static void ReplaceResetsWithConsoleColor(ref string message, ConsoleColor color)
    {
        if (color != ConsoleColor.Gray && message.IndexOf(FormattingUtil.ConsoleEscapeCharacter) != -1)
        {
            message = message.Replace(FormattingUtil.ANSIForegroundReset, FormattingUtil.GetANSIString(color, false)) + FormattingUtil.ANSIForegroundReset;
        }
    }
    [Conditional("DEBUG")]
    public static void LogDebug(string message, ConsoleColor color = ConsoleColor.DarkGray)
    {
        if (string.IsNullOrWhiteSpace(message))
            Terminal.Write(string.Empty, color, true, Severity.Debug);
        else
        {
            ReplaceResetsWithConsoleColor(ref message, color);
            Terminal.Write("[" + DateTime.UtcNow.ToString(TimeFormat) + "] [DEVKIT SERVER] [DEBUG] " + message, color, true, Severity.Debug);
        }
    }
    public static void LogInfo(string message, ConsoleColor color = ConsoleColor.DarkCyan)
    {
        if (string.IsNullOrWhiteSpace(message))
            Terminal.Write(string.Empty, color, true, Severity.Info);
        else
        {
            ReplaceResetsWithConsoleColor(ref message, color);
            Terminal.Write("[" + DateTime.UtcNow.ToString(TimeFormat) + "] [DEVKIT SERVER] [INFO]  " + message, color, true, Severity.Info);
        }
    }
    public static void LogWarning(string message, ConsoleColor color = ConsoleColor.Yellow, [CallerMemberName] string method = "")
    {
        if (string.IsNullOrWhiteSpace(message))
            Terminal.Write(string.Empty, color, true, Severity.Warning);
        else
        {
            method ??= string.Empty;
            method = FormattingUtil.SpaceProperCaseString(method);
            DateTime now = DateTime.UtcNow;
            ReplaceResetsWithConsoleColor(ref message, color);
            if (!Level.isLoaded)
                _loadingErrors?.Add((now, Severity.Warning, message, color, method));
            Terminal.Write("[" + now.ToString(TimeFormat) + "] [DEVKIT SERVER] [WARN]  [" + method.ToUpperInvariant() + "] " + message, color, true, Severity.Warning);
        }
    }
    public static void LogError(string message, ConsoleColor color = ConsoleColor.Red, [CallerMemberName] string method = "")
    {
        if (string.IsNullOrWhiteSpace(message))
            Terminal.Write(string.Empty, color, true, Severity.Error);
        else
        {
            method ??= string.Empty;
            method = FormattingUtil.SpaceProperCaseString(method);
            DateTime now = DateTime.UtcNow;
            ReplaceResetsWithConsoleColor(ref message, color);
            if (!Level.isLoaded)
                _loadingErrors?.Add((now, Severity.Error, message, color, method));
            Terminal.Write("[" + now.ToString(TimeFormat) + "] [DEVKIT SERVER] [ERROR] [" + method.ToUpperInvariant() + "] " + message, color, true, Severity.Error);
        }
    }
    public static void DumpJson<T>(T obj, ConsoleColor color = ConsoleColor.DarkGray, bool condensed = false)
    {
        if (obj == null)
            Terminal.Write("null", color, true, Severity.Debug);
        else
        {
            try
            {
                Terminal.Write(JsonSerializer.Serialize(obj, obj.GetType(), condensed ? DevkitServerConfig.CondensedSerializerSettings : DevkitServerConfig.SerializerSettings),
                    color, true, Severity.Debug);
            }
            catch (Exception ex)
            {
                LogError("Error serializing " + obj + ".");
                LogError(ex);
            }
        }
    }
    public static void DumpGameObject(GameObject go, ConsoleColor color = ConsoleColor.White)
    {
        if (!DevkitServerModule.UnityLoaded)
            throw new InvalidOperationException("External access violation.");
        LogInfo("Gameobject Dump: \"" + go.name + "\":", color);
        Terminal.Write("Transform:", color, true, Severity.Debug);
        Terminal.Write($" Parent: {(go.transform.parent == null ? "none" : go.transform.parent.name)}", color, true, Severity.Debug);
        Terminal.Write($" Position: {go.transform.position:F2}", color, true, Severity.Debug);
        Terminal.Write($" Rotation: {go.transform.rotation.eulerAngles:F2}", color, true, Severity.Debug);
        Terminal.Write($" Scale:    {go.transform.localScale:F2}", color, true, Severity.Debug);
        Terminal.Write("Components:", color, true, Severity.Debug);
        Component[] comps = go.GetComponents<Component>();
        Terminal.Write(" ========================================", color, true, Severity.Debug);
        foreach (Component comp in comps)
        {
            Terminal.Write($" Parent: {comp.transform.gameObject.name}", color, true, Severity.Debug);
            Terminal.Write($" Type: {comp.GetType().Format()}{FormattingUtil.GetANSIString(color, false)}", color, true, Severity.Debug);
            Terminal.Write(" ========================================", color, true, Severity.Debug);
        }
        int childCt = go.transform.childCount;
        Terminal.Write($"Children: {childCt}:", color, true, Severity.Debug);
        for (int i = 0; i < childCt; ++i)
        {
            DumpGameObject(go.transform.GetChild(i).gameObject, color);
        }
    }
    private static string? GetErrorMessage(Exception ex)
    {
        return ex.Message != null && ex is BadImageFormatException && ex.Message.Equals("Method has zero rva", StringComparison.Ordinal) ? "Method has no body (Method has zero rva)" : ex.Message;
    }
    public static void LogError(Exception ex, bool cleanStack = true, [CallerMemberName] string method = "") => WriteExceptionIntl(ex, cleanStack, 0, DateTime.UtcNow, method);
    private static void WriteExceptionIntl(Exception ex, bool cleanStack, int indent, DateTime timestamp, string? method = null)
    {
        if (method != null)
            method = FormattingUtil.SpaceProperCaseString(method);
        string ind = indent == 0 ? string.Empty : new string(' ', indent);
        bool inner = indent > 0;
        while (ex != null)
        {
            if (inner)
            {
                Terminal.Write(string.Empty, ConsoleColor.Red, true, Severity.Error);
            }

            string message = GetErrorMessage(ex) ?? "No message";
            Terminal.Write(ind + (
                inner
                    ? "Inner Exception: "
                    : "[" + timestamp.ToString(TimeFormat) + "]" + (string.IsNullOrEmpty(method) ? string.Empty : " [" + method!.ToUpperInvariant() + "] ") + "Exception: ")
                               + ex.GetType().Format() + FormattingUtil.GetANSIString(ConsoleColor.Red, false) + ".", ConsoleColor.Red, true, Severity.Error);
            Terminal.Write(ind + message, ConsoleColor.DarkRed, true, Severity.Error);
            if (!Level.isLoaded)
                _loadingErrors?.Add((DateTime.UtcNow, Severity.Error, message, ConsoleColor.DarkRed, method ?? string.Empty));
            if (ex is TypeLoadException t)
            {
                Terminal.Write(ind + "Type: " + t.TypeName, ConsoleColor.DarkRed, true, Severity.Error);
            }
            else if (ex is ReflectionTypeLoadException t2)
            {
                Terminal.Write(ind + "Type load exceptions:", ConsoleColor.DarkRed, true, Severity.Error);
                foreach (Exception ex2 in t2.LoaderExceptions)
                {
                    WriteExceptionIntl(ex2, cleanStack, indent + 1, timestamp);
                }
            }
            else if (ex is TargetInvocationException { InnerException: not null } t4)
            {
                Terminal.Write(ind + "Invoked exception:", ConsoleColor.DarkRed, true, Severity.Error);
                WriteExceptionIntl(t4.InnerException, cleanStack, indent + 1, timestamp);
            }
            else if (ex is AggregateException t3)
            {
                Terminal.Write(ind + "Inner exceptions:", ConsoleColor.DarkRed, true, Severity.Error);
                foreach (Exception ex2 in t3.InnerExceptions)
                {
                    WriteExceptionIntl(ex2, cleanStack, indent + 1, timestamp);
                }
            }
            if (ex.StackTrace != null)
            {
                if (cleanStack)
                {
                    string str;
                    try
                    {
                        str = StackCleaner.GetString(ex);
                    }
                    catch (Exception ex2)
                    {
                        LogDebug("Ran into an error cleaning a stack trace: " + ex2.Message + " (" + ex2.GetType().Format() + ").");
                        str = ex.StackTrace;
                    }

                    Terminal.Write(str, ConsoleColor.DarkGray, true, Severity.Error);
                }
                /*
                Terminal.Write(indent != 0
                    ? string.Join(Environment.NewLine, ex.StackTrace.Split(SplitChars).Select(x => ind + x.Trim(TrimChars)))
                    : ex.StackTrace, ConsoleColor.DarkGray);*/
            }
            if (ex is AggregateException or TargetInvocationException) break;
            ex = ex.InnerException!;
            inner = true;
        }
    }
    private static void OnTerminalInput(string input, ref bool shouldhandle)
    {
        OnInputting?.Invoke(input, ref shouldhandle);
        if (!shouldhandle) return;
        if (DevkitServerModule.IsMainThread || IsExternalFeatureset)
            OnTerminalInputIntl(input);
        else
            DevkitServerUtility.QueueOnMainThread(() => OnTerminalInputIntl(input));
    }
    private static void OnTerminalOutput(ref string outputMessage, ref ConsoleColor color)
    {
        OnOutputting?.Invoke(ref outputMessage, ref color);
        if (DevkitServerModule.IsMainThread || IsExternalFeatureset)
            OnTerminalOutputIntl(outputMessage, color);
        else
        {
            string om2 = outputMessage;
            ConsoleColor cc2 = color;
            DevkitServerUtility.QueueOnMainThread(() => OnTerminalOutputIntl(om2, cc2));
        }
    }
    private static void OnTerminalOutputIntl(string outputMessage, ConsoleColor color)
    {
#if SERVER
        if (!IsExternalFeatureset)
        {
            BackupLogs? logs = BackupLogs.Instance;
            logs?.GetOrAdd<LogQueue>().Logs.Add(outputMessage);
        }
#endif
        OnOutputed?.Invoke(outputMessage, color);
    }
    private static void OnTerminalInputIntl(string input)
    {
        OnInputted?.Invoke(input);
        LogInfo(input);
    }
    /// <summary>
    /// Simulates input through the terminal (the same as typing into the terminal and pressing enter).
    /// </summary>
    /// <param name="skipEventCheck">Doesn't call <see cref="OnInputting"/> when <see langword="true"/>.</param>
    public static void SimulateTerminalInput(string input, bool skipEventCheck = false)
    {
        if (!skipEventCheck)
        {
            bool shouldHandle = true;
            OnTerminalInput(input, ref shouldHandle);
        }
        else if (DevkitServerModule.IsMainThread || IsExternalFeatureset)
            OnTerminalInputIntl(input);
        else
            DevkitServerUtility.QueueOnMainThread(() => OnTerminalInputIntl(input));
    }
#if SERVER
    private sealed class LogQueue : IBackupLog
    {
        public List<string> Logs { get; } = new List<string>(1024);
        public string RelativeName => "main_log";
        public void Write(TextWriter fileWriter)
        {
            for (int i = Logs.Count - 1; i >= 0; --i)
            {
                fileWriter.WriteLine(FormattingUtil.RemoveANSIFormatting(Logs[i]));
            }
        }
    }
#endif
}

public enum Severity : byte
{
    Debug,
    Info,
    Warning,
    Error,
    Fatal
}