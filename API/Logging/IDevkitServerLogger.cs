namespace DevkitServer.API.Logging;

/// <summary>
/// Represents a log handler for DevkitServer.
/// </summary>
public interface IDevkitServerLogger
{
    /// <summary>
    /// Accepts a log line.
    /// </summary>
    /// <param name="source">Usually will be <see cref="ILogSource"/>, <see cref="string"/>, or <see langword="null"/>.</param>
    /// <param name="baseColorArgb">Console colors will have an A=0 and can be casted straight to <see cref="ConsoleColor"/>.</param>
    void AddLog(ITerminal terminal, object? source, Severity severity, ReadOnlySpan<char> message, Exception? exception, int baseColorArgb);
}