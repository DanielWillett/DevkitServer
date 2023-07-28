using DevkitServer.API.Permissions;
using DevkitServer.Core.Permissions;
using DevkitServer.Models;
using DevkitServer.Multiplayer;
using DevkitServer.Multiplayer.Actions;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Multiplayer.Levels;
using DevkitServer.Multiplayer.Sync;
using DevkitServer.Players.UI;
using DevkitServer.Util.Region;
#if CLIENT
using DevkitServer.Patches;
using DevkitServer.Players;
using System.Reflection;
#endif
#if SERVER
using DevkitServer.Players;
#endif

namespace DevkitServer.Util;
public static class LevelObjectUtil
{
    private const string Source = "LEVEL OBJECTS";

    public static readonly Quaternion DefaultObjectRotation = Quaternion.Euler(-90f, 0.0f, 0.0f);

    public const int MaxDeletePacketSize = 64;
    public const int MaxMovePacketSize = 32;
    public const int MaxMovePreviewSelectionSize = 8;
    public const int MaxCopySelectionSize = 64;
    public const int MaxUpdateObjectsPacketSize = 64;

    internal static CachedMulticastEvent<BuildableRegionUpdated> EventOnBuildableRegionUpdated = new CachedMulticastEvent<BuildableRegionUpdated>(typeof(LevelObjectUtil), nameof(OnBuildableRegionUpdated));
    internal static CachedMulticastEvent<LevelObjectRegionUpdated> EventOnLevelObjectRegionUpdated = new CachedMulticastEvent<LevelObjectRegionUpdated>(typeof(LevelObjectUtil), nameof(OnLevelObjectRegionUpdated));
    
    internal static CachedMulticastEvent<BuildableMoved> EventOnBuildableMoved = new CachedMulticastEvent<BuildableMoved>(typeof(LevelObjectUtil), nameof(OnBuildableMoved));
    internal static CachedMulticastEvent<LevelObjectMoved> EventOnLevelObjectMoved = new CachedMulticastEvent<LevelObjectMoved>(typeof(LevelObjectUtil), nameof(OnLevelObjectMoved));
    
    internal static CachedMulticastEvent<BuildableRemoved> EventOnBuildableRemoved = new CachedMulticastEvent<BuildableRemoved>(typeof(LevelObjectUtil), nameof(OnBuildableRemoved));
    internal static CachedMulticastEvent<LevelObjectRemoved> EventOnLevelObjectRemoved = new CachedMulticastEvent<LevelObjectRemoved>(typeof(LevelObjectUtil), nameof(OnLevelObjectRemoved));

    public static event BuildableRegionUpdated OnBuildableRegionUpdated
    {
        add => EventOnBuildableRegionUpdated.Add(value);
        remove => EventOnBuildableRegionUpdated.Remove(value);
    }
    public static event LevelObjectRegionUpdated OnLevelObjectRegionUpdated
    {
        add => EventOnLevelObjectRegionUpdated.Add(value);
        remove => EventOnLevelObjectRegionUpdated.Remove(value);
    }
    public static event BuildableMoved OnBuildableMoved
    {
        add => EventOnBuildableMoved.Add(value);
        remove => EventOnBuildableMoved.Remove(value);
    }
    public static event LevelObjectMoved OnLevelObjectMoved
    {
        add => EventOnLevelObjectMoved.Add(value);
        remove => EventOnLevelObjectMoved.Remove(value);
    }
    public static event BuildableRemoved OnBuildableRemoved
    {
        add => EventOnBuildableRemoved.Add(value);
        remove => EventOnBuildableRemoved.Remove(value);
    }
    public static event LevelObjectRemoved OnLevelObjectRemoved
    {
        add => EventOnLevelObjectRemoved.Add(value);
        remove => EventOnLevelObjectRemoved.Remove(value);
    }

#if CLIENT
    private static readonly Action? CallClearSelection = Accessor.GenerateStaticCaller<EditorObjects, Action>("clearSelection");
#endif

    [UsedImplicitly]
    private static readonly NetCall<Guid, Vector3, Quaternion, Vector3> SendRequestInstantiation = new NetCall<Guid, Vector3, Quaternion, Vector3>(NetCalls.RequestLevelObjectInstantiation);

    [UsedImplicitly]
    private static readonly NetCall<Guid, Vector3, Quaternion, Vector3, ulong, NetId> SendLevelObjectInstantiation = new NetCall<Guid, Vector3, Quaternion, Vector3, ulong, NetId>(NetCalls.SendLevelObjectInstantiation);


#if CLIENT
    private static readonly Action<CullingVolume>? ClearCullingVolumeObjects =
        Accessor.GenerateInstanceCaller<CullingVolume, Action<CullingVolume>>("ClearObjects", Array.Empty<Type>());

    private static readonly Action<CullingVolume>? FindCullingVolumeObjects =
        Accessor.GenerateInstanceCaller<CullingVolume, Action<CullingVolume>>("FindObjectsInsideVolume", Array.Empty<Type>());
#endif

