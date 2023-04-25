using DevkitServer.API;
using DevkitServer.API.Commands;
using DevkitServer.Configuration;
using DevkitServer.Multiplayer;
using DevkitServer.Players;

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
    private readonly List<IExecutableCommand> _registeredCommands = new List<IExecutableCommand>();
    public IReadOnlyList<IExecutableCommand> Commands { get; }

    public event ExecutedCommand? OnCommandExecuted;
    public event ExecutingCommand? OnExecutingCommand;
    public event Action<IExecutableCommand>? OnCommandRegistered;
    public event Action<IExecutableCommand>? OnCommandDeregistered;

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
    }


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
                Logger.LogError("Plugin threw an error in " + this.GetType().Format() + "." + nameof(OnCommandRegistered) + ".");
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
                Logger.LogError("Plugin threw an error in " + this.GetType().Format() + "." + nameof(OnCommandDeregistered) + ".");
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
                Logger.LogError("Plugin threw an error in " + this.GetType().Format() + "." + nameof(OnCommandExecuted) + ".");
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
                Logger.LogError("Plugin threw an error in " + this.GetType().Format() + "." + nameof(OnExecutingCommand) + ".");
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
        string[] args, string originalMessage)
    {
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
                command);
            return;
        }

        CommandContext ctx = new CommandContext(command, args, originalMessage
#if SERVER
        , user
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
                inputmessage, ref shouldHandle, false))
        {
#if SERVER
            SendHelpMessage(null);
#else
            // run as server command or send chat
            if (Provider.isConnected)
                ChatManager.sendChat(EChatMode.LOCAL, inputmessage[0] == '/' ? inputmessage : ("/" + inputmessage));
            else
                SendHelpMessage();
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
        )
    {
        string tr = DevkitServerModule.CommandLocalization.format("UnknownCommand");
#if SERVER
        if (user != null)
        {
            if (DevkitServerModule.IsMainThread)
                ChatManager.say(user.SteamId, tr, Palette.AMBIENT, EChatMode.SAY, true);
            else
                DevkitServerUtility.QueueOnMainThread(() => ChatManager.say(user.SteamId, tr, Palette.AMBIENT, EChatMode.SAY, true));
        }
        else
#endif
        Logger.LogInfo(DevkitServerUtility.RemoveRichText(tr), ConsoleColor.Red);
    }

    public virtual void SendNoPermissionMessage(
#if SERVER
        EditorUser? user,
#endif
        IExecutableCommand command)
    {
        string tr = DevkitServerModule.CommandLocalization.format("NoPermissions");
#if SERVER
        if (user != null)
        {
            if (DevkitServerModule.IsMainThread)
                ChatManager.say(user.SteamId, tr, Palette.AMBIENT, EChatMode.SAY, true);
            else
                DevkitServerUtility.QueueOnMainThread(() => ChatManager.say(user.SteamId, tr, Palette.AMBIENT, EChatMode.SAY, true));
        }
        else
#endif
        Logger.LogInfo(DevkitServerUtility.RemoveRichText(tr), ConsoleColor.Red);
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
        {

        }
    }
}
