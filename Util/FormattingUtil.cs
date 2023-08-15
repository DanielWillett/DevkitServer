using DevkitServer.API;
using HarmonyLib;
using StackCleaner;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using Version = System.Version;

namespace DevkitServer.Util;
public static class FormattingUtil
{
    // For unit tests
    internal static ITerminalFormatProvider FormatProvider = new LoggerFormatProvider();
    private static char[][]? _tags;
    private static RemoveRichTextOptions[]? _tagFlags;
    public static Func<object, string> FormatSelector = x => x.Format();
    public const char ConsoleEscapeCharacter = '\u001b';
    public const string ANSIForegroundReset = "\u001b[39m";
    public const string ANSIBackgroundReset = "\u001b[49m";
    private const int DefaultForeground = -9013642;  // gray
    private const int DefaultBackground = -15987700; // black
    public static unsafe string GetANSIString(ConsoleColor color, bool background)
    {
        char* chrs = stackalloc char[5];
        SetANSICode(chrs, 0, color, background);
        return new string(chrs, 0, 5);
    }
    private static unsafe void SetANSICode(char* data, int index, ConsoleColor color, bool background)
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
        if (background)
            num += 10;
        data[index] = '\u001b';
        data[index + 1] = '[';
        data[index + 2] = (char)(num / 10 + 48);
        data[index + 3] = (char)(num % 10 + 48);
        data[index + 4] = 'm';
    }
    public static unsafe string GetExtendedANSIString(int argb, bool background)
    {
        byte r = unchecked((byte)(argb >> 16));
        byte g = unchecked((byte)(argb >> 8));
        byte b = unchecked((byte)argb);
        int l = 10 + (r > 9 ? r > 99 ? 3 : 2 : 1) + (g > 9 ? g > 99 ? 3 : 2 : 1) + (b > 9 ? b > 99 ? 3 : 2 : 1);
        char* chrs = stackalloc char[l];
        SetExtendedANSICode(chrs, 0, r, g, b, background);
        return new string(chrs, 0, l);
    }
    private static unsafe void SetExtendedANSICode(char* data, int index, byte r, byte g, byte b, bool background)
    {
        // https://learn.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences?redirectedfrom=MSDN#text-formatting
        data[index] = ConsoleEscapeCharacter;
        data[index + 1] = '[';
        data[index + 2] = background ? '4' : '3';
        data[index + 3] = '8';
        data[index + 4] = ';';
        data[index + 5] = '2';
        data[index + 6] = ';';
        index += 6;
        if (r > 99)
            data[++index] = (char)(r / 100 + 48);
        if (r > 9)
            data[++index] = (char)((r % 100) / 10 + 48);
        data[++index] = (char)(r % 10 + 48);
        data[++index] = ';';
        if (g > 99)
            data[++index] = (char)(g / 100 + 48);
        if (g > 9)
            data[++index] = (char)((g % 100) / 10 + 48);
        data[++index] = (char)(g % 10 + 48);
        data[++index] = ';';
        if (b > 99)
            data[++index] = (char)(b / 100 + 48);
        if (b > 9)
            data[++index] = (char)((b % 100) / 10 + 48);
        data[++index] = (char)(b % 10 + 48);
        data[index + 1] = 'm';
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

    /// <summary>
    /// Adds spaces to a proper-case string. For example: DevkitServer -> Devkit Server.
    /// </summary>
    /// <remarks>Also replaces underscores with spaces.</remarks>
    public static string SpaceProperCaseString(string text)
    {
        if (text.Length < 1)
            return text;

        if (text.IndexOf('_', 1) != -1)
            text = text.Replace('_', ' ');

        for (int i = 1; i < text.Length; ++i)
        {
            char current = text[i];

            bool digit = char.IsDigit(current);
            bool upper = char.IsUpper(current);
            if (char.IsWhiteSpace(current) || !digit && !upper || char.IsWhiteSpace(text[i - 1]) ||
                digit && char.IsDigit(text[i - 1]) && (i == text.Length - 1 || char.IsDigit(text[i + 1])) ||
                upper && char.IsUpper(text[i - 1]) && (i == text.Length - 1 || char.IsUpper(text[i + 1])))
                continue;

            text = text.Substring(0, i) + " " + text.Substring(i, text.Length - i);
            ++i;
        }

        return text;
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
    public static string Format(this FieldInfo? field)
    {
        if (field == null)
            return ((object?)null).Format();
        string type = field.IsStatic
            ? "static ".ColorizeNoReset(FormatProvider.StackCleaner.Configuration.Colors!.KeywordColor)
            : string.Empty;
        type += FormatProvider.StackCleaner.GetString(field.FieldType) + " ";
        if (field.DeclaringType != null)
            type += FormatProvider.StackCleaner.GetString(field.DeclaringType) + ".";
        else
            type += "global".ColorizeNoReset(FormatProvider.StackCleaner.Configuration.Colors!.KeywordColor) +
                   "::".Colorize(FormatProvider.StackCleaner.Configuration.Colors!.PunctuationColor);

        return type + field.Name.Colorize(FormatProvider.StackCleaner.Configuration.Colors!.PropertyColor);
    }
    public static string Format(this PropertyInfo? property)
    {
        if (property == null)
            return ((object?)null).Format();
        MethodInfo? getter = property.GetGetMethod();
        MethodInfo? setter = property.GetSetMethod();
        string type = (getter == null ? setter != null && setter.IsStatic : getter.IsStatic)
            ? "static ".ColorizeNoReset(FormatProvider.StackCleaner.Configuration.Colors!.KeywordColor)
            : string.Empty;
        type += FormatProvider.StackCleaner.GetString(property.PropertyType) + " ";
        if (property.DeclaringType != null)
            type += FormatProvider.StackCleaner.GetString(property.DeclaringType) + " ";
        else
            type += "global".ColorizeNoReset(FormatProvider.StackCleaner.Configuration.Colors!.KeywordColor) +
                   "::".ColorizeNoReset(FormatProvider.StackCleaner.Configuration.Colors!.PunctuationColor) + " ";

        type += property.Name.ColorizeNoReset(FormatProvider.StackCleaner.Configuration.Colors!.PropertyColor) + " {".ColorizeNoReset(FormatProvider.StackCleaner.Configuration.Colors!.PunctuationColor);
        
        if (getter != null)
        {
            if (getter.IsAssembly)
                type += " internal get".ColorizeNoReset(FormatProvider.StackCleaner.Configuration.Colors!.KeywordColor);
            else if (getter.IsFamilyAndAssembly)
                type += " protected internal get".ColorizeNoReset(FormatProvider.StackCleaner.Configuration.Colors!.KeywordColor);
            else if (getter.IsPrivate)
                type += " private get".ColorizeNoReset(FormatProvider.StackCleaner.Configuration.Colors!.KeywordColor);
            else if (getter.IsFamily)
                type += " protected get".ColorizeNoReset(FormatProvider.StackCleaner.Configuration.Colors!.KeywordColor);
            else
                type += " get".ColorizeNoReset(FormatProvider.StackCleaner.Configuration.Colors!.KeywordColor);
            type += ";".ColorizeNoReset(FormatProvider.StackCleaner.Configuration.Colors!.PunctuationColor);
        }

        if (setter != null)
        {
            if (setter.IsAssembly)
                type += " internal set".ColorizeNoReset(FormatProvider.StackCleaner.Configuration.Colors!.KeywordColor);
            else if (setter.IsFamilyAndAssembly)
                type += " protected internal set".ColorizeNoReset(FormatProvider.StackCleaner.Configuration.Colors!.KeywordColor);
            else if (setter.IsPrivate)
                type += " private set".ColorizeNoReset(FormatProvider.StackCleaner.Configuration.Colors!.KeywordColor);
            else if (setter.IsFamily)
                type += " protected set".ColorizeNoReset(FormatProvider.StackCleaner.Configuration.Colors!.KeywordColor);
            else
                type += " set".ColorizeNoReset(FormatProvider.StackCleaner.Configuration.Colors!.KeywordColor);
            type += ";".ColorizeNoReset(FormatProvider.StackCleaner.Configuration.Colors!.PunctuationColor);
        }

        type += " }";

        return type + GetReset();
    }
    public static string Format(this MethodBase? method) => method == null ? ((object)null!).Format() : FormatProvider.StackCleaner.GetString(method);
    public static string FormatMethod(Type delegateType, string name, bool removeInstance = false, bool isStatic = false, bool isAsync = false, bool isGetter = false, bool isSetter = false, bool isIndexer = false, Type? declTypeOverride = null)
    {
        Accessor.GetDelegateSignature(delegateType, out Type returnType, out ParameterInfo[] parameters);
        (Type type, string? name)[] typeParameters = new (Type, string?)[removeInstance && parameters.Length > 0 ? parameters.Length - 1 : parameters.Length];
        for (int i = 0; i < parameters.Length; ++i)
            typeParameters[i] = (parameters[removeInstance ? i + 1 : i].ParameterType, parameters[removeInstance ? i + 1 : i].Name);
        
        return FormatMethod(returnType, declTypeOverride ?? (removeInstance && parameters.Length > 0 ? parameters[0].ParameterType : null), name, typeParameters, null, null, isStatic, isAsync, isGetter, isSetter, isIndexer);
    }
    public static string FormatMethod<TDelegate>(string name, bool removeInstance = false, bool isStatic = false, bool isAsync = false, bool isGetter = false, bool isSetter = false, bool isIndexer = false, Type? declTypeOverride = null) where TDelegate : Delegate
    {
        ParameterInfo[] parameters = Accessor.GetParameters<TDelegate>();
        Type returnType = Accessor.GetReturnType<TDelegate>();
        (Type type, string? name)[] typeParameters = new (Type, string?)[removeInstance && parameters.Length > 0 ? parameters.Length - 1 : parameters.Length];
        for (int i = 0; i < parameters.Length; ++i)
            typeParameters[i] = (parameters[removeInstance ? i + 1 : i].ParameterType, parameters[removeInstance ? i + 1 : i].Name);
        
        return FormatMethod(returnType, declTypeOverride ?? (removeInstance && parameters.Length > 0 ? parameters[0].ParameterType : null), name, typeParameters, null, null, isStatic, isAsync, isGetter, isSetter, isIndexer);
    }
    public static string FormatMethod(Type? rtnType, Type? declType, string name, (Type type, string? name)[]? namedArguments = null, Type[]? arguments = null, Type[]? genericArgs = null, bool isStatic = false, bool isAsync = false, bool isGetter = false, bool isSetter = false, bool isIndexer = false)
    {
        StringBuilder sb = new StringBuilder(32);
        if (!isIndexer && isStatic)
            sb.Append("static ".Colorize(FormatProvider.StackCleaner.Configuration.Colors!.KeywordColor));
        if (isAsync && !(isGetter || isSetter || isIndexer))
            sb.Append("async ".Colorize(FormatProvider.StackCleaner.Configuration.Colors!.KeywordColor));

        if (rtnType != null)
            sb.Append(Format(rtnType)).Append(' ');

        if (isGetter)
            sb.Append("get ".Colorize(FormatProvider.StackCleaner.Configuration.Colors!.KeywordColor));
        if (isSetter)
            sb.Append("set ".Colorize(FormatProvider.StackCleaner.Configuration.Colors!.KeywordColor));

        if (declType != null)
            sb.Append(Format(declType)).Append('.');

        if (!isIndexer)
            sb.Append(name.Colorize(FormatProvider.StackCleaner.Configuration.Colors!.MethodColor));
        else
            sb.Append("this".Colorize(FormatProvider.StackCleaner.Configuration.Colors!.KeywordColor));

        if (!(isGetter || isSetter || isIndexer) && genericArgs is { Length: > 0 })
        {
            sb.Append("<".Colorize(FormatProvider.StackCleaner.Configuration.Colors!.PunctuationColor));
            for (int i = 0; i < genericArgs.Length; ++i)
            {
                if (i != 0)
                    sb.Append(", ".Colorize(FormatProvider.StackCleaner.Configuration.Colors!.PunctuationColor));
                sb.Append(Format(genericArgs[i]));
            }
            sb.Append(">".Colorize(FormatProvider.StackCleaner.Configuration.Colors!.PunctuationColor));
        }
        if (isIndexer || !isGetter && !isSetter)
        {
            sb.Append((isIndexer ? "[" : "(").Colorize(FormatProvider.StackCleaner.Configuration.Colors!.PunctuationColor));
            if (namedArguments is { Length: > 0 })
            {
                for (int i = 0; i < namedArguments.Length; ++i)
                {
                    (Type type, string? paramName) = namedArguments[i];
                    if (i != 0)
                        sb.Append(", ".Colorize(FormatProvider.StackCleaner.Configuration.Colors!.PunctuationColor));
                    sb.Append(Format(type));
                    if (!string.IsNullOrEmpty(paramName))
                        sb.Append(" " + paramName!.Colorize(FormatProvider.StackCleaner.Configuration.Colors!.ParameterColor));
                }
            }
            else if (arguments is { Length: > 0 })
            {
                for (int i = 0; i < arguments.Length; ++i)
                {
                    Type type = arguments[i];
                    if (i != 0)
                        sb.Append(", ".Colorize(FormatProvider.StackCleaner.Configuration.Colors!.PunctuationColor));
                    sb.Append(Format(type));
                }
            }
            sb.Append((isIndexer ? "]" : ")").Colorize(FormatProvider.StackCleaner.Configuration.Colors!.PunctuationColor));
        }
        return sb.ToString();
    }
    public static string Format(this Type? typedef) => typedef == null ? ((object)null!).Format() : FormatProvider.StackCleaner.GetString(typedef);
    public static string Format(this ExceptionBlock? block)
    {
        if (block == null)
            return ((object)null!).Format();
        switch (block.blockType)
        {
            case ExceptionBlockType.BeginExceptionBlock:
                return GetColor(FormatProvider.StackCleaner.Configuration.Colors!.FlowKeywordColor) + "try" + GetReset() + Environment.NewLine + "{";
            case ExceptionBlockType.EndExceptionBlock:
                return "}";
            case ExceptionBlockType.BeginExceptFilterBlock:
                return GetColor(FormatProvider.StackCleaner.Configuration.Colors!.FlowKeywordColor) + " when" + GetReset() + Environment.NewLine + "{";
            case ExceptionBlockType.BeginCatchBlock:
                string str = "}" + GetColor(FormatProvider.StackCleaner.Configuration.Colors!.FlowKeywordColor) + "catch" + GetReset() + Environment.NewLine;
                if (block.catchType != null)
                    str += " (" + block.catchType.Format() + ")";
                return str;
            case ExceptionBlockType.BeginFinallyBlock:
                return "}" + GetColor(FormatProvider.StackCleaner.Configuration.Colors!.FlowKeywordColor) + "finally" + GetReset() + Environment.NewLine + "{";
            case ExceptionBlockType.BeginFaultBlock:
                return "}" + GetColor(FormatProvider.StackCleaner.Configuration.Colors!.FlowKeywordColor) + "fault" + GetReset() + Environment.NewLine + "{";
        }

        return "}" + GetColor(FormatProvider.StackCleaner.Configuration.Colors!.FlowKeywordColor) + block.blockType + GetReset() + Environment.NewLine + "{";
    }
    public static string Format(this Label label) => (FormatProvider.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.None
        ? string.Empty
        : (FormatProvider.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.ExtendedANSIColor
            ? GetExtendedANSIString(FormatProvider.StackCleaner.Configuration.Colors!.StructColor, false)
            : GetANSIString(ToConsoleColor(FormatProvider.StackCleaner.Configuration.Colors!.StructColor), false))) + "Label #" + label.GetLabelId() +
                                                     GetReset();
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
                    op += " " + GetColor(FormatProvider.StackCleaner.Configuration.Colors!.ExtraDataColor) + num + GetReset();
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
                    op += " " + GetColor(FormatProvider.StackCleaner.Configuration.Colors!.ExtraDataColor) + lng + GetReset();
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
                    op += " " + GetColor(FormatProvider.StackCleaner.Configuration.Colors!.ExtraDataColor) + dbl + GetReset();
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
                    op += " " + GetColor(FormatProvider.StackCleaner.Configuration.Colors!.ExtraDataColor) + num + GetReset();
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
                        op += Environment.NewLine + "  " + GetColor(FormatProvider.StackCleaner.Configuration.Colors!.ExtraDataColor) + i + GetReset() + " => " + GetColor(FormatProvider.StackCleaner.Configuration.Colors!.StructColor) + " Label #" + jumps[i].GetLabelId() + GetReset();

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
                    op += " " + GetColor(FormatProvider.StackCleaner.Configuration.Colors!.ExtraDataColor) + lb.LocalIndex + GetReset() + " : " + lb.LocalType!.Format();
                else if (instruction.operand is int index)
                    op += " " + GetColor(FormatProvider.StackCleaner.Configuration.Colors!.ExtraDataColor) + index + GetReset();
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
        if (FormatProvider.StackCleaner.Configuration.ColorFormatting != StackColorFormatType.None)
        {
            int argb = ToArgb(instruction.FlowControl switch
            {
                FlowControl.Call => new Color32(220, 220, 170, 255),
                FlowControl.Branch => new Color32(216, 160, 223, 255),
                FlowControl.Cond_Branch => new Color32(224, 179, 230, 255),
                FlowControl.Break or FlowControl.Return or FlowControl.Throw => new Color32(208, 140, 217, 255),
                _ => new Color32(86, 156, 214, 255)
            });
            if (FormatProvider.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.ExtendedANSIColor)
                clr = GetExtendedANSIString(argb, false);
            else
                clr = GetANSIString(ToConsoleColor(argb), false);
        }

        if (clr != null)
            return clr + instruction.Name + ANSIForegroundReset;

        return instruction.Name;
    }

    public static readonly Color32 StringColor = new Color32(214, 157, 133, 255);
    public static string Format(this string? str, bool quotes)
    {
        if (str == null) return ((object?)null).Format();
        if (FormatProvider.StackCleaner.Configuration.ColorFormatting != StackColorFormatType.None)
        {
            string clr = FormatProvider.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.ExtendedANSIColor
                ? GetExtendedANSIString(ToArgb(StringColor), false)
                : GetANSIString(ToConsoleColor(ToArgb(StringColor)), false);

            if (quotes)
                return clr + "\"" + str + "\"" + ANSIForegroundReset;
            
            return clr + str + ANSIForegroundReset;
        }

        return str;
    }
    public static string Format(this object? obj, string? format = null)
    {
        if (obj == null || obj.Equals(null))
        {
            if (FormatProvider.StackCleaner.Configuration.ColorFormatting != StackColorFormatType.None)
                return GetColor(FormatProvider.StackCleaner.Configuration.Colors!.KeywordColor) + "null" + ANSIForegroundReset;
            return "null";
        }

        if (obj is IDevkitServerPlugin plugin)
        {
            if (FormatProvider.StackCleaner.Configuration.ColorFormatting != StackColorFormatType.None)
                return GetColor(ToArgb(plugin is IDevkitServerColorPlugin p ? p.Color : Plugin.DefaultColor)) + plugin.Name + ANSIForegroundReset;
            return "null";
        }

        if (obj is ITerminalFormattable formattable)
            return formattable.Format(FormatProvider);

        if (obj is Guid guid)
        {
            if (format != null)
                return guid.ToString(format).Colorize(FormatProvider.StackCleaner.Configuration.Colors!.StructColor);
            return ("{" + guid.ToString("N") + "}").Colorize(FormatProvider.StackCleaner.Configuration.Colors!.StructColor);
        }
        if (obj is IAssetReference assetReference)
        {
            if (Assets.find(assetReference.GUID) is { } asset2)
                obj = asset2;
            else
                return ("{" + assetReference.GUID.ToString("N") + "}").Colorize(FormatProvider.StackCleaner.Configuration.Colors!.StructColor);
        }
        if (obj is Asset asset)
        {
            Color color = asset switch
            {
                ItemAsset item => ItemTool.getRarityColorUI(item.rarity),
                VehicleAsset vehicle => ItemTool.getRarityColorUI(vehicle.rarity),
                _ => ItemTool.getRarityColorUI(EItemRarity.COMMON)
            };
            string name = (asset.FriendlyName ?? asset.name).Colorize(color);
            return ("[" + asset.assetCategory + "] {" + asset.GUID.ToString("N") + "}").Colorize(color) + " : ".Colorize(ConsoleColor.White) + name;
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
            if (FormatProvider.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.None)
                return s64.ToString("D17");

            return GetColor(ToArgb(new Color32(102, 192, 244, 255))) + s64.ToString("D17") + ANSIForegroundReset;
        }
        if (obj is CSteamID cs64 && cs64.UserSteam64())
        {
            if (FormatProvider.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.None)
                return cs64.m_SteamID.ToString("D17");

            return GetColor(ToArgb(new Color32(102, 192, 244, 255))) + cs64.m_SteamID.ToString("D17") + ANSIForegroundReset;
        }
        if (obj is ITransportConnection connection)
        {
            if (FormatProvider.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.None)
                return connection.GetAddressString(true);

            if (connection.TryGetIPv4Address(out uint addr))
            {
                str = GetColor(ToArgb(new Color32(204, 255, 102, 255)))
                      + Parser.getIPFromUInt32(addr);
                if (connection.TryGetPort(out ushort port))
                    str += GetColor(FormatProvider.StackCleaner.Configuration.Colors!.PunctuationColor) + ":" +
                           GetColor(ToArgb(new Color32(170, 255, 0, 255))) +
                           port.ToString(CultureInfo.InvariantCulture);

                return str + ANSIForegroundReset;
            }

            return GetColor(ToArgb(new Color32(204, 255, 102, 255))) + (connection.GetAddressString(true) ?? "<unknown address>") + ANSIForegroundReset;
        }

        if (obj is Version version)
        {
            if (FormatProvider.StackCleaner.Configuration.ColorFormatting != StackColorFormatType.None)
                return version.ToString(4).Colorize(FormattingColorType.Struct);
            return version.ToString(4);
        }

        if (FormatProvider.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.None)
            return str;
        if (str.Equals(type.ToString(), StringComparison.Ordinal))
            return "{" + type.Format() + "}";

        if (FormatProvider.StackCleaner.Configuration.ColorFormatting != StackColorFormatType.None)
        {
            if (obj is string or char)
                return GetColor(ToArgb(new Color32(214, 157, 133, 255))) + "\"" + str + "\"" + ANSIForegroundReset;

            if (type.IsEnum)
                return GetColor(FormatProvider.StackCleaner.Configuration.Colors!.EnumColor) + str + ANSIForegroundReset;

            if (type.IsPrimitive)
            {
                if (obj is bool)
                    return GetColor(FormatProvider.StackCleaner.Configuration.Colors!.KeywordColor) + str + ANSIForegroundReset;

                Color32 color = new Color32(181, 206, 168, 255);
                if (format != null)
                {
                    switch (obj)
                    {
                        case float n:
                            return GetColor(ToArgb(color)) + n.ToString(format) + ANSIForegroundReset;
                        case double n:
                            return GetColor(ToArgb(color)) + n.ToString(format) + ANSIForegroundReset;
                        case decimal n:
                            return GetColor(ToArgb(color)) + n.ToString(format) + ANSIForegroundReset;
                        case int n:
                            return GetColor(ToArgb(color)) + n.ToString(format) + ANSIForegroundReset;
                        case uint n:
                            return GetColor(ToArgb(color)) + n.ToString(format) + ANSIForegroundReset;
                        case short n:
                            return GetColor(ToArgb(color)) + n.ToString(format) + ANSIForegroundReset;
                        case ushort n:
                            return GetColor(ToArgb(color)) + n.ToString(format) + ANSIForegroundReset;
                        case sbyte n:
                            return GetColor(ToArgb(color)) + n.ToString(format) + ANSIForegroundReset;
                        case byte n:
                            return GetColor(ToArgb(color)) + n.ToString(format) + ANSIForegroundReset;
                        case long n:
                            return GetColor(ToArgb(color)) + n.ToString(format) + ANSIForegroundReset;
                        case ulong n:
                            return GetColor(ToArgb(color)) + n.ToString(format) + ANSIForegroundReset;
                    }
                }


                return GetColor(ToArgb(color)) + str + ANSIForegroundReset;
            }

            if (type.IsInterface)
                return GetColor(FormatProvider.StackCleaner.Configuration.Colors!.InterfaceColor) + str + ANSIForegroundReset;

            if (type.IsValueType)
                return GetColor(FormatProvider.StackCleaner.Configuration.Colors!.StructColor) + str + ANSIForegroundReset;

            return GetColor(FormatProvider.StackCleaner.Configuration.Colors!.ClassColor) + str + ANSIForegroundReset;
        }

        return str;
    }
    public static string Colorize(this string str, ConsoleColor color)
    {
        if (FormatProvider.StackCleaner.Configuration.ColorFormatting != StackColorFormatType.None)
        {
            return GetANSIString(color, false) + str + ANSIForegroundReset;
        }

        return str;
    }
    public static string Colorize(this string str, Color color)
    {
        if (FormatProvider.StackCleaner.Configuration.ColorFormatting != StackColorFormatType.None)
        {
            return (FormatProvider.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.ExtendedANSIColor
                       ? GetExtendedANSIString(ToArgb(color), false)
                       : GetANSIString(ToConsoleColor(ToArgb(color)), false))
                + str + ANSIForegroundReset;
        }

        return str;
    }
    public static string Colorize(this string str, Color32 color)
    {
        if (FormatProvider.StackCleaner.Configuration.ColorFormatting != StackColorFormatType.None)
        {
            return (FormatProvider.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.ExtendedANSIColor
                       ? GetExtendedANSIString(ToArgb(color), false)
                       : GetANSIString(ToConsoleColor(ToArgb(color)), false))
                + str + ANSIForegroundReset;
        }

        return str;
    }
    public static string Colorize(this string str, int argb)
    {
        if (FormatProvider.StackCleaner.Configuration.ColorFormatting != StackColorFormatType.None)
        {
            return (FormatProvider.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.ExtendedANSIColor
                       ? GetExtendedANSIString(argb, false)
                       : GetANSIString(ToConsoleColor(argb), false))
                + str + ANSIForegroundReset;
        }

        return str;
    }
    public static string Colorize(this string str, FormattingColorType tokenType)
    {
        if (FormatProvider.StackCleaner.Configuration.ColorFormatting != StackColorFormatType.None)
            return ColorizeNoReset(str, tokenType) + ANSIForegroundReset;
        return str;
    }
    public static string ColorizeNoReset(this string str, FormattingColorType tokenType)
    {
        StackCleanerConfiguration config = FormatProvider.StackCleaner.Configuration;
        if (config.ColorFormatting != StackColorFormatType.None)
        {
            ColorConfig colors = config.Colors ?? (config.ColorFormatting == StackColorFormatType.ExtendedANSIColor ? (DevkitServerModule.UnityLoaded ? UnityColor32Config.Default : Color32Config.Default) : Color4Config.Default);
            int argb = tokenType switch
            {
                FormattingColorType.Keyword => colors.KeywordColor,
                FormattingColorType.Method => colors.MethodColor,
                FormattingColorType.Property => colors.PropertyColor,
                FormattingColorType.Parameter => colors.ParameterColor,
                FormattingColorType.Class => colors.ClassColor,
                FormattingColorType.Struct => colors.StructColor,
                FormattingColorType.FlowKeyword => colors.FlowKeywordColor,
                FormattingColorType.Interface => colors.InterfaceColor,
                FormattingColorType.GenericParameter => colors.GenericParameterColor,
                FormattingColorType.Enum => colors.EnumColor,
                FormattingColorType.Namespace => colors.NamespaceColor,
                FormattingColorType.Punctuation => colors.PunctuationColor,
                FormattingColorType.ExtraData => colors.ExtraDataColor,
                FormattingColorType.LinesHiddenWarning => colors.LinesHiddenWarningColor,
                FormattingColorType.HtmlBackground => colors.HtmlBackgroundColor,
                _ => unchecked((int)0xFFFFFFFF)
            };

            return (config.ColorFormatting == StackColorFormatType.ExtendedANSIColor
                       ? GetExtendedANSIString(argb, false)
                       : GetANSIString(ToConsoleColor(argb), false))
                + str + ANSIForegroundReset;
        }

        return str;
    }
    public static string ColorizeNoReset(this string str, ConsoleColor color)
    {
        if (FormatProvider.StackCleaner.Configuration.ColorFormatting != StackColorFormatType.None)
        {
            return GetANSIString(color, false) + str;
        }

        return str;
    }
    public static string ColorizeNoReset(this string str, Color color)
    {
        if (FormatProvider.StackCleaner.Configuration.ColorFormatting != StackColorFormatType.None)
        {
            return (FormatProvider.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.ExtendedANSIColor
                       ? GetExtendedANSIString(ToArgb(color), false)
                       : GetANSIString(ToConsoleColor(ToArgb(color)), false))
                + str;
        }

        return str;
    }
    public static string ColorizeNoReset(this string str, Color32 color)
    {
        if (FormatProvider.StackCleaner.Configuration.ColorFormatting != StackColorFormatType.None)
        {
            return (FormatProvider.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.ExtendedANSIColor
                       ? GetExtendedANSIString(ToArgb(color), false)
                       : GetANSIString(ToConsoleColor(ToArgb(color)), false))
                + str;
        }

        return str;
    }
    public static string ColorizeNoReset(this string str, int argb)
    {
        if (FormatProvider.StackCleaner.Configuration.ColorFormatting != StackColorFormatType.None)
        {
            return (FormatProvider.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.ExtendedANSIColor
                       ? GetExtendedANSIString(argb, false)
                       : GetANSIString(ToConsoleColor(argb), false))
                + str;
        }

        return str;
    }
    private static string GetColor(int argb)
    {
        return (FormatProvider.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.None
            ? string.Empty
            : (FormatProvider.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.ExtendedANSIColor
                ? GetExtendedANSIString(argb, false)
                : GetANSIString(ToConsoleColor(argb), false)));
    }
    private static string GetReset() => FormatProvider.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.None ? string.Empty : ANSIForegroundReset;

    public static void PrintBytesHex(byte[] bytes, int columnCount = 64, int offset = 0, int len = -1)
    {
        Logger.LogInfo(Environment.NewLine + GetBytesHex(bytes, columnCount, offset, len));
    }
    public static void PrintBytesDec(byte[] bytes, int columnCount = 64, int offset = 0, int len = -1)
    {
        Logger.LogInfo(Environment.NewLine + GetBytesDec(bytes, columnCount, offset, len));
    }
    [Pure]
    public static string GetBytesHex(byte[] bytes, int columnCount = 64, int offset = 0, int len = -1)
    {
        return BytesToString(bytes, columnCount, offset, len, "X2");
    }
    [Pure]
    public static string GetBytesDec(byte[] bytes, int columnCount = 64, int offset = 0, int len = -1)
    {
        return BytesToString(bytes, columnCount, offset, len, "000");
    }
    public static unsafe void PrintBytesHex(byte* bytes, int len, int columnCount = 64, int offset = 0)
    {
        Logger.LogInfo(Environment.NewLine + GetBytesHex(bytes, len, columnCount, offset));
    }
    public static unsafe void PrintBytesDec(byte* bytes, int len, int columnCount = 64, int offset = 0)
    {
        Logger.LogInfo(Environment.NewLine + GetBytesDec(bytes, len, columnCount, offset));
    }
    [Pure]
    public static unsafe string GetBytesHex(byte* bytes, int len, int columnCount = 64, int offset = 0)
    {
        return BytesToString(bytes, columnCount, offset, len, "X2");
    }
    [Pure]
    public static unsafe string GetBytesDec(byte* bytes, int len, int columnCount = 64, int offset = 0)
    {
        return BytesToString(bytes, columnCount, offset, len, "000");
    }
    public static unsafe void PrintBytesHex<T>(T* bytes, int len, int columnCount = 64, int offset = 0) where T : unmanaged
    {
        Logger.LogInfo(Environment.NewLine + GetBytesHex(bytes, len, columnCount, offset));
    }
    public static unsafe void PrintBytesDec<T>(T* bytes, int len, int columnCount = 64, int offset = 0) where T : unmanaged
    {
        Logger.LogInfo(Environment.NewLine + GetBytesDec(bytes, len, columnCount, offset));
    }
    [Pure]
    public static unsafe string GetBytesHex<T>(T* bytes, int len, int columnCount = 64, int offset = 0) where T : unmanaged
    {
        return BytesToString(bytes, columnCount, offset, len);
    }
    [Pure]
    public static unsafe string GetBytesDec<T>(T* bytes, int len, int columnCount = 64, int offset = 0) where T : unmanaged
    {
        return BytesToString(bytes, columnCount, offset, len);
    }
    [Pure]
    public static string BytesToString(byte[] bytes, int columnCount, int offset, int len, string fmt)
    {
        if (offset >= bytes.Length)
            offset = bytes.Length - 1;
        if (len < 0 || len + offset < 0 || len + offset > bytes.Length)
            len = bytes.Length - offset;
        StringBuilder sb = new StringBuilder(len * 4);
        for (int i = 0; i < len; ++i)
        {
            if (i != 0 && i % columnCount == 0)
                sb.Append(Environment.NewLine);
            else if (i != 0)
                sb.Append(' ');
            sb.Append(bytes[i + offset].ToString(fmt));
        }
        return sb.ToString();
    }
    [Pure]
    public static unsafe string BytesToString(byte* bytes, int columnCount, int offset, int len, string fmt)
    {
        if (offset >= len)
            offset = len - 1;
        StringBuilder sb = new StringBuilder(len * 4);
        for (int i = 0; i < len; ++i)
        {
            if (i != 0 && i % columnCount == 0)
                sb.Append(Environment.NewLine);
            else if (i != 0)
                sb.Append(' ');
            sb.Append(bytes[i + offset].ToString(fmt));
        }
        return sb.ToString();
    }
    [Pure]
    public static unsafe string BytesToString<T>(T* bytes, int columnCount, int offset, int len) where T : unmanaged
    {
        if (offset >= len)
            offset = len - 1;
        StringBuilder sb = new StringBuilder(len * 4);
        for (int i = 0; i < len; ++i)
        {
            if (i != 0 && i % columnCount == 0)
                sb.Append(Environment.NewLine);
            else if (i != 0)
                sb.Append(' ');
            sb.Append(bytes[i + offset].ToString());
        }
        return sb.ToString();
    }

    /// <summary>
    /// Remove rich text tags from text, and replace &lt;color&gt; and &lt;mark&gt; tags with the ANSI or extended ANSI equivalent.
    /// </summary>
    /// <param name="options">Tags to check for and remove.</param>
    /// <param name="argbForeground">Color to reset the foreground to.</param>
    /// <param name="argbBackground">Color to reset the background to.</param>
    /// <exception cref="ArgumentOutOfRangeException"/>
    [Pure]
    public static unsafe string ConvertRichTextToANSI(string str, int index = 0, int length = -1, RemoveRichTextOptions options = RemoveRichTextOptions.All, int argbForeground = DefaultForeground, int argbBackground = DefaultBackground)
    {
        CheckTags();
        if (index >= str.Length || index < 0)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (length < 0)
            length = str.Length - index;
        else if (index + length > str.Length)
            throw new ArgumentOutOfRangeException(nameof(length));
        else if (length == 0)
            return str;

        const int defaultForegroundStackSize = 2;
        StackColorFormatType format = FormatProvider.StackCleaner.Configuration.ColorFormatting;

        char[] rtn = new char[str.Length + 16];
        int foregroundStackLength = defaultForegroundStackSize;
        int backgroundStackLength = 0;
        Color32* foregroundStack = stackalloc Color32[defaultForegroundStackSize];
        Color32* backgroundStack = null;
        int foregroundStackValuesLength = 0;
        int backgroundStackValuesLength = 0;
        int nextCopyStartIndex = 0;
        int writeIndex = 0;
        bool useColor = format is StackColorFormatType.ExtendedANSIColor or StackColorFormatType.ANSIColor;

        int nonDefaults = 1 | 2;
        if (useColor)
            AppendDefaults();

        fixed (char* mainPtr = str)
        {
            char* ptr = mainPtr + index;
            for (int i = 0; i < length; ++i)
            {
                char current = ptr[i];
                if (current == '<')
                {
                    bool pushColor = false;
                    bool background = false;
                    bool isEndTag = i != length - 1 && ptr[i + 1] == '/';
                    int endIndex = -1;
                    for (int j = i + (isEndTag ? 2 : 1); j < length; ++j)
                    {
                        if (ptr[j] == '>')
                        {
                            endIndex = j;
                            break;
                        }
                    }

                    if (endIndex == -1)
                        continue;

                    if (!isEndTag && useColor)
                    {
                        int colorIndex = -1;
                        int colorLength = 0;
                        // <color=#etc>
                        if (ptr[i + 1] is 'c' or 'C' && i + 7 <= endIndex && (options & RemoveRichTextOptions.Color) != 0 &&
                            ptr[i + 2] is 'o' or 'O' &&
                            ptr[i + 3] is 'l' or 'L' &&
                            ptr[i + 4] is 'o' or 'O' &&
                            ptr[i + 5] is 'r' or 'R' &&
                            ptr[i + 6] == '=')
                        {
                            colorIndex = i + 7;
                            colorLength = endIndex - (i + 7);
                        }
                        else if (ptr[i + 1] == '#' && (options & RemoveRichTextOptions.Color) != 0 && i + 2 <= endIndex)
                        {
                            colorIndex = i + 1;
                            colorLength = endIndex - (i + 1);
                        }
                        else if ((options & RemoveRichTextOptions.Mark) != 0 &&
                                 ptr[i + 1] is 'm' or 'M' && i + 6 <= endIndex &&
                                 ptr[i + 2] is 'a' or 'A' &&
                                 ptr[i + 3] is 'r' or 'R' &&
                                 ptr[i + 4] is 'k' or 'K' &&
                                 ptr[i + 5] == '=')
                        {
                            colorIndex = i + 6;
                            colorLength = endIndex - (i + 6);
                            background = true;
                        }
                        else if (!CompareRichTextTag(ptr, endIndex, i, options))
                            continue;

                        if (colorIndex >= 0 && DevkitServerUtility.TryParseColor32(new string(ptr, colorIndex, colorLength), out Color32 color))
                        {
                            pushColor = true;
                            if (!background)
                            {
                                if (foregroundStackValuesLength >= foregroundStackLength)
                                {
                                    // ReSharper disable once StackAllocInsideLoop (won't happen much)
                                    Color32* newAlloc = stackalloc Color32[foregroundStackValuesLength + 1];
                                    if (foregroundStackValuesLength > 0)
                                        Buffer.MemoryCopy(foregroundStack, newAlloc, foregroundStackValuesLength * sizeof(Color32), foregroundStackValuesLength * sizeof(Color32));
                                    foregroundStackLength = foregroundStackValuesLength + 1;
                                    foregroundStack = newAlloc;
                                }

                                foregroundStack[foregroundStackValuesLength] = color;
                                ++foregroundStackValuesLength;
                            }
                            else
                            {
                                background = true;
                                if (backgroundStackValuesLength >= backgroundStackLength)
                                {
                                    // ReSharper disable once StackAllocInsideLoop (won't happen much)
                                    Color32* newAlloc = stackalloc Color32[backgroundStackLength + 1];
                                    if (backgroundStackValuesLength > 0)
                                        Buffer.MemoryCopy(backgroundStack, newAlloc, backgroundStackValuesLength * sizeof(Color32), backgroundStackValuesLength * sizeof(Color32));
                                    backgroundStackLength = backgroundStackValuesLength + 1;
                                    backgroundStack = newAlloc;
                                }

                                backgroundStack![backgroundStackValuesLength] = color;
                                ++backgroundStackValuesLength;
                            }
                        }
                    }
                    else if (useColor && (options & RemoveRichTextOptions.Color) != 0 &&
                             ptr[i + 2] is 'c' or 'C' &&
                             ptr[i + 3] is 'o' or 'O' &&
                             ptr[i + 4] is 'l' or 'L' &&
                             ptr[i + 5] is 'o' or 'O' &&
                             ptr[i + 6] is 'r' or 'R')
                    {
                        if (foregroundStackValuesLength > 0)
                            --foregroundStackValuesLength;
                        pushColor = true;
                    }
                    else if (useColor && (options & RemoveRichTextOptions.Mark) != 0 &&
                             ptr[i + 2] is 'm' or 'M' &&
                             ptr[i + 3] is 'a' or 'A' &&
                             ptr[i + 4] is 'r' or 'R' &&
                             ptr[i + 5] is 'k' or 'K')
                    {
                        if (backgroundStackValuesLength > 0)
                            --backgroundStackValuesLength;
                        pushColor = true;
                        background = true;
                    }
                    else if (!CompareRichTextTag(ptr, endIndex, i, options))
                        continue;

                    Append(ref rtn, ptr + nextCopyStartIndex, writeIndex, i - nextCopyStartIndex);
                    writeIndex += i - nextCopyStartIndex;
                    nextCopyStartIndex = endIndex + 1;
                    i = endIndex;
                    if (pushColor)
                    {
                        int len = background ? backgroundStackValuesLength : foregroundStackValuesLength;
                        if (len > 0)
                        {
                            Color32* nextColor = (background ? backgroundStack : foregroundStack) + (len - 1);

                            if (format == StackColorFormatType.ExtendedANSIColor)
                                writeIndex += AppendExtANSIForegroundCode(ref rtn, writeIndex, nextColor->r, nextColor->g, nextColor->b, background);
                            else
                                writeIndex += AppendANSIForegroundCode(ref rtn, writeIndex, ToConsoleColor(ToArgb(*nextColor)), background);
                            nonDefaults |= background ? 2 : 1;
                        }
                        else
                        {
                            AppendDefaults();
                        }
                    }
                }
            }
            Append(ref rtn, ptr + nextCopyStartIndex, writeIndex, str.Length - nextCopyStartIndex);
            writeIndex += str.Length - nextCopyStartIndex;
            if (useColor)
                AppendDefaults();
        }
        void AppendDefaults()
        {
            if ((nonDefaults & 2) != 0)
            {
                if (argbBackground == DefaultBackground)
                {
                    fixed (char* ptr2 = ANSIBackgroundReset)
                        Append(ref rtn, ptr2, writeIndex, ANSIBackgroundReset.Length);
                    writeIndex += ANSIBackgroundReset.Length;
                }
                else if (format == StackColorFormatType.ExtendedANSIColor)
                    writeIndex += AppendExtANSIForegroundCode(ref rtn, writeIndex, (byte)(argbBackground >> 16), (byte)(argbBackground >> 8), (byte)argbBackground, true);
                else
                {
                    ConsoleColor consoleColor = ToConsoleColor(argbBackground);
                    if (consoleColor == ConsoleColor.Black)
                    {
                        fixed (char* ptr2 = ANSIBackgroundReset)
                            Append(ref rtn, ptr2, writeIndex, ANSIBackgroundReset.Length);
                        writeIndex += ANSIBackgroundReset.Length;
                    }
                    else
                        writeIndex += AppendANSIForegroundCode(ref rtn, writeIndex, consoleColor, true);
                }
            }

            if ((nonDefaults & 1) != 0)
            {
                if (argbForeground == DefaultForeground)
                {
                    fixed (char* ptr2 = ANSIForegroundReset)
                        Append(ref rtn, ptr2, writeIndex, ANSIForegroundReset.Length);
                    writeIndex += ANSIForegroundReset.Length;
                }
                else if (format == StackColorFormatType.ExtendedANSIColor)
                    writeIndex += AppendExtANSIForegroundCode(ref rtn, writeIndex, (byte)(argbForeground >> 16), (byte)(argbForeground >> 8), (byte)argbForeground, false);
                else
                {
                    ConsoleColor consoleColor = ToConsoleColor(argbForeground);
                    if (consoleColor == ConsoleColor.Gray)
                    {
                        fixed (char* ptr2 = ANSIForegroundReset)
                            Append(ref rtn, ptr2, writeIndex, ANSIForegroundReset.Length);
                        writeIndex += ANSIForegroundReset.Length;
                    }
                    else
                        writeIndex += AppendANSIForegroundCode(ref rtn, writeIndex, consoleColor, false);
                }
            }

            nonDefaults = 0;
        }

        return new string(rtn, 0, writeIndex);
    }
    /// <summary>
    /// Remove rich text, including TextMeshPro and normal Unity tags.
    /// </summary>
    /// <param name="options">Tags to check for and remove.</param>
    /// <exception cref="ArgumentOutOfRangeException"/>
    [Pure]
    public static unsafe string RemoveRichText(string str, int index = 0, int length = -1, RemoveRichTextOptions options = RemoveRichTextOptions.All)
    {
        CheckTags();
        if (index >= str.Length || index < 0)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (length < 0)
            length = str.Length - index;
        else if (index + length > str.Length)
            throw new ArgumentOutOfRangeException(nameof(length));
        else if (length == 0)
            return str;
        
        char[] rtn = new char[str.Length + 16];
        int nextCopyStartIndex = 0;
        int writeIndex = 0;

        fixed (char* mainPtr = str)
        {
            char* ptr = mainPtr + index;
            for (int i = 0; i < length; ++i)
            {
                char current = ptr[i];
                if (current == '<')
                {
                    bool isEndTag = i != length - 1 && ptr[i + 1] == '/';
                    int endIndex = -1;
                    for (int j = i + (isEndTag ? 2 : 1); j < length; ++j)
                    {
                        if (ptr[j] == '>')
                        {
                            endIndex = j;
                            break;
                        }
                    }

                    if (endIndex == -1 || !CompareRichTextTag(ptr, endIndex, i, options))
                        continue;

                    Append(ref rtn, ptr + nextCopyStartIndex, writeIndex, i - nextCopyStartIndex);
                    writeIndex += i - nextCopyStartIndex;
                    nextCopyStartIndex = endIndex + 1;
                    i = endIndex;
                }
            }
            Append(ref rtn, ptr + nextCopyStartIndex, writeIndex, str.Length - nextCopyStartIndex);
            writeIndex += str.Length - nextCopyStartIndex;
        }

        return new string(rtn, 0, writeIndex);
    }
    private static unsafe bool CompareRichTextTag(char* data, int endIndex, int index, RemoveRichTextOptions options)
    {
        ++index;
        if (data[index] == '/')
            ++index;
        else if (data[index] == '#')
            return true;
        for (int j = index; j < endIndex; ++j)
        {
            if (data[j] is '=' or ' ')
            {
                endIndex = j;
                break;
            }
        }

        int length = endIndex - index;
        bool found = false;
        for (int j = 0; j < _tags!.Length; ++j)
        {
            char[] tag = _tags[j];
            if (tag.Length != length) continue;
            if ((options & _tagFlags![j]) == 0)
                continue;
            bool matches = true;
            for (int k = 0; k < length; ++k)
            {
                char c = data[index + k];
                if ((int)c is > 64 and < 91)
                    c = (char)(c + 32);
                if (tag[k] != c)
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                found = true;
                break;
            }
        }

        return found;
    }
    private static void CheckTags()
    {
        _tags ??= new char[][]
        {
            "align".ToCharArray(),
            "allcaps".ToCharArray(),
            "alpha".ToCharArray(),
            "b".ToCharArray(),
            "br".ToCharArray(),
            "color".ToCharArray(),
            "cspace".ToCharArray(),
            "font".ToCharArray(),
            "font-weight".ToCharArray(),
            "gradient".ToCharArray(),
            "i".ToCharArray(),
            "indent".ToCharArray(),
            "line-height".ToCharArray(),
            "line-indent".ToCharArray(),
            "link".ToCharArray(),
            "lowercase".ToCharArray(),
            "material".ToCharArray(),
            "margin".ToCharArray(),
            "mark".ToCharArray(),
            "mspace".ToCharArray(),
            "nobr".ToCharArray(),
            "noparse".ToCharArray(),
            "page".ToCharArray(),
            "pos".ToCharArray(),
            "quad".ToCharArray(),
            "rotate".ToCharArray(),
            "s".ToCharArray(),
            "size".ToCharArray(),
            "smallcaps".ToCharArray(),
            "space".ToCharArray(),
            "sprite".ToCharArray(),
            "strikethrough".ToCharArray(),
            "style".ToCharArray(),
            "sub".ToCharArray(),
            "sup".ToCharArray(),
            "u".ToCharArray(),
            "underline".ToCharArray(),
            "uppercase".ToCharArray(),
            "voffset".ToCharArray(),
            "width".ToCharArray()
        };
        _tagFlags ??= new RemoveRichTextOptions[]
        {
            RemoveRichTextOptions.Align,
            RemoveRichTextOptions.Uppercase,
            RemoveRichTextOptions.Alpha,
            RemoveRichTextOptions.Bold,
            RemoveRichTextOptions.LineBreak,
            RemoveRichTextOptions.Color,
            RemoveRichTextOptions.CharacterSpacing,
            RemoveRichTextOptions.Font,
            RemoveRichTextOptions.FontWeight,
            RemoveRichTextOptions.Gradient,
            RemoveRichTextOptions.Italic,
            RemoveRichTextOptions.Indent,
            RemoveRichTextOptions.LineHeight,
            RemoveRichTextOptions.LineIndent,
            RemoveRichTextOptions.Link,
            RemoveRichTextOptions.Lowercase,
            RemoveRichTextOptions.Material,
            RemoveRichTextOptions.Margin,
            RemoveRichTextOptions.Mark,
            RemoveRichTextOptions.Monospace,
            RemoveRichTextOptions.NoLineBreak,
            RemoveRichTextOptions.NoParse,
            RemoveRichTextOptions.PageBreak,
            RemoveRichTextOptions.Position,
            RemoveRichTextOptions.Quad,
            RemoveRichTextOptions.Rotate,
            RemoveRichTextOptions.Strikethrough,
            RemoveRichTextOptions.Size,
            RemoveRichTextOptions.Smallcaps,
            RemoveRichTextOptions.Space,
            RemoveRichTextOptions.Sprite,
            RemoveRichTextOptions.Strikethrough,
            RemoveRichTextOptions.Style,
            RemoveRichTextOptions.Subscript,
            RemoveRichTextOptions.Superscript,
            RemoveRichTextOptions.Underline,
            RemoveRichTextOptions.Underline,
            RemoveRichTextOptions.Uppercase,
            RemoveRichTextOptions.VerticalOffset,
            RemoveRichTextOptions.TextWidth
        };
    }
    private static unsafe void Append(ref char[] arr, char* data, int index, int length)
    {
        if (length == 0) return;

        if (index + length > arr.Length)
        {
            char[] old = arr;
            arr = new char[index + length];
            Buffer.BlockCopy(old, 0, arr, 0, old.Length * sizeof(char));
        }
        for (int i = 0; i < length; ++i)
            arr[i + index] = data[i];
    }
    private static unsafe int AppendANSIForegroundCode(ref char[] data, int index, ConsoleColor color, bool background)
    {
        char* ptr = stackalloc char[5];
        SetANSICode(ptr, 0, color, background);
        Append(ref data, ptr, index, 5);
        return 5;
    }
    private static unsafe int AppendExtANSIForegroundCode(ref char[] data, int index, byte r, byte g, byte b, bool background)
    {
        int l = 10 + (r > 9 ? r > 99 ? 3 : 2 : 1) + (g > 9 ? g > 99 ? 3 : 2 : 1) + (b > 9 ? b > 99 ? 3 : 2 : 1);
        char* ptr = stackalloc char[l];
        SetExtendedANSICode(ptr, 0, r, g, b, background);
        Append(ref data, ptr, index, l);
        return l;
    }
    /// <summary>
    /// Replaces all null arguments in <paramref name="formatting"/> with the string "null".
    /// </summary>
    public static void RemoveNullFormattingArguemnts(object?[] formatting)
    {
        for (int i = 0; i < formatting.Length; i++)
            formatting[i] ??= "null";
    }
}

