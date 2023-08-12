using DevkitServer.API.Commands;
using DevkitServer.Commands.Subsystem;
using DevkitServer.Plugins;
using DevkitServer.Util.Encoding;
using System.Globalization;
using System.Reflection;
using SDG.Framework.Modules;
using Module = SDG.Framework.Modules.Module;

namespace DevkitServer.API.Abstractions;
/// <summary>
/// Translation sources allow you to send a source, a key, and maybe formatting and have it translated client-side depending on their language.
/// </summary>
public class TranslationSource
{
    internal static readonly ITranslationSource MainLocalizationSource = new PluginTranslationSource(0);
    internal static readonly ITranslationSource MessageLocalizationSource = new PluginTranslationSource(1);
    internal static readonly ITranslationSource CommandLocalizationSource = new PluginTranslationSource(2);
    /// <summary>
    /// Uses <see cref="ILocalizedCommand.Translations"/>. The command must be registered on the client.
    /// </summary>
    public static ITranslationSource FromCommand(ILocalizedCommand command)
    {
        return command is ICachedTranslationSourceCommand { TranslationSource: { } src } ? src : new CommandTranslationSource(command);
    }
    /// <summary>
    /// Works for any directory under the root unturned directory. (Pass a folder with .dat children, like a folder containing English.dat).<br/>
    /// Try to avoid using this when possible, as it reads the file for each translation.
    /// </summary>
    /// <remarks>This directory must exist on the client.</remarks>
    public static ITranslationSource FromRelativeDirectory(string folder)
    {
        return new FileTranslationSource(folder);
    }
    /// <summary>
    /// Uses <see cref="IDevkitServerPlugin.Translations"/>. The plugin must be registered on the client.
    /// </summary>
    public static ITranslationSource FromPlugin(IDevkitServerPlugin plugin)
    {
        return plugin is ICachedTranslationSourcePlugin { TranslationSource: { } src } ? src : new PluginTranslationSource(plugin);
    }
    /// <summary>
    /// Works with any saved keys from the Language.dat file.
    /// </summary>
    public static ITranslationSource FromAssetLocalization(Asset asset)
    {
        return new AssetTranslationSource(asset.GUID);
    }
    /// <summary>
    /// Works with any saved keys from the Language.dat file.
    /// </summary>
    public static ITranslationSource FromAssetLocalization(Guid guid)
    {
        return new AssetTranslationSource(guid);
    }
    /// <summary>
    /// <c>string[0, n]</c> should be the language name<br/>
    /// <c>string[1, n]</c> should be the value
    /// </summary>
    public static ITranslationSource FromTranslationTable(string[,] table)
    {
        return new ExplicitTranslationSource(table);
    }
    /// <summary>
    /// Key is language, value is the value.
    /// </summary>
    /// <remarks>Recommended to cache this if you plan to re-use it.</remarks>
    public static ITranslationSource FromTranslationTable(ICollection<KeyValuePair<string, string>> table)
    {
        return new ExplicitTranslationSource(table);
    }
    public static void Write(ByteWriter writer, ITranslationSource? source)
    {
        byte code = source switch
        {
            CommandTranslationSource => 1,
            FileTranslationSource => 2,
            PluginTranslationSource => 3,
            AssetTranslationSource => 4,
            ExplicitTranslationSource => 5,
            VariableTranslationSource => 6,
            null => 255,
            _ => 0
        };
        writer.Write(code);
        if (code == 0)
            writer.Write(source!.GetType());
        if (code != 255)
            source!.Write(writer);
    }
    public static ITranslationSource? Read(ByteReader reader)
    {
        byte code = reader.ReadUInt8();
        if (code == 255)
            return null;
        ITranslationSource? source;
        if (code == 0)
        {
            Type type = reader.ReadType()!;
            if (type == null || !typeof(ITranslationSource).IsAssignableFrom(type))
                return null;
            try
            {
                source = (ITranslationSource)Activator.CreateInstance(type, true);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to read tranlation source.");
                Logger.LogError(ex);
                return null;
            }
        }
        else
        {
            source = code switch
            {
                1 => new CommandTranslationSource(),
                2 => new FileTranslationSource(),
                3 => new PluginTranslationSource(),
                4 => new AssetTranslationSource(),
                5 => new ExplicitTranslationSource(),
                6 => new VariableTranslationSource(),
                _ => null
            };
        }

        if (source == null)
        {
            Logger.LogError($"Failed to read tranlation source: {code.Format("x")}.");
            return null;
        }
        source.Read(reader);
        return source;
    }

