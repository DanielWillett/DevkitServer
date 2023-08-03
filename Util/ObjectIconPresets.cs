#if CLIENT
using DevkitServer.API;
using DevkitServer.Configuration;
using DevkitServer.Players;
using DevkitServer.Plugins;
using System.Text.Json.Serialization;

namespace DevkitServer.Util;
internal static class ObjectIconPresets
{
    private static readonly List<JsonConfigurationFile<List<AssetIconPreset>>> _presetProviders = new List<JsonConfigurationFile<List<AssetIconPreset>>>(1);
    private static JsonConfigurationFile<List<AssetIconPreset>>? _customPresets;
    private static readonly string _customPresetsPath = Path.Combine(DevkitServerConfig.Directory, "configured_icons.json");
    public static Dictionary<Guid, AssetIconPreset> Presets = new Dictionary<Guid, AssetIconPreset>(128);
    public static AssetIconPreset? ActivelyEditing { get; private set; }
    public static void Init()
    {
        Level.onPostLevelLoaded += OnLevelLoaded;
    }
    public static void Deinit()
    {
        Level.onPostLevelLoaded += OnLevelLoaded;
    }
    public static void UpdateEditCache(LevelObject levelObject, ObjectAsset asset)
    {
        Transform? ctrl = EditorUser.User?.Input.Aim;
        if (ctrl == null)
            return;

        Transform? transform = levelObject.GetTransform();
        if (transform == null)
            return;
        if (ActivelyEditing == null || ActivelyEditing.Asset.GUID != asset.GUID)
        {
            ActivelyEditing = new AssetIconPreset
            {
                Asset = asset.getReferenceTo<ObjectAsset>(),
                File = null
            };
        }

        ActivelyEditing.IconPosition = transform.InverseTransformPoint(ctrl.transform.position);
        ActivelyEditing.IconRotation = transform.InverseTransformRotation(ctrl.transform.rotation);

        IconGenerator.ClearCache(asset.GUID);
    }
    public static void SaveEditCache(bool asNew)
    {
        AssetIconPreset? preset = ActivelyEditing;
        if (preset == null)
            return;
        Guid guid = preset.Asset.GUID;

        asNewRedo:
        if (asNew)
        {
            AssetIconPreset? existing = null;
            bool contains = false;
            if (_customPresets != null)
            {
                existing = _customPresets.Configuration.Where(x => x.Asset.GUID == guid).OrderByDescending(x => x.Priority).FirstOrDefault();
                contains = existing != null;
            }
            else _presetProviders.Add(_customPresets = new JsonConfigurationFile<List<AssetIconPreset>>(_customPresetsPath) { Defaultable = true });

            existing ??= new AssetIconPreset
            {
                Asset = new AssetReference<ObjectAsset>(guid),
                File = _customPresets.File
            };
            int priority = -1;
            foreach (AssetIconPreset p in _presetProviders.SelectMany(x => x.Configuration).Where(x => x.Asset.GUID == guid && x != existing))
            {
                if (priority < p.Priority)
                    priority = p.Priority;
            }
            ++priority;

            existing.Priority = priority;
            existing.IconPosition = preset.IconPosition;
            existing.IconRotation = preset.IconRotation;
            if (!contains)
                _customPresets.Configuration.Add(existing);
            _customPresets.SaveConfig();
            Presets[guid] = existing;
            Logger.LogInfo($"Updated asset icon preset: {preset.Asset.Format()}, saved to {_customPresets.File.Format()}.");
        }
        else
        {
            (JsonConfigurationFile<List<AssetIconPreset>> config, AssetIconPreset? existing) = _presetProviders
                .Where(x => !x.ReadOnlyReloading)
                .SelectMany(x => x.Configuration.Select(y => (x, y)))
                .Where(x => x.y.Asset.GUID == guid)
                .OrderByDescending(x => x.y.Priority)
                .FirstOrDefault();
            if (existing == null)
            {
                asNew = true;
                goto asNewRedo;
            }
            int priority = -1;
            foreach (AssetIconPreset p in _presetProviders.SelectMany(x => x.Configuration).Where(x => x.Asset.GUID == guid && x != existing))
            {
                if (priority < p.Priority)
                    priority = p.Priority;
            }
            ++priority;
            existing.Priority = priority;
            existing.IconPosition = preset.IconPosition;
            existing.IconRotation = preset.IconRotation;
            config.SaveConfig();
            Presets[guid] = existing;
            Logger.LogInfo($"Updated asset icon preset: {preset.Asset.Format()}, saved to {config.File.Format()}.");
        }

        ClearEditCache();
    }
    public static void ClearEditCache()
    {
        if (ActivelyEditing == null)
            return;
        
        IconGenerator.ClearCache(ActivelyEditing.Asset.GUID);
        ActivelyEditing = null;
    }
    private static void OnLevelLoaded(int level)
    {
        if (level != Level.BUILD_INDEX_GAME)
            return;

        ReloadPresetProviders();
    }

