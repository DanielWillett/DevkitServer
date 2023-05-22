using DevkitServer.API;
using HarmonyLib;
using StackCleaner;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace DevkitServer.Util;
public static class FormattingUtil
{
    public static Func<object, string> FormatSelector = x => x.Format();
    public const string ANSIReset = "\u001b[39m";
    public const char ConsoleEscapeCharacter = '\u001b';
    public static unsafe string GetANSIForegroundString(ConsoleColor color)
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
    public static unsafe string GetExtANSIForegroundString(int argb)
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
    }
    /// <summary>
    /// Convert to <see cref="Color"/> to ARGB data.
    /// </summary>
    public static int ToArgb(Color color)
    {
        return (byte)Math.Min(255, Mathf.RoundToInt(color.a * 255)) << 24 |
               (byte)Math.Min(255, Mathf.RoundToInt(color.r * 255)) << 16 |
               (byte)Math.Min(255, Mathf.RoundToInt(color.g * 255)) << 8 |
               (byte)Math.Min(255, Mathf.RoundToInt(color.b * 255));
    }
    /// <summary>
    /// Convert to <see cref="Color32"/> to ARGB data.
    /// </summary>
    public static int ToArgb(Color32 color)
    {
        return color.a << 24 |
               color.r << 16 |
               color.g << 8 |
               color.b;
    }
    public static ConsoleColor ToConsoleColor(int argb)
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
    // this is not a mess, scroll away
    public static string Format(this FieldInfo? field) => field == null ? ((object)null!).Format() : ((field.DeclaringType != null
                                                              ? Logger.StackCleaner.GetString(field.DeclaringType)
                                                              : ((Logger.StackCleaner.Configuration.ColorFormatting ==
                                                                  StackColorFormatType.None
                                                                     ? string.Empty
                                                                     : (Logger.StackCleaner.Configuration.ColorFormatting ==
                                                                        StackColorFormatType.ExtendedANSIColor
                                                                         ? GetExtANSIForegroundString(
                                                                             Logger.StackCleaner.Configuration.Colors!
                                                                                 .KeywordColor)
                                                                         : GetANSIForegroundString(
                                                                             ToConsoleColor(
                                                                                 Logger.StackCleaner.Configuration.Colors!
                                                                                     .KeywordColor)))) + "global" +
                                                                 (Logger.StackCleaner.Configuration.ColorFormatting ==
                                                                  StackColorFormatType.None
                                                                     ? string.Empty
                                                                     : (Logger.StackCleaner.Configuration.ColorFormatting ==
                                                                        StackColorFormatType.ExtendedANSIColor
                                                                         ? GetExtANSIForegroundString(
                                                                             Logger.StackCleaner.Configuration.Colors!
                                                                                 .PunctuationColor)
                                                                         : GetANSIForegroundString(
                                                                             ToConsoleColor(
                                                                                 Logger.StackCleaner.Configuration.Colors!
                                                                                     .PunctuationColor)))) + "::" +
                                                                 (Logger.StackCleaner.Configuration.ColorFormatting ==
                                                                  StackColorFormatType.None
                                                                     ? string.Empty
                                                                     : ANSIReset))) + " " +
                                                          (Logger.StackCleaner.Configuration.ColorFormatting ==
                                                           StackColorFormatType.None
                                                              ? string.Empty
                                                              : (Logger.StackCleaner.Configuration.ColorFormatting ==
                                                                 StackColorFormatType.ExtendedANSIColor
                                                                  ? GetExtANSIForegroundString(
                                                                      Logger.StackCleaner.Configuration.Colors!.PropertyColor)
                                                                  : GetANSIForegroundString(
                                                                      ToConsoleColor(Logger.StackCleaner.Configuration.Colors!
                                                                          .PropertyColor)))) + field.Name +
                                                          (Logger.StackCleaner.Configuration.ColorFormatting ==
                                                           StackColorFormatType.None
                                                              ? string.Empty
                                                              : ANSIReset));
    public static string Format(this PropertyInfo? property) => property == null ? ((object)null!).Format() : ((property.DeclaringType != null
                                                                     ? Logger.StackCleaner.GetString(property.DeclaringType)
                                                                     : ((Logger.StackCleaner.Configuration.ColorFormatting ==
                                                                         StackColorFormatType.None
                                                                            ? string.Empty
                                                                            : (Logger.StackCleaner.Configuration
                                                                                   .ColorFormatting ==
                                                                               StackColorFormatType.ExtendedANSIColor
                                                                                ? GetExtANSIForegroundString(
                                                                                    Logger.StackCleaner.Configuration.Colors!
                                                                                        .KeywordColor)
                                                                                : GetANSIForegroundString(
                                                                                    ToConsoleColor(
                                                                                        Logger.StackCleaner.Configuration
                                                                                            .Colors!.KeywordColor)))) +
                                                                        "global" + (Logger.StackCleaner.Configuration
                                                                            .ColorFormatting == StackColorFormatType.None
                                                                            ? string.Empty
                                                                            : (Logger.StackCleaner.Configuration
                                                                                   .ColorFormatting ==
                                                                               StackColorFormatType.ExtendedANSIColor
                                                                                ? GetExtANSIForegroundString(
                                                                                    Logger.StackCleaner.Configuration.Colors!
                                                                                        .PunctuationColor)
                                                                                : GetANSIForegroundString(
                                                                                    ToConsoleColor(
                                                                                        Logger.StackCleaner.Configuration
                                                                                            .Colors!
                                                                                            .PunctuationColor)))) +
                                                                        "::" +
                                                                        (Logger.StackCleaner.Configuration.ColorFormatting ==
                                                                         StackColorFormatType.None
                                                                            ? string.Empty
                                                                            : ANSIReset))) + " " +
                                                                 (Logger.StackCleaner.Configuration.ColorFormatting ==
                                                                  StackColorFormatType.None
                                                                     ? string.Empty
                                                                     : (Logger.StackCleaner.Configuration.ColorFormatting ==
                                                                        StackColorFormatType.ExtendedANSIColor
                                                                         ? GetExtANSIForegroundString(
                                                                             Logger.StackCleaner.Configuration.Colors!
                                                                                 .PropertyColor)
                                                                         : GetANSIForegroundString(
                                                                             ToConsoleColor(
                                                                                 Logger.StackCleaner.Configuration.Colors!
                                                                                     .PropertyColor)))) + property.Name +
                                                                 (Logger.StackCleaner.Configuration.ColorFormatting ==
                                                                  StackColorFormatType.None
                                                                     ? string.Empty
                                                                     : ANSIReset));
    public static string Format(this MethodBase? method) => method == null ? ((object)null!).Format() : Logger.StackCleaner.GetString(method);
    public static string FormatMethod(Type rtnType, Type? declType, string name, (Type type, string? name)[]? namedArguments = null, Type[]? arguments = null, Type[]? genericArgs = null, bool isStatic = false, bool isAsync = false, bool isGetter = false, bool isSetter = false, bool isIndexer = false)
    {
        StringBuilder sb = new StringBuilder(32);
        if (!isIndexer && isStatic)
            sb.Append("static ".Colorize(Logger.StackCleaner.Configuration.Colors!.KeywordColor));
        if (isAsync && !(isGetter || isSetter || isIndexer))
            sb.Append("async ".Colorize(Logger.StackCleaner.Configuration.Colors!.KeywordColor));

        sb.Append(Format(rtnType)).Append(' ');

        if (isGetter)
            sb.Append("get ".Colorize(Logger.StackCleaner.Configuration.Colors!.KeywordColor));
        if (isSetter)
            sb.Append("set ".Colorize(Logger.StackCleaner.Configuration.Colors!.KeywordColor));

        if (declType != null)
            sb.Append(Format(declType)).Append('.');

        if (!isIndexer)
            sb.Append(name.Colorize(Logger.StackCleaner.Configuration.Colors!.MethodColor));
        else
            sb.Append("this".Colorize(Logger.StackCleaner.Configuration.Colors!.KeywordColor));

        if (!(isGetter || isSetter || isIndexer) && genericArgs is { Length: > 0 })
        {
            sb.Append("<".Colorize(Logger.StackCleaner.Configuration.Colors!.PunctuationColor));
            for (int i = 0; i < genericArgs.Length; ++i)
            {
                if (i != 0)
                    sb.Append(", ".Colorize(Logger.StackCleaner.Configuration.Colors!.PunctuationColor));
                sb.Append(Format(genericArgs[i]));
            }
            sb.Append(">".Colorize(Logger.StackCleaner.Configuration.Colors!.PunctuationColor));
        }
        if (isIndexer || !isGetter && !isSetter)
        {
            sb.Append((isIndexer ? "[" : "(").Colorize(Logger.StackCleaner.Configuration.Colors!.PunctuationColor));
            if (namedArguments is { Length: > 0 })
            {
                for (int i = 0; i < namedArguments.Length; ++i)
                {
                    (Type type, string? paramName) = namedArguments[i];
                    if (i != 0)
                        sb.Append(", ".Colorize(Logger.StackCleaner.Configuration.Colors!.PunctuationColor));
                    sb.Append(Format(type));
                    if (!string.IsNullOrEmpty(paramName))
                        sb.Append(" " + paramName!.Colorize(Logger.StackCleaner.Configuration.Colors!.ParameterColor));
                }
            }
            else if (arguments is { Length: > 0 })
            {
                for (int i = 0; i < arguments.Length; ++i)
                {
                    Type type = arguments[i];
                    if (i != 0)
                        sb.Append(", ".Colorize(Logger.StackCleaner.Configuration.Colors!.PunctuationColor));
                    sb.Append(Format(type));
                }
            }
            sb.Append((isIndexer ? "]" : ")").Colorize(Logger.StackCleaner.Configuration.Colors!.PunctuationColor));
        }
        return sb.ToString();
    }
    public static string Format(this Type? typedef) => typedef == null ? ((object)null!).Format() : Logger.StackCleaner.GetString(typedef);
    public static string Format(this ExceptionBlock? block)
    {
        if (block == null)
            return ((object)null!).Format();
        switch (block.blockType)
        {
            case ExceptionBlockType.BeginExceptionBlock:
                return GetColor(Logger.StackCleaner.Configuration.Colors!.FlowKeywordColor) + "try" + GetReset() + Environment.NewLine + "{";
            case ExceptionBlockType.EndExceptionBlock:
                return "}";
            case ExceptionBlockType.BeginExceptFilterBlock:
                return GetColor(Logger.StackCleaner.Configuration.Colors!.FlowKeywordColor) + " when" + GetReset() + Environment.NewLine + "{";
            case ExceptionBlockType.BeginCatchBlock:
                string str = "}" + GetColor(Logger.StackCleaner.Configuration.Colors!.FlowKeywordColor) + "catch" + GetReset() + Environment.NewLine;
                if (block.catchType != null)
                    str += " (" + block.catchType.Format() + ")";
                return str;
            case ExceptionBlockType.BeginFinallyBlock:
                return "}" + GetColor(Logger.StackCleaner.Configuration.Colors!.FlowKeywordColor) + "finally" + GetReset() + Environment.NewLine + "{";
            case ExceptionBlockType.BeginFaultBlock:
                return "}" + GetColor(Logger.StackCleaner.Configuration.Colors!.FlowKeywordColor) + "fault" + GetReset() + Environment.NewLine + "{";
        }

        return "}" + GetColor(Logger.StackCleaner.Configuration.Colors!.FlowKeywordColor) + block.blockType + GetReset() + Environment.NewLine + "{";
    }
    public static string Format(this Label label) => (Logger.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.None
        ? string.Empty
        : (Logger.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.ExtendedANSIColor
            ? GetExtANSIForegroundString(Logger.StackCleaner.Configuration.Colors!.StructColor)
            : GetANSIForegroundString(ToConsoleColor(Logger.StackCleaner.Configuration.Colors!.StructColor)))) + "Label #" + label.GetLabelId() +
                                                     (Logger.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.None ? string.Empty : ANSIReset);
    public static string Format(this CodeInstruction? instruction)
    {
        if (instruction == null)
            return ((object)null!).Format();
        string op = instruction.opcode.Format();
        switch (instruction.opcode.OperandType)
        {
            case OperandType.ShortInlineBrTarget:
            case OperandType.InlineBrTarget:
                if (instruction.operand is Label lbl)
                    op += " " + lbl.Format();
                break;
            case OperandType.InlineField:
                if (instruction.operand is FieldInfo field)
                    op += " " + field.Format();
                break;
            case OperandType.ShortInlineI:
            case OperandType.InlineI:
                try
                {
                    int num = Convert.ToInt32(instruction.operand);
                    op += " " + GetColor(Logger.StackCleaner.Configuration.Colors!.ExtraDataColor) + num + GetReset();
                }
                catch
                {
                    // ignored
                }
                break;
            case OperandType.InlineI8:
                try
                {
                    long lng = Convert.ToInt64(instruction.operand);
                    op += " " + GetColor(Logger.StackCleaner.Configuration.Colors!.ExtraDataColor) + lng + GetReset();
                }
                catch
                {
                    // ignored
                }
                break;
            case OperandType.InlineMethod:
                if (instruction.operand is MethodBase method)
                    op += " " + method.Format();
                break;
            case OperandType.ShortInlineR:
            case OperandType.InlineR:
                try
                {
                    double dbl = Convert.ToDouble(instruction.operand);
                    op += " " + GetColor(Logger.StackCleaner.Configuration.Colors!.ExtraDataColor) + dbl + GetReset();
                }
                catch
                {
                    // ignored
                }
                break;
            case OperandType.InlineSig:
                try
                {
                    int num = Convert.ToInt32(instruction.operand);
                    op += " " + GetColor(Logger.StackCleaner.Configuration.Colors!.ExtraDataColor) + num + GetReset();
                }
                catch
                {
                    // ignored
                }
                break;
            case OperandType.InlineString:
                if (instruction.operand is string str)
                    op += " " + GetColor(ToArgb(new Color32(214, 157, 133, 255))) + "\"" + str + "\"" + GetReset();
                break;
            case OperandType.InlineSwitch:
                if (instruction.operand is Label[] jumps)
                {
                    op += Environment.NewLine + "{";
                    for (int i = 0; i < jumps.Length; ++i)
                        op += Environment.NewLine + "  " + GetColor(Logger.StackCleaner.Configuration.Colors!.ExtraDataColor) + i + GetReset() + " => " + GetColor(Logger.StackCleaner.Configuration.Colors!.StructColor) + " Label #" + jumps[i].GetLabelId() + GetReset();

                    op += Environment.NewLine + "}";
                }
                break;
            case OperandType.InlineTok:
                switch (instruction.operand)
                {
                    case Type typeToken:
                        op += " " + typeToken.Format();
                        break;
                    case MethodBase methodToken:
                        op += " " + methodToken.Format();
                        break;
                    case FieldInfo fieldToken:
                        op += " " + fieldToken.Format();
                        break;
                }
                break;
            case OperandType.InlineType:
                if (instruction.operand is Type type)
                    op += " " + type.Format();
                break;
            case OperandType.ShortInlineVar:
            case OperandType.InlineVar:
                if (instruction.operand is LocalBuilder lb)
                    op += " " + GetColor(Logger.StackCleaner.Configuration.Colors!.ExtraDataColor) + lb.LocalIndex + GetReset() + " : " + lb.LocalType!.Format();
                break;
        }

        foreach (Label lbl in instruction.labels)
        {
            op += " .lbl #".Colorize(ConsoleColor.DarkRed) + lbl.GetLabelId().Format();
        }


        return op;
    }
    public static string Format(this OpCode instruction)
    {
        string? clr = null;
        if (Logger.StackCleaner.Configuration.ColorFormatting != StackColorFormatType.None)
        {
            int argb = ToArgb(instruction.FlowControl switch
            {
                FlowControl.Call => new Color32(220, 220, 170, 255),
                FlowControl.Branch => new Color32(216, 160, 223, 255),
                FlowControl.Cond_Branch => new Color32(224, 179, 230, 255),
                FlowControl.Break or FlowControl.Return or FlowControl.Throw => new Color32(208, 140, 217, 255),
                _ => new Color32(86, 156, 214, 255)
            });
            if (Logger.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.ExtendedANSIColor)
                clr = GetExtANSIForegroundString(argb);
            else
                clr = GetANSIForegroundString(ToConsoleColor(argb));
        }

        if (clr != null)
            return clr + instruction.Name + ANSIReset;

        return instruction.Name;
    }
    public static string Format(this string? str, bool quotes)
    {
        if (str == null) return ((object?)null).Format();
        if (Logger.StackCleaner.Configuration.ColorFormatting != StackColorFormatType.None)
        {
            string clr = (Logger.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.ExtendedANSIColor
                ? GetExtANSIForegroundString(ToArgb(new Color32(214, 157, 133, 255)))
                : GetANSIForegroundString(ToConsoleColor(ToArgb(new Color32(214, 157, 133, 255)))));

            if (quotes)
                return clr + "\"" + str + "\"" + ANSIReset;
            
            return clr + str + ANSIReset;
        }

        return str;
    }
    public static string Format(this object? obj)
    {
        if (obj == null)
        {
            if (Logger.StackCleaner.Configuration.ColorFormatting != StackColorFormatType.None)
                return GetColor(Logger.StackCleaner.Configuration.Colors!.KeywordColor) + "null" + ANSIReset;
            return "null";
        }

        if (obj is IDevkitServerPlugin plugin)
        {
            if (Logger.StackCleaner.Configuration.ColorFormatting != StackColorFormatType.None)
                return GetColor(ToArgb(plugin is IDevkitServerColorPlugin p ? p.Color : Plugin.DefaultColor)) + plugin.Name + ANSIReset;
            return "null";
        }
        if (obj is Guid guid)
        {
            return ("{" + guid.ToString("N") + "}").Colorize(Logger.StackCleaner.Configuration.Colors!.StructColor);
        }
        if (obj is MemberInfo)
        {
            if (obj is FieldInfo field)
                return field.Format();
            if (obj is PropertyInfo property)
                return property.Format();
            if (obj is MethodBase method)
                return method.Format();
            if (obj is Type type2)
                return type2.Format();
        }
        Type type = obj.GetType();
        string str = obj.ToString();
        if (obj is ulong s64 && s64.UserSteam64())
        {
            if (Logger.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.None)
                return s64.ToString("D17");

            return GetColor(ToArgb(new Color32(102, 192, 244, 255))) + s64.ToString("D17") + ANSIReset;
        }
        if (obj is CSteamID cs64 && cs64.UserSteam64())
        {
            if (Logger.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.None)
                return cs64.m_SteamID.ToString("D17");

            return GetColor(ToArgb(new Color32(102, 192, 244, 255))) + cs64.m_SteamID.ToString("D17") + ANSIReset;
        }
        if (obj is ITransportConnection connection)
        {
            if (Logger.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.None)
                return connection.GetAddressString(true);

            if (connection.TryGetIPv4Address(out uint addr))
            {
                str = GetColor(ToArgb(new Color32(204, 255, 102, 255)))
                      + Parser.getIPFromUInt32(addr);
                if (connection.TryGetPort(out ushort port))
                    str += GetColor(Logger.StackCleaner.Configuration.Colors!.PunctuationColor) + ":" +
                           GetColor(ToArgb(new Color32(170, 255, 0, 255))) +
                           port.ToString(CultureInfo.InvariantCulture);

                return str + ANSIReset;
            }

            return GetColor(ToArgb(new Color32(204, 255, 102, 255))) + (connection.GetAddressString(true) ?? "<unknown address>") + ANSIReset;
        }

        if (Logger.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.None)
            return str;
        if (str.Equals(type.ToString(), StringComparison.Ordinal))
            return "{" + type.Format() + "}";

        if (Logger.StackCleaner.Configuration.ColorFormatting != StackColorFormatType.None)
        {
            if (obj is string or char)
                return GetColor(ToArgb(new Color32(214, 157, 133, 255))) + "\"" + str + "\"" + ANSIReset;

            if (type.IsEnum)
                return GetColor(Logger.StackCleaner.Configuration.Colors!.EnumColor) + str + ANSIReset;

            if (type.IsPrimitive)
            {
                if (obj is bool)
                    return GetColor(Logger.StackCleaner.Configuration.Colors!.KeywordColor) + str + ANSIReset;

                return GetColor(ToArgb(new Color32(181, 206, 168, 255))) + str + ANSIReset;
            }

            if (type.IsInterface)
                return GetColor(Logger.StackCleaner.Configuration.Colors!.InterfaceColor) + str + ANSIReset;

            if (type.IsValueType)
                return GetColor(Logger.StackCleaner.Configuration.Colors!.StructColor) + str + ANSIReset;

            return GetColor(Logger.StackCleaner.Configuration.Colors!.ClassColor) + str + ANSIReset;
        }

        return str;
    }
    public static string Colorize(this string str, ConsoleColor color)
    {
        if (Logger.StackCleaner.Configuration.ColorFormatting != StackColorFormatType.None)
        {
            return GetANSIForegroundString(color) + str + ANSIReset;
        }

        return str;
    }
    public static string Colorize(this string str, Color color)
    {
        if (Logger.StackCleaner.Configuration.ColorFormatting != StackColorFormatType.None)
        {
            return (Logger.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.ExtendedANSIColor
                       ? GetExtANSIForegroundString(ToArgb(color))
                       : GetANSIForegroundString(ToConsoleColor(ToArgb(color))))
                + str + ANSIReset;
        }

        return str;
    }
    public static string Colorize(this string str, Color32 color)
    {
        if (Logger.StackCleaner.Configuration.ColorFormatting != StackColorFormatType.None)
        {
            return (Logger.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.ExtendedANSIColor
                       ? GetExtANSIForegroundString(ToArgb(color))
                       : GetANSIForegroundString(ToConsoleColor(ToArgb(color))))
                + str + ANSIReset;
        }

        return str;
    }
    public static string Colorize(this string str, int argb)
    {
        if (Logger.StackCleaner.Configuration.ColorFormatting != StackColorFormatType.None)
        {
            return (Logger.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.ExtendedANSIColor
                       ? GetExtANSIForegroundString(argb)
                       : GetANSIForegroundString(ToConsoleColor(argb)))
                + str + ANSIReset;
        }

        return str;
    }
    private static string GetColor(int argb)
    {
        return (Logger.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.None
            ? string.Empty
            : (Logger.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.ExtendedANSIColor
                ? GetExtANSIForegroundString(argb)
                : GetANSIForegroundString(ToConsoleColor(argb))));
    }
    private static string GetReset() => Logger.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.None ? string.Empty : ANSIReset;
}
