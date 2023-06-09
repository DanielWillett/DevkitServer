using System.Reflection;
using DevkitServer.API;
using DevkitServer.API.Commands;
using DevkitServer.Configuration;
using DevkitServer.Patches;
#if SERVER
using DevkitServer.Multiplayer;
using DevkitServer.Players;
#endif
using HarmonyLib;

namespace DevkitServer.Commands.Subsystem;
/// <summary>
/// Provides an API for interacting with commands, late-registering custom commands, or completely replacing the command implementation.
/// </summary>
/// <remarks>Override to change behavior (or implement <see cref="ICommandHandler"/>) and set <see cref="Handler"/>.</remarks>
public class CommandHandler : ICommandHandler, IDisposable
{
    public static event ExecutedCommand? GlobalOnCommandExecuted;
    public static event ExecutingCommand? GlobalOnExecutingCommand;
    public static event Action<IExecutableCommand>? GlobalOnCommandRegistered;
    public static event Action<IExecutableCommand>? GlobalOnCommandDeregistered;

    public delegate void ExecutingCommand(
#if SERVER
        EditorUser? user,
#endif
        CommandContext context,
        ref bool shouldExecute
    );

    public delegate void ExecutedCommand(
#if SERVER
        EditorUser? user,
# endif
        CommandContext context);

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

    public event ExecutedCommand? OnCommandExecuted;
    public event ExecutingCommand? OnExecutingCommand;
    public event Action<IExecutableCommand>? OnCommandRegistered;
    public event Action<IExecutableCommand>? OnCommandDeregistered;
    internal static bool IsLoggingFromDevkitServer;