    public static void ReloadPresetProviders()
    {
        ThreadUtil.assertIsGameThread();

        foreach (JsonConfigurationFile<List<AssetIconPreset>> config in _presetProviders)
            config.OnRead -= OnConfigReloaded;
        _presetProviders.Clear();
        string dir;
        if (Provider.provider?.workshopService?.ugc != null)
        {
            foreach (SteamContent content in Provider.provider.workshopService.ugc)
            {
                dir = content.type == ESteamUGCType.MAP ? Path.Combine(content.path, "Bundles") : content.path;
                if (!Directory.Exists(dir))
                    continue;
                foreach (string directory in Directory.EnumerateDirectories(dir, "*", SearchOption.AllDirectories))
                    DiscoverAssetIconPresetProvidersIn(directory, true);
            }
        }

        int workshop = _presetProviders.Count;

        if (Level.info != null && Level.info.path != null)
        {
            dir = Path.Combine(Level.info.path, "Bundles");
            if (Directory.Exists(dir))
            {
                foreach (string directory in Directory.EnumerateDirectories(Path.Combine(Level.info.path, "Bundles"), "*", SearchOption.AllDirectories))
                    DiscoverAssetIconPresetProvidersIn(directory, DevkitServerModule.IsEditing);
            }
        }

        dir = Path.Combine(ReadWrite.PATH, "Sandbox");
        if (Directory.Exists(dir))
        {
            foreach (string directory in Directory.EnumerateDirectories(dir, "*", SearchOption.AllDirectories))
                DiscoverAssetIconPresetProvidersIn(directory, false);
        }

        DiscoverAssetIconPresetProvidersIn(Path.Combine(ReadWrite.PATH, "Bundles"), true);

        dir = Path.Combine(ReadWrite.PATH, "Bundles", "Objects");
        if (Directory.Exists(dir))
        {
            DiscoverAssetIconPresetProvidersIn(dir, true);
            foreach (string directory in Directory.EnumerateDirectories(dir, "*", SearchOption.AllDirectories))
                DiscoverAssetIconPresetProvidersIn(directory, true);
        }
        
        foreach (string directory in DevkitServerModule.AssemblyFileSearchLocations)
            DiscoverAssetIconPresetProvidersIn(directory, true);

        DiscoverAssetIconPresetProvidersIn(DevkitServerConfig.Directory, true);

        foreach (IDevkitServerPlugin plugin in PluginLoader.Plugins)
        {
            if (plugin.DataDirectory is not { Length: > 0 } dir2 || !Directory.Exists(dir2))
                continue;
            foreach (string directory in Directory.EnumerateDirectories(dir2, "*", SearchOption.AllDirectories))
                DiscoverAssetIconPresetProvidersIn(directory, !plugin.DeveloperMode);
        }
        
        if (!_presetProviders.Exists(x => x.File.Equals(_customPresetsPath, StringComparison.Ordinal)))
            _presetProviders.Add(_customPresets = new JsonConfigurationFile<List<AssetIconPreset>>(Path.GetFullPath(_customPresetsPath)) { Defaultable = true });

        Presets.Clear();
        int ct = DefaultPresets.Count;

        foreach (AssetIconPreset preset in DefaultPresets)
        {
            preset.Priority = int.MinValue;
            preset.File = null;
            Presets[preset.Asset.GUID] = preset;
        }


        for (int i = _presetProviders.Count - 1; i >= 0; i--)
        {
            JsonConfigurationFile<List<AssetIconPreset>> configFile = _presetProviders[i];
            configFile.ReloadConfig();
            if (configFile.Configuration is not { Count: > 0 })
            {
                _presetProviders.RemoveAt(i);
                continue;
            }
            configFile.Configuration.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            configFile.OnRead += OnConfigReloaded;
            ct += configFile.Configuration.Count;
            foreach (AssetIconPreset preset in configFile.Configuration)
            {
                if (preset.Asset.Find() == null)
                {
                    if (i < workshop)
                        Logger.LogWarning($"Object not found for icon preset: {preset.Asset.GUID.Format()} in {configFile.File.Format()}.", method: IconGenerator.Source);
                    else
                        Logger.LogDebug($"[{IconGenerator.Source}] Object not found for workshop icon preset: {preset.Asset.GUID.Format()} in {configFile.File.Format()}.");
                    return;
                }
                preset.File = configFile.File;
                if (!Presets.TryGetValue(preset.Asset.GUID, out AssetIconPreset existing) || existing.Priority < preset.Priority)
                    Presets[preset.Asset.GUID] = preset;
            }
        }

        Logger.LogInfo($"[{IconGenerator.Source}] Registered {Presets.Count.Format()} unique icon presets from {ct.Format()} presets.");

        GC.Collect();
    }
    private static void OnConfigReloaded()
    {
        IconGenerator.ClearCache();
    }
    private static void DiscoverAssetIconPresetProvidersIn(string path, bool isReadonly)
    {
        if (!Directory.Exists(path))
            return;

        foreach (string file in Directory.EnumerateFiles(path, "*.json", SearchOption.TopDirectoryOnly))
        {
            string name = Path.GetFileNameWithoutExtension(file);
            if ((name.StartsWith("object icons", StringComparison.InvariantCultureIgnoreCase)
                || name.StartsWith("object_icons", StringComparison.InvariantCultureIgnoreCase)
                || name.StartsWith("object icon presets", StringComparison.InvariantCultureIgnoreCase)
                || name.StartsWith("object_icon_presets", StringComparison.InvariantCultureIgnoreCase)
                || name.StartsWith("object presets", StringComparison.InvariantCultureIgnoreCase)
                || name.StartsWith("object_presets", StringComparison.InvariantCultureIgnoreCase)
                ) && !_presetProviders.Exists(x => x.File.Equals(file, StringComparison.Ordinal)))
            {
                Logger.LogDebug($" + Registered icon provider {file.Format()}.");
                _presetProviders.Add(new JsonConfigurationFile<List<AssetIconPreset>>(Path.GetFullPath(file)) { ReadOnlyReloading = isReadonly });
            }
        }
    }