    public static object?[] ReadFormattingParameters(ByteReader reader)
    {
        int ct = reader.ReadUInt8();
        if (ct == 0)
            return Array.Empty<object>();

        object?[] parameters = new object?[ct];
        for (int i = 0; i < ct; ++i)
        {
            TypeCode code = (TypeCode)reader.ReadUInt8();
            if (code == TypeCode.Object)
            {
                Type? type = reader.ReadType();
                if (type == null)
                    continue;
                MethodInfo? readerMethod = ByteReader.GetReadMethod(type);

                parameters[i] = readerMethod?.Invoke(reader, Array.Empty<object>());
                continue;
            }

            parameters[i] = code switch
            {
                TypeCode.DBNull => DBNull.Value,
                TypeCode.Boolean => reader.ReadBool(),
                TypeCode.Char => reader.ReadChar(),
                TypeCode.SByte => reader.ReadInt8(),
                TypeCode.Byte => reader.ReadUInt8(),
                TypeCode.Int16 => reader.ReadInt16(),
                TypeCode.UInt16 => reader.ReadUInt16(),
                TypeCode.Int32 => reader.ReadInt32(),
                TypeCode.UInt32 => reader.ReadUInt32(),
                TypeCode.Int64 => reader.ReadInt64(),
                TypeCode.UInt64 => reader.ReadUInt64(),
                TypeCode.Single => reader.ReadFloat(),
                TypeCode.Double => reader.ReadDouble(),
                TypeCode.Decimal => reader.ReadDecimal(),
                TypeCode.DateTime => reader.ReadDateTimeOffset().DateTime,
                TypeCode.String => reader.ReadString(),
                _ => parameters[i]
            };
        }

        return parameters;
    }
    public static void WriteFormattingParameters(ByteWriter writer, object?[]? parameters)
    {
        if (parameters is not { Length: > 0 })
        {
            writer.Write((byte)0);
            return;
        }

        int ct = Math.Min(byte.MaxValue, parameters.Length);
        writer.Write((byte)ct);
        for (int i = 0; i < ct; ++i)
        {
            object? value = parameters[i];
            TypeCode typeCode = Convert.GetTypeCode(value);
            if (typeCode is not TypeCode.Object)
            {
                writer.Write((byte)typeCode);
                switch (typeCode)
                {
                    case TypeCode.Boolean:
                        writer.Write((bool)value!);
                        break;
                    case TypeCode.Char:
                        writer.Write((char)value!);
                        break;
                    case TypeCode.SByte:
                        writer.Write((sbyte)value!);
                        break;
                    case TypeCode.Byte:
                        writer.Write((byte)value!);
                        break;
                    case TypeCode.Int16:
                        writer.Write((short)value!);
                        break;
                    case TypeCode.UInt16:
                        writer.Write((ushort)value!);
                        break;
                    case TypeCode.Int32:
                        writer.Write((int)value!);
                        break;
                    case TypeCode.UInt32:
                        writer.Write((uint)value!);
                        break;
                    case TypeCode.Int64:
                        writer.Write((long)value!);
                        break;
                    case TypeCode.UInt64:
                        writer.Write((ulong)value!);
                        break;
                    case TypeCode.Single:
                        writer.Write((float)value!);
                        break;
                    case TypeCode.Double:
                        writer.Write((double)value!);
                        break;
                    case TypeCode.Decimal:
                        writer.Write((decimal)value!);
                        break;
                    case TypeCode.DateTime:
                        DateTime dt = (DateTime)value!;
                        writer.Write(new DateTimeOffset(dt));
                        break;
                    case TypeCode.String:
                        writer.Write((string)value!);
                        break;
                }
            }
            else
            {
                Type type = value!.GetType();
                MethodInfo? writerMethod = ByteWriter.GetWriteMethod(type);
                if (writerMethod == null)
                {
                    writer.Write((byte)TypeCode.String);
                    writer.Write(value.ToString());
                }
                else
                {
                    writer.Write((byte)TypeCode.Object);
                    writer.Write(type);
                    writerMethod.Invoke(writer, new object[] { value });
                }
            }
        }
    }

