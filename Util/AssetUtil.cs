using System.Reflection;
using System.Reflection.Emit;

namespace DevkitServer.Util;
[EarlyTypeInit]
public static class AssetUtil
{
    private const string Source = "ASSET UTIL";

    private static readonly DatParser Parser = new DatParser();
    private static readonly Action<string, AssetOrigin>? LoadFile;
    private static readonly Action<AssetOrigin>? SyncAssetsFromOriginMethod;
    private static readonly Action<MasterBundleConfig, byte[]>? SetHash;
    private static readonly Action<MasterBundleConfig, AssetBundle>? SetAssetBundle;
    private static readonly Action<MasterBundleConfig>? CheckOwnerCustomDataAndMaybeUnload;
    private static readonly StaticGetter<List<MasterBundleConfig>>? AllMasterBundlesGetter;

    private static readonly InstanceGetter<Asset, AssetOrigin>? GetAssetOrigin = Accessor.GenerateInstanceGetter<Asset, AssetOrigin>("origin");
    
    /// <returns>The origin of the asset, or <see langword="null"/> in the case of a reflection failure.</returns>
    [Pure]
    public static AssetOrigin? GetOrigin(this Asset asset)
    {
        return asset == null ? null : GetAssetOrigin?.Invoke(asset);
    }

    /// <remarks><see cref="ItemPantsAsset"/> and <see cref="ItemShirtAsset"/> takes a <see cref="Texture2D"/> instead of a <see cref="GameObject"/> so they are not included in this method.</remarks>
    [Pure]
    public static GameObject? GetItemInstance(ItemAsset asset)
    {
        return asset switch
        {
            ItemBackpackAsset a => a.backpack,
            ItemBarrelAsset a => a.barrel,
            ItemBarricadeAsset a => a.barricade,
            ItemGlassesAsset a => a.glasses,
            ItemGripAsset a => a.grip,
            ItemHatAsset a => a.hat,
            ItemMagazineAsset a => a.magazine,
            ItemMaskAsset a => a.mask,
            ItemSightAsset a => a.sight,
            ItemStructureAsset a => a.structure,
            ItemTacticalAsset a => a.tactical,
            ItemThrowableAsset a => a.throwable,
            ItemVestAsset a => a.vest,
            _ => null
        };
    }
    /// <summary>
    /// Returns the asset category (<see cref="EAssetType"/>) of <typeparamref name="TAsset"/>. Effeciently cached.
    /// </summary>
    [Pure]
    public static EAssetType GetAssetCategory<TAsset>() where TAsset : Asset => GetAssetCategoryCache<TAsset>.Category;

    /// <summary>
    /// Returns a read-only list of all loaded master bundles, or empty in the case of a reflection failure.
    /// </summary>
    [Pure]
    public static IReadOnlyList<MasterBundleConfig> GetAllMasterBundles() => AllMasterBundlesGetter == null ? Array.Empty<MasterBundleConfig>() : AllMasterBundlesGetter().AsReadOnly();

    /// <summary>
    /// Loads one file (<paramref name="path"/>) synchronously, doesn't add it to current mapping. Call <see cref="SyncAssetsFromOrigin"/> to do this after you're done with the <paramref name="origin"/>.
    /// </summary>
    public static void LoadFileSync(string path, AssetOrigin origin)
    {
        ThreadUtil.assertIsGameThread();

        path = Path.GetFullPath(path);
        if (!Assets.shouldLoadAnyAssets)
            return;
        if (LoadFile == null)
        {
            Logger.LogWarning($"Unable to load file from origin {origin.name.Format(false)}: {path.Format()}.", method: Source);
            return;
        }
        try
        {
            LoadFile(path, origin);
            Logger.LogDebug($"[{Source}] Loaded asset: {origin.GetAssets().FirstOrDefault(x => Path.GetFullPath(x.getFilePath()).Equals(path, StringComparison.Ordinal)).Format()}");
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error loading asset from origin {origin.name.Format(false)}: {path.Format()}.", method: Source);
            Logger.LogError(ex, method: Source);
        }
    }

