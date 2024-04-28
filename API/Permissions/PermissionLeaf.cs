using DanielWillett.SpeedBytes;
using DevkitServer.Plugins;
using StackCleaner;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevkitServer.API.Permissions;

/// <summary>
/// Represents an absolute permission with no wildcards or additive/subtractive metadata.
/// </summary>
[JsonConverter(typeof(PermissionLeafConverter))]
public readonly struct PermissionLeaf : IEquatable<PermissionLeaf>, IEquatable<PermissionBranch>, ITerminalFormattable
{
    /// <summary>
    /// A permission that can never be met.
    /// </summary>
    public static readonly PermissionLeaf Nil = new PermissionLeaf();
    
    /// <summary>
    /// Prefix for Core permissions.
    /// </summary>
    public const string CoreModulePrefix = "unturned";

    /// <summary>
    /// Prefix for DevkitServer module permissions.
    /// </summary>
    public const string DevkitServerModulePrefix = "devkitserver";

    /// <summary>
    /// Prefix for invalid permissions.
    /// </summary>
    public const string InvalidPrefix = "devkitserver";

    private readonly byte _flags;

    /// <summary>
    /// The path relative to the leaf's domain. The domain is the permission prefix of the permission owner.
    /// </summary>
    /// <remarks>Example: <c>control.editor</c>.</remarks>
    public string Path { get; }

    /// <summary>
    /// Permission depth level (number of periods plus one).
    /// </summary>
    public int Level { get; }

    /// <summary>
    /// Plugin that registered this permission. Only <see langword="null"/> when <see cref="Core"/> or <see cref="DevkitServer"/> are <see langword="true"/>.
    /// </summary>
    public IDevkitServerPlugin? Plugin { get; }

    /// <summary>
    /// Is this permission relating to the base game?
    /// </summary>
    public bool Core =>         (_flags & 0b001) != 0;

    /// <summary>
    /// Is this permission relating to the DevkitServer module?
    /// </summary>
    public bool DevkitServer => (_flags & 0b010) != 0;

    /// <summary>
    /// Is this permission valid (has a path and a source definition)?
    /// </summary>
    public bool Valid => ((_flags & 0b011) is not 0 and not 0b011 || Plugin != null) && !string.IsNullOrWhiteSpace(Path);

    /// <summary>
    /// Parse a permission leaf with or without a prefix.
    /// </summary>
    /// <remarks>Permission leafs without prefixes will be invalid.</remarks>
    /// <exception cref="FormatException">Parse failure.</exception>
    public PermissionLeaf(string path)
    {
        if (!TryParse(path, out this) && Path == null)
            throw new FormatException("Unable to parse permission leaf.");
    }
    internal PermissionLeaf(string path, IDevkitServerPlugin plugin)
    {
        Plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        Path = path;
        Level = CountLevels(path);
    }
    internal PermissionLeaf(string path, bool core = false, bool devkitServer = false)
    {
        _flags = (byte)((core ? 0b001 : 0) | (devkitServer ? 0b010 : 0));
        Path = path;
        Level = CountLevels(path);
    }

    /// <summary>
    /// Count the number of levels in a path. Example: <c>devkitserver::control.editor</c> would return 2.
    /// </summary>
    /// <remarks>Can optionally have a prefix.</remarks>
    public static int CountLevels(string path)
    {
        int prefixSeparator = -1;

        do
        {
            prefixSeparator = path.IndexOf(':', prefixSeparator + 1);
        }
        while (prefixSeparator > 0 && prefixSeparator < path.Length - 1 && path[prefixSeparator + 1] != ':');

        int ct = 1;
        for (int i = prefixSeparator > 0 ? prefixSeparator + 2 : 0; i < path.Length; ++i)
        {
            if (path[i] == '.') ++ct;
        }

        return ct;
    }

    /// <summary>
    /// Get the permission prefix.
    /// </summary>
    /// <remarks>Looks like this when placed in a full permission leaf: <c>devkitserver::control.editor</c>.</remarks>
    public string GetPrefix()
    {
        if (Core)
            return CoreModulePrefix;

        if (DevkitServer)
            return DevkitServerModulePrefix;

        if (Plugin == null || string.IsNullOrWhiteSpace(Plugin.PermissionPrefix))
            return InvalidPrefix;

        return Plugin.PermissionPrefix;
    }

    public override string ToString() => GetPrefix() + "::" + Path;
    public string Format(ITerminalFormatProvider provider)
    {
        if (provider.StackCleaner.Configuration.ColorFormatting == StackColorFormatType.None)
            return ToString();

        return GetPrefix().Colorize(Core ? DevkitServerModule.UnturnedColor : Plugin.GetColor()) +
               "::".Colorize(FormattingColorType.Punctuation) +
               Path.Colorize(FormattingColorType.Struct);
    }

    public bool Equals(PermissionBranch branch) => branch.Equals(this);
    public bool Equals(PermissionLeaf leaf)
    {
        return _flags == leaf._flags && Equals(Plugin, leaf.Plugin) && string.Equals(Path, leaf.Path, StringComparison.InvariantCultureIgnoreCase);
    }
    public override bool Equals(object? obj)
    {
        if (obj is PermissionLeaf leaf)
            return Equals(leaf);

        if (obj is PermissionBranch branch)
            return branch.Equals(this);

        return false;
    }

    public override int GetHashCode() => Plugin == null ? HashCode.Combine(_flags, Path) : HashCode.Combine(Plugin, Path);

    /// <summary>
    /// Parse a permission leaf with a prefix.
    /// </summary>
    /// <exception cref="FormatException">Parse failure.</exception>
    public static PermissionLeaf Parse(string path)
    {
        if (!TryParse(path, out PermissionLeaf leaf))
            throw new FormatException(leaf.Path != null ? "Unable to find prefix domain for permission leaf." : "Unable to parse permission leaf.");

        return leaf;
    }

    /// <summary>
    /// Parse a permission leaf with a prefix.
    /// </summary>
    /// <returns><see langword="true"/> after a successful parse, otherwise <see langword="false"/>.</returns>
    public static bool TryParse(string path, out PermissionLeaf permissionLeaf)
    {
        permissionLeaf = default;

        if (string.IsNullOrEmpty(path))
            return false;

        int prefixSeparator = -1;

        do
        {
            prefixSeparator = path.IndexOf(':', prefixSeparator + 1);
            if (prefixSeparator <= 0 || prefixSeparator >= path.Length - 2)
                return false;
        }
        while (path[prefixSeparator + 1] != ':');

        int laterIndex = path.IndexOf(':', prefixSeparator + 2);
        if (laterIndex != -1 && laterIndex < path.Length - 1 && path[laterIndex + 1] == ':')
            return false;

        ReadOnlySpan<char> prefix = path.AsSpan(0, prefixSeparator);

        string value = path[(prefixSeparator + 2)..];
        if (string.IsNullOrWhiteSpace(value) || prefix.IsWhiteSpace())
            return false;

        int wildcardIndex = value.IndexOf('*');

        if (prefix.Equals(CoreModulePrefix, StringComparison.InvariantCultureIgnoreCase))
        {
            permissionLeaf = new PermissionLeaf(value, core: true);
            return wildcardIndex < 0;
        }

        if (prefix.Equals(DevkitServerModulePrefix, StringComparison.InvariantCultureIgnoreCase))
        {
            permissionLeaf = new PermissionLeaf(value, devkitServer: true);
            return wildcardIndex < 0;
        }

        foreach (IDevkitServerPlugin plugin in PluginLoader.Plugins)
        {
            if (prefix.Equals(plugin.PermissionPrefix, StringComparison.InvariantCultureIgnoreCase))
            {
                permissionLeaf = new PermissionLeaf(value, plugin);
                return wildcardIndex < 0;
            }
        }

        permissionLeaf = new PermissionLeaf(value, false, false);
        return false;
    }

    public static void Write(ByteWriter writer, PermissionLeaf leaf)
    {
        byte flags = (byte)(leaf._flags | (leaf.Plugin == null ? 1 << 7 : 0) | (leaf.Path.Length > byte.MaxValue ? 1 << 6 : 0));

        if ((flags & 0b10000011) == 0)
            flags |= (byte)(leaf.Plugin!.PermissionPrefix.Length > byte.MaxValue ? 1 << 5 : 0);

        writer.Write(flags);

        if ((flags & 0b10000011) == 0)
        {
            if ((flags & (1 << 5)) != 0)
                writer.Write(leaf.Plugin!.PermissionPrefix);
            else
                writer.WriteShort(leaf.Plugin!.PermissionPrefix);
        }

        if ((flags & (1 << 6)) != 0)
            writer.Write(leaf.Path);
        else
            writer.WriteShort(leaf.Path);
    }

    public static PermissionLeaf Read(ByteReader reader)
    {
        byte flags = reader.ReadUInt8();

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

        return plugin == null ? new PermissionLeaf(path, core: (flags & 0b001) != 0, devkitServer: (flags & 0b010) != 0) : new PermissionLeaf(path, plugin);
    }

    public static implicit operator PermissionBranch(PermissionLeaf leaf)
    {
        return (leaf._flags & 0b011) != 0 || leaf.Plugin == null
            ? new PermissionBranch(PermissionMode.Additive, leaf.Path, (leaf._flags & 0b001) != 0, (leaf._flags & 0b010) != 0)
            : new PermissionBranch(PermissionMode.Additive, leaf.Path, leaf.Plugin!);
    }
    public static bool operator ==(PermissionLeaf left, PermissionLeaf right) => left.Equals(right);
    public static bool operator !=(PermissionLeaf left, PermissionLeaf right) => !left.Equals(right);
}

public sealed class PermissionLeafConverter : JsonConverter<PermissionLeaf>
{
    public override PermissionLeaf Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return default;
            case JsonTokenType.String:
                string? str = reader.GetString();
                if (string.IsNullOrEmpty(str) || str.Equals("null", StringComparison.Ordinal))
                    return default;
                if (PermissionLeaf.TryParse(str, out PermissionLeaf leaf))
                    return leaf;

                throw new JsonException("Invalid string value for permission leaf: \"" + str + "\".");
            default:
                throw new JsonException("Unexpected token " + reader.TokenType + " while reading permission leaf.");
        }
    }

    public override void Write(Utf8JsonWriter writer, PermissionLeaf value, JsonSerializerOptions options)
    {
        if (value.Path == null)
            writer.WriteNullValue();
        else
            writer.WriteStringValue(value.ToString());
    }
}