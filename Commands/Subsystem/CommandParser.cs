using DevkitServer.API.Commands;
#if SERVER
using DevkitServer.Players;
#endif

namespace DevkitServer.Commands.Subsystem;
public class CommandParser
{
    private static readonly char[] Prefixes = { '/', '@', '\\' };
    private static readonly char[] ContinueArgChars = { '\'', '"', '`', '“', '”', '‘', '’' };
    private const int MaxArgCount = 24;
    private static readonly ArgumentInfo[] ArgBuffer = new ArgumentInfo[MaxArgCount];
    private readonly ICommandHandler _handler;
    public CommandParser(ICommandHandler handler)
    {
        _handler = handler;
    }
    private struct ArgumentInfo
    {
        public int Start;
        public int End;
    }
    internal virtual unsafe bool TryRunCommand(
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

                if (cmdStart == -1)
                {
                    if (c == ' ') goto c;
                    if (!requirePrefix)
                    {
                        for (int j = 0; j < Prefixes.Length; ++j)
                        {
                            if (c == Prefixes[j]) goto c;
                        }
                    }
                    cmdStart = i;
                    c:;
                }

                if (cmdEnd == -1)
                {
                    if (i != len - 1)
                    {
                        if (c == ' ')
                        {
                            char next = message[i + 1];
                            if (next != ' ')
                            {
                                for (int j = 0; j < ContinueArgChars.Length; ++j)
                                {
                                    if (next == ContinueArgChars[j])
                                        goto c;
                                }

                                ref ArgumentInfo info = ref ArgBuffer[++argCt];
                                info.End = -1;
                                info.Start = i + 1;
                            }

                            c:
                            cmdEnd = i - 1;
                            goto getCommand;
                        }
                        for (int j = 0; j < ContinueArgChars.Length; ++j)
                        {
                            if (c == ContinueArgChars[j])
                            {
                                ref ArgumentInfo info = ref ArgBuffer[++argCt];
                                info.End = -1;
                                info.Start = i + 1;
                                cmdEnd = i - 1;
                                goto getCommand;
                            }
                        }

                        continue;
                    }

                    cmdEnd = c == ' ' ? i - 1 : i;
                    goto getCommand;
                }

                for (int j = 0; j < ContinueArgChars.Length; ++j)
                {
                    if (c == ContinueArgChars[j])
                        goto contArgChr;
                }

                if (c == ' ' && !inArg)
                {
                    if (i == len - 1)
                    {
                        if (argCt != -1)
                        {
                            ref ArgumentInfo info2 = ref ArgBuffer[argCt];
                            if (info2.End == -1)
                                info2.End = i - 1;
                        }
                        break;
                    }

                    char next = message[i + 1];
                    if (next == ' ')
                    {
                        if (argCt != -1)
                        {
                            ref ArgumentInfo info2 = ref ArgBuffer[argCt];
                            if (info2.End == -1)
                                info2.End = i - 1;
                        }
                        continue;
                    }

                    for (int j = 0; j < ContinueArgChars.Length; ++j)
                    {
                        if (next == ContinueArgChars[j])
                            goto c;
                    }
                    goto n;
                    c:
                    continue;
                    n:
                    if (argCt != -1)
                    {
                        ref ArgumentInfo info2 = ref ArgBuffer[argCt];
                        if (info2.End == -1)
                            info2.End = i - 1;
                    }
                    if (i == len - 1) break;
                    if (argCt >= MaxArgCount - 1)
                        goto runCommand;
                    ref ArgumentInfo info = ref ArgBuffer[++argCt];
                    info.End = -1;
                    info.Start = i + 1;
                }
                continue;

                contArgChr:
                if (inArg)
                {
                    ref ArgumentInfo info = ref ArgBuffer[argCt];
                    info.End = i - 1;
                    inArg = false;
                }
                else
                {
                    if (argCt != -1)
                    {
                        ref ArgumentInfo info2 = ref ArgBuffer[argCt];
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
                    ref ArgumentInfo info = ref ArgBuffer[++argCt];
                    info.Start = i + 1;
                    info.End = -1;
                    inArg = true;
                }
                continue;
                getCommand:
                shouldList = false;
                if (cmdStart < 0 || cmdEnd - cmdStart < 0)
                    goto notCommand;
                string command = new string(ptr, cmdStart, cmdEnd - cmdStart + 1);
                Logger.LogDebug("Command: \"" + command + "\"");
                for (int k = 0; k < _handler.Commands.Count; ++k)
                {
                    string c2 = _handler.Commands[k].CommandName;
                    if (command.Equals(c2, StringComparison.InvariantCultureIgnoreCase))
                    {
                        cmdInd = k;
                        break;
                    }
                }

                if (cmdInd == -1)
                {
                    for (int k = 0; k < _handler.Commands.Count; ++k)
                    {
                        IExecutableCommand cmd = _handler.Commands[k];
                        if (cmd.Aliases is { Count: > 0 })
                        {
                            for (int a = 0; a < cmd.Aliases.Count; ++a)
                            {
                                string c2 = cmd.Aliases[a];
                                if (command.Equals(c2, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    cmdInd = k;
                                    goto brk;
                                }
                            }
                        }
                        continue;
                    brk:
                        break;
                    }

                    if (cmdInd == -1)
                        goto notCommand;
                }
                if (i == len - 1) goto runCommand;
            }
            if (argCt != -1)
            {
                ref ArgumentInfo info = ref ArgBuffer[argCt];
                if (info.End == -1)
                {
                    bool endIsC = false;
                    char end = message[len - 1];
                    for (int j = 0; j < ContinueArgChars.Length; ++j)
                    {
                        if (end == ContinueArgChars[j])
                            endIsC = true;
                    }
                    if (endIsC)
                    {
                        info.End = len - 2;
                    }
                    else
                    {
                        info.End = len;
                        do --info.End;
                        while (message[info.End] == ' ' && info.End > -1);
                        if (info.End > 0)
                        {
                            endIsC = false;
                            end = message[info.End];
                            for (int j = 0; j < ContinueArgChars.Length; ++j)
                            {
                                if (end == ContinueArgChars[j])
                                    endIsC = true;
                            }
                            if (endIsC) --info.End;
                        }
                    }
                }
            }
            runCommand:
            if (cmdInd == -1) goto notCommand;
            int ct2 = 0;
            for (int i = 0; i <= argCt; ++i)
            {
                ref ArgumentInfo ai = ref ArgBuffer[i];
                if (ai.End > 0) ct2++;
            }

            int i3 = -1;
            string[] args = argCt == -1 ? Array.Empty<string>() : new string[ct2];
            for (int i = 0; i <= argCt; ++i)
            {
                ref ArgumentInfo ai = ref ArgBuffer[i];
                if (ai.End < 1) continue;
                args[++i3] = new string(ptr, ai.Start, ai.End - ai.Start + 1);
            }

            string originalMessage = message;
            if (!requirePrefix && Prefixes.Length > 0)
            {
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
