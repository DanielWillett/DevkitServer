using System.Reflection;
using Cysharp.Threading.Tasks;
using DevkitServer.API;
using DevkitServer.API.Abstractions;
using DevkitServer.API.Commands;
using DevkitServer.API.Logging;
using DevkitServer.Configuration;
using DevkitServer.Patches;
#if SERVER
using DevkitServer.Multiplayer;
using DevkitServer.Players;
#endif
using HarmonyLib;

namespace DevkitServer.Core.Commands.Subsystem;
/// <summary>
/// Provides an API for interacting with commands, late-registering custom commands, or completely replacing the command implementation.
/// </summary>
/// <remarks>Override to change behavior (or implement <see cref="ICommandHandler"/>) and set <see cref="Handler"/>.</remarks>
public class CommandHandler : ICommandHandler, IDisposable
{
    protected static CachedMulticastEvent<ExecutedCommand> EventGlobalOnCommandExecuted = new CachedMulticastEvent<ExecutedCommand>(typeof(CommandHandler), nameof(GlobalOnCommandExecuted));
    protected static CachedMulticastEvent<ExecutingCommand> EventGlobalOnExecutingCommand = new CachedMulticastEvent<ExecutingCommand>(typeof(CommandHandler), nameof(GlobalOnExecutingCommand));
    protected static CachedMulticastEvent<Action<IExecutableCommand>> EventGlobalOnCommandRegistered = new CachedMulticastEvent<Action<IExecutableCommand>>(typeof(CommandHandler), nameof(GlobalOnCommandRegistered));
    protected static CachedMulticastEvent<Action<IExecutableCommand>> EventGlobalOnCommandDeregistered = new CachedMulticastEvent<Action<IExecutableCommand>>(typeof(CommandHandler), nameof(GlobalOnCommandDeregistered));

    protected CachedMulticastEvent<ExecutedCommand> EventOnCommandExecuted = new CachedMulticastEvent<ExecutedCommand>(typeof(CommandHandler), nameof(OnCommandExecuted));
    protected CachedMulticastEvent<ExecutingCommand> EventOnExecutingCommand = new CachedMulticastEvent<ExecutingCommand>(typeof(CommandHandler), nameof(OnExecutingCommand));
    protected CachedMulticastEvent<Action<IExecutableCommand>> EventOnCommandRegistered = new CachedMulticastEvent<Action<IExecutableCommand>>(typeof(CommandHandler), nameof(OnCommandRegistered));
    protected CachedMulticastEvent<Action<IExecutableCommand>> EventOnCommandDeregistered = new CachedMulticastEvent<Action<IExecutableCommand>>(typeof(CommandHandler), nameof(OnCommandDeregistered));

    public static event ExecutedCommand GlobalOnCommandExecuted
    {
        add => EventGlobalOnCommandExecuted.Add(value);
        remove => EventGlobalOnCommandExecuted.Remove(value);
    }
    public static event ExecutingCommand GlobalOnExecutingCommand
    {
        add => EventGlobalOnExecutingCommand.Add(value);
        remove => EventGlobalOnExecutingCommand.Remove(value);
    }
    public static event Action<IExecutableCommand> GlobalOnCommandRegistered
    {
        add => EventGlobalOnCommandRegistered.Add(value);
        remove => EventGlobalOnCommandRegistered.Remove(value);
    }
    public static event Action<IExecutableCommand> GlobalOnCommandDeregistered
    {
        add => EventGlobalOnCommandDeregistered.Add(value);
        remove => EventGlobalOnCommandDeregistered.Remove(value);
    }

    public delegate void ExecutedCommand(
#if SERVER
        SteamPlayer? user,
# endif
        CommandContext context);

    public delegate void ExecutingCommand(
#if SERVER
        SteamPlayer? user,
#endif
        CommandContext context,
        ref bool shouldExecute
    );

    protected bool Initialized;
    private static ICommandHandler _handler = null!;
    protected readonly CommandParser Parser;
    private CommandContext? _activeVanillaCommand;
    private bool _logPatched;
    private MethodInfo? _logMethod;
    private MethodInfo? _logPrefix;
#if CLIENT
    private MethodInfo? _chatMethod;
    private MethodInfo? _chatPrefix;
    private bool _chatPatched;
    private bool _resending;
#endif
    private readonly List<IExecutableCommand> _registeredCommands = new List<IExecutableCommand>();
    public IReadOnlyList<IExecutableCommand> Commands { get; }

