using System;
using DevkitServer.API.Logging;

namespace DevkitServer.API.Logging.Terminals;
public interface ITerminal
{
    event TerminalPreReadDelegate? OnInput;
    event TerminalPreWriteDelegate? OnOutput;
    bool IsComittingToUnturnedLog { get; }
    void Write(string input, ConsoleColor color, bool save, Severity severity);
    void Init();
    void Close();
}

public delegate void TerminalPreReadDelegate(string inputMessage, ref bool shouldHandle);
public delegate void TerminalPostReadDelegate(string inputMessage);

public delegate void TerminalPreWriteDelegate(ref string outputMessage, ref ConsoleColor color);
public delegate void TerminalPostWriteDelegate(string outputMessage, ConsoleColor color);