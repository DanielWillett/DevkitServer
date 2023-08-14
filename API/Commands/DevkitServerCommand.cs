using Cysharp.Threading.Tasks;
using DevkitServer.API.Abstractions;
using DevkitServer.API.Permissions;
#if SERVER
using DevkitServer.Players;
#endif

namespace DevkitServer.API.Commands;

/// <summary>
/// Default <see cref="IExecutableCommand"/> implementation. If you need to limit execution to one of this command at a time, use <see cref="SynchronizedDevkitServerCommand"/>.
/// </summary>
public abstract class DevkitServerCommand : ICachedTranslationSourceCommand
{
    public virtual bool AnyPermissions => true;
    public string CommandName { get; }
    public int Priority { get; }
    public IList<string> Aliases { get; } = new List<string>(0);
    public IList<Permission> Permissions { get; } = new List<Permission>(0);
    public IDevkitServerPlugin? Plugin { get; set; }
    protected DevkitServerCommand(string name, int priority = 0)
    {
        CommandName = name;
        Priority = priority;
    }
    
    public abstract UniTask Execute(CommandContext ctx, CancellationToken token);
#if SERVER
    public virtual bool CheckPermission(EditorUser? user) => user.CheckPermission(Permissions, AnyPermissions);
#else
    public virtual bool CheckPermission() => true;
#endif

    protected void AddAlias(string alias) => Aliases.Add(alias);
    protected void AddPermission(Permission permission) => Permissions.Add(permission);
    ITranslationSource ICachedTranslationSourceCommand.TranslationSource { get; set; } = null!;
}

/// <summary>
/// Async command implementation that only allows one execution of this command to run at once.
/// </summary>
public abstract class SynchronizedDevkitServerCommand : DevkitServerCommand, ISynchronizedCommand
{
    SemaphoreSlim ISynchronizedCommand.Semaphore { get; set; } = null!;
    protected SynchronizedDevkitServerCommand(string name, int priority = 0) : base(name, priority) { }
}