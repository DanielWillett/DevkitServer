#if SERVER
using DevkitServer.Configuration;
using System.Reflection;
using System.Text.Json;

namespace DevkitServer.Patches;
internal static class MapCreation
{
    private static readonly int MapArgb = FormattingUtil.ToArgb(new Color32(102, 255, 102, 255));
    internal static void PrefixLoadingDedicatedUGC()
    {
        LevelInfo level = Level.getLevel(Provider.map);

        if (level != null)
        {
            Logger.LogInfo($"Loading existing map: {Provider.map.Colorize(MapArgb)}");
            return;
        }

        if (!ReadWrite.folderExists("/Extras/LevelTemplate/", true))
        {
            Logger.LogError($"Folder missing for level creation: {"\\Extras\\LevelTemplate\\".Format(true)}!");
            return;
        }
        
        /*
         *
         *  This method creates a new map and preloads it with some convenience data:
         *
         *   Owner set to either what's in config or the first admin on the server.
         *   'DevkitServer' is added to the Thanks section of the level config.
         *   The owner's s
         *   All legacy features are turned off by default.
         *   A copy of the default level asset is created with a new GUID.
         *   Tiles are created based on the selected size.
         *   A FoliageSystem is added.
         *   Player clip volumes are added as a world border based on the selected size.
         *
         */

        CreateMap(Provider.map);
    }
    internal static void CreateMap(string mapName)
    {
        SystemConfig.NewLevelCreationOptions options = DevkitServerConfig.Config.NewLevelInfo ?? SystemConfig.NewLevelCreationOptions.Default;
        FieldInfo? clientField = typeof(Provider).GetField("_client", BindingFlags.Static | BindingFlags.NonPublic);
        CSteamID owner = options.Owner;
        if (clientField == null || clientField.FieldType != typeof(CSteamID))
        {
            Level.add(mapName, options.LevelSize, options.LevelType);
            Logger.LogWarning($"Unable to set owner of newly created map, it will be set to: {Provider.server.Format()}.");

            Logger.LogInfo($"Level created: {mapName.Colorize(MapArgb)}. Size: {options.LevelSize.Format()}. Type: {options.LevelType.Format()}.");
        }
        else
        {
            if (owner.m_SteamID == 0ul && SteamAdminlist.list is { Count: > 0 })
                owner = SteamAdminlist.list[0].playerID;

            CSteamID clientOld = (CSteamID)clientField.GetValue(null);
            clientField.SetValue(null, owner);
            Level.add(mapName, options.LevelSize, options.LevelType);
            clientField.SetValue(null, clientOld);

            Logger.LogInfo($"Level created: {mapName.Colorize(MapArgb)}. Size: {options.LevelSize.Format()}. Type: {options.LevelType.Format()}. Owner: {owner.Format()}.");
        }

        string lvlPath = Path.Combine(ReadWrite.PATH, "Maps", mapName);

        Guid assetGuid = LevelAsset.defaultLevel.GUID;

        string? levelAssetFilePath = LevelAsset.defaultLevel.Find()?.getFilePath();
        if (levelAssetFilePath != null && File.Exists(levelAssetFilePath))
        {
            // Copy over the default level asset but replace the GUID with a new one
            string defaultLevelAsset = File.ReadAllText(levelAssetFilePath);
            string dir = Path.Combine(lvlPath, "Bundles", "Assets");
            DevkitServerUtility.CheckDirectory(false, dir);

            assetGuid = Guid.NewGuid();
            string path = Path.Combine(dir, mapName + ".asset");
            using (StreamWriter writer = new StreamWriter(path))
            {
                writer.Write(defaultLevelAsset.Replace(LevelAsset.defaultLevel.GUID.ToString("N"), assetGuid.ToString("N")));
            }

            AssetOrigin origin = new AssetOrigin { name = "Map \"" + mapName + "\"", workshopFileId = 0ul };

            AssetUtil.LoadFileSync(path, origin);
        }

        DevkitServerUtility.CheckDirectory(false, lvlPath);
        WriteLevelInfoConfigData(new LevelInfoConfigData
        {
            Use_Legacy_Clip_Borders = false,
            Use_Legacy_Fog_Height = false,
            Use_Legacy_Water = false,
            Use_Legacy_Ground = false,
            Use_Legacy_Oxygen_Height = false,
            Use_Legacy_Snow_Height = false,
            Asset = new AssetReference<LevelAsset>(assetGuid),
            Creators = owner.GetEAccountType() == EAccountType.k_EAccountTypeIndividual ? new string[]
            {
                owner.m_SteamID.ToString()
            } : Array.Empty<string>(),
            Thanks = new string[]
            {
                DevkitServerModule.ModuleName + " - BlazingFlame"
            }
        }, Path.Combine(lvlPath, "Config.json"));

        // todo starter tiles, foliage system, etc.
    }
    internal static void WriteLevelInfoConfigData(LevelInfoConfigData data, string path)
    {
        FieldInfo[] fields = typeof(LevelInfoConfigData).GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);

        LevelInfoConfigData defaultValues = new LevelInfoConfigData();

        using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        Utf8JsonWriter writer = new Utf8JsonWriter(stream, DevkitServerConfig.WriterOptions);

        writer.WriteStartObject();

        foreach (FieldInfo field in fields.Where(x => !Attribute.IsDefined(x, typeof(Newtonsoft.Json.JsonIgnoreAttribute))))
        {
            object defaultValue = field.GetValue(defaultValues);
            object value = field.GetValue(data);
            if (defaultValue == null && value == null)
                continue;
            if (value == null)
            {
                writer.WritePropertyName(field.Name);
                writer.WriteNullValue();
                continue;
            }

            if (value.Equals(defaultValue))
                continue;

            if (typeof(IList).IsAssignableFrom(field.FieldType))
            {
                IList listA = (IList)value;
                IList? listB = (IList?)defaultValue;
                if (listB != null && listA.Count == listB.Count)
                {
                    bool eq = true;
                    for (int i = 0; i < listA.Count; ++i)
                    {
                        object valA = listA[i];
                        object valB = listB[i];
                        if (valA == null)
                        {
                            if (valB == null)
                                continue;
                            eq = false;
                            break;
                        }

                        if (!valA.Equals(valB))
                        {
                            eq = false;
                            break;
                        }
                    }

                    if (eq)
                        continue;
                }
            }
            else if (typeof(IDictionary).IsAssignableFrom(field.FieldType))
            {
                IDictionary dictA = (IDictionary)value;
                IDictionary? dictB = (IDictionary?)defaultValue;
                if (dictB != null && dictA.Count == dictB.Count && dictA.Count == 0)
                    continue;
            }

            writer.WritePropertyName(field.Name);

            if (field.FieldType.IsEnum)
                writer.WriteStringValue(value.ToString());
            else
                JsonSerializer.Serialize(writer, value, field.FieldType, DevkitServerConfig.SerializerSettings);
        }

        writer.WriteEndObject();
        writer.Flush();
    }
}
#endif