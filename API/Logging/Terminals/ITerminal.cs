namespace DevkitServer.API.Logging.Terminals;

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
    void Write(string input, ConsoleColor color, bool save, Severity severity);

    /// <summary>
    /// Called on start.
    /// </summary>
    void Init();

    /// <summary>
    /// Called on close or when a new <see cref="ITerminal"/> is set.
    /// </summary>
    void Close();
}

public delegate void TerminalPreReadDelegate(string inputMessage, ref bool shouldHandle);
public delegate void TerminalPostReadDelegate(string inputMessage);

public delegate void TerminalPreWriteDelegate(ref string outputMessage, ref ConsoleColor color);
public delegate void TerminalPostWriteDelegate(string outputMessage, ConsoleColor color);