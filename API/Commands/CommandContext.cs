using Cysharp.Threading.Tasks;
using DevkitServer.API.Permissions;
using DevkitServer.Core.Commands.Subsystem;
using DevkitServer.Multiplayer;
using DevkitServer.Players;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace DevkitServer.API.Commands;
/// <summary>
/// Context for a command, includes arguments, caller info, parsing, reply capability, etc. It is safe to <see langword="throw"/> this
/// to exit or <see langword="throw"/> a response from any applicable methods.
/// </summary>
public class CommandContext : Exception
{
    /// <summary>
    /// Command that was called.
    /// </summary>
    public IExecutableCommand Command { get; }

    /// <summary>
    /// Plugin that implements the called command. May be <see langword="null"/> if the command comes from <see cref="DevkitServer"/>.
    /// </summary>
    public IDevkitServerPlugin? Plugin => Command.Plugin;

    /// <summary>
    /// Command arguments not including the name.
    /// </summary>
    /// <remarks>This is not affected by <see cref="ArgumentOffset"/>.</remarks>
    public string[] Arguments { get; }

    /// <summary>
    /// Original command message sent by the caller.
    /// </summary>
    public string OriginalMessage { get; }

    /// <summary>
    /// Whether or not the console was the caller of the command.
    /// </summary>
    /// <remarks>On the client-side build, this will never be true.</remarks>
    public bool IsConsole { get; }
#nullable disable
    /// <summary>
    /// Caller of the command, or <see langword="null"/> if the caller was the console.
    /// </summary>
    /// <remarks><see langword="null"/> when <see cref="IsConsole"/> is <see langword="true"/>. Not marked nullable for convenience.</remarks>
    public SteamPlayer Caller { get; }
#nullable enable

    /// <summary>
    /// Caller of the command as <see cref="EditorUser"/>.
    /// </summary>
    public EditorUser? EditorUser { get; }

    /// <summary>
    /// Useful for sub-commands, offsets any parsing methods.
    /// </summary>
    /// <remarks>Increment this to skip one argument, for example.</remarks>
    public int ArgumentOffset { get; set; }

    /// <summary>
    /// Steam 64 id of the caller.
    /// </summary>
    /// <remarks><see cref="CSteamID.Nil"/> when called by console.</remarks>
    public CSteamID CallerId { get; set; }

#if CLIENT
    /// <summary>
    /// Clientside flag that will be <see langword="true"/> when the command was invoked using the console.
    /// </summary>
    public bool InvokedFromConsole { get; set; }
#endif

    /// <summary>
    /// Number of arguments provided, taking <see cref="ArgumentOffset"/> into account.
    /// </summary>
    /// <remarks>This is not affected by <see cref="ArgumentOffset"/>.</remarks>
    public int ArgumentCount => Arguments.Length - ArgumentOffset;

    private bool ReplyingToConsole => IsConsole
#if CLIENT
        || InvokedFromConsole;
#else
        ;
#endif

    public CommandContext(IExecutableCommand command, string[] arguments, string originalMessage
#if SERVER
        , SteamPlayer? caller
#endif
#if CLIENT
        , bool console
#endif
        )
    {
        Command = command;
        Arguments = arguments;
        OriginalMessage = originalMessage;
#if SERVER
        Caller = caller!;
        IsConsole = Caller == null;
        CallerId = caller == null ? CSteamID.Nil : caller.playerID.steamID;
        if (DevkitServerModule.IsEditing && Caller != null)
        {
            EditorUser = UserManager.FromId(CallerId.m_SteamID);
            if (EditorUser == null || !EditorUser.IsOnline)
                Logger.DevkitServer.LogWarning(nameof(CommandContext), $"Unable to find EditorUser for player {Caller.playerID.steamID.Format()}.");
        }
#else
        InvokedFromConsole = console;
        if (Player.player != null)
        {
            IsConsole = false;
            Caller = Player.player?.channel.owner;
            CallerId = Provider.client;
            if (DevkitServerModule.IsEditing)
                EditorUser = EditorUser.User;
        }
        else
        {
            InvokedFromConsole = true;
            IsConsole = true;
            CallerId = Provider.client;
            Caller = null;
        }
#endif
    }

    /// <summary>
    /// Default translation table for this command. Depends on the command's localization override and the plugin's default localization.
    /// </summary>
    public Local Translations
    {
        get
        {
            if (Command is ILocalizedCommand { Translations: not null } loc)
                return loc.Translations;
            return Plugin?.Translations ?? DevkitServerModule.CommandLocalization;
        }
    }

#if CLIENT
    /// <summary>
    /// Translate a formatting key using the default translation file (<see cref="Translations"/>).
    /// </summary>
    /// <remarks>If <see cref="IsConsole"/> is true, rich text is removed from the result.</remarks>
    /// <exception cref="CommandContext"/>
    public void BreakAndRunOnServer()
    {
        if (DevkitServerModule.IsEditing)
            CommandHandler.Handler.TransitionCommandExecutionToServer(this);
        
        throw this;
    }
#endif

    /// <summary>
    /// Translate a formatting key using the default translation file (<see cref="Translations"/>).
    /// </summary>
    /// <remarks>If <see cref="IsConsole"/> is true, rich text is removed from the result.</remarks>
    /// <param name="format">Formatting key.</param>
    public string Translate(string format) => ReplyingToConsole ? FormattingUtil.ConvertRichTextToVirtualTerminalSequences(Translations.Translate(format)) : Translations.Translate(format);

    /// <summary>
    /// Translate a formatting key using the default translation file (<see cref="Translations"/>).
    /// </summary>
    /// <remarks>If <see cref="IsConsole"/> is true, rich text is removed from the result.</remarks>
    /// <param name="format">Formatting key.</param>
    /// <param name="arg0">Replaces '{0}' in the value of the localization.</param>
    public string Translate(string format, object? arg0) => ReplyingToConsole ? FormattingUtil.ConvertRichTextToVirtualTerminalSequences(Translations.Translate(format, arg0)) : Translations.Translate(format, arg0);

    /// <summary>
    /// Translate a formatting key using the default translation file (<see cref="Translations"/>).
    /// </summary>
    /// <remarks>If <see cref="IsConsole"/> is true, rich text is removed from the result.</remarks>
    /// <param name="format">Formatting key.</param>
    /// <param name="arg0">Replaces '{0}' in the value of the localization.</param>
    /// <param name="arg1">Replaces '{1}' in the value of the localization.</param>
    public string Translate(string format, object? arg0, object? arg1) => ReplyingToConsole ? FormattingUtil.ConvertRichTextToVirtualTerminalSequences(Translations.Translate(format, arg0, arg1)) : Translations.Translate(format, arg0, arg1);

    /// <summary>
    /// Translate a formatting key using the default translation file (<see cref="Translations"/>).
    /// </summary>
    /// <remarks>If <see cref="IsConsole"/> is true, rich text is removed from the result.</remarks>
    /// <param name="format">Formatting key.</param>
    /// <param name="arg0">Replaces '{0}' in the value of the localization.</param>
    /// <param name="arg1">Replaces '{1}' in the value of the localization.</param>
    /// <param name="arg2">Replaces '{2}' in the value of the localization.</param>
    public string Translate(string format, object? arg0, object? arg1, object? arg2) => ReplyingToConsole ? FormattingUtil.ConvertRichTextToVirtualTerminalSequences(Translations.Translate(format, arg0, arg1, arg2)) : Translations.Translate(format, arg0, arg1, arg2);

    /// <summary>
    /// Translate a formatting key using the default translation file (<see cref="Translations"/>).
    /// </summary>
    /// <remarks>If <see cref="IsConsole"/> is true, rich text is removed from the result.</remarks>
    /// <param name="format">Formatting key.</param>
    /// <param name="args">Replaces '{n}' in the value of the localization.</param>
    public string Translate(string format, params object?[] args) => ReplyingToConsole ? FormattingUtil.ConvertRichTextToVirtualTerminalSequences(Translations.Translate(format, args)) : Translations.Translate(format, args);

