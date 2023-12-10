using DevkitServer.API.Commands;
using DevkitServer.Plugins;
using DevkitServer.Util.Encoding;
using System.Globalization;
using System.Reflection;
using SDG.Framework.Modules;
using Module = SDG.Framework.Modules.Module;
using DevkitServer.Core.Commands.Subsystem;

namespace DevkitServer.API.Abstractions;
/// <summary>
/// Translation sources allow you to send a source, a key, and maybe formatting and have it translated client-side depending on their language.
/// </summary>
/// <remarks>Use <see cref="TranslationData"/> to package this data and send it to a client.</remarks>
public class TranslationSource
{
    /// <summary>
    /// Translation source referencing <see cref="DevkitServerModule.MainLocalization"/>.
    /// </summary>
    public static ITranslationSource DevkitServerMainLocalizationSource { get; } = new PluginTranslationSource(0);

    /// <summary>
    /// Translation source referencing <see cref="DevkitServerModule.MessageLocalization"/>.
    /// </summary>
    public static ITranslationSource DevkitServerMessageLocalizationSource { get; } = new PluginTranslationSource(1);

    /// <summary>
    /// Translation source referencing <see cref="DevkitServerModule.CommandLocalization"/>.
    /// </summary>
    public static ITranslationSource DevkitServerCommandLocalizationSource { get; } = new PluginTranslationSource(2);

    /// <summary>
    /// Translation source referencing <see cref="DevkitServerModule.LevelLoadingLocalization"/>.
    /// </summary>
    public static ITranslationSource DevkitServerLevelLoadingLocalizationSource { get; } = new PluginTranslationSource(3);

    /// <summary>
    /// Uses <see cref="ILocalizedCommand.Translations"/>. The command must be registered on the receiving party.
    /// </summary>
    public static ITranslationSource FromCommand(ILocalizedCommand command)
    {
        return command is ICachedTranslationSourceCommand { TranslationSource: { } src } ? src : new CommandTranslationSource(command);
    }

    /// <summary>
    /// Works for any directory under the root unturned directory. (Pass a folder with .dat children, like a folder containing English.dat).<br/>
    /// Try to avoid using this when possible, as it reads the file for each translation.
    /// </summary>
    /// <remarks>This directory must exist on the receiving party.</remarks>
    public static ITranslationSource FromRelativeDirectory(string folder)
    {
        return new FileTranslationSource(folder);
    }

    /// <summary>
    /// Uses <see cref="IDevkitServerPlugin.Translations"/>. The plugin must be registered on the receiving party.
    /// </summary>
    public static ITranslationSource FromPlugin(IDevkitServerPlugin plugin)
    {
        return plugin is ICachedTranslationSourcePlugin { TranslationSource: { } src } ? src : new PluginTranslationSource(plugin);
    }

    /// <summary>
    /// Pulls from an <see cref="Asset"/>'s saved localization data.
    /// </summary>
    /// <remarks>Only works on values that are saved in the asset during read.</remarks>
    public static ITranslationSource FromAssetLocalization(Asset asset)
    {
        return new AssetTranslationSource(asset.GUID);
    }

    /// <summary>
    /// Pulls from an <see cref="Asset"/>'s saved localization data.
    /// </summary>
    /// <remarks>Only works on values that are saved in the asset during read.</remarks>
    public static ITranslationSource FromAssetLocalization(Guid guid)
    {
        return new AssetTranslationSource(guid);
    }

    /// <summary>
    /// Reads from a table of languages and values.<br/>
    /// <c>string[0, n]</c> should be the language name<br/>
    /// <c>string[1, n]</c> should be the value
    /// </summary>
    public static ITranslationSource FromTranslationTable(string[,] table)
    {
        return new ExplicitTranslationSource(table);
    }

    /// <summary>
    /// Reads from a table of languages and values.<br/>
    /// Key is language, value is the value.
    /// </summary>
    /// <remarks>Recommended to cache this if you plan to re-use it.</remarks>
    public static ITranslationSource FromTranslationTable(ICollection<KeyValuePair<string, string>> table)
    {
        return new ExplicitTranslationSource(table);
    }

    /// <summary>
    /// Accesses a static property, or an instance property belonging to a plugin, command, or module.
    /// </summary>
    /// <remarks>Must be of type <see cref="Local"/> and must be available to the receiving party.</remarks>
    public static ITranslationSource FromProperty(PropertyInfo property) => FromVariable(property.AsVariable());

    /// <summary>
    /// Accesses a static field, or an instance field belonging to a plugin, command, or module.
    /// </summary>
    /// <remarks>Must be of type <see cref="Local"/> and must be available to the receiving party.</remarks>
    public static ITranslationSource FromField(FieldInfo field) => FromVariable(field.AsVariable());

    /// <summary>
    /// Accesses a static property/field, or an instance property/field belonging to a plugin, command, or module.
    /// </summary>
    /// <remarks>Must be of type <see cref="Local"/> and must be available to the receiving party.</remarks>
    public static ITranslationSource FromVariable(IVariable variable)
    {
        return new VariableTranslationSource(variable);
    }

    /// <summary>
    /// Write a translation source to a <see cref="ByteWriter"/>. Works with custom implementations as long as there is a parameterless constructor (can be non-public).
    /// </summary>
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

    /// <summary>
    /// Read a translation source from a <see cref="ByteReader"/>. Works with custom implementations as long as there is a parameterless constructor (can be non-public).
    /// </summary>
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
}