    private static readonly InstanceGetter<LevelObject, AssetReference<MaterialPaletteAsset>>? GetCustomMaterialOverrideIntl =
        Accessor.GenerateInstanceGetter<LevelObject, AssetReference<MaterialPaletteAsset>>("customMaterialOverride");

    private static readonly InstanceGetter<LevelObject, int>? GetMaterialIndexOverrideIntl =
        Accessor.GenerateInstanceGetter<LevelObject, int>("materialIndexOverride");

    private static readonly InstanceSetter<LevelObject, AssetReference<MaterialPaletteAsset>>? SetCustomMaterialOverrideIntl =
        Accessor.GenerateInstanceSetter<LevelObject, AssetReference<MaterialPaletteAsset>>("customMaterialOverride");

    private static readonly InstanceSetter<LevelObject, int>? SetMaterialIndexOverrideIntl =
        Accessor.GenerateInstanceSetter<LevelObject, int>("materialIndexOverride");

    private static readonly Action<LevelObject>? CallReapplyMaterialOverridesIntl =
        Accessor.GenerateInstanceCaller<LevelObject, Action<LevelObject>>("ReapplyMaterialOverrides");


#if CLIENT
    private static List<EditorSelection>? _selections;
    private static TransformHandles? _handles;
    private static List<EditorCopy>? _copies;
    public static List<EditorCopy> EditorObjectCopies => _copies ??=
        (List<EditorCopy>?)typeof(EditorObjects).GetField("copies", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null)
        ?? throw new MemberAccessException("Unable to find field: EditorObjects.copies.");
    public static List<EditorSelection> EditorObjectSelection => _selections ??=
        (List<EditorSelection>?)typeof(EditorObjects).GetField("selection", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null)
        ?? throw new MemberAccessException("Unable to find field: EditorObjects.selection.");
    public static TransformHandles EditorObjectHandles => _handles ??=
        (TransformHandles?)typeof(EditorObjects).GetField("handles", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null)
        ?? throw new MemberAccessException("Unable to find field: EditorObjects.handles.");
    public static void RequestInstantiation(Guid asset, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        SendRequestInstantiation.Invoke(asset, position, rotation, scale);
    }

