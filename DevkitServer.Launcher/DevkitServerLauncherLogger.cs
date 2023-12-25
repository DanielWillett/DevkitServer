﻿using NuGet.Common;
using SDG.Unturned;
using System;
using System.Threading.Tasks;
using ILogger = NuGet.Common.ILogger;

namespace DevkitServer.Launcher;
internal class DevkitServerLauncherLogger : ILogger
{
    private const string TimeFormat = "hh:mm:ss";
    public bool ShouldLogDebug { get; set; }
    private static string GetPrefix(LogLevel lvl) => GetPrefix(lvl, DateTime.UtcNow);
    private static string GetPrefix(LogLevel lvl, DateTime time) => "[" + time.ToString(TimeFormat) +
                                                                    "] [" + lvl switch
                                                                    {
                                                                        LogLevel.Debug => "DBG",
                                                                        LogLevel.Error => "ERR",
                                                                        LogLevel.Minimal => "MIN",
                                                                        LogLevel.Verbose => "VRB",
                                                                        LogLevel.Warning => "WRN",
                                                                        _ => "INF"
                                                                    } + "] [DEVKIT SERVER] [LAUNCHER] ";
    public void Log(LogLevel level, string data)
    {
        if (level <= LogLevel.Debug && !ShouldLogDebug)
            return;
        if (level == LogLevel.Warning)
            LogWarning(data);
        else if (level == LogLevel.Error)
            LogError(data);
        else
            CommandWindow.Log(GetPrefix(level) + data + (data.Length > 0 && data[data.Length - 1] == '.' ? string.Empty : "."));
    }
    public void Log(ILogMessage message)
    {
        string data = GetPrefix(message.Level, message.Time.UtcDateTime) +
                      "[" + message.WarningLevel + "] " + message.Code + " " +
                      message.Message +
                      (message.Message.Length > 0 && message.Message[message.Message.Length - 1] == '.' ? string.Empty : ".");

        if (message.Level == LogLevel.Warning)
            CommandWindow.LogWarning(data);
        else if (message.Level == LogLevel.Error)
            CommandWindow.LogError(data);
        else
            CommandWindow.Log(data);
    }

    public void LogDebug(string data)
    {
        Log(LogLevel.Debug, data);
    }

    public void LogVerbose(string data)
    {
        Log(LogLevel.Verbose, data);
    }

    public void LogInformation(string data)
    {
        Log(LogLevel.Information, data);
    }

    public void LogMinimal(string data)
    {
        Log(LogLevel.Minimal, data);
    }

    public void LogWarning(string data)
    {
        CommandWindow.LogWarning(GetPrefix(LogLevel.Warning) + data + (data.Length > 0 && data[data.Length - 1] == '.' ? string.Empty : "."));
    }

    public void LogError(string data)
    {
        CommandWindow.LogError(GetPrefix(LogLevel.Error) + data + (data.Length > 0 && data[data.Length - 1] == '.' ? string.Empty : "."));
    }
    public void LogInformationSummary(string data)
    {
        Log(LogLevel.Information, data);
    }
    Task ILogger.LogAsync(LogLevel level, string data)
    {
        Log(level, data);
        return Task.CompletedTask;
    }
    Task ILogger.LogAsync(ILogMessage message)
    {
        Log(message);
        return Task.CompletedTask;
    }
}
