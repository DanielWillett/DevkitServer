using DevkitServer.Multiplayer.Networking;
using DevkitServer.Util.Terminals;
using StackCleaner;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using DevkitServer.Configuration;
using DevkitServer.Patches;
using HarmonyLib;

namespace DevkitServer.Util;
internal static class Logger
{
    public static readonly StackTraceCleaner StackCleaner;
    private static readonly NetCall<string, Severity> SendLogMessage = new NetCall<string, Severity>((ushort)NetCalls.SendLog);
    public static ITerminal Terminal;
    private const string TimeFormat = "yyyy-MM-dd hh:mm:ss";
    internal const string ANSIReset = "\u001b[39m";
    internal const char ConsoleEscapeCharacter = '\u001b';
    static Logger()
    {
        StackCleanerConfiguration config = new StackCleanerConfiguration
        {
            ColorFormatting = StackColorFormatType.ExtendedANSIColor,
            Colors = UnityColor32Config.Default,
            IncludeNamespaces = false,
#if DEBUG
            IncludeFileData = true,
            IncludeSourceData = true,
            IncludeILOffset = true
#else
            IncludeFileData = false,
            IncludeSourceData = false,
            IncludeILOffset = false
#endif
        };
#if !SERVER
        if (Application.platform is RuntimePlatform.WindowsPlayer or RuntimePlatform.WindowsEditor)
        {
            Terminal = DevkitServerModule.GameObjectHost.AddComponent<WindowsClientTerminal>();
            LogInfo("Initalized Windows terminal.", ConsoleColor.DarkCyan);
            config.ColorFormatting = StackColorFormatType.ExtendedANSIColor;
        }
        else
        {
            Terminal = DevkitServerModule.GameObjectHost.AddComponent<BackgroundLoggingTerminal>();
            LogInfo("Did not initialize a terminal.", ConsoleColor.DarkCyan);
            config.ColorFormatting = StackColorFormatType.None;
        }
#else
        if (Application.platform is not RuntimePlatform.WindowsPlayer and not RuntimePlatform.WindowsEditor)
            config.ColorFormatting = StackColorFormatType.ANSIColor;
        Terminal = DevkitServerModule.GameObjectHost.AddComponent<ServerTerminal>();
#endif
        if (!DevkitServerConfig.Config.ConsoleVisualANSISupport)
        {
            config.ColorFormatting = StackColorFormatType.None;
            config.Colors = Color4Config.Default;
        }
        else if (!DevkitServerConfig.Config.ConsoleExtendedVisualANSISupport)
        {
            config.ColorFormatting = StackColorFormatType.ANSIColor;
            config.Colors = Color4Config.Default;
        }

        StackCleaner = new StackTraceCleaner(config);
        
        UnturnedLog.info("Loading logger with log type: " + Terminal.GetType().Format() + " colorized with: " + config.ColorFormatting.Format() + ".");
    }
    internal static void InitLogger()
    {
        Terminal.Init();
        Terminal.OnInput += OnTerminalInput;
    }
#if CLIENT
    internal static void PostPatcherSetupInitLogger()
    {
        try
        {
            MethodInfo? method = typeof(LogFile).GetMethod(nameof(LogFile.writeLine), BindingFlags.Instance | BindingFlags.Public);
            if (method != null)
                PatchesMain.Patcher.Patch(method, prefix: new HarmonyMethod(typeof(Logger).GetMethod(nameof(OnLinePrinted), BindingFlags.Static | BindingFlags.NonPublic)));

            string log = Logs.getLogFilePath();
            if (File.Exists(log))
            {
                using FileStream str = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using StreamReader reader = new StreamReader(str, System.Text.Encoding.GetEncoding(65001, new EncoderReplacementFallback(), new DecoderReplacementFallback()));
                while (reader.ReadLine() is { } line)
                {
                    OnLinePrinted(ref line);
                }
            }
        }
        catch (Exception ex)
        {
            LogWarning("Failed to fetch vanilla logs.");
            LogError(ex);
        }
    }
    private static void OnLinePrinted(ref string line)
    {
        if (Terminal is not { IsCommitingToUnturnedLog: false })
            return;
        line = RemoveANSIFormatting(line);
        string msg2 = line;
        TryRemoveDateFromLine(ref msg2);
        Terminal.Write("[" + DateTime.UtcNow.ToString(TimeFormat) + "] [UNTURNED]      [LOG]   " + msg2, ConsoleColor.DarkGray, false, Severity.Info);
    }
#endif
    internal static void TryRemoveDateFromLine(ref string message)
    {
        int ind = message.Length > 21 ? message.IndexOf(']', 0, 21) : -1;
        if (ind != -1)
            message = message.Substring(ind + (message.Length > ind + 2 && message[ind + 1] == ' ' ? 2 : (message.Length > ind + 1 ? 1 : 0)));
    }
    internal static void CloseLogger()
    {
        Terminal.Close();
        Terminal.OnInput -= OnTerminalInput;
        if (Terminal is Object obj)
            Object.Destroy(obj);
    }
#if SERVER
    public static void SendLog(ITransportConnection connection, string message, Severity severity)
    {
        SendLogMessage.Invoke(connection, message, severity);
    }
    public static void SendLog(List<ITransportConnection> connections, string message, Severity severity)
    {
        SendLogMessage.Invoke(connections, message, severity);
    }
#else
    public static void SendLog(string message, Severity severity)
    {
        SendLogMessage.Invoke(message, severity);
    }
#endif
    [NetCall(NetCallSource.FromEither, (ushort)NetCalls.SendLog)]
    private static void ReceiveLogMessage(MessageContext ctx, string message, Severity severity)
    {
        Log(severity, message);
        ctx.Acknowledge(StandardErrorCode.Success);
    }
    public static void Log(Severity severity, string message, ConsoleColor? color = null)
    {
        switch (severity)
        {
            case Severity.Error:
            case Severity.Fatal:
                LogError(message, color ?? ConsoleColor.Red);
                break;
            case Severity.Warning:
                LogError(message, color ?? ConsoleColor.Yellow);
                break;
            case Severity.Debug:
                LogError(message, color ?? ConsoleColor.DarkGray);
                break;
            default: // Info
                LogError(message, color ?? ConsoleColor.DarkCyan);
                break;
        }
    }
    public static void ChangeResets(ref string message, ConsoleColor color)
    {
        if (message.IndexOf('\u001b') != -1)
        {
            message = message.Replace(ANSIReset, GetANSIForegroundString(color)) + ANSIReset;
        }
    }
    [Conditional("DEBUG")]
    public static void LogDebug(string message, ConsoleColor color = ConsoleColor.DarkGray)
    {
        ChangeResets(ref message, color);
        Terminal.Write("[" + DateTime.UtcNow.ToString(TimeFormat) + "] [DEVKIT SERVER] [DEBUG] " + message, color, true, Severity.Debug);
    }
    public static void LogInfo(string message, ConsoleColor color = ConsoleColor.DarkCyan)
    {
        ChangeResets(ref message, color);
        Terminal.Write("[" + DateTime.UtcNow.ToString(TimeFormat) + "] [DEVKIT SERVER] [INFO]  " + message, color, true, Severity.Info);
    }
    public static void LogWarning(string message, ConsoleColor color = ConsoleColor.Yellow, [CallerMemberName] string method = "")
    {
        ChangeResets(ref message, color);
        Terminal.Write("[" + DateTime.UtcNow.ToString(TimeFormat) + "] [DEVKIT SERVER] [WARN]  " + "[" + method.ToUpperInvariant() + "] " + message, color, true, Severity.Warning);
    }
    public static void LogError(string message, ConsoleColor color = ConsoleColor.Red, [CallerMemberName] string method = "")
    {
        ChangeResets(ref message, color);
        Terminal.Write("[" + DateTime.UtcNow.ToString(TimeFormat) + "] [DEVKIT SERVER] [ERROR] " + "[" + method.ToUpperInvariant() + "] " + message, color, true, Severity.Error);
    }
    public static void DumpJson<T>(T obj, ConsoleColor color = ConsoleColor.DarkGray, bool condensed = false)
    {
        if (obj == null)
            Terminal.Write("null", color, true, Severity.Debug);
        else
        {
            try
            {
                Terminal.Write(JsonSerializer.Serialize(obj, obj!.GetType(), condensed ? DevkitServerConfig.CondensedSerializerSettings : DevkitServerConfig.SerializerSettings),
                    color, true, Severity.Debug);
            }
            catch (Exception ex)
            {
                LogError("Error serializing " + obj + ".");
                LogError(ex);
            }
        }
    }
    public static void DumpGameObject(GameObject go, ConsoleColor color = ConsoleColor.White)
    {
        LogInfo("Gameobject Dump: \"" + go.name + "\":", color);
        Terminal.Write("Transform:", color, true, Severity.Debug);
        Terminal.Write($" Parent: {(go.transform.parent == null ? "none" : go.transform.parent.name)}", color, true, Severity.Debug);
        Terminal.Write($" Position: {go.transform.position:F2}", color, true, Severity.Debug);
        Terminal.Write($" Rotation: {go.transform.rotation.eulerAngles:F2}", color, true, Severity.Debug);
        Terminal.Write($" Scale:    {go.transform.localScale:F2}", color, true, Severity.Debug);
        Terminal.Write("Components:", color, true, Severity.Debug);
        Component[] comps = go.GetComponents<Component>();
        Terminal.Write(" ========================================", color, true, Severity.Debug);
        foreach (Component comp in comps)
        {
            Terminal.Write($" Parent: {comp.transform.gameObject.name}", color, true, Severity.Debug);
            Terminal.Write($" Type: {comp.GetType().Format()}{GetANSIForegroundString(color)}", color, true, Severity.Debug);
            Terminal.Write(" ========================================", color, true, Severity.Debug);
        }
        int childCt = go.transform.childCount;
        Terminal.Write($"Children: {childCt}:", color, true, Severity.Debug);
        for (int i = 0; i < childCt; ++i)
        {
            DumpGameObject(go.transform.GetChild(i).gameObject, color);
        }
    }
    public static void LogError(Exception ex, bool cleanStack = true, [CallerMemberName] string method = "") => WriteExceptionIntl(ex, cleanStack, 0, method);
    private static void WriteExceptionIntl(Exception ex, bool cleanStack, int indent, string? method = null)
    {
        string ind = indent == 0 ? string.Empty : new string(' ', indent);
        bool inner = indent > 0;
        while (ex != null)
        {
            if (inner)
            {
                Terminal.Write(string.Empty, ConsoleColor.Red, true, Severity.Error);
            }
            Terminal.Write(ind + (inner ? "Inner Exception: " : ((string.IsNullOrEmpty(method) ? string.Empty : ("[" + method!.ToUpper() + "] ")) + "Exception: ")) + ex.GetType().Format() + GetANSIForegroundString(ConsoleColor.Red), ConsoleColor.Red, true, Severity.Error);
            Terminal.Write(ind + (ex.Message ?? "No message"), ConsoleColor.DarkRed, true, Severity.Error);
            if (ex is TypeLoadException t)
            {
                Terminal.Write(ind + "Type: " + t.TypeName, ConsoleColor.DarkRed, true, Severity.Error);
            }
            else if (ex is ReflectionTypeLoadException t2)
            {
                Terminal.Write(ind + "Type load exceptions:", ConsoleColor.DarkRed, true, Severity.Error);
                foreach (Exception ex2 in t2.LoaderExceptions)
                {
                    WriteExceptionIntl(ex2, cleanStack, indent + 1);
                }
            }
            else if (ex is TargetInvocationException { InnerException: not null } t4)
            {
                Terminal.Write(ind + "Invoked exception:", ConsoleColor.DarkRed, true, Severity.Error);
                WriteExceptionIntl(t4.InnerException, cleanStack, indent + 1);
            }
            else if (ex is AggregateException t3)
            {
                Terminal.Write(ind + "Inner exceptions:", ConsoleColor.DarkRed, true, Severity.Error);
                foreach (Exception ex2 in t3.InnerExceptions)
                {
                    WriteExceptionIntl(ex2, cleanStack, indent + 1);
                }
            }
            if (ex.StackTrace != null)
            {
                if (cleanStack)
                {
                    string str;
                    try
                    {
                        str = StackCleaner.GetString(ex);
                    }
                    catch (Exception ex2)
                    {
                        LogDebug("Ran into an error cleaning a stack trace: " + ex2.Message + " (" + ex2.GetType().Format() + ").");
                        str = ex.StackTrace;
                    }
                    
                    Terminal.Write(str, ConsoleColor.DarkGray, true, Severity.Error);
                }
                /*
                Terminal.Write(indent != 0
                    ? string.Join(Environment.NewLine, ex.StackTrace.Split(SplitChars).Select(x => ind + x.Trim(TrimChars)))
                    : ex.StackTrace, ConsoleColor.DarkGray);*/
            }
            if (ex is AggregateException or TargetInvocationException) break;
            ex = ex.InnerException!;
            inner = true;
        }
    }
    internal static unsafe string GetANSIForegroundString(ConsoleColor color)
    {
        int num = color switch
        {
            ConsoleColor.Black => 30,
            ConsoleColor.DarkRed => 31,
            ConsoleColor.DarkGreen => 32,
            ConsoleColor.DarkYellow => 33,
            ConsoleColor.DarkBlue => 34,
            ConsoleColor.DarkMagenta => 35,
            ConsoleColor.DarkCyan => 36,
            ConsoleColor.Gray => 37,
            ConsoleColor.DarkGray => 90,
            ConsoleColor.Red => 91,
            ConsoleColor.Green => 92,
            ConsoleColor.Yellow => 93,
            ConsoleColor.Blue => 94,
            ConsoleColor.Magenta => 95,
            ConsoleColor.Cyan => 96,
            ConsoleColor.White => 97,
            _ => 39
        };
        char* chrs = stackalloc char[5];
        chrs[0] = '\u001b';
        chrs[1] = '[';
        chrs[2] = (char)(num / 10 + 48);
        chrs[3] = (char)(num % 10 + 48);
        chrs[4] = 'm';

        return new string(chrs, 0, 5);
    }
    /// <summary>
    /// Returns extended ANSI text format codes for 32 bit ARGB data formatted as <code>
    /// ESC[38;2;*r*;*g*;*b*m
    /// </code> where 'ESC' is '\u001b'.
    /// </summary>
    /// <param name="argb">32 bit ARGB data, convert using <see cref="System.Drawing.Color.ToArgb"/> and <see cref="System.Drawing.Color.FromArgb(int)"/>.</param>
    private static unsafe string GetExtANSIForegroundString(int argb)
    {
        // https://learn.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences?redirectedfrom=MSDN#text-formatting
        byte r = unchecked((byte)(argb >> 16));
        byte g = unchecked((byte)(argb >> 8));
        byte b = unchecked((byte)argb);
        int l = 10 + (r > 9 ? r > 99 ? 3 : 2 : 1) + (g > 9 ? g > 99 ? 3 : 2 : 1) + (b > 9 ? b > 99 ? 3 : 2 : 1);
        char* chrs = stackalloc char[l];
        chrs[0] = ConsoleEscapeCharacter;
        chrs[1] = '[';
        chrs[2] = '3';
        chrs[3] = '8';
        chrs[4] = ';';
        chrs[5] = '2';
        chrs[6] = ';';
        int index = 6;
        if (r > 99)
            chrs[++index] = (char)(r / 100 + 48);
        if (r > 9)
            chrs[++index] = (char)((r % 100) / 10 + 48);
        chrs[++index] = (char)(r % 10 + 48);
        chrs[++index] = ';';
        if (g > 99)
            chrs[++index] = (char)(g / 100 + 48);
        if (g > 9)
            chrs[++index] = (char)((g % 100) / 10 + 48);
        chrs[++index] = (char)(g % 10 + 48);
        chrs[++index] = ';';
        if (b > 99)
            chrs[++index] = (char)(b / 100 + 48);
        if (b > 9)
            chrs[++index] = (char)((b % 100) / 10 + 48);
        chrs[++index] = (char)(b % 10 + 48);
        chrs[index + 1] = 'm';
        return new string(chrs, 0, l);
    }/// <summary>
    /// Convert to <see cref="Color"/> to ARGB data.
    /// </summary>
    public static int ToArgb(Color color)
    {
        return 0xFF << 24 |
               (byte)Math.Min(255, Mathf.RoundToInt(color.r * 255)) << 16 |
               (byte)Math.Min(255, Mathf.RoundToInt(color.g * 255)) << 8 |
               (byte)Math.Min(255, Mathf.RoundToInt(color.b * 255));
    }
    internal static ConsoleColor ToConsoleColor(int argb)
    {
        int bits = ((argb >> 16) & byte.MaxValue) > 128 || ((argb >> 8) & byte.MaxValue) > 128 || (argb & byte.MaxValue) > 128 ? 8 : 0;
        if (((argb >> 16) & byte.MaxValue) > 180)
            bits |= 4;
        if (((argb >> 8) & byte.MaxValue) > 180)
            bits |= 2;
        if ((argb & byte.MaxValue) > 180)
            bits |= 1;
        return (ConsoleColor)bits;
    }
    public static unsafe string RemoveANSIFormatting(string orig)
    {
        if (orig.Length < 5)
            return orig;
        bool found = false;
        int l = orig.Length;
        for (int i = 0; i < l; ++i)
        {
            if (orig[i] == ConsoleEscapeCharacter)
            {
                found = true;
            }
        }

        if (!found)
            return orig;

        try
        {
            // regex: \u001B\[[\d;]*m

            int outpInd = 0;
            char* outp = stackalloc char[l - 3];
            fixed (char* chars = orig)
            {
                int lastCpy = -1;
                for (int i = 0; i < l - 2; ++i)
                {
                    if (l > i + 3 && chars[i] == ConsoleEscapeCharacter && chars[i + 1] == '[' && char.IsDigit(chars[i + 2]))
                    {
                        int st = i;
                        int c = i + 3;
                        for (; c < l; ++c)
                        {
                            if (chars[c] != ';' && !char.IsDigit(chars[c]))
                            {
                                if (chars[c] == 'm')
                                    i = c;

                                break;
                            }

                            i = c;
                        }

                        Buffer.MemoryCopy(chars + lastCpy + 1, outp + outpInd, (l - outpInd) * sizeof(char), (st - lastCpy - 1) * sizeof(char));
                        outpInd += st - lastCpy - 1;
                        lastCpy += st - lastCpy + (c - st);
                    }
                }
                Buffer.MemoryCopy(chars + lastCpy + 1, outp + outpInd, (l - outpInd) * sizeof(char), (l - lastCpy) * sizeof(char));
                outpInd += l - lastCpy;
            }

            return new string(outp, 0, outpInd - 1);
        }
        catch
        {
            return orig;
        }
    }
    private static void OnTerminalInput(string input, ref bool shouldhandle)
    {
        if (!shouldhandle) return;
        LogInfo(input);
    }

