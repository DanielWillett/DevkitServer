using DevkitServer.API.Commands;
#if SERVER
using DevkitServer.Players;
#endif

namespace DevkitServer.Commands.Subsystem;
public class CommandParser
{
    public const int MaxArgCount = 32;

    public char[] Prefixes = { '/', '@', '\\' };
    public char[] ContinueArgChars = { '\'', '"', '`', '“', '”', '‘', '’' };
    protected readonly ArgumentInfo[] ArgumentBuffer = new ArgumentInfo[MaxArgCount];
    private readonly ICommandHandler _handler;
    public ICommandHandler Handler => _handler;
    public CommandParser(ICommandHandler handler)
    {
        _handler = handler;
    }
    protected struct ArgumentInfo
    {
        public int Start;
        public int End;
    }
    public virtual unsafe bool TryRunCommand(
#if SERVER
        EditorUser? user, 
#endif
#if CLIENT
        bool console, 
#endif
        string message, ref bool shouldList, bool requirePrefix)
    {
        ThreadUtil.assertIsGameThread();
        
        if (message == null || message.Length < (requirePrefix ? 2 : 1)) goto notCommand;
        int cmdStart = -1;
        int cmdEnd = -1;
        int argCt = -1;
        bool foundPrefix = false;
        int len = message.Length;
        int cmdInd = -1;
        bool inArg = false;
        fixed (char* ptr = message)
        {
            for (int i = 0; i < len; ++i)
            {
                char c = *(ptr + i);

                // check for '/' prefix
                if (!foundPrefix && requirePrefix)
                {
                    if (c == ' ') continue;
                    for (int j = 0; j < Prefixes.Length; ++j)
                    {
                        if (c == Prefixes[j])
                        {
                            foundPrefix = true;
                            break;
                        }
                    }
                    if (!foundPrefix) goto notCommand;
                    continue;
                }

                // start finding command name
                if (cmdStart == -1)
                {
                    if (c == ' ') goto c;
                    if (!requirePrefix)
                    {
                        // skip prefix
                        for (int j = 0; j < Prefixes.Length; ++j)
                        {
                            if (c == Prefixes[j]) goto c;
                        }
                    }
                    cmdStart = i;
                    c:;
                }

                // finish finding command name
                if (cmdEnd == -1)
                {
                    if (i != len - 1)
                    {
                        if (c == ' ')
                        {
                            // space after command name, check if next is a quotation mark
                            // if there's a character after the space start the next argument
                            char next = message[i + 1];
                            if (next != ' ')
                            {
                                for (int j = 0; j < ContinueArgChars.Length; ++j)
                                {
                                    if (next == ContinueArgChars[j])
                                        goto c;
                                }

                                ref ArgumentInfo info = ref ArgumentBuffer[++argCt];
                                info.End = -1;
                                info.Start = i + 1;
                            }

                            c:
                            cmdEnd = i - 1;
                            goto getCommand;
                        }

                        // quotation mark directly after the command name
                        for (int j = 0; j < ContinueArgChars.Length; ++j)
                        {
                            if (c == ContinueArgChars[j])
                            {
                                ref ArgumentInfo info = ref ArgumentBuffer[++argCt];
                                inArg = true;
                                info.End = -1;
                                info.Start = i + 1;
                                cmdEnd = i - 1;
                                goto getCommand;
                            }
                        }

                        continue;
                    }

                    // end the command at the last letter
                    cmdEnd = c == ' ' ? i - 1 : i;
                    goto getCommand;
                }

                // quotation mark
                for (int j = 0; j < ContinueArgChars.Length; ++j)
                {
                    if (c == ContinueArgChars[j])
                        goto contArgChr;
                }

                // space while not in quotation marks
                if (c == ' ' && !inArg)
                {
                    // end current argument if space at end of string
                    if (i == len - 1)
                    {
                        if (argCt != -1)
                        {
                            ref ArgumentInfo info2 = ref ArgumentBuffer[argCt];
                            if (info2.End == -1)
                                info2.End = i - 1;
                        }
                        break;
                    }

                    // end current argument if double space
                    char next = message[i + 1];
                    if (next == ' ')
                    {
                        if (argCt != -1)
                        {
                            ref ArgumentInfo info2 = ref ArgumentBuffer[argCt];
                            if (info2.End == -1)
                                info2.End = i - 1;
                        }
                        continue;
                    }

                    // if next is a quotation mark continue to next character which will end the current argument
                    for (int j = 0; j < ContinueArgChars.Length; ++j)
                    {
                        if (next == ContinueArgChars[j])
                            goto c;
                    }
                    goto n;
                    c:
                    continue;
                    n:
                    // end the current argument
                    if (argCt != -1)
                    {
                        ref ArgumentInfo info2 = ref ArgumentBuffer[argCt];
                        if (info2.End == -1)
                            info2.End = i - 1;
                    }
                    if (i == len - 1) break;
                    if (argCt >= MaxArgCount - 1)
                        goto runCommand;

                    // start next argument
                    ref ArgumentInfo info = ref ArgumentBuffer[++argCt];
                    info.End = -1;
                    info.Start = i + 1;
                }
                continue;

                contArgChr:
                if (inArg)
                {
                    // end current quotation mark argument
                    ref ArgumentInfo info = ref ArgumentBuffer[argCt];
                    info.End = i - 1;
                    inArg = false;
                    if (i < len - 1 && argCt < MaxArgCount - 1)
                    {
                        char next = message[i + 1];
                        bool cont = next == ' ';
                        // check for argument right after current and start a new one
                        if (!cont)
                        {
                            for (int j = 0; j < ContinueArgChars.Length; ++j)
                            {
                                if (next == ContinueArgChars[j])
                                {
                                    cont = true;
                                    break;
                                }
                            }
                            if (!cont)
                            {
                                info = ref ArgumentBuffer[++argCt];
                                info.Start = i + 1;
                                info.End = -1;
                            }
                        }
                    }
                }
                else
                {
                    // end current argument and start a quotation mark argument
                    if (argCt != -1)
                    {
                        ref ArgumentInfo info2 = ref ArgumentBuffer[argCt];
                        if (info2.End == -1)
                        {
                            if (message[i - 1] == ' ')
                                info2.End = i - 2;
                            else
                                info2.End = i - 1;
                        }
                    }
                    if (i == len - 1) break;
                    if (argCt >= MaxArgCount - 1)
                        goto runCommand;
                    ref ArgumentInfo info = ref ArgumentBuffer[++argCt];
                    info.Start = i + 1;
                    info.End = -1;
                    inArg = true;
                }
                continue;

                getCommand:
                shouldList = false;
                if (cmdStart < 0 || cmdEnd - cmdStart < 0)
                    goto notCommand;

                // find command, assumes already sorted by priority
                string command = new string(ptr, cmdStart, cmdEnd - cmdStart + 1);
                for (int k = 0; k < _handler.Commands.Count; ++k)
                {
                    IExecutableCommand cmd = _handler.Commands[k];
                    string c2 = cmd.CommandName;
                    if (command.Equals(c2, StringComparison.InvariantCultureIgnoreCase))
                    {
                        cmdInd = k;
                        break;
                    }

                    // check aliases
                    IList<string> aliases = cmd.Aliases;
                    if (aliases is { Count: > 0 })
                    {
                        for (int a = 0; a < aliases.Count; ++a)
                        {
                            string alias = aliases[a];
                            if (command.Equals(alias, StringComparison.InvariantCultureIgnoreCase))
                            {
                                cmdInd = k;
                                break;
                            }
                        }
                    }

                    if (cmdInd != -1)
                        break;
                }
                
                if (cmdInd == -1)
                    goto notCommand;
                if (i == len - 1) goto runCommand;
            }
            if (argCt != -1)
            {
                ref ArgumentInfo info = ref ArgumentBuffer[argCt];
                // check last argument for quotation mark
                if (info.End == -1)
                {
                    bool endIsQuote = false;
                    char end = message[len - 1];
                    for (int j = 0; j < ContinueArgChars.Length; ++j)
                    {
                        if (end == ContinueArgChars[j])
                        {
                            endIsQuote = true;
                            break;
                        }
                    }
                    if (endIsQuote)
                    {
                        info.End = len - 2;
                    }
                    else
                    {
                        info.End = len;
                        // trim argument
                        do --info.End;
                        while (message[info.End] == ' ' && info.End > -1);
                        if (info.End > 0)
                        {
                            endIsQuote = false;
                            end = message[info.End];
                            for (int j = 0; j < ContinueArgChars.Length; ++j)
                            {
                                if (end == ContinueArgChars[j])
                                {
                                    endIsQuote = true;
                                    break;
                                }
                            }
                            if (endIsQuote) --info.End;
                        }
                    }
                }
            }

            // prepare command for execution
            runCommand:
            if (cmdInd == -1) goto notCommand;
            int ct2 = 0;
            for (int i = 0; i <= argCt; ++i)
            {
                ref ArgumentInfo ai = ref ArgumentBuffer[i];
                if (ai.End > 0) ct2++;
            }

            int i3 = -1;
            // prepare argument array
            string[] args = argCt == -1 ? Array.Empty<string>() : new string[ct2];
            for (int i = 0; i <= argCt; ++i)
            {
                ref ArgumentInfo ai = ref ArgumentBuffer[i];
                if (ai.End < 1) continue;
                args[++i3] = new string(ptr, ai.Start, ai.End - ai.Start + 1);
            }

            string originalMessage = message;
            if (!requirePrefix && Prefixes.Length > 0)
            {
                // add prefix for consistancy if left out
                char prefix = originalMessage[0];
                for (int i = 0; i < Prefixes.Length; ++i)
                {
                    if (Prefixes[i] == prefix)
                        goto brk2;
                }

                originalMessage = Prefixes[0] + originalMessage;
            }
            brk2:
            _handler.ExecuteCommand(_handler.Commands[cmdInd],
#if SERVER
                user, 
#endif
#if CLIENT
                console,
#endif
                args, originalMessage);
        }

        shouldList = false;
        return true;
        notCommand:
        return false;
    }
}