    public static void RemoveNullFormattingArguemnts(object?[] formatting)
    {
        for (int i = 0; i < formatting.Length; i++)
            formatting[i] ??= "null";
    }
}
public interface ITranslationSource
{
    string Translate(string key, object[]? parameters);
    void Write(ByteWriter writer);
    void Read(ByteReader reader);
}
public sealed class VariableTranslationSource : ITranslationSource
{
#nullable disable
    public IVariable Variable { get; private set; }
#nullable restore
    public VariableTranslationSource(IVariable variable)
    {
        if (!typeof(Local).IsAssignableFrom(variable.MemberType))
            throw new ArgumentException("Must be of type Local.", nameof(variable));

        if (!variable.IsStatic)
        {
            Type? decl = variable.DeclaringType;
            if (decl == null || (!typeof(IDevkitServerPlugin).IsAssignableFrom(decl) &&
                                 !typeof(IExecutableCommand).IsAssignableFrom(decl) &&
                                 !typeof(IModuleNexus).IsAssignableFrom(decl)))
            {
                throw new ArgumentException("Must be static or belong to a singleton member like a plugin, command, or other module.", nameof(variable));
            }
        }

        Variable = variable;
    }
    internal VariableTranslationSource() { }

    public string Translate(string key, object[]? parameters)
    {
        if (Variable == null)
            return key;
        bool isPlugin = false, isCommand = false;
        if (!Variable.IsStatic)
        {
            Type? decl = Variable.DeclaringType;
            if (decl == null)
                return key;
            isPlugin = typeof(IDevkitServerPlugin).IsAssignableFrom(decl);
            isCommand = !isPlugin && typeof(IExecutableCommand).IsAssignableFrom(decl);
            bool isModule = !isCommand && typeof(IModuleNexus).IsAssignableFrom(decl);
            if (!isPlugin && !isCommand && !isModule)
                return key;
        }

        Local? value;
        if (Variable.IsStatic)
            value = (Local?)Variable.GetValue(null);
        else
        {
            if (isPlugin)
            {
                IDevkitServerPlugin? plugin = PluginLoader.Plugins.FirstOrDefault(Variable.DeclaringType!.IsInstanceOfType);
                if (plugin == null)
                    return key;
                value = (Local?)Variable.GetValue(plugin);
            }
            else if (isCommand)
            {
                IExecutableCommand? command = CommandHandler.Handler.Commands.FirstOrDefault(Variable.DeclaringType!.IsInstanceOfType);
                if (command == null)
                    return key;
                value = (Local?)Variable.GetValue(command);
            }
            else
            {
                value = null;
                for (int i = 0; i < ModuleHook.modules.Count; ++i)
                {
                    Module module = ModuleHook.modules[i];
                    IReadOnlyList<IModuleNexus>? nexii = AssetUtil.GetModuleNexii(module);
                    if (nexii == null)
                        break;
                    for (int j = 0; j < nexii.Count; ++j)
                    {
                        if (Variable.DeclaringType!.IsInstanceOfType(nexii[j]))
                        {
                            value = (Local?)Variable.GetValue(nexii[j]);
                            break;
                        }
                    }

                    if (value != null)
                        break;
                }
            }
        }

        return value?.Translate(key, parameters ?? Array.Empty<object>()) ?? key;
    }

