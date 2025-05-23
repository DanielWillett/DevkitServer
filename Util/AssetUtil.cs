using Cysharp.Threading.Tasks;
using DanielWillett.ReflectionTools;
using DanielWillett.ReflectionTools.Emit;
using DevkitServer.API;
using SDG.Framework.Modules;
using SDG.Framework.Utilities;
using System.Reflection;
using System.Reflection.Emit;
using Module = SDG.Framework.Modules.Module;

namespace DevkitServer.Util;

public delegate UniTask BeginLevelLoading(LevelInfo level, CancellationToken token);

[EarlyTypeInit]
public static class AssetUtil
{
    private const string Source = "ASSET UTIL";

    private static readonly DatParser Parser = new DatParser();
    private static readonly Action<string, AssetOrigin, List<string>>? LoadFile;
    private static readonly Action<AssetOrigin>? SyncAssetsFromOriginMethod;
    private static readonly Action<MasterBundleConfig, byte[]>? SetHash;
    private static readonly Action<MasterBundleConfig, AssetBundle>? SetAssetBundle;
    private static readonly Action<MasterBundleConfig>? CheckOwnerCustomDataAndMaybeUnload;
    private static readonly StaticGetter<List<MasterBundleConfig>>? AllMasterBundlesGetter;
    private static readonly StaticGetter<List<LevelInfo>>? KnownLevelsGetter = Accessor.GenerateStaticGetter<Level, List<LevelInfo>>("knownLevels");

    private static readonly Action? CallScanKnownLevels = Accessor.GenerateStaticCaller<Level, Action>("ScanKnownLevels");
    private static readonly InstanceGetter<Asset, AssetOrigin>? GetAssetOrigin = Accessor.GenerateInstanceGetter<Asset, AssetOrigin>("origin");
    private static readonly InstanceGetter<NPCRewardsList, INPCReward[]>? GetRewardsFromList = Accessor.GenerateInstanceGetter<NPCRewardsList, INPCReward[]>("rewards");
    private static readonly InstanceGetter<Module, List<IModuleNexus>>? GetNexii = Accessor.GenerateInstanceGetter<Module, List<IModuleNexus>>("nexii");
    private static readonly StaticGetter<Assets>? GetAssetsInstance = Accessor.GenerateStaticGetter<Assets, Assets>("instance");
    private static readonly InstanceSetter<AssetOrigin, bool>? SetOverrideIDs = Accessor.GenerateInstanceSetter<AssetOrigin, bool>("shouldAssetsOverrideExistingIds");
    private static readonly InstanceGetter<INPCCondition, List<int>>? GetUIRequirements = Accessor.GenerateInstanceGetter<INPCCondition, List<int>>("uiRequirementIndices");

    public static event BeginLevelLoading? OnBeginLevelLoading;

    /// <summary>
    /// Singleton instance of the <see cref="Assets"/> class, or <see langword="null"/> in the case of a reflection failure.
    /// </summary>
    public static Assets? AssetsInstance => GetAssetsInstance?.Invoke();

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
    /// Returns the asset category (<see cref="EAssetType"/>) of <typeparamref name="TAsset"/>. Efficiently cached.
    /// </summary>
    [Pure]
    public static EAssetType GetAssetCategory<TAsset>() where TAsset : Asset => GetAssetCategoryCache<TAsset>.Category;

    /// <summary>
    /// Returns the asset category (<see cref="EAssetType"/>) of <paramref name="assetType"/>.
    /// </summary>
    [Pure]
    public static EAssetType GetAssetCategory(Type assetType)
    {
        if (typeof(ItemAsset).IsAssignableFrom(assetType))
        {
            return EAssetType.ITEM;
        }
        if (typeof(VehicleAsset).IsAssignableFrom(assetType))
        {
            return EAssetType.VEHICLE;
        }
        if (typeof(ObjectAsset).IsAssignableFrom(assetType))
        {
            return EAssetType.OBJECT;
        }
        if (typeof(EffectAsset).IsAssignableFrom(assetType))
        {
            return EAssetType.EFFECT;
        }
        if (typeof(AnimalAsset).IsAssignableFrom(assetType))
        {
            return EAssetType.ANIMAL;
        }
        if (typeof(SpawnAsset).IsAssignableFrom(assetType))
        {
            return EAssetType.SPAWN;
        }
        if (typeof(SkinAsset).IsAssignableFrom(assetType))
        {
            return EAssetType.SKIN;
        }
        if (typeof(MythicAsset).IsAssignableFrom(assetType))
        {
            return EAssetType.MYTHIC;
        }
        if (typeof(ResourceAsset).IsAssignableFrom(assetType))
        {
            return EAssetType.RESOURCE;
        }
        if (typeof(DialogueAsset).IsAssignableFrom(assetType) || typeof(QuestAsset).IsAssignableFrom(assetType) || typeof(VendorAsset).IsAssignableFrom(assetType))
        {
            return EAssetType.NPC;
        }

        return EAssetType.NONE;
    }

