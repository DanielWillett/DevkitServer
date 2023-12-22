#if CLIENT
using DevkitServer.Configuration;
using DevkitServer.Players;
using DevkitServer.Plugins;
using SDG.Framework.Modules;
using System.Collections.ObjectModel;
using Module = SDG.Framework.Modules.Module;

namespace DevkitServer.API.UI.Icons;

/// <summary>
/// Storage provider for object icon presets.
/// </summary>
public static class ObjectIconPresets
{
    private static readonly List<JsonConfigurationFile<List<AssetIconPreset>>> _presetProviders = new List<JsonConfigurationFile<List<AssetIconPreset>>>(1);
    private static JsonConfigurationFile<List<AssetIconPreset>>? _customPresets;
    private static readonly Dictionary<Guid, AssetIconPreset> PresetsIntl = new Dictionary<Guid, AssetIconPreset>(128);
    private static readonly List<AssetIconPreset> DefaultPresets = new List<AssetIconPreset>(128);

    /// <summary>
    /// File path to the active configured icons file (DevkitServer/configured_icons.json).
    /// </summary>
    public static readonly string CustomPresetsPath = Path.Combine(DevkitServerConfig.Directory, "configured_icons.json");

    /// <summary>
    /// The current asset icon being edited.
    /// </summary>
    public static AssetIconPreset? ActivelyEditing { get; private set; }

    /// <summary>
    /// A map of object GUIDs to the highest priority <see cref="AssetIconPreset"/>s.
    /// </summary>
    public static IReadOnlyDictionary<Guid, AssetIconPreset> ActivePresets { get; } = new ReadOnlyDictionary<Guid, AssetIconPreset>(PresetsIntl);
    internal static void Init()
    {
        Level.onPostLevelLoaded += OnLevelLoaded;
    }
    internal static void Deinit()
    {
        Level.onPostLevelLoaded += OnLevelLoaded;
    }
    internal static void UpdateEditCache(LevelObject levelObject, ObjectAsset asset)
    {
        Transform? ctrl = UserInput.LocalAim;
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

        ObjectIconGenerator.ClearCache(asset.GUID);
    }
    internal static void SaveEditCache(bool asNew)
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
            else _presetProviders.Add(_customPresets = new JsonConfigurationFile<List<AssetIconPreset>>(CustomPresetsPath) { Defaultable = true });

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
            PresetsIntl[guid] = existing;