    private static readonly List<AssetIconPreset> DefaultPresets = new List<AssetIconPreset>(128)
    {
        /* Billboard 0-16 */
        new AssetIconPreset
        {
            Asset = new AssetReference<ObjectAsset>(new Guid("205a8cc33c9849c9bd65790403d0753d")),
            IconPosition = new Vector3(-3f, -10f, 9f),
            IconRotation = Quaternion.Euler(-107f, -80f, 260f)
        },
        new AssetIconPreset
        {
            Asset = new AssetReference<ObjectAsset>(new Guid("a227d89ce4f34339a0124c146c1f8218")),
            IconPosition = new Vector3(-3f, -10f, 9f),
            IconRotation = Quaternion.Euler(-107f, -80f, 260f)
        },
        new AssetIconPreset
        {
            Asset = new AssetReference<ObjectAsset>(new Guid("a8681fcb59d44b1588caec49f30a9c8f")),
            IconPosition = new Vector3(-3f, -10f, 9f),
            IconRotation = Quaternion.Euler(-107f, -80f, 260f)
        },
        new AssetIconPreset
        {
            Asset = new AssetReference<ObjectAsset>(new Guid("70a1a85305a54009890ad292cd31b4ba")),
            IconPosition = new Vector3(-3f, -10f, 9f),
            IconRotation = Quaternion.Euler(-107f, -80f, 260f)
        },
        new AssetIconPreset
        {
            Asset = new AssetReference<ObjectAsset>(new Guid("8631144673b34a89af1e6f7160591892")),
            IconPosition = new Vector3(-3f, -10f, 9f),
            IconRotation = Quaternion.Euler(-107f, -80f, 260f)
        },
        new AssetIconPreset
        {
            Asset = new AssetReference<ObjectAsset>(new Guid("4925ee9047d846eea13fe2ffd91d34a3")),
            IconPosition = new Vector3(-3f, -10f, 9f),
            IconRotation = Quaternion.Euler(-107f, -80f, 260f)
        },
        new AssetIconPreset
        {
            Asset = new AssetReference<ObjectAsset>(new Guid("61b260059a16486da1cd434cf7f2a88f")),
            IconPosition = new Vector3(-3f, -10f, 9f),
            IconRotation = Quaternion.Euler(-107f, -80f, 260f)
        },
        new AssetIconPreset
        {
            Asset = new AssetReference<ObjectAsset>(new Guid("026f1c9406c34b8786517e1a6a10db4b")),
            IconPosition = new Vector3(-3f, -10f, 9f),
            IconRotation = Quaternion.Euler(-107f, -80f, 260f)
        },
        new AssetIconPreset
        {
            Asset = new AssetReference<ObjectAsset>(new Guid("ebfb3476f7f0406f88d0cba7054afc3f")),
            IconPosition = new Vector3(-3f, -10f, 9f),
            IconRotation = Quaternion.Euler(-107f, -80f, 260f)
        },
        new AssetIconPreset
        {
            Asset = new AssetReference<ObjectAsset>(new Guid("4182d6f8e54044f08c30d1961bb1dbec")),
            IconPosition = new Vector3(-3f, -10f, 9f),
            IconRotation = Quaternion.Euler(-107f, -80f, 260f)
        },
        new AssetIconPreset
        {
            Asset = new AssetReference<ObjectAsset>(new Guid("7f48479a84a94592932e61684af5ecc6")),
            IconPosition = new Vector3(-3f, -10f, 9f),
            IconRotation = Quaternion.Euler(-107f, -80f, 260f)
        },
        new AssetIconPreset
        {
            Asset = new AssetReference<ObjectAsset>(new Guid("b6f9ed46e7a04072834094ba45bf7bc2")),
            IconPosition = new Vector3(-3f, -10f, 9f),
            IconRotation = Quaternion.Euler(-107f, -80f, 260f)
        },
        new AssetIconPreset
        {
            Asset = new AssetReference<ObjectAsset>(new Guid("74a98d29022343a0904bd6641c96f8c1")),
            IconPosition = new Vector3(-3f, -10f, 9f),
            IconRotation = Quaternion.Euler(-107f, -80f, 260f)
        },
        new AssetIconPreset
        {
            Asset = new AssetReference<ObjectAsset>(new Guid("60b06f3164e24fcf8025fdabbad01841")),
            IconPosition = new Vector3(-3f, -10f, 9f),
            IconRotation = Quaternion.Euler(-107f, -80f, 260f)
        },
        new AssetIconPreset
        {
            Asset = new AssetReference<ObjectAsset>(new Guid("7d8cee8f7ce143d5819629c761fbe861")),
            IconPosition = new Vector3(-3f, -10f, 9f),
            IconRotation = Quaternion.Euler(-107f, -80f, 260f)
        },
        new AssetIconPreset
        {
            Asset = new AssetReference<ObjectAsset>(new Guid("5ba775bc88fe4171ab457e724179086a")),
            IconPosition = new Vector3(-3f, -10f, 9f),
            IconRotation = Quaternion.Euler(-107f, -80f, 260f)
        },
        new AssetIconPreset
        {
            Asset = new AssetReference<ObjectAsset>(new Guid("fe5a47b6ef0f4e3087028d57180e0b71")),
            IconPosition = new Vector3(-3f, -10f, 9f),
            IconRotation = Quaternion.Euler(-107f, -80f, 260f)
        }
    };
}

public class AssetIconPreset
{
    [JsonPropertyName("object")]
    [JsonConverter(typeof(AssetReferenceJsonConverterFactory.AssetReferenceJsonConverterGuidPreferred<ObjectAsset>))]
    public AssetReference<ObjectAsset> Asset { get; set; }

    [JsonPropertyName("position")]
    public Vector3 IconPosition { get; set; }

    [JsonPropertyName("rotation")]
    [JsonConverter(typeof(QuaternionEulerPreferredJsonConverter))]
    public Quaternion IconRotation { get; set; }

    [JsonPropertyName("priority")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Priority { get; set; }

    [JsonIgnore]
    public string? File { get; set; }
}
#endif