/// <summary>
/// Use <see cref="TranslationSource"/> to create an <see cref="ITranslationSource"/>.
/// </summary>
public interface ITranslationSource
{
    /// <summary>
    /// Formats a <paramref name="key"/> from this source using <paramref name="parameters"/> to format it.
    /// </summary>
    string Translate(string key, object[]? parameters);

    /// <summary>
    /// Write the data of this source to a <see cref="ByteWriter"/>.
    /// </summary>
    /// <remarks>No need to write any type identifiers, that is handled internally.</remarks>
    void Write(ByteWriter writer);

    /// <summary>
    /// Read the data of this source from a <see cref="ByteReader"/>.
    /// </summary>
    /// <remarks>No need to read any type identifiers, that is handled internally.</remarks>
    void Read(ByteReader reader);
}

/// <summary>
/// Accesses a static property/field, or an instance property/field belonging to a plugin, command, or module.
/// </summary>
public sealed class VariableTranslationSource : ITranslationSource
{
#nullable disable
    /// <summary>
    /// Variable containing a <see cref="Local"/> collection.
    /// </summary>
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

/// <summary>
/// Reads from a table of languages and values.
/// </summary>
public sealed class ExplicitTranslationSource : ITranslationSource
{
#nullable disable
    /// <summary>
    /// Table of languages (<c>[0, n]</c>) and values (<c>[1, n]</c>).
    /// </summary>
    public string[,] Values { get; private set; }
    /// <summary>
    /// Amount of translations in <see cref="Values"/>. Equal to <see cref="Array.GetLength"/> of dimension 1.
    /// </summary>
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

/// <summary>
/// Pulls from a <see cref="ILocalizedCommand"/>'s localization file.
/// </summary>
public sealed class CommandTranslationSource : ITranslationSource
{
#nullable disable
    /// <summary>
    /// Name of the command (what you type in for /name)
    /// </summary>
    public string CommandName { get; private set; }

    /// <summary>
    /// Plugin the command is defined in. This helps with ambiguous references.
    /// </summary>
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

/// <summary>
/// Reads a localization file and tries to translate from it (not recommended to use if possible).
/// </summary>
/// <remarks>Must be a child of the root unturned directroy.</remarks>
public sealed class FileTranslationSource : ITranslationSource
{
#nullable disable
    /// <summary>
    /// Path to the directory containing the language .dat files relative to <see cref="UnturnedPaths.RootDirectory"/>.
    /// </summary>
    public string RelativeDirectory { get; private set; }
#nullable restore
    public string Translate(string key, object[]? parameters)
    {
        DirectoryInfo directory = new DirectoryInfo(Path.Combine(UnturnedPaths.RootDirectory.FullName, DevkitServerUtility.UnformatUniversalPath(RelativeDirectory)));

        try
        {
            Local local = Localization.tryRead(directory.FullName, false);
            return local.Translate(key, parameters ?? Array.Empty<object>());
        }
        catch (Exception ex)
        {
            Logger.LogError("Error accessing a file for translation.", method: "FILE TRANSLATION SOURCE");
            Logger.LogError(ex, method: "FILE TRANSLATION SOURCE");
            return key;
        }
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

/// <summary>
/// Pulls from a <see cref="IDevkitServerPlugin"/>'s localization file.<br/>
/// Also can pull from <see cref="DevkitServerModule"/> translation using an internal index parameter to reference different ones.
/// </summary>
public sealed class PluginTranslationSource : ITranslationSource
{
#nullable disable
    /// <summary>
    /// Type of <see cref="IDevkitServerPlugin"/> to look for.
    /// </summary>
    public Type Plugin { get; private set; }
    /// <summary>
    /// Internal index to reference the various <see cref="DevkitServerModule"/> translations.
    /// </summary>
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
            if (Index == 3 && DevkitServerModule.LevelLoadingLocalization.has(key))
                return DevkitServerModule.LevelLoadingLocalization.Translate(key, parameters);
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
        if (Plugin == typeof(DevkitServerModule))
        {
            writer.Write((Type?)null);
            writer.Write(Index);
        }
        else
            writer.Write(Plugin);
    }
    public void Read(ByteReader reader)
    {
        Type? type = reader.ReadType(out bool wasPassedNull);
        Plugin = type ?? typeof(DevkitServerModule);
        if (wasPassedNull)
            Index = reader.ReadUInt8();
    }
}

/// <summary>
/// Pulls from an <see cref="Asset"/>'s saved localization data.
/// </summary>
/// <remarks>Only works on values that are saved in the asset during read.</remarks>
[EarlyTypeInit]
public sealed class AssetTranslationSource : ITranslationSource
{
    internal static readonly InstanceGetter<ItemGunAsset, NPCRewardsList>? GetRewardsList =
        Accessor.GenerateInstanceGetter<ItemGunAsset, NPCRewardsList>("shootQuestRewards", false);
    /// <summary>
    /// Guid of an <see cref="Asset"/> to get localization data from.
    /// </summary>
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

        int underscoreIndex = key2.IndexOf('_');

        if (!int.TryParse(underscoreIndex == -1 ? key2 : key2.Substring(0, underscoreIndex), NumberStyles.Number, CultureInfo.InvariantCulture, out int rewardIndex) ||
            conditions.Count >= rewardIndex)
            return key;

        if (conditions != null)
        {
            if (underscoreIndex != -1 && key2.Length > underscoreIndex + 1 &&
                key2.Substring(underscoreIndex + 1).Equals("UI_Requirements", StringComparison.OrdinalIgnoreCase) &&
                AssetUtil.GetConditionUIRequirements(conditions[rewardIndex]) is { } list)
            {
                return string.Join(", ", list);
            }

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