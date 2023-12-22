namespace DevkitServer.Core.Logging;

public readonly record struct LogMessage(string Message, ConsoleColor? Color, bool SaveToUnturnedLog, Severity Severity);