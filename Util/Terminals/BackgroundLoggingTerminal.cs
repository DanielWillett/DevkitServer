using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DevkitServer.Util.Terminals;
internal sealed class BackgroundLoggingTerminal : MonoBehaviour, ITerminal
{
    public event TerminalReadDelegate? OnInput;
    public event TerminalWriteDelegate? OnOutput;
    public void Write(string input, ConsoleColor color)
    {
        OnOutput?.Invoke(ref input, ref color);
        switch (color)
        {
            case ConsoleColor.Yellow:
            case ConsoleColor.DarkYellow:
                UnturnedLog.warn(input);
                break;
            case ConsoleColor.Red:
            case ConsoleColor.DarkRed:
                UnturnedLog.error(input);
                break;
            default:
                UnturnedLog.info(input);
                break;
        }
    }
    public void Init() { }
    public void Close() { }
}
