using DevkitServer.Configuration;
using DevkitServer.Core.Logging;
using DevkitServer.Core.Logging.Loggers;
using DevkitServer.Core.Logging.Terminals;
using DevkitServer.Levels;
using StackCleaner;
using LoadingError = (System.DateTime Timestamp, DevkitServer.API.Logging.Severity Severity, string Message, System.ConsoleColor? Color, string Method);
using LoadingException = (System.DateTime Timestamp, System.Exception Exception, string Method);

namespace DevkitServer.API.Logging;
public static class Logger
{
    private static bool _hasDsInited;
    public static object LogSync { get; } = new object();
    internal static IDevkitServerLogger DevkitServer { get; set; } = new CoreLogger("DEVKIT SERVER");
    internal static IDevkitServerLogger Unturned { get; set; } = new CoreLogger("UNTURNED");
    public static StackTraceCleaner StackCleaner { get; set; }
    internal static bool Debug => !_hasDsInited || DevkitServerConfig.Config.DebugLogging;
    static Logger()
    {
        StackCleaner = StackTraceCleaner.Default;
        Terminal = DevkitServerModule.UnturnedLoaded ? new BackgroundLoggingTerminal() : new ExternalLoggingTerminal();
    }

#nullable disable
    private static ITerminal _term;
#nullable restore

