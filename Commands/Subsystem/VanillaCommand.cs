using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DevkitServer.API;
using DevkitServer.API.Commands;
using DevkitServer.API.Permissions;
using DevkitServer.Players;

namespace DevkitServer.Commands.Subsystem;

[Ignore]
internal sealed class VanillaCommand : IExecutableCommand
{
    public Command Command { get; }
    bool IExecutableCommand.Asynchronous => false;
    bool IExecutableCommand.AnyPermissions => true;
    public string CommandName => Command.command;
    int IExecutableCommand.Priority => 0;
    public IList<string> Aliases { get; } = new List<string>(0);
    public IList<Permission> Permissions { get; }
    IDevkitServerPlugin? IExecutableCommand.Plugin { get; set; }
    public VanillaCommand(Command command)
    {
        Command = command;
        Permissions = new List<Permission>(1) { new Permission(Command.command.ToLowerInvariant(), null, true, false) };
    }
    void IExecutableCommand.Execute(CommandContext ctx)
    {
        Command.check(ctx.IsConsole ? CSteamID.Nil : ctx.Caller.SteamId, CommandName, string.Join("/", ctx.Arguments));
    }

    Task IExecutableCommand.ExecuteAsync(CommandContext ctx, CancellationToken token) => throw new NotImplementedException();

#if SERVER
    public bool CheckPermission(EditorUser? user) => user.CheckPermission(Permissions, (this as IExecutableCommand).AnyPermissions);
#else
    public bool CheckPermission() => Command.ClientHasPermissionToRun();
#endif
}