[Flags]
public enum RemoveRichTextOptions : ulong
{
    None = 0L,
    /// <summary>
    /// &lt;align&gt;
    /// </summary>
    Align = 1L << 0,
    /// <summary>
    /// &lt;allcaps&gt;, &lt;uppercase&gt;
    /// </summary>
    Uppercase = 1L << 1,
    /// <summary>
    /// &lt;alpha&gt;
    /// </summary>
    Alpha = 1L << 2,
    /// <summary>
    /// &lt;b&gt;
    /// </summary>
    Bold = 1L << 3,
    /// <summary>
    /// &lt;br&gt;
    /// </summary>
    LineBreak = 1L << 4,
    /// <summary>
    /// &lt;color=...&gt;, &lt;#...&gt;
    /// </summary>
    Color = 1L << 5,
    /// <summary>
    /// &lt;cspace&gt;
    /// </summary>
    CharacterSpacing = 1L << 6,
    /// <summary>
    /// &lt;font&gt;
    /// </summary>
    Font = 1L << 7,
    /// <summary>
    /// &lt;font-weight&gt;
    /// </summary>
    FontWeight = 1L << 8,
    /// <summary>
    /// &lt;gradient&gt;
    /// </summary>
    Gradient = 1L << 9,
    /// <summary>
    /// &lt;i&gt;
    /// </summary>
    Italic = 1L << 10,
    /// <summary>
    /// &lt;indent&gt;
    /// </summary>
    Indent = 1L << 11,
    /// <summary>
    /// &lt;line-height&gt;
    /// </summary>
    LineHeight = 1L << 12,
    /// <summary>
    /// &lt;line-indent&gt;
    /// </summary>
    LineIndent = 1L << 13,
    /// <summary>
    /// &lt;link&gt;
    /// </summary>
    Link = 1L << 14,
    /// <summary>
    /// &lt;lowercase&gt;
    /// </summary>
    Lowercase = 1L << 15,
    /// <summary>
    /// &lt;material&gt;
    /// </summary>
    Material = 1L << 16,
    /// <summary>
    /// &lt;margin&gt;
    /// </summary>
    Margin = 1L << 17,
    /// <summary>
    /// &lt;mark&gt;
    /// </summary>
    Mark = 1L << 18,
    /// <summary>
    /// &lt;mspace&gt;
    /// </summary>
    Monospace = 1L << 19,
    /// <summary>
    /// &lt;nobr&gt;
    /// </summary>
    NoLineBreak = 1L << 20,
    /// <summary>
    /// &lt;noparse&gt;
    /// </summary>
    NoParse = 1L << 21,
    /// <summary>
    /// &lt;page&gt;
    /// </summary>
    PageBreak = 1L << 22,
    /// <summary>
    /// &lt;pos&gt;
    /// </summary>
    Position = 1L << 23,
    /// <summary>
    /// &lt;quad&gt;
    /// </summary>
    Quad = 1L << 24,
    /// <summary>
    /// &lt;rotate&gt;
    /// </summary>
    Rotate = 1L << 25,
    /// <summary>
    /// &lt;s&gt;, &lt;strikethrough&gt;
    /// </summary>
    Strikethrough = 1L << 26,
    /// <summary>
    /// &lt;size&gt;
    /// </summary>
    Size = 1L << 27,
    /// <summary>
    /// &lt;smallcaps&gt;
    /// </summary>
    Smallcaps = 1L << 28,
    /// <summary>
    /// &lt;space&gt;
    /// </summary>
    Space = 1L << 29,
    /// <summary>
    /// &lt;sprite&gt;
    /// </summary>
    Sprite = 1L << 30,
    /// <summary>
    /// &lt;style&gt;
    /// </summary>
    Style = 1L << 31,
    /// <summary>
    /// &lt;sub&gt;
    /// </summary>
    Subscript = 1L << 32,
    /// <summary>
    /// &lt;sup&gt;
    /// </summary>
    Superscript = 1L << 33,
    /// <summary>
    /// &lt;u&gt;, &lt;underline&gt;
    /// </summary>
    Underline = 1L << 34,
    /// <summary>
    /// &lt;voffset&gt;
    /// </summary>
    VerticalOffset = 1L << 35,
    /// <summary>
    /// &lt;width&gt;
    /// </summary>
    TextWidth = 1L << 36,

    /// <summary>
    /// All rich text tags.
    /// </summary>
    All = Align | Alpha | Bold | LineBreak | CharacterSpacing | Font | FontWeight | Gradient | Italic | Indent |
          LineHeight | LineIndent | Link | Lowercase | Material | Margin | Mark | Monospace | NoLineBreak |
          NoParse | PageBreak | Position | Quad | Rotate | Strikethrough | Size | Smallcaps | Space | Sprite |
          Style | Subscript | Superscript | Underline | Uppercase | VerticalOffset | TextWidth
}

public enum FormattingColorType
{
    Keyword,
    Method,
    Property,
    Parameter,
    Class,
    Struct,
    FlowKeyword,
    Interface,
    GenericParameter,
    Enum,
    Namespace,
    Punctuation,
    ExtraData,
    LinesHiddenWarning,
    HtmlBackground
}

internal class LoggerFormatProvider : ITerminalFormatProvider
{
    public StackTraceCleaner StackCleaner => Logger.StackCleaner;
}

internal class CustomTerminalFormatProvider
{
    public StackTraceCleaner StackCleaner { get; }
    public CustomTerminalFormatProvider(StackTraceCleaner stackCleaner)
    {
        StackCleaner = stackCleaner;
    }
}