    internal static List<LoadingError>? LoadingErrors = new List<LoadingError>();
    internal static List<LoadingException>? LoadingExceptions = new List<LoadingException>();
    public static event TerminalPreReadDelegate? OnInputting;
    public static event TerminalPostReadDelegate? OnInputted;
    public static event TerminalPreWriteDelegate? OnOutputting;
    public static event TerminalPostWriteDelegate? OnOutputed;
    internal static bool HasLoadingErrors => LoadingErrors is { Count: > 0 };
    public static bool UseStackCleanerForExceptions { get; set; } //= true;
    public static ITerminal Terminal
    {
        get => _term;
        set
        {
            Exception? disposeEx = null, destroyEx = null;
            ITerminal? old;
            lock (LogSync)
            {
                old = Interlocked.Exchange(ref _term, value);
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
            }
            if (disposeEx != null && destroyEx != null)
            {
                DevkitServer.LogError("Set Terminal", new AggregateException($"Errors disposing of and destroying old terminal: {old!.GetType().Format()}", disposeEx, destroyEx));
            }
            else if (disposeEx != null)
            {
                DevkitServer.LogError("Set Terminal", new Exception($"Error disposing of old terminal: {old!.GetType().Format()}", disposeEx));
            }
            else if (destroyEx != null)
            {
                DevkitServer.LogError("Set Terminal", new Exception($"Error destroying old terminal: {old!.GetType().Format()}", destroyEx));
            }
        }
    }
    internal static void ClearLoadingErrors()
    {
        if (LoadingErrors != null)
        {
            if (LoadingErrors.Count > 0)
            {
                IEnumerable<(DateTime, Severity, string, ConsoleColor?, string)> logs = LoadingErrors.OrderBy(x => x.Timestamp);
                LoadingErrors = null;
                Terminal.Write("---- LAUNCH " + "WARNINGS".Colorize(ConsoleColor.Yellow) + "/" + "ERRORS".Colorize(ConsoleColor.Red) + " ----", ConsoleColor.Cyan, false, Severity.Info);
                foreach ((DateTime timestamp, Severity severity, string message, ConsoleColor? color, string method) in logs)
                {
                    Terminal.Write("[" + timestamp.ToString(LoggerExtensions.LogTimeFormat) + "] [" + (severity == Severity.Warning ? "WARN] " : "ERROR]") + " [DEVKIT SERVER] " +
                                   " [" + method.ToUpperInvariant() + "] " + message, color, false, severity);
                }
            }
            else
                LoadingErrors = null;
        }
        if (LoadingExceptions != null)
        {
            if (LoadingExceptions.Count > 0)
            {
                IEnumerable<(DateTime, Exception, string)> logs = LoadingExceptions.OrderBy(x => x.Timestamp);
                LoadingExceptions = null;
                Terminal.Write("---- LAUNCH " + "EXCEPTIONS".Colorize(ConsoleColor.DarkRed) + " ----", ConsoleColor.Cyan, false, Severity.Info);
                foreach ((_, Exception ex, string method) in logs)
                    LoggerExtensions.DefaultWriteException(method, Severity.Error, LoggerExtensions.DefaultErrorColor, Terminal, ex);
            }
            else
                LoadingExceptions = null;
        }
    }
    public static void InitLogger() { }
    internal static void InitForDevkitServer()
    {
        ANSIFileLogger.OpenANSILog();
        DevkitServerSystemConfig config = DevkitServerConfig.Config;
        _hasDsInited = true;

        StackCleanerConfiguration stackConfig = new StackCleanerConfiguration
        {
            ColorFormatting = StackColorFormatType.ExtendedANSIColor,
            Colors = UnityColor32Config.Default,
            IncludeNamespaces = false,
            IncludeLineData = true,
            IncludeSourceData = true,
            IncludeILOffset = true,
            IncludeFileData = false,
            IncludeAssemblyData = false
        };
#if DEBUG
        if (Debug)
        {
            stackConfig.IncludeFileData = true;
        }
#endif

        stackConfig.AddUniTaskSkippedTypes();

#if !SERVER
        bool isWindows = Application.platform is RuntimePlatform.WindowsPlayer or RuntimePlatform.WindowsEditor or RuntimePlatform.WindowsServer;

        stackConfig.ColorFormatting = isWindows && !config.DisableTerminal ? StackColorFormatType.ExtendedANSIColor : StackColorFormatType.None;
#endif

        if (!config.ConsoleVirtualSequenceSupport)
        {
            stackConfig.ColorFormatting = StackColorFormatType.None;
            stackConfig.Colors = Color4Config.Default;
        }
        else if (!config.ConsoleFullRGBSupport)
        {
            stackConfig.ColorFormatting = StackColorFormatType.ANSIColor;
            stackConfig.Colors = Color4Config.Default;
        }

#if SERVER
        Terminal = new ServerTerminal();
#else
        if (isWindows && !config.DisableTerminal)
            Terminal = DevkitServerModule.GameObjectHost.AddComponent<WindowsClientTerminal>();
        else
            Terminal = new BackgroundLoggingTerminal();
#endif

#if CLIENT
        LoggerExtensions.ReadInitialVanillaLogs();
#endif
        DevkitServer.LogInfo("LOGGER", $"Using color config: {stackConfig.ColorFormatting}, terminal: {Terminal.GetType().Name}.");
        StackCleaner = new StackTraceCleaner(stackConfig);
    }
    internal static void CloseLogger()
    {
        Terminal = null!;
        ANSIFileLogger.CloseANSILog();
    }
    private static void OnTerminalInput(ReadOnlySpan<char> input, ref bool shouldhandle)
    {
        OnInputting?.Invoke(input, ref shouldhandle);
        if (!shouldhandle) return;
        if (DevkitServerModule.Module == null || DevkitServerModule.IsMainThread)
            OnTerminalInputIntl(input);
        else
        {
            string om2 = input.ToString();
            DevkitServerUtility.QueueOnMainThread(() => OnTerminalInputIntl(om2));
        }
    }
    private static void OnTerminalOutput(ref ReadOnlySpan<char> outputMessage, ref ConsoleColor? color, Severity severity)
    {
        OnOutputting?.Invoke(ref outputMessage, ref color, severity);
        if (DevkitServerModule.Module == null || DevkitServerModule.IsMainThread)
            OnTerminalOutputIntl(outputMessage, color, severity);
        else
        {
            string om2 = outputMessage.ToString();
            ConsoleColor? cc2 = color;
            DevkitServerUtility.QueueOnMainThread(() => OnTerminalOutputIntl(om2, cc2, severity));
        }

        if (ANSIFileLogger.LogWriter is not { BaseStream.CanWrite: true })
            return;

        try
        {
            if (color.HasValue)
            {
                ReadOnlySpan<char> fg = FormattingUtil.GetTerminalColorSequenceString(color.Value, false);
                if (!outputMessage.StartsWith(fg))
                    ANSIFileLogger.LogWriter.Write(fg);
            }

            ANSIFileLogger.LogWriter.Write(outputMessage);

            if (!outputMessage.EndsWith(FormattingUtil.ForegroundResetSequence, StringComparison.Ordinal))
                ANSIFileLogger.LogWriter.WriteLine(FormattingUtil.ForegroundResetSequence);
            else
                ANSIFileLogger.LogWriter.WriteLine();
        }
        catch (Exception ex)
        {
            ANSIFileLogger.CloseANSILog();
            DevkitServer.LogError("Logger", ex, "Failed to write to ANSI log.");
        }
    }
    private static void OnTerminalOutputIntl(ReadOnlySpan<char> outputMessage, ConsoleColor? color, Severity severity)
    {
        if (DevkitServerModule.Module != null)
        {
            BackupLogs? logs = BackupLogs.Instance;
            logs?.GetOrAdd<LogQueue>().Add(outputMessage);
        }

        OnOutputed?.Invoke(outputMessage, color, severity);
    }
    private static void OnTerminalInputIntl(ReadOnlySpan<char> input)
    {
        OnInputted?.Invoke(input);
        DevkitServer.LogInfo("Input", input);
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
        else if (DevkitServerModule.IsMainThread || DevkitServerModule.Module == null)
            OnTerminalInputIntl(input);
        else
            DevkitServerUtility.QueueOnMainThread(() => OnTerminalInputIntl(input));
    }
    private sealed class LogQueue : IBackupLog
    {
        public List<string> Logs { get; } = new List<string>(1024);
        public string RelativeName => "main_log";
        public void Add(ReadOnlySpan<char> log)
        {
            lock (Logs)
                Logs.Add(log.ToString());
        }
        public void Write(TextWriter fileWriter)
        {
            lock (Logs)
            {
                for (int i = Logs.Count - 1; i >= 0; --i)
                {
                    fileWriter.WriteLine(FormattingUtil.RemoveVirtualTerminalSequences(Logs[i]));
                }
            }
        }
    }
}
