using DevkitServer.Plugins;
using DevkitServer.Util.Encoding;
using StackCleaner;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevkitServer.API.Permissions;

/// <summary>
/// Represents a relative permission with wildcards and additive/subtractive metadata.
/// </summary>
[JsonConverter(typeof(PermissionBranchConverter))]
public readonly struct PermissionBranch : IEquatable<PermissionBranch>, IEquatable<PermissionLeaf>
{
    /// <summary>
    /// Additive superuser branch. Unlocks all permissions.
    /// </summary>
    public static readonly PermissionBranch Superuser = new PermissionBranch(PermissionMode.Additive);

    /// <summary>
    /// Removes a superuser permission branch which would've unlocked all permissions. Doesn't necessarily remove all permissions.
    /// </summary>
    public static readonly PermissionBranch SuperuserSubtractive = new PermissionBranch(PermissionMode.Subtractive);

    private readonly byte _flags;

    /// <summary>
    /// The path relative to the branch's domain. The domain is the permission prefix of the permission owner.
    /// </summary>
    /// <remarks>Example: <c>control.*</c>.</remarks>
    public string Path { get; }

    /// <summary>
    /// Permission depth level (number of periods plus one) where the wildcard is at, or zero for absolute permissions.
    /// </summary>
    /// <remarks>Example: <c>devkitserver::control.*</c> would be 2.</remarks>
    public int WildcardLevel { get; }

    /// <summary>
    /// How this branch affects existing permissions. Additive gives the permission, subtractive removes it.
    /// </summary>
    public PermissionMode Mode => (PermissionMode)((_flags >> 3) & 1);

    /// <summary>
    /// If this branch doesn't contain a wildcard.
    /// </summary>
    public bool IsAbsolute => WildcardLevel <= 0;

    /// <summary>
    /// Plugin that registered this permission. Only <see langword="null"/> when <see cref="Core"/> or <see cref="DevkitServer"/> are <see langword="true"/>.
    /// </summary>
    public IDevkitServerPlugin? Plugin { get; }

    /// <summary>
    /// Is this permission relating to the base game?
    /// </summary>
    public bool Core => (_flags & 0b001) != 0;

    /// <summary>
    /// Is this permission relating to the DevkitServer module?
    /// </summary>
    public bool DevkitServer => (_flags & 0b010) != 0;

    /// <summary>
    /// Is this the superuser permission (unlocks all permissions).
    /// </summary>
    public bool IsSuperuser => (_flags & 0b100) != 0;

    /// <summary>
    /// Is this permission valid (has a path and a source definition)?
    /// </summary>
    public bool Valid => ((_flags & 0b111) is not 0 and not 0b111 and not 0b110 and not 0b101 and not 0b011 || Plugin != null) && !string.IsNullOrWhiteSpace(Path);

    /// <summary>
    /// Parse a permission branch with a prefix.
    /// </summary>
    /// <exception cref="FormatException">Parse failure.</exception>
    public PermissionBranch(string path)
    {
        this = Parse(path);
    }

    internal PermissionBranch(PermissionMode mode)
    {
        _flags = (byte)(((int)mode << 2) | 0b100);
        Path = "*";
        WildcardLevel = 1;
    }
    internal PermissionBranch(PermissionMode mode, string path, IDevkitServerPlugin plugin)
    {
        Plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        WildcardLevel = GetWildcardLevel(path);
        Path = path;
        _flags = (byte)((int)mode << 2);
    }
    internal PermissionBranch(PermissionMode mode, string path, bool core = false, bool devkitServer = false)
    {
        _flags = (byte)(((int)mode << 2) | (core ? 0b001 : 0) | (devkitServer ? 0b010 : 0));
        WildcardLevel = GetWildcardLevel(path);
        Path = path;
    }

    /// <summary>
    /// Get the wildcard level in a path. Example: <c>devkitserver::control.*</c> would return 2. If there's no wildcard zero is returned.
    /// </summary>
    /// <remarks>Can optionally have a prefix.</remarks>
    public static int GetWildcardLevel(string path)
    {
        if (path.Length == 1 && path[0] == '*')
            return 1;

        int prefixSeparator = -1;

        do
        {
            prefixSeparator = path.IndexOf(':', prefixSeparator + 1);
        }
        while (prefixSeparator > 0 && prefixSeparator < path.Length - 1 && path[prefixSeparator + 1] != ':');

        int ct = 1;
        bool foundWildcard = false;
        for (int i = prefixSeparator > 0 ? prefixSeparator + 2 : 0; i < path.Length; ++i)
        {
            char c = path[i];
            if (c == '.') ++ct;
            else if (c == '*')
            {
                foundWildcard = true;
                break;
            }
        }

        return foundWildcard ? ct : 0;
    }

    /// <summary>
    /// Get the permission prefix.
    /// </summary>
    /// <remarks>Looks like this when placed in a full permission branch: <c>devkitserver::control.*</c>.</remarks>
    public string GetPrefix()
    {
        if (Core)
            return PermissionLeaf.CoreModulePrefix;

        if (DevkitServer)
            return PermissionLeaf.DevkitServerModulePrefix;

        if (Plugin == null || string.IsNullOrWhiteSpace(Plugin.PermissionPrefix))
            return PermissionLeaf.InvalidPrefix;

        return Plugin.PermissionPrefix;
    }

    public override string ToString()
    {
        string str = !IsSuperuser ? GetPrefix() + "::" + Path : "*";

        if (Mode == PermissionMode.Subtractive)
            str = "-" + str;
        
        return str;
    }
    private static readonly Color32 SubtractiveColor = new Color32(255, 80, 80, 255);
    public string Format(ITerminalFormatProvider provider)
    {
        if (provider.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.None)
            return ToString();

        string str = GetPrefix().Colorize(Core ? DevkitServerModule.UnturnedColor : Plugin.GetColor()) +
               "::".Colorize(FormattingColorType.Punctuation) +
               Path.Colorize(FormattingColorType.Struct);

        if (Mode == PermissionMode.Subtractive)
            str = "-".Colorize(SubtractiveColor) + str;

        return str;
    }

    public bool Equals(PermissionLeaf leaf)
    {
        if (WildcardLevel > 0 || IsSuperuser)
            return false;

        return Core == leaf.Core
               && DevkitServer == leaf.DevkitServer
               && Equals(Plugin, leaf.Plugin)
               && string.Equals(Path, leaf.Path, StringComparison.InvariantCultureIgnoreCase);
    }
    public bool Equals(PermissionBranch branch)
    {
        return WildcardLevel == branch.WildcardLevel
               && _flags == branch._flags
               && Equals(Plugin, branch.Plugin)
               && string.Equals(Path, branch.Path, StringComparison.InvariantCultureIgnoreCase);
    }
    public bool EqualsWithoutMode(PermissionBranch branch)
    {
        return WildcardLevel == branch.WildcardLevel
               && (_flags & ~(1 << 3)) == (branch._flags & ~(1 << 3))
               && Equals(Plugin, branch.Plugin)
               && string.Equals(Path, branch.Path, StringComparison.InvariantCultureIgnoreCase);
    }
    public override int GetHashCode() => ~(Plugin == null ? HashCode.Combine(_flags, Path) : HashCode.Combine(Plugin, Path));
    public override bool Equals(object? obj)
    {
        if (obj is PermissionBranch branch)
            return Equals(branch);

        if (obj is PermissionLeaf leaf)
            return Equals(leaf);

        return false;
    }

    /// <summary>
    /// Parse a permission branch with a prefix.
    /// </summary>
    /// <exception cref="FormatException">Parse failure.</exception>
    public static PermissionBranch Parse(string path)
    {
        if (!TryParse(path, out PermissionBranch leaf))
            throw new FormatException(leaf.Path != null ? "Unable to find prefix domain for permission branch." : "Unable to parse permission branch.");

        return leaf;
    }

    /// <summary>
    /// Parse a permission branch with a prefix.
    /// </summary>
    /// <returns><see langword="true"/> after a successful parse, otherwise <see langword="false"/>.</returns>
    public static bool TryParse(string path, out PermissionBranch permissionBranch)
    {
        permissionBranch = default;
        if (string.IsNullOrEmpty(path))
            return false;

        char firstChar = path[0];
        PermissionMode mode = firstChar == '-' ? PermissionMode.Subtractive : PermissionMode.Additive;

        if (path.Length == 1 && path[0] == '*')
        {
            permissionBranch = new PermissionBranch(PermissionMode.Additive);
            return true;
        }
        if (path.Length == 2 && path[1] == '*' && path[0] is '-' or '+')
        {
            permissionBranch = new PermissionBranch(mode);
            return true;
        }

        int startIndex = firstChar is '+' or '-' || path.Length > 1 && firstChar == '\\' && path[1] is '+' or '-' ? 0 : -1;
        int prefixSeparator = startIndex;

        do
        {
            prefixSeparator = path.IndexOf(':', prefixSeparator + 1);
            if (prefixSeparator <= 0 || prefixSeparator >= path.Length - 2)
                return false;
        }
        while (path[prefixSeparator + 1] != ':');

        ReadOnlySpan<char> prefix = path.AsSpan(startIndex + 1, prefixSeparator - startIndex - 1);

        prefixSeparator += 2;

        int wildcardIndex = path.IndexOf('*', prefixSeparator) + 1;
        if (wildcardIndex <= 0)
            wildcardIndex = path.Length;

        string value = path[prefixSeparator..wildcardIndex];

        if (string.IsNullOrWhiteSpace(value) || prefix.IsWhiteSpace())
            return false;

        if (prefix.Equals(PermissionLeaf.CoreModulePrefix, StringComparison.InvariantCultureIgnoreCase))
        {
            permissionBranch = new PermissionBranch(mode, value, core: true);
            return true;
        }

        if (prefix.Equals(PermissionLeaf.DevkitServerModulePrefix, StringComparison.InvariantCultureIgnoreCase))
        {
            permissionBranch = new PermissionBranch(mode, value, devkitServer: true);
            return true;
        }

        foreach (IDevkitServerPlugin plugin in PluginLoader.Plugins)
        {
            if (prefix.Equals(plugin.PermissionPrefix, StringComparison.InvariantCultureIgnoreCase))
            {
                permissionBranch = new PermissionBranch(mode, value, plugin);
                return true;
            }
        }

        permissionBranch = new PermissionBranch(mode, value, false, false);
        return false;
    }

    public static void Write(ByteWriter writer, PermissionBranch branch)
    {
        byte flags = (byte)(branch._flags | (branch.Plugin == null ? 1 << 7 : 0) | (branch.Path.Length > byte.MaxValue ? 1 << 6 : 0));

        if ((flags & 0b10000011) == 0)
            flags |= (byte)(branch.Plugin!.PermissionPrefix.Length > byte.MaxValue ? 1 << 5 : 0);

        writer.Write(flags);

        // superuser
        if ((flags & 0b100) != 0)
            return;

        if ((flags & 0b10000011) == 0)
        {
            if ((flags & (1 << 5)) != 0)
                writer.Write(branch.Plugin!.PermissionPrefix);
            else
                writer.WriteShort(branch.Plugin!.PermissionPrefix);
        }

        if ((flags & (1 << 6)) != 0)
            writer.Write(branch.Path);
        else
            writer.WriteShort(branch.Path);
    }

    public static PermissionBranch Read(ByteReader reader)
    {
        byte flags = reader.ReadUInt8();

        PermissionMode mode = (PermissionMode)((flags >> 3) & 1);

        // superuser
        if ((flags & 0b100) != 0)
            return new PermissionBranch(mode);

        string? pluginPrefix = null;
        if ((flags & 0b10000011) == 0)
            pluginPrefix = (flags & (1 << 5)) != 0 ? reader.ReadString() : reader.ReadShortString();

        IDevkitServerPlugin? plugin = null;
        if (pluginPrefix != null)
        {
            foreach (IDevkitServerPlugin pl in PluginLoader.Plugins)
            {
                if (!string.Equals(pl.PermissionPrefix, pluginPrefix, StringComparison.InvariantCultureIgnoreCase))
                    continue;

                plugin = pl;
                break;
            }
        }

        string path = (flags & (1 << 6)) != 0 ? reader.ReadString() : reader.ReadShortString();
        return plugin == null
            ? new PermissionBranch(mode, path, core: (flags & 0b001) != 0, devkitServer: (flags & 0b010) != 0)
            : new PermissionBranch(mode, path, plugin);
    }

    /// <summary>
    /// If <paramref name="leaf"/> would be included in this branch.
    /// </summary>
    /// <remarks>Does not check <see cref="Mode"/>.</remarks>
    public bool Contains(PermissionLeaf leaf)
    {
        if (IsSuperuser)
            return true;

        if (leaf.Core != Core || leaf.DevkitServer != DevkitServer || !Equals(leaf.Plugin, Plugin))
            return false;

        if (WildcardLevel == 0)
            return Path.Equals(leaf.Path, StringComparison.InvariantCultureIgnoreCase);
        
        return leaf.Level >= WildcardLevel && leaf.Path.AsSpan().StartsWith(Path.AsSpan(0, Path.Length - 1), StringComparison.InvariantCultureIgnoreCase);
    }

    /// <summary>
    /// If <paramref name="branch"/> (and any leaves it contains) would be included in this branch.
    /// </summary>
    /// <remarks>Does not check <see cref="Mode"/>.</remarks>
    public bool Contains(PermissionBranch branch)
    {
        if (IsSuperuser)
            return true;

        if (branch.Core != Core || branch.DevkitServer != DevkitServer || !Equals(branch.Plugin, Plugin))
            return false;

        if (WildcardLevel == 0)
            return branch.WildcardLevel == 0 && Path.Equals(branch.Path, StringComparison.InvariantCultureIgnoreCase);

        if (branch.WildcardLevel == 0)
            return branch.Path.AsSpan().StartsWith(Path.AsSpan(0, Path.Length - 1), StringComparison.InvariantCultureIgnoreCase);

        return branch.WildcardLevel >= WildcardLevel &&
               branch.Path.AsSpan(0, branch.Path.Length - 1)
                          .StartsWith(Path.AsSpan(0, Path.Length - 1), StringComparison.InvariantCultureIgnoreCase);
    }

    public static explicit operator PermissionLeaf(PermissionBranch branch)
    {
        if ((branch._flags & 0b100) != 0)
            throw new InvalidCastException("Can not represent superuser branch as a leaf.");

        if (branch.WildcardLevel > 0)
            throw new InvalidCastException("Can not represent branches with wildcards as a leaf.");

        return (branch._flags & 0b011) != 0 || branch.Plugin == null
            ? new PermissionLeaf(branch.Path, (branch._flags & 0b001) != 0, (branch._flags & 0b010) != 0)
            : new PermissionLeaf(branch.Path, branch.Plugin!);
    }
    public static bool operator ==(PermissionBranch left, PermissionBranch right) => left.Equals(right);
    public static bool operator !=(PermissionBranch left, PermissionBranch right) => !left.Equals(right);
}

public sealed class PermissionBranchConverter : JsonConverter<PermissionBranch>
{
    public override PermissionBranch Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return default;
            case JsonTokenType.String:
                string? str = reader.GetString();
                if (string.IsNullOrEmpty(str) || str.Equals("null", StringComparison.Ordinal))
                    return default;
                if (PermissionBranch.TryParse(str, out PermissionBranch leaf))
                    return leaf;

                throw new JsonException("Invalid string value for permission branch: \"" + str + "\".");
            default:
                throw new JsonException("Unexpected token " + reader.TokenType + " while reading permission branch.");
        }
    }

    public override void Write(Utf8JsonWriter writer, PermissionBranch value, JsonSerializerOptions options)
    {
        if (value.Path == null)
            writer.WriteNullValue();
        else
            writer.WriteStringValue(value.ToString());
    }
}