    /// <summary>
    /// Translate a formatting key using <paramref name="lcl"/>.
    /// </summary>
    /// <remarks>If <see cref="IsConsole"/> is true, rich text is removed from the result.</remarks>
    /// <param name="format">Formatting key.</param>
    /// <param name="lcl">Localization table to use for the translation.</param>
    public string Translate(Local lcl, string format) => ReplyingToConsole ? FormattingUtil.ConvertRichTextToVirtualTerminalSequences(lcl.Translate(format)) : lcl.Translate(format);

    /// <summary>
    /// Translate a formatting key using <paramref name="lcl"/>.
    /// </summary>
    /// <remarks>If <see cref="IsConsole"/> is true, rich text is removed from the result.</remarks>
    /// <param name="format">Formatting key.</param>
    /// <param name="arg0">Replaces '{0}' in the value of the localization.</param>
    /// <param name="lcl">Localization table to use for the translation.</param>
    public string Translate(Local lcl, string format, object? arg0) => ReplyingToConsole ? FormattingUtil.ConvertRichTextToVirtualTerminalSequences(lcl.Translate(format, arg0)) : lcl.Translate(format, arg0);

    /// <summary>
    /// Translate a formatting key using <paramref name="lcl"/>.
    /// </summary>
    /// <remarks>If <see cref="IsConsole"/> is true, rich text is removed from the result.</remarks>
    /// <param name="format">Formatting key.</param>
    /// <param name="arg0">Replaces '{0}' in the value of the localization.</param>
    /// <param name="arg1">Replaces '{1}' in the value of the localization.</param>
    /// <param name="lcl">Localization table to use for the translation.</param>
    public string Translate(Local lcl, string format, object? arg0, object? arg1) => ReplyingToConsole ? FormattingUtil.ConvertRichTextToVirtualTerminalSequences(lcl.Translate(format, arg0, arg1)) : lcl.Translate(format, arg0, arg1);

    /// <summary>
    /// Translate a formatting key using <paramref name="lcl"/>.
    /// </summary>
    /// <remarks>If <see cref="IsConsole"/> is true, rich text is removed from the result.</remarks>
    /// <param name="format">Formatting key.</param>
    /// <param name="arg0">Replaces '{0}' in the value of the localization.</param>
    /// <param name="arg1">Replaces '{1}' in the value of the localization.</param>
    /// <param name="arg2">Replaces '{2}' in the value of the localization.</param>
    /// <param name="lcl">Localization table to use for the translation.</param>
    public string Translate(Local lcl, string format, object? arg0, object? arg1, object? arg2) => ReplyingToConsole ? FormattingUtil.ConvertRichTextToVirtualTerminalSequences(lcl.Translate(format, arg0, arg1, arg2)) : lcl.Translate(format, arg0, arg1, arg2);

    /// <summary>
    /// Translate a formatting key using <paramref name="lcl"/>.
    /// </summary>
    /// <remarks>If <see cref="IsConsole"/> is true, rich text is removed from the result.</remarks>
    /// <param name="format">Formatting key.</param>
    /// <param name="args">Replaces '{n}' in the value of the localization.</param>
    /// <param name="lcl">Localization table to use for the translation.</param>
    public string Translate(Local lcl, string format, params object?[] args) => ReplyingToConsole ? FormattingUtil.ConvertRichTextToVirtualTerminalSequences(lcl.Translate(format, args)) : lcl.Translate(format, args);

    /// <summary>
    /// Send a message to the caller or console using the default translation file (<see cref="Translations"/>).
    /// </summary>
    /// <remarks>Thread safe. Rich text is always enabled.</remarks>
    /// <param name="format">Formatting key.</param>
    /// <returns>A throwable execption to break out of the command.</returns>
    public Exception Reply(string format) => ReplyStringIntl(Translate(format));

    /// <summary>
    /// Send a message to the caller or console using the default translation file (<see cref="Translations"/>).
    /// </summary>
    /// <remarks>Thread safe. Rich text is always enabled.</remarks>
    /// <param name="format">Formatting key.</param>
    /// <param name="arg0">Replaces '{0}' in the value of the localization.</param>
    /// <returns>A throwable execption to break out of the command.</returns>
    public Exception Reply(string format, object? arg0) => ReplyStringIntl(Translate(format, arg0));

    /// <summary>
    /// Send a message to the caller or console using the default translation file (<see cref="Translations"/>).
    /// </summary>
    /// <remarks>Thread safe. Rich text is always enabled.</remarks>
    /// <param name="format">Formatting key.</param>
    /// <param name="arg0">Replaces '{0}' in the value of the localization.</param>
    /// <param name="arg1">Replaces '{1}' in the value of the localization.</param>
    /// <returns>A throwable execption to break out of the command.</returns>
    public Exception Reply(string format, object? arg0, object? arg1) => ReplyStringIntl(Translate(format, arg0, arg1));

    /// <summary>
    /// Send a message to the caller or console using the default translation file (<see cref="Translations"/>).
    /// </summary>
    /// <remarks>Thread safe. Rich text is always enabled.</remarks>
    /// <param name="format">Formatting key.</param>
    /// <param name="arg0">Replaces '{0}' in the value of the localization.</param>
    /// <param name="arg1">Replaces '{1}' in the value of the localization.</param>
    /// <param name="arg2">Replaces '{2}' in the value of the localization.</param>
    /// <returns>A throwable execption to break out of the command.</returns>
    public Exception Reply(string format, object? arg0, object? arg1, object? arg2) => ReplyStringIntl(Translate(format, arg0, arg1, arg2));

    /// <summary>
    /// Send a message to the caller or console using the default translation file (<see cref="Translations"/>).
    /// </summary>
    /// <remarks>Thread safe. Rich text is always enabled.</remarks>
    /// <param name="format">Formatting key.</param>
    /// <param name="args">Replaces '{n}' in the value of the localization.</param>
    /// <returns>A throwable execption to break out of the command.</returns>
    public Exception Reply(string format, params object?[] args) => ReplyStringIntl(Translate(format, args));

    /// <summary>
    /// Send a message to the caller or console using <paramref name="lcl"/>.
    /// </summary>
    /// <remarks>Thread safe. Rich text is always enabled.</remarks>
    /// <param name="format">Formatting key.</param>
    /// <returns>A throwable execption to break out of the command.</returns>
    public Exception Reply(Local lcl, string format) => ReplyStringIntl(Translate(lcl, format));

    /// <summary>
    /// Send a message to the caller or console using <paramref name="lcl"/>.
    /// </summary>
    /// <remarks>Thread safe. Rich text is always enabled.</remarks>
    /// <param name="format">Formatting key.</param>
    /// <param name="arg0">Replaces '{0}' in the value of the localization.</param>
    /// <returns>A throwable execption to break out of the command.</returns>
    public Exception Reply(Local lcl, string format, object? arg0) => ReplyStringIntl(Translate(lcl, format, arg0));

    /// <summary>
    /// Send a message to the caller or console using <paramref name="lcl"/>.
    /// </summary>
    /// <remarks>Thread safe. Rich text is always enabled.</remarks>
    /// <param name="format">Formatting key.</param>
    /// <param name="arg0">Replaces '{0}' in the value of the localization.</param>
    /// <param name="arg1">Replaces '{1}' in the value of the localization.</param>
    /// <returns>A throwable execption to break out of the command.</returns>
    public Exception Reply(Local lcl, string format, object? arg0, object? arg1) => ReplyStringIntl(Translate(lcl, format, arg0, arg1));

    /// <summary>
    /// Send a message to the caller or console using <paramref name="lcl"/>.
    /// </summary>
    /// <remarks>Thread safe. Rich text is always enabled.</remarks>
    /// <param name="format">Formatting key.</param>
    /// <param name="arg0">Replaces '{0}' in the value of the localization.</param>
    /// <param name="arg1">Replaces '{1}' in the value of the localization.</param>
    /// <param name="arg2">Replaces '{2}' in the value of the localization.</param>
    /// <returns>A throwable execption to break out of the command.</returns>
    public Exception Reply(Local lcl, string format, object? arg0, object? arg1, object? arg2) => ReplyStringIntl(Translate(lcl, format, arg0, arg1, arg2));

