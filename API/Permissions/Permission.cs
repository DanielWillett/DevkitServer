using DevkitServer.Plugins;
using DevkitServer.Util.Encoding;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevkitServer.API.Permissions;

[JsonConverter(typeof(PermissionConverter))]
public sealed class Permission
{
    public static readonly Permission SuperuserPermission = new Permission(true);
    public const string CoreModuleCode = "unturned";
    public const string DevkitServerModuleCode = "devkitserver";
    public string Id { get; }
    public IDevkitServerPlugin? Plugin { get; internal set; }
    public bool Core { get; }
    public bool DevkitServer { get; }
    public bool Superuser { get; }
    public bool Valid => (Core || DevkitServer || Superuser || Plugin != null) && !string.IsNullOrWhiteSpace(Id);
    public Permission(string permissionId)
    {
        Id = permissionId;
    }
    private Permission(bool superuser)
    {
        Id = "*";
        Superuser = superuser;
    }
    internal Permission(string permissionId, IDevkitServerPlugin? plugin) : this(permissionId)
    {
        Id = permissionId;
        Plugin = plugin;
    }
    internal Permission(string permissionId, IDevkitServerPlugin? plugin, bool core, bool devkitServer) : this (permissionId, plugin)
    {
        Core = plugin == null && core;
        DevkitServer = plugin == null && devkitServer;
    }
    internal Permission(string permissionId, bool core = false, bool devkitServer = false) : this (permissionId)
    {
        Core = core;
        DevkitServer = devkitServer;
    }

    public static void WritePermission(ByteWriter writer, Permission permission) => writer.Write(permission.ToString());
    public static Permission ReadPermission(ByteReader reader) => TryParse(reader.ReadString(), out Permission permission) ? permission : null!;

    public override string ToString()
    {
        if (Superuser)
            return "*";
        if (Core)
            return CoreModuleCode + "." + Id;
        if (DevkitServer)
            return DevkitServerModuleCode + "." + Id;

        if (Plugin == null)
            return "INVALID." + Id;
        return Plugin.PermissionPrefix + "." + Id;
    }

    public static bool TryParse(string str, out Permission permission)
    {
        if (string.IsNullOrWhiteSpace(str))
        {
            permission = null!;
            return false;
        }

        if (str.Equals("*", StringComparison.InvariantCultureIgnoreCase))
        {
            permission = SuperuserPermission;
            return true;
        }
        int index = str.IndexOf('.');
        if (index == -1 || index == str.Length - 1)
        {
            permission = null!;
            return false;
        }

        string module = str.Substring(0, index);
        string id = str.Substring(index + 1);
        if (module.Equals(CoreModuleCode, StringComparison.InvariantCultureIgnoreCase))
        {
            permission = UserPermissions.Handler.FindCorePermission(id) ?? new Permission(id, null, true, false);
            return true;
        }
        
        if (module.Equals(DevkitServerModuleCode, StringComparison.InvariantCultureIgnoreCase))
        {
            permission = UserPermissions.Handler.FindDevkitServerPermission(id) ?? new Permission(id, null, false, true);
            return true;
        }

        foreach (IDevkitServerPlugin plugin in PluginLoader.Plugins)
        {
            if (module.Equals(plugin.PermissionPrefix, StringComparison.InvariantCultureIgnoreCase))
            {
                permission = UserPermissions.Handler.FindPermission(plugin, id) ?? new Permission(id, plugin);
                return true;
            }
        }
#if SERVER
        Logger.LogWarning("Unable to find pluign for permission " + str.Format() + ".");
#endif
        permission = new Permission(id, null, core: false, devkitServer: false);
        return false;
    }

    public override bool Equals(object? obj)
    {
        return obj is Permission perm && (
            ReferenceEquals(perm, this) ||
            perm.Superuser && Superuser ||
            perm.Core == Core && perm.DevkitServer == DevkitServer &&
                (Plugin == null && perm.Plugin == null ||
                 Plugin != null && Plugin.Equals(perm.Plugin))
              && perm.Id.Equals(Id, StringComparison.InvariantCultureIgnoreCase));
    }
    public static bool operator ==(Permission? left, object? right) => left is null ? right is null : left.Equals(right);
    public static bool operator !=(Permission? left, object? right) => left is null ? right is not null : !left.Equals(right);
    public override int GetHashCode()
    {
        unchecked
        {
            int hashCode = Id.GetHashCode();
            hashCode = (hashCode * 397) ^ (Plugin != null ? Plugin.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ Core.GetHashCode();
            hashCode = (hashCode * 397) ^ DevkitServer.GetHashCode();
            return hashCode;
        }
    }
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class PermissionPrefixAttribute : Attribute
{
    public string Prefix { get; }
    public PermissionPrefixAttribute(string prefix) => Prefix = prefix;
}
public sealed class PermissionConverter : JsonConverter<Permission>
{
    public override Permission? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.String:
                string? str = reader.GetString();
                if (string.IsNullOrEmpty(str) || str!.Equals("null", StringComparison.Ordinal))
                    return null;
                if (Permission.TryParse(str, out Permission permission))
                    return permission;

                throw new JsonException("Invalid string value for permission: \"" + str + "\".");
            default:
                throw new JsonException("Unexpected token " + reader.TokenType + " while reading permission.");
        }
    }

    public override void Write(Utf8JsonWriter writer, Permission value, JsonSerializerOptions options)
    {
        if (value == null)
            writer.WriteNullValue();
        else
            writer.WriteStringValue(value.ToString());
    }
}