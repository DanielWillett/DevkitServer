#if SERVER
using System;

namespace DevkitServer.Util.Terminals;
internal class ServerTerminal : MonoBehaviour, ITerminal
{
    private bool _writing;
    public event TerminalReadDelegate? OnInput;
    public event TerminalWriteDelegate? OnOutput;

    public bool IsCommitingToUnturnedLog => _writing;

    public void Write(string input, ConsoleColor color, bool save)
    {
        OnOutput?.Invoke(ref input, ref color);
        if (save)
        {
            _writing = true;
            if (color == ConsoleColor.Red)
                CommandWindow.LogError(input);
            else if (color == ConsoleColor.Yellow)
                CommandWindow.LogWarning(input);
            else if (color == ConsoleColor.Gray)
                CommandWindow.Log(input);
            else
                CommandWindow.Log(Logger.GetANSIForegroundString(color) + input + Logger.ANSIReset);
            _writing = false;
        }
    }

    public void Init()
    {
        CommandWindow.onCommandWindowInputted += OnInputted;
    }

    public void Close()
    {
        CommandWindow.onCommandWindowInputted -= OnInputted;
    }

    private void OnInputted(string text, ref bool shouldexecutecommand)
    {
        OnInput?.Invoke(text, ref shouldexecutecommand);
    }
}
#endif