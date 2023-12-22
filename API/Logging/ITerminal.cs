namespace DevkitServer.API.Logging;

/// <summary>
/// Represents a command line interface.
/// </summary>
public interface ITerminal
{
    /// <summary>
    /// Invoke when input is received from the command line.
    /// </summary>
    event TerminalPreReadDelegate? OnInput;

    /// <summary>
    /// Invoke before outputting data to the command line.
    /// </summary>
    event TerminalPreWriteDelegate? OnOutput;

    /// <summary>
    /// Is manually adding logs to <see cref="Logs"/> that weren't added before.
    /// </summary>
    bool IsComittingToUnturnedLog { get; }

    /// <summary>
    /// Write or queue a write for command line data.
    /// </summary>
    void Write(ReadOnlySpan<char> input, ConsoleColor? color, bool saveToUnturnedLog, Severity severity);

    /// <summary>
    /// Called on start.
    /// </summary>
    void Init();

    /// <summary>
    /// Called on close or when a new <see cref="ITerminal"/> is set.
    /// </summary>
    void Close();
}

public delegate void TerminalPreReadDelegate(ReadOnlySpan<char> inputMessage, ref bool shouldHandle);
public delegate void TerminalPostReadDelegate(ReadOnlySpan<char> inputMessage);

public delegate void TerminalPreWriteDelegate(ref ReadOnlySpan<char> outputMessage, ref ConsoleColor? color, Severity severity);
public delegate void TerminalPostWriteDelegate(ReadOnlySpan<char> outputMessage, ConsoleColor? color, Severity severity);