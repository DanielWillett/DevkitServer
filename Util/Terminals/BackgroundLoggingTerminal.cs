using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DevkitServer.Util.Terminals;
internal sealed class BackgroundLoggingTerminal : MonoBehaviour, ITerminal
{
    private bool _writing;
    public event TerminalReadDelegate? OnInput;
    public event TerminalWriteDelegate? OnOutput;
    public bool IsCommitingToUnturnedLog => _writing;
    
    public void Write(string input, ConsoleColor color, bool save, Severity severity)
    {
        OnOutput?.Invoke(ref input, ref color);
        if (save)
        {
            _writing = true;
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
            _writing = false;
        }
    }
    public void Init() { }
    public void Close() { }
}
