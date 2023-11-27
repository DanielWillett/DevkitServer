using DevkitServer.Configuration;
using DevkitServer.Util.Encoding;
using System.Text.Json;
using System.Text.Json.Serialization;
#if CLIENT
using DevkitServer.Core.Commands.Subsystem;
#endif

namespace DevkitServer.API.Permissions;
[JsonConverter(typeof(PermissionGroupConverter))]
public sealed class PermissionGroup : IReadOnlyList<PermissionBranch>
{
#nullable disable
    private readonly List<PermissionBranch> _permissions;

    [JsonPropertyName("id")]
    public string Id { get; private set; }

    [JsonPropertyName("name")]
    public string DisplayName { get; internal set; }

    [JsonPropertyName("color")]
    public Color Color { get; internal set; }

    [JsonPropertyName("priority")]
    public int Priority { get; internal set; }

    [JsonPropertyName("permissions")]
    public IReadOnlyList<PermissionBranch> Permissions { get; }
#nullable restore
    private PermissionGroup()
    {
        _permissions = new List<PermissionBranch>(8);
        Permissions = _permissions.AsReadOnly();
    }
    public PermissionGroup(string id, string displayName, Color color, int priority, IEnumerable<PermissionBranch> permissions)
    {
        Id = id;
        DisplayName = displayName;
        Color = color;
        Priority = priority;
        _permissions = new List<PermissionBranch>(permissions);
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
            if (reader.TokenType != JsonTokenType.PropertyName)
                continue;

            string? prop = reader.GetString();
            if (!reader.Read() || prop == null)
                continue;

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
                    throw new JsonException("Failed to read PermissionGroup.Priority (\"priority\").");
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
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    switch (reader.TokenType)
                    {
                        case JsonTokenType.Null:
                            return default;
                        case JsonTokenType.String:
                            string? str = reader.GetString();
                            if (string.IsNullOrEmpty(str) || str.Equals("null", StringComparison.Ordinal))
                                return default;
                            if (!PermissionBranch.TryParse(str, out PermissionBranch branch))
                            {
                                if (branch.Path == null)
                                    throw new JsonException($"Invalid syntax for permission[{i}] in permission group: \"{str}\".");

#if SERVER
                                Logger.LogWarning($"Unrecognized prefix in permission[{i.Format()}] in permission group: \"{branch.Format()}\".");
#else
                                if (DevkitServerPermissions.DebugLogging)
                                    Logger.LogInfo($"[ReadPermissionGroup] Unrecognized prefix in permission[{i.Format()}] in permission group: \"{branch.Format()}\".");
#endif
                            }
                            else
                            {
                                group._permissions.Add(branch);
                            }
                            break;

                        default:
                            throw new JsonException("Unexpected token " + reader.TokenType + $" while reading permission[{i}] in permission group.");
                    }
                    ++i;
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
            writer.WriteStringValue(group._permissions[i].ToString());
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
        writer.Write(group._permissions.Count);
        for (int j = 0; j < group._permissions.Count; ++j)
        {
            PermissionBranch.Write(writer, group._permissions[j]);
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
        group._permissions.IncreaseCapacity(len);
        for (int i = 0; i < len; ++i)
        {
            PermissionBranch branch = PermissionBranch.Read(reader);
            if (branch.Valid)
                group._permissions.Add(branch);
            else
            {
#if SERVER
                Logger.LogWarning($"Unrecognized prefix in permission[{i.Format()}] in permission group: \"{branch.Format()}\".");
#else
                if (DevkitServerPermissions.DebugLogging)
                    Logger.LogInfo($"[ReadPermissionGroup] Unrecognized prefix in permission[{i.Format()}] in permission group: \"{branch.Format()}\".");
#endif
            }
        }

        return group;
    }
    internal bool AddPermission(PermissionBranch branch)
    {
        for (int i = 0; i < _permissions.Count; ++i)
        {
            if (_permissions[i].Equals(branch))
                return false;
        }
        for (int i = _permissions.Count - 1; i >= 0; --i)
        {
            if (_permissions[i].EqualsWithoutMode(branch))
                _permissions.RemoveAt(i);
        }
        _permissions.Add(branch);
        return true;
    }
    internal bool RemovePermission(PermissionBranch permission)
    {
        bool removed = false;
        for (int i = _permissions.Count - 1; i >= 0; --i)
        {
            if (_permissions[i].Equals(permission))
            {
                _permissions.RemoveAt(i);
                removed = true;
            }
        }

        return removed;
    }

    internal void UpdateFrom(PermissionGroup group)
    {
        DisplayName = group.DisplayName;
        Color = group.Color;
        Priority = group.Priority;
        _permissions.Clear();
        _permissions.AddRange(group._permissions);
    }
    public IEnumerator<PermissionBranch> GetEnumerator() => _permissions.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_permissions).GetEnumerator();
    public int Count => _permissions.Count;
    public PermissionBranch this[int index] => _permissions[index];
    public override string ToString() => $"{{Permission Group | \"{Id}\" ({DisplayName}) | {Permissions.Count} permission(s)}}";
    public override bool Equals(object? obj) => obj is PermissionGroup g && g.Id.Equals(Id, StringComparison.InvariantCultureIgnoreCase);
    // ReSharper disable NonReadonlyMemberInGetHashCode
    public override int GetHashCode() => Id != null ? Id.GetHashCode() : 0;

    // ReSharper restore NonReadonlyMemberInGetHashCode
    public static bool operator ==(PermissionGroup? left, object? right) => left is null ? right is null : left.Equals(right);
    public static bool operator !=(PermissionGroup? left, object? right) => !(left == right);
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