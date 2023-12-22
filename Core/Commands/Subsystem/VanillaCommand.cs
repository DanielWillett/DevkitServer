using Cysharp.Threading.Tasks;
using DevkitServer.API;
using DevkitServer.API.Abstractions;
using DevkitServer.API.Commands;
using DevkitServer.API.Permissions;

namespace DevkitServer.Core.Commands.Subsystem;

[Ignore]
internal sealed class VanillaCommand : ICachedTranslationSourceCommand, IEquatable<IExecutableCommand>
{
    private readonly CommandExecutionMode _mode;
    private readonly bool _isDedicatedServerOnly;
    private readonly bool _isServerOnly;
    private readonly bool _isStartupOnly;
    private readonly bool _isConsoleOnly;
    public Command Command { get; }
    public CommandExecutionMode Mode => _mode;
    bool IExecutableCommand.AnyPermissions => true;
    public string CommandName => Command.command;
    int IExecutableCommand.Priority => 0;
    public IList<string> Aliases { get; } = new List<string>(0);
    public IList<PermissionLeaf> Permissions { get; }
    IDevkitServerPlugin? IExecutableCommand.Plugin { get; set; }
    public PermissionLeaf Permission { get; }
    public VanillaCommand(Command command)
    {
        Command = command;
        PermissionLeaf permission = new PermissionLeaf("commands." + Command.command.ToLowerInvariant(), true, false);
        Permissions = new List<PermissionLeaf>(1) { permission };
        Permission = permission;

        VanillaCommandInfo.GetInfo(command.GetType(), out _mode, out _isConsoleOnly, out _isDedicatedServerOnly, out _isServerOnly, out _isStartupOnly);
    }
    UniTask IExecutableCommand.Execute(CommandContext ctx, CancellationToken token)
    {
        Command.check(ctx.IsConsole ? CSteamID.Nil : ctx.Caller.playerID.steamID, CommandName, string.Join("/", ctx.Arguments));
        return UniTask.CompletedTask;
    }

#if SERVER
    public bool CheckPermission(SteamPlayer? user) => CheckVanillaPermissions(user == null) && user.CheckPermission(Permissions, (this as IExecutableCommand).AnyPermissions);
#else
    public bool CheckPermission() => CheckVanillaPermissions(true);
#endif
    ITranslationSource ICachedTranslationSourceCommand.TranslationSource { get; set; } = null!;

    private bool CheckVanillaPermissions(bool terminal)
    {
        if (_isConsoleOnly && !terminal)
            return false;

        if (_isServerOnly && !Provider.isServer)
            return false;

        if (_isDedicatedServerOnly && !Dedicator.IsDedicatedServer)
            return false;

        if (_isStartupOnly && (Dedicator.isStandaloneDedicatedServer ? Provider.isServer : (Level.isLoading || Level.isLoaded)))
            return false;

        return true;
    }

    public bool Equals(IExecutableCommand? other) => other != null && CommandName.Equals(other.CommandName, StringComparison.Ordinal);
    public override bool Equals(object? obj) => ReferenceEquals(this, obj) || obj is IExecutableCommand other && Equals(other);
    public override int GetHashCode() => CommandName.GetHashCode();
    public static bool operator ==(VanillaCommand? left, VanillaCommand? right) => Equals(left, right);
    public static bool operator !=(VanillaCommand? left, VanillaCommand? right) => !Equals(left, right);
    public string Format(ITerminalFormatProvider provider) => Command.GetType().Format();
    void IDevkitServerLogger.AddLog(ITerminal terminal, object? source, Severity severity, ReadOnlySpan<char> message, Exception? exception, int baseColorArgb)
        => Logger.Unturned.AddLog(terminal, source, severity, message, exception, baseColorArgb);
    string ILogSource.Source => Command.command.ToUpperInvariant() + " CMD";
}