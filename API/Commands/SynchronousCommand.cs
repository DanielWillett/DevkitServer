using DevkitServer.API.Permissions;
using DevkitServer.Commands.Subsystem;
#if SERVER
using DevkitServer.Players;
#endif

namespace DevkitServer.API.Commands;

/// <summary>
/// Synchronous command implementation. Runs on game thread.
/// </summary>
public abstract class SynchronousCommand : IExecutableCommand
{
    public bool Asynchronous => false;
    public virtual bool AnyPermissions => true;
    public string CommandName { get; }
    public int Priority { get; }
    public IList<string> Aliases { get; } = new List<string>(0);
    public IList<Permission> Permissions { get; } = new List<Permission>(0);
    public IDevkitServerPlugin? Plugin { get; set; }
    protected SynchronousCommand(string name, int priority = 0)
    {
        CommandName = name;
        Priority = priority;
    }

    public abstract void Execute(CommandContext ctx);
    Task IExecutableCommand.ExecuteAsync(CommandContext ctx, CancellationToken token) => throw new NotImplementedException();
#if SERVER
    public bool CheckPermission(EditorUser? user) => user.CheckPermission(Permissions, AnyPermissions);
#else
    public bool CheckPermission() => true;
#endif

    protected void AddAlias(string alias) => Aliases.Add(alias);
    protected void AddPermission(Permission permission) => Permissions.Add(permission);
}
