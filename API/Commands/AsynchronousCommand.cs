using DevkitServer.API.Permissions;
using DevkitServer.Commands.Subsystem;
using DevkitServer.Players;

namespace DevkitServer.API.Commands;

/// <summary>
/// Async command implementation. If you need to limit execution to one of this command at a time, use <see cref="AsyncSynchronizedCommand"/>.
/// </summary>
public abstract class AsynchronousCommand : IExecutableCommand
{
    public bool Asynchronous => true;
    public virtual bool AnyPermissions => true;
    public string CommandName { get; }
    public int Priority { get; }
    public IList<string> Aliases { get; } = new List<string>(0);
    public IList<Permission> Permissions { get; } = new List<Permission>(0);
    public IDevkitServerPlugin? Plugin { get; set; }
    protected AsynchronousCommand(string name, int priority = 0)
    {
        CommandName = name;
        Priority = priority;
    }

    void IExecutableCommand.Execute(CommandContext ctx) => throw new NotImplementedException();
    public abstract Task ExecuteAsync(CommandContext ctx, CancellationToken token);
#if SERVER
    public bool CheckPermission(EditorUser? user) => user.CheckPermission(Permissions, AnyPermissions);
#else
    public bool CheckPermission() => true;
#endif

    protected void AddAlias(string alias) => Aliases.Add(alias);
    protected void AddPermission(Permission permission) => Permissions.Add(permission);
}

/// <summary>
/// Async command implementation that only allows one execution of this command to run at once.
/// </summary>
public abstract class AsyncSynchronizedCommand : AsynchronousCommand, ISynchronizedAsyncCommand
{
    SemaphoreSlim ISynchronizedAsyncCommand.Semaphore { get; set; } = null!;
    protected AsyncSynchronizedCommand(string name, int priority = 0) : base(name, priority) { }
}