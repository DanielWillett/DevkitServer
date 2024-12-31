using DanielWillett.ReflectionTools;
using DevkitServer.Configuration;
using SDG.Framework.Devkit;
using SDG.Framework.Foliage;
using SDG.Framework.IO.FormattedFiles.KeyValueTables;
using SDG.Framework.Landscapes;
using SDG.Framework.Water;
using System.Reflection;
using System.Text.Json;
#if CLIENT
using HarmonyLib;
using System.Reflection.Emit;
#endif

namespace DevkitServer.Patches;
public static class MapCreation
{
    private static readonly int MapArgb = FormattingUtil.ToArgb(new Color32(102, 255, 102, 255));
#if SERVER
    internal static void PrefixLoadingDedicatedUGC()
    {
        LevelInfo level = Level.getLevel(Provider.map);

        if (level != null)
        {
            Logger.DevkitServer.LogInfo(nameof(MapCreation), $"Loading existing map: {Provider.map.Colorize(MapArgb)}. Size: {level.size.Format()}. Type: {level.type.Format()}.");
            return;
        }

        if (!ReadWrite.folderExists("/Extras/LevelTemplate/", true))
        {
            Logger.DevkitServer.LogError(nameof(MapCreation), $"Folder missing for level creation: {@"\Extras\LevelTemplate\".Format(true)}!");
            return;
        }

        DevkitServerSystemConfig.NewLevelCreationOptions options = DevkitServerConfig.Config.NewLevelInfo ??= DevkitServerSystemConfig.NewLevelCreationOptions.Default;
        CreateMap(Provider.map, options.LevelSize, options.LevelType);
    }
#elif CLIENT
    internal static IEnumerable<CodeInstruction> TranspileOnClickedAddLevelButton(IEnumerable<CodeInstruction> instructions, MethodBase method)
    {
        MethodInfo? levelAdd = typeof(Level).GetMethod("add", BindingFlags.Public | BindingFlags.Static, null, CallingConventions.Any, new Type[] { typeof(string), typeof(ELevelSize), typeof(ELevelType) }, null);
        if (levelAdd == null)
        {
            Logger.DevkitServer.LogWarning(nameof(MapCreation), $"{method.Format()} - Unable to find method: Level.add(string, ELevelSize, ELevelType).");
        }

        bool patched = false;
        foreach (CodeInstruction ins in instructions)
        {
            if (levelAdd != null && ins.Calls(levelAdd))
            {
                CodeInstruction newInst = new CodeInstruction(OpCodes.Call, Accessor.GetMethod(CreateMap)!);
                ins.MoveBlocksAndLabels(newInst);
                patched = true;
                Logger.DevkitServer.LogDebug(nameof(MapCreation), $"{method.Format()} - Replaced call to {levelAdd.Format()} with {newInst.operand.Format()}.");
                yield return newInst;
            }
            else yield return ins;
        }

        if (!patched)
        {
            Logger.DevkitServer.LogWarning(nameof(MapCreation), $"{method.Format()} - Unable to replace call to {(levelAdd == null ? "Level.add" : levelAdd.Format())} with a better map creation implemention.");
        }
    }
#endif

