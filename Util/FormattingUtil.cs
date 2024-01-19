using DevkitServer.API;
using DevkitServer.Multiplayer.Networking;
using HarmonyLib;
using StackCleaner;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;
using Version = System.Version;

namespace DevkitServer.Util;

/// <summary>
/// Methods for formatting strings and termi
/// </summary>
public static class FormattingUtil
{
    private static char[][]? _tags;
    private static RemoveRichTextOptions[]? _tagFlags;
    private static string[]? _sizeCodes;
    private static double[]? _sizeIncrements;
    internal static Func<object, string> FormatSelector = x => x.Format();

    /// <summary>
    /// Provide a custom <see cref="StackTraceCleaner"/> configuration for formatting methods.
    /// </summary>
    public static ITerminalFormatProvider FormatProvider { get; set; } = new LoggerFormatProvider();

    /// <summary>
    /// Regular expression to remove all rich text.
    /// </summary>
    /// <remarks>Does not include &lt;#ffffff&gt; colors.</remarks>
    public static Regex RemoveRichTextRegex { get; } =
        new Regex("""(?<!(?:\<noparse\>(?!\<\/noparse\>)).*)\<\/{0,1}(?:(?:color=\"{0,1}[#a-z]{0,9}\"{0,1})|(?:color)|(?:size=\"{0,1}\d+\"{0,1})|(?:size)|(?:alpha)|(?:alpha=#[0-f]{1,2})|(?:#.{3,8})|(?:[isub])|(?:su[pb])|(?:lowercase)|(?:uppercase)|(?:smallcaps))\>""", RegexOptions.IgnoreCase);

    /// <summary>
    /// Regular expression to remove Text Mesh Pro tags.
    /// </summary>
    public static Regex RemoveTMProRichTextRegex { get; } =
        new Regex("""(?<!(?:\<noparse\>(?!\<\/noparse\>)).*)\<\/{0,1}(?:(?:noparse)|(?:alpha)|(?:alpha=#[0-f]{1,2})|(?:[su])|(?:su[pb])|(?:lowercase)|(?:uppercase)|(?:smallcaps))\>""", RegexOptions.IgnoreCase);
    
    /// <summary>
    /// ANSI escape character for virtual terminal sequences.
    /// </summary>>
    /// <remarks>See <see href="https://learn.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences#text-formatting"/>.</remarks>
    public const char ConsoleEscapeCharacter = '\u001b';

    /// <summary>
    /// Visual ANSI virtual termianl sequence for reseting the foreground color.
    /// </summary>
    public const string ForegroundResetSequence = "\u001b[39m";

    /// <summary>
    /// Visual ANSI virtual termianl sequence for reseting the background color.
    /// </summary>
    public const string BackgroundResetSequence = "\u001b[49m";
    private const int DefaultForeground = -9013642;  // gray
    private const int DefaultBackground = -15987700; // black

    /// <summary>
    /// Remove any rich text tags from Text Mesh Pro.
    /// </summary>
    /// <remarks>Does not include &lt;#ffffff&gt; colors.</remarks>
    [Pure]
    public static string RemoveTMProRichText(string text)
    {
        return RemoveTMProRichTextRegex.Replace(text, string.Empty);
    }

    /// <summary>
    /// Converts a <see cref="ConsoleColor"/> value to a 8-bit foreground color virtual terminal sequence.
    /// </summary>
    /// <remarks>See <see href="https://learn.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences#text-formatting"/>.</remarks>
    public static unsafe string GetForegroundSequenceString(ConsoleColor color, bool background)
    {
        char* chrs = stackalloc char[5];
        SetForegroundSequenceCode(chrs, 0, color, background);
        return new string(chrs, 0, 5);
    }
    internal static unsafe string WrapMessageWithColor(ConsoleColor color, ReadOnlySpan<char> message)
    {
        int l = 5 + message.Length + ForegroundResetSequence.Length;
        char* chrs = stackalloc char[l];
        SetForegroundSequenceCode(chrs, 0, color, false);
        ForegroundResetSequence.AsSpan().CopyTo(new Span<char>(chrs + (l - ForegroundResetSequence.Length), ForegroundResetSequence.Length));
        message.CopyTo(new Span<char>(chrs + 5, l - 5));
        return new string(chrs, 0, l);
    }
    internal static unsafe string WrapMessageWithColor(int argb, ReadOnlySpan<char> message)
    {
        byte r = unchecked((byte)(argb >> 16));
        byte g = unchecked((byte)(argb >> 8));
        byte b = unchecked((byte)argb);
        int l1 = 10 + (r > 9 ? r > 99 ? 3 : 2 : 1) + (g > 9 ? g > 99 ? 3 : 2 : 1) + (b > 9 ? b > 99 ? 3 : 2 : 1);
        int l = l1 + message.Length + ForegroundResetSequence.Length;
        char* chrs = stackalloc char[l];
        SetForegroundSequenceString(chrs, 0, r, g, b, false);
        ForegroundResetSequence.AsSpan().CopyTo(new Span<char>(chrs + (l - ForegroundResetSequence.Length), ForegroundResetSequence.Length));
        message.CopyTo(new Span<char>(chrs + l1, l - l1));
        return new string(chrs, 0, l);
    }
    private static unsafe void SetForegroundSequenceCode(char* data, int index, ConsoleColor color, bool background)
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