    /// <summary>
    /// Loads one master bundle (<paramref name="masterBundleDatFilePath"/>) synchronously.
    /// </summary>
    public static void LoadMasterBundleSync(string masterBundleDatFilePath, AssetOrigin origin)
    {
        if (!Assets.shouldLoadAnyAssets)
            return;
        DatDictionary dict = ReadFileWithoutHash(masterBundleDatFilePath);
        MasterBundleConfig config = new MasterBundleConfig(Path.GetDirectoryName(masterBundleDatFilePath), dict, origin);
        byte[] data;
        byte[] hash;
        using (FileStream fs = new FileStream(config.getAssetBundlePath(), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            using SHA1Stream hashStream = new SHA1Stream(fs);
            using MemoryStream memory = new MemoryStream();
            hashStream.CopyTo(memory);
            data = memory.ToArray();
            hash = hashStream.Hash;
        }

        SetHash?.Invoke(config, hash);
        SetAssetBundle?.Invoke(config, AssetBundle.LoadFromMemory(data));
        CheckOwnerCustomDataAndMaybeUnload?.Invoke(config);

        AllMasterBundlesGetter?.Invoke().Add(config);

        if (config.assetBundle != null)
            Logger.LogInfo($"Loaded master bundle: {config.assetBundleName.Format()} from {masterBundleDatFilePath.Format()}.");
    }

    /// <summary>
    /// Loads all asset files from <paramref name="directory"/>.
    /// </summary>
    /// <remarks>Do not reuse <paramref name="origin"/>.</remarks>
    public static void LoadAssetsSync(string directory, AssetOrigin origin, bool includeSubDirectories = true, bool loadMasterBundles = true)
    {
        if (!Assets.shouldLoadAnyAssets)
            return;
        LoadAssetsSyncIntl(directory, origin, includeSubDirectories, loadMasterBundles, true);
    }

    /// <summary>
    /// Adds all the assets loaded from <paramref name="origin"/> to the current asset mapping.
    /// </summary>
    public static void SyncAssetsFromOrigin(AssetOrigin origin)
    {
        if (SyncAssetsFromOriginMethod == null)
        {
            Logger.LogWarning($"Unable to sync assets from origin: {origin.name.Format()}.", method: Source);
            return;
        }
        SyncAssetsFromOriginMethod(origin);
    }

    /// <summary>
    /// Reads a <see cref="DatDictionary"/> from <paramref name="path"/> without hashing it.
    /// </summary>
    [Pure]
    public static DatDictionary ReadFileWithoutHash(string path)
    {
        ThreadUtil.assertIsGameThread();

        using FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using StreamReader inputReader = new StreamReader(fileStream);
        return Parser.Parse(inputReader);
    }

    private static void LoadAssetsSyncIntl(string directory, AssetOrigin origin, bool includeSubDirectories, bool loadMasterBundles, bool apply)
    {
        string dirName = Path.GetFileName(directory);
        string assetFile = Path.Combine(directory, dirName + ".asset");
        if (File.Exists(assetFile))
        {
            LoadFileSync(assetFile, origin);
            goto apply;
        }
        assetFile = Path.Combine(directory, dirName + ".dat");
        if (File.Exists(assetFile))
        {
            LoadFileSync(assetFile, origin);
            goto apply;
        }
        assetFile = Path.Combine(directory, "Asset.dat");
        if (File.Exists(assetFile))
        {
            LoadFileSync(assetFile, origin);
            goto apply;
        }

        foreach (string assetFileName in Directory.EnumerateFiles(directory, "*.asset", SearchOption.TopDirectoryOnly))
            LoadFileSync(assetFileName, origin);

        assetFile = Path.Combine(directory, "MasterBundle.dat");
        if (loadMasterBundles && File.Exists(assetFile))
        {
            LoadMasterBundleSync(assetFile, origin);
        }

        apply:
        if (includeSubDirectories)
        {
            foreach (string dir in Directory.EnumerateDirectories(directory, "*", includeSubDirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
            {
                LoadAssetsSyncIntl(dir, origin, false, loadMasterBundles, false);
            }
        }

        if (apply)
            SyncAssetsFromOrigin(origin);
    }
    private static void GetData(string filePath, out DatDictionary assetData, out string? assetError, out byte[] hash, out DatDictionary? translationData, out DatDictionary? fallbackTranslationData)
    {
        ThreadUtil.assertIsGameThread();

        string directoryName = Path.GetDirectoryName(filePath)!;
        using FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using SHA1Stream sha1Fs = new SHA1Stream(fs);
        using StreamReader input = new StreamReader(sha1Fs);

        assetData = Parser.Parse(input);
        assetError = Parser.ErrorMessage;
        hash = sha1Fs.Hash;
        string localLang = Path.Combine(directoryName, Provider.language + ".dat");
        string englishLang = Path.Combine(directoryName, "English.dat");
        translationData = null;
        fallbackTranslationData = null;
        if (File.Exists(localLang))
        {
            translationData = ReadFileWithoutHash(localLang);
            if (!Provider.language.Equals("English", StringComparison.Ordinal) && File.Exists(englishLang))
                fallbackTranslationData = ReadFileWithoutHash(englishLang);
        }
        else if (File.Exists(englishLang))
            translationData = ReadFileWithoutHash(englishLang);
    }
#if CLIENT
    public static void RefreshLevelsUI()
    {
        Level.broadcastLevelsRefreshed();
    }
#endif
    static AssetUtil()
    {
        try
        {
            // yes it's this hard to synchronously load an asset file

            SyncAssetsFromOriginMethod = (Action<AssetOrigin>)typeof(Assets)
                .GetMethod("AddAssetsFromOriginToCurrentMapping", BindingFlags.Static | BindingFlags.NonPublic)?
                .CreateDelegate(typeof(Action<AssetOrigin>))!;
            if (SyncAssetsFromOriginMethod == null)
            {
                Logger.LogError("Method not found: Assets.AddAssetsFromOriginToCurrentMapping.", method: Source);
                return;
            }

            MethodInfo method = typeof(AssetUtil).GetMethod(nameof(GetData), BindingFlags.Static | BindingFlags.NonPublic)!;
            Type? assetInfo = Accessor.AssemblyCSharp.GetType("SDG.Unturned.AssetsWorker+AssetDefinition", false, false);
            if (assetInfo == null)
            {
                Logger.LogError("Type not found: AssetsWorker.AssetDefinition.", method: Source);
                return;
            }

            MethodInfo? loadFileMethod = typeof(Assets).GetMethod("LoadFile", BindingFlags.NonPublic | BindingFlags.Static);
            if (loadFileMethod == null)
            {
                Logger.LogError("Method not found: Assets.LoadFile.", method: Source);
                return;
            }

            MethodInfo? setHash = typeof(MasterBundleConfig).GetProperty(nameof(MasterBundleConfig.hash), BindingFlags.Public | BindingFlags.Instance)?.GetSetMethod(true);
            if (setHash == null)
            {
                Logger.LogError("Method not found: set MasterBundleConfig.hash.", method: Source);
                return;
            }

            MethodInfo? setAssetBundle = typeof(MasterBundleConfig).GetProperty(nameof(MasterBundleConfig.assetBundle), BindingFlags.Public | BindingFlags.Instance)?.GetSetMethod(true);
            if (setAssetBundle == null)
            {
                Logger.LogError("Method not found: set MasterBundleConfig.assetBundle.", method: Source);
                return;
            }

            MethodInfo? checkOwnerCustomDataAndMaybeUnload = typeof(MasterBundleConfig).GetMethod("CheckOwnerCustomDataAndMaybeUnload", BindingFlags.NonPublic | BindingFlags.Instance);
            if (checkOwnerCustomDataAndMaybeUnload == null)
            {
                Logger.LogError("Method not found: MasterBundleConfig.CheckOwnerCustomDataAndMaybeUnload.", method: Source);
                return;
            }

            FieldInfo? pathField = assetInfo.GetField("path", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            FieldInfo? hashField = assetInfo.GetField("hash", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            FieldInfo? assetDataField = assetInfo.GetField("assetData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            FieldInfo? translationDataField = assetInfo.GetField("translationData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            FieldInfo? fallbackTranslationDataField = assetInfo.GetField("fallbackTranslationData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            FieldInfo? assetErrorField = assetInfo.GetField("assetError", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            FieldInfo? originField = assetInfo.GetField("origin", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (pathField == null || hashField == null || assetDataField == null || translationDataField == null || fallbackTranslationDataField == null || assetErrorField == null || originField == null)
            {
                Logger.LogError("Missing field in AssetsWorker.AssetDefinition.", method: Source);
                return;
            }

            DynamicMethod dm = new DynamicMethod("LoadAsset", typeof(void), new Type[] { typeof(string), typeof(AssetOrigin) }, typeof(AssetUtil).Module, true);
            ILGenerator generator = dm.GetILGenerator();
            dm.DefineParameter(0, ParameterAttributes.None, "path");
            dm.DefineParameter(1, ParameterAttributes.None, "assetOrigin");
            generator.DeclareLocal(typeof(DatDictionary));

            generator.DeclareLocal(typeof(string));
            generator.DeclareLocal(typeof(byte[]));
            generator.DeclareLocal(typeof(DatDictionary));
            generator.DeclareLocal(typeof(DatDictionary));
            generator.DeclareLocal(assetInfo);

            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldloca_S, 0);
            generator.Emit(OpCodes.Ldloca_S, 1);
            generator.Emit(OpCodes.Ldloca_S, 2);
            generator.Emit(OpCodes.Ldloca_S, 3);
            generator.Emit(OpCodes.Ldloca_S, 4);
            generator.Emit(OpCodes.Call, method);

            generator.Emit(OpCodes.Ldloca_S, 5);
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Stfld, pathField);

            generator.Emit(OpCodes.Ldloca_S, 5);
            generator.Emit(OpCodes.Ldloc_2);
            generator.Emit(OpCodes.Stfld, hashField);

            generator.Emit(OpCodes.Ldloca_S, 5);
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Stfld, assetDataField);

            generator.Emit(OpCodes.Ldloca_S, 5);
            generator.Emit(OpCodes.Ldloc_3);
            generator.Emit(OpCodes.Stfld, translationDataField);

            generator.Emit(OpCodes.Ldloca_S, 5);
            generator.Emit(OpCodes.Ldloc_S, 4);
            generator.Emit(OpCodes.Stfld, fallbackTranslationDataField);

            generator.Emit(OpCodes.Ldloca_S, 5);
            generator.Emit(OpCodes.Ldloc_1);
            generator.Emit(OpCodes.Stfld, assetErrorField);

            generator.Emit(OpCodes.Ldloca_S, 5);
            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Stfld, originField);

            generator.Emit(OpCodes.Ldloc_S, 5);
            generator.Emit(OpCodes.Call, loadFileMethod);

            generator.Emit(OpCodes.Ret);

            LoadFile = (Action<string, AssetOrigin>)dm.CreateDelegate(typeof(Action<string, AssetOrigin>));


            if (setAssetBundle != null)
            {
                SetAssetBundle = (Action<MasterBundleConfig, AssetBundle>)setAssetBundle.CreateDelegate(typeof(Action<MasterBundleConfig, AssetBundle>));
            }
            if (setHash != null)
            {
                SetHash = (Action<MasterBundleConfig, byte[]>)setHash.CreateDelegate(typeof(Action<MasterBundleConfig, byte[]>));
            }
            if (checkOwnerCustomDataAndMaybeUnload != null)
            {
                CheckOwnerCustomDataAndMaybeUnload = (Action<MasterBundleConfig>)checkOwnerCustomDataAndMaybeUnload.CreateDelegate(typeof(Action<MasterBundleConfig>));
            }

            AllMasterBundlesGetter = Accessor.GenerateStaticGetter<Assets, List<MasterBundleConfig>>("allMasterBundles");
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Failed to initialize syncronous asset loading tools. Something probably changed on the games side. " +
                              "The level Bundles folder may not be loaded server-side, and some other minor issues may arise.", method: Source);
            Logger.LogError(ex, method: Source);
        }
    }
    private static class GetAssetCategoryCache<TAsset> where TAsset : Asset
    {
        public static readonly EAssetType Category;
        static GetAssetCategoryCache()
        {
            Type type = typeof(TAsset);
            if (typeof(ItemAsset).IsAssignableFrom(type))
            {
                Category = EAssetType.ITEM;
            }
            else if (typeof(VehicleAsset).IsAssignableFrom(type))
            {
                Category = EAssetType.VEHICLE;
            }
            else if (typeof(ObjectAsset).IsAssignableFrom(type))
            {
                Category = EAssetType.OBJECT;
            }
            else if (typeof(EffectAsset).IsAssignableFrom(type))
            {
                Category = EAssetType.EFFECT;
            }
            else if (typeof(AnimalAsset).IsAssignableFrom(type))
            {
                Category = EAssetType.ANIMAL;
            }
            else if (typeof(SpawnAsset).IsAssignableFrom(type))
            {
                Category = EAssetType.SPAWN;
            }
            else if (typeof(SkinAsset).IsAssignableFrom(type))
            {
                Category = EAssetType.SKIN;
            }
            else if (typeof(MythicAsset).IsAssignableFrom(type))
            {
                Category = EAssetType.MYTHIC;
            }
            else if (typeof(ResourceAsset).IsAssignableFrom(type))
            {
                Category = EAssetType.RESOURCE;
            }
            else if (typeof(DialogueAsset).IsAssignableFrom(type) || typeof(QuestAsset).IsAssignableFrom(type) || typeof(VendorAsset).IsAssignableFrom(type))
            {
                Category = EAssetType.NPC;
            }
            else
                Category = EAssetType.NONE;
        }
    }
}