    /// <summary>
    /// <br/>This method creates a new map and preloads it with some convenience data:
    /// <br/>
    /// <br/>• Owner set to either what's in config or the first admin on the server.
    /// <br/>• 'DevkitServer' is added to the Thanks section of the level config (as a template, feel free to change).
    /// <br/>• All legacy features are turned off by default.
    /// <br/>• A copy of the default level asset is created with a new GUID.
    /// <br/>• Tiles are created based on the selected size.
    /// <br/>• A FoliageSystem is added.
    /// <br/>• Player clip volumes are added as a world border based on the selected size.
    /// <br/>• Cartography volume is added.
    /// <br/>• Water volume is added.
    /// <br/>• Non-breathable oxygen volume with falloff is added.
    /// </summary>
    public static void CreateMap(string mapName, ELevelSize size, ELevelType type)
    {
#if SERVER
        DevkitServerSystemConfig.NewLevelCreationOptions options = DevkitServerConfig.Config.NewLevelInfo ?? DevkitServerSystemConfig.NewLevelCreationOptions.Default;
        FieldInfo? clientField = typeof(Provider).GetField("_client", BindingFlags.Static | BindingFlags.NonPublic);
        CSteamID owner = options.Owner;

        if (clientField == null || clientField.FieldType != typeof(CSteamID))
        {
            Level.add(mapName, size, type);
            Logger.DevkitServer.LogWarning(nameof(MapCreation), $"Unable to set owner of newly created map, it will be set to: {Provider.client.Format()}.");

            Logger.DevkitServer.LogInfo(nameof(MapCreation), $"Level created: {mapName.Colorize(MapArgb)}. Size: {size.Format()}. Type: {type.Format()}.");
        }
        else
        {
            if (owner.m_SteamID == 0ul)
            {
                owner = SteamAdminlist.ownerID;

                if (owner.m_SteamID == 0ul && SteamAdminlist.list is { Count: > 0 })
                    owner = SteamAdminlist.list[0].playerID;
            }

            CSteamID clientOld = (CSteamID)clientField.GetValue(null);
            clientField.SetValue(null, owner);
            Level.add(mapName, size, type);
            clientField.SetValue(null, clientOld);

            Logger.DevkitServer.LogInfo(nameof(MapCreation), $"Level created: {mapName.Colorize(MapArgb)}. Size: {size.Format()}. Type: {type.Format()}. Owner: {owner.Format()}.");
        }
#else
        Level.add(mapName, size, type);

        Logger.DevkitServer.LogInfo(nameof(MapCreation), $"Level created: {mapName.Colorize(MapArgb)}. Size: {size.Format()}. Type: {type.Format()}. Owner: {Provider.client.Format()} ({Provider.clientName.Format(false)}).");
#endif
        string lvlPath = Path.GetFullPath(Path.Combine(ReadWrite.PATH, "Maps", mapName));

        Guid assetGuid = LevelAsset.defaultLevel.GUID;

        string? levelAssetFilePath = LevelAsset.defaultLevel.Find()?.getFilePath();
        if (levelAssetFilePath != null && File.Exists(levelAssetFilePath))
        {
            // Copy over the default level asset but replace the GUID with a new one
            string defaultLevelAsset = File.ReadAllText(levelAssetFilePath);
            string dir = Path.Combine(lvlPath, "Bundles", "Assets");
            FileUtil.CheckDirectory(false, dir);

            assetGuid = Guid.NewGuid();
            string path = Path.Combine(dir, mapName + ".asset");
            using (StreamWriter sw = new StreamWriter(path))
            {
                sw.Write(defaultLevelAsset.Replace(LevelAsset.defaultLevel.GUID.ToString("N"), assetGuid.ToString("N")));
            }

            AssetOrigin origin = new AssetOrigin { name = "Map \"" + mapName + "\"", workshopFileId = 0ul };

            AssetUtil.LoadFileSync(path, origin);
            AssetUtil.SyncAssetsFromOrigin(origin);
        }

        FileUtil.CheckDirectory(false, lvlPath);
        WriteLevelInfoConfigData(new LevelInfoConfigData
        {
            Use_Legacy_Clip_Borders = false,
            Use_Legacy_Fog_Height = false,
            Use_Legacy_Water = false,
            Use_Legacy_Ground = false,
            Use_Legacy_Oxygen_Height = false,
            Use_Legacy_Snow_Height = false,
            Version = "3.0.0.0",
            Asset = new AssetReference<LevelAsset>(assetGuid),
            Tips = 1,
#if SERVER
            Creators = owner.GetEAccountType() == EAccountType.k_EAccountTypeIndividual ? new string[]
            {
                owner.m_SteamID.ToString()
            }
#else
            Creators = !string.IsNullOrEmpty(Provider.clientName) ? new string[]
            {
                Provider.clientName
            }
#endif
            : new string[]
            {
                "Enter creators here."
            },
            Collaborators = new string[]
            {
                "Enter collaborators here."
            },
            Thanks = new string[]
            {
                DevkitServerModule.ModuleName
            }
        }, Path.Combine(lvlPath, "Config.json"));

        DevkitServerUtility.WriteData(Path.Combine(lvlPath, "English.dat"), new DatDictionary
        {
            { "Name", new DatValue(FormattingUtil.SpaceProperCaseString(mapName)) },
            { "Description", new DatValue("Sick new " + size.ToString().ToLower() + " map.") },
            { "Tip_0", new DatValue("Use DevkitServer to collaborate on maps.") }
        });

        using FileStream fs = new FileStream(Path.Combine(lvlPath, "Level.hierarchy"), FileMode.Create, FileAccess.Write, FileShare.Read);
        using StreamWriter tw = new StreamWriter(fs);
        KeyValueTableWriter writer = new KeyValueTableWriter(tw);

        int worldSize = size switch
        {
            ELevelSize.TINY => Level.TINY_SIZE,
            ELevelSize.SMALL => Level.SMALL_SIZE,
            ELevelSize.LARGE => Level.LARGE_SIZE,
            ELevelSize.INSANE => Level.INSANE_SIZE,
            _ => Level.MEDIUM_SIZE
        };
        int border = size switch
        {
            ELevelSize.TINY => Level.TINY_BORDER,
            ELevelSize.SMALL => Level.SMALL_BORDER,
            ELevelSize.LARGE => Level.LARGE_BORDER,
            ELevelSize.INSANE => Level.INSANE_BORDER,
            _ => Level.MEDIUM_BORDER
        };
        Vector2 center = Vector2.zero;
        if (size == ELevelSize.TINY)
        {
            center = new Vector2(Landscape.TILE_SIZE / 2f, Landscape.TILE_SIZE / 2f);
        }
        int ground2Size = size is ELevelSize.TINY or ELevelSize.SMALL ? (worldSize * 2) : (worldSize + 2 * Landscape.TILE_SIZE_INT);
        int tiles = Mathf.CeilToInt(ground2Size / Landscape.TILE_SIZE);
        int half = Mathf.CeilToInt(tiles / 2f);
        writer.writeValue("Available_Instance_ID", 13);
        writer.beginArray("Items");
        writer.beginObject();
        writer.writeValue("Type", typeof(Landscape).AssemblyQualifiedName);
        writer.writeValue("Instance_ID", 1);
        writer.beginObject("Item");
        writer.beginArray("Tiles");

        AssetReference<LandscapeMaterialAsset>[] defaultMaterials =
        {
            new AssetReference<LandscapeMaterialAsset>("e52b20e26b7c47c89aa5a350938f8f42"),
            new AssetReference<LandscapeMaterialAsset>("e981f9fae3fa43d68a9a0bfa6472a69f"),
            new AssetReference<LandscapeMaterialAsset>("5020515a0b9a4b1eb610c006d81f806c"),
            new AssetReference<LandscapeMaterialAsset>("a14df8dd9bb44f1d967a53f43bde54e6"),
            new AssetReference<LandscapeMaterialAsset>("8729d40d361c4947be4188c70dd7100b"),
            new AssetReference<LandscapeMaterialAsset>("684f4b28200d4ceb9c5362d78d2c9619"),
            new AssetReference<LandscapeMaterialAsset>("d691f78202c84951a3a697f310abd115"),
            new AssetReference<LandscapeMaterialAsset>("50acf0bddd844f93addd0097f7d95d95")
        };
        
        for (int x = 0; x < half; ++x)
        {
            int posX2 = -(x + 1);
            for (int y = 0; y < half; ++y)
            {
                int posY2 = -(y + 1);

                WriteTile(x, y);
                if (y == half - 1 && x == half - 1 && tiles % 2 == 1)
                    continue;

                if (x != posX2)
                    WriteTile(posX2, y);
                if (y != posY2)
                    WriteTile(x, posY2);
                if (y != posY2 && x != posX2)
                    WriteTile(posX2, posY2);
            }
        }

        writer.endArray();
        writer.endObject();
        writer.endObject();


        // Foliage System and Volume

        writer.beginObject();
        writer.writeValue("Type", typeof(FoliageSystem).AssemblyQualifiedName);
        writer.writeValue("Instance_ID", 2);
        writer.beginObject("Item");
        writer.writeValue("Version", 2);

        writer.beginArray("Tiles");
        writer.endArray();

        writer.endObject();
        writer.endObject();

        writer.beginObject();
        writer.writeValue("Type", typeof(FoliageVolume).AssemblyQualifiedName);
        writer.writeValue("Instance_ID", 3);
        writer.beginObject("Item");

        writer.beginObject("Position");
        writer.writeValue("X", center.x);
        writer.writeValue("Y", 0f);
        writer.writeValue("Z", center.y);
        writer.endObject();

        writer.beginObject("Rotation");
        writer.writeValue("X", 0f);
        writer.writeValue("Y", 0f);
        writer.writeValue("Z", 0f);
        writer.writeValue("W", 1f);
        writer.endObject();

        writer.beginObject("Scale");
        writer.writeValue("X", worldSize - 2 * border);
        writer.writeValue("Y", Level.HEIGHT * 2f);
        writer.writeValue("Z", worldSize - 2 * border);
        writer.endObject();
        writer.writeValue("Shape", "Box");
        writer.writeValue("Mode", "ADDITIVE");
        writer.writeValue("Instanced_Meshes", true);
        writer.writeValue("Resources", true);
        writer.writeValue("Objects", true);

        writer.endObject();
        writer.endObject();


        // Cartography Volume

        writer.beginObject();
        writer.writeValue("Type", typeof(CartographyVolume).AssemblyQualifiedName);
        writer.writeValue("Instance_ID", 4);
        writer.beginObject("Item");

        writer.beginObject("Position");
        writer.writeValue("X", center.x);
        writer.writeValue("Y", 0f);
        writer.writeValue("Z", center.y);
        writer.endObject();

        writer.beginObject("Rotation");
        writer.writeValue("X", 0f);
        writer.writeValue("Y", 0f);
        writer.writeValue("Z", 0f);
        writer.writeValue("W", 1f);
        writer.endObject();

        writer.beginObject("Scale");
        writer.writeValue("X", worldSize - 2 * border);
        writer.writeValue("Y", Level.HEIGHT * 2f);
        writer.writeValue("Z", worldSize - 2 * border);
        writer.endObject();
        writer.writeValue("Shape", "Box");

        writer.endObject();
        writer.endObject();

        float defaultSeaLevel = 0.1f * Level.TERRAIN;

        float oxygenHeight = Level.HEIGHT - defaultSeaLevel;
        float oxygenY = defaultSeaLevel + oxygenHeight;
        float falloff = oxygenHeight / 2;

        float oxygenSize = worldSize - 2 * border;

        writer.beginObject();
        writer.writeValue("Type", typeof(OxygenVolume).AssemblyQualifiedName);
        writer.writeValue("Instance_ID", 5);
        writer.beginObject("Item");

        writer.beginObject("Position");
        writer.writeValue("X", center.x);
        writer.writeValue("Y", oxygenY);
        writer.writeValue("Z", center.y);
        writer.endObject();

        writer.beginObject("Rotation");
        writer.writeValue("X", 0f);
        writer.writeValue("Y", 0f);
        writer.writeValue("Z", 0f);
        writer.writeValue("W", 1f);
        writer.endObject();

        writer.beginObject("Scale");
        writer.writeValue("X", oxygenSize + falloff * 2);
        writer.writeValue("Y", oxygenHeight);
        writer.writeValue("Z", oxygenSize + falloff * 2);
        writer.endObject();
        writer.writeValue("Shape", "Box");
        writer.writeValue("Falloff", falloff);
        writer.writeValue("Is_Breathable", "false");

        writer.endObject();
        writer.endObject();


        // Player Clip Volumes

        // bottom
        WritePlayerClip(
            new Vector3(center.x, -Level.HEIGHT - 4f, center.y),
            Quaternion.Euler(-90f, 0f, 0f),
            new Vector3(worldSize - border * 2 + Level.CLIP * 2, worldSize - border * 2 + Level.CLIP * 2, 1f),
            6);

        // top
        WritePlayerClip(
            new Vector3(center.x, Level.HEIGHT + 4f, center.y),
            Quaternion.Euler(-90f, 0f, 0f),
            new Vector3(worldSize - border * 2 + Level.CLIP * 2, worldSize - border * 2 + Level.CLIP * 2, 1f),
            7);

        // sides
        WritePlayerClip(
            new Vector3(center.x, 0f, center.y + (-worldSize / 2f + border)),
            Quaternion.identity,
            new Vector3(worldSize - border * 2, Level.HEIGHT * 2 + 8f, 1f),
            8);
        WritePlayerClip(
            new Vector3(center.x + (worldSize / 2f - border), 0f, center.y),
            Quaternion.Euler(0f, -90f, 0f),
            new Vector3(worldSize - border * 2, Level.HEIGHT * 2 + 8f, 1f),
            9);
        WritePlayerClip(
            new Vector3(center.x + (-worldSize / 2f + border), 0f, center.y),
            Quaternion.Euler(0f, 90f, 0f),
            new Vector3(worldSize - border * 2, Level.HEIGHT * 2 + 8f, 1f),
            10);
        WritePlayerClip(
            new Vector3(center.x, 0f, center.y + (worldSize / 2f - border)),
            Quaternion.Euler(0f, 180f, 0f),
            new Vector3(worldSize - border * 2, Level.HEIGHT * 2 + 8f, 1f),
            11);


        // Water Volume

        writer.beginObject();
        writer.writeValue("Type", typeof(WaterVolume).AssemblyQualifiedName);
        writer.writeValue("Instance_ID", 12);
        writer.beginObject("Item");

        writer.beginObject("Position");
        writer.writeValue("X", center.x);
        writer.writeValue("Y", 0.1f * Level.TERRAIN * 0.5f - 512f);
        writer.writeValue("Z", center.y);
        writer.endObject();

        writer.beginObject("Rotation");
        writer.writeValue("X", 0f);
        writer.writeValue("Y", 0f);
        writer.writeValue("Z", 0f);
        writer.writeValue("W", 1f);
        writer.endObject();

        writer.beginObject("Scale");
        writer.writeValue("X", ground2Size);
        writer.writeValue("Y", 1024f + 0.1f * Level.TERRAIN);
        writer.writeValue("Z", ground2Size);
        writer.endObject();
        writer.writeValue("Shape", "Box");
        writer.writeValue("Is_Surface_Visible", true);
        writer.writeValue("Is_Reflection_Visible", true);
        writer.writeValue("Is_Sea_Level", true);
        writer.writeValue("Water_Type", ERefillWaterType.SALTY);

        writer.endObject();
        writer.endObject();

        tw.Flush();

#if CLIENT
        // update cached config file
        AssetUtil.RescanLevel(lvlPath);
#endif

        void WriteTile(int posX, int posY)
        {
            writer.beginObject();

            writer.beginObject("Coord");
            writer.writeValue("X", posX);
            writer.writeValue("Y", posY);
            writer.endObject();

            writer.beginArray("Materials");
            for (int i = 0; i < defaultMaterials.Length; ++i)
            {
                writer.beginObject();
                writer.writeValue("GUID", defaultMaterials[i].GUID);
                writer.endObject();
            }

            writer.endArray();

            writer.endObject();
        }

        void WritePlayerClip(Vector3 pos, Quaternion rot, Vector3 scale, uint instanceId)
        {
            writer.beginObject();
            writer.writeValue("Type", typeof(PlayerClipVolume).AssemblyQualifiedName);
            writer.writeValue("Instance_ID", instanceId);
            writer.beginObject("Item");

            writer.beginObject("Position");
            writer.writeValue("X", pos.x);
            writer.writeValue("Y", pos.y);
            writer.writeValue("Z", pos.z);
            writer.endObject();

            writer.beginObject("Rotation");
            writer.writeValue("X", rot.x);
            writer.writeValue("Y", rot.y);
            writer.writeValue("Z", rot.z);
            writer.writeValue("W", rot.w);
            writer.endObject();

            writer.beginObject("Scale");
            writer.writeValue("X", scale.x);
            writer.writeValue("Y", scale.y);
            writer.writeValue("Z", scale.z);
            writer.endObject();
            writer.writeValue("Shape", "Box");
            writer.writeValue("Block_Player", true);

            writer.endObject();
            writer.endObject();
        }
    }

    /// <summary>
    /// Write a config to a json file at <paramref name="path"/>.
    /// </summary>
    public static void WriteLevelInfoConfigData(LevelInfoConfigData data, string path)
    {
        FieldInfo[] fields = typeof(LevelInfoConfigData).GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);

        LevelInfoConfigData defaultValues = new LevelInfoConfigData();

        using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        Utf8JsonWriter writer = new Utf8JsonWriter(stream, DevkitServerConfig.WriterOptions);

        writer.WriteStartObject();

        foreach (FieldInfo field in fields.Where(x => !x.HasAttributeSafe(typeof(Newtonsoft.Json.JsonIgnoreAttribute))))
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