    public void Write(ByteWriter writer)
    {
        writer.Write(Variable.DeclaringType ?? throw new Exception("Global variables not supported."));
        writer.Write(Variable.Member.Name);
        writer.Write(Variable.Member is PropertyInfo);
    }

    public void Read(ByteReader reader)
    {
        Type? declaringType = reader.ReadType();
        string name = reader.ReadString();
        bool property = reader.ReadBool();
        if (declaringType != null)
        {
            if (property)
            {
                PropertyInfo? propertyInfo = declaringType.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic);
                if (propertyInfo != null)
                {
                    Variable = propertyInfo.AsVariable();
                    return;
                }
            }
            else
            {
                FieldInfo? fieldInfo = declaringType.GetField(name, BindingFlags.Public | BindingFlags.NonPublic);
                if (fieldInfo != null)
                {
                    Variable = fieldInfo.AsVariable();
                    return;
                }
            }
        }
        Variable = null;
    }
}

public sealed class ExplicitTranslationSource : ITranslationSource
{
#nullable disable
    public string[,] Values { get; private set; }
    public int Length { get; private set; }
#nullable restore
    public ExplicitTranslationSource(string[,] table)
    {
        if (table.GetLength(0) != 2)
            throw new ArgumentException("Dimension #0 should have 2 rows (0 = language name, 1 = value).", nameof(table));
        Values = table;
        Length = table.GetLength(1);
    }
    public ExplicitTranslationSource(ICollection<KeyValuePair<string, string>> dictionary)
    {
        string[,] table = new string[2, dictionary.Count];
        int index = -1;
        foreach (KeyValuePair<string, string> kvp in dictionary)
        {
            table[0, ++index] = kvp.Key;
            table[1, index] = kvp.Value ?? string.Empty;
        }

        Length = index + 1;
        Values = table;
    }
    internal ExplicitTranslationSource() { }
    public string Translate(string key, object[]? parameters)
    {
        for (int i = 0; i < Length; ++i)
        {
            if (!Values[0, i].Equals(Provider.language, StringComparison.OrdinalIgnoreCase))
                continue;

            if (parameters is not { Length: > 0 })
                return Values[1, i];
            
            try
            {
                return string.Format(Values[1, i], parameters);
            }
            catch
            {
                string args = string.Empty;
                for (int j = 0; j < parameters.Length; ++j)
                {
                    if (args.Length > 0)
                        args += ",";
                    args += $"{{{j.Format()}}} = {parameters[j].Format()}";
                }
                Logger.LogError($"Caught localization string formatting exception (key: {key.Format()} (in {typeof(ExplicitTranslationSource).Format()}) text: {Values[1, i].Format()} {args})");
                return key;
            }
        }

        return key;
    }

    public void Write(ByteWriter writer)
    {
        int len = Math.Min(byte.MaxValue, Values.GetLength(1));
        Length = len;
        writer.Write((byte)len);
        for (int i = 0; i < len; ++i)
        {
            writer.Write(Values[0, i]);
            writer.Write(Values[1, i]);
        }
    }
    public void Read(ByteReader reader)
    {
        int len = reader.ReadUInt8();
        Values = new string[2, len];
        for (int i = 0; i < len; ++i)
        {
            Values[0, i] = reader.ReadString();
            Values[1, i] = reader.ReadString();
        }
    }
}
public sealed class CommandTranslationSource : ITranslationSource
{
#nullable disable
    public string CommandName { get; private set; }
    public Type Plugin { get; private set; }
#nullable restore
    public string Translate(string key, object[]? parameters)
    {
        IExecutableCommand? command = CommandHandler.Handler.Commands
            .FirstOrDefault(x => x.CommandName.Equals(CommandName, StringComparison.InvariantCultureIgnoreCase) &&
                                 x.GetPluginType() == Plugin);

        return command is not ILocalizedCommand local ? key : local.Translations.Translate(key, parameters ?? Array.Empty<object>());
    }
    public CommandTranslationSource(ILocalizedCommand command)
    {
        Plugin = command.GetPluginType();
        CommandName = command.CommandName;
    }
    internal CommandTranslationSource(string commandName, Type pluginType)
    {
        Plugin = pluginType;
        CommandName = commandName;
    }
    internal CommandTranslationSource() { }