    [NetCall(NetCallSource.FromServer, NetCalls.SendLevelObjectInstantiation)]
    internal static StandardErrorCode ReceiveInstantiation(MessageContext ctx, Guid asset, Vector3 position, Quaternion rotation, Vector3 scale, ulong owner, NetId netId)
    {
        if (Assets.find(asset) is not ObjectAsset objAsset)
        {
            if (Assets.find(asset) is not ItemAsset itemAsset || itemAsset is not ItemBarricadeAsset and not ItemStructureAsset)
            {
                Logger.LogError($"Asset not found for incoming object: {asset.Format()}.", method: Source);
                return StandardErrorCode.InvalidData;
            }
            if (!EditorActions.HasProcessedPendingLevelObjects)
            {
                EditorActions.TemporaryEditorActions?.QueueInstantiation(itemAsset, position, rotation, scale, owner, netId);
                return StandardErrorCode.Success;
            }

            SyncIfAuthority(netId);

            try
            {
                Transform? newBuildable = LevelObjects.addBuildable(position, rotation, itemAsset.id);
                if (newBuildable == null)
                    return StandardErrorCode.GenericError;

                InitializeBuildable(newBuildable, out RegionIdentifier id, netId);

                if (owner == Provider.client.m_SteamID)
                    BuildableResponsibilities.Set(id, true, false);
                SyncIfAuthority(id);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to initialize buildable: {asset.Format()}.", method: Source);
                Logger.LogError(ex, method: Source);
                return StandardErrorCode.GenericError;
            }

            return StandardErrorCode.Success;
        }
        if (!EditorActions.HasProcessedPendingLevelObjects)
        {
            EditorActions.TemporaryEditorActions?.QueueInstantiation(objAsset, position, rotation, scale, owner, netId);
            return StandardErrorCode.Success;
        }

        try
        {
            Transform newObject = LevelObjects.registerAddObject(position, rotation, scale, objAsset, null);
            if (newObject == null)
                return StandardErrorCode.GenericError;

            InitializeLevelObject(newObject, out LevelObject? lvlObject, netId);
            if (lvlObject == null)
                return StandardErrorCode.GenericError;

            if (owner == Provider.client.m_SteamID)
                LevelObjectResponsibilities.Set(lvlObject.instanceID);

            SyncIfAuthority(lvlObject);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to initialize object: {asset.Format()}.", method: Source);
            Logger.LogError(ex, method: Source);
            return StandardErrorCode.GenericError;
        }

        return StandardErrorCode.Success;
    }
    public static void ClearSelection()
    {
        ThreadUtil.assertIsGameThread();

        if (CallClearSelection != null)
        {
            CallClearSelection.Invoke();
            return;
        }

        List<EditorSelection> selection = EditorObjectSelection;
        for (int i = selection.Count - 1; i >= 0; ++i)
            EditorObjects.removeSelection(selection[i].transform);
    }
#elif SERVER
    [NetCall(NetCallSource.FromClient, NetCalls.RequestLevelObjectInstantiation)]
    public static void ReceiveLevelObjectInstantiation(MessageContext ctx, Guid guid, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        EditorUser? user = ctx.GetCaller();
        if (user == null || !user.IsOnline)
        {
            Logger.LogError("Unable to get user from level object instantiation request.", method: Source);
            return;
        }

        if (!GetObjectOrBuildableAsset(guid, out ObjectAsset? objectAsset, out ItemAsset? buildableAsset))
        {
            Logger.LogError($"Unable to get asset for level object instantiation request from {user.Format()}.", method: Source);
            UIMessage.SendEditorMessage(user, DevkitServerModule.MessageLocalization.Translate("Error", guid.ToString("N") + " Unknown Asset"));
            return;
        }
        Asset asset = objectAsset ?? (Asset)buildableAsset!;

        LevelObject? levelObject = null;
        Transform transform;
        RegionIdentifier id;
        NetId netId;
        try
        {
            transform = LevelObjects.registerAddObject(position, rotation, scale, objectAsset, buildableAsset);
            if (transform == null)
            {
                Logger.LogError($"Failed to create object: {(objectAsset ?? (Asset?)buildableAsset).Format()}, registerAddObject returned {((object?)null).Format()}.");
                return;
            }
            
            if (objectAsset != null)
            {
                id = default;
                InitializeLevelObject(transform, out levelObject, out netId);
            }
            else
            {
                InitializeBuildable(transform, out id, out netId);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error instantiating {asset.Format()}.", method: Source);
            Logger.LogError(ex, method: Source);
            UIMessage.SendEditorMessage(user, DevkitServerModule.MessageLocalization.Translate("Error", ex.Message));
            return;
        }
        
        position = transform.position;
        rotation = transform.rotation;
        scale = transform.localScale;

        PooledTransportConnectionList list;
        if (!ctx.IsRequest)
            list = DevkitServerUtility.GetAllConnections();
        else
        {
            ctx.ReplyLayered(SendLevelObjectInstantiation, asset.GUID, position, rotation, scale, user.SteamId.m_SteamID, netId);
            list = DevkitServerUtility.GetAllConnections(ctx.Connection);
        }

        SendLevelObjectInstantiation.Invoke(list, asset.GUID, position, rotation, scale, user.SteamId.m_SteamID, netId);

        if (levelObject != null)
        {
            LevelObjectResponsibilities.Set(levelObject.instanceID, user.SteamId.m_SteamID);
            Logger.LogDebug($"[{Source}] Granted request for instantiation of object {asset.Format()}, instance ID: {levelObject.instanceID.Format()} from {user.SteamId.Format()}.");
        }
        else 
        {
            BuildableResponsibilities.Set(id, user.SteamId.m_SteamID);
            Logger.LogDebug($"[{Source}] Granted request for instantiation of buildable {asset.Format()} {id.Format()} from {user.SteamId.Format()}.");
        }

        SyncIfAuthority(netId);
    }
#endif
    private static void InitializeLevelObject(Transform transform, out LevelObject? levelObject,
#if SERVER
        out
#endif
            NetId netId)
    {
#if SERVER
        netId = NetId.INVALID;
#endif
        if (TryFindObject(transform, out RegionIdentifier id))
        {
            levelObject = GetObjectUnsafe(id);
#if SERVER
            netId = LevelObjectNetIdDatabase.AddObject(levelObject);
#else
            LevelObjectNetIdDatabase.RegisterObject(levelObject, netId);
#endif
            Logger.LogDebug($"[{Source}] Assigned object NetId: {netId.Format()}.");
            return;
        }

        levelObject = null;
        Logger.LogWarning($"Did not find object of transform {transform.name.Format()}.", method: Source);
    }
    private static void InitializeBuildable(Transform transform, out RegionIdentifier id,
#if SERVER
        out
#endif
            NetId netId)
    {
#if SERVER
        netId = NetId.INVALID;
#endif
        if (TryFindBuildable(transform, out id))
        {
            LevelBuildableObject buildable = GetBuildableUnsafe(id);
#if SERVER
            netId = LevelObjectNetIdDatabase.AddBuildable(buildable, id);
#else
            LevelObjectNetIdDatabase.RegisterBuildable(buildable, id, netId);
#endif
            Logger.LogDebug($"[{Source}] Assigned buildable NetId: {netId.Format()}.");
            return;
        }
        
        id = RegionIdentifier.Invalid;
        Logger.LogWarning($"Did not find buildable of transform {transform.name.Format()}.", method: Source);
    }
    [Pure]
    public static int GetMaterialIndexOverride(this LevelObject levelObject)
    {
        return GetMaterialIndexOverrideIntl == null ? -1 : GetMaterialIndexOverrideIntl(levelObject);
    }
    [Pure]
    public static AssetReference<MaterialPaletteAsset> GetCustomMaterialOverride(this LevelObject levelObject)
    {
        return GetCustomMaterialOverrideIntl == null ? AssetReference<MaterialPaletteAsset>.invalid : GetCustomMaterialOverrideIntl(levelObject);
    }
    internal static bool SetMaterialIndexOverrideLocal(LevelObject levelObject, int materialIndexOverride, bool reapply = true)
    {
        ThreadUtil.assertIsGameThread();

        if (SetMaterialIndexOverrideIntl == null || reapply && !Dedicator.IsDedicatedServer && CallReapplyMaterialOverridesIntl == null)
            return false;
        if (materialIndexOverride < -1)
            materialIndexOverride = -1;
        SetMaterialIndexOverrideIntl(levelObject, materialIndexOverride);
        if (reapply && !Dedicator.IsDedicatedServer)
            CallReapplyMaterialOverridesIntl!(levelObject);
        return true;
    }
    internal static bool SetCustomMaterialPaletteOverrideLocal(LevelObject levelObject, AssetReference<MaterialPaletteAsset> customMaterialOverride
#if CLIENT
        , bool reapply = true
#endif
        )
    {
        ThreadUtil.assertIsGameThread();

        if (SetCustomMaterialOverrideIntl == null
#if CLIENT
            || reapply && CallReapplyMaterialOverridesIntl == null
#endif
            )
            return false;
        SetCustomMaterialOverrideIntl(levelObject, customMaterialOverride);
#if CLIENT
        if (reapply)
            CallReapplyMaterialOverridesIntl!(levelObject);
#endif
        return true;
    }
    public static bool SetMaterialIndexOverride(this LevelObject levelObject, int materialIndexOverride
#if CLIENT
        , bool reapply = true
#endif
        )
    {
        if (!SetMaterialIndexOverrideLocal(levelObject, materialIndexOverride
#if CLIENT
                , reapply
#endif
            ))
            return false;
#if CLIENT
        if (DevkitServerModule.IsEditing && LevelObjectNetIdDatabase.TryGetObjectNetId(levelObject, out NetId netId))
            ClientEvents.InvokeOnUpdateObjectsMaterialIndexOverride(new UpdateObjectsMaterialIndexOverrideProperties(new NetId[] { netId }, materialIndexOverride, CachedTime.DeltaTime));
#else
        if (LevelObjectNetIdDatabase.TryGetObjectNetId(levelObject, out NetId netId))
        {
            EditorActions.QueueServerAction(new UpdateObjectsMaterialIndexOverrideAction
            {
                DeltaTime = CachedTime.DeltaTime,
                Value = materialIndexOverride,
                NetIds = new NetId[] { netId }
            });
        }
#endif
        return true;
    }
    public static bool SetCustomMaterialPaletteOverride(this LevelObject levelObject, AssetReference<MaterialPaletteAsset> customMaterialPaletteOverride
#if CLIENT
        , bool reapply = true
#endif
        )
    {
        if (!SetCustomMaterialPaletteOverrideLocal(levelObject, customMaterialPaletteOverride
#if CLIENT
                , reapply
#endif
                ))
            return false;
#if CLIENT
        if (DevkitServerModule.IsEditing && LevelObjectNetIdDatabase.TryGetObjectNetId(levelObject, out NetId netId))
            ClientEvents.InvokeOnUpdateObjectsCustomMaterialPaletteOverride(new UpdateObjectsCustomMaterialPaletteOverrideProperties(new NetId[] { netId }, customMaterialPaletteOverride, CachedTime.DeltaTime));
#else
        if (LevelObjectNetIdDatabase.TryGetObjectNetId(levelObject, out NetId netId))
        {
            EditorActions.QueueServerAction(new UpdateObjectsCustomMaterialPaletteOverrideAction
            {
                DeltaTime = CachedTime.DeltaTime,
                Value = customMaterialPaletteOverride,
                NetIds = new NetId[] { netId }
            });
        }
#endif
        return true;
    }
#if CLIENT
    public static bool ReapplyMaterialOverrides(this LevelObject levelObject)
    {
        ThreadUtil.assertIsGameThread();

        if (CallReapplyMaterialOverridesIntl == null)
            return false;
        CallReapplyMaterialOverridesIntl(levelObject);
        return true;
    }
#endif
    public static bool GetObjectOrBuildableAsset(Guid guid, out ObjectAsset? @object, out ItemAsset? buildable)
    {
        Asset asset = Assets.find(guid);
        @object = asset as ObjectAsset;
        buildable = asset as ItemAsset;
        return @object != null || buildable is ItemStructureAsset or ItemBarricadeAsset;
    }
    private static bool CheckSync(out ObjectSync sync)
    {
        sync = null!;
#if CLIENT
        if (!DevkitServerModule.IsEditing || EditorUser.User == null || EditorUser.User.ObjectSync == null || !EditorUser.User.ObjectSync.HasAuthority)
            return false;
        sync = EditorUser.User.ObjectSync;
#elif SERVER
        if (!DevkitServerModule.IsEditing || ObjectSync.ServersideAuthority == null || !ObjectSync.ServersideAuthority.HasAuthority)
            return false;
        sync = ObjectSync.ServersideAuthority;
#endif
        return true;
    }
    public static bool SyncIfAuthority(LevelObject levelObject)
    {
        if (!CheckSync(out ObjectSync sync))
            return false;
        sync.EnqueueSync(levelObject);
        return true;
    }
    public static bool SyncIfAuthority(LevelBuildableObject buildable)
    {
        if (!CheckSync(out ObjectSync sync))
            return false;
        sync.EnqueueSync(buildable);
        return true;
    }
    public static bool SyncIfAuthority(RegionIdentifier buildable)
    {
        if (!CheckSync(out ObjectSync sync))
            return false;
        sync.EnqueueSync(buildable);
        return true;
    }
    public static bool SyncIfAuthority(NetId buildableOrObject)
    {
        if (!CheckSync(out ObjectSync sync))
            return false;
        sync.EnqueueSync(buildableOrObject);
        return true;
    }
    [Pure]
    public static Transform? GetTransform(this LevelObject obj)
    {
        if (obj.transform != null)
            return obj.transform;

        if (obj.skybox != null)
            return obj.skybox;

        if (obj.placeholderTransform != null)
            return obj.placeholderTransform;

        return null;
    }
    public static LevelObject? FindObject(Transform transform, bool checkSkyboxAndPlaceholder = false) => TryFindObject(transform, out LevelObject obj, checkSkyboxAndPlaceholder) ? obj : null;
    public static LevelObject? FindObject(uint instanceId) => TryFindObject(instanceId, out LevelObject obj) ? obj : null;
    public static LevelObject? FindObject(Vector3 expectedPosition, uint instanceId) => TryFindObject(expectedPosition, instanceId, out LevelObject obj) ? obj : null;
    public static LevelBuildableObject? FindBuildable(Transform transform) => TryFindBuildable(transform, out LevelBuildableObject obj) ? obj : null;
    public static RegionIdentifier FindObjectCoordinates(Transform transform, bool checkSkyboxAndPlaceholder = false) => TryFindObject(transform, out RegionIdentifier regionId, checkSkyboxAndPlaceholder) ? regionId : RegionIdentifier.Invalid;
    public static RegionIdentifier FindObjectCoordinates(uint instanceId) => TryFindObject(instanceId, out RegionIdentifier regionId) ? regionId : RegionIdentifier.Invalid;
    public static RegionIdentifier FindObjectCoordinates(Vector3 expectedPosition, uint instanceId) => TryFindObject(expectedPosition, instanceId, out RegionIdentifier regionId) ? regionId : RegionIdentifier.Invalid;
    public static RegionIdentifier FindBuildableCoordinates(Transform transform) => TryFindBuildable(transform, out RegionIdentifier regionId) ? regionId : RegionIdentifier.Invalid;
    public static bool TryFindObject(Transform transform, out LevelObject levelObject, bool checkSkyboxAndPlaceholder = false)
    {
        if (transform == null)
        {
            levelObject = null!;
            return false;
        }
        LevelObject? found = null;
        RegionUtil.ForEachRegion(transform.position, coord => !TryFindObject(transform, coord, out found, checkSkyboxAndPlaceholder));
        levelObject = found!;
        return found != null;
    }
    public static bool TryFindObject(Transform transform, out RegionIdentifier regionId, bool checkSkyboxAndPlaceholder = false)
    {
        if (transform == null)
        {
            regionId = RegionIdentifier.Invalid;
            return false;
        }
        RegionIdentifier r = RegionIdentifier.Invalid;
        RegionUtil.ForEachRegion(transform.position, coord => !TryFindObject(transform, coord, out r, checkSkyboxAndPlaceholder));
        regionId = r;
        return !r.IsInvalid;
    }
    public static bool TryFindObject(uint instanceId, out LevelObject levelObject)
    {
        LevelObject? found = null;
        RegionUtil.ForEachRegion(coord => !TryFindObject(instanceId, coord, out found));
        levelObject = found!;
        return found != null;
    }
    public static bool TryFindObject(uint instanceId, out RegionIdentifier regionId)
    {
        RegionIdentifier r = RegionIdentifier.Invalid;
        RegionUtil.ForEachRegion(coord => !TryFindObject(instanceId, coord, out r));
        regionId = r;
        return !r.IsInvalid;
    }
    public static bool TryFindObject(Vector3 expectedPosition, uint instanceId, out LevelObject levelObject)
    {
        LevelObject? found = null;
        RegionUtil.ForEachRegion(expectedPosition, coord => !TryFindObject(instanceId, coord, out found));
        levelObject = found!;
        return found != null;
    }
    public static bool TryFindObject(Vector3 expectedPosition, uint instanceId, out RegionIdentifier regionId)
    {
        RegionIdentifier r = RegionIdentifier.Invalid;
        RegionUtil.ForEachRegion(expectedPosition, coord => !TryFindObject(instanceId, coord, out r));
        regionId = r;
        return !r.IsInvalid;
    }
    public static bool TryFindObject(Transform transform, RegionCoord region, out LevelObject levelObject, bool checkSkyboxAndPlaceholder = false)
    {
        List<LevelObject> objRegion = LevelObjects.objects[region.x, region.y];
        int ct = objRegion.Count;
        for (int i = 0; i < ct; ++i)
        {
            if (ReferenceEquals(objRegion[i].transform, transform))
            {
                levelObject = objRegion[i];
                return true;
            }
        }
        if (checkSkyboxAndPlaceholder)
        {
            for (int i = 0; i < ct; ++i)
            {
                if (ReferenceEquals(objRegion[i].skybox, transform))
                {
                    levelObject = objRegion[i];
                    return true;
                }
            }
            for (int i = 0; i < ct; ++i)
            {
                if (ReferenceEquals(objRegion[i].placeholderTransform, transform))
                {
                    levelObject = objRegion[i];
                    return true;
                }
            }
        }

        levelObject = null!;
        return false;
    }
    public static bool TryFindObject(Transform transform, RegionCoord region, out RegionIdentifier regionId, bool checkSkyboxAndPlaceholder = false)
    {
        List<LevelObject> objRegion = LevelObjects.objects[region.x, region.y];
        int ct = objRegion.Count;
        for (int i = 0; i < ct; ++i)
        {
            if (ReferenceEquals(objRegion[i].transform, transform))
            {
                regionId = new RegionIdentifier(region, i);
                return true;
            }
        }
        if (checkSkyboxAndPlaceholder)
        {
            for (int i = 0; i < ct; ++i)
            {
                if (ReferenceEquals(objRegion[i].skybox, transform))
                {
                    regionId = new RegionIdentifier(region, i);
                    return true;
                }
            }
            for (int i = 0; i < ct; ++i)
            {
                if (ReferenceEquals(objRegion[i].placeholderTransform, transform))
                {
                    regionId = new RegionIdentifier(region, i);
                    return true;
                }
            }
        }

        regionId = RegionIdentifier.Invalid;
        return false;
    }
    public static bool TryFindObject(uint instanceId, RegionCoord region, out RegionIdentifier regionId)
    {
        List<LevelObject> objRegion = LevelObjects.objects[region.x, region.y];
        int ct = objRegion.Count;
        for (int i = 0; i < ct; ++i)
        {
            if (objRegion[i].instanceID == instanceId)
            {
                regionId = new RegionIdentifier(region, i);
                return true;
            }
        }

        regionId = RegionIdentifier.Invalid;
        return false;
    }
    public static bool TryFindObject(uint instanceId, RegionCoord region, out LevelObject levelObject)
    {
        List<LevelObject> objRegion = LevelObjects.objects[region.x, region.y];
        int ct = objRegion.Count;
        for (int i = 0; i < ct; ++i)
        {
            if (objRegion[i].instanceID == instanceId)
            {
                levelObject = objRegion[i];
                return true;
            }
        }

        levelObject = null!;
        return false;
    }
    public static bool TryFindBuildable(Transform transform, out LevelBuildableObject buildable)
    {
        LevelBuildableObject? found = null;
        RegionUtil.ForEachRegion(transform.position, coord => !TryFindBuildable(transform, coord, out found));
        buildable = found!;
        return found != null;
    }
    public static bool TryFindBuildable(Transform transform, out RegionIdentifier regionId)
    {
        RegionIdentifier r = RegionIdentifier.Invalid;
        RegionUtil.ForEachRegion(transform.position, coord => !TryFindBuildable(transform, coord, out r));
        regionId = r;
        return !r.IsInvalid;
    }
    public static bool TryFindBuildable(Transform transform, RegionCoord region, out LevelBuildableObject buildable)
    {
        List<LevelBuildableObject> buildableRegion = LevelObjects.buildables[region.x, region.y];
        int ct = buildableRegion.Count;
        for (int i = 0; i < ct; ++i)
        {
            if (ReferenceEquals(buildableRegion[i].transform, transform))
            {
                buildable = buildableRegion[i];
                return true;
            }
        }

        buildable = null!;
        return false;
    }
    public static bool TryFindBuildable(Transform transform, RegionCoord region, out RegionIdentifier regionId)
    {
        List<LevelBuildableObject> buildableRegion = LevelObjects.buildables[region.x, region.y];
        int ct = buildableRegion.Count;
        for (int i = 0; i < ct; ++i)
        {
            if (ReferenceEquals(buildableRegion[i].transform, transform))
            {
                regionId = new RegionIdentifier(region, i);
                return true;
            }
        }

        regionId = RegionIdentifier.Invalid;
        return false;
    }
    public static bool TryFindObjectOrBuildable(Transform transform, out LevelObject? @object, out LevelBuildableObject? buildable)
    {
        RegionIdentifier r = RegionIdentifier.Invalid;
        bool isBuildable = false;
        RegionUtil.ForEachRegion(transform.position, coord =>
        {
            if (!TryFindObject(transform, coord, out r, false))
            {
                if (!TryFindBuildable(transform, coord, out r))
                    return true;
                isBuildable = true;
            }

            return false;
        });
        if (r.IsInvalid)
        {
            @object = null;
            buildable = null;
            return false;
        }
        @object = isBuildable ? null : GetObjectUnsafe(r);
        buildable = isBuildable ? GetBuildableUnsafe(r) : null;
        return @object != null || buildable != null;
    }
#if SERVER
    [Pure]
    public static bool CheckMovePermission(uint instanceId, ulong user)
    {
        return VanillaPermissions.EditObjects.Has(user, true) ||
               VanillaPermissions.MoveUnownedObjects.Has(user, false) ||
               VanillaPermissions.PlaceObjects.Has(user, false) &&
               LevelObjectResponsibilities.IsPlacer(instanceId, user);
    }
    [Pure]
    public static bool CheckPlacePermission(ulong user)
    {
        return VanillaPermissions.EditObjects.Has(user, true) ||
               VanillaPermissions.PlaceObjects.Has(user, false);
    }
    [Pure]
    public static bool CheckDeletePermission(uint instanceId, ulong user)
    {
        return VanillaPermissions.EditObjects.Has(user, true) ||
               VanillaPermissions.RemoveUnownedObjects.Has(user, false) ||
               VanillaPermissions.PlaceObjects.Has(user, false) && HierarchyResponsibilities.IsPlacer(instanceId, user);
    }
    [Pure]
    public static bool CheckMoveBuildablePermission(RegionIdentifier id, ulong user)
    {
        return VanillaPermissions.EditObjects.Has(user, true) ||
               VanillaPermissions.MoveUnownedObjects.Has(user, false) ||
               VanillaPermissions.PlaceObjects.Has(user, false) &&
               BuildableResponsibilities.IsPlacer(id, user);
    }
    [Pure]
    public static bool CheckDeleteBuildablePermission(RegionIdentifier id, ulong user)
    {
        return VanillaPermissions.EditObjects.Has(user, true) ||
               VanillaPermissions.RemoveUnownedObjects.Has(user, false) ||
               VanillaPermissions.PlaceObjects.Has(user, false) &&
               BuildableResponsibilities.IsPlacer(id, user);
    }
#elif CLIENT
    [Pure]
    public static bool CheckMovePermission(uint instanceId)
    {
        return VanillaPermissions.EditObjects.Has(true) ||
               VanillaPermissions.MoveUnownedObjects.Has(false) ||
               VanillaPermissions.PlaceObjects.Has(false) &&
               LevelObjectResponsibilities.IsPlacer(instanceId);
    }
    [Pure]
    public static bool CheckPlacePermission()
    {
        return VanillaPermissions.EditObjects.Has(true) ||
               VanillaPermissions.PlaceObjects.Has(false);
    }
    [Pure]
    public static bool CheckDeletePermission(uint instanceId)
    {
        return VanillaPermissions.EditObjects.Has(true) ||
               VanillaPermissions.RemoveUnownedObjects.Has(false) ||
               VanillaPermissions.PlaceObjects.Has(false) && HierarchyResponsibilities.IsPlacer(instanceId);
    }
    [Pure]
    public static bool CheckMoveBuildablePermission(RegionIdentifier id)
    {
        return VanillaPermissions.EditObjects.Has(true) ||
               VanillaPermissions.MoveUnownedObjects.Has(false) ||
               VanillaPermissions.PlaceObjects.Has(false) &&
               BuildableResponsibilities.IsPlacer(id);
    }
    [Pure]
    public static bool CheckDeleteBuildablePermission(RegionIdentifier id)
    {
        return VanillaPermissions.EditObjects.Has(true) ||
               VanillaPermissions.RemoveUnownedObjects.Has(false) ||
               VanillaPermissions.PlaceObjects.Has(false) &&
               BuildableResponsibilities.IsPlacer(id);
    }
#endif
    [Pure]
    public static LevelBuildableObject? GetBuildable(RegionIdentifier id)
    {
        if (id.X >= Regions.WORLD_SIZE || id.Y >= Regions.WORLD_SIZE)
            return null;
        List<LevelBuildableObject> buildables = LevelObjects.buildables[id.X, id.Y];
        return buildables.Count > id.Index ? buildables[id.Index] : null;
    }
    [Pure]
    public static LevelBuildableObject GetBuildableUnsafe(RegionIdentifier id) => LevelObjects.buildables[id.X, id.Y][id.Index];
    [Pure]
    public static LevelObject? GetObject(RegionIdentifier id)
    {
        if (id.X >= Regions.WORLD_SIZE || id.Y >= Regions.WORLD_SIZE)
            return null;
        List<LevelObject> objects = LevelObjects.objects[id.X, id.Y];
        return objects.Count > id.Index ? objects[id.Index] : null;
    }
    [Pure]
    public static LevelObject GetObjectUnsafe(RegionIdentifier id) => LevelObjects.objects[id.X, id.Y][id.Index];
    public static Asset? GetAsset(this EditorCopy copy) => copy.objectAsset ?? (Asset)copy.itemAsset;
#if CLIENT
    public static bool UpdateCulledObjects(CullingVolume volume)
    {
        if (ClearCullingVolumeObjects == null || FindCullingVolumeObjects == null)
            return false;

        ClearCullingVolumeObjects(volume);
        FindCullingVolumeObjects(volume);
        return true;
    }
    public static void UpdateContainingCullingVolumesForMove(Vector3 from, Vector3 to)
    {
        foreach (CullingVolume volume in CullingVolumeManager.Get().GetAllVolumes())
        {
            if (volume.IsPositionInsideVolume(from) || volume.IsPositionInsideVolume(to))
                UpdateCulledObjects(volume);
        }
    }
    public static void UpdateContainingCullingVolumes(Vector3 position)
    {
        foreach (CullingVolume volume in CullingVolumeManager.Get().GetAllVolumes())
        {
            if (volume.IsPositionInsideVolume(position))
                UpdateCulledObjects(volume);
        }
    }
    internal static void ClientInstantiateObjectsAndLock(EditorCopy[] copies)
    {
        LevelObjectPatches.IsSyncing = true;
        UIMessage.SendEditorMessage("Syncing");
        DevkitServerModule.ComponentHost.StartCoroutine(PasteObjectsCoroutine(copies));
    }
    private static IEnumerator PasteObjectsCoroutine(EditorCopy[] copies)
    {
        try
        {
            EditorUser? user = EditorUser.User;
            if (user == null)
                yield break;
            for (int i = 0; i < copies.Length && DevkitServerModule.IsEditing && Level.isEditor; ++i)
            {
                if (!user.IsOnline)
                    break;
                EditorCopy copy = copies[i];
                if (copy.itemAsset == null && copy.objectAsset == null)
                    continue;
                NetTask instantiateRequest = SendRequestInstantiation.Request(SendLevelObjectInstantiation,
                    copy.objectAsset != null ? copy.objectAsset.GUID : copy.itemAsset!.GUID,
                    copy.position, copy.rotation, copy.scale, 5000);

                yield return instantiateRequest;

                if (!user.IsOnline)
                    break;

                if (!instantiateRequest.Parameters.Success)
                {
                    Logger.LogWarning($"Failed to instantiate {copy.GetAsset().Format()} at {copy.position.Format()} (#{i.Format()}). {instantiateRequest.Parameters}.");
                    continue;
                }

                if (!instantiateRequest.Parameters.TryGetParameter(5, out NetId netId))
                    continue;

                if (!LevelObjectNetIdDatabase.TryGetObjectOrBuildable(netId, out LevelObject? levelObject, out LevelBuildableObject? buildable, out _))
                    continue;

                Transform pasted = levelObject == null ? buildable!.transform : levelObject.transform;

                EditorObjects.addSelection(pasted);
                Logger.LogDebug(buildable != null ? $"Pasted buildable: {buildable.asset.itemName}." : $"Pasted object: {levelObject!.asset.objectName}.");

                SyncIfAuthority(netId);
            }
        }
        finally
        {
            LevelObjectPatches.IsSyncing = false;
        }
    }
#endif
}

public delegate void BuildableRegionUpdated(LevelBuildableObject buildable, RegionIdentifier from, RegionIdentifier to);
public delegate void LevelObjectRegionUpdated(LevelObject levelObject, RegionIdentifier from, RegionIdentifier to);
public delegate void BuildableMoved(LevelBuildableObject buildable, Vector3 fromPos, Quaternion fromRot, Vector3 fromScale, Vector3 toPos, Quaternion toRot, Vector3 toScale);
public delegate void LevelObjectMoved(LevelObject levelObject, Vector3 fromPos, Quaternion fromRot, Vector3 fromScale, Vector3 toPos, Quaternion toRot, Vector3 toScale);
public delegate void BuildableRemoved(LevelBuildableObject buildable, RegionIdentifier id);
public delegate void LevelObjectRemoved(LevelObject levelObject, RegionIdentifier id);