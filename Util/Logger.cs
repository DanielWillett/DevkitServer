﻿using DevkitServer.Multiplayer.Networking;
using DevkitServer.Util.Terminals;
using StackCleaner;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using DevkitServer.API;
using DevkitServer.Configuration;
using DevkitServer.Patches;
using HarmonyLib;

namespace DevkitServer.Util;
internal static class Logger
{
#nullable disable
    private static ITerminal _term;
#nullable restore
    public static readonly StackTraceCleaner StackCleaner;
    private static readonly NetCall<string, Severity> SendLogMessage = new NetCall<string, Severity>((ushort)NetCalls.SendLog);
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
            if (old != null)
            {
                old.Close();
                old.OnInput -= OnTerminalInput;
                old.OnOutput -= OnTerminalOutput;
            }
            _term.Init();
            _term.OnInput += OnTerminalInput;
            _term.OnOutput += OnTerminalOutput;
        }
    }
    private const string TimeFormat = "yyyy-MM-dd hh:mm:ss";
    static Logger()
    {
        StackCleanerConfiguration config = new StackCleanerConfiguration
        {
            ColorFormatting = StackColorFormatType.ExtendedANSIColor,
            Colors = UnityColor32Config.Default,
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

        StackCleaner = new StackTraceCleaner(config);
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
        
        CommandWindow.Log("Loading logger with log type: " + Terminal.GetType().Format() + " colorized with: " + config.ColorFormatting.Format() + ".");
    }
    internal static void InitLogger()
    {
        // empty
    }
#if CLIENT
    internal static void PostPatcherSetupInitLogger()
    {
        try
        {
            MethodInfo? method = typeof(LogFile).GetMethod(nameof(LogFile.writeLine), BindingFlags.Instance | BindingFlags.Public);
            if (method != null)
                PatchesMain.Patcher.Patch(method, prefix: new HarmonyMethod(typeof(Logger).GetMethod(nameof(OnLinePrinted), BindingFlags.Static | BindingFlags.NonPublic)));

            string log = Logs.getLogFilePath();
            if (File.Exists(log))
            {
                using FileStream str = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using StreamReader reader = new StreamReader(str, System.Text.Encoding.GetEncoding(65001, new EncoderReplacementFallback(), new DecoderReplacementFallback()));
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
    private static void OnLinePrinted(ref string line)
    {
        if (Terminal is not { IsCommitingToUnturnedLog: false })
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
            message = message.Substring(ind + (message.Length > ind + 2 && message[ind + 1] == ' ' ? 2 : (message.Length > ind + 1 ? 1 : 0)));
    }
    internal static void CloseLogger()
    {
        Terminal.Close();
        Terminal.OnInput -= OnTerminalInput;
        if (Terminal is Object obj)
            Object.Destroy(obj);
    }
#if SERVER
    public static void SendLog(ITransportConnection connection, string message, Severity severity)
    {
        SendLogMessage.Invoke(connection, message, severity);
    }
    public static void SendLog(List<ITransportConnection> connections, string message, Severity severity)
    {
        SendLogMessage.Invoke(connections, message, severity);
    }
#else
    public static void SendLog(string message, Severity severity)
    {
        SendLogMessage.Invoke(message, severity);
    }
#endif
    [NetCall(NetCallSource.FromEither, (ushort)NetCalls.SendLog)]
    private static void ReceiveLogMessage(MessageContext ctx, string message, Severity severity)
    {
        Log(severity, message);
        ctx.Acknowledge(StandardErrorCode.Success);
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
                LogError(message, color ?? ConsoleColor.Yellow);
                break;
            case Severity.Debug:
                LogError(message, color ?? ConsoleColor.DarkGray);
                break;
            default: // Info
                LogError(message, color ?? ConsoleColor.DarkCyan);
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
        ChangeResets(ref message, color.Value);
        Terminal.Write("[" + DateTime.UtcNow.ToString(TimeFormat) + "] " +
                       "[" + core.ToUpperInvariant() + "]"
                       + new string(' ', Math.Max(1, 14 - core.Length)) 
                       + "[" + type!.ToUpperInvariant() + "]" + new string(' ', Math.Max(1, 6 - type.Length)) + message,
            color.Value, true, severity);
    }
    public static void ChangeResets(ref string message, ConsoleColor color)
    {
        if (message.IndexOf(FormattingUtil.ConsoleEscapeCharacter) != -1)
        {
            message = message.Replace(FormattingUtil.ANSIReset, FormattingUtil.GetANSIForegroundString(color)) + FormattingUtil.ANSIReset;
        }
    }
    [Conditional("DEBUG")]
    public static void LogDebug(string message, ConsoleColor color = ConsoleColor.DarkGray)
    {
        ChangeResets(ref message, color);
        Terminal.Write("[" + DateTime.UtcNow.ToString(TimeFormat) + "] [DEVKIT SERVER] [DEBUG] " + message, color, true, Severity.Debug);
    }
    public static void LogInfo(string message, ConsoleColor color = ConsoleColor.DarkCyan)
    {
        ChangeResets(ref message, color);
        Terminal.Write("[" + DateTime.UtcNow.ToString(TimeFormat) + "] [DEVKIT SERVER] [INFO]  " + message, color, true, Severity.Info);
    }
    public static void LogWarning(string message, ConsoleColor color = ConsoleColor.Yellow, [CallerMemberName] string method = "")
    {
        ChangeResets(ref message, color);
        Terminal.Write("[" + DateTime.UtcNow.ToString(TimeFormat) + "] [DEVKIT SERVER] [WARN]  " + "[" + method.ToUpperInvariant() + "] " + message, color, true, Severity.Warning);
    }
    public static void LogError(string message, ConsoleColor color = ConsoleColor.Red, [CallerMemberName] string method = "")
    {
        ChangeResets(ref message, color);
        Terminal.Write("[" + DateTime.UtcNow.ToString(TimeFormat) + "] [DEVKIT SERVER] [ERROR] " + "[" + method.ToUpperInvariant() + "] " + message, color, true, Severity.Error);
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
            Terminal.Write($" Type: {comp.GetType().Format()}{FormattingUtil.GetANSIForegroundString(color)}", color, true, Severity.Debug);
            Terminal.Write(" ========================================", color, true, Severity.Debug);
        }
        int childCt = go.transform.childCount;
        Terminal.Write($"Children: {childCt}:", color, true, Severity.Debug);
        for (int i = 0; i < childCt; ++i)
        {
            DumpGameObject(go.transform.GetChild(i).gameObject, color);
        }
    }
    public static void LogError(Exception ex, bool cleanStack = true, [CallerMemberName] string method = "") => WriteExceptionIntl(ex, cleanStack, 0, method);
    private static void WriteExceptionIntl(Exception ex, bool cleanStack, int indent, string? method = null)
    {
        string ind = indent == 0 ? string.Empty : new string(' ', indent);
        bool inner = indent > 0;
        while (ex != null)
        {
            if (inner)
            {
                Terminal.Write(string.Empty, ConsoleColor.Red, true, Severity.Error);
            }
            Terminal.Write(ind + (inner ? "Inner Exception: " : ((string.IsNullOrEmpty(method) ? string.Empty : ("[" + method!.ToUpper() + "] ")) + "Exception: ")) + ex.GetType().Format() + FormattingUtil.GetANSIForegroundString(ConsoleColor.Red), ConsoleColor.Red, true, Severity.Error);
            Terminal.Write(ind + (ex.Message ?? "No message"), ConsoleColor.DarkRed, true, Severity.Error);
            if (ex is TypeLoadException t)
            {
                Terminal.Write(ind + "Type: " + t.TypeName, ConsoleColor.DarkRed, true, Severity.Error);
            }
            else if (ex is ReflectionTypeLoadException t2)
            {
                Terminal.Write(ind + "Type load exceptions:", ConsoleColor.DarkRed, true, Severity.Error);
                foreach (Exception ex2 in t2.LoaderExceptions)
                {
                    WriteExceptionIntl(ex2, cleanStack, indent + 1);
                }
            }
            else if (ex is TargetInvocationException { InnerException: not null } t4)
            {
                Terminal.Write(ind + "Invoked exception:", ConsoleColor.DarkRed, true, Severity.Error);
                WriteExceptionIntl(t4.InnerException, cleanStack, indent + 1);
            }
            else if (ex is AggregateException t3)
            {
                Terminal.Write(ind + "Inner exceptions:", ConsoleColor.DarkRed, true, Severity.Error);
                foreach (Exception ex2 in t3.InnerExceptions)
                {
                    WriteExceptionIntl(ex2, cleanStack, indent + 1);
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
        OnInputted?.Invoke(input);
        LogInfo(input);
    }
    private static void OnTerminalOutput(ref string outputMessage, ref ConsoleColor color)
    {
        OnOutputting?.Invoke(ref outputMessage, ref color);
        OnOutputed?.Invoke(outputMessage, color);
    }
}

public enum Severity : byte
{
    Debug,
    Info,
    Warning,
    Error,
    Fatal
}