    /// <summary>
    /// Returns a read-only list of all loaded master bundles, or empty in the case of a reflection failure.
    /// </summary>
    [Pure]
    public static IReadOnlyList<MasterBundleConfig> GetAllMasterBundles() => AllMasterBundlesGetter == null ? Array.Empty<MasterBundleConfig>() : AllMasterBundlesGetter().AsReadOnly();

    /// <summary>
    /// Loads one file (<paramref name="path"/>) synchronously, doesn't add it to current mapping. Call <see cref="SyncAssetsFromOrigin"/> to do this after you're done with the <paramref name="origin"/>.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void LoadFileSync(string path, AssetOrigin origin)
    {
        ThreadUtil.assertIsGameThread();

        path = Path.GetFullPath(path);
        if (!Assets.shouldLoadAnyAssets)
            return;
        if (LoadFile == null)
        {
            Logger.DevkitServer.LogWarning(nameof(LoadFileSync), $"Unable to load file from origin {origin.name.Format(false)}: {path.Format()}.");
            return;
        }
        List<string> errors = ListPool<string>.claim();
        try
        {
            LoadFile(path, origin, errors);

            if (errors.Count > 0)
            {
                foreach (string error in errors)
                {
                    Logger.DevkitServer.LogWarning(nameof(LoadFileSync), $"Error loading asset: {error.Format(true)}.");
                }
            }
            else
            {
                Logger.DevkitServer.LogDebug(nameof(LoadFileSync), $"Loaded asset: {origin.GetAssets().LastOrDefault(x => Path.GetFullPath(x.getFilePath()).Equals(path, StringComparison.Ordinal)).Format()}");
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogWarning(nameof(LoadFileSync), ex, $"Error loading asset from origin {origin.name.Format(false)}: {path.Format()}.");
        }
        finally
        {
            ListPool<string>.release(errors);
        }
    }

    /// <summary>
    /// Loads one master bundle (<paramref name="masterBundleDatFilePath"/>) synchronously.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void LoadMasterBundleSync(string masterBundleDatFilePath, AssetOrigin origin)
    {
        ThreadUtil.assertIsGameThread();

        if (!Assets.shouldLoadAnyAssets)
            return;
        IDatDictionary dict = ReadFileWithoutHash(masterBundleDatFilePath);
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
            Logger.DevkitServer.LogInfo(nameof(LoadMasterBundleSync), $"Loaded master bundle: {config.assetBundleName.Format()} from {masterBundleDatFilePath.Format()}.");
    }

    /// <summary>
    /// Loads all asset files from <paramref name="directory"/>.
    /// </summary>
    /// <remarks>Do not reuse <paramref name="origin"/>.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void LoadAssetsSync(string directory, AssetOrigin origin, bool includeSubDirectories = true, bool loadMasterBundles = true)
    {
        ThreadUtil.assertIsGameThread();

        if (!Assets.shouldLoadAnyAssets)
            return;
        LoadAssetsSyncIntl(directory, origin, includeSubDirectories, loadMasterBundles, true);
    }

    /// <summary>
    /// Adds all the assets loaded from <paramref name="origin"/> to the current asset mapping.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void SyncAssetsFromOrigin(AssetOrigin origin)
    {
        ThreadUtil.assertIsGameThread();

        if (SyncAssetsFromOriginMethod == null)
        {
            Logger.DevkitServer.LogWarning(nameof(SyncAssetsFromOrigin), $"Unable to sync assets from origin: {origin.name.Format()}.");
            return;
        }
        SyncAssetsFromOriginMethod(origin);
    }

    /// <summary>
    /// Reads a <see cref="DatDictionary"/> from <paramref name="path"/> without hashing it.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    [Pure]
    public static IDatDictionary ReadFileWithoutHash(string path)
    {
        ThreadUtil.assertIsGameThread();

        using FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using StreamReader inputReader = new StreamReader(fileStream);
        return Parser.Parse(inputReader);
    }


    /// <summary>
    /// Get internal reward array from an <see cref="NPCRewardsList"/>.
    /// </summary>
    /// <returns><see langword="null"/> in the case of a refelction error, otherwise the internal list of NPC rewards.</returns>
    [Pure]
    public static INPCReward[]? GetRewards(NPCRewardsList list)
    {
        return GetRewardsFromList?.Invoke(list);
    }

    /// <summary>
    /// Get internal UI requirement list from a <see cref="INPCCondition"/>.
    /// </summary>
    /// <returns><see langword="null"/> in the case of a refelction error, otherwise the internal UI requirement list of a condition.</returns>
    [Pure]
    public static IReadOnlyList<int>? GetConditionUIRequirements(INPCCondition condition)
    {
        return GetUIRequirements?.Invoke(condition);
    }

    /// <summary>
    /// Gets a list of <see cref="IModuleNexus"/> loaded by a <see cref="Module"/>.
    /// </summary>
    /// <returns><see langword="null"/> in the case of a refelction error, otherwise the list of nexii.</returns>
    [Pure]
    public static IReadOnlyList<IModuleNexus>? GetModuleNexii(Module module)
    {
        return GetNexii?.Invoke(module);
    }

    /// <summary>
    /// Creates a <see cref="AssetOrigin"/> with the provided properties. This has the capability to set the internal field, 'shouldAssetsOverrideExistingIds'.
    /// </summary>
    [Pure]
    public static AssetOrigin CreateAssetOrigin(string name, ulong workshopFileId, bool shouldAssetsOverrideExistingIds)
    {
        AssetOrigin origin = new AssetOrigin { name = name, workshopFileId = workshopFileId };

        if (shouldAssetsOverrideExistingIds)
        {
            if (SetOverrideIDs == null)
                Logger.DevkitServer.LogWarning(nameof(CreateAssetOrigin), $"Unable to set asset origin field, 'shouldAssetsOverrideExistingIds' to true: {origin.name.Format()}.");
            else
                SetOverrideIDs.Invoke(origin, shouldAssetsOverrideExistingIds);
        }

        return origin;
    }

    /// <summary>
    /// Tells the game to scan for any level changes; new levels or removed levels. If you need to re-read config files, use <see cref="RescanLevel"/> instead.
    /// </summary>
    /// <returns><see langword="false"/> in the case of a refelction error, otherwise <see langword="true"/>.</returns>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool ScanForKnownLevelChanges()
    {
        ThreadUtil.assertIsGameThread();

        if (CallScanKnownLevels == null)
            return false;

        CallScanKnownLevels();
        return true;
    }

    /// <summary>
    /// Causes the game to re-read (or remove if the level has been deleted) the <see cref="LevelInfo"/> of the level at path <paramref name="path"/> if it's been scanned.
    /// </summary>
    /// <returns><see langword="false"/> in the case of a refelction error, otherwise <see langword="true"/>, even if the level wasn't found (it'll be re-scanned anyway if it exists).</returns>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool RescanLevel(string path)
    {
        ThreadUtil.assertIsGameThread();

        if (KnownLevelsGetter == null)
            return false;

        List<LevelInfo> levels = KnownLevelsGetter();
        int index = levels.FindIndex(levelInfo => string.Equals(levelInfo.path, path, StringComparison.Ordinal));
            
        if (index >= 0)
            levels.RemoveAt(index);

        CallScanKnownLevels?.Invoke();
#if CLIENT
        Level.broadcastLevelsRefreshed();
#endif
        return true;
    }

    /// <summary>
    /// Causes the game to re-read (or remove if the levels have been deleted) all <see cref="LevelInfo"/> that have been scanned.
    /// </summary>
    /// <returns><see langword="false"/> in the case of a refelction error, otherwise <see langword="true"/>.</returns>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool RescanAllLevels()
    {
        ThreadUtil.assertIsGameThread();

        if (KnownLevelsGetter == null)
            return false;

        List<LevelInfo> levels = KnownLevelsGetter();
        levels.Clear();
        CallScanKnownLevels?.Invoke();
#if CLIENT
        Level.broadcastLevelsRefreshed();
#endif
        return true;
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


    private static void GetData(string filePath, out IDatDictionary assetData, List<string>? assetErrors, out byte[] hash, out IDatDictionary? translationData, out IDatDictionary? fallbackTranslationData)
    {
        ThreadUtil.assertIsGameThread();

        string directoryName = Path.GetDirectoryName(filePath)!;
        using FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using SHA1Stream sha1Fs = new SHA1Stream(fs);
        using StreamReader input = new StreamReader(sha1Fs);

        assetData = Parser.Parse(input);
        if (Parser.ErrorMessages is { Count: > 0 } && assetErrors != null)
        {
            assetErrors.AddRange(Parser.ErrorMessages);
        }

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
                Logger.DevkitServer.LogError(Source, "Method not found: Assets.AddAssetsFromOriginToCurrentMapping.");
                return;
            }

            Type? assetInfo = typeof(Assets).Assembly.GetType("SDG.Unturned.AssetsWorker+AssetDefinition", false, false);
            if (assetInfo == null)
            {
                Logger.DevkitServer.LogError(Source, "AssetsWorker.AssetDefinition not found.");
                return;
            }

            MethodInfo? loadFileMethod = typeof(Assets).GetMethod("LoadFile", BindingFlags.NonPublic | BindingFlags.Static);
            if (loadFileMethod == null)
            {
                Logger.DevkitServer.LogError(Source, "Assets.LoadFile not found.");
                return;
            }

            FieldInfo? pathField = assetInfo.GetField("path", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            FieldInfo? hashField = assetInfo.GetField("hash", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            FieldInfo? assetDataField = assetInfo.GetField("assetData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            FieldInfo? translationDataField = assetInfo.GetField("translationData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            FieldInfo? fallbackTranslationDataField = assetInfo.GetField("fallbackTranslationData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            FieldInfo? assetErrorsField = assetInfo.GetField("assetErrors", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            FieldInfo? originField = assetInfo.GetField("origin", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (pathField == null || hashField == null || assetDataField == null || translationDataField == null || fallbackTranslationDataField == null || assetErrorsField == null || originField == null)
            {
                Logger.DevkitServer.LogError(Source, "Missing field in AssetsWorker.AssetDefinition.");
                return;
            }

            DynamicMethodInfo<Action<string, AssetOrigin, List<string>>> dynMethod = DynamicMethodHelper.Create<Action<string, AssetOrigin, List<string>>>("LoadAsset", typeof(AssetUtil), initLocals: false);

#if DEBUG
            IOpCodeEmitter emit = dynMethod.GetEmitter(debuggable: true);
#else
        IOpCodeEmitter emit = dynMethod.GetEmitter(debuggable: false);
#endif
            dynMethod.DefineParameter(0, ParameterAttributes.None, "path");
            dynMethod.DefineParameter(1, ParameterAttributes.None, "assetOrigin");

            LocalBuilder lclData = emit.DeclareLocal(typeof(IDatDictionary));

            LocalBuilder lclHash = emit.DeclareLocal(typeof(byte[]));
            LocalBuilder lclTranslationData = emit.DeclareLocal(typeof(IDatDictionary));
            LocalBuilder lclFallbackTranslationData = emit.DeclareLocal(typeof(IDatDictionary));
            LocalBuilder lclAssetInfo = emit.DeclareLocal(assetInfo);

            emit.LoadArgument(0)
                .LoadLocalAddress(lclData)
                .LoadArgument(2)
                .LoadLocalAddress(lclHash)
                .LoadLocalAddress(lclTranslationData)
                .LoadLocalAddress(lclFallbackTranslationData)
                .Invoke(Accessor.GetMethod(GetData)!);

            emit.LoadLocalAddress(lclAssetInfo)
                .LoadArgument(0)
                .SetInstanceFieldValue(pathField);

            emit.LoadLocalAddress(lclAssetInfo)
                .LoadLocalValue(lclHash)
                .SetInstanceFieldValue(hashField);

            emit.LoadLocalAddress(lclAssetInfo)
                .LoadLocalValue(lclData)
                .SetInstanceFieldValue(assetDataField);

            emit.LoadLocalAddress(lclAssetInfo)
                .LoadLocalValue(lclTranslationData)
                .SetInstanceFieldValue(translationDataField);

            emit.LoadLocalAddress(lclAssetInfo)
                .LoadLocalValue(lclFallbackTranslationData)
                .SetInstanceFieldValue(fallbackTranslationDataField);

            emit.LoadLocalAddress(lclAssetInfo)
                .LoadArgument(2)
                .SetInstanceFieldValue(assetErrorsField);

            emit.LoadLocalAddress(lclAssetInfo)
                .LoadArgument(1)
                .SetInstanceFieldValue(originField);

            emit.LoadLocalValue(lclAssetInfo)
                .Invoke(loadFileMethod)
                .Return();

            LoadFile = dynMethod.CreateDelegate();

            MethodInfo? setHash = typeof(MasterBundleConfig).GetProperty(nameof(MasterBundleConfig.hash), BindingFlags.Public | BindingFlags.Instance)?.GetSetMethod(true);
            if (setHash == null)
            {
                Logger.DevkitServer.LogError(Source, "Method not found: set MasterBundleConfig.hash.");
            }
            else
            {
                SetHash = (Action<MasterBundleConfig, byte[]>)setHash.CreateDelegate(typeof(Action<MasterBundleConfig, byte[]>));
            }

            MethodInfo? setAssetBundle = typeof(MasterBundleConfig).GetProperty(nameof(MasterBundleConfig.assetBundle), BindingFlags.Public | BindingFlags.Instance)?.GetSetMethod(true);
            if (setAssetBundle == null)
            {
                Logger.DevkitServer.LogError(Source, "Method not found: set MasterBundleConfig.assetBundle.");
            }
            else
            {
                SetAssetBundle = (Action<MasterBundleConfig, AssetBundle>)setAssetBundle.CreateDelegate(typeof(Action<MasterBundleConfig, AssetBundle>));
            }

            MethodInfo? checkOwnerCustomDataAndMaybeUnload = typeof(MasterBundleConfig).GetMethod("CheckOwnerCustomDataAndMaybeUnload", BindingFlags.NonPublic | BindingFlags.Instance);
            if (checkOwnerCustomDataAndMaybeUnload == null)
            {
                Logger.DevkitServer.LogError(Source, "Method not found: MasterBundleConfig.CheckOwnerCustomDataAndMaybeUnload.");
            }
            else
            {
                CheckOwnerCustomDataAndMaybeUnload = (Action<MasterBundleConfig>)checkOwnerCustomDataAndMaybeUnload.CreateDelegate(typeof(Action<MasterBundleConfig>));
            }

            AllMasterBundlesGetter = Accessor.GenerateStaticGetter<Assets, List<MasterBundleConfig>>("allMasterBundles");
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogWarning(Source, ex, "Failed to initialize syncronous asset loading tools. Something probably changed on the games side. " +
                                           "The level Bundles folder may not be loaded server-side, and some other minor issues may arise.");
        }
    }
    private static class GetAssetCategoryCache<TAsset> where TAsset : Asset
    {
        public static readonly EAssetType Category = GetAssetCategory(typeof(TAsset));
    }

    internal static async UniTask InvokeOnBeginLevelLoading(CancellationToken token)
    {
        if (OnBeginLevelLoading == null)
            return;
        LoadingUI.SetLoadingText("DevkitServer Initialization");
        LoadingUI.NotifyLevelLoadingProgress(0.00001f);
        LevelInfo info = Level.info;
        foreach (BeginLevelLoading dele in OnBeginLevelLoading.GetInvocationList().Cast<BeginLevelLoading>())
        {
            try
            {
                await dele(info, token);
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogError(dele.Method.Module.Name, ex,
                    $"Caller in {dele.Method.Format()} threw an error in {typeof(AssetUtil).Format()}.{nameof(OnBeginLevelLoading).Colorize(ConsoleColor.White)}.");
            }
        }
        LoadingUI.NotifyLevelLoadingProgress(1f / 30f);
        LoadingUI.updateScene();
    }
}
