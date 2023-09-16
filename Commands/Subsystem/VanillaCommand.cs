using Cysharp.Threading.Tasks;
using DevkitServer.API;
using DevkitServer.API.Abstractions;
using DevkitServer.API.Commands;
using DevkitServer.API.Permissions;
#if SERVER
using DevkitServer.Players;
#endif

namespace DevkitServer.Commands.Subsystem;

[Ignore]
internal sealed class VanillaCommand : ICachedTranslationSourceCommand
{
    public Command Command { get; }
    bool IExecutableCommand.AnyPermissions => true;
    public string CommandName => Command.command;
    int IExecutableCommand.Priority => 0;
    public IList<string> Aliases { get; } = new List<string>(0);
    public IList<Permission> Permissions { get; }
    IDevkitServerPlugin? IExecutableCommand.Plugin { get; set; }
    public VanillaCommand(Command command)
    {
        Command = command;
        Permissions = new List<Permission>(1) { new Permission(Command.command.ToLowerInvariant(), true, false) };
        UserPermissions.Handler.Register(Permissions[0]);
    }
    UniTask IExecutableCommand.Execute(CommandContext ctx, CancellationToken token)
    {
        Command.check(ctx.IsConsole ? CSteamID.Nil : ctx.Caller.SteamId, CommandName, string.Join("/", ctx.Arguments));
        return UniTask.CompletedTask;
    }

#if SERVER
    public bool CheckPermission(EditorUser? user) => user.CheckPermission(Permissions, (this as IExecutableCommand).AnyPermissions);
#else
    public bool CheckPermission() => Command.ClientHasPermissionToRun();
#endif
    ITranslationSource ICachedTranslationSourceCommand.TranslationSource { get; set; }
}
