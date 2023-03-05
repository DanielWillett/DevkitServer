#if SERVER
using System;

namespace DevkitServer.Util.Terminals;
internal class ServerTerminal : MonoBehaviour, ITerminal
{
    public event TerminalReadDelegate? OnInput;
    public event TerminalWriteDelegate? OnOutput;
    public void Write(string input, ConsoleColor color)
    {
        OnOutput?.Invoke(ref input, ref color);
        if (color == ConsoleColor.Red)
            CommandWindow.LogError(input);
        else if (color == ConsoleColor.Yellow)
            CommandWindow.LogWarning(input);
        else if (color == ConsoleColor.Gray)
            CommandWindow.Log(input);
        else
            CommandWindow.Log(Logger.GetANSIForegroundString(color) + input + Logger.ANSIReset);
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