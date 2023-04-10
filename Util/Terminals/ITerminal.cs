using System;

namespace DevkitServer.Util.Terminals;
internal interface ITerminal
{
    event TerminalReadDelegate? OnInput;
    event TerminalWriteDelegate? OnOutput;
    bool IsCommitingToUnturnedLog { get; }
    void Write(string input, ConsoleColor color, bool save, Severity severity);
    void Init();
    void Close();
}

public delegate void TerminalReadDelegate(string inputMessage, ref bool shouldHandle);

public delegate void TerminalWriteDelegate(ref string outputMessage, ref ConsoleColor color);