    /// <summary>
    /// Send a message to the caller or console using <paramref name="lcl"/>.
    /// </summary>
    /// <remarks>Thread safe. Rich text is always enabled.</remarks>
    /// <param name="format">Formatting key.</param>
    /// <param name="args">Replaces '{n}' in the value of the localization.</param>
    /// <returns>A throwable execption to break out of the command.</returns>
    public Exception Reply(Local lcl, string format, params object?[] args) => ReplyStringIntl(Translate(lcl, format, args));

    /// <summary>
    /// Send a message to the caller or console without translation.
    /// </summary>
    /// <remarks>Thread safe. Rich text is always enabled.</remarks>
    /// <param name="rawText">Formatting key.</param>
    /// <returns>A throwable execption to break out of the command.</returns>
    public Exception ReplyString(string rawText) => ReplyStringIntl(ReplyingToConsole ? FormattingUtil.ConvertRichTextToVirtualTerminalSequences(rawText) : rawText);
    private Exception ReplyStringIntl(string rawText)
    {
        if (ReplyingToConsole)
        {
            Command.LogInfo(FormattingUtil.ConvertRichTextToVirtualTerminalSequences(rawText));
        }
        else
        {
#if SERVER
            CommandHandler.SendMessage(rawText, Caller, Command, Severity.Info);
#else
            CommandHandler.SendMessage(rawText, false, Command, Severity.Info);
#endif
        }

        return this;
    }

    /// <summary>
    /// Checks if the argument at index <paramref name="position"/> exists.
    /// </summary>
    /// <remarks>Zero based indexing.</remarks>
    public bool HasArg(int position)
    {
        position += ArgumentOffset;
        return position > -1 && position < Arguments.Length;
    }

    /// <summary>
    /// Checks if there are at least <paramref name="count"/> arguments.
    /// </summary>
    /// <remarks>One based indexing.</remarks>
    public bool HasArgs(int count)
    {
        count += ArgumentOffset;
        return count > -1 && count <= Arguments.Length;
    }

    /// <summary>
    /// Checks if there are exactly <paramref name="count"/> arguments.
    /// </summary>
    /// <remarks>One based indexing.</remarks>
    public bool HasArgsExact(int count)
    {
        count += ArgumentOffset;
        return count == Arguments.Length;
    }

    /// <summary>
    /// Fuzzy comparison of strings.
    /// </summary>
    private static bool CompareStrings(string a, string b) => string.Compare(a, b, CultureInfo.InvariantCulture,
        CompareOptions.IgnoreCase | CompareOptions.IgnoreKanaType | CompareOptions.IgnoreSymbols |
        CompareOptions.IgnoreWidth | CompareOptions.IgnoreNonSpace) == 0;

    /// <summary>
    /// Compares the value of argument <paramref name="parameter"/> with <paramref name="value"/>. Case and culture insensitive.
    /// </summary>
    /// <remarks>Zero based indexing.</remarks>
    /// <returns><see langword="true"/> if <paramref name="parameter"/> matches <paramref name="value"/>.</returns>
    public bool MatchParameter(int parameter, string value)
    {
        parameter += ArgumentOffset;
        if (parameter < 0 || parameter >= Arguments.Length)
            return false;
        return CompareStrings(GetParamForParse(parameter), value);
    }

    /// <summary>
    /// Compares the value of argument <paramref name="parameter"/> with <paramref name="value"/> and <paramref name="alternate"/>. Case and culture insensitive.
    /// </summary>
    /// <remarks>Zero based indexing.</remarks>
    /// <returns><see langword="true"/> if <paramref name="parameter"/> matches one of the values.</returns>
    public bool MatchParameter(int parameter, string value, string alternate)
    {
        parameter += ArgumentOffset;
        if (parameter < 0 || parameter >= Arguments.Length)
            return false;
        string v = GetParamForParse(parameter);
        return CompareStrings(v, value) || CompareStrings(v, alternate);
    }
    /// <summary>
    /// Compares the value of argument <paramref name="parameter"/> with <paramref name="value"/>, <paramref name="alternate1"/>, and <paramref name="alternate2"/>. Case and culture insensitive.
    /// </summary>
    /// <remarks>Zero based indexing.</remarks>
    /// <returns><see langword="true"/> if <paramref name="parameter"/> matches one of the values.</returns>
    public bool MatchParameter(int parameter, string value, string alternate1, string alternate2)
    {
        parameter += ArgumentOffset;
        if (parameter < 0 || parameter >= Arguments.Length)
            return false;
        string v = GetParamForParse(parameter);
        return CompareStrings(v, value) || CompareStrings(v, alternate1) || CompareStrings(v, alternate2);
    }
    /// <summary>
    /// Compares the value of argument <paramref name="parameter"/> with <paramref name="value"/>, <paramref name="alternate1"/>, <paramref name="alternate2"/>, and <paramref name="alternate3"/>. Case and culture insensitive.
    /// </summary>
    /// <remarks>Zero based indexing.</remarks>
    /// <returns><see langword="true"/> if <paramref name="parameter"/> matches one of the values.</returns>
    public bool MatchParameter(int parameter, string value, string alternate1, string alternate2, string alternate3)
    {
        parameter += ArgumentOffset;
        if (parameter < 0 || parameter >= Arguments.Length)
            return false;
        string v = GetParamForParse(parameter);
        return CompareStrings(v, value) || CompareStrings(v, alternate1) || CompareStrings(v, alternate2) || CompareStrings(v, alternate3);
    }
    /// <summary>
    /// Compares the value of argument <paramref name="parameter"/> with <paramref name="alternates"/>. Case and culture insensitive.
    /// </summary>
    /// <remarks>Zero based indexing.</remarks>
    /// <returns><see langword="true"/> if <paramref name="parameter"/> matches one of the values in <paramref name="alternates"/>.</returns>
    public bool MatchParameter(int parameter, params string[] alternates)
    {
        parameter += ArgumentOffset;
        if (parameter < 0 || parameter >= Arguments.Length)
            return false;
        string v = GetParamForParse(parameter);
        for (int i = 0; i < alternates.Length; ++i)
        {
            if (CompareStrings(v, alternates[i]))
                return true;
        }
        return false;
    }

    /// <remarks>Zero based indexing.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string GetParamForParse(int index) => Arguments[index];

    /// <summary>
    /// Returns the <paramref name="parameter"/> at a given index, or <see langword="null"/> if out of range.
    /// </summary>
    /// <remarks>Zero based indexing.</remarks>
    public string? Get(int parameter)
    {
        parameter += ArgumentOffset;
        if (parameter < 0 || parameter >= Arguments.Length)
            return null;
        return GetParamForParse(parameter);
    }

    /// <summary>
    /// Returns a range of parameters from a given <paramref name="start"/> index along a given <paramref name="length"/> (joined by spaces), or <see langword="null"/> if out of range.
    /// </summary>
    /// <remarks>Zero based indexing.</remarks>
    public string? GetRange(int start, int length = -1)
    {
        if (length == 1) return Get(start);
        start += ArgumentOffset;
        if (start < 0 || start >= Arguments.Length)
            return null;
        if (start == Arguments.Length - 1)
            return GetParamForParse(start);
        if (length == -1)
            return string.Join(" ", Arguments, start, Arguments.Length - start);
        if (length < 1) return null;
        if (start + length >= Arguments.Length)
            length = Arguments.Length - start;
        return string.Join(" ", Arguments, start, length);
    }

    /// <summary>
    /// Gets a range of parameters from a given <paramref name="start"/> index along a given <paramref name="length"/> (joined by spaces), or returns <see langword="false"/> if out of range.
    /// </summary>
    /// <remarks>Zero based indexing.</remarks>
    public bool TryGetRange(int start, out string value, int length = -1)
    {
        value = GetRange(start, length)!;
        return value is not null;
    }

    /// <summary>
    /// Gets a <paramref name="parameter"/> at a given index, or returns <see langword="false"/> if out of range.
    /// </summary>
    /// <remarks>Zero based indexing.</remarks>
    public bool TryGet(int parameter, out string value)
    {
        parameter += ArgumentOffset;
        if (parameter < 0 || parameter >= Arguments.Length)
        {
            value = null!;
            return false;
        }
        value = GetParamForParse(parameter);
        return true;
    }

