using DevkitServer.Configuration;
using DevkitServer.Util.Encoding;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevkitServer.API.Permissions;
[JsonConverter(typeof(PermissionGroupConverter))]
public sealed class PermissionGroup : IReadOnlyList<GroupPermission>
{
#nullable disable
    private readonly List<GroupPermission> _permissions;

    [JsonPropertyName("id")]
    public string Id { get; private set; }

    [JsonPropertyName("name")]
    public string DisplayName { get; internal set; }

    [JsonPropertyName("color")]
    public Color Color { get; internal set; }

    [JsonPropertyName("priority")]
    public int Priority { get; internal set; }

    [JsonPropertyName("permissions")]
    public IReadOnlyList<GroupPermission> Permissions { get; }
#nullable restore
    private PermissionGroup()
    {
        _permissions = new List<GroupPermission>(8);
        Permissions = _permissions.AsReadOnly();
    }
    public PermissionGroup(string id, string displayName, Color color, int priority, IEnumerable<GroupPermission> permissions)
    {
        Id = id;
        DisplayName = displayName;
        Color = color;
        Priority = priority;
        _permissions = new List<GroupPermission>(permissions);
        Permissions = _permissions.AsReadOnly();
    }
    internal static PermissionGroup? ReadJson(ref Utf8JsonReader reader, JsonSerializerOptions? options = null)
    {
        options ??= DevkitServerConfig.SerializerSettings;
        if (reader.TokenType == JsonTokenType.Null)
            return null;
        PermissionGroup? group = null;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;
            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                string? prop = reader.GetString();
                if (reader.Read() && prop != null)
                {
                    if (prop.Equals("id", StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (reader.TokenType != JsonTokenType.String || reader.GetString() is not { } str || string.IsNullOrWhiteSpace(str))
                            throw new JsonException("Failed to read PermissionGroup.Id (\"id\").");
                        (group ??= new PermissionGroup()).Id = str;
                        if (string.IsNullOrWhiteSpace(group.DisplayName))
                            group.DisplayName = str;
                    }
                    else if (prop.Equals("name", StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (reader.TokenType == JsonTokenType.Null)
                        {
                            (group ??= new PermissionGroup()).DisplayName = group.Id;
                            continue;
                        }
                        if (reader.TokenType != JsonTokenType.String || reader.GetString() is not { } str || string.IsNullOrWhiteSpace(str))
                            throw new JsonException("Failed to read PermissionGroup.DisplayName (\"name\").");
                        (group ??= new PermissionGroup()).DisplayName = str;
                    }
                    else if (prop.Equals("color", StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (reader.TokenType == JsonTokenType.Null)
                            continue;
                        try
                        {
                            (group ??= new PermissionGroup()).Color = JsonSerializer.Deserialize<Color>(ref reader, options);
                        }
                        catch (Exception ex)
                        {
                            throw new JsonException("Failed to read PermissionGroup.Color (\"color\").", ex);
                        }
                    }
                    else if (prop.Equals("priority", StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (reader.TokenType == JsonTokenType.Null)
                            continue;
                        if (reader.TokenType != JsonTokenType.Number || !reader.TryGetInt32(out int z))
                            throw new JsonException("Failed to read PermissionGroup.Proiority (\"priority\").");
                        (group ??= new PermissionGroup()).Priority = z;
                    }
                    else if (prop.Equals("permissions", StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (reader.TokenType == JsonTokenType.Null)
                            continue;
                        if (reader.TokenType != JsonTokenType.StartArray)
                            throw new JsonException("Failed to read PermissionGroup.Permissions (\"permissions\").");
                        group ??= new PermissionGroup();
                        int i = 0;
                        try
                        {
                            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                            {
                                GroupPermission perm = JsonSerializer.Deserialize<GroupPermission>(ref reader, options);
                                if (perm.Permission == null) continue;
                                PermissionBranch perm2 = perm.Permission;
                                group._permissions.Add(new GroupPermission(perm2, perm.IsRemoved));
                                ++i;
                            }
                        }
                        catch (Exception ex)
                        {
                            throw new JsonException("Failed to read PermissionGroup.Permissions[" + i + "] (\"permissions[" + i + "]\").", ex);
                        }
                    }
                }
            }
        }

        return group;
    }
    internal static void WriteJson(Utf8JsonWriter writer, PermissionGroup? group, JsonSerializerOptions? options = null)
    {
        options ??= DevkitServerConfig.SerializerSettings;
        if (group == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();

        writer.WritePropertyName("id");
        writer.WriteStringValue(group.Id);

        writer.WritePropertyName("name");
        writer.WriteStringValue(group.DisplayName);

        writer.WritePropertyName("color");
        try
        {
            JsonSerializer.Serialize(writer, group.Color, options);
        }
        catch (Exception ex)
        {
            throw new JsonException("Failed to write PermissionGroup.Color (\"color\").", ex);
        }

        writer.WritePropertyName("priority");
        writer.WriteNumberValue(group.Priority);

        writer.WritePropertyName("permissions");
        writer.WriteStartArray();
        for (int i = 0; i < group._permissions.Count; ++i)
        {
            try
            {
                JsonSerializer.Serialize(writer, group._permissions[i], options);
            }
            catch (Exception ex)
            {
                throw new JsonException("Failed to write PermissionGroup.Permissions[" + i + "] (\"permissions[" + i + "]\").", ex);
            }
        }
        writer.WriteEndArray();

        writer.WriteEndObject();
    }
    public static void WritePermissionGroup(ByteWriter writer, PermissionGroup group)
    {
        writer.Write(group.Id);
        writer.Write(group.DisplayName);
        writer.Write(group.Color);
        writer.Write(group.Priority);
        writer.Write(group.Permissions.Count);
        for (int j = 0; j < group.Permissions.Count; ++j)
        {
            writer.Write(group.Permissions[j].IsRemoved);
            writer.Write(group.Permissions[j].Permission.ToString());
        }
    }
    public static PermissionGroup ReadPermissionGroup(ByteReader reader)
    {
        string id = reader.ReadString();
        string name = reader.ReadString();
        Color clr = reader.ReadColor();
        int priority = reader.ReadInt32();
        int len = reader.ReadInt32();
        PermissionGroup group = new PermissionGroup
        {
            Id = id,
            DisplayName = name,
            Color = clr,
            Priority = priority
        };
        if (group._permissions.Capacity < len)
            group._permissions.Capacity = len;
        for (int j = 0; j < len; ++j)
        {
            bool rem = reader.ReadBool();
            string str = reader.ReadString();
            if (PermissionLeaf.TryParse(str, out PermissionLeaf perm))
            {
                group._permissions.Add(new GroupPermission(perm, rem));
            }
#if SERVER
            else
                Logger.LogWarning("Unable to find permission: " + str.Format() + ".");
#endif
        }

        return group;
    }
    internal bool AddPermission(GroupPermission permission)
    {
        for (int i = 0; i < _permissions.Count; ++i)
        {
            if (_permissions[i].Equals(permission))
                return false;
        }
        for (int i = _permissions.Count - 1; i >= 0; --i)
        {
            if (_permissions[i].Permission.Equals(permission.Permission))
                _permissions.RemoveAt(i);
        }
        _permissions.Add(permission);
        return true;
    }
    internal bool RemovePermission(GroupPermission permission)
    {
        for (int i = 0; i < _permissions.Count; ++i)
        {
            if (_permissions[i].Equals(permission))
            {
                _permissions.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    /// <returns><see langword="true"/> if priority was changed.</returns>
    internal bool UpdateFrom(PermissionGroup group)
    {
        DisplayName = group.DisplayName;
        Color = group.Color;
        int oldPriority = Priority;
        Priority = group.Priority;
        _permissions.Clear();
        _permissions.AddRange(group._permissions);
        return Priority != oldPriority;
    }
    public IEnumerator<GroupPermission> GetEnumerator() => _permissions.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_permissions).GetEnumerator();
    public int Count => _permissions.Count;
    public GroupPermission this[int index] => _permissions[index];
    public override string ToString() => $"{{Permission Group | \"{Id}\" ({DisplayName}) | {Permissions.Count} permission(s)}}";
    public override bool Equals(object? obj) => obj is PermissionGroup g && g.Id.Equals(Id, StringComparison.InvariantCultureIgnoreCase);
    // ReSharper disable NonReadonlyMemberInGetHashCode
    public override int GetHashCode() => Id != null ? Id.GetHashCode() : 0;

    // ReSharper restore NonReadonlyMemberInGetHashCode
    public static bool operator ==(PermissionGroup? left, object? right) => left is null ? right is null : left.Equals(right);
    public static bool operator !=(PermissionGroup? left, object? right) => !(left == right);
}

[JsonConverter(typeof(GroupPermissionConverter))]
public readonly struct GroupPermission
{
    public PermissionBranch Permission { get; }
    public bool IsRemoved { get; }
#nullable disable
    public GroupPermission() { }
#nullable restore
    public GroupPermission(PermissionBranch permission, bool isRemoved)
    {
        Permission = permission;
        IsRemoved = isRemoved;
    }
    public static bool TryParse(string str, out GroupPermission result)
    {
        if (string.IsNullOrWhiteSpace(str))
        {
            result = default;
            return false;
        }

        bool rem = false;
        if (str[0] == '-')
        {
            rem = true;
            str = str.Substring(1);
            if (string.IsNullOrEmpty(str))
                throw new JsonException("Invalid permission in GroupPermission converter: \"-" + str + "\".");
        }
        else if (str[0] == '+')
        {
            str = str.Substring(1);
            if (string.IsNullOrEmpty(str))
                throw new JsonException("Invalid permission in GroupPermission converter: \"+" + str + "\".");
        }
        if (PermissionBranch.TryParse(str, out PermissionBranch permission))
        {
            result = new GroupPermission(permission, rem);
            return true;
        }

        result = new GroupPermission(permission, rem);
        return false;
    }

    public static implicit operator GroupPermission(PermissionLeaf permission) => permission.Valid ? default : new GroupPermission(permission, false);
    public override bool Equals(object? obj) => obj is GroupPermission g && g.IsRemoved == IsRemoved && g.Permission.Equals(Permission);
    public override int GetHashCode() => (Permission != null ? Permission.GetHashCode() : 0) + (IsRemoved ? 1 : 0);
    public static bool operator ==(GroupPermission? left, object? right) => right is not null && left.Equals(right);
    public static bool operator !=(GroupPermission? left, object? right) => !(left == right);
    public override string ToString()
    {
        string str = Permission.ToString();
        if (IsRemoved)
            str = "-" + str;

        return str;
    }
}

public sealed class PermissionGroupConverter : JsonConverter<PermissionGroup>
{
    public override PermissionGroup Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return PermissionGroup.ReadJson(ref reader, options)!;
    }
    public override void Write(Utf8JsonWriter writer, PermissionGroup? value, JsonSerializerOptions options)
    {
        PermissionGroup.WriteJson(writer, value, options);
    }
}
public sealed class GroupPermissionConverter : JsonConverter<GroupPermission>
{
    public override GroupPermission Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return default;
            case JsonTokenType.String:
                string? str = reader.GetString();
                if (string.IsNullOrWhiteSpace(str) || str!.Equals("null", StringComparison.Ordinal))
                    return default;

                if (GroupPermission.TryParse(str, out GroupPermission permission))
                    return permission;

                if (permission == null)
                    throw new JsonException("Invalid string value for permission: \"" + str + "\".");
                
                return default;
            default:
                throw new JsonException("Unexpected token " + reader.TokenType + " while reading permission.");
        }
    }
    public override void Write(Utf8JsonWriter writer, GroupPermission value, JsonSerializerOptions options)
    {
        if (value.Permission == null)
            writer.WriteNullValue();
        else
            writer.WriteStringValue(value.ToString());
    }
}