    public event ExecutedCommand OnCommandExecuted
    {
        add => EventOnCommandExecuted.Add(value);
        remove => EventOnCommandExecuted.Remove(value);
    }
    public event ExecutingCommand OnExecutingCommand
    {
        add => EventOnExecutingCommand.Add(value);
        remove => EventOnExecutingCommand.Remove(value);
    }
    public event Action<IExecutableCommand> OnCommandRegistered
    {
        add => EventOnCommandRegistered.Add(value);
        remove => EventOnCommandRegistered.Remove(value);
    }
    public event Action<IExecutableCommand> OnCommandDeregistered
    {
        add => EventOnCommandDeregistered.Add(value);
        remove => EventOnCommandDeregistered.Remove(value);
    }
    internal static bool IsLoggingFromDevkitServer;

    /// <exception cref="NotSupportedException">Called setter on non-game thread.</exception>
    public static ICommandHandler Handler
    {
        get => _handler;
        set
        {
            ThreadUtil.assertIsGameThread();

            value.OnCommandExecuted += EventGlobalOnCommandExecuted.TryInvoke;
            value.OnExecutingCommand += EventGlobalOnExecutingCommand.TryInvoke;
            value.OnCommandRegistered += EventGlobalOnCommandRegistered.TryInvoke;
            value.OnCommandDeregistered += EventGlobalOnCommandDeregistered.TryInvoke;
            ICommandHandler? old = Interlocked.Exchange(ref _handler, value);
            if (old != null)
            {
                foreach (IExecutableCommand command in old.Commands.ToList())
                {
                    old.TryDeregisterCommand(command);
                    value.TryRegisterCommand(command);
                }
                old.OnCommandExecuted -= EventGlobalOnCommandExecuted.TryInvoke;
                old.OnExecutingCommand -= EventGlobalOnExecutingCommand.TryInvoke;
                old.OnCommandRegistered -= EventGlobalOnCommandRegistered.TryInvoke;
                old.OnCommandDeregistered -= EventGlobalOnCommandDeregistered.TryInvoke;
                if (old is IDisposable disp)
                    disp.Dispose();
            }
            value.Init();
        }
    }
    protected CommandHandler()
    {
        Commands = _registeredCommands.AsReadOnly();
        Parser = new CommandParser(this);
        try
        {
            _logMethod = typeof(Logs).GetMethod(nameof(Logs.printLine), BindingFlags.Static | BindingFlags.Public);
            if (_logMethod == null)
                Logger.LogWarning("Unable to find method " + typeof(Logs).Format() + "." + nameof(Logs.printLine).Colorize(Color.red) + " for a patch to listen to vanilla command responses.");
            else
            {
                _logPrefix = Accessor.GetMethod(OnLogged);
                PatchesMain.Patcher.Patch(_logMethod, prefix: new HarmonyMethod(_logPrefix));
                _logPatched = true;
                Logger.LogDebug("Patched " + _logMethod.Format() + " to listen to vanilla command responses.");
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Unable to patch " + typeof(Logs).Format() + "." + nameof(Logs.printLine).Colorize(Color.red) + " to listen to vanilla command responses.");
            Logger.LogError(ex);
        }
#if CLIENT
        try
        {
            _chatMethod = typeof(ChatManager).GetMethod(nameof(ChatManager.sendChat), BindingFlags.Static | BindingFlags.Public);
            if (_chatMethod == null)
                Logger.LogWarning("Unable to find method " + typeof(ChatManager).Format() + "." + nameof(ChatManager.sendChat).Colorize(Color.red) + " for a patch to listen to client commands.");
            else
            {
                _chatPrefix = Accessor.GetMethod(OnClientChatted);
                PatchesMain.Patcher.Patch(_chatMethod, prefix: new HarmonyMethod(_chatPrefix));
                _chatPatched = true;
                Logger.LogDebug("Patched " + _chatMethod.Format() + " to listen to client commands.");
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Unable to patch " + typeof(ChatManager).Format() + "." + nameof(ChatManager.sendChat).Colorize(Color.red) + " to listen to client commands.");
            Logger.LogError(ex);
        }
#endif
    }

    private static void OnLogged(string message)
    {
        if (!IsLoggingFromDevkitServer && Handler is CommandHandler { _activeVanillaCommand.IsConsole: false } hndl)
            hndl._activeVanillaCommand.ReplyString(message);
    }
#if CLIENT
    void ICommandHandler.TransitionCommandExecutionToServer(CommandContext ctx)
    {
        if (DevkitServerModule.IsMainThread)
            TransitionCommandExecutionToServerIntl(ctx);
        else
            DevkitServerUtility.QueueOnMainThread(() => TransitionCommandExecutionToServerIntl(ctx));
    }
    private void TransitionCommandExecutionToServerIntl(CommandContext ctx)
    {
        _resending = true;
        ChatManager.sendChat(EChatMode.GLOBAL, ctx.OriginalMessage);
        _resending = false;
    }
    private static bool OnClientChatted(EChatMode mode, string text)
    {
        if (DevkitServerModule.IsEditing && Handler is CommandHandler hndl)
        {
            bool shouldList = true;
            if (!hndl._resending && hndl.Parser.TryRunCommand(false, text, ref shouldList, true))
                return false;
        }

        return true;
    }
#endif

    public virtual void ExecuteCommand(IExecutableCommand command,
#if SERVER
        SteamPlayer? user, 
#endif
#if CLIENT
        bool console,
#endif
        string[] args, string originalMessage
        )
    {
        ThreadUtil.assertIsGameThread();

        if (!command.CheckPermission(
#if SERVER
                user
#endif
            ))
        {
            SendNoPermissionMessage(
#if SERVER
                user,
#endif
#if CLIENT
                console,
#endif
                command);
            return;
        }

        CommandContext ctx = new CommandContext(command, args, originalMessage
#if SERVER
            , user
#endif
#if CLIENT
            , console
#endif
        );
        bool shouldExecute = true;
        EventOnExecutingCommand.TryInvoke(
#if SERVER
            ctx.Caller,
#endif
            ctx, ref shouldExecute);
        if (!shouldExecute)
            return;
        if (command is VanillaCommand)
            _activeVanillaCommand = ctx;

        UniTask.Create(ctx.ExecuteAsync);

        if (ctx == _activeVanillaCommand)
            _activeVanillaCommand = null;
    }

    internal static void InitImpl()
    {
        if (_handler == null)
        {
            Handler = new CommandHandler();
        }
    }
    public virtual void Init()
    {
        if (!Initialized)
        {
#if SERVER
            ChatManager.onCheckPermissions += OnChatProcessing;
#else

#endif
            Logger.OnInputting += OnCommandInput;
            Initialized = true;
            CommandEx.RegisterVanillaCommands();
            CommandEx.DefaultReflectCommands();
            Logger.LogInfo("[CMD] Registered " + _registeredCommands.Count + " command(s).", ConsoleColor.DarkGreen);
        }
    }
    public virtual void Dispose()
    {
        if (Initialized)
        {
            try
            {
                if (_logPatched && _logMethod != null && _logPrefix != null)
                {
                    PatchesMain.Patcher.Unpatch(_logMethod, _logPrefix);
                    _logMethod = null;
                    _logPrefix = null;
                    _logPatched = false;
                }
            }
            catch (Exception ex)
            {
                if (_logMethod != null)
                    Logger.LogError("Failed to unpatch " + _logMethod.Format() + " when disposing of a " + GetType().Format() + ".");
                Logger.LogError(ex);
            }
#if CLIENT
            try
            {
                if (_chatPatched && _chatMethod != null && _chatPrefix != null)
                {
                    PatchesMain.Patcher.Unpatch(_chatMethod, _chatPrefix);
                    _chatMethod = null;
                    _chatPrefix = null;
                    _chatPatched = false;
                }
            }
            catch (Exception ex)
            {
                if (_chatMethod != null)
                    Logger.LogError("Failed to unpatch " + _chatMethod.Format() + " when disposing of a " + GetType().Format() + ".");
                Logger.LogError(ex);
            }
#endif
#if SERVER
            ChatManager.onCheckPermissions -= OnChatProcessing;
#endif
            Logger.OnInputting -= OnCommandInput;

            foreach (IExecutableCommand command in _registeredCommands)
            {
                if (command is IDisposable disp)
                    disp.Dispose();
            }
            _registeredCommands.Clear();
        }
    }
    protected virtual void OnCommandInput(string inputmessage, ref bool shouldHandle)
    {
        if (!shouldHandle) return;
        shouldHandle = false;
        if (inputmessage.Length > 0 && !Parser.TryRunCommand(
#if SERVER
                null,
#endif
#if CLIENT
                true,
#endif
                inputmessage, ref shouldHandle, false))
        {
#if SERVER
            SendHelpMessage(null);
#else
            // run as server command or send chat
            if (Provider.isConnected && Level.isLoaded)
            {
                if (Array.IndexOf(CommandParser.Prefixes, inputmessage[0]) == -1)
                {
                    if (inputmessage[0] is '>' && inputmessage.Length > 1)
                        inputmessage = inputmessage.Substring(char.IsWhiteSpace(inputmessage[1]) && inputmessage.Length > 1 ? 2 : 1);
                    else
                        inputmessage = CommandParser.Prefixes[0] + inputmessage;
                }

                ChatManager.sendChat(EChatMode.GLOBAL, inputmessage);
            }
            else
            {
                SendHelpMessage(true);
            }
#endif
        }
    }

#if SERVER
    protected virtual void OnChatProcessing(SteamPlayer player, string text, ref bool shouldExecuteCommand, ref bool shouldList)
    {
        shouldExecuteCommand = false;
        if (!Parser.TryRunCommand(player, text, ref shouldList, true))
            SendHelpMessage(player);
    }
#endif
    public virtual void SendHelpMessage(
#if SERVER
        SteamPlayer? user
#endif
#if CLIENT
        bool console
#endif
        )
    {
        string tr = DevkitServerModule.CommandLocalization.format("UnknownCommand");
#if SERVER
        SendMessage(tr, user, null);
#else
        SendMessage(tr, console, null);
#endif
    }
    public virtual void SendNoPermissionMessage(
#if SERVER
        SteamPlayer? user,
#endif
#if CLIENT
        bool console,
#endif
        IExecutableCommand command)
    {
        string tr = DevkitServerModule.CommandLocalization.format("NoPermissions");
#if SERVER
        SendMessage(tr, user, command);
#else
        SendMessage(tr, console, command);
#endif
    }
    internal static void SendMessage(string tr
#if SERVER
        , SteamPlayer? user
#endif
#if CLIENT
        , bool console
#endif
    , IExecutableCommand? command, Severity severity = Severity.Info)
    {
#if SERVER
        if (user != null)
        {
            if (DevkitServerModule.IsMainThread)
                ChatManager.say(user.playerID.steamID, tr, Palette.AMBIENT, EChatMode.SAY, true);
            else
                DevkitServerUtility.QueueOnMainThread(() => ChatManager.say(user.playerID.steamID, tr, Palette.AMBIENT, EChatMode.SAY, true));
        }
        else
            Log(FormattingUtil.ConvertRichTextToANSI(tr));
#else
        if (!console)
        {
            if (DevkitServerModule.IsMainThread)
                ChatManager.receiveChatMessage(CSteamID.Nil, string.Empty, EChatMode.SAY, Palette.AMBIENT, true, tr);
            else
                DevkitServerUtility.QueueOnMainThread(() => ChatManager.receiveChatMessage(CSteamID.Nil, string.Empty, EChatMode.SAY, Palette.AMBIENT, true, tr));
        }
        Log(FormattingUtil.ConvertRichTextToANSI(tr));
#endif
        void Log(string msg)
        {
            if (command != null)
            {
                switch (severity)
                {
                    case Severity.Error:
                    case Severity.Fatal:
                        command.LogError(msg);
                        break;
                    case Severity.Warning:
                        command.LogWarning(msg);
                        break;
                    case Severity.Debug:
                        command.LogDebug(msg);
                        break;
                    case Severity.Info:
                    default:
                        command.LogInfo(msg);
                        break;
                }
            }
            else
                Logger.Log(severity, msg);
        }
    }
    public virtual bool TryRegisterCommand(IExecutableCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.CommandName))
        {
            command.LogError("Invalid command name: " + command.CommandName.Format() + ".");
            return false;
        }


        if (command is ISynchronizedCommand sync)
        {
            if (sync.Semaphore == null)
                sync.Semaphore = new SemaphoreSlim(1, 1);
            else if (sync.Semaphore.CurrentCount != 1)
                command.LogWarning("Predefined semaphore should have a 1 initialCount and 1 maxCount to properly synchronize access.");
        }
        if (command is ICommandLocalizationFile file)
        {
            if (file.Translations == null)
            {
                string dir = file.TranslationsDirectory;
                if (string.IsNullOrWhiteSpace(dir))
                    dir = file.CommandName;
                string basePath = command.Plugin == null ? DevkitServerConfig.CommandLocalizationFilePath : command.Plugin.LocalizationDirectory;
                dir = Path.Combine(basePath, dir);
                Local lcl = Localization.tryRead(dir, false);
                LocalDatDictionary def = file.DefaultTranslations;
                if (def == null)
                    command.LogError("No default translations provided.");
                else
                {
                    try
                    {
                        DevkitServerUtility.UpdateLocalizationFile(ref lcl, def, dir);
                    }
                    catch (Exception ex)
                    {
                        command.LogError("Error updating localization file.");
                        command.LogError(ex);
                    }
                }

                file.Translations = lcl;
            }
            else
            {
                command.LogWarning("When " + nameof(ILocalizedCommand.Translations).Format(false) + " is predefined," +
                                   "it's better to inherit " + typeof(ILocalizedCommand).Format() + " instead.");
            }
        }
        else if (command is ILocalizedCommand { Translations: null })
        {
            command.LogWarning("No localization data found. Inherit " + typeof(ICommandLocalizationFile).Format() + " to read from a file instead.");
        }

        bool added = false;
        for (int i = 0; i < _registeredCommands.Count; ++i)
        {
            if (_registeredCommands[i].Priority < command.Priority)
            {
                _registeredCommands.Insert(i, command);
                added = true;
                break;
            }
        }

        if (!added)
            _registeredCommands.Add(command);

        if (command is ILocalizedCommand localizedCommand and ICachedTranslationSourceCommand { TranslationSource: null } cachedTranslationSourceCommand)
        {
            cachedTranslationSourceCommand.TranslationSource = TranslationSource.FromCommand(localizedCommand);
        }

        Color clr = new Color32(230, 77, 0, 255);
        if (command.Plugin != null)
            command.LogInfo("Registered from assembly: ".Colorize(clr) + command.Plugin.Assembly.Assembly.GetName().Name.Format() + ".".Colorize(clr), ConsoleColor.DarkGray);
        else if (command is VanillaCommand)
            command.LogInfo("Registered from ".Colorize(clr) + "Unturned".Colorize(DevkitServerModule.UnturnedColor) + ".".Colorize(clr), ConsoleColor.DarkGray);
        else
            command.LogInfo("Registered from ".Colorize(clr) + DevkitServerModule.MainLocalization.format("Name").Colorize(DevkitServerModule.ModuleColor) + ".".Colorize(clr), ConsoleColor.DarkGray);

        EventOnCommandRegistered.TryInvoke(command);

        return true;
    }

    public virtual bool TryDeregisterCommand(IExecutableCommand command)
    {
        int ind = _registeredCommands.IndexOf(command);
        if (ind < 0)
            return false;

        _registeredCommands.RemoveAt(ind);

        EventOnCommandDeregistered.TryInvoke(command);
        return true;
    }

    public virtual void HandleCommandException(CommandContext ctx, Exception ex)
    {
        Logger.LogError("Error while executing " + ctx.Format() + ".");
        Logger.LogError(ex);
        if (!ctx.IsConsole)
            ctx.Reply("Exception", ex.GetType().Name);
    }

    internal void TryInvokeOnCommandExecuted(
#if SERVER
        SteamPlayer? user,
#endif
        CommandContext ctx
    ) => EventOnCommandExecuted.TryInvoke(
#if SERVER
        user,
#endif
        ctx);
}