    /// <exception cref="NotSupportedException">Called setter on non-game thread.</exception>
    public static ICommandHandler Handler
    {
        get => _handler;
        set
        {
            ThreadUtil.assertIsGameThread();

            value.OnCommandExecuted += TryInvokeGlobalOnCommandExecuted;
            value.OnExecutingCommand += TryInvokeGlobalOnCommandExecuting;
            value.OnCommandRegistered += TryInvokeGlobalOnCommandRegistered;
            value.OnCommandDeregistered += TryInvokeGlobalOnCommandDeregistered;
            ICommandHandler? old = Interlocked.Exchange(ref _handler, value);
            if (old != null)
            {
                foreach (IExecutableCommand command in old.Commands.ToList())
                {
                    old.TryDeregisterCommand(command);
                    value.TryRegisterCommand(command);
                }
                old.OnCommandExecuted -= TryInvokeGlobalOnCommandExecuted;
                old.OnExecutingCommand -= TryInvokeGlobalOnCommandExecuting;
                old.OnCommandRegistered -= TryInvokeGlobalOnCommandRegistered;
                old.OnCommandDeregistered -= TryInvokeGlobalOnCommandDeregistered;
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
                _logPrefix = Accessor.GetMethodInfo(OnLogged);
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
                _chatPrefix = Accessor.GetMethodInfo(OnClientChatted);
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
    protected void TryInvokeOnCommandRegistered(IExecutableCommand command)
    {
        if (OnCommandRegistered == null)
            return;
        foreach (Action<IExecutableCommand> action in OnCommandRegistered.GetInvocationList().Cast<Action<IExecutableCommand>>())
        {
            try
            {
                action(command);
            }
            catch (Exception ex)
            {
                Logger.LogError("Plugin threw an error in " + GetType().Format() + "." + nameof(OnCommandRegistered) + ".");
                Logger.LogError(ex);
            }
        }
    }

    protected void TryInvokeOnCommandDeregistered(IExecutableCommand command)
    {
        if (OnCommandDeregistered == null)
            return;
        foreach (Action<IExecutableCommand> action in OnCommandDeregistered.GetInvocationList().Cast<Action<IExecutableCommand>>())
        {
            try
            {
                action(command);
            }
            catch (Exception ex)
            {
                Logger.LogError("Plugin threw an error in " + GetType().Format() + "." + nameof(OnCommandDeregistered) + ".");
                Logger.LogError(ex);
            }
        }
    }

    private static void TryInvokeGlobalOnCommandRegistered(IExecutableCommand command)
    {
        if (GlobalOnCommandRegistered == null)
            return;
        foreach (Action<IExecutableCommand> action in GlobalOnCommandRegistered.GetInvocationList().Cast<Action<IExecutableCommand>>())
        {
            try
            {
                action(command);
            }
            catch (Exception ex)
            {
                Logger.LogError("Plugin threw an error in " + typeof(CommandHandler).Format() + "." + nameof(GlobalOnCommandRegistered) + ".");
                Logger.LogError(ex);
            }
        }
    }

    private static void TryInvokeGlobalOnCommandDeregistered(IExecutableCommand command)
    {
        if (GlobalOnCommandDeregistered == null)
            return;
        foreach (Action<IExecutableCommand> action in GlobalOnCommandDeregistered.GetInvocationList().Cast<Action<IExecutableCommand>>())
        {
            try
            {
                action(command);
            }
            catch (Exception ex)
            {
                Logger.LogError("Plugin threw an error in " + typeof(CommandHandler).Format() + "." + nameof(GlobalOnCommandDeregistered) + ".");
                Logger.LogError(ex);
            }
        }
    }

    protected void TryInvokeOnCommandExecuted(
#if SERVER
        EditorUser? user,
#endif
        CommandContext ctx)
    {
        if (OnCommandExecuted == null)
            return;
        foreach (ExecutedCommand action in OnCommandExecuted.GetInvocationList().Cast<ExecutedCommand>())
        {
            try
            {
                action(
#if SERVER
                    user,
#endif
                    ctx);
            }
            catch (Exception ex)
            {
                Logger.LogError("Plugin threw an error in " + GetType().Format() + "." + nameof(OnCommandExecuted) + ".");
                Logger.LogError(ex);
            }
        }
    }

    protected void TryInvokeOnCommandExecuting(
#if SERVER
        EditorUser? user,
#endif
        CommandContext ctx,
        ref bool shouldExecute)
    {
        if (OnExecutingCommand == null)
            return;
        foreach (ExecutingCommand action in OnExecutingCommand.GetInvocationList().Cast<ExecutingCommand>())
        {
            try
            {
                action(
#if SERVER
                    user,
#endif
                    ctx, ref shouldExecute);
            }
            catch (Exception ex)
            {
                Logger.LogError("Plugin threw an error in " + GetType().Format() + "." + nameof(OnExecutingCommand) + ".");
                Logger.LogError(ex);
            }
        }
    }

    private static void TryInvokeGlobalOnCommandExecuted(
#if SERVER
        EditorUser? user,
#endif
        CommandContext ctx)
    {
        if (GlobalOnCommandExecuted == null)
            return;
        foreach (ExecutedCommand action in GlobalOnCommandExecuted.GetInvocationList().Cast<ExecutedCommand>())
        {
            try
            {
                action(
#if SERVER
                    user,
#endif
                    ctx);
            }
            catch (Exception ex)
            {
                Logger.LogError("Plugin threw an error in " + typeof(CommandHandler).Format() + "." + nameof(OnCommandExecuted) + ".");
                Logger.LogError(ex);
            }
        }
    }

    private static void TryInvokeGlobalOnCommandExecuting(
#if SERVER
        EditorUser? user,
#endif
        CommandContext ctx,
        ref bool shouldExecute)
    {
        if (GlobalOnExecutingCommand == null)
            return;
        foreach (ExecutingCommand action in GlobalOnExecutingCommand.GetInvocationList().Cast<ExecutingCommand>())
        {
            try
            {
                action(
#if SERVER
                    user,
#endif
                    ctx, ref shouldExecute);
            }
            catch (Exception ex)
            {
                Logger.LogError("Plugin threw an error in " + typeof(CommandHandler).Format() + "." + nameof(OnExecutingCommand) + ".");
                Logger.LogError(ex);
            }
        }
    }

    public virtual void ExecuteCommand(IExecutableCommand command,
#if SERVER
        EditorUser? user, 
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
        TryInvokeOnCommandExecuting(
#if SERVER
            ctx.Caller,
#endif
            ctx, ref shouldExecute);
        if (!shouldExecute)
            return;
        if (command.Asynchronous)
        {
            Task.Run(async () =>
            {
                CancellationToken token = DevkitServerModule.UnloadToken;
                await DevkitServerUtility.ToUpdate(token);
                SemaphoreSlim? waited;
                if (command is ISynchronizedAsyncCommand sync)
                {
                    waited = sync.Semaphore;
                    await waited.WaitAsync(token).ConfigureAwait(false);
                    await DevkitServerUtility.ToUpdate(token);
                }
                else waited = null;

                try
                {
                    await command.ExecuteAsync(ctx, token).ConfigureAwait(false);
                }
                catch (CommandContext) { }
                catch (Exception ex)
                {
                    await DevkitServerUtility.ToUpdate(token);
                    HandleCommandException(ctx, ex);
                }
                finally
                {
                    waited?.Release();
                    TryInvokeOnCommandExecuted(
#if SERVER
                        ctx.Caller,
#endif
                        ctx);
                }
            });
        }
        else
        {
            bool isVanilla = command is VanillaCommand;
            if (isVanilla)
                _activeVanillaCommand = ctx;
            try
            {
                command.Execute(ctx);
            }
            catch (CommandContext) { }
            catch (Exception ex)
            {
                HandleCommandException(ctx, ex);
            }
            finally
            {
                TryInvokeOnCommandExecuted(
#if SERVER
                    ctx.Caller,
#endif
                    ctx);
                if (isVanilla)
                    _activeVanillaCommand = null;
            }
        }
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
            if (Provider.isConnected)
                ChatManager.sendChat(EChatMode.LOCAL, inputmessage[0] == '/' ? inputmessage : ("/" + inputmessage));
            else
                SendHelpMessage(true);
#endif
        }
    }

#if SERVER
    protected virtual void OnChatProcessing(SteamPlayer player, string text, ref bool shouldExecuteCommand, ref bool shouldList)
    {
        shouldExecuteCommand = false;
        EditorUser? user = UserManager.FromSteamPlayer(player);
        if (user != null && !Parser.TryRunCommand(user, text, ref shouldList, true))
            SendHelpMessage(user);
    }
#endif
    public virtual void SendHelpMessage(
#if SERVER
        EditorUser? user
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
        EditorUser? user,
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
        , EditorUser? user
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
                ChatManager.say(user.SteamId, tr, Palette.AMBIENT, EChatMode.SAY, true);
            else
                DevkitServerUtility.QueueOnMainThread(() => ChatManager.say(user.SteamId, tr, Palette.AMBIENT, EChatMode.SAY, true));
        }
        else
            Log(DevkitServerUtility.RemoveRichText(tr));
#else
        if (!console)
        {
            if (DevkitServerModule.IsMainThread)
                ChatManager.receiveChatMessage(CSteamID.Nil, string.Empty, EChatMode.SAY, Palette.AMBIENT, true, tr);
            else
                DevkitServerUtility.QueueOnMainThread(() => ChatManager.receiveChatMessage(CSteamID.Nil, string.Empty, EChatMode.SAY, Palette.AMBIENT, true, tr));
        }
        Log(DevkitServerUtility.RemoveRichText(tr));
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

        if (command.Asynchronous)
        {
            if (command is ISynchronizedAsyncCommand sync)
            {
                if (sync.Semaphore == null)
                    sync.Semaphore = new SemaphoreSlim(1, 1);
                else if (sync.Semaphore.CurrentCount != 1)
                    command.LogWarning("Predefined semaphore should have a 1 initialCount and 1 maxCount to properly synchronize access.");
            }
        }
        if (command is ICommandLocalizationFile file)
        {
            if (file.Translations == null)
            {
                string dir = file.TranslationsDirectory;
                if (string.IsNullOrWhiteSpace(dir))
                    command.LogError("No localization path provided.");
                else
                {
                    dir = Path.Combine(command.Plugin == null ? DevkitServerConfig.CommandLocalizationFilePath : command.Plugin.LocalizationDirectory, dir);
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

        Color clr = new Color32(230, 77, 0, 255);
        if (command.Plugin != null)
            command.LogInfo("Registered from assembly: ".Colorize(clr) + command.Plugin.Assembly.GetName().Name.Format() + ".".Colorize(clr), ConsoleColor.DarkGray);
        else if (command is VanillaCommand)
            command.LogInfo("Registered from ".Colorize(clr) + "Unturned".Colorize(DevkitServerModule.UnturnedColor) + ".".Colorize(clr), ConsoleColor.DarkGray);
        else
            command.LogInfo("Registered from ".Colorize(clr) + DevkitServerModule.MainLocalization.format("Name").Colorize(DevkitServerModule.PluginColor) + ".".Colorize(clr), ConsoleColor.DarkGray);

        TryInvokeOnCommandRegistered(command);

        return true;
    }

    public virtual bool TryDeregisterCommand(IExecutableCommand command)
    {
        int ind = _registeredCommands.IndexOf(command);
        if (ind < 0)
            return false;

        _registeredCommands.RemoveAt(ind);

        TryInvokeOnCommandDeregistered(command);
        return true;
    }

    public virtual void HandleCommandException(CommandContext ctx, Exception ex)
    {
        Logger.LogError("Error while executing " + ctx.Format() + ".");
        Logger.LogError(ex);
        if (!ctx.IsConsole)
            ctx.Reply("Exception", ex.GetType().Name);
    }
}