    /// <summary>
    /// Gets a <paramref name="parameter"/> at a given index, parses it as an <typeparamref name="TEnum"/>, or returns <see langword="false"/> if out of range or unable to parse.
    /// </summary>
    /// <remarks>Zero based indexing.</remarks>
    public bool TryGet<TEnum>(int parameter, out TEnum value) where TEnum : unmanaged, Enum
    {
        parameter += ArgumentOffset;
        if (parameter < 0 || parameter >= Arguments.Length)
        {
            value = default;
            return false;
        }
        return Enum.TryParse(GetParamForParse(parameter), true, out value);
    }

    /// <summary>
    /// Gets a <paramref name="parameter"/> at a given index, parses it as an <see cref="Color"/>, or returns <see langword="false"/> if out of range or unable to parse.
    /// </summary>
    /// <remarks>Zero based indexing.</remarks>
    public bool TryGet(int parameter, out Color value)
    {
        parameter += ArgumentOffset;
        if (parameter < 0 || parameter >= Arguments.Length)
        {
            value = Color.white;
            return false;
        }

        return DevkitServerUtility.TryParseColor(GetParamForParse(parameter), out value);
    }

    /// <summary>
    /// Gets a <paramref name="parameter"/> at a given index, parses it as an <see cref="Color32"/>, or returns <see langword="false"/> if out of range or unable to parse.
    /// </summary>
    /// <remarks>Zero based indexing.</remarks>
    public bool TryGet(int parameter, out Color32 value)
    {
        parameter += ArgumentOffset;
        if (parameter < 0 || parameter >= Arguments.Length)
        {
            value = Color.white;
            return false;
        }

        return DevkitServerUtility.TryParseColor32(GetParamForParse(parameter), DevkitServerModule.CommandParseLocale, out value);
    }

    /// <summary>
    /// Gets a <paramref name="parameter"/> at a given index, parses it as an <see cref="int"/>, or returns <see langword="false"/> if out of range or unable to parse.
    /// </summary>
    /// <remarks>Zero based indexing.</remarks>
    public bool TryGet(int parameter, out int value)
    {
        parameter += ArgumentOffset;
        if (parameter < 0 || parameter >= Arguments.Length)
        {
            value = 0;
            return false;
        }
        return int.TryParse(GetParamForParse(parameter), NumberStyles.Number, DevkitServerModule.CommandParseLocale, out value);
    }

    /// <summary>
    /// Gets a <paramref name="parameter"/> at a given index, parses it as a <see cref="byte"/>, or returns <see langword="false"/> if out of range or unable to parse.
    /// </summary>
    /// <remarks>Zero based indexing.</remarks>
    public bool TryGet(int parameter, out byte value)
    {
        parameter += ArgumentOffset;
        if (parameter < 0 || parameter >= Arguments.Length)
        {
            value = 0;
            return false;
        }
        return byte.TryParse(GetParamForParse(parameter), NumberStyles.Number, DevkitServerModule.CommandParseLocale, out value);
    }

    /// <summary>
    /// Gets a <paramref name="parameter"/> at a given index, parses it as a <see cref="short"/>, or returns <see langword="false"/> if out of range or unable to parse.
    /// </summary>
    /// <remarks>Zero based indexing.</remarks>
    public bool TryGet(int parameter, out short value)
    {
        parameter += ArgumentOffset;
        if (parameter < 0 || parameter >= Arguments.Length)
        {
            value = 0;
            return false;
        }
        return short.TryParse(GetParamForParse(parameter), NumberStyles.Number, DevkitServerModule.CommandParseLocale, out value);
    }

    /// <summary>
    /// Gets a <paramref name="parameter"/> at a given index, parses it as a <see cref="sbyte"/>, or returns <see langword="false"/> if out of range or unable to parse.
    /// </summary>
    /// <remarks>Zero based indexing.</remarks>
    public bool TryGet(int parameter, out sbyte value)
    {
        parameter += ArgumentOffset;
        if (parameter < 0 || parameter >= Arguments.Length)
        {
            value = 0;
            return false;
        }
        return sbyte.TryParse(GetParamForParse(parameter), NumberStyles.Number, DevkitServerModule.CommandParseLocale, out value);
    }

    /// <summary>
    /// Gets a <paramref name="parameter"/> at a given index, parses it as a <see cref="Guid"/>, or returns <see langword="false"/> if out of range or unable to parse.
    /// </summary>
    /// <remarks>Zero based indexing.</remarks>
    public bool TryGet(int parameter, out Guid value)
    {
        parameter += ArgumentOffset;
        if (parameter < 0 || parameter >= Arguments.Length)
        {
            value = default;
            return false;
        }
        return Guid.TryParse(GetParamForParse(parameter), out value);
    }

    /// <summary>
    /// Gets a <paramref name="parameter"/> at a given index, parses it as a <see cref="uint"/>, or returns <see langword="false"/> if out of range or unable to parse.
    /// </summary>
    /// <remarks>Zero based indexing.</remarks>
    public bool TryGet(int parameter, out uint value)
    {
        parameter += ArgumentOffset;
        if (parameter < 0 || parameter >= Arguments.Length)
        {
            value = 0;
            return false;
        }
        return uint.TryParse(GetParamForParse(parameter), NumberStyles.Number, DevkitServerModule.CommandParseLocale, out value);
    }

    /// <summary>
    /// Gets a <paramref name="parameter"/> at a given index, parses it as a <see cref="ushort"/>, or returns <see langword="false"/> if out of range or unable to parse.
    /// </summary>
    /// <remarks>Zero based indexing.</remarks>
    public bool TryGet(int parameter, out ushort value)
    {
        parameter += ArgumentOffset;
        if (parameter < 0 || parameter >= Arguments.Length)
        {
            value = 0;
            return false;
        }
        return ushort.TryParse(GetParamForParse(parameter), NumberStyles.Number, DevkitServerModule.CommandParseLocale, out value);
    }

    /// <summary>
    /// Gets a <paramref name="parameter"/> at a given index, parses it as a <see cref="ulong"/>, or returns <see langword="false"/> if out of range or unable to parse.
    /// </summary>
    /// <remarks>Zero based indexing. Use <see cref="TryGet(int,out ulong,out EditorUser?, bool)"/> instead for Steam64 IDs.</remarks>
    public bool TryGet(int parameter, out ulong value)
    {
        parameter += ArgumentOffset;
        if (parameter < 0 || parameter >= Arguments.Length)
        {
            value = 0;
            return false;
        }
        return ulong.TryParse(GetParamForParse(parameter), NumberStyles.Number, DevkitServerModule.CommandParseLocale, out value);
    }

    /// <summary>
    /// Gets a <paramref name="parameter"/> at a given index, parses it as a <see cref="bool"/>, or returns <see langword="false"/> if out of range or unable to parse.
    /// </summary>
    /// <remarks>Zero based indexing.</remarks>
    public bool TryGet(int parameter, out bool value)
    {
        parameter += ArgumentOffset;
        if (parameter < 0 || parameter >= Arguments.Length)
        {
            value = false;
            return false;
        }

        string p = GetParamForParse(parameter);
        if (p.Equals("true", StringComparison.InvariantCultureIgnoreCase) ||
            p.Equals("yes", StringComparison.InvariantCultureIgnoreCase) ||
            p.Equals("1", StringComparison.InvariantCultureIgnoreCase) ||
            p.Equals("y", StringComparison.InvariantCultureIgnoreCase))
        {
            value = true;
        }
        else if (p.Equals("false", StringComparison.InvariantCultureIgnoreCase) ||
                 p.Equals("no", StringComparison.InvariantCultureIgnoreCase) ||
                 p.Equals("0", StringComparison.InvariantCultureIgnoreCase) ||
                 p.Equals("n", StringComparison.InvariantCultureIgnoreCase))
        {
            value = false;
        }
        else
        {
            value = false;
            return false;
        }


        return true;
    }