    /// <summary>
    /// Converts an ARGB value to an extended foreground color virtual terminal sequence.
    /// </summary>
    /// <remarks>See <see href="https://learn.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences#extended-colors"/>.</remarks>
    public static unsafe string GetForegroundSequenceString(int argb, bool background)
    {
        if (unchecked((byte)(argb >> 24)) == 0) // console color
        {
            ConsoleColor color = (ConsoleColor)argb;
            return GetForegroundSequenceString(color, background);
        }
        byte r = unchecked((byte)(argb >> 16));
        byte g = unchecked((byte)(argb >> 8));
        byte b = unchecked((byte)argb);
        int l = 10 + (r > 9 ? r > 99 ? 3 : 2 : 1) + (g > 9 ? g > 99 ? 3 : 2 : 1) + (b > 9 ? b > 99 ? 3 : 2 : 1);
        char* chrs = stackalloc char[l];
        SetForegroundSequenceString(chrs, 0, r, g, b, background);
        return new string(chrs, 0, l);
    }
    private static unsafe void SetForegroundSequenceString(char* data, int index, byte r, byte g, byte b, bool background)
    {
        // https://learn.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences#extended-colors
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
    /// Faster version of <see cref="string.Format(string, object)"/> that only formats {0}.
    /// </summary>
    /// <param name="input">The string to search for matches in.</param>
    /// <param name="val">The string to replace matches with.</param>
    /// <param name="repeat">Keep looking for {0} after the first one is found?</param>
    public static string QuickFormat(string input, string? val, bool repeat = false) => QuickFormat(input, val, 0, repeat);

    /// <summary>
    /// Faster version of <see cref="string.Format(string, object)"/> that only formats {<paramref name="index"/>}.
    /// </summary>
    /// <param name="input">The string to search for matches in.</param>
    /// <param name="val">The string to replace matches with.</param>
    /// <param name="index">The index of formatting placeholder to match.</param>
    /// <param name="repeat">Keep looking for {<paramref name="index"/>} after the first one is found?</param>
    public static string QuickFormat(string input, string? val, int index, bool repeat = false)
    {
        string srch = "{" + index.ToString(CultureInfo.InvariantCulture) + "}";
        int len = srch.Length;
        int ind = -3;
        do
        {
            ind = input.IndexOf(srch, ind + len, StringComparison.Ordinal);
            if (ind < 0 || ind > input.Length - len)
                break;

            if (string.IsNullOrEmpty(val))
                input = input[..ind] + input.Substring(ind + len, input.Length - ind - len);
            else
                input = input[..ind] + val + input.Substring(ind + len, input.Length - ind - len);

            if (!repeat || ind + len >= input.Length)
                return input;
        }
        while (true);
        return input;
    }

    /// <summary>
    /// Convert a <see cref="Color"/> to ARGB data.
    /// </summary>
    public static int ToArgb(Color color)
    {
        return (byte)Math.Min(255, Mathf.RoundToInt(color.a * 255)) << 24 |
               (byte)Math.Min(255, Mathf.RoundToInt(color.r * 255)) << 16 |
               (byte)Math.Min(255, Mathf.RoundToInt(color.g * 255)) << 8 |
               (byte)Math.Min(255, Mathf.RoundToInt(color.b * 255));
    }

    /// <summary>
    /// Convert to <see cref="ConsoleColor"/> to an int which will be reinterpreted as ARGB later on. This is done by making the alpha value zero.
    /// </summary>
    public static int ToArgbRepresentation(ConsoleColor color) => (int)color;

    /// <summary>
    /// Convert a <see cref="Color32"/> to ARGB data.
    /// </summary>
    public static int ToArgb(Color32 color)
    {
        return color.a << 24 |
               color.r << 16 |
               color.g << 8 |
               color.b;
    }
    
    /// <summary>
    /// Get the closest <see cref="ConsoleColor"/> to the given ARGB data.
    /// </summary>
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
    public static string SpaceProperCaseString(string text, char space = ' ')
    {
        if (text.Length < 1)
            return text;

        if (text.IndexOf('_', 1) != -1 && space != '_')
            text = text.Replace('_', space);

        for (int i = 1; i < text.Length; ++i)
        {
            char current = text[i];

            bool digit = char.IsDigit(current);
            bool upper = char.IsUpper(current);
            if (char.IsWhiteSpace(current) || !digit && !upper || char.IsWhiteSpace(text[i - 1]) ||
                i == text.Length - 1 || char.IsWhiteSpace(text[i + 1]) ||
                digit && char.IsDigit(text[i - 1]) && (i == text.Length - 1 || char.IsDigit(text[i + 1])) ||
                upper && char.IsUpper(text[i - 1]) && (i == text.Length - 1 || char.IsUpper(text[i + 1])))
                continue;

            text = text.Substring(0, i) + space + text.Substring(i, text.Length - i);
            ++i;
        }

        return text;
    }

    /// <summary>
    /// Effeciently removes any virtual terminal sequences from a string and returns the result as a copy.
    /// </summary>
    public static unsafe string RemoveVirtualTerminalSequences(ReadOnlySpan<char> orig)
    {
        if (orig.Length < 5)
            return orig.ToString();
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
            return orig.ToString();

        // regex: \u001B\[[\d;]*m

        int outpInd = 0;
        char* outp = stackalloc char[l - 3];
        fixed (char* chars = orig)
        {
            int lastCpy = -1;
            for (int i = 0; i < l - 2; ++i)
            {
                if (l <= i + 3 || chars[i] != ConsoleEscapeCharacter || chars[i + 1] != '[' || !char.IsDigit(chars[i + 2]))
                    continue;

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
            Buffer.MemoryCopy(chars + lastCpy + 1, outp + outpInd, (l - outpInd) * sizeof(char), (l - lastCpy) * sizeof(char));
            outpInd += l - lastCpy;
        }

        return new string(outp, 0, outpInd - 1);
    }

    [Pure]
    public static string FormatCapacity(long length, int decimals = 1, bool colorize = false)
    {
        _sizeCodes ??=
        [
            "B",
            "KiB",
            "MiB",
            "GiB",
            "TiB",
            "PiB",
            "EiB"
        ];

        if (_sizeIncrements == null)
        {
            _sizeIncrements = new double[_sizeCodes.Length];
            for (int i = 0; i < _sizeCodes.Length; ++i)
                _sizeIncrements[i] = Math.Pow(1024, i);
        }

        string numStr;
        if (length == 0)
        {
            numStr = 0.ToString("N" + Math.Max(0, decimals).ToString(CultureInfo.CurrentCulture));
            return colorize ? numStr.Colorize(FormattingColorType.Number) : numStr;
        }

        bool neg = length < 0;
        length = Math.Abs(length);

        double incr = Math.Log(length, 1024);
        int inc;
        if (incr % 1 > 0.8)
            inc = (int)Math.Ceiling(incr);
        else
            inc = (int)Math.Floor(incr);

        if (inc >= _sizeIncrements.Length)
            inc = _sizeIncrements.Length - 1;

        double len = length / _sizeIncrements[inc];
        if (neg) len = -len;

        numStr = len.ToString("N" + Math.Max(0, decimals).ToString(CultureInfo.CurrentCulture));
        return (colorize ? numStr.ColorizeNoReset(FormattingColorType.Number) : numStr) + " " + (colorize ? _sizeCodes[inc].Colorize(FormattingColorType.Struct) : _sizeCodes[inc]);
    }

    /// <summary>
    /// Formats a field for easier viewing in a console.
    /// </summary>
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

    /// <summary>
    /// Formats a property for easier viewing in a console.
    /// </summary>
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
            type += FormatProvider.StackCleaner.GetString(property.DeclaringType) + ".";
        else
            type += "global".ColorizeNoReset(FormatProvider.StackCleaner.Configuration.Colors!.KeywordColor) +
                    "::".ColorizeNoReset(FormatProvider.StackCleaner.Configuration.Colors!.PunctuationColor);

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
                type += " protected internal set".ColorizeNoReset(FormatProvider.
                    StackCleaner.Configuration.Colors!.KeywordColor);
            else if (setter.IsPrivate)
                type += " private set".ColorizeNoReset(FormatProvider.StackCleaner.Configuration.Colors!.KeywordColor);
            else if (setter.IsFamily)
                type += " protected set".ColorizeNoReset(FormatProvider.StackCleaner.Configuration.Colors!.KeywordColor);
            else
                type += " set".ColorizeNoReset(FormatProvider.StackCleaner.Configuration.Colors!.KeywordColor);
            type += ";".ColorizeNoReset(FormatProvider.StackCleaner.Configuration.Colors!.PunctuationColor);
        }

        type += " }";

        return type + GetResetSuffix();
    }

