using DevkitServer.Core.Commands.Subsystem;
#if SERVER
using DevkitServer.Players;
#endif

namespace DevkitServer.API.Commands;
public interface ICommandHandler
{
    IReadOnlyList<IExecutableCommand> Commands { get; }

    event CommandHandler.ExecutedCommand OnCommandExecuted;
    event CommandHandler.ExecutingCommand OnExecutingCommand;
    event Action<IExecutableCommand> OnCommandRegistered;
    event Action<IExecutableCommand> OnCommandDeregistered;

    void Init();
    void ExecuteCommand(IExecutableCommand command,
#if SERVER
        SteamPlayer? user,
#endif
#if CLIENT
        bool console,
#endif
        string[] args, string originalMessage);
    void SendHelpMessage(
#if SERVER
        SteamPlayer? user
#endif
#if CLIENT
        bool console
#endif
        );
    void SendNoPermissionMessage(
#if SERVER
        SteamPlayer? user,
#endif
#if CLIENT
        bool console,
#endif
        IExecutableCommand command);
    bool TryRegisterCommand(IExecutableCommand command);
    bool TryDeregisterCommand(IExecutableCommand command);
    void HandleCommandException(CommandContext ctx, Exception ex);
#if CLIENT
    void TransitionCommandExecutionToServer(CommandContext ctx);
#endif
}