using DevkitServer.API.Commands;
#if SERVER
using DevkitServer.Players;
#endif

namespace DevkitServer.Commands.Subsystem;
public class CommandParser
{
    private static readonly char[] Prefixes = { '/', '@', '\\' };
    private static readonly char[] ContinueArgChars = { '\'', '"', '`', '“', '”', '‘', '’' };
    private const int MaxArgCount = 16;
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
        string message, ref bool shouldList, bool requirePrefix)
    {
        ThreadUtil.assertIsGameThread();
        
        if (message == null || message.Length < 2) goto notCommand;
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
                    c:
                    continue;
                }

                if (cmdEnd == -1)
                {
                    if (i != len - 1)
                    {
                        if (c != ' ') continue;
                        else
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
                        }
                    }
                    else
                        cmdEnd = i;
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
                for (int k = 0; k < _handler.Commands.Count; ++k)
                {
                    string c2 = _handler.Commands[k].CommandName;
                    fixed (char* ptr2 = c2)
                    {
                        if (cmdEnd - cmdStart + 1 != c2.Length)
                            continue;
                        for (int i2 = cmdStart; i2 <= cmdEnd; ++i2)
                        {
                            char c1 = *(ptr + i2);
                            char c3 = *(ptr2 + i2 - cmdStart);
                            if (!(c1 == c3 ||
                                (c1 < 91 && c1 > 64 && c1 + 32 == c3) ||
                                (c3 < 91 && c3 > 64 && c3 + 32 == c1))) goto nxt;
                        }
                        cmdInd = k;
                        break;
                    nxt:;
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
                                fixed (char* ptr2 = c2)
                                {
                                    if (cmdEnd - cmdStart + 1 != c2.Length)
                                        continue;
                                    for (int i2 = cmdStart; i2 <= cmdEnd; ++i2)
                                    {
                                        char c1 = *(ptr + i2);
                                        char c3 = *(ptr2 + i2 - cmdStart);
                                        if (!(c1 == c3 ||
                                              (c1 < 91 && c1 > 64 && c1 + 32 == c3) ||
                                              (c3 < 91 && c3 > 64 && c3 + 32 == c1))) goto nxt;
                                    }
                                    cmdInd = k;
                                    goto brk;
                                nxt:;
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
            _handler.ExecuteCommand(_handler.Commands[cmdInd],
#if SERVER
                user, 
#endif
                args, message);
        }

        shouldList = false;
        return true;
        notCommand:
        return false;
    }
}