    /// <summary>
    /// Formats a method for easier viewing in a console.
    /// </summary>
    public static string Format(this MethodBase? method) => method == null ? ((object)null!).Format() : FormatProvider.StackCleaner.GetString(method);

    /// <summary>
    /// Formats a described method (with a passed delegate type) for easier viewing in a console.
    /// </summary>
    public static string FormatMethod(Type delegateType, string name, bool removeInstance = false, bool isStatic = false, bool isAsync = false, bool isGetter = false, bool isSetter = false, bool isIndexer = false, Type? declTypeOverride = null)
    {
        Accessor.GetDelegateSignature(delegateType, out Type returnType, out ParameterInfo[] parameters);
        (Type type, string? name)[] typeParameters = new (Type, string?)[removeInstance && parameters.Length > 0 ? parameters.Length - 1 : parameters.Length];
        for (int i = 0; i < parameters.Length; ++i)
            typeParameters[i] = (parameters[removeInstance ? i + 1 : i].ParameterType, parameters[removeInstance ? i + 1 : i].Name);
        
        return FormatMethod(returnType, declTypeOverride ?? (removeInstance && parameters.Length > 0 ? parameters[0].ParameterType : null), name, typeParameters, null, null, isStatic, isAsync, isGetter, isSetter, isIndexer);
    }

    /// <summary>
    /// Formats a described method (with a passed delegate type) for easier viewing in a console.
    /// </summary>
    public static string FormatMethod<TDelegate>(string name, bool removeInstance = false, bool isStatic = false, bool isAsync = false, bool isGetter = false, bool isSetter = false, bool isIndexer = false, Type? declTypeOverride = null) where TDelegate : Delegate
    {
        ParameterInfo[] parameters = Accessor.GetParameters<TDelegate>();
        Type returnType = Accessor.GetReturnType<TDelegate>();
        (Type type, string? name)[] typeParameters = new (Type, string?)[removeInstance && parameters.Length > 0 ? parameters.Length - 1 : parameters.Length];
        for (int i = 0; i < parameters.Length; ++i)
            typeParameters[i] = (parameters[removeInstance ? i + 1 : i].ParameterType, parameters[removeInstance ? i + 1 : i].Name);
        
        return FormatMethod(returnType, declTypeOverride ?? (removeInstance && parameters.Length > 0 ? parameters[0].ParameterType : null), name, typeParameters, null, null, isStatic, isAsync, isGetter, isSetter, isIndexer);
    }

    /// <summary>
    /// Formats a described method for easier viewing in a console.
    /// </summary>
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
                        sb.Append(" " + paramName.Colorize(FormatProvider.StackCleaner.Configuration.Colors!.ParameterColor));
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

    /// <summary>
    /// Formats a type for easier viewing in a console.
    /// </summary>
    public static string Format(this Type? typedef) => typedef == null ? ((object)null!).Format() : FormatProvider.StackCleaner.GetString(typedef);

    /// <summary>
    /// Formats a Harmony exception block for easier viewing in a console.
    /// </summary>
    public static string Format(this ExceptionBlock? block)
    {
        if (block == null)
            return ((object)null!).Format();
        switch (block.blockType)
        {
            case ExceptionBlockType.BeginExceptionBlock:
                return GetColorPrefix(FormatProvider.StackCleaner.Configuration.Colors!.FlowKeywordColor) + "try" + GetResetSuffix() + " {";
            case ExceptionBlockType.EndExceptionBlock:
                return "} " + GetColorPrefix(FormatProvider.StackCleaner.Configuration.Colors!.PunctuationColor) + "// end exception block" + GetResetSuffix();
            case ExceptionBlockType.BeginExceptFilterBlock:
                return GetColorPrefix(FormatProvider.StackCleaner.Configuration.Colors!.FlowKeywordColor) + " when" + GetResetSuffix() + " {";
            case ExceptionBlockType.BeginCatchBlock:
                string str = "} " + GetColorPrefix(FormatProvider.StackCleaner.Configuration.Colors!.FlowKeywordColor) + "catch" + GetResetSuffix();
                if (block.catchType != null)
                    str += " (" + GetColorPrefix(FormatProvider.StackCleaner.Configuration.Colors!.FlowKeywordColor) + block.catchType.Format() + GetResetSuffix() + ")";
                return str + "{";
            case ExceptionBlockType.BeginFinallyBlock:
                return "} " + GetColorPrefix(FormatProvider.StackCleaner.Configuration.Colors!.FlowKeywordColor) + "finally" + GetResetSuffix() + " {";
            case ExceptionBlockType.BeginFaultBlock:
                return "} " + GetColorPrefix(FormatProvider.StackCleaner.Configuration.Colors!.FlowKeywordColor) + "fault" + GetResetSuffix() + " {";
        }

        return "} " + GetColorPrefix(FormatProvider.StackCleaner.Configuration.Colors!.FlowKeywordColor) + block.blockType + GetResetSuffix() + " {";
    }

