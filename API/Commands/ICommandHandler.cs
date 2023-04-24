using DevkitServer.API.Permissions;
using DevkitServer.Commands.Subsystem;
using DevkitServer.Players;

namespace DevkitServer.API.Commands;
public interface ICommandHandler
{
    IReadOnlyList<IExecutableCommand> Commands { get; }

    event CommandHandler.ExecutedCommand? OnCommandExecuted;
    event CommandHandler.ExecutingCommand? OnExecutingCommand;
    event Action<IExecutableCommand>? OnCommandRegistered;
    event Action<IExecutableCommand>? OnCommandDeregistered;

    void Init();
    void ExecuteCommand(IExecutableCommand command,
#if SERVER
        EditorUser? user,
#endif
        string[] args, string originalMessage);
    void SendHelpMessage(
#if SERVER
        EditorUser? user
#endif
        );
    void SendNoPermissionMessage(
#if SERVER
        EditorUser? user,
#endif
        IExecutableCommand command);
    bool TryRegisterCommand(IExecutableCommand command);
    bool TryDeregisterCommand(IExecutableCommand command);
    void HandleCommandException(CommandContext ctx, Exception ex);
}
