namespace DevkitServer.Core.Logging.Loggers;

/// <summary>
/// Provides a 'core mod' prefix for logs and formats them as <c>[time] [severity] [core] [source] message</c>.
/// </summary>
/// <param name="coreType">Text to go in the 'core' section.</param>
public class CoreLogger(string coreType) : IDevkitServerLogger
{
    /// <summary>
    /// Text to go in the 'core' section.
    /// </summary>
    public virtual string CoreType { get; } = coreType;

    /// <summary>
    /// Determines the visibility of a message of a severity based on it's source.
    /// </summary>
    public virtual bool IsSeverityEnabled(Severity severity, object? source) => LoggerExtensions.IsSeverityEnabled(severity, source);
    public void AddLog(ITerminal terminal, object? source, Severity severity, ReadOnlySpan<char> message, Exception? exception, int baseColorArgb)
    {
        if (!IsSeverityEnabled(severity, source))
            return;

        string? newLogMessage = LoggerExtensions.FormatAddedLogMessage(baseColorArgb, message, out ConsoleColor? color, out ReadOnlySpan<char> foregroundSequence);

        bool msgNull = string.IsNullOrWhiteSpace(newLogMessage);

        if (!msgNull || exception == null)
        {
            int sourceLen = LoggerExtensions.GetSourceLength(source, foregroundSequence, out ReadOnlySpan<char> sourceSpan, out bool anyResetsInSource);

            ReadOnlySpan<char> timeSpan = DateTime.UtcNow.ToString(LoggerExtensions.LogTimeFormat);
            ReadOnlySpan<char> coreTypeSpan = CoreType;

            int coreLen = LoggerExtensions.GetReplaceResetsWithConsoleColorInfo(coreTypeSpan, foregroundSequence, out bool anyCoreResetsFound);

            ReadOnlySpan<char> resetSequence = foregroundSequence.Length > 0 ? FormattingUtil.GetResetSuffix() : default;
            int severityLen = LoggerExtensions.GetSeverityLength(severity, foregroundSequence);
            int len = severityLen + resetSequence.Length + (color.HasValue ? 0 : foregroundSequence.Length) + timeSpan.Length + coreTypeSpan.Length + 8;

            if (sourceLen != 0)
                len += 3 + sourceLen;

            if (!msgNull)
                len += newLogMessage!.Length + 1;

            Span<char> outputMessage = len > 256 ? new char[len] : stackalloc char[len];
            int index = 0;
            if (!color.HasValue)
            {
                foregroundSequence.CopyTo(outputMessage);
                index = foregroundSequence.Length;
            }
            outputMessage[index++] = '[';
            timeSpan.CopyTo(outputMessage[index..]);
            index += timeSpan.Length;
            outputMessage[index++] = ']';
            outputMessage[index++] = ' ';
            outputMessage[index++] = '[';
            LoggerExtensions.FormatSeverity(severity, foregroundSequence, outputMessage[index..]);
            index += severityLen;
            outputMessage[index++] = ']';
            outputMessage[index++] = ' ';
            outputMessage[index++] = '[';
            if (!anyCoreResetsFound)
                coreTypeSpan.CopyTo(outputMessage[index..]);
            else
                LoggerExtensions.ReplaceResetsWithConsoleColor(coreTypeSpan, outputMessage[index..], foregroundSequence);
            index += coreLen;
            outputMessage[index++] = ']';

            if (sourceLen != 0)
            {
                outputMessage[index++] = ' ';
                outputMessage[index++] = '[';
                if (!anyResetsInSource)
                    sourceSpan.CopyTo(outputMessage[index..]);
                else
                    LoggerExtensions.ReplaceResetsWithConsoleColor(sourceSpan, outputMessage[index..], foregroundSequence);
                index += sourceLen;
                outputMessage[index++] = ']';
            }

            if (!msgNull)
            {
                outputMessage[index++] = ' ';
                newLogMessage.AsSpan().CopyTo(outputMessage[index..]);
                index += newLogMessage!.Length;
            }

            resetSequence.CopyTo(outputMessage[index..]);

            terminal.Write(outputMessage, color, true, severity);
        }

        if (exception != null)
        {
            LoggerExtensions.DefaultWriteException(source, severity, baseColorArgb, terminal, exception);
        }
    }
}