    public void Write(ByteWriter writer)
    {
        writer.Write(CommandName);
        writer.Write(Plugin);
    }

    public void Read(ByteReader reader)
    {
        CommandName = reader.ReadString();
        Plugin = reader.ReadType() ?? typeof(DevkitServerModule);
    }
}
public sealed class FileTranslationSource : ITranslationSource
{
#nullable disable
    public string RelativeDirectory { get; private set; }
#nullable restore
    public string Translate(string key, object[]? parameters)
    {
        DirectoryInfo directory = new DirectoryInfo(DevkitServerUtility.UnformatUniversalPath(RelativeDirectory));
        if (!DevkitServerUtility.IsChildOf(UnturnedPaths.RootDirectory, directory))
            throw new FormatException("Relative path must be a child directory of the root unturned directory.");

        Local local = Localization.tryRead(directory.FullName, false);
        return local.Translate(key, parameters ?? Array.Empty<object>());
    }

    public FileTranslationSource(string relativeDirectory)
    {
        DirectoryInfo directory = new DirectoryInfo(relativeDirectory);
        RelativeDirectory = DevkitServerUtility.FormatUniversalPath(DevkitServerUtility.GetRelativePath(UnturnedPaths.RootDirectory.FullName, directory.FullName));
    }
    // ReSharper disable once UnusedParameter.Local
    internal FileTranslationSource(string relativeDirectory, bool dummy)
    {
        RelativeDirectory = relativeDirectory;
    }
    internal FileTranslationSource() { }
    public void Write(ByteWriter writer)
    {
        writer.Write(RelativeDirectory);
    }
    public void Read(ByteReader reader)
    {
        RelativeDirectory = reader.ReadString();
    }
}
public sealed class PluginTranslationSource : ITranslationSource
{
#nullable disable
    public Type Plugin { get; private set; }
    internal byte Index { get; private set; }
#nullable restore
    public string Translate(string key, object[]? parameters)
    {
        parameters ??= Array.Empty<object>();
        if (Plugin == typeof(DevkitServerModule))
        {
            if (Index == 0 && DevkitServerModule.MainLocalization.has(key))
                return DevkitServerModule.MainLocalization.Translate(key, parameters);
            if (Index == 1 && DevkitServerModule.MessageLocalization.has(key))
                return DevkitServerModule.MessageLocalization.Translate(key, parameters);
            if (Index == 2 && DevkitServerModule.CommandLocalization.has(key))
                return DevkitServerModule.CommandLocalization.Translate(key, parameters);
        }
        else
        {
            IDevkitServerPlugin? plugin = PluginLoader.Plugins.FirstOrDefault(Plugin.IsInstanceOfType);
            if (plugin != null)
                return plugin.Translations.Translate(key, parameters);
        }
        return key;
    }

