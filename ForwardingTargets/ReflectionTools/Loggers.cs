using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using DevkitServer.API.Logging;
using DevkitServer.Util;

namespace DanielWillett.ReflectionTools;

public interface IReflectionToolsLogger
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    void LogDebug(string source, string message);
    [MethodImpl(MethodImplOptions.NoInlining)]
    void LogInfo(string source, string message);
    [MethodImpl(MethodImplOptions.NoInlining)]
    void LogWarning(string source, string message);
    [MethodImpl(MethodImplOptions.NoInlining)]
    void LogError(string source, Exception? ex, string? message);
}

[Ignore]
public class ReflectionToolsLoggerProxy : IReflectionToolsLogger, IDisposable
{
    private Dictionary<string, ILogger>? _loggers;
    public ILoggerFactory LoggerFactory { get; }
    public bool DisposeFactoryOnDispose { get; }
    public ReflectionToolsLoggerProxy(ILoggerFactory loggerFactory, bool disposeFactoryOnDispose = false)
    {
        LoggerFactory = loggerFactory;
        DisposeFactoryOnDispose = disposeFactoryOnDispose;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void LogDebug(string source, string message)
    {
        ILogger logger = GetOrAddLogger(source);
        logger.LogDebug(message);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void LogInfo(string source, string message)
    {
        ILogger logger = GetOrAddLogger(source);
        logger.LogInformation(message);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void LogWarning(string source, string message)
    {
        ILogger logger = GetOrAddLogger(source);
        logger.LogWarning(message);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void LogError(string source, Exception? ex, string? message)
    {
        ILogger logger = GetOrAddLogger(source);
        logger.LogError(ex, message);
    }
    private ILogger GetOrAddLogger(string? source)
    {
        if (source == null)
            return LoggerFactory.CreateLogger("DanielWillett.ReflectionTools");

        lock (this)
        {
            _loggers ??= new Dictionary<string, ILogger>(4);
            if (!_loggers.TryGetValue(source, out ILogger logger))
                _loggers.Add(source, logger = LoggerFactory.CreateLogger("DanielWillett.ReflectionTools::" + source));

            return logger;
        }
    }
    public void Dispose()
    {
        if (DisposeFactoryOnDispose)
            LoggerFactory.Dispose();
    }
}

public class ConsoleReflectionToolsLogger : IReflectionToolsLogger
{
    public ConsoleColor DebugColor { get; set; } = ConsoleColor.DarkGray;
    public ConsoleColor InfoColor { get; set; } = ConsoleColor.DarkCyan;
    public ConsoleColor WarningColor { get; set; } = ConsoleColor.Yellow;
    public ConsoleColor ErrorColor { get; set; } = ConsoleColor.Red;
    public bool LogErrorStackTrace { get; set; } = true;
    public bool LogWarningStackTrace { get; set; } = false;
    public bool LogInfoStackTrace { get; set; } = false;
    public bool LogDebugStackTrace { get; set; } = false;
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void LogDebug(string source, string? message)
    {
        if (!string.IsNullOrEmpty(message))
            Logger.LogDebug("[" + source + "] " + message, DebugColor);
        else
            Logger.LogDebug("[" + source + "]", DebugColor);

        if (LogDebugStackTrace)
            Logger.LogDebug("[" + source + "] " + Environment.NewLine + FormattingUtil.FormatProvider.StackCleaner.GetString(new StackTrace(1)), DebugColor);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void LogInfo(string source, string? message)
    {
        if (!string.IsNullOrEmpty(message))
            Logger.LogInfo("[" + source + "] " + message, InfoColor);
        else
            Logger.LogInfo("[" + source + "]", InfoColor);

        if (LogInfoStackTrace)
            Logger.LogInfo("[" + source + "] " + Environment.NewLine + FormattingUtil.FormatProvider.StackCleaner.GetString(new StackTrace(1)), InfoColor);
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void LogWarning(string source, string? message)
    {
        if (!string.IsNullOrEmpty(message))
            Logger.LogWarning(message!, WarningColor, method: source);
        else
            Logger.LogWarning(string.Empty, WarningColor);

        if (LogWarningStackTrace)
            Logger.LogWarning(Environment.NewLine + FormattingUtil.FormatProvider.StackCleaner.GetString(new StackTrace(1)), WarningColor, method: source);
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void LogError(string source, Exception? ex, string? message)
    {
        if (!string.IsNullOrEmpty(message))
            Logger.LogError(message!, ErrorColor, method: source);

        if (ex != null)
            Logger.LogError(ex, method: source);
        
        if (ex == null && string.IsNullOrEmpty(message))
            Logger.LogError(string.Empty, ErrorColor);
        else if (ex == null && LogErrorStackTrace)
            Logger.LogError(Environment.NewLine + FormattingUtil.FormatProvider.StackCleaner.GetString(new StackTrace(1)), ErrorColor, method: source);
    }
}