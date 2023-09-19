using NuGet.Common;
using System;
using System.Threading.Tasks;
using ILogger = NuGet.Common.ILogger;

namespace DevkitServer.Launcher;
internal class DevkitServerLauncherLogger : ILogger
{
    private const string TimeFormat = "yyyy-MM-dd hh:mm:ss";

    private static string GetPrefix(LogLevel lvl) => GetPrefix(lvl, DateTime.UtcNow);
    private static string GetPrefix(LogLevel lvl, DateTime time) => "[" + time.ToString(TimeFormat) +
                                                                    "] [DEVKITSERVER.LAUNCHER] [" + lvl switch
                                                                    {
                                                                        LogLevel.Debug => "DEBUG",
                                                                        LogLevel.Error => "ERROR",
                                                                        LogLevel.Minimal => "MIN",
                                                                        LogLevel.Verbose => "VRBSE",
                                                                        LogLevel.Warning => "WARN",
                                                                        _ => "INFO"
                                                                    } + "]" + lvl switch
                                                                    {
                                                                        LogLevel.Debug or LogLevel.Error or LogLevel.Verbose => " ",
                                                                        LogLevel.Minimal => "   ",
                                                                        _ => "  "
                                                                    };
    public void Log(LogLevel level, string data)
    {
#if !DEBUG
        if (level == LogLevel.Debug)
            return;
#endif
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