    /// <summary>
    /// Formats a label for easier viewing in a console.
    /// </summary>
    public static string Format(this Label label) => (FormatProvider.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.None
                                                         ? string.Empty
                                                         : (FormatProvider.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.ExtendedANSIColor
                                                             ? GetForegroundSequenceString(FormatProvider.StackCleaner.Configuration.Colors!.StructColor, false)
                                                             : GetForegroundSequenceString(ToConsoleColor(FormatProvider.StackCleaner.Configuration.Colors!.StructColor), false))) + "Label #" + label.GetLabelId() +
                                                     GetResetSuffix();

    /// <summary>
    /// Formats a Harmony code instruction for easier viewing in a console.
    /// </summary>
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
                    op += " " + GetColorPrefix(FormatProvider.StackCleaner.Configuration.Colors!.ExtraDataColor) + num + GetResetSuffix();
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
                    op += " " + GetColorPrefix(FormatProvider.StackCleaner.Configuration.Colors!.ExtraDataColor) + lng + GetResetSuffix();
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
                    op += " " + GetColorPrefix(FormatProvider.StackCleaner.Configuration.Colors!.ExtraDataColor) + dbl + GetResetSuffix();
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
                    op += " " + GetColorPrefix(FormatProvider.StackCleaner.Configuration.Colors!.ExtraDataColor) + num + GetResetSuffix();
                }
                catch
                {
                    // ignored
                }
                break;
            case OperandType.InlineString:
                if (instruction.operand is string str)
                    op += " " + GetColorPrefix(ToArgb(new Color32(214, 157, 133, 255))) + "\"" + str + "\"" + GetResetSuffix();
                break;
            case OperandType.InlineSwitch:
                if (instruction.operand is Label[] jumps)
                {
                    op += Environment.NewLine + "{";
                    for (int i = 0; i < jumps.Length; ++i)
                        op += Environment.NewLine + "  " + GetColorPrefix(FormatProvider.StackCleaner.Configuration.Colors!.ExtraDataColor) + i + GetResetSuffix() + " => " + GetColorPrefix(FormatProvider.StackCleaner.Configuration.Colors!.StructColor) + " Label #" + jumps[i].GetLabelId() + GetResetSuffix();

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
                    op += " " + GetColorPrefix(FormatProvider.StackCleaner.Configuration.Colors!.ExtraDataColor) + lb.LocalIndex + GetResetSuffix() + " : " + lb.LocalType!.Format();
                else if (instruction.operand is int index)
                    op += " " + GetColorPrefix(FormatProvider.StackCleaner.Configuration.Colors!.ExtraDataColor) + index + GetResetSuffix();
                break;
        }

        foreach (Label lbl in instruction.labels)
        {
            op += " .lbl #".Colorize(ConsoleColor.DarkRed) + lbl.GetLabelId().Format();
        }


        return op;
    }

    /// <summary>
    /// Formats an IL opcode for easier viewing in a console.
    /// </summary>
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
                clr = GetForegroundSequenceString(argb, false);
            else
                clr = GetForegroundSequenceString(ToConsoleColor(argb), false);
        }

        if (clr != null)
            return clr + instruction.Name + ForegroundResetSequence;

        return instruction.Name;
    }

    /// <summary>
    /// Default string color during formatting.
    /// </summary>
    public static Color32 StringColor = new Color32(214, 157, 133, 255);

    /// <summary>
    /// Default number color during formatting.
    /// </summary>
    public static Color32 NumberColor = new Color32(181, 206, 168, 255);

    /// <summary>
    /// Colors a string and optionally adds quotes.
    /// </summary>
    public static string Format(this string? str, bool quotes)
    {
        if (str == null) return ((object?)null).Format();
        if (FormatProvider.StackCleaner.Configuration.ColorFormatting != StackColorFormatType.None)
        {
            string clr = FormatProvider.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.ExtendedANSIColor
                ? GetForegroundSequenceString(ToArgb(StringColor), false)
                : GetForegroundSequenceString(ToConsoleColor(ToArgb(StringColor)), false);

            if (quotes)
                return clr + "\"" + str + "\"" + ForegroundResetSequence;
            
            return clr + str + ForegroundResetSequence;
        }

        return str;
    }

    /// <summary>
    /// Formats any object for easier viewing in the console. Most common types have custom formatting.
    /// </summary>
    /// <remarks>Any objects implementing <see cref="ITerminalFormattable"/> will use their custom format implementation.</remarks>
    public static string Format(this object? obj, string? format = null)
    {
        if (obj == null || obj.Equals(null))
        {
            if (FormatProvider.StackCleaner.Configuration.ColorFormatting != StackColorFormatType.None)
                return GetColorPrefix(FormatProvider.StackCleaner.Configuration.Colors!.KeywordColor) + "null" + ForegroundResetSequence;
            return "null";
        }

        if (obj is Vector2 v2)
        {
            return v2.ToString(format).Colorize(FormattingColorType.Struct);
        }
        if (obj is Vector3 v3)
        {
            return v3.ToString(format).Colorize(FormattingColorType.Struct);
        }
        if (obj is Vector3 v4)
        {
            return v4.ToString(format).Colorize(FormattingColorType.Struct);
        }
        if (obj is Quaternion q)
        {
            return q.ToString(format).Colorize(FormattingColorType.Struct);
        }

        if (obj is IDevkitServerPlugin plugin)
        {
            if (FormatProvider.StackCleaner.Configuration.ColorFormatting != StackColorFormatType.None)
                return GetColorPrefix(ToArgb(plugin is IDevkitServerColorPlugin p ? p.Color : Plugin.DefaultColor)) + plugin.Name + ForegroundResetSequence;
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
            if (DevkitServerModule.IsMainThread && Assets.find(assetReference.GUID) is { } asset2)
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

        if (obj is SteamPlayer pl)
        {
            return GetColorPrefix(ToArgb(new Color32(102, 192, 244, 255))) + pl.playerID.steamID.m_SteamID.ToString("D17") + " " + pl.playerID.characterName.Format(false);
        }
        if (obj is Player pl2)
        {
            return GetColorPrefix(ToArgb(new Color32(102, 192, 244, 255))) + pl2.channel.owner.playerID.steamID.m_SteamID.ToString("D17") +
                   " " + pl2.channel.owner.playerID.characterName.Format(false);
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

            return GetColorPrefix(ToArgb(new Color32(102, 192, 244, 255))) + s64.ToString("D17") + ForegroundResetSequence;
        }
        if (obj is CSteamID cs64 && cs64.UserSteam64())
        {
            if (FormatProvider.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.None)
                return cs64.m_SteamID.ToString("D17");

            return GetColorPrefix(ToArgb(new Color32(102, 192, 244, 255))) + cs64.m_SteamID.ToString("D17") + ForegroundResetSequence;
        }
        if (obj is ITransportConnection connection)
        {
            if (FormatProvider.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.None)
                return connection.GetAddressString(true);

            if (connection.TryGetIPv4Address(out uint addr))
            {
                if (addr == 0 && connection.GetAddress() is { } ipAddress)
                {
                    str = GetColorPrefix(ToArgb(new Color32(204, 255, 102, 255)))
                          + ipAddress;
                }
                else
                {
                    str = GetColorPrefix(ToArgb(new Color32(204, 255, 102, 255)))
                          + Parser.getIPFromUInt32(addr);

                    if (connection.TryGetPort(out ushort port))
                    {
                        str += GetColorPrefix(FormatProvider.StackCleaner.Configuration.Colors!.PunctuationColor) + ":" +
                               GetColorPrefix(ToArgb(new Color32(170, 255, 0, 255))) +
                               port.ToString(CultureInfo.InvariantCulture);
                    }
                }

                return str + ForegroundResetSequence;
            }

            return GetColorPrefix(ToArgb(new Color32(204, 255, 102, 255))) + (connection.GetAddressString(true) ?? "<unknown address>") + ForegroundResetSequence;
        }

        if (obj is IClientTransport)
        {
            return "server".Colorize(new Color32(204, 255, 102, 255));
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
                return GetColorPrefix(ToArgb(new Color32(214, 157, 133, 255))) + "\"" + str + "\"" + ForegroundResetSequence;

            if (type.IsEnum)
                return GetColorPrefix(FormatProvider.StackCleaner.Configuration.Colors!.EnumColor) + str + ForegroundResetSequence;

            if (type.IsPrimitive)
            {
                if (obj is bool)
                    return GetColorPrefix(FormatProvider.StackCleaner.Configuration.Colors!.KeywordColor) + str + ForegroundResetSequence;
                
                if (format != null)
                {
                    switch (obj)
                    {
                        case float n:
                            return GetColorPrefix(ToArgb(NumberColor)) + n.ToString(format) + ForegroundResetSequence;
                        case double n:
                            return GetColorPrefix(ToArgb(NumberColor)) + n.ToString(format) + ForegroundResetSequence;
                        case decimal n:
                            return GetColorPrefix(ToArgb(NumberColor)) + n.ToString(format) + ForegroundResetSequence;
                        case int n:
                            return GetColorPrefix(ToArgb(NumberColor)) + n.ToString(format) + ForegroundResetSequence;
                        case uint n:
                            return GetColorPrefix(ToArgb(NumberColor)) + n.ToString(format) + ForegroundResetSequence;
                        case short n:
                            return GetColorPrefix(ToArgb(NumberColor)) + n.ToString(format) + ForegroundResetSequence;
                        case ushort n:
                            return GetColorPrefix(ToArgb(NumberColor)) + n.ToString(format) + ForegroundResetSequence;
                        case sbyte n:
                            return GetColorPrefix(ToArgb(NumberColor)) + n.ToString(format) + ForegroundResetSequence;
                        case byte n:
                            return GetColorPrefix(ToArgb(NumberColor)) + n.ToString(format) + ForegroundResetSequence;
                        case long n:
                            return GetColorPrefix(ToArgb(NumberColor)) + n.ToString(format) + ForegroundResetSequence;
                        case ulong n:
                            return GetColorPrefix(ToArgb(NumberColor)) + n.ToString(format) + ForegroundResetSequence;
                    }
                }


                return GetColorPrefix(ToArgb(NumberColor)) + str + ForegroundResetSequence;
            }

            if (type.IsInterface)
                return GetColorPrefix(FormatProvider.StackCleaner.Configuration.Colors!.InterfaceColor) + str + ForegroundResetSequence;

            if (type.IsValueType)
                return GetColorPrefix(FormatProvider.StackCleaner.Configuration.Colors!.StructColor) + str + ForegroundResetSequence;

            if (type.IsArray)
            {
                return (type.GetElementType().Format() + "[".Colorize(FormattingColorType.Punctuation) +
                        ((Array)obj).Length.ToString(CultureInfo.InvariantCulture).Colorize(NumberColor) +
                        "]".Colorize(FormattingColorType.Punctuation));
            }

            return GetColorPrefix(FormatProvider.StackCleaner.Configuration.Colors!.ClassColor) + str + ForegroundResetSequence;
        }

        return str;
    }

    /// <summary>
    /// Colorize a string with an 8-bit color. Appends a reset sequence afterwards.
    /// </summary>
    public static string Colorize(this string str, ConsoleColor color)
    {
        if (FormatProvider.StackCleaner.Configuration.ColorFormatting != StackColorFormatType.None)
        {
            string ansiString = GetForegroundSequenceString(color, false);
            return ansiString + str.Replace(ForegroundResetSequence, ansiString) + ForegroundResetSequence;
        }

        return str;
    }

    /// <summary>
    /// Colorize a string with a 24-bit RGB color. Appends a reset sequence afterwards.
    /// </summary>
    public static string Colorize(this string str, Color color)
    {
        if (FormatProvider.StackCleaner.Configuration.ColorFormatting != StackColorFormatType.None)
        {
            string ansiString = (FormatProvider.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.ExtendedANSIColor
                ? GetForegroundSequenceString(ToArgb(color), false)
                : GetForegroundSequenceString(ToConsoleColor(ToArgb(color)), false));
            return ansiString + str.Replace(ForegroundResetSequence, ansiString) + ForegroundResetSequence;
        }

        return str;
    }

    /// <summary>
    /// Colorize a string with a 24-bit RGB color. Appends a reset sequence afterwards.
    /// </summary>
    public static string Colorize(this string str, Color32 color)
    {
        if (FormatProvider.StackCleaner.Configuration.ColorFormatting != StackColorFormatType.None)
        {
            string ansiString = (FormatProvider.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.ExtendedANSIColor
                ? GetForegroundSequenceString(ToArgb(color), false)
                : GetForegroundSequenceString(ToConsoleColor(ToArgb(color)), false));
            return ansiString + str.Replace(ForegroundResetSequence, ansiString) + ForegroundResetSequence;
        }

        return str;
    }

    /// <summary>
    /// Colorize a string with a 24-bit RGB color in ARGB format. Appends a reset sequence afterwards.
    /// </summary>
    public static string Colorize(this string str, int argb)
    {
        if (FormatProvider.StackCleaner.Configuration.ColorFormatting != StackColorFormatType.None)
        {
            string ansiString = (FormatProvider.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.ExtendedANSIColor
                ? GetForegroundSequenceString(argb, false)
                : GetForegroundSequenceString(ToConsoleColor(argb), false));
            return ansiString + str.Replace(ForegroundResetSequence, ansiString) + ForegroundResetSequence;
        }

        return str;
    }

    /// <summary>
    /// Colorize a string based on a defined token type. Appends a reset sequence afterwards.
    /// </summary>
    public static string Colorize(this string str, FormattingColorType tokenType)
    {
        if (FormatProvider.StackCleaner.Configuration.ColorFormatting != StackColorFormatType.None)
            return ColorizeNoReset(str, tokenType) + ForegroundResetSequence;
        return str;
    }
    private static int ToArgb(FormattingColorType tokenType)
    {
        ColorConfig colors = FormatProvider.StackCleaner.Configuration.Colors ?? Color4Config.Default;
        return tokenType switch
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
            FormattingColorType.Number => ToArgb(NumberColor),
            FormattingColorType.String => ToArgb(StringColor),
            _ => unchecked((int)0xFFFFFFFF)
        };
    }

    /// <summary>
    /// Colorize a string based on a defined token type. Does not append a reset sequence afterwards.
    /// </summary>
    public static string ColorizeNoReset(this string str, FormattingColorType tokenType)
    {
        StackCleanerConfiguration config = FormatProvider.StackCleaner.Configuration;
        if (config.ColorFormatting != StackColorFormatType.None)
        {
            int argb = ToArgb(tokenType);

            string ansiString = (config.ColorFormatting == StackColorFormatType.ExtendedANSIColor
                ? GetForegroundSequenceString(argb, false)
                : GetForegroundSequenceString(ToConsoleColor(argb), false));
            return ansiString + str.Replace(ForegroundResetSequence, ansiString) + ForegroundResetSequence;
        }

        return str;
    }

    /// <summary>
    /// Colorize a string with an 8-bit color. Does not append a reset sequence afterwards.
    /// </summary>
    public static string ColorizeNoReset(this string str, ConsoleColor color)
    {
        if (FormatProvider.StackCleaner.Configuration.ColorFormatting != StackColorFormatType.None)
        {
            string ansiString = GetForegroundSequenceString(color, false);
            return ansiString + str.Replace(ForegroundResetSequence, ansiString);
        }

        return str;
    }

    /// <summary>
    /// Colorize a string with an 24-bit RGB color. Does not append a reset sequence afterwards.
    /// </summary>
    public static string ColorizeNoReset(this string str, Color color)
    {
        if (FormatProvider.StackCleaner.Configuration.ColorFormatting != StackColorFormatType.None)
        {
            string ansiString = FormatProvider.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.ExtendedANSIColor
                ? GetForegroundSequenceString(ToArgb(color), false)
                : GetForegroundSequenceString(ToConsoleColor(ToArgb(color)), false);
            return ansiString + str.Replace(ForegroundResetSequence, ansiString);
        }

        return str;
    }

    /// <summary>
    /// Colorize a string with an 24-bit RGB color. Does not append a reset sequence afterwards.
    /// </summary>
    public static string ColorizeNoReset(this string str, Color32 color)
    {
        if (FormatProvider.StackCleaner.Configuration.ColorFormatting != StackColorFormatType.None)
        {
            string ansiString = FormatProvider.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.ExtendedANSIColor
                ? GetForegroundSequenceString(ToArgb(color), false)
                : GetForegroundSequenceString(ToConsoleColor(ToArgb(color)), false);
            return ansiString + str.Replace(ForegroundResetSequence, ansiString);
        }

        return str;
    }

    /// <summary>
    /// Colorize a string with an 24-bit RGB color in ARGB format. Does not append a reset sequence afterwards.
    /// </summary>
    public static string ColorizeNoReset(this string str, int argb)
    {
        if (FormatProvider.StackCleaner.Configuration.ColorFormatting != StackColorFormatType.None)
        {
            string ansiString = FormatProvider.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.ExtendedANSIColor
                ? GetForegroundSequenceString(argb, false)
                : GetForegroundSequenceString(ToConsoleColor(argb), false);
            return ansiString + str.Replace(ForegroundResetSequence, ansiString) + ForegroundResetSequence;
        }

        return str;
    }

    /// <summary>
    /// Construct a color prefix based on the current settings and an ARGB color.
    /// </summary>
    public static string GetColorPrefix(int argb)
    {
        return FormatProvider.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.None
            ? string.Empty
            : (FormatProvider.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.ExtendedANSIColor
                ? GetForegroundSequenceString(argb, false)
                : GetForegroundSequenceString(ToConsoleColor(argb), false));
    }

    /// <summary>
    /// Construct a color prefix based on the current settings and an 8-bit color.
    /// </summary>
    public static string GetColorPrefix(ConsoleColor color)
    {
        return FormatProvider.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.None
            ? string.Empty
            : GetForegroundSequenceString(color, false);
    }

    /// <summary>
    /// Construct a color prefix based on the current settings and a 24-bit RGB color.
    /// </summary>
    public static string GetColorPrefix(Color color) => GetColorPrefix(ToArgb(color));

    /// <summary>
    /// Construct a color prefix based on the current settings and a 24-bit RGB color.
    /// </summary>
    public static string GetColorPrefix(Color32 color) => GetColorPrefix(ToArgb(color));

    /// <summary>
    /// Construct a color prefix based on the current settings and a defined token type.
    /// </summary>
    public static string GetColorPrefix(FormattingColorType color) => GetColorPrefix(ToArgb(color));

    /// <summary>
    /// Construct a reset suffix based on the current settings.
    /// </summary>
    public static string GetResetSuffix() => FormatProvider.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.None ? string.Empty : ForegroundResetSequence;
    
    /// <summary>
    /// Format the given bytes in hexadecimal (base 16) columns.
    /// </summary>
    /// <param name="formatMessageOverhead">Should the data be colorized as a <see cref="MessageOverhead"/>?</param>
    [Pure]
    public static string GetBytesHex(byte[] bytes, int columnCount = 64, int offset = 0, int len = -1, bool formatMessageOverhead = false)
    {
        return BytesToString(bytes, columnCount, offset, len, "X2", formatMessageOverhead);
    }

    /// <summary>
    /// Format the given bytes in decimal (base 10) columns.
    /// </summary>
    /// <param name="formatMessageOverhead">Should the data be colorized as a <see cref="MessageOverhead"/>?</param>
    [Pure]
    public static string GetBytesDec(byte[] bytes, int columnCount = 64, int offset = 0, int len = -1, bool formatMessageOverhead = false)
    {
        return BytesToString(bytes, columnCount, offset, len, "000", formatMessageOverhead);
    }

    /// <summary>
    /// Format the given bytes in hexadecimal (base 16) columns.
    /// </summary>
    /// <param name="formatMessageOverhead">Should the data be colorized as a <see cref="MessageOverhead"/>?</param>
    [Pure]
    public static unsafe string GetBytesHex(byte* bytes, int len, int columnCount = 64, int offset = 0, bool formatMessageOverhead = false)
    {
        return BytesToString(bytes, columnCount, offset, len, "X2", formatMessageOverhead);
    }

    /// <summary>
    /// Format the given bytes in decimal (base 10) columns.
    /// </summary>
    /// <param name="formatMessageOverhead">Should the data be colorized as a <see cref="MessageOverhead"/>?</param>
    [Pure]
    public static unsafe string GetBytesDec(byte* bytes, int len, int columnCount = 64, int offset = 0, bool formatMessageOverhead = false)
    {
        return BytesToString(bytes, columnCount, offset, len, "000", formatMessageOverhead);
    }

    /// <summary>
    /// Format the given bytes in the given <paramref name="fmt"/> into columns. The format should return a fixed length string.
    /// </summary>
    /// <param name="formatMessageOverhead">Should the data be colorized as a <see cref="MessageOverhead"/>?</param>
    [Pure]
    public static unsafe string BytesToString(byte[] bytes, int columnCount, int offset, int len, string fmt, bool formatMessageOverhead = false)
    {
        if (offset >= bytes.Length)
            offset = bytes.Length - 1;
        if (len < 0 || len + offset < 0 || len + offset > bytes.Length)
            len = bytes.Length - offset;
        fixed (byte* ptr = &bytes[offset])
            return BytesToString(ptr, columnCount, offset, len, fmt, formatMessageOverhead);
    }
    
    /// <summary>
    /// The color of <see cref="MessageOverhead.Flags"/> in the byte formatting methods.
    /// </summary>
    public static Color32 MessageOverheadFlag    = new Color32(0,   255, 255, 255);

    /// <summary>
    /// The color of <see cref="MessageOverhead.MessageId"/> in the byte formatting methods.
    /// </summary>
    public static Color32 MessageOverheadId      = new Color32(186, 222, 192, 255);

    /// <summary>
    /// The color of <see cref="MessageOverhead.Size"/> in the byte formatting methods.
    /// </summary>
    public static Color32 MessageOverheadSize    = new Color32(102, 255, 153, 255);

    /// <summary>
    /// The color of <see cref="MessageOverhead.RequestKey"/> in the byte formatting methods.
    /// </summary>
    public static Color32 MessageOverheadReqKey  = new Color32(75,  155, 88,  255);

    /// <summary>
    /// The color of <see cref="MessageOverhead.ResponseKey"/> in the byte formatting methods.
    /// </summary>
    public static Color32 MessageOverheadRespKey = new Color32(51,  255, 119, 255);

    /// <summary>
    /// Format the given bytes in the given <paramref name="fmt"/> into columns. The format should return a fixed length string.
    /// </summary>
    /// <param name="formatMessageOverhead">Should the data be colorized as a <see cref="MessageOverhead"/>?</param>
    [Pure]
    public static unsafe string BytesToString(byte* bytes, int columnCount, int offset, int len, string fmt, bool formatMessageOverhead = false)
    {
        if (offset >= len)
            offset = len - 1;
        StringBuilder sb = new StringBuilder(len * 4);
        MessageOverhead ovh = default;
        int reset = -1;
        for (int i = 0; i < len; ++i)
        {
            if (i != 0 && i % columnCount == 0)
                sb.Append(Environment.NewLine);
            else if (i != 0)
                sb.Append(' ');
            if (formatMessageOverhead && i <= 41)
            {
                if (reset == i)
                {
                    sb.Append(GetResetSuffix());
                    reset = -1;
                }

                switch (i)
                {
                    case 0:
                        ovh = new MessageOverhead(bytes + offset);
                        sb.Append(GetColorPrefix(MessageOverheadFlag));
                        reset = 1;
                        break;

                    // uint16
                    case 1:
                        sb.Append(GetColorPrefix(MessageOverheadId));
                        reset = (ovh.Flags & MessageFlags.Guid) == 0 ? 17 : 3;
                        break;
                    case 3 when (ovh.Flags & MessageFlags.Guid) == 0:
                        sb.Append(GetColorPrefix(MessageOverheadSize));
                        reset = 7;
                        break;
                    case 7 when (ovh.Flags & MessageFlags.Guid) == 0 && (ovh.Flags & MessageOverhead.RequestKeyMask) != 0:
                        sb.Append(GetColorPrefix(MessageOverheadReqKey));
                        reset = 15;
                        break;
                    case 7 when ((ovh.Flags | MessageOverhead.RequestKeyMask) & MessageFlags.Guid) == 0 && (ovh.Flags & MessageOverhead.ResponseKeyMask) != 0:
                        sb.Append(GetColorPrefix(MessageOverheadRespKey));
                        reset = 15;
                        break;
                    case 15 when (ovh.Flags & MessageFlags.Guid) == 0 && (ovh.Flags & MessageOverhead.RequestKeyMask) != 0 && (ovh.Flags & MessageOverhead.ResponseKeyMask) != 0:
                        sb.Append(GetColorPrefix(MessageOverheadRespKey));
                        reset = 23;
                        break;

                    // guid
                    case 17 when (ovh.Flags & MessageFlags.Guid) != 0:
                        sb.Append(GetColorPrefix(MessageOverheadSize));
                        reset = 21;
                        break;
                    case 21 when (ovh.Flags & MessageFlags.Guid) != 0:
                        sb.Append(GetColorPrefix(MessageOverheadSize));
                        reset = 25;
                        break;
                    case 25 when (ovh.Flags & MessageFlags.Guid) != 0 && (ovh.Flags & MessageOverhead.RequestKeyMask) != 0:
                        sb.Append(GetColorPrefix(MessageOverheadReqKey));
                        reset = 33;
                        break;
                    case 25 when (ovh.Flags & MessageFlags.Guid) != 0 && (ovh.Flags & MessageOverhead.RequestKeyMask) == 0 && (ovh.Flags & MessageOverhead.ResponseKeyMask) != 0:
                        sb.Append(GetColorPrefix(MessageOverheadRespKey));
                        reset = 33;
                        break;
                    case 33 when (ovh.Flags & MessageFlags.Guid) == 0 && (ovh.Flags & MessageOverhead.RequestKeyMask) != 0 && (ovh.Flags & MessageOverhead.ResponseKeyMask) != 0:
                        sb.Append(GetColorPrefix(MessageOverheadRespKey));
                        reset = 41;
                        break;
                }
            }
            sb.Append(bytes[i + offset].ToString(fmt));
        }

        if (reset != -1)
            sb.Append(GetResetSuffix());
        return sb.ToString();
    }

    /// <summary>
    /// Remove rich text tags from text, and replace &lt;color&gt; and &lt;mark&gt; tags with virtual terminal sequences (depending on the current configuration).
    /// </summary>
    /// <param name="options">Tags to check for and remove.</param>
    /// <param name="argbForeground">Color to reset the foreground to.</param>
    /// <param name="argbBackground">Color to reset the background to.</param>
    /// <exception cref="ArgumentOutOfRangeException"/>
    [Pure]
    public static unsafe string ConvertRichTextToVirtualTerminalSequences(string str, int index = 0, int length = -1, RemoveRichTextOptions options = RemoveRichTextOptions.All, int argbForeground = DefaultForeground, int argbBackground = DefaultBackground)
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
            AppendDefaults(nonDefaults, ref writeIndex, ref rtn, argbBackground, argbForeground, format);

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
                            AppendDefaults(nonDefaults, ref writeIndex, ref rtn, argbBackground, argbForeground, format);
                        }
                    }
                }
            }
            Append(ref rtn, ptr + nextCopyStartIndex, writeIndex, str.Length - nextCopyStartIndex);
            writeIndex += str.Length - nextCopyStartIndex;
            if (useColor)
                AppendDefaults(nonDefaults, ref writeIndex, ref rtn, argbBackground, argbForeground, format);
        }

        return new string(rtn, 0, writeIndex);

        static void AppendDefaults(int nonDefaults, ref int writeIndex, ref char[] rtn, int argbBackground, int argbForeground, StackColorFormatType format)
        {
            if ((nonDefaults & 2) != 0)
            {
                if (argbBackground == DefaultBackground)
                {
                    fixed (char* ptr2 = BackgroundResetSequence)
                        Append(ref rtn, ptr2, writeIndex, BackgroundResetSequence.Length);
                    writeIndex += BackgroundResetSequence.Length;
                }
                else if (format == StackColorFormatType.ExtendedANSIColor)
                    writeIndex += AppendExtANSIForegroundCode(ref rtn, writeIndex, (byte)(argbBackground >> 16), (byte)(argbBackground >> 8), (byte)argbBackground, true);
                else
                {
                    ConsoleColor consoleColor = ToConsoleColor(argbBackground);
                    if (consoleColor == ConsoleColor.Black)
                    {
                        fixed (char* ptr2 = BackgroundResetSequence)
                            Append(ref rtn, ptr2, writeIndex, BackgroundResetSequence.Length);
                        writeIndex += BackgroundResetSequence.Length;
                    }
                    else
                        writeIndex += AppendANSIForegroundCode(ref rtn, writeIndex, consoleColor, true);
                }
            }

            if ((nonDefaults & 1) != 0)
            {
                if (argbForeground == DefaultForeground)
                {
                    fixed (char* ptr2 = ForegroundResetSequence)
                        Append(ref rtn, ptr2, writeIndex, ForegroundResetSequence.Length);
                    writeIndex += ForegroundResetSequence.Length;
                }
                else if (format == StackColorFormatType.ExtendedANSIColor)
                    writeIndex += AppendExtANSIForegroundCode(ref rtn, writeIndex, (byte)(argbForeground >> 16), (byte)(argbForeground >> 8), (byte)argbForeground, false);
                else
                {
                    ConsoleColor consoleColor = ToConsoleColor(argbForeground);
                    if (consoleColor == ConsoleColor.Gray)
                    {
                        fixed (char* ptr2 = ForegroundResetSequence)
                            Append(ref rtn, ptr2, writeIndex, ForegroundResetSequence.Length);
                        writeIndex += ForegroundResetSequence.Length;
                    }
                    else
                        writeIndex += AppendANSIForegroundCode(ref rtn, writeIndex, consoleColor, false);
                }
            }

            nonDefaults = 0;
        }
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
        _tags ??=
        [
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
        ];
        _tagFlags ??=
        [
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
        ];
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
        SetForegroundSequenceCode(ptr, 0, color, background);
        Append(ref data, ptr, index, 5);
        return 5;
    }
    private static unsafe int AppendExtANSIForegroundCode(ref char[] data, int index, byte r, byte g, byte b, bool background)
    {
        int l = 10 + (r > 9 ? r > 99 ? 3 : 2 : 1) + (g > 9 ? g > 99 ? 3 : 2 : 1) + (b > 9 ? b > 99 ? 3 : 2 : 1);
        char* ptr = stackalloc char[l];
        SetForegroundSequenceString(ptr, 0, r, g, b, background);
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

/// <summary>
/// Represents a token type for colorizing based on object type instead of color with <see cref="FormattingUtil"/>.
/// </summary>
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
    Number,
    String
}

internal class LoggerFormatProvider : ITerminalFormatProvider
{
    public StackTraceCleaner StackCleaner => Logger.StackCleaner;
}

/// <summary>
/// Create a custom <see cref="StackTraceCleaner"/> provider for <see cref="FormattingUtil"/>.
/// </summary>
public class CustomTerminalFormatProvider(StackTraceCleaner stackCleaner) : ITerminalFormatProvider
{
    public StackTraceCleaner StackCleaner { get; } = stackCleaner;
}