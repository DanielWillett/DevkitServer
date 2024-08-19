using System.Diagnostics;
using StackCleaner;
using System.Reflection;
using System.Text;
using System.Text.Json;
using DanielWillett.ReflectionTools;
using DevkitServer.Configuration;
using DevkitServer.Patches;

namespace DevkitServer.API.Logging;
public static class LoggerExtensions
{
    private static string _debugSeverityColorFormat = null!;
    private static string _infoSeverityColorFormat = null!;
    private static string _warningSeverityColorFormat = null!;
    private static string _errorSeverityColorFormat = null!;
    private static string _fatalSeverityColorFormat = null!;
    private static int _defaultDebugSeverityColor;
    private static int _defaultInfoSeverityColor;
    private static int _defaultWarningSeverityColor;
    private static int _defaultErrorSeverityColor;
    private static int _defaultFatalSeverityColor;
    
    static LoggerExtensions()
    {
        DefaultDebugSeverityColor = FormattingUtil.ToArgb(new Color32(166, 166, 166, 255));
        DefaultInfoSeverityColor = FormattingUtil.ToArgb(new Color32(51, 204, 255, 255));
        DefaultWarningSeverityColor = FormattingUtil.ToArgb(new Color32(255, 204, 102, 255));
        DefaultErrorSeverityColor = FormattingUtil.ToArgb(new Color32(255, 102, 0, 255));
        DefaultFatalSeverityColor = FormattingUtil.ToArgb(new Color32(255, 77, 77, 255));
    }
    public static string LogTimeFormat { get; set; } = "hh:mm:ss";
    public static int DefaultDebugColor { get; set; } = (int)ConsoleColor.DarkGray;
    public static int DefaultInfoColor { get; set; } = (int)ConsoleColor.DarkCyan;
    public static int DefaultWarningColor { get; set; } = (int)ConsoleColor.Yellow;
    public static int DefaultErrorColor { get; set; } = (int)ConsoleColor.Red;
    public static int DefaultFatalColor { get; set; } = (int)ConsoleColor.Red;
    public static int DefaultChatColor { get; set; } = (int)ConsoleColor.White;
    public static int DefaultDebugSeverityColor
    {
        get => _defaultDebugSeverityColor;
        set
        {
            _defaultDebugSeverityColor = value;
            _debugSeverityColorFormat = string.Empty.ColorizeNoReset(value);
        }
    }
    public static int DefaultInfoSeverityColor
    {
        get => _defaultInfoSeverityColor;
        set
        {
            _defaultInfoSeverityColor = value;
            _infoSeverityColorFormat = string.Empty.ColorizeNoReset(value);
        }
    }
    public static int DefaultWarningSeverityColor
    {
        get => _defaultWarningSeverityColor;
        set
        {
            _defaultWarningSeverityColor = value;
            _warningSeverityColorFormat = string.Empty.ColorizeNoReset(value);
        }
    }
    public static int DefaultErrorSeverityColor
    {
        get => _defaultErrorSeverityColor;
        set
        {
            _defaultErrorSeverityColor = value;
            _errorSeverityColorFormat = string.Empty.ColorizeNoReset(value);
        }
    }
    public static int DefaultFatalSeverityColor
    {
        get => _defaultFatalSeverityColor;
        set
        {
            _defaultFatalSeverityColor = value;
            _fatalSeverityColorFormat = string.Empty.ColorizeNoReset(value);
        }
    }
    internal static void OnSettingsUpdated()
    {
        DefaultDebugSeverityColor   = DefaultDebugSeverityColor;
        DefaultInfoSeverityColor    = DefaultInfoSeverityColor;
        DefaultWarningSeverityColor = DefaultWarningSeverityColor;
        DefaultErrorSeverityColor   = DefaultErrorSeverityColor;
        DefaultFatalSeverityColor   = DefaultFatalSeverityColor;
    }
    public static void LogDebug(this IDevkitServerLogger logger, string source, ReadOnlySpan<char> message) => logger.AddLog(Logger.Terminal, source, Severity.Debug, message, null, DefaultDebugColor);
    public static void LogDebug(this IDevkitServerLogger logger, ILogSource source, ReadOnlySpan<char> message) => logger.AddLog(Logger.Terminal, source, Severity.Debug, message, null, DefaultDebugColor);
    public static void LogDebug(this IDevkitServerSourceLogger logger, ReadOnlySpan<char> message) => logger.AddLog(Logger.Terminal, logger, Severity.Debug, message, null, DefaultDebugColor);
    public static void LogDebug(this IDevkitServerSourceLogger logger, Exception exception) => logger.AddLog(Logger.Terminal, logger, Severity.Debug, null, exception, DefaultDebugColor);
    public static void LogDebug(this IDevkitServerSourceLogger logger, Exception exception, ReadOnlySpan<char> message) => logger.AddLog(Logger.Terminal, logger, Severity.Debug, message, exception, DefaultDebugColor);
    public static void LogDebug(this IDevkitServerLogger logger, string source, Exception exception) => logger.AddLog(Logger.Terminal, source, Severity.Debug, null, exception, DefaultDebugColor);
    public static void LogDebug(this IDevkitServerLogger logger, ILogSource source, Exception exception) => logger.AddLog(Logger.Terminal, source, Severity.Debug, null, exception, DefaultDebugColor);
    public static void LogDebug(this IDevkitServerLogger logger, string source, Exception exception, ReadOnlySpan<char> message) => logger.AddLog(Logger.Terminal, source, Severity.Debug, message, exception, DefaultDebugColor);
    public static void LogDebug(this IDevkitServerLogger logger, ILogSource source, Exception exception, ReadOnlySpan<char> message) => logger.AddLog(Logger.Terminal, source, Severity.Debug, message, exception, DefaultDebugColor);
    public static void LogDebug(this IDevkitServerLogger logger, string source, ReadOnlySpan<char> message, Color32 baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Debug, message, null, FormattingUtil.ToArgb(baseColor));
    public static void LogDebug(this IDevkitServerLogger logger, ILogSource source, ReadOnlySpan<char> message, Color32 baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Debug, message, null, FormattingUtil.ToArgb(baseColor));
    public static void LogDebug(this IDevkitServerSourceLogger logger, ReadOnlySpan<char> message, Color32 baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Debug, message, null, FormattingUtil.ToArgb(baseColor));
    public static void LogDebug(this IDevkitServerSourceLogger logger, Exception exception, Color32 baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Debug, null, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogDebug(this IDevkitServerSourceLogger logger, Exception exception, ReadOnlySpan<char> message, Color32 baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Debug, message, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogDebug(this IDevkitServerLogger logger, string source, Exception exception, Color32 baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Debug, null, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogDebug(this IDevkitServerLogger logger, ILogSource source, Exception exception, Color32 baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Debug, null, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogDebug(this IDevkitServerLogger logger, string source, Exception exception, ReadOnlySpan<char> message, Color32 baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Debug, message, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogDebug(this IDevkitServerLogger logger, ILogSource source, Exception exception, ReadOnlySpan<char> message, Color32 baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Debug, message, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogDebug(this IDevkitServerLogger logger, string source, ReadOnlySpan<char> message, Color baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Debug, message, null, FormattingUtil.ToArgb(baseColor));
    public static void LogDebug(this IDevkitServerLogger logger, ILogSource source, ReadOnlySpan<char> message, Color baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Debug, message, null, FormattingUtil.ToArgb(baseColor));
    public static void LogDebug(this IDevkitServerSourceLogger logger, ReadOnlySpan<char> message, Color baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Debug, message, null, FormattingUtil.ToArgb(baseColor));
    public static void LogDebug(this IDevkitServerSourceLogger logger, Exception exception, Color baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Debug, null, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogDebug(this IDevkitServerSourceLogger logger, Exception exception, ReadOnlySpan<char> message, Color baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Debug, message, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogDebug(this IDevkitServerLogger logger, string source, Exception exception, Color baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Debug, null, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogDebug(this IDevkitServerLogger logger, ILogSource source, Exception exception, Color baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Debug, null, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogDebug(this IDevkitServerLogger logger, string source, Exception exception, ReadOnlySpan<char> message, Color baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Debug, message, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogDebug(this IDevkitServerLogger logger, ILogSource source, Exception exception, ReadOnlySpan<char> message, Color baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Debug, message, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogDebug(this IDevkitServerLogger logger, string source, ReadOnlySpan<char> message, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Debug, message, null, FormattingUtil.ToArgbRepresentation(baseColor));
    public static void LogDebug(this IDevkitServerLogger logger, ILogSource source, ReadOnlySpan<char> message, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Debug, message, null, FormattingUtil.ToArgbRepresentation(baseColor));
    public static void LogDebug(this IDevkitServerSourceLogger logger, ReadOnlySpan<char> message, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Debug, message, null, FormattingUtil.ToArgbRepresentation(baseColor));
    public static void LogDebug(this IDevkitServerSourceLogger logger, Exception exception, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Debug, null, exception, FormattingUtil.ToArgbRepresentation(baseColor));
    public static void LogDebug(this IDevkitServerSourceLogger logger, Exception exception, ReadOnlySpan<char> message, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Debug, message, exception, FormattingUtil.ToArgbRepresentation(baseColor));
    public static void LogDebug(this IDevkitServerLogger logger, string source, Exception exception, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Debug, null, exception, FormattingUtil.ToArgbRepresentation(baseColor));
    public static void LogDebug(this IDevkitServerLogger logger, ILogSource source, Exception exception, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Debug, null, exception, FormattingUtil.ToArgbRepresentation(baseColor));
    public static void LogDebug(this IDevkitServerLogger logger, string source, Exception exception, ReadOnlySpan<char> message, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Debug, message, exception, FormattingUtil.ToArgbRepresentation(baseColor));
    public static void LogDebug(this IDevkitServerLogger logger, ILogSource source, Exception exception, ReadOnlySpan<char> message, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Debug, message, exception, FormattingUtil.ToArgbRepresentation(baseColor));
    /// <summary>Only logs with the 'DEBUG' flag present.</summary>
    [Conditional("DEBUG")]
    public static void LogConditional(this IDevkitServerLogger logger, string source, ReadOnlySpan<char> message) => logger.AddLog(Logger.Terminal, source, Severity.Debug, message, null, DefaultDebugColor);
    /// <summary>Only logs with the 'DEBUG' flag present.</summary>
    [Conditional("DEBUG")]
    public static void LogConditional(this IDevkitServerLogger logger, ILogSource source, ReadOnlySpan<char> message) => logger.AddLog(Logger.Terminal, source, Severity.Debug, message, null, DefaultDebugColor);
    /// <summary>Only logs with the 'DEBUG' flag present.</summary>
    [Conditional("DEBUG")]
    public static void LogConditional(this IDevkitServerSourceLogger logger, ReadOnlySpan<char> message) => logger.AddLog(Logger.Terminal, logger, Severity.Debug, message, null, DefaultDebugColor);
    /// <summary>Only logs with the 'DEBUG' flag present.</summary>
    [Conditional("DEBUG")]
    public static void LogConditional(this IDevkitServerSourceLogger logger, Exception exception) => logger.AddLog(Logger.Terminal, logger, Severity.Debug, null, exception, DefaultDebugColor);
    /// <summary>Only logs with the 'DEBUG' flag present.</summary>
    [Conditional("DEBUG")]
    public static void LogConditional(this IDevkitServerSourceLogger logger, Exception exception, ReadOnlySpan<char> message) => logger.AddLog(Logger.Terminal, logger, Severity.Debug, message, exception, DefaultDebugColor);
    /// <summary>Only logs with the 'DEBUG' flag present.</summary>
    [Conditional("DEBUG")]
    public static void LogConditional(this IDevkitServerLogger logger, string source, Exception exception) => logger.AddLog(Logger.Terminal, source, Severity.Debug, null, exception, DefaultDebugColor);
    /// <summary>Only logs with the 'DEBUG' flag present.</summary>
    [Conditional("DEBUG")]
    public static void LogConditional(this IDevkitServerLogger logger, ILogSource source, Exception exception) => logger.AddLog(Logger.Terminal, source, Severity.Debug, null, exception, DefaultDebugColor);
    /// <summary>Only logs with the 'DEBUG' flag present.</summary>
    [Conditional("DEBUG")]
    public static void LogConditional(this IDevkitServerLogger logger, string source, Exception exception, ReadOnlySpan<char> message) => logger.AddLog(Logger.Terminal, source, Severity.Debug, message, exception, DefaultDebugColor);
    /// <summary>Only logs with the 'DEBUG' flag present.</summary>
    [Conditional("DEBUG")]
    public static void LogConditional(this IDevkitServerLogger logger, ILogSource source, Exception exception, ReadOnlySpan<char> message) => logger.AddLog(Logger.Terminal, source, Severity.Debug, message, exception, DefaultDebugColor);
    /// <summary>Only logs with the 'DEBUG' flag present.</summary>
    [Conditional("DEBUG")]
    public static void LogConditional(this IDevkitServerLogger logger, string source, ReadOnlySpan<char> message, Color32 baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Debug, message, null, FormattingUtil.ToArgb(baseColor));
    /// <summary>Only logs with the 'DEBUG' flag present.</summary>
    [Conditional("DEBUG")]
    public static void LogConditional(this IDevkitServerLogger logger, ILogSource source, ReadOnlySpan<char> message, Color32 baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Debug, message, null, FormattingUtil.ToArgb(baseColor));
    /// <summary>Only logs with the 'DEBUG' flag present.</summary>
    [Conditional("DEBUG")]
    public static void LogConditional(this IDevkitServerSourceLogger logger, ReadOnlySpan<char> message, Color32 baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Debug, message, null, FormattingUtil.ToArgb(baseColor));
    /// <summary>Only logs with the 'DEBUG' flag present.</summary>
    [Conditional("DEBUG")]
    public static void LogConditional(this IDevkitServerSourceLogger logger, Exception exception, Color32 baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Debug, null, exception, FormattingUtil.ToArgb(baseColor));
    /// <summary>Only logs with the 'DEBUG' flag present.</summary>
    [Conditional("DEBUG")]
    public static void LogConditional(this IDevkitServerSourceLogger logger, Exception exception, ReadOnlySpan<char> message, Color32 baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Debug, message, exception, FormattingUtil.ToArgb(baseColor));
    /// <summary>Only logs with the 'DEBUG' flag present.</summary>
    [Conditional("DEBUG")]
    public static void LogConditional(this IDevkitServerLogger logger, string source, Exception exception, Color32 baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Debug, null, exception, FormattingUtil.ToArgb(baseColor));
    /// <summary>Only logs with the 'DEBUG' flag present.</summary>
    [Conditional("DEBUG")]
    public static void LogConditional(this IDevkitServerLogger logger, ILogSource source, Exception exception, Color32 baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Debug, null, exception, FormattingUtil.ToArgb(baseColor));
    /// <summary>Only logs with the 'DEBUG' flag present.</summary>
    [Conditional("DEBUG")]
    public static void LogConditional(this IDevkitServerLogger logger, string source, Exception exception, ReadOnlySpan<char> message, Color32 baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Debug, message, exception, FormattingUtil.ToArgb(baseColor));
    /// <summary>Only logs with the 'DEBUG' flag present.</summary>
    [Conditional("DEBUG")]
    public static void LogConditional(this IDevkitServerLogger logger, ILogSource source, Exception exception, ReadOnlySpan<char> message, Color32 baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Debug, message, exception, FormattingUtil.ToArgb(baseColor));
    /// <summary>Only logs with the 'DEBUG' flag present.</summary>
    [Conditional("DEBUG")]
    public static void LogConditional(this IDevkitServerLogger logger, string source, ReadOnlySpan<char> message, Color baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Debug, message, null, FormattingUtil.ToArgb(baseColor));
    /// <summary>Only logs with the 'DEBUG' flag present.</summary>
    [Conditional("DEBUG")]
    public static void LogConditional(this IDevkitServerLogger logger, ILogSource source, ReadOnlySpan<char> message, Color baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Debug, message, null, FormattingUtil.ToArgb(baseColor));
    /// <summary>Only logs with the 'DEBUG' flag present.</summary>
    [Conditional("DEBUG")]
    public static void LogConditional(this IDevkitServerSourceLogger logger, ReadOnlySpan<char> message, Color baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Debug, message, null, FormattingUtil.ToArgb(baseColor));
    /// <summary>Only logs with the 'DEBUG' flag present.</summary>
    [Conditional("DEBUG")]
    public static void LogConditional(this IDevkitServerSourceLogger logger, Exception exception, Color baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Debug, null, exception, FormattingUtil.ToArgb(baseColor));
    /// <summary>Only logs with the 'DEBUG' flag present.</summary>
    [Conditional("DEBUG")]
    public static void LogConditional(this IDevkitServerSourceLogger logger, Exception exception, ReadOnlySpan<char> message, Color baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Debug, message, exception, FormattingUtil.ToArgb(baseColor));
    /// <summary>Only logs with the 'DEBUG' flag present.</summary>
    [Conditional("DEBUG")]
    public static void LogConditional(this IDevkitServerLogger logger, string source, Exception exception, Color baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Debug, null, exception, FormattingUtil.ToArgb(baseColor));
    /// <summary>Only logs with the 'DEBUG' flag present.</summary>
    [Conditional("DEBUG")]
    public static void LogConditional(this IDevkitServerLogger logger, ILogSource source, Exception exception, Color baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Debug, null, exception, FormattingUtil.ToArgb(baseColor));
    /// <summary>Only logs with the 'DEBUG' flag present.</summary>
    [Conditional("DEBUG")]
    public static void LogConditional(this IDevkitServerLogger logger, string source, Exception exception, ReadOnlySpan<char> message, Color baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Debug, message, exception, FormattingUtil.ToArgb(baseColor));
    /// <summary>Only logs with the 'DEBUG' flag present.</summary>
    [Conditional("DEBUG")]
    public static void LogConditional(this IDevkitServerLogger logger, ILogSource source, Exception exception, ReadOnlySpan<char> message, Color baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Debug, message, exception, FormattingUtil.ToArgb(baseColor));
    /// <summary>Only logs with the 'DEBUG' flag present.</summary>
    [Conditional("DEBUG")]
    public static void LogConditional(this IDevkitServerLogger logger, string source, ReadOnlySpan<char> message, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Debug, message, null, FormattingUtil.ToArgbRepresentation(baseColor));
    /// <summary>Only logs with the 'DEBUG' flag present.</summary>
    [Conditional("DEBUG")]
    public static void LogConditional(this IDevkitServerLogger logger, ILogSource source, ReadOnlySpan<char> message, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Debug, message, null, FormattingUtil.ToArgbRepresentation(baseColor));
    /// <summary>Only logs with the 'DEBUG' flag present.</summary>
    [Conditional("DEBUG")]
    public static void LogConditional(this IDevkitServerSourceLogger logger, ReadOnlySpan<char> message, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Debug, message, null, FormattingUtil.ToArgbRepresentation(baseColor));
    /// <summary>Only logs with the 'DEBUG' flag present.</summary>
    [Conditional("DEBUG")]
    public static void LogConditional(this IDevkitServerSourceLogger logger, Exception exception, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Debug, null, exception, FormattingUtil.ToArgbRepresentation(baseColor));
    /// <summary>Only logs with the 'DEBUG' flag present.</summary>
    [Conditional("DEBUG")]
    public static void LogConditional(this IDevkitServerSourceLogger logger, Exception exception, ReadOnlySpan<char> message, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Debug, message, exception, FormattingUtil.ToArgbRepresentation(baseColor));
    /// <summary>Only logs with the 'DEBUG' flag present.</summary>
    [Conditional("DEBUG")]
    public static void LogConditional(this IDevkitServerLogger logger, string source, Exception exception, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Debug, null, exception, FormattingUtil.ToArgbRepresentation(baseColor));
    /// <summary>Only logs with the 'DEBUG' flag present.</summary>
    [Conditional("DEBUG")]
    public static void LogConditional(this IDevkitServerLogger logger, ILogSource source, Exception exception, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Debug, null, exception, FormattingUtil.ToArgbRepresentation(baseColor));
    /// <summary>Only logs with the 'DEBUG' flag present.</summary>
    [Conditional("DEBUG")]
    public static void LogConditional(this IDevkitServerLogger logger, string source, Exception exception, ReadOnlySpan<char> message, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Debug, message, exception, FormattingUtil.ToArgbRepresentation(baseColor));
    /// <summary>Only logs with the 'DEBUG' flag present.</summary>
    [Conditional("DEBUG")]
    public static void LogConditional(this IDevkitServerLogger logger, ILogSource source, Exception exception, ReadOnlySpan<char> message, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Debug, message, exception, FormattingUtil.ToArgbRepresentation(baseColor));
    public static void LogInfo(this IDevkitServerLogger logger, string source, ReadOnlySpan<char> message) => logger.AddLog(Logger.Terminal, source, Severity.Info, message, null, DefaultInfoColor);
    public static void LogInfo(this IDevkitServerLogger logger, ILogSource source, ReadOnlySpan<char> message) => logger.AddLog(Logger.Terminal, source, Severity.Info, message, null, DefaultInfoColor);
    public static void LogInfo(this IDevkitServerSourceLogger logger, ReadOnlySpan<char> message) => logger.AddLog(Logger.Terminal, logger, Severity.Info, message, null, DefaultInfoColor);
    public static void LogInfo(this IDevkitServerSourceLogger logger, Exception exception) => logger.AddLog(Logger.Terminal, logger, Severity.Info, null, exception, DefaultInfoColor);
    public static void LogInfo(this IDevkitServerSourceLogger logger, Exception exception, ReadOnlySpan<char> message) => logger.AddLog(Logger.Terminal, logger, Severity.Info, message, exception, DefaultInfoColor);
    public static void LogInfo(this IDevkitServerLogger logger, string source, Exception exception) => logger.AddLog(Logger.Terminal, source, Severity.Info, null, exception, DefaultInfoColor);
    public static void LogInfo(this IDevkitServerLogger logger, ILogSource source, Exception exception) => logger.AddLog(Logger.Terminal, source, Severity.Info, null, exception, DefaultInfoColor);
    public static void LogInfo(this IDevkitServerLogger logger, string source, Exception exception, ReadOnlySpan<char> message) => logger.AddLog(Logger.Terminal, source, Severity.Info, message, exception, DefaultInfoColor);
    public static void LogInfo(this IDevkitServerLogger logger, ILogSource source, Exception exception, ReadOnlySpan<char> message) => logger.AddLog(Logger.Terminal, source, Severity.Info, message, exception, DefaultInfoColor);
    public static void LogInfo(this IDevkitServerLogger logger, string source, ReadOnlySpan<char> message, Color32 baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Info, message, null, FormattingUtil.ToArgb(baseColor));
    public static void LogInfo(this IDevkitServerLogger logger, ILogSource source, ReadOnlySpan<char> message, Color32 baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Info, message, null, FormattingUtil.ToArgb(baseColor));
    public static void LogInfo(this IDevkitServerSourceLogger logger, ReadOnlySpan<char> message, Color32 baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Info, message, null, FormattingUtil.ToArgb(baseColor));
    public static void LogInfo(this IDevkitServerSourceLogger logger, Exception exception, Color32 baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Info, null, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogInfo(this IDevkitServerSourceLogger logger, Exception exception, ReadOnlySpan<char> message, Color32 baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Info, message, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogInfo(this IDevkitServerLogger logger, string source, Exception exception, Color32 baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Info, null, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogInfo(this IDevkitServerLogger logger, ILogSource source, Exception exception, Color32 baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Info, null, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogInfo(this IDevkitServerLogger logger, string source, Exception exception, ReadOnlySpan<char> message, Color32 baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Info, message, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogInfo(this IDevkitServerLogger logger, ILogSource source, Exception exception, ReadOnlySpan<char> message, Color32 baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Info, message, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogInfo(this IDevkitServerLogger logger, string source, ReadOnlySpan<char> message, Color baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Info, message, null, FormattingUtil.ToArgb(baseColor));
    public static void LogInfo(this IDevkitServerLogger logger, ILogSource source, ReadOnlySpan<char> message, Color baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Info, message, null, FormattingUtil.ToArgb(baseColor));
    public static void LogInfo(this IDevkitServerSourceLogger logger, ReadOnlySpan<char> message, Color baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Info, message, null, FormattingUtil.ToArgb(baseColor));
    public static void LogInfo(this IDevkitServerSourceLogger logger, Exception exception, Color baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Info, null, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogInfo(this IDevkitServerSourceLogger logger, Exception exception, ReadOnlySpan<char> message, Color baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Info, message, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogInfo(this IDevkitServerLogger logger, string source, Exception exception, Color baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Info, null, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogInfo(this IDevkitServerLogger logger, ILogSource source, Exception exception, Color baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Info, null, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogInfo(this IDevkitServerLogger logger, string source, Exception exception, ReadOnlySpan<char> message, Color baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Info, message, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogInfo(this IDevkitServerLogger logger, ILogSource source, Exception exception, ReadOnlySpan<char> message, Color baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Info, message, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogInfo(this IDevkitServerLogger logger, string source, ReadOnlySpan<char> message, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Info, message, null, FormattingUtil.ToArgbRepresentation(baseColor));
    public static void LogInfo(this IDevkitServerLogger logger, ILogSource source, ReadOnlySpan<char> message, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Info, message, null, FormattingUtil.ToArgbRepresentation(baseColor));
    public static void LogInfo(this IDevkitServerSourceLogger logger, ReadOnlySpan<char> message, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Info, message, null, FormattingUtil.ToArgbRepresentation(baseColor));
    public static void LogInfo(this IDevkitServerSourceLogger logger, Exception exception, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Info, null, exception, FormattingUtil.ToArgbRepresentation(baseColor));
    public static void LogInfo(this IDevkitServerSourceLogger logger, Exception exception, ReadOnlySpan<char> message, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Info, message, exception, FormattingUtil.ToArgbRepresentation(baseColor));
    public static void LogInfo(this IDevkitServerLogger logger, string source, Exception exception, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Info, null, exception, FormattingUtil.ToArgbRepresentation(baseColor));
    public static void LogInfo(this IDevkitServerLogger logger, ILogSource source, Exception exception, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Info, null, exception, FormattingUtil.ToArgbRepresentation(baseColor));
    public static void LogInfo(this IDevkitServerLogger logger, string source, Exception exception, ReadOnlySpan<char> message, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Info, message, exception, FormattingUtil.ToArgbRepresentation(baseColor));
    public static void LogInfo(this IDevkitServerLogger logger, ILogSource source, Exception exception, ReadOnlySpan<char> message, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Info, message, exception, FormattingUtil.ToArgbRepresentation(baseColor));
    public static void LogWarning(this IDevkitServerLogger logger, string source, ReadOnlySpan<char> message) => logger.AddLog(Logger.Terminal, source, Severity.Warning, message, null, DefaultWarningColor);
    public static void LogWarning(this IDevkitServerLogger logger, ILogSource source, ReadOnlySpan<char> message) => logger.AddLog(Logger.Terminal, source, Severity.Warning, message, null, DefaultWarningColor);
    public static void LogWarning(this IDevkitServerSourceLogger logger, ReadOnlySpan<char> message) => logger.AddLog(Logger.Terminal, logger, Severity.Warning, message, null, DefaultWarningColor);
    public static void LogWarning(this IDevkitServerSourceLogger logger, Exception exception) => logger.AddLog(Logger.Terminal, logger, Severity.Warning, null, exception, DefaultWarningColor);
    public static void LogWarning(this IDevkitServerSourceLogger logger, Exception exception, ReadOnlySpan<char> message) => logger.AddLog(Logger.Terminal, logger, Severity.Warning, message, exception, DefaultWarningColor);
    public static void LogWarning(this IDevkitServerLogger logger, string source, Exception exception) => logger.AddLog(Logger.Terminal, source, Severity.Warning, null, exception, DefaultWarningColor);
    public static void LogWarning(this IDevkitServerLogger logger, ILogSource source, Exception exception) => logger.AddLog(Logger.Terminal, source, Severity.Warning, null, exception, DefaultWarningColor);
    public static void LogWarning(this IDevkitServerLogger logger, string source, Exception exception, ReadOnlySpan<char> message) => logger.AddLog(Logger.Terminal, source, Severity.Warning, message, exception, DefaultWarningColor);
    public static void LogWarning(this IDevkitServerLogger logger, ILogSource source, Exception exception, ReadOnlySpan<char> message) => logger.AddLog(Logger.Terminal, source, Severity.Warning, message, exception, DefaultWarningColor);
    public static void LogWarning(this IDevkitServerLogger logger, string source, ReadOnlySpan<char> message, Color32 baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Warning, message, null, FormattingUtil.ToArgb(baseColor));
    public static void LogWarning(this IDevkitServerLogger logger, ILogSource source, ReadOnlySpan<char> message, Color32 baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Warning, message, null, FormattingUtil.ToArgb(baseColor));
    public static void LogWarning(this IDevkitServerSourceLogger logger, ReadOnlySpan<char> message, Color32 baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Warning, message, null, FormattingUtil.ToArgb(baseColor));
    public static void LogWarning(this IDevkitServerSourceLogger logger, Exception exception, Color32 baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Warning, null, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogWarning(this IDevkitServerSourceLogger logger, Exception exception, ReadOnlySpan<char> message, Color32 baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Warning, message, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogWarning(this IDevkitServerLogger logger, string source, Exception exception, Color32 baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Warning, null, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogWarning(this IDevkitServerLogger logger, ILogSource source, Exception exception, Color32 baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Warning, null, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogWarning(this IDevkitServerLogger logger, string source, Exception exception, ReadOnlySpan<char> message, Color32 baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Warning, message, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogWarning(this IDevkitServerLogger logger, ILogSource source, Exception exception, ReadOnlySpan<char> message, Color32 baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Warning, message, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogWarning(this IDevkitServerLogger logger, string source, ReadOnlySpan<char> message, Color baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Warning, message, null, FormattingUtil.ToArgb(baseColor));
    public static void LogWarning(this IDevkitServerLogger logger, ILogSource source, ReadOnlySpan<char> message, Color baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Warning, message, null, FormattingUtil.ToArgb(baseColor));
    public static void LogWarning(this IDevkitServerSourceLogger logger, ReadOnlySpan<char> message, Color baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Warning, message, null, FormattingUtil.ToArgb(baseColor));
    public static void LogWarning(this IDevkitServerSourceLogger logger, Exception exception, Color baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Warning, null, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogWarning(this IDevkitServerSourceLogger logger, Exception exception, ReadOnlySpan<char> message, Color baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Warning, message, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogWarning(this IDevkitServerLogger logger, string source, Exception exception, Color baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Warning, null, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogWarning(this IDevkitServerLogger logger, ILogSource source, Exception exception, Color baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Warning, null, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogWarning(this IDevkitServerLogger logger, string source, Exception exception, ReadOnlySpan<char> message, Color baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Warning, message, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogWarning(this IDevkitServerLogger logger, ILogSource source, Exception exception, ReadOnlySpan<char> message, Color baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Warning, message, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogWarning(this IDevkitServerLogger logger, string source, ReadOnlySpan<char> message, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Warning, message, null, FormattingUtil.ToArgbRepresentation(baseColor));
    public static void LogWarning(this IDevkitServerLogger logger, ILogSource source, ReadOnlySpan<char> message, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Warning, message, null, FormattingUtil.ToArgbRepresentation(baseColor));
    public static void LogWarning(this IDevkitServerSourceLogger logger, ReadOnlySpan<char> message, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Warning, message, null, FormattingUtil.ToArgbRepresentation(baseColor));
    public static void LogWarning(this IDevkitServerSourceLogger logger, Exception exception, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Warning, null, exception, FormattingUtil.ToArgbRepresentation(baseColor));
    public static void LogWarning(this IDevkitServerSourceLogger logger, Exception exception, ReadOnlySpan<char> message, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Warning, message, exception, FormattingUtil.ToArgbRepresentation(baseColor));
    public static void LogWarning(this IDevkitServerLogger logger, string source, Exception exception, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Warning, null, exception, FormattingUtil.ToArgbRepresentation(baseColor));
    public static void LogWarning(this IDevkitServerLogger logger, ILogSource source, Exception exception, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Warning, null, exception, FormattingUtil.ToArgbRepresentation(baseColor));
    public static void LogWarning(this IDevkitServerLogger logger, string source, Exception exception, ReadOnlySpan<char> message, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Warning, message, exception, FormattingUtil.ToArgbRepresentation(baseColor));
    public static void LogWarning(this IDevkitServerLogger logger, ILogSource source, Exception exception, ReadOnlySpan<char> message, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Warning, message, exception, FormattingUtil.ToArgbRepresentation(baseColor));
    public static void LogError(this IDevkitServerLogger logger, string source, ReadOnlySpan<char> message) => logger.AddLog(Logger.Terminal, source, Severity.Error, message, null, DefaultErrorColor);
    public static void LogError(this IDevkitServerLogger logger, ILogSource source, ReadOnlySpan<char> message) => logger.AddLog(Logger.Terminal, source, Severity.Error, message, null, DefaultErrorColor);
    public static void LogError(this IDevkitServerSourceLogger logger, ReadOnlySpan<char> message) => logger.AddLog(Logger.Terminal, logger, Severity.Error, message, null, DefaultErrorColor);
    public static void LogError(this IDevkitServerSourceLogger logger, Exception exception) => logger.AddLog(Logger.Terminal, logger, Severity.Error, null, exception, DefaultErrorColor);
    public static void LogError(this IDevkitServerSourceLogger logger, Exception exception, ReadOnlySpan<char> message) => logger.AddLog(Logger.Terminal, logger, Severity.Error, message, exception, DefaultErrorColor);
    public static void LogError(this IDevkitServerLogger logger, string source, Exception exception) => logger.AddLog(Logger.Terminal, source, Severity.Error, null, exception, DefaultErrorColor);
    public static void LogError(this IDevkitServerLogger logger, ILogSource source, Exception exception) => logger.AddLog(Logger.Terminal, source, Severity.Error, null, exception, DefaultErrorColor);
    public static void LogError(this IDevkitServerLogger logger, string source, Exception exception, ReadOnlySpan<char> message) => logger.AddLog(Logger.Terminal, source, Severity.Error, message, exception, DefaultErrorColor);
    public static void LogError(this IDevkitServerLogger logger, ILogSource source, Exception exception, ReadOnlySpan<char> message) => logger.AddLog(Logger.Terminal, source, Severity.Error, message, exception, DefaultErrorColor);
    public static void LogError(this IDevkitServerLogger logger, string source, ReadOnlySpan<char> message, Color32 baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Error, message, null, FormattingUtil.ToArgb(baseColor));
    public static void LogError(this IDevkitServerLogger logger, ILogSource source, ReadOnlySpan<char> message, Color32 baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Error, message, null, FormattingUtil.ToArgb(baseColor));
    public static void LogError(this IDevkitServerSourceLogger logger, ReadOnlySpan<char> message, Color32 baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Error, message, null, FormattingUtil.ToArgb(baseColor));
    public static void LogError(this IDevkitServerSourceLogger logger, Exception exception, Color32 baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Error, null, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogError(this IDevkitServerSourceLogger logger, Exception exception, ReadOnlySpan<char> message, Color32 baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Error, message, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogError(this IDevkitServerLogger logger, string source, Exception exception, Color32 baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Error, null, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogError(this IDevkitServerLogger logger, ILogSource source, Exception exception, Color32 baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Error, null, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogError(this IDevkitServerLogger logger, string source, Exception exception, ReadOnlySpan<char> message, Color32 baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Error, message, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogError(this IDevkitServerLogger logger, ILogSource source, Exception exception, ReadOnlySpan<char> message, Color32 baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Error, message, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogError(this IDevkitServerLogger logger, string source, ReadOnlySpan<char> message, Color baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Error, message, null, FormattingUtil.ToArgb(baseColor));
    public static void LogError(this IDevkitServerLogger logger, ILogSource source, ReadOnlySpan<char> message, Color baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Error, message, null, FormattingUtil.ToArgb(baseColor));
    public static void LogError(this IDevkitServerSourceLogger logger, ReadOnlySpan<char> message, Color baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Error, message, null, FormattingUtil.ToArgb(baseColor));
    public static void LogError(this IDevkitServerSourceLogger logger, Exception exception, Color baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Error, null, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogError(this IDevkitServerSourceLogger logger, Exception exception, ReadOnlySpan<char> message, Color baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Error, message, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogError(this IDevkitServerLogger logger, string source, Exception exception, Color baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Error, null, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogError(this IDevkitServerLogger logger, ILogSource source, Exception exception, Color baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Error, null, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogError(this IDevkitServerLogger logger, string source, Exception exception, ReadOnlySpan<char> message, Color baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Error, message, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogError(this IDevkitServerLogger logger, ILogSource source, Exception exception, ReadOnlySpan<char> message, Color baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Error, message, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogError(this IDevkitServerLogger logger, string source, ReadOnlySpan<char> message, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Error, message, null, FormattingUtil.ToArgbRepresentation(baseColor));
    public static void LogError(this IDevkitServerLogger logger, ILogSource source, ReadOnlySpan<char> message, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Error, message, null, FormattingUtil.ToArgbRepresentation(baseColor));
    public static void LogError(this IDevkitServerSourceLogger logger, ReadOnlySpan<char> message, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Error, message, null, FormattingUtil.ToArgbRepresentation(baseColor));
    public static void LogError(this IDevkitServerSourceLogger logger, Exception exception, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Error, null, exception, FormattingUtil.ToArgbRepresentation(baseColor));
    public static void LogError(this IDevkitServerSourceLogger logger, Exception exception, ReadOnlySpan<char> message, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Error, message, exception, FormattingUtil.ToArgbRepresentation(baseColor));
    public static void LogError(this IDevkitServerLogger logger, string source, Exception exception, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Error, null, exception, FormattingUtil.ToArgbRepresentation(baseColor));
    public static void LogError(this IDevkitServerLogger logger, ILogSource source, Exception exception, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Error, null, exception, FormattingUtil.ToArgbRepresentation(baseColor));
    public static void LogError(this IDevkitServerLogger logger, string source, Exception exception, ReadOnlySpan<char> message, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Error, message, exception, FormattingUtil.ToArgbRepresentation(baseColor));
    public static void LogError(this IDevkitServerLogger logger, ILogSource source, Exception exception, ReadOnlySpan<char> message, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Error, message, exception, FormattingUtil.ToArgbRepresentation(baseColor));
    public static void LogFatal(this IDevkitServerLogger logger, string source, ReadOnlySpan<char> message) => logger.AddLog(Logger.Terminal, source, Severity.Fatal, message, null, DefaultFatalColor);
    public static void LogFatal(this IDevkitServerLogger logger, ILogSource source, ReadOnlySpan<char> message) => logger.AddLog(Logger.Terminal, source, Severity.Fatal, message, null, DefaultFatalColor);
    public static void LogFatal(this IDevkitServerSourceLogger logger, ReadOnlySpan<char> message) => logger.AddLog(Logger.Terminal, logger, Severity.Fatal, message, null, DefaultFatalColor);
    public static void LogFatal(this IDevkitServerSourceLogger logger, Exception exception) => logger.AddLog(Logger.Terminal, logger, Severity.Fatal, null, exception, DefaultFatalColor);
    public static void LogFatal(this IDevkitServerSourceLogger logger, Exception exception, ReadOnlySpan<char> message) => logger.AddLog(Logger.Terminal, logger, Severity.Fatal, message, exception, DefaultFatalColor);
    public static void LogFatal(this IDevkitServerLogger logger, string source, Exception exception) => logger.AddLog(Logger.Terminal, source, Severity.Fatal, null, exception, DefaultFatalColor);
    public static void LogFatal(this IDevkitServerLogger logger, ILogSource source, Exception exception) => logger.AddLog(Logger.Terminal, source, Severity.Fatal, null, exception, DefaultFatalColor);
    public static void LogFatal(this IDevkitServerLogger logger, string source, Exception exception, ReadOnlySpan<char> message) => logger.AddLog(Logger.Terminal, source, Severity.Fatal, message, exception, DefaultFatalColor);
    public static void LogFatal(this IDevkitServerLogger logger, ILogSource source, Exception exception, ReadOnlySpan<char> message) => logger.AddLog(Logger.Terminal, source, Severity.Fatal, message, exception, DefaultFatalColor);
    public static void LogFatal(this IDevkitServerLogger logger, string source, ReadOnlySpan<char> message, Color32 baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Fatal, message, null, FormattingUtil.ToArgb(baseColor));
    public static void LogFatal(this IDevkitServerLogger logger, ILogSource source, ReadOnlySpan<char> message, Color32 baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Fatal, message, null, FormattingUtil.ToArgb(baseColor));
    public static void LogFatal(this IDevkitServerSourceLogger logger, ReadOnlySpan<char> message, Color32 baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Fatal, message, null, FormattingUtil.ToArgb(baseColor));
    public static void LogFatal(this IDevkitServerSourceLogger logger, Exception exception, Color32 baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Fatal, null, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogFatal(this IDevkitServerSourceLogger logger, Exception exception, ReadOnlySpan<char> message, Color32 baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Fatal, message, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogFatal(this IDevkitServerLogger logger, string source, Exception exception, Color32 baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Fatal, null, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogFatal(this IDevkitServerLogger logger, ILogSource source, Exception exception, Color32 baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Fatal, null, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogFatal(this IDevkitServerLogger logger, string source, Exception exception, ReadOnlySpan<char> message, Color32 baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Fatal, message, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogFatal(this IDevkitServerLogger logger, ILogSource source, Exception exception, ReadOnlySpan<char> message, Color32 baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Fatal, message, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogFatal(this IDevkitServerLogger logger, string source, ReadOnlySpan<char> message, Color baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Fatal, message, null, FormattingUtil.ToArgb(baseColor));
    public static void LogFatal(this IDevkitServerLogger logger, ILogSource source, ReadOnlySpan<char> message, Color baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Fatal, message, null, FormattingUtil.ToArgb(baseColor));
    public static void LogFatal(this IDevkitServerSourceLogger logger, ReadOnlySpan<char> message, Color baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Fatal, message, null, FormattingUtil.ToArgb(baseColor));
    public static void LogFatal(this IDevkitServerSourceLogger logger, Exception exception, Color baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Fatal, null, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogFatal(this IDevkitServerSourceLogger logger, Exception exception, ReadOnlySpan<char> message, Color baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Fatal, message, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogFatal(this IDevkitServerLogger logger, string source, Exception exception, Color baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Fatal, null, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogFatal(this IDevkitServerLogger logger, ILogSource source, Exception exception, Color baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Fatal, null, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogFatal(this IDevkitServerLogger logger, string source, Exception exception, ReadOnlySpan<char> message, Color baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Fatal, message, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogFatal(this IDevkitServerLogger logger, ILogSource source, Exception exception, ReadOnlySpan<char> message, Color baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Fatal, message, exception, FormattingUtil.ToArgb(baseColor));
    public static void LogFatal(this IDevkitServerLogger logger, string source, ReadOnlySpan<char> message, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Fatal, message, null, FormattingUtil.ToArgbRepresentation(baseColor));
    public static void LogFatal(this IDevkitServerLogger logger, ILogSource source, ReadOnlySpan<char> message, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Fatal, message, null, FormattingUtil.ToArgbRepresentation(baseColor));
    public static void LogFatal(this IDevkitServerSourceLogger logger, ReadOnlySpan<char> message, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Fatal, message, null, FormattingUtil.ToArgbRepresentation(baseColor));
    public static void LogFatal(this IDevkitServerSourceLogger logger, Exception exception, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Fatal, null, exception, FormattingUtil.ToArgbRepresentation(baseColor));
    public static void LogFatal(this IDevkitServerSourceLogger logger, Exception exception, ReadOnlySpan<char> message, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, logger, Severity.Fatal, message, exception, FormattingUtil.ToArgbRepresentation(baseColor));
    public static void LogFatal(this IDevkitServerLogger logger, string source, Exception exception, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Fatal, null, exception, FormattingUtil.ToArgbRepresentation(baseColor));
    public static void LogFatal(this IDevkitServerLogger logger, ILogSource source, Exception exception, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Fatal, null, exception, FormattingUtil.ToArgbRepresentation(baseColor));
    public static void LogFatal(this IDevkitServerLogger logger, string source, Exception exception, ReadOnlySpan<char> message, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Fatal, message, exception, FormattingUtil.ToArgbRepresentation(baseColor));
    public static void LogFatal(this IDevkitServerLogger logger, ILogSource source, Exception exception, ReadOnlySpan<char> message, ConsoleColor baseColor) => logger.AddLog(Logger.Terminal, source, Severity.Fatal, message, exception, FormattingUtil.ToArgbRepresentation(baseColor));
    public static bool IsSeverityEnabled(Severity severity, object? source)
    {
        bool? setting = (source as ILogSource)?.GetExplicitVisibilitySetting(severity);

        if (setting.HasValue)
            return setting.Value;

        return Logger.Debug || severity > Severity.Debug;
    }
    public static int GetSeverityLength(Severity severity, ReadOnlySpan<char> foregroundSequence)
    {
        if (FormattingUtil.FormatProvider.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.None)
            return 3;

        return 3 + foregroundSequence.Length + severity switch
        {
            Severity.Error => _errorSeverityColorFormat.Length,
            Severity.Debug => _debugSeverityColorFormat.Length,
            Severity.Fatal => _fatalSeverityColorFormat.Length,
            Severity.Warning => _warningSeverityColorFormat.Length,
            _ => _infoSeverityColorFormat.Length
        };
    }
    public static void FormatSeverity(Severity severity, ReadOnlySpan<char> foregroundSequence, Span<char> output)
    {
        ReadOnlySpan<char> s = severity switch
        {
            Severity.Error => "ERR",
            Severity.Debug => "DBG",
            Severity.Fatal => "FTL",
            Severity.Warning => "WRN",
            _ => "INF"
        };

        if (FormattingUtil.FormatProvider.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.None)
        {
            s.CopyTo(output);
            return;
        }

        ReadOnlySpan<char> p1 = severity switch
        {
            Severity.Error => _errorSeverityColorFormat,
            Severity.Debug => _debugSeverityColorFormat,
            Severity.Fatal => _fatalSeverityColorFormat,
            Severity.Warning => _warningSeverityColorFormat,
            _ => _infoSeverityColorFormat
        };

        p1.CopyTo(output);
        s.CopyTo(output[p1.Length..]);
        foregroundSequence.CopyTo(output[(p1.Length + s.Length)..]);
    }
    public static ReadOnlySpan<char> RemoveDateFromLine(ReadOnlySpan<char> input)
    {
        int firstEnd = input.IndexOf(']');
        firstEnd += input.Length > firstEnd + 1 && input[firstEnd + 1] == ' ' ? 2 : 1;
        return input[firstEnd..];
    }
    public static int GetSourceLength(object? source, ReadOnlySpan<char> foregroundSequence, out ReadOnlySpan<char> sourceSpan, out bool anyResetsInSource)
    {
        string? sourceOut = source switch
        {
            ITerminalFormattable fmt => fmt.Format(FormattingUtil.FormatProvider),
            ILogSource src => src.Source,
            string str => str,
            null => null,
            IConvertible => source.ToString(), 
            _ => source.GetType().Name
        };

        sourceSpan = sourceOut;

        if (string.IsNullOrWhiteSpace(sourceOut))
        {
            anyResetsInSource = false;
            return 0;
        }

        int len = GetReplaceResetsWithConsoleColorInfo(sourceSpan, foregroundSequence, out anyResetsInSource);
        return len;
    }

    /// <summary>
    /// Colorizes a log message and fixes any virtual terminal foreground resets.
    /// </summary>
    public static string? FormatAddedLogMessage(int argb, ReadOnlySpan<char> message, out ConsoleColor? color, out ReadOnlySpan<char> foregroundSequence)
    {
        if (unchecked((byte)(argb >> 24)) == 0)
            color = (ConsoleColor)argb;
        else
            color = null;

        if (message.IsEmpty)
        {
            foregroundSequence = FormattingUtil.GetTerminalColorSequenceString(argb, false);
            return null;
        }
        
        int len = GetReplaceResetsWithConsoleColorInfo(message, out foregroundSequence, out bool anyFound, argb);

        if (!anyFound || (ConsoleColor)argb == ConsoleColor.Gray)
            return !color.HasValue ? FormattingUtil.WrapMessageWithColor(argb, message) : message.ToString();

        Span<char> newString = len > 512 ? new char[len] : stackalloc char[len];
        ReplaceResetsWithConsoleColor(message, newString, foregroundSequence);
        return color.HasValue ? newString.ToString() : FormattingUtil.WrapMessageWithColor(argb, newString);

    }

    /// <summary>
    /// Calculate info for replacing resets with a color.
    /// </summary>
    /// <remarks>Chain with <see cref="ReplaceResetsWithConsoleColor"/>.</remarks>
    public static int GetReplaceResetsWithConsoleColorInfo(ReadOnlySpan<char> message, out ReadOnlySpan<char> foregroundSequence, out bool anyFound, int argb)
    {
        ReadOnlySpan<char> reset = FormattingUtil.ForegroundResetSequence;
        ReadOnlySpan<char> foreground = FormattingUtil.GetTerminalColorSequenceString(argb, false);

        int amt = message.Count(reset);

        foregroundSequence = foreground;
        anyFound = amt != 0;

        return message.Length + amt * (foreground.Length - reset.Length);
    }

    /// <summary>
    /// Calculate info for replacing resets with a color.
    /// </summary>
    /// <remarks>Chain with <see cref="ReplaceResetsWithConsoleColor"/>.</remarks>
    public static int GetReplaceResetsWithConsoleColorInfo(ReadOnlySpan<char> message, ReadOnlySpan<char> foregroundSequence, out bool anyFound)
    {
        ReadOnlySpan<char> reset = FormattingUtil.ForegroundResetSequence;
        int amt = message.Count(reset);
        anyFound = amt != 0;

        return message.Length + amt * (foregroundSequence.Length - reset.Length);
    }

    /// <summary>
    /// Replaces any virtual terminal reset sequences with the ANSI string for the provided ARGB or console color.
    /// </summary>
    /// <remarks>Chain with <see cref="GetReplaceResetsWithConsoleColorInfo(ReadOnlySpan{char}, out ReadOnlySpan{char}, out bool, int)"/>.</remarks>
    public static void ReplaceResetsWithConsoleColor(ReadOnlySpan<char> message, Span<char> output, ReadOnlySpan<char> foregroundSequence)
    {
        ReadOnlySpan<char> reset = FormattingUtil.ForegroundResetSequence;

        int lastIndex = -reset.Length;
        int outputIndex = 0;
        int sourceStartIndex;
        while (true)
        {
            int index = message.IndexOf(reset, lastIndex + reset.Length);
            if (index < 0 || lastIndex + reset.Length >= message.Length)
                break;

            sourceStartIndex = lastIndex + reset.Length;
            message.Slice(sourceStartIndex, index - sourceStartIndex).CopyTo(output[outputIndex..]);
            outputIndex += index - sourceStartIndex;
            foregroundSequence.CopyTo(output[outputIndex..]);
            outputIndex += foregroundSequence.Length;

            lastIndex = index;
        }

        sourceStartIndex = lastIndex + reset.Length;
        message.Slice(sourceStartIndex, message.Length - sourceStartIndex).CopyTo(output[outputIndex..]);
    }

    /// <summary>
    /// Adds various underlying types from UniTask to the skipped types list.
    /// </summary>
    public static void AddUniTaskSkippedTypes(this StackCleanerConfiguration config)
    {
        Type? uniTaskType = Type.GetType("Cysharp.Threading.Tasks.UniTask, UniTask", false, false);

        if (uniTaskType == null)
            return;

        List<Type> hiddenTypes = [ ..config.GetHiddenTypes(), uniTaskType ];
        Assembly uniTask = uniTaskType.Assembly;
        Type? type = uniTask.GetType("Cysharp.Threading.Tasks.EnumeratorAsyncExtensions+EnumeratorPromise", false, false);

        if (type != null)
            hiddenTypes.Add(type);

        type = uniTask.GetType("Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask`1", false, false);

        if (type != null)
            hiddenTypes.Add(type);

        type = uniTask.GetType("Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask`2", false, false);

        if (type != null)
            hiddenTypes.Add(type);

        foreach (Type baseType in uniTaskType
                     .GetNestedTypes(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                     .Where(x => x.Name.IndexOf("promise", StringComparison.OrdinalIgnoreCase) != -1))
        {
            hiddenTypes.Add(baseType);
        }

        config.HiddenTypes = hiddenTypes;
    }
    public static void DumpJson<T>(T obj, ConsoleColor color = ConsoleColor.DarkGray, bool condensed = false, Severity severity = Severity.Debug)
    {
        if (obj == null)
        {
            Logger.Terminal.Write("null", color, true, severity);
            return;
        }

        try
        {
            Logger.Terminal.Write(JsonSerializer.Serialize(obj, obj.GetType(), condensed ? DevkitServerConfig.CondensedSerializerSettings : DevkitServerConfig.SerializerSettings),
                color, true, severity);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("DumpJson<>", ex, $"Error serializing {typeof(T).Name}.");
        }
    }
    public static void DumpGameObject(this GameObject go, ConsoleColor color = ConsoleColor.White, Severity severity = Severity.Debug)
    {
        if (!DevkitServerModule.UnityLoaded)
            throw new InvalidOperationException("External access violation.");

        Logger.Terminal.Write("Gameobject Dump: \"" + go.name + "\":", color, true, severity);
        Logger.Terminal.Write("Transform:", color, true, severity);
        Logger.Terminal.Write($" Parent: {(go.transform.parent == null ? "none" : go.transform.parent.name)}", color, true, severity);
        Logger.Terminal.Write($" Position: {go.transform.position:F2}", color, true, severity);
        Logger.Terminal.Write($" Rotation: {go.transform.rotation.eulerAngles:F2}", color, true, severity);
        Logger.Terminal.Write($" Scale:    {go.transform.localScale:F2}", color, true, severity);
        Logger.Terminal.Write("Components:", color, true, severity);
        Component[] comps = go.GetComponents<Component>();
        Logger.Terminal.Write(" ========================================", color, true, severity);

        foreach (Component comp in comps)
        {
            Logger.Terminal.Write($" Parent: {comp.transform.gameObject.name}", color, true, severity);
            Logger.Terminal.Write($" Type: {comp.GetType().Format()}{FormattingUtil.GetTerminalColorSequenceString(color, false)}", color, true, severity);
            Logger.Terminal.Write(" ========================================", color, true, severity);
        }
        int childCt = go.transform.childCount;

        Logger.Terminal.Write($"Children: {childCt}:", color, true, severity);
        for (int i = 0; i < childCt; ++i)
        {
            DumpGameObject(go.transform.GetChild(i).gameObject, color);
        }
    }

    public static void DefaultWriteException(object? source, Severity severity, int baseColorArgb, ITerminal terminal, Exception exception)
    {
        WriteExceptionIntl(source, severity, baseColorArgb, terminal, exception, 0, DateTimeOffset.UtcNow);
    }
    private static void WriteExceptionIntl(object? source, Severity severity, int baseColorArgb, ITerminal terminal, Exception exception, int indent, DateTimeOffset timestamp)
    {
        string ind = indent == 0 ? string.Empty : new string(' ', indent);
        bool inner = indent > 0;
        ReadOnlySpan<char> foregroundSpan = FormattingUtil.GetTerminalColorSequenceString(baseColorArgb, false);
        int len = GetSourceLength(source, foregroundSpan, out ReadOnlySpan<char> sourceSpan, out bool anyResetsInSource);

        string? srcString;
        if (anyResetsInSource)
        {
            Span<char> sourceStr = stackalloc char[len];
            ReplaceResetsWithConsoleColor(sourceSpan, sourceStr, foregroundSpan);
            srcString = sourceStr.ToString();
        }
        else srcString = sourceSpan.ToString();

        if (srcString.Length == 0)
            srcString = null;

        string prefix = FormattingUtil.GetTerminalColorSequenceString(baseColorArgb, false) ?? string.Empty;
        string suffix = FormattingUtil.GetResetSuffix();
        ConsoleColor? color = baseColorArgb >> 24 == 0 ? (ConsoleColor)baseColorArgb : null;

        while (exception != null)
        {
            if (inner)
                terminal.Write(default, color, true, severity);

            string message = GetErrorMessage(exception) ?? "No message";
            terminal.Write(prefix + ind + (inner
                               ? "Inner Exception: "
                               : "[" + timestamp.ToString(LogTimeFormat) + "]" + (srcString == null ? string.Empty : " [" + srcString + "] ") + "Exception: ")
                           + exception.GetType().Format() + prefix + "." + suffix, color, true, severity);
            terminal.Write(prefix + ind + message + suffix, color, true, severity);
            if (DevkitServerModule.Module != null)
                Logger.LoadingErrors?.Add((DateTime.UtcNow, severity, message, color, srcString ?? string.Empty));
            if (exception is TypeLoadException t)
            {
                terminal.Write(prefix + ind + "Type: " + t.TypeName + suffix, color, true, severity);
            }
            else if (exception is ReflectionTypeLoadException t2)
            {
                terminal.Write(prefix + ind + "Type load exceptions:" + suffix, color, true, severity);
                foreach (Exception ex2 in t2.LoaderExceptions)
                {
                    WriteExceptionIntl(source, severity, baseColorArgb, terminal, ex2, indent + 1, timestamp);
                }
            }
            else if (exception is TargetInvocationException { InnerException: not null } t4)
            {
                terminal.Write(ind + "Invoked exception:", ConsoleColor.DarkRed, true, Severity.Error);
                WriteExceptionIntl(source, severity, baseColorArgb, terminal, t4.InnerException, indent + 1, timestamp);
            }
            else if (exception is AggregateException t3)
            {
                terminal.Write(ind + "Inner exceptions:", ConsoleColor.DarkRed, true, Severity.Error);
                foreach (Exception ex2 in t3.InnerExceptions)
                {
                    WriteExceptionIntl(source, severity, baseColorArgb, terminal, ex2, indent + 1, timestamp);
                }
            }
            if (exception.StackTrace != null)
            {
                if (Logger.UseStackCleanerForExceptions)
                {
                    string str;
                    try
                    {
                        str = Logger.StackCleaner.GetString(exception);
                    }
                    catch (Exception ex2)
                    {
                        terminal.Write("Ran into an error cleaning a stack trace: " + ex2.Message + " (" + ex2.GetType().Format() + ").", ConsoleColor.DarkGray, true, Severity.Debug);
                        terminal.Write(ex2.ToString(), ConsoleColor.DarkGray, true, Severity.Debug);
                        str = exception.StackTrace;
                    }

                    terminal.Write(str, color, true, severity);
                }
                else
                {
                    terminal.Write(exception.StackTrace, color, true, severity);
                }
            }
            if (exception is AggregateException or TargetInvocationException)
                break;

            exception = exception.InnerException!;
            inner = true;
        }
    }
    private static string? GetErrorMessage(Exception ex)
    {
        return ex.Message != null && ex is BadImageFormatException && ex.Message.Equals("Method has zero rva", StringComparison.Ordinal) ? "Method has no body (Method has zero rva)" : ex.Message;
    }

#if CLIENT
    internal static void ReadInitialVanillaLogs()
    {
        try
        {
            string log = Logs.getLogFilePath();

            if (!File.Exists(log))
                return;

            using FileStream str = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using StreamReader reader = new StreamReader(str, Encoding.GetEncoding(65001, new EncoderReplacementFallback(), new DecoderReplacementFallback()));
            while (reader.ReadLine() is { } line)
            {
                OnLinePrinted(line);
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogWarning(nameof(ReadInitialVanillaLogs), ex, "Failed to fetch vanilla logs.");
        }
    }
    internal static void ClientPatchUnturnedLogs()
    {
        try
        {
            MethodInfo? method = typeof(LogFile).GetMethod(nameof(LogFile.writeLine), BindingFlags.Instance | BindingFlags.Public);
            if (method != null)
                PatchesMain.Patcher.Patch(method, prefix: Accessor.Active.GetHarmonyMethod(OnLinePrinted));
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogWarning(nameof(ClientPatchUnturnedLogs), ex, "Failed to patch vanilla logs.");
        }
    }
    internal static void ClientUnpatchUnturnedLogs()
    {
        try
        {
            MethodInfo? method = typeof(LogFile).GetMethod(nameof(LogFile.writeLine), BindingFlags.Instance | BindingFlags.Public);
            if (method != null)
                PatchesMain.Patcher.Unpatch(method, Accessor.GetMethod(OnLinePrinted));
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogWarning(nameof(ClientUnpatchUnturnedLogs), ex, "Failed to unpatch vanilla logs.");
        }
    }
    private static void OnLinePrinted(string line)
    {
        if (Logger.Terminal is not { IsComittingToUnturnedLog: false })
            return;
        line = RemoveDateFromLine(line).ToString();
        string msg2 = line;
        if (msg2.IndexOf("[DEVKIT SERVER] [LAUNCHER]", StringComparison.Ordinal) != -1)
        {
            ConsoleColor color = ConsoleColor.DarkCyan;
            Severity severity = Severity.Info;
            if (msg2.IndexOf("[INF]", StringComparison.Ordinal) == -1)
            {
                if (msg2.IndexOf("[DBG]", StringComparison.Ordinal) != -1)
                {
                    color = ConsoleColor.DarkGray;
                    severity = Severity.Debug;
                }
                else if (msg2.IndexOf("[WRN]", StringComparison.Ordinal) != -1)
                {
                    color = ConsoleColor.Yellow;
                    severity = Severity.Warning;
                }
                else if (msg2.IndexOf("[ERR]", StringComparison.Ordinal) != -1)
                {
                    color = ConsoleColor.Red;
                    severity = Severity.Error;
                }
            }
            else if (msg2.IndexOf("Update available", StringComparison.Ordinal) != -1)
            {
                color = ConsoleColor.Green;
            }

            Logger.Terminal.Write(msg2, color, false, severity);
            return;
        }

        Logger.Terminal.Write("[" + DateTime.UtcNow.ToString(LogTimeFormat) + "] [LOG] [UNTURNED]      " + msg2, ConsoleColor.DarkGray, false, Severity.Info);
    }
#endif
}