    // this is not a mess, scroll away
    public static string Format(this FieldInfo field) => (field.DeclaringType != null ? StackCleaner.GetString(field.DeclaringType) : ((StackCleaner.Configuration.ColorFormatting == StackColorFormatType.None
                                                             ? string.Empty
                                                             : (StackCleaner.Configuration.ColorFormatting == StackColorFormatType.ExtendedANSIColor
                                                                 ? GetExtANSIForegroundString(StackCleaner.Configuration.Colors!.KeywordColor)
                                                                 : GetANSIForegroundString(ToConsoleColor(StackCleaner.Configuration.Colors!.KeywordColor)))) + "global" + (StackCleaner.Configuration.ColorFormatting == StackColorFormatType.None
                                                                 ? string.Empty
                                                                 : (StackCleaner.Configuration.ColorFormatting == StackColorFormatType.ExtendedANSIColor
                                                                     ? GetExtANSIForegroundString(StackCleaner.Configuration.Colors!.PunctuationColor)
                                                                     : GetANSIForegroundString(ToConsoleColor(StackCleaner.Configuration.Colors!.PunctuationColor)))) + "::" +
                                                             (StackCleaner.Configuration.ColorFormatting == StackColorFormatType.None ? string.Empty : ANSIReset))) + " " + (StackCleaner.Configuration.ColorFormatting == StackColorFormatType.None
                                                                        ? string.Empty
                                                                        : (StackCleaner.Configuration.ColorFormatting == StackColorFormatType.ExtendedANSIColor
                                                                            ? GetExtANSIForegroundString(StackCleaner.Configuration.Colors!.PropertyColor)
                                                                            : GetANSIForegroundString(ToConsoleColor(StackCleaner.Configuration.Colors!.PropertyColor)))) + field.Name +
                                                                                                                         (StackCleaner.Configuration.ColorFormatting == StackColorFormatType.None ? string.Empty : ANSIReset);
    public static string Format(this MethodBase method) => StackCleaner.GetString(method);
    public static string Format(this Type typedef) => StackCleaner.GetString(typedef);
    public static string Format(this ExceptionBlock block)
    {
        switch (block.blockType)
        {
            case ExceptionBlockType.BeginExceptionBlock:
                return GetColor(StackCleaner.Configuration.Colors!.FlowKeywordColor) + "try" + GetReset() + Environment.NewLine + "{";
            case ExceptionBlockType.EndExceptionBlock:
                return "}";
            case ExceptionBlockType.BeginExceptFilterBlock:
                return GetColor(StackCleaner.Configuration.Colors!.FlowKeywordColor) + " when" + GetReset() + Environment.NewLine + "{";
            case ExceptionBlockType.BeginCatchBlock:
                string str = "}" + GetColor(StackCleaner.Configuration.Colors!.FlowKeywordColor) + "catch" + GetReset() + Environment.NewLine ;
                if (block.catchType != null)
                    str += " (" + block.catchType.Format() + ")";
                return str;
            case ExceptionBlockType.BeginFinallyBlock:
                return "}" + GetColor(StackCleaner.Configuration.Colors!.FlowKeywordColor) + "finally" + GetReset() + Environment.NewLine +  "{";
            case ExceptionBlockType.BeginFaultBlock:
                return "}" + GetColor(StackCleaner.Configuration.Colors!.FlowKeywordColor) + "fault" + GetReset() + Environment.NewLine + "{";
        }

        return "}" + GetColor(StackCleaner.Configuration.Colors!.FlowKeywordColor) + block.blockType + GetReset() + Environment.NewLine + "{";

        string GetColor(int argb)
        {
            return (StackCleaner.Configuration.ColorFormatting == StackColorFormatType.None
                ? string.Empty
                : (StackCleaner.Configuration.ColorFormatting == StackColorFormatType.ExtendedANSIColor
                    ? GetExtANSIForegroundString(argb)
                    : GetANSIForegroundString(ToConsoleColor(argb))));
        }
        string GetReset() => StackCleaner.Configuration.ColorFormatting == StackColorFormatType.None ? string.Empty : ANSIReset;
    }
    public static string Format(this Label label) => (StackCleaner.Configuration.ColorFormatting == StackColorFormatType.None
        ? string.Empty
        : (StackCleaner.Configuration.ColorFormatting == StackColorFormatType.ExtendedANSIColor
            ? GetExtANSIForegroundString(StackCleaner.Configuration.Colors!.StructColor)
            : GetANSIForegroundString(ToConsoleColor(StackCleaner.Configuration.Colors!.StructColor)))) + "Label #" + label.GetLabelId() +
                                                     (StackCleaner.Configuration.ColorFormatting == StackColorFormatType.None ? string.Empty : ANSIReset);
    public static string Format(this CodeInstruction instruction)
    {
        string op = instruction.opcode.Format();
        switch (instruction.opcode.OperandType)
        {
            case OperandType.ShortInlineBrTarget:
            case OperandType.InlineBrTarget:
                if (instruction.operand is Label lbl)
                    return op + " " + lbl.Format();
                break;
            case OperandType.InlineField:
                if (instruction.operand is FieldInfo field)
                    return op + " " + field.Format();
                break;
            case OperandType.ShortInlineI:
            case OperandType.InlineI:
                try
                {
                    int num = Convert.ToInt32(instruction.operand);
                    return op + " " + GetColor(StackCleaner.Configuration.Colors!.ExtraDataColor) + num + GetReset();
                }
                catch
                {
                    break;
                }
            case OperandType.InlineI8:
                try
                {
                    long lng = Convert.ToInt64(instruction.operand);
                    return op + " " + GetColor(StackCleaner.Configuration.Colors!.ExtraDataColor) + lng + GetReset();
                }
                catch
                {
                    break;
                }
            case OperandType.InlineMethod:
                if (instruction.operand is MethodBase method)
                    return op + " " + method.Format();
                break;
            case OperandType.ShortInlineR:
            case OperandType.InlineR:
                try
                {
                    double dbl = Convert.ToDouble(instruction.operand);
                    return op + " " + GetColor(StackCleaner.Configuration.Colors!.ExtraDataColor) + dbl + GetReset();
                }
                catch
                {
                    break;
                }
            case OperandType.InlineSig:
                try
                {
                    int num = Convert.ToInt32(instruction.operand);
                    return op + " " + GetColor(StackCleaner.Configuration.Colors!.ExtraDataColor) + num + GetReset();
                }
                catch
                {
                    break;
                }
            case OperandType.InlineString:
                if (instruction.operand is string str)
                    return op + " " + GetColor(ToArgb(new Color32(214, 157, 133, 255))) + "\"" + str + "\"" + GetReset();
                break;
            case OperandType.InlineSwitch:
                if (instruction.operand is Label[] jumps)
                {
                    op += Environment.NewLine + "{";
                    for (int i = 0; i < jumps.Length; ++i)
                        op += Environment.NewLine + "  " + GetColor(StackCleaner.Configuration.Colors!.ExtraDataColor) + i + GetReset() + " => " + GetColor(StackCleaner.Configuration.Colors!.StructColor) + " Label #" + jumps[i].GetLabelId() + GetReset();

                    op += Environment.NewLine + "}";
                    return op;
                }
                break;
            case OperandType.InlineTok:
                switch (instruction.operand)
                {
                    case Type typeToken:
                        return op + " " + typeToken.Format();
                    case MethodBase methodToken:
                        return op + " " + methodToken.Format();
                    case FieldInfo fieldToken:
                        return op + " " + fieldToken.Format();
                }

                break;
            case OperandType.InlineType:
                if (instruction.operand is MethodBase type)
                    return op + " " + type.Format();
                break;
            case OperandType.ShortInlineVar:
            case OperandType.InlineVar:
                if (instruction.operand is LocalBuilder lb)
                    return op + " " + GetColor(StackCleaner.Configuration.Colors!.ExtraDataColor) + lb.LocalIndex + GetReset() + " : " + lb.LocalType!.Format();
                break;

        }
        string GetColor(int argb)
        {
            return (StackCleaner.Configuration.ColorFormatting == StackColorFormatType.None
                ? string.Empty
                : (StackCleaner.Configuration.ColorFormatting == StackColorFormatType.ExtendedANSIColor
                    ? GetExtANSIForegroundString(argb)
                    : GetANSIForegroundString(ToConsoleColor(argb))));
        }

        string GetReset() => StackCleaner.Configuration.ColorFormatting == StackColorFormatType.None ? string.Empty : ANSIReset;

        return op;
    }
    public static string Format(this OpCode instruction)
    {
        string? clr = null;
        if (StackCleaner.Configuration.ColorFormatting != StackColorFormatType.None)
        {
            int argb = ToArgb(instruction.FlowControl switch
            {
                FlowControl.Call => new Color32(220, 220, 170, 255),
                FlowControl.Branch => new Color32(216, 160, 223, 255),
                FlowControl.Cond_Branch => new Color32(224, 179, 230, 255),
                FlowControl.Break or FlowControl.Return or FlowControl.Throw => new Color32(208, 140, 217, 255),
                _ => new Color32(86, 156, 214, 255)
            });
            if (StackCleaner.Configuration.ColorFormatting == StackColorFormatType.ExtendedANSIColor)
                clr = GetExtANSIForegroundString(argb);
            else
                clr = GetANSIForegroundString(ToConsoleColor(argb));
        }

        if (clr != null)
            return clr + instruction.Name + ANSIReset;

        return instruction.Name;
    }
    public static string Format(this object obj)
    {
        Type type = obj.GetType();
        string str = obj.ToString();
        if (str.Equals(type.ToString(), StringComparison.Ordinal))
            return "{" + type.Format() + "}";
        
        if (StackCleaner.Configuration.ColorFormatting != StackColorFormatType.None)
        {
            if (obj is string)
                return GetColor(ToArgb(new Color32(214, 157, 133, 255))) + "\"" + str + "\"" + ANSIReset;

            if (type.IsPrimitive)
                return GetColor(StackCleaner.Configuration.Colors!.KeywordColor) + str + ANSIReset;
            
            if (type.IsEnum)
                return GetColor(StackCleaner.Configuration.Colors!.EnumColor) + str + ANSIReset;
            
            if (type.IsInterface)
                return GetColor(StackCleaner.Configuration.Colors!.InterfaceColor) + str + ANSIReset;
            
            if (type.IsValueType)
                return GetColor(StackCleaner.Configuration.Colors!.StructColor) + str + ANSIReset;

            return GetColor(StackCleaner.Configuration.Colors!.ClassColor) + str + ANSIReset;
        }

        return str;

        string GetColor(int argb)
        {
            return (StackCleaner.Configuration.ColorFormatting == StackColorFormatType.None
                ? string.Empty
                : (StackCleaner.Configuration.ColorFormatting == StackColorFormatType.ExtendedANSIColor
                    ? GetExtANSIForegroundString(argb)
                    : GetANSIForegroundString(ToConsoleColor(argb))));
        }
    }
    public static string Colorize(this string str, ConsoleColor color)
    {
        if (StackCleaner.Configuration.ColorFormatting != StackColorFormatType.None)
        {
            return GetANSIForegroundString(color) + str + ANSIReset;
        }

        return str;
    }
    public static string Colorize(this string str, Color color)
    {
        if (StackCleaner.Configuration.ColorFormatting != StackColorFormatType.None)
        {
            return GetExtANSIForegroundString(ToArgb(color)) + str + ANSIReset;
        }

        return str;
    }
}

public enum Severity : byte
{
    Debug,
    Info,
    Warning,
    Error,
    Fatal
}