    public PluginTranslationSource(IDevkitServerPlugin plugin)
    {
        Plugin = plugin.GetType();
    }
    internal PluginTranslationSource() { }
    internal PluginTranslationSource(byte index)
    {
        Plugin = typeof(DevkitServerModule);
        Index = index;
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(Plugin);
        if (Plugin == typeof(DevkitServerModule))
            writer.Write(Index);
    }
    public void Read(ByteReader reader)
    {
        Plugin = reader.ReadType() ?? typeof(DevkitServerModule);
        if (Plugin == typeof(DevkitServerModule))
            Index = reader.ReadUInt8();
    }
}
[EarlyTypeInit]
public sealed class AssetTranslationSource : ITranslationSource
{
    internal static readonly InstanceGetter<ItemGunAsset, NPCRewardsList>? GetRewardsList =
        Accessor.GenerateInstanceGetter<ItemGunAsset, NPCRewardsList>("shootQuestRewards", false);
    public Guid Guid { get; private set; }
    public void Write(ByteWriter writer)
    {
        writer.Write(Guid);
    }
    public void Read(ByteReader reader)
    {
        Guid = reader.ReadGuid();
    }
    private static string TranslateRewards(IList<INPCReward> rewards, string prefix, string key, string? originalKey = null)
    {
        string key2 = key.Substring(prefix.Length);

        if (!int.TryParse(key2, NumberStyles.Number, CultureInfo.InvariantCulture, out int rewardIndex) ||
            rewards.Count >= rewardIndex)
            return key;

        
        if (rewards != null)
        {
            return rewards[rewardIndex].formatReward(
                Player.player ?? Provider.clients.FirstOrDefault()?.player ?? throw new FormatException("The format: " + (originalKey ?? key) + " requires a player."));
        }

        return key;
    }
    private static string TranslateConditions(IList<INPCCondition> conditions, string prefix, string key, string? originalKey = null)
    {
        string key2 = key.Substring(prefix.Length);

        if (!int.TryParse(key2, NumberStyles.Number, CultureInfo.InvariantCulture, out int rewardIndex) ||
            conditions.Count >= rewardIndex)
            return key;

        
        if (conditions != null)
        {
            return conditions[rewardIndex].formatCondition(
                Player.player ?? Provider.clients.FirstOrDefault()?.player ?? throw new FormatException("The format: " + (originalKey ?? key) + " requires a player."));
        }

        return key;
    }
    public string Translate(string key, object[]? parameters)
    {
        if (Assets.find(Guid) is not { } asset) return key;

        switch (asset)
        {
            case AnimalAsset animal:
                if (key.Equals("Name", StringComparison.OrdinalIgnoreCase))
                    return animal.animalName;
                break;
            case DialogueAsset dialogue:
                if (key.StartsWith("Response_", StringComparison.OrdinalIgnoreCase))
                {
                    string key2 = key.Substring(9);
                    int index = key2.IndexOf('_');
                    if (index == -1)
                        index = key2.Length;

                    if (!int.TryParse(key2.Substring(0, index), NumberStyles.Number, CultureInfo.InvariantCulture, out int responseIndex) ||
                        dialogue.responses.Length >= responseIndex)
                        return key;

                    DialogueResponse response = dialogue.responses[responseIndex];

                    if (index == key2.Length || index == key2.Length - 1)
                        return response.text;

                    key2 = key2.Substring(index + 1);

                    if (key2.StartsWith("Condition_", StringComparison.OrdinalIgnoreCase))
                    {
                        return TranslateConditions(response.conditions, "Condition_", key2, key);
                    }
                    if (key2.StartsWith("Reward_", StringComparison.OrdinalIgnoreCase))
                    {
                        return TranslateRewards(response.rewards, "Reward_", key2, key);
                    }
                }
                else if (key.StartsWith("Message_", StringComparison.OrdinalIgnoreCase))
                {
                    string key2 = key.Substring(9);
                    int index = key2.IndexOf('_');
                    if (index == -1)
                        index = key2.Length;

                    if (!int.TryParse(key2.Substring(0, index), NumberStyles.Number, CultureInfo.InvariantCulture, out int messageIndex) ||
                        dialogue.responses.Length >= messageIndex)
                        return key;

                    DialogueMessage message = dialogue.messages[messageIndex];

                    if (index == key2.Length || index == key2.Length - 1)
                        return key;

                    key2 = key2.Substring(index + 1);

                    if (key2.StartsWith("Page_", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!int.TryParse(key2.Substring(5), NumberStyles.Number, CultureInfo.InvariantCulture, out int pageIndex) ||
                            message.conditions.Length >= pageIndex)
                            return key;

                        return message.pages[pageIndex].text;
                    }
                    if (key2.StartsWith("Condition_", StringComparison.OrdinalIgnoreCase))
                    {
                        return TranslateConditions(message.conditions, "Condition_", key2, key);
                    }
                    if (key2.StartsWith("Reward_", StringComparison.OrdinalIgnoreCase))
                    {
                        return TranslateRewards(message.rewards, "Reward_", key2, key);
                    }
                }
                break;
            case ItemAsset item:
                if (key.Equals("Name", StringComparison.OrdinalIgnoreCase))
                    return item.itemName;
                if (key.Equals("Description", StringComparison.OrdinalIgnoreCase))
                    return item.itemDescription;

                if (item is ItemConsumeableAsset consumable)
                {
                    if (key.StartsWith("Quest_Reward_", StringComparison.OrdinalIgnoreCase))
                    {
                        return TranslateRewards(consumable.questRewards, "Quest_Reward_", key);
                    }
                }

                if (item is ItemGunAsset gun && GetRewardsList != null)
                {
                    if (key.StartsWith("Shoot_Quest_Reward_", StringComparison.OrdinalIgnoreCase))
                    {
                        NPCRewardsList list = GetRewardsList(gun);
                        INPCReward[]? rewards = AssetUtil.GetRewards(list);
                        if (rewards != null)
                        {
                            return TranslateRewards(rewards, "Shoot_Quest_Reward_", key);
                        }
                    }
                }

                if (key.StartsWith("Blueprint_", StringComparison.OrdinalIgnoreCase))
                {
                    string key2 = key.Substring(10);
                    int index = key2.IndexOf('_');
                    if (index == -1)
                        index = key2.Length;

                    if (!int.TryParse(key2.Substring(0, index), NumberStyles.Number, CultureInfo.InvariantCulture, out int blueprintIndex) ||
                        item.blueprints.Count >= blueprintIndex)
                        return key;

                    if (index == key2.Length || index == key2.Length - 1)
                        return key;

                    Blueprint blueprint = item.blueprints[blueprintIndex];

                    key2 = key2.Substring(index + 1);

                    if (key2.StartsWith("Condition_", StringComparison.OrdinalIgnoreCase))
                    {
                        return TranslateConditions(blueprint.questConditions, "Condition_", key2, key);
                    }
                    if (key2.StartsWith("Reward_", StringComparison.OrdinalIgnoreCase))
                    {
                        return TranslateRewards(blueprint.questRewards, "Reward_", key2, key);
                    }
                }
                else if (key.StartsWith("Action_", StringComparison.OrdinalIgnoreCase))
                {
                    string key2 = key.Substring(7);
                    int index = key2.IndexOf('_');
                    if (index == -1)
                        index = key2.Length;

                    if (!int.TryParse(key.Substring(0, index), NumberStyles.Number, CultureInfo.InvariantCulture, out int actionIndex) ||
                        item.actions.Count >= actionIndex)
                        return key;

                    SDG.Unturned.Action action = item.actions[actionIndex];

                    if (index == key2.Length || index == key2.Length - 1)
                        return key;

                    key2 = key2.Substring(index + 1);

                    if (key2.Equals("Text", StringComparison.OrdinalIgnoreCase))
                        return action.text;

                    if (key2.Equals("Tooltip", StringComparison.OrdinalIgnoreCase))
                        return action.tooltip;
                }
                break;
            case ObjectAsset @object:
                if (key.Equals("Name", StringComparison.OrdinalIgnoreCase))
                    return @object.objectName;

                if (@object is ObjectNPCAsset npc && key.Equals("Character", StringComparison.OrdinalIgnoreCase))
                    return npc.npcName;

                if (@object.interactability != EObjectInteractability.NONE)
                {
                    if (@object.interactabilityText != null && (key.StartsWith("Interactability_Text_Line_", StringComparison.OrdinalIgnoreCase) ||
                                                                key.Equals("Interact", StringComparison.Ordinal)))
                        return @object.interactabilityText;

                    if (key.StartsWith("Interactability_Condition_", StringComparison.OrdinalIgnoreCase))
                        return TranslateConditions(@object.interactabilityConditions, "Interactability_Condition_", key);

                    if (key.StartsWith("Interactability_Reward_", StringComparison.OrdinalIgnoreCase))
                        return TranslateConditions(@object.interactabilityConditions, "Interactability_Reward_", key);
                }

                if (key.StartsWith("Condition_", StringComparison.OrdinalIgnoreCase))
                    return TranslateConditions(@object.interactabilityConditions, "Condition_", key);
                break;
            case QuestAsset quest:
                if (key.Equals("Name", StringComparison.OrdinalIgnoreCase))
                    return quest.questName;
                if (key.Equals("Description", StringComparison.OrdinalIgnoreCase))
                    return quest.questDescription;

                if (key.StartsWith("Condition_", StringComparison.OrdinalIgnoreCase))
                    return TranslateConditions(quest.conditions, "Condition_", key);

                if (key.StartsWith("Reward_", StringComparison.OrdinalIgnoreCase))
                    return TranslateRewards(quest.rewards, "Reward_", key);

                break;
            case VehicleAsset vehicle:
                if (key.Equals("Name", StringComparison.OrdinalIgnoreCase))
                    return vehicle.vehicleName;
                break;
            case VendorAsset vendor:
                if (key.Equals("Name", StringComparison.OrdinalIgnoreCase))
                    return vendor.vendorName;
                if (key.Equals("Description", StringComparison.OrdinalIgnoreCase))
                    return vendor.vendorDescription;
                if (key.StartsWith("Buying_", StringComparison.OrdinalIgnoreCase))
                {
                    string key2 = key.Substring(7);
                    int index = key2.IndexOf('_');
                    if (index == -1)
                        index = key2.Length;

                    if (!int.TryParse(key2.Substring(0, index), NumberStyles.Number, CultureInfo.InvariantCulture, out int buyingIndex) ||
                        vendor.buying.Length >= buyingIndex)
                        return key;

                    if (index == key2.Length || index == key2.Length - 1)
                        return key;

                    VendorBuying buying = vendor.buying[buyingIndex];

                    key2 = key2.Substring(index + 1);

                    if (key2.StartsWith("Condition_", StringComparison.OrdinalIgnoreCase))
                    {
                        return TranslateConditions(buying.conditions, "Condition_", key2, key);
                    }
                    if (key2.StartsWith("Reward_", StringComparison.OrdinalIgnoreCase))
                    {
                        return TranslateRewards(buying.rewards, "Reward_", key2, key);
                    }
                }
                else if (key.StartsWith("Selling_", StringComparison.OrdinalIgnoreCase))
                {
                    string key2 = key.Substring(8);
                    int index = key2.IndexOf('_');
                    if (index == -1)
                        index = key2.Length;

                    if (!int.TryParse(key2.Substring(0, index), NumberStyles.Number, CultureInfo.InvariantCulture, out int sellingIndex) ||
                        vendor.selling.Length >= sellingIndex)
                        return key;

                    if (index == key2.Length || index == key2.Length - 1)
                        return key;

                    VendorSellingBase selling = vendor.selling[sellingIndex];

                    key2 = key2.Substring(index + 1);

                    if (key2.StartsWith("Condition_", StringComparison.OrdinalIgnoreCase))
                    {
                        return TranslateConditions(selling.conditions, "Condition_", key2, key);
                    }
                    if (key2.StartsWith("Reward_", StringComparison.OrdinalIgnoreCase))
                    {
                        return TranslateRewards(selling.rewards, "Reward_", key2, key);
                    }
                }

                break;
            case null:
                return Guid.ToString("N");
        }
        
        return "( [" + asset.assetCategory + "] {" + asset.GUID.ToString("N") + "} : " + (asset.FriendlyName ?? asset.name) + " )/" + key;
    }

    public AssetTranslationSource(Guid guid)
    {
        Guid = guid;
    }
    internal AssetTranslationSource() { }
}