            Logger.DevkitServer.LogInfo(ObjectIconGenerator.Source, $"Updated asset icon preset: {preset.Asset.Format()}, saved to {_customPresets.File.Format()}.");
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
            PresetsIntl[guid] = existing;
            Logger.DevkitServer.LogInfo(ObjectIconGenerator.Source, $"Updated asset icon preset: {preset.Asset.Format()}, saved to {config.File.Format()}.");
        }

        ClearEditCache();
    }
    internal static void ClearEditCache()
    {
        if (ActivelyEditing == null)
            return;

        ObjectIconGenerator.ClearCache(ActivelyEditing.Asset.GUID);
        ActivelyEditing = null;
    }
    private static void OnLevelLoaded(int level)
    {
        if (level != Level.BUILD_INDEX_GAME)
            return;

        ReloadPresetProviders();
    }

    internal static void ReloadPresetProviders()
    {
        ThreadUtil.assertIsGameThread();

        foreach (JsonConfigurationFile<List<AssetIconPreset>> config in _presetProviders)
            config.OnRead -= OnConfigReloaded;
        _presetProviders.Clear();
        Logger.DevkitServer.LogDebug(ObjectIconGenerator.Source, "Searching for object icon preset provider JSON files.");
        string dir;
        if (Provider.isConnected)
        {
            // use server mapping instead of default mapping

            List<Asset> allAssets = new List<Asset>();
            Assets.find(allAssets);
            foreach (AssetOrigin? origin in allAssets.Select(x => x.GetOrigin()).Distinct())
            {
                if (origin == null || origin.workshopFileId == 0ul)
                    continue;

                SteamContent content = Provider.provider.workshopService.ugc.Find(x => x.publishedFileID.m_PublishedFileId == origin.workshopFileId);
                if (content == null)
                    continue;

                dir = content.type == ESteamUGCType.MAP ? Path.Combine(content.path, "Bundles") : content.path;
                if (Directory.Exists(dir))
                {
                    DiscoverAssetIconPresetProvidersIn(dir, true);
                    foreach (string directory in Directory.EnumerateDirectories(dir, "*", SearchOption.AllDirectories))
                        DiscoverAssetIconPresetProvidersIn(directory, true);
                }
            }
        }
        else
        {
            if (Provider.provider?.workshopService?.ugc != null)
            {
                foreach (SteamContent content in Provider.provider.workshopService.ugc)
                {
                    dir = content.type == ESteamUGCType.MAP ? Path.Combine(content.path, "Bundles") : content.path;
                    if (!Directory.Exists(dir) || !LocalWorkshopSettings.get().getEnabled(content.publishedFileID))
                        continue;
                    DiscoverAssetIconPresetProvidersIn(dir, true);
                    foreach (string directory in Directory.EnumerateDirectories(dir, "*", SearchOption.AllDirectories))
                        DiscoverAssetIconPresetProvidersIn(directory, true);
                }
            }
        }

        int workshop = _presetProviders.Count;

        if (Level.info != null && Level.info.path != null)
        {
            dir = Path.Combine(Level.info.path, "Bundles");
            if (Directory.Exists(dir))
            {
                DiscoverAssetIconPresetProvidersIn(dir, !(Level.isEditor && !DevkitServerModule.IsEditing));
                foreach (string directory in Directory.EnumerateDirectories(Path.Combine(Level.info.path, "Bundles"), "*", SearchOption.AllDirectories))
                    DiscoverAssetIconPresetProvidersIn(directory, !(Level.isEditor && !DevkitServerModule.IsEditing));
            }
        }

        dir = Path.Combine(ReadWrite.PATH, "Sandbox");
        if (Directory.Exists(dir))
        {
            DiscoverAssetIconPresetProvidersIn(dir, false);
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
            DiscoverAssetIconPresetProvidersIn(dir2, !plugin.DeveloperMode);
            foreach (string directory in Directory.EnumerateDirectories(dir2, "*", SearchOption.AllDirectories))
                DiscoverAssetIconPresetProvidersIn(directory, !plugin.DeveloperMode);
        }

        if (ModuleHook.modules != null)
        {
            foreach (Module module in ModuleHook.modules)
            {
                if (!module.isEnabled || module == DevkitServerModule.Module)
                    continue;

                DiscoverAssetIconPresetProvidersIn(module.config.DirectoryPath, true);
                foreach (string directory in Directory.EnumerateDirectories(module.config.DirectoryPath, "*", SearchOption.AllDirectories))
                    DiscoverAssetIconPresetProvidersIn(directory, true);
            }
        }

        if (!_presetProviders.Exists(x => x.File.Equals(CustomPresetsPath, StringComparison.Ordinal)))
        {
            string path = Path.GetFullPath(CustomPresetsPath);
            _presetProviders.Add(_customPresets = new JsonConfigurationFile<List<AssetIconPreset>>(path) { Defaultable = true });
            Logger.DevkitServer.LogDebug(ObjectIconGenerator.Source, $"+ Registered working icon provider: {path.Format(false)}.");
        }

        PresetsIntl.Clear();

        ApplyDefaultProviders();

        int ct = DefaultPresets.Count;

        foreach (AssetIconPreset preset in DefaultPresets)
        {
            preset.File = null;
            PresetsIntl[preset.Asset.GUID] = preset;
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
                    if (i >= workshop)
                        Logger.DevkitServer.LogWarning(ObjectIconGenerator.Source, $"Object not found for icon preset: {preset.Asset.GUID.Format()} in {configFile.File.Format()}.");
                    else
                        Logger.DevkitServer.LogDebug(ObjectIconGenerator.Source, $"Object not found for workshop icon preset: {preset.Asset.GUID.Format()} in {configFile.File.Format()}.");
                    continue;
                }
                preset.File = configFile.File;
                if (!PresetsIntl.TryGetValue(preset.Asset.GUID, out AssetIconPreset existing) || existing.Priority < preset.Priority)
                    PresetsIntl[preset.Asset.GUID] = preset;
            }
        }

        Logger.DevkitServer.LogInfo(ObjectIconGenerator.Source, $"Registered {PresetsIntl.Count.Format()} unique icon presets from {ct.Format()} presets.");

        GC.Collect();
    }
    private static void OnConfigReloaded()
    {
        ObjectIconGenerator.ClearCache();
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
                Logger.DevkitServer.LogDebug(ObjectIconGenerator.Source, $"+ Registered icon provider {file.Format()}.");
                _presetProviders.Add(new JsonConfigurationFile<List<AssetIconPreset>>(Path.GetFullPath(file)) { ReadOnlyReloading = isReadonly });
            }
        }
    }
    private static void ApplyDefaultProviders()
    {
        IEnumerable<Type> types = !DevkitServerConfig.Config.DisableDefaultLevelObjectIconProviderSearch
            ? Accessor.GetTypesSafe(ModuleHook.modules.Where(x => x.assemblies != null).SelectMany(x => x.assemblies).Concat(PluginLoader.Assemblies.Select(x => x.Assembly)), true)
            : Accessor.GetTypesSafe(true);

        List <IDefaultIconProvider> providers = new List<IDefaultIconProvider>(2);
        foreach (Type type in types.Where(x => x is { IsInterface: false, IsAbstract: false }))
        {
            try
            {
                if (!typeof(IDefaultIconProvider).IsAssignableFrom(type))
                    continue;
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogInfo(ObjectIconGenerator.Source, $"Unable to check type for {typeof(IDefaultIconProvider).Format()} - {ex.GetType().Format()} {ex.Message.Format(true)}");
                continue;
            }

            try
            {
                IDefaultIconProvider provider = (IDefaultIconProvider)Activator.CreateInstance(type);
                providers.Add(provider);
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogError(ObjectIconGenerator.Source, ex, $"Unable to apply icon provider: {type.Format()}.");
            }
        }

        if (DevkitServerModule.AssemblyResolver.TriedToLoadLevelObjectIcons)
        {
            Compat.LevelObjectIconsCompat.AddIconProviders(providers);
        }

        providers.Sort((a, b) => b.Priority.CompareTo(a.Priority));

#if DEBUG
        foreach (IDefaultIconProvider provider in providers)
        {
            Logger.DevkitServer.LogDebug(ObjectIconGenerator.Source, $"+ Registered default icon provider: {provider.GetType().Format()} (Priority: {provider.Priority.Format()}).");
        }
#endif

        List<ObjectAsset> objects = new List<ObjectAsset>(4096);
        Assets.find(objects);

        foreach (ObjectAsset obj in objects)
        {
            if (DefaultPresets.Exists(x => x.Asset.GUID == obj.GUID))
                continue;

            IDefaultIconProvider? provider = providers.Find(x => x.AppliesTo(obj));

            if (provider == null)
                continue;

            provider.GetMetrics(obj, out Vector3 position, out Quaternion rotation);

            DefaultPresets.Add(new AssetIconPreset
            {
                Asset = new AssetReference<ObjectAsset>(obj.GUID),
                Priority = int.MinValue,
                IconPosition = position,
                IconRotation = rotation
            });
        }
    }

}
#endif