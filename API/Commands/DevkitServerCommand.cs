using Cysharp.Threading.Tasks;
using DevkitServer.API.Abstractions;
using DevkitServer.API.Permissions;
using DevkitServer.Plugins;

namespace DevkitServer.API.Commands;

/// <summary>
/// Default <see cref="IExecutableCommand"/> implementation. If you need to limit execution to one of this command at a time, use <see cref="SynchronizedDevkitServerCommand"/>.
/// </summary>
public abstract class DevkitServerCommand(string name, int priority = 0) : ICachedTranslationSourceCommand
{
    public virtual CommandExecutionMode Mode => CommandExecutionMode.RequireMultiEditing;
    public virtual bool AnyPermissions => true;
    public string CommandName { get; } = name;
    public int Priority { get; } = priority;
    public IList<string> Aliases { get; } = new List<string>(0);
    public IList<PermissionLeaf> Permissions { get; } = new List<PermissionLeaf>(0);
    public IDevkitServerPlugin? Plugin { get; set; }
    public abstract UniTask Execute(CommandContext ctx, CancellationToken token);
#if SERVER
    public virtual bool CheckPermission(SteamPlayer? user) => user.CheckPermission(Permissions, AnyPermissions);
#else
    public virtual bool CheckPermission() => true;
#endif
    protected void AddAlias(string alias) => Aliases.Add(alias);
    protected void AddPermission(PermissionLeaf permission) => Permissions.Add(permission);
    ITranslationSource ICachedTranslationSourceCommand.TranslationSource { get; set; } = null!;
    string ILogSource.Source => CommandName.ToUpperInvariant().Colorize(Plugin.GetColor()) + " CMD";
    void IDevkitServerLogger.AddLog(ITerminal terminal, object? source, Severity severity, ReadOnlySpan<char> message, Exception? exception, int baseColorArgb)
    {
        if (Plugin == null)
            Logger.DevkitServer.AddLog(terminal, source, severity, message, exception, baseColorArgb);
        else
            Plugin.AddLog(terminal, source, severity, message, exception, baseColorArgb);
    }
}

/// <summary>
/// Async command implementation that only allows one execution of this command to run at once.
/// </summary>
public abstract class SynchronizedDevkitServerCommand(string name, int priority = 0) : DevkitServerCommand(name, priority), ISynchronizedCommand
{
    SemaphoreSlim ISynchronizedCommand.Semaphore { get; set; } = null!;
}