    /// <summary>
    /// Gets a <paramref name="parameter"/> at a given index, parses it as a <see cref="float"/>, or returns <see langword="false"/> if out of range or unable to parse.
    /// </summary>
    /// <remarks>Zero based indexing.</remarks>
    public bool TryGet(int parameter, out float value)
    {
        parameter += ArgumentOffset;
        if (parameter < 0 || parameter >= Arguments.Length)
        {
            value = 0;
            return false;
        }
        return float.TryParse(GetParamForParse(parameter), NumberStyles.Number, DevkitServerModule.CommandParseLocale, out value) && !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>
    /// Gets a <paramref name="parameter"/> at a given index, parses it as a <see cref="double"/>, or returns <see langword="false"/> if out of range or unable to parse.
    /// </summary>
    /// <remarks>Zero based indexing.</remarks>
    public bool TryGet(int parameter, out double value)
    {
        parameter += ArgumentOffset;
        if (parameter < 0 || parameter >= Arguments.Length)
        {
            value = 0;
            return false;
        }
        return double.TryParse(GetParamForParse(parameter), NumberStyles.Number, DevkitServerModule.CommandParseLocale, out value);
    }

    /// <summary>
    /// Gets a <paramref name="parameter"/> at a given index, parses it as a <see cref="decimal"/>, or returns <see langword="false"/> if out of range or unable to parse.
    /// </summary>
    /// <remarks>Zero based indexing.</remarks>
    public bool TryGet(int parameter, out decimal value)
    {
        parameter += ArgumentOffset;
        if (parameter < 0 || parameter >= Arguments.Length)
        {
            value = 0;
            return false;
        }
        return decimal.TryParse(GetParamForParse(parameter), NumberStyles.Number, DevkitServerModule.CommandParseLocale, out value);
    }

    /// <summary>
    /// Gets a <paramref name="parameter"/> at a given index, parses it as an <typeparamref name="TEnum"/>, or returns <see langword="false"/> if out of range or unable to parse.
    /// </summary>
    /// <remarks>Zero based indexing. Use the 'ref' set of TryGet methods to ensure the original <paramref name="value"/> isn't overwritten.</remarks>
    public bool TryGetRef<TEnum>(int parameter, ref TEnum value) where TEnum : unmanaged, Enum
    {
        parameter += ArgumentOffset;
        if (parameter < 0 || parameter >= Arguments.Length)
        {
            value = default;
            return false;
        }
        if (Enum.TryParse(GetParamForParse(parameter), true, out TEnum value2))
        {
            value = value2;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets a <paramref name="parameter"/> at a given index, parses it as an <see cref="int"/>, or returns <see langword="false"/> if out of range or unable to parse.
    /// </summary>
    /// <remarks>Zero based indexing. Use the 'ref' set of TryGet methods to ensure the original <paramref name="value"/> isn't overwritten.</remarks>
    public bool TryGetRef(int parameter, ref int value)
    {
        parameter += ArgumentOffset;
        if (parameter < 0 || parameter >= Arguments.Length)
        {
            value = 0;
            return false;
        }
        if (int.TryParse(GetParamForParse(parameter), NumberStyles.Number, DevkitServerModule.CommandParseLocale, out int value2))
        {
            value = value2;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets a <paramref name="parameter"/> at a given index, parses it as a <see cref="byte"/>, or returns <see langword="false"/> if out of range or unable to parse.
    /// </summary>
    /// <remarks>Zero based indexing. Use the 'ref' set of TryGet methods to ensure the original <paramref name="value"/> isn't overwritten.</remarks>
    public bool TryGetRef(int parameter, ref byte value)
    {
        parameter += ArgumentOffset;
        if (parameter < 0 || parameter >= Arguments.Length)
        {
            value = 0;
            return false;
        }
        if (byte.TryParse(GetParamForParse(parameter), NumberStyles.Number, DevkitServerModule.CommandParseLocale, out byte value2))
        {
            value = value2;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets a <paramref name="parameter"/> at a given index, parses it as a <see cref="sbyte"/>, or returns <see langword="false"/> if out of range or unable to parse.
    /// </summary>
    /// <remarks>Zero based indexing. Use the 'ref' set of TryGet methods to ensure the original <paramref name="value"/> isn't overwritten.</remarks>
    public bool TryGetRef(int parameter, ref sbyte value)
    {
        parameter += ArgumentOffset;
        if (parameter < 0 || parameter >= Arguments.Length)
        {
            value = 0;
            return false;
        }
        if (sbyte.TryParse(GetParamForParse(parameter), NumberStyles.Number, DevkitServerModule.CommandParseLocale, out sbyte value2))
        {
            value = value2;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets a <paramref name="parameter"/> at a given index, parses it as a <see cref="Guid"/>, or returns <see langword="false"/> if out of range or unable to parse.
    /// </summary>
    /// <remarks>Zero based indexing. Use the 'ref' set of TryGet methods to ensure the original <paramref name="value"/> isn't overwritten.</remarks>
    public bool TryGetRef(int parameter, ref Guid value)
    {
        parameter += ArgumentOffset;
        if (parameter < 0 || parameter >= Arguments.Length)
        {
            value = default;
            return false;
        }
        if (Guid.TryParse(GetParamForParse(parameter), out Guid value2))
        {
            value = value2;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets a <paramref name="parameter"/> at a given index, parses it as a <see cref="uint"/>, or returns <see langword="false"/> if out of range or unable to parse.
    /// </summary>
    /// <remarks>Zero based indexing. Use the 'ref' set of TryGet methods to ensure the original <paramref name="value"/> isn't overwritten.</remarks>
    public bool TryGetRef(int parameter, ref uint value)
    {
        parameter += ArgumentOffset;
        if (parameter < 0 || parameter >= Arguments.Length)
        {
            value = 0;
            return false;
        }
        if (uint.TryParse(GetParamForParse(parameter), NumberStyles.Number, DevkitServerModule.CommandParseLocale, out uint value2))
        {
            value = value2;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets a <paramref name="parameter"/> at a given index, parses it as a <see cref="ushort"/>, or returns <see langword="false"/> if out of range or unable to parse.
    /// </summary>
    /// <remarks>Zero based indexing. Use the 'ref' set of TryGet methods to ensure the original <paramref name="value"/> isn't overwritten.</remarks>
    public bool TryGetRef(int parameter, ref ushort value)
    {
        parameter += ArgumentOffset;
        if (parameter < 0 || parameter >= Arguments.Length)
        {
            value = 0;
            return false;
        }
        if (ushort.TryParse(GetParamForParse(parameter), NumberStyles.Number, DevkitServerModule.CommandParseLocale, out ushort value2))
        {
            value = value2;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets a <paramref name="parameter"/> at a given index, parses it as a <see cref="ulong"/>, or returns <see langword="false"/> if out of range or unable to parse.
    /// </summary>
    /// <remarks>Zero based indexing. Use the 'ref' set of TryGet methods to ensure the original <paramref name="value"/> isn't overwritten.</remarks>
    public bool TryGetRef(int parameter, ref ulong value)
    {
        parameter += ArgumentOffset;
        if (parameter < 0 || parameter >= Arguments.Length)
        {
            value = 0;
            return false;
        }
        if (ulong.TryParse(GetParamForParse(parameter), NumberStyles.Number, DevkitServerModule.CommandParseLocale, out ulong value2))
        {
            value = value2;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets a <paramref name="parameter"/> at a given index, parses it as a <see cref="float"/>, or returns <see langword="false"/> if out of range or unable to parse.
    /// </summary>
    /// <remarks>Zero based indexing. Use the 'ref' set of TryGet methods to ensure the original <paramref name="value"/> isn't overwritten.</remarks>
    public bool TryGetRef(int parameter, ref float value)
    {
        parameter += ArgumentOffset;
        if (parameter < 0 || parameter >= Arguments.Length)
        {
            value = 0;
            return false;
        }
        if (float.TryParse(GetParamForParse(parameter), NumberStyles.Number, DevkitServerModule.CommandParseLocale, out float value2) && !float.IsNaN(value2) && !float.IsInfinity(value2))
        {
            value = value2;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets a <paramref name="parameter"/> at a given index, parses it as a <see cref="double"/>, or returns <see langword="false"/> if out of range or unable to parse.
    /// </summary>
    /// <remarks>Zero based indexing. Use the 'ref' set of TryGet methods to ensure the original <paramref name="value"/> isn't overwritten.</remarks>
    public bool TryGetRef(int parameter, ref double value)
    {
        parameter += ArgumentOffset;
        if (parameter < 0 || parameter >= Arguments.Length)
        {
            value = 0;
            return false;
        }
        if (double.TryParse(GetParamForParse(parameter), NumberStyles.Number, DevkitServerModule.CommandParseLocale, out double value2) && !double.IsNaN(value2) && !double.IsInfinity(value2))
        {
            value = value2;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets a <paramref name="parameter"/> at a given index, parses it as a <see cref="decimal"/>, or returns <see langword="false"/> if out of range or unable to parse.
    /// </summary>
    /// <remarks>Zero based indexing. Use the 'ref' set of TryGet methods to ensure the original <paramref name="value"/> isn't overwritten.</remarks>
    public bool TryGetRef(int parameter, ref decimal value)
    {
        parameter += ArgumentOffset;
        if (parameter < 0 || parameter >= Arguments.Length)
        {
            value = 0;
            return false;
        }
        if (decimal.TryParse(GetParamForParse(parameter), NumberStyles.Number, DevkitServerModule.CommandParseLocale, out decimal value2))
        {
            value = value2;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Find a user or Steam64 ID from an argument. Will take either Steam64 or name. Will only find offline users by Steam64 ID.
    /// </summary>
    /// <param name="steam64">Parsed steam ID.</param>
    /// <param name="onlinePlayer">Will be set to the <see cref="EditorUser"/> instance if they're online.</param>
    /// <param name="remainder">Select the rest of the arguments instead of just one.</param>
    /// <remarks>Zero based indexing.</remarks>
    /// <returns><see langword="true"/> if a valid Steam64 id is parsed (even when the user is offline).</returns>
    public bool TryGet(int parameter, out ulong steam64, out SteamPlayer? onlinePlayer, bool remainder = false, NameSearchType type = NameSearchType.CharacterName)
    {
        parameter += ArgumentOffset;
        if (parameter < 0 || parameter >= Arguments.Length)
        {
            steam64 = 0;
            onlinePlayer = null;
            return false;
        }
        if (!IsConsole && MatchParameter(parameter, "me"))
        {
            onlinePlayer = Caller;
            steam64 = Caller.playerID.steamID.m_SteamID;
            return true;
        }

        string? s = remainder ? GetRange(parameter - ArgumentOffset) : GetParamForParse(parameter);
        if (s != null)
        {
            if (DevkitServerUtility.TryParseSteamId(s, out CSteamID csteam64) && csteam64.UserSteam64())
            {
                onlinePlayer = PlayerTool.getSteamPlayer(csteam64);
                steam64 = csteam64.m_SteamID;
                return true;
            }
            onlinePlayer = UserManager.FromName(s, type);
            if (onlinePlayer is not null)
            {
                steam64 = onlinePlayer.playerID.steamID.m_SteamID;
                return true;
            }
        }

        steam64 = default;
        onlinePlayer = null;
        return false;
    }
    /// <summary>
    /// Find a user or Steam64 ID from an argument. Will take either Steam64 or name. Searches online players in <paramref name="selection"/>.
    /// </summary>
    /// <param name="steam64">Parsed steam ID.</param>
    /// <param name="onlinePlayer">Will be set to the <see cref="EditorUser"/> instance when <see langword="true"/> is returned.</param>
    /// <param name="remainder">Select the rest of the arguments instead of just one.</param>
    /// <remarks>Zero based indexing.</remarks>
    /// <returns><see langword="true"/> if a valid Steam64 id is parsed and that player is in <paramref name="selection"/>.</returns>
    public bool TryGet(int parameter, out ulong steam64, out SteamPlayer onlinePlayer, IEnumerable<SteamPlayer> selection, bool remainder = false, NameSearchType type = NameSearchType.CharacterName)
    {
        parameter += ArgumentOffset;
        if (parameter < 0 || parameter >= Arguments.Length)
        {
            steam64 = 0;
            onlinePlayer = null!;
            return false;
        }
        if (!IsConsole && MatchParameter(parameter, "me"))
        {
            onlinePlayer = Caller;
            steam64 = Caller.playerID.steamID.m_SteamID;
            return selection.Contains(onlinePlayer);
        }

        string? s = remainder ? GetRange(parameter - ArgumentOffset) : GetParamForParse(parameter);
        if (s != null)
        {
            if (DevkitServerUtility.TryParseSteamId(s, out CSteamID csteam64) && csteam64.UserSteam64())
            {
                steam64 = csteam64.m_SteamID;
                foreach (SteamPlayer player in selection)
                {
                    if (player.playerID.steamID.m_SteamID == steam64)
                    {
                        onlinePlayer = player;
                        return true;
                    }
                }
                onlinePlayer = PlayerTool.getSteamPlayer(csteam64);
                return false;
            }
            onlinePlayer = UserManager.FromName(s, type, selection)!;
            if (onlinePlayer is not null)
            {
                steam64 = onlinePlayer.playerID.steamID.m_SteamID;
                return true;
            }
        }

        steam64 = default;
        onlinePlayer = null!;
        return false;
    }
    
    /// <summary>Get an asset based on a <see cref="Guid"/> search, <see cref="ushort"/> search, then <see cref="Asset.FriendlyName"/> search.</summary>
    /// <typeparam name="TAsset"><see cref="Asset"/> type to find.</typeparam>
    /// <param name="len">Set to 1 to only get one parameter (default), set to -1 to get any remaining Arguments.</param>
    /// <param name="multipleResultsFound"><see langword="true"/> if <paramref name="allowMultipleResults"/> is <see langword="false"/> and multiple results were found.</param>
    /// <param name="allowMultipleResults">Set to <see langword="false"/> to make the function return <see langword="false"/> if multiple results are found. <paramref name="asset"/> will still be set.</param>
    /// <param name="selector">Filter assets to pick from.</param>
    /// <remarks>Zero based indexing. Do not use <see cref="ushort"/>s to search for objects, this is a deprecated feature by Unturned.</remarks>
    /// <returns><see langword="true"/> If a <typeparamref name="TAsset"/> is found or multiple are found and <paramref name="allowMultipleResults"/> is <see langword="true"/>.</returns>
    public bool TryGet<TAsset>(int parameter, out TAsset asset, out bool multipleResultsFound, bool remainder = false, int len = 1, bool allowMultipleResults = false, Func<TAsset, bool>? selector = null) where TAsset : Asset
    {
        if (!TryGetRange(parameter, out string p, remainder ? -1 : len) || p.Length == 0)
        {
            multipleResultsFound = false;
            asset = null!;
            return false;
        }
        if ((remainder || parameter == ArgumentCount - 1) && p[p.Length - 1] == '\\')
            p = p.Substring(0, p.Length - 1);
        if (Guid.TryParse(p, out Guid guid))
        {
            asset = Assets.find<TAsset>(guid);
            multipleResultsFound = false;
            return asset is not null && (selector is null || selector(asset));
        }
        EAssetType type = AssetUtil.GetAssetCategory<TAsset>();
        if (type != EAssetType.NONE)
        {
            if (ushort.TryParse(p, out ushort value))
            {
                if (Assets.find(type, value) is TAsset asset2)
                {
                    if (selector is not null && !selector(asset2))
                    {
                        asset = null!;
                        multipleResultsFound = false;
                        return false;
                    }

                    asset = asset2;
                    multipleResultsFound = false;
                    return true;
                }
            }
        }

        List<TAsset> assets = new List<TAsset>();
        Assets.find(assets);
        if (selector != null)
            assets.RemoveAll(x => !selector(x));
        assets.Sort((a, b) => a.FriendlyName.Length.CompareTo(b.FriendlyName.Length));
        if (allowMultipleResults)
        {
            for (int i = 0; i < assets.Count; ++i)
            {
                TAsset a = assets[i];
                if ((selector == null || selector(a)) && a.FriendlyName.Equals(p, StringComparison.InvariantCultureIgnoreCase))
                {
                    asset = a;
                    multipleResultsFound = false;
                    return true;
                }
            }
            for (int i = 0; i < assets.Count; ++i)
            {
                TAsset a = assets[i];
                if ((selector == null || selector(a)) && a.FriendlyName.IndexOf(p, StringComparison.InvariantCultureIgnoreCase) != -1)
                {
                    asset = a;
                    multipleResultsFound = false;
                    return true;
                }
            }
        }
        else
        {
            TAsset? found = null;
            for (int i = 0; i < assets.Count; ++i)
            {
                TAsset a = assets[i];
                if ((selector == null || selector(a)) && assets[i].FriendlyName.Equals(p, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (found == null)
                        found = a;
                    else
                    {
                        multipleResultsFound = true;
                        asset = found;
                        return false;
                    }
                }
            }

            if (found != null)
            {
                asset = found;
                multipleResultsFound = false;
                return true;
            }

            for (int i = 0; i < assets.Count; ++i)
            {
                TAsset a = assets[i];
                if (assets[i].FriendlyName.IndexOf(p, StringComparison.InvariantCultureIgnoreCase) != -1)
                {
                    if (found == null)
                        found = a;
                    else
                    {
                        multipleResultsFound = true;
                        asset = found;
                        return false;
                    }
                }
            }

            if (found != null)
            {
                asset = found;
                multipleResultsFound = false;
                return true;
            }
        }
        multipleResultsFound = false;
        asset = null!;
        return false;
    }
    private float GetDistance(float distance)
    {
        return distance >= 0 ? distance : ((EditorUser == null ? !Level.isEditor : EditorUser.Control.Controller == CameraController.Player) ? 4f : 16f);
    }
    private Transform? GetAim()
    {
        if (IsConsole)
            return null;

        if (EditorUser != null)
            return EditorUser.IsOnline ? EditorUser.Control.Aim : null;

        return Caller?.player != null ? Caller.player.look.aim : MainCamera.instance.transform;
    }

    /// <summary>
    /// Get the transform the user is looking at.
    /// </summary>
    /// <param name="mask">Default raymask is <see cref="RayMasks.PLAYER_INTERACT"/>.</param>
    /// <param name="distance">Default distance is 4m for players and 16m for editors.</param>
    public bool TryGetTarget(out Transform transform, int mask = 0, float distance = -1)
    {
        Transform? aim = GetAim();
        if (aim == null)
        {
            transform = null!;
            return false;
        }
        RaycastInfo info = DamageTool.raycast(new Ray(aim.position, aim.forward), GetDistance(distance), mask == 0 ? RayMasks.PLAYER_INTERACT : mask, Caller?.player);
        transform = info.transform;
        return transform != null;
    }

    /// <summary>
    /// Get <see cref="RaycastInfo"/> from the user.
    /// </summary>
    /// <param name="mask">Raycast mask, could also use <see cref="ERayMask"/>.</param>
    /// <param name="distance">Default distance is 4m for players and 16m for editors.</param>
    public bool TryRaycast(out RaycastInfo info, int mask, float distance = -1)
    {
        Transform? aim = GetAim();
        if (aim == null)
        {
            info = null!;
            return false;
        }
        info = DamageTool.raycast(new Ray(aim.position, aim.forward), GetDistance(distance), mask == 0 ? RayMasks.PLAYER_INTERACT : mask, Caller?.player);
        return info.transform != null;
    }

    /// <summary>
    /// Get the <see cref="Interactable"/> the user is looking at.
    /// </summary>
    /// <param name="mask">Default raymask is <see cref="RayMasks.PLAYER_INTERACT"/>.</param>
    /// <param name="distance">Default distance is 4m for players and 16m for editors.</param>
    public bool TryGetTarget<T>(out T interactable, int mask = 0, float distance = 4f) where T : Interactable
    {
        Transform? aim = GetAim();
        if (aim == null)
        {
            interactable = null!;
            return false;
        }
        RaycastInfo info = DamageTool.raycast(new Ray(aim.position, aim.forward), GetDistance(distance), mask == 0 ? RayMasks.PLAYER_INTERACT : mask, Caller?.player);
        if (info.transform == null)
        {
            interactable = null!;
            return false;
        }
        if (typeof(InteractableVehicle).IsAssignableFrom(typeof(T)))
        {
            interactable = (info.vehicle as T)!;
            return interactable != null;
        }
        if (typeof(InteractableForage).IsAssignableFrom(typeof(T)))
        {
            if (info.transform.TryGetComponent(out InteractableForage forage))
            {
                interactable = (forage as T)!;
                return interactable != null;
            }
        }
        BarricadeDrop drop = BarricadeManager.FindBarricadeByRootTransform(info.transform);
        interactable = (drop?.interactable as T)!;
        return interactable != null;
    }

    /// <summary>
    /// Get the <see cref="BarricadeDrop"/> the user is looking at.
    /// </summary>
    /// <param name="distance">Default distance is 4m for players and 16m for editors.</param>
    public bool TryGetTarget(out BarricadeDrop drop, float distance = 4f)
    {
        Transform? aim = GetAim();
        if (aim == null)
        {
            drop = null!;
            return false;
        }
        RaycastInfo info = DamageTool.raycast(new Ray(aim.position, aim.forward), GetDistance(distance), RayMasks.BARRICADE, Caller?.player);
        if (info.transform == null)
        {
            drop = null!;
            return false;
        }
        drop = BarricadeManager.FindBarricadeByRootTransform(info.transform);
        return drop != null;
    }

    /// <summary>
    /// Get the <see cref="StructureDrop"/> the user is looking at.
    /// </summary>
    /// <param name="distance">Default distance is 4m for players and 16m for editors.</param>
    public bool TryGetTarget(out StructureDrop drop, float distance = 4f)
    {
        Transform? aim = GetAim();
        if (aim == null)
        {
            drop = null!;
            return false;
        }
        RaycastInfo info = DamageTool.raycast(new Ray(aim.position, aim.forward), GetDistance(distance), RayMasks.STRUCTURE, Caller?.player);
        if (info.transform == null)
        {
            drop = null!;
            return false;
        }
        drop = StructureManager.FindStructureByRootTransform(info.transform);
        return drop != null;
    }

    /// <summary>
    /// Get the <see cref="InteractableVehicle"/> the user is looking at.
    /// </summary>
    /// <param name="distance">Default distance is 4m for players and 16m for editors.</param>
    public bool TryGetTarget(out InteractableVehicle vehicle, float distance = 4f, bool allowDead = false)
    {
        Transform? aim = GetAim();
        if (aim == null)
        {
            vehicle = null!;
            return false;
        }
        if (Caller != null && Caller.player != null)
        {
            vehicle = Caller.player.movement.getVehicle();
            if (vehicle != null)
                return true;
        }

        RaycastInfo info = DamageTool.raycast(new Ray(aim.position, aim.forward), GetDistance(distance), RayMasks.VEHICLE, Caller?.player);

        vehicle = info.vehicle;
        return vehicle != null && (allowDead || !vehicle.isDead);
    }

    /// <summary>
    /// Check if <see cref="Caller"/> has <paramref name="permission"/>. Always returns <see langword="true"/> when ran with console.
    /// </summary>
    public bool HasPermission(PermissionLeaf permission)
    { 
        if (IsConsole) return true;
#if SERVER
        return permission.Has(Caller.playerID.steamID.m_SteamID);
#else
        return permission.Has();
#endif
    }

    /// <summary>
    /// Throws an exception and sends the generic 'no permission' message if the caller doesn't have <paramref name="permission"/>.
    /// </summary>
    /// <exception cref="CommandContext"/>
    public void AssertPermissions(PermissionLeaf permission)
    {
        if (!HasPermission(permission))
            throw SendNoPermission();
    }

    /// <summary>
    /// Throws an exception and sends the generic 'no permission' message if the caller doesn't have at least one of the provided permissions.
    /// </summary>
    /// <exception cref="CommandContext"/>
    public void AssertPermissionsOr(PermissionLeaf permission1, PermissionLeaf permission2)
    {
        if (!HasPermission(permission1) && !HasPermission(permission2))
            throw SendNoPermission();
    }

    /// <summary>
    /// Throws an exception and sends the generic 'no permission' message if the caller doesn't have at least one of the provided permissions.
    /// </summary>
    /// <exception cref="CommandContext"/>
    public void AssertPermissionsOr(PermissionLeaf permission1, PermissionLeaf permission2, PermissionLeaf permission3)
    {
        if (!HasPermission(permission1) && !HasPermission(permission2) && !HasPermission(permission3))
            throw SendNoPermission();
    }

    /// <summary>
    /// Throws an exception and sends the generic 'no permission' message if the caller doesn't have at least one of the provided <paramref name="permissions"/>.
    /// </summary>
    /// <exception cref="CommandContext"/>
    public void AssertPermissionsOr(params PermissionLeaf[] permissions)
    {
        for (int i = 0; i < permissions.Length; i++)
        {
            if (HasPermission(permissions[i]))
                return;
        }

        throw SendNoPermission();
    }

    /// <summary>
    /// Throws an exception and sends the generic 'no permission' message if the caller doesn't have all of the provided permissions.
    /// </summary>
    /// <exception cref="CommandContext"/>
    public void AssertPermissionsAnd(PermissionLeaf permission1, PermissionLeaf permission2)
    {
        if (!HasPermission(permission1) || !HasPermission(permission2))
            throw SendNoPermission();
    }

    /// <summary>
    /// Throws an exception and sends the generic 'no permission' message if the caller doesn't have all of the provided permissions.
    /// </summary>
    /// <exception cref="CommandContext"/>
    public void AssertPermissionsAnd(PermissionLeaf permission1, PermissionLeaf permission2, PermissionLeaf permission3)
    {
        if (!HasPermission(permission1) || !HasPermission(permission2) || !HasPermission(permission3))
            throw SendNoPermission();
    }

    /// <summary>
    /// Throws an exception and sends the generic 'no permission' message if the caller doesn't have all of the provided <paramref name="permissions"/>.
    /// </summary>
    /// <exception cref="CommandContext"/>
    public void AssertPermissionsAnd(params PermissionLeaf[] permissions)
    {
        for (int i = 0; i < permissions.Length; i++)
        {
            if (!HasPermission(permissions[i]))
                throw SendNoPermission();
        }
    }

    /// <summary>
    /// Throws an exception and sends the generic 'no permission' message if the caller doesn't have at least one of the provided permissions.
    /// </summary>
    /// <exception cref="CommandContext"/>
    public void AssertDevkitServerClient()
    {
        if (!DevkitServerModule.IsEditing)
            throw SendNotDevkitServerClient();
    }

    /// <summary>
    /// Throws an exception if the command was called from console or the player has left the server since the command was executed.
    /// </summary>
    /// <exception cref="CommandContext"/>
    public void AssertRanByPlayer()
    {
        if (IsConsole || EditorUser == null && Caller?.player == null || EditorUser != null && !EditorUser.IsOnline)
            throw SendPlayerOnlyError();
    }

    /// <summary>
    /// Throws an exception if the command was called from console or the player has left the server since the command was executed or the player is not an <see cref="Players.EditorUser"/>.
    /// </summary>
    /// <exception cref="CommandContext"/>
    public void AssertRanByEditorUser()
    {
        if (IsConsole || EditorUser == null || !EditorUser.IsOnline)
            throw Reply(DevkitServerModule.CommandLocalization, "CommandMustBeEditorPlayer");
    }

    /// <summary>
    /// Throws an exception if the command was called by a player.
    /// </summary>
    /// <exception cref="CommandContext"/>
    public void AssertRanByConsole()
    {
        if (!IsConsole)
            throw SendConsoleOnlyError();
    }

    /// <summary>
    /// Throws an exception if there were less than <paramref name="count"/> arguments supplied, and sends a generic 'correct usage' message with <paramref name="usage"/>.
    /// </summary>
    /// <exception cref="CommandContext"/>
    public void AssertArgs(int count, string usage)
    {
        if (!HasArgs(count))
            throw SendCorrectUsage(usage);
    }

    /// <summary>
    /// Throws an exception if there were not exactly <paramref name="count"/> arguments supplied, and sends a generic 'correct usage' message with <paramref name="usage"/>.
    /// </summary>
    /// <exception cref="CommandContext"/>
    public void AssertArgsExact(int count, string usage)
    {
        if (!HasArgsExact(count))
            throw SendCorrectUsage(usage);
    }

    /// <summary>
    /// Throws an exception if <paramref name="parameter"/> matches 'help', and sends <paramref name="helpMessage"/> as a raw string.
    /// </summary>
    /// <exception cref="CommandContext"/>
    public void AssertHelpCheckString(int parameter, string helpMessage)
    {
        if (MatchParameter(parameter, DevkitServerModule.HelpMessage))
            throw ReplyString(helpMessage);
    }

    /// <summary>
    /// Throws an exception if <paramref name="parameter"/> matches 'help', and sends <paramref name="format"/> translated through the default translation table (<see cref="Translations"/>).
    /// </summary>
    /// <exception cref="CommandContext"/>
    public void AssertHelpCheckFormat(int parameter, string format)
    {
        if (MatchParameter(parameter, DevkitServerModule.HelpMessage))
            throw Reply(format);
    }

    /// <summary>
    /// Throws an exception if <paramref name="parameter"/> matches 'help', and sends <paramref name="format"/> translated through the default translation table (<see cref="Translations"/>).
    /// </summary>
    /// <param name="args">Replaces '{n}' in the value of the localization.</param>
    /// <exception cref="CommandContext"/>
    public void AssertHelpCheckFormat(int parameter, string format, params object?[] args)
    {
        if (MatchParameter(parameter, DevkitServerModule.HelpMessage))
            throw Reply(format, args);
    }

    /// <summary>
    /// Throws an exception if <paramref name="parameter"/> matches 'help', and sends a generic 'correct usage' message with <paramref name="usage"/>.
    /// </summary>
    /// <exception cref="CommandContext"/>
    public void AssertHelpCheck(int parameter, string usage)
    {
        if (MatchParameter(parameter, DevkitServerModule.HelpMessage))
            throw SendCorrectUsage(usage);
    }

    /// <summary>
    /// Sends a generic 'can only be ran by players' message.
    /// </summary>
    public Exception SendPlayerOnlyError() => Reply(DevkitServerModule.CommandLocalization, "PlayersOnly");

    /// <summary>
    /// Sends a generic 'can only be ran by console' message.
    /// </summary>
    public Exception SendConsoleOnlyError() => Reply(DevkitServerModule.CommandLocalization, "ConsoleOnly");

    /// <summary>
    /// Sends a generic 'ran into an error' message with <paramref name="errorType"/>.
    /// </summary>
    public Exception SendUnknownError(string errorType = "Exception") => Reply(DevkitServerModule.CommandLocalization, "Exception", errorType);

    /// <summary>
    /// Sends a generic 'no permissions' message.
    /// </summary>
    public Exception SendNoPermission() => Reply(DevkitServerModule.CommandLocalization, "NoPermissions");

    /// <summary>
    /// Sends a generic 'not connected to DevkitServer server' message.
    /// </summary>
    public Exception SendNotDevkitServerClient() => Reply(DevkitServerModule.CommandLocalization, "NotDevkitServerClient");

    /// <summary>
    /// Sends a generic 'correct usage' message with <paramref name="usage"/>.
    /// </summary>
    public Exception SendCorrectUsage(string usage) => Reply(DevkitServerModule.CommandLocalization, "CorrectUsage", usage);


    public override string ToString() => $"\"/{Command.CommandName.ToLower()}\" ran by {(IsConsole ? "{CONSOLE}" : Caller)} with args [{string.Join(", ", Arguments)}]";

    internal async UniTask ExecuteAsync()
    {
        CancellationToken token = DevkitServerModule.UnloadToken;
        await UniTask.SwitchToMainThread(token);

        try
        {
            this.AssertMode(Command.Mode);
        }
        catch (CommandContext)
        {
            return;
        }

        SemaphoreSlim? waited = null;
        if (Command is ISynchronizedCommand { Semaphore: not null } sync)
        {
            waited = sync.Semaphore;

            await waited.WaitAsync(token).ConfigureAwait(false);

            await UniTask.SwitchToMainThread(token);
        }

        try
        {
            await Command.Execute(this, token);
        }
        catch (CommandContext) { }
        catch (Exception ex)
        {
            CommandHandler.Handler.HandleCommandException(this, ex);
        }
        finally
        {
            waited?.Release();

            if (CommandHandler.Handler is CommandHandler handler)
            {
                handler.TryInvokeOnCommandExecuted(
#if SERVER
                    Caller,
#endif
                    this);
            }
        }
    }
}
