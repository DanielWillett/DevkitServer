using DevkitServer.API.Permissions;
using DevkitServer.Core.Permissions;
using DevkitServer.Models;
using DevkitServer.Multiplayer;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Multiplayer.Levels;
using DevkitServer.Players.UI;
using JetBrains.Annotations;
#if CLIENT
using DevkitServer.Multiplayer.Actions;
using DevkitServer.Patches;
using DevkitServer.Players;
using System.Reflection;
using Action = System.Action;
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

            try
            {
                Transform? newBuildable = LevelObjects.addBuildable(position, rotation, itemAsset.id);
                if (newBuildable == null)
                    return StandardErrorCode.GenericError;

                InitializeBuildable(newBuildable, out RegionIdentifier id, netId);

                if (owner == Provider.client.m_SteamID)
                    BuildableResponsibilities.Set(id, true, false);
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
    public static bool GetObjectOrBuildableAsset(Guid guid, out ObjectAsset? @object, out ItemAsset? buildable)
    {
        Asset asset = Assets.find(guid);
        @object = asset as ObjectAsset;
        buildable = asset as ItemAsset;
        return @object != null || buildable is ItemStructureAsset or ItemBarricadeAsset;
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

    [Pure]
    public static LevelObject? FindObject(Transform transform, bool checkSkyboxAndPlaceholder = false)
    {
        ThreadUtil.assertIsGameThread();
        if (transform == null)
            return null;

        bool r = false;
        if (Regions.tryGetCoordinate(transform.position, out byte x, out byte y))
        {
            LevelObject? obj =
                SearchInRegion(transform, x, y, checkSkyboxAndPlaceholder) ??
                SearchInRegion(transform, x + 1, y, checkSkyboxAndPlaceholder) ??
                SearchInRegion(transform, x - 1, y, checkSkyboxAndPlaceholder) ??
                SearchInRegion(transform, x, y + 1, checkSkyboxAndPlaceholder) ??
                SearchInRegion(transform, x + 1, y + 1, checkSkyboxAndPlaceholder) ??
                SearchInRegion(transform, x + 1, y - 1, checkSkyboxAndPlaceholder) ??
                SearchInRegion(transform, x - 1, y - 1, checkSkyboxAndPlaceholder) ??
                SearchInRegion(transform, x - 1, y + 1, checkSkyboxAndPlaceholder);

            if (obj != null)
                return obj;

            r = true;
        }

        for (int x2 = 0; x2 < Regions.WORLD_SIZE; ++x2)
        {
            for (int y2 = 0; y2 < Regions.WORLD_SIZE; ++y2)
            {
                if (r && x2 <= x + 1 && x2 >= x - 1 && y2 <= y + 1 && y2 >= y - 1)
                    continue;
                LevelObject? obj = SearchInRegion(transform, x2, y2, checkSkyboxAndPlaceholder);
                if (obj != null)
                    return obj;
            }
        }

        return null;
    }
    public static bool TryFindObject(Transform transform, out RegionIdentifier id, bool checkSkyboxAndPlaceholder = false)
    {
        ThreadUtil.assertIsGameThread();
        if (transform != null)
        {
            bool r = false;
            if (Regions.tryGetCoordinate(transform.position, out byte x, out byte y))
            {
                int[] offsets = LandscapeUtil.SurroundingOffsets;
                for (int i = 0; i < offsets.Length; i += 2)
                {
                    if (SearchInRegion(transform, x + offsets[i], y + offsets[i + 1], ref x, ref y, out ushort index, checkSkyboxAndPlaceholder))
                    {
                        id = new RegionIdentifier(x, y, index);
                        return true;
                    }
                }

                r = true;
            }

            for (int x2 = 0; x2 < Regions.WORLD_SIZE; ++x2)
            {
                for (int y2 = 0; y2 < Regions.WORLD_SIZE; ++y2)
                {
                    if (r && x2 <= x + 1 && x2 >= x - 1 && y2 <= y + 1 && y2 >= y - 1)
                        continue;
                    if (SearchInRegion(transform, x2, y2, ref x, ref y, out ushort index, checkSkyboxAndPlaceholder))
                    {
                        id = new RegionIdentifier(x, y, index);
                        return true;
                    }
                }
            }
        }
        
        id = RegionIdentifier.Invalid;
        return false;
    }
    [Pure]
    public static LevelObject? FindObject(uint instanceId)
    {
        if (TryFindObjectCoordinates(instanceId, out RegionIdentifier id))
            return LevelObjects.objects[id.X, id.Y][id.Index];

        return null;
    }
    [Pure]
    public static LevelObject? FindObject(Vector3 pos, uint instanceId)
    {
        if (TryFindObjectCoordinates(pos, instanceId, out RegionIdentifier id))
            return LevelObjects.objects[id.X, id.Y][id.Index];

        return null;
    }

    public static bool TryFindObjectCoordinates(uint instanceId, out RegionIdentifier id)
    {
        ThreadUtil.assertIsGameThread();

        for (int x2 = 0; x2 < Regions.WORLD_SIZE; ++x2)
        {
            for (int y2 = 0; y2 < Regions.WORLD_SIZE; ++y2)
            {
                List<LevelObject> region = LevelObjects.objects[x2, y2];
                int c = Math.Min(region.Count, ushort.MaxValue);
                for (int i = 0; i < c; ++i)
                {
                    if (region[i].instanceID == instanceId)
                    {
                        id = new RegionIdentifier((byte)x2, (byte)y2, (ushort)i);
                        return true;
                    }
                }
            }
        }

        id = RegionIdentifier.Invalid;
        return false;
    }

    public static bool TryFindObjectCoordinates(Vector3 expectedPosition, uint instanceId, out RegionIdentifier id)
    {
        ThreadUtil.assertIsGameThread();

        bool r = false;
        if (Regions.tryGetCoordinate(expectedPosition, out byte x, out byte y))
        {
            int[] offsets = LandscapeUtil.SurroundingOffsets;
            for (int i = 0; i < offsets.Length; i += 2)
            {
                if (SearchInRegion(x + offsets[i], y + offsets[i + 1], instanceId, ref x, ref y, out ushort index))
                {
                    id = new RegionIdentifier(x, y, index);
                    return true;
                }
            }

            r = true;
        }

        for (int x2 = 0; x2 < Regions.WORLD_SIZE; ++x2)
        {
            for (int y2 = 0; y2 < Regions.WORLD_SIZE; ++y2)
            {
                if (r && x2 <= x + 1 && x2 >= x - 1 && y2 <= y + 1 && y2 >= y - 1)
                    continue;
                List<LevelObject> region = LevelObjects.objects[x2, y2];
                int c = Math.Min(region.Count, ushort.MaxValue);
                for (int i = 0; i < c; ++i)
                {
                    if (region[i].instanceID == instanceId)
                    {
                        id = new RegionIdentifier((byte)x2, (byte)y2, (ushort)i);
                        return true;
                    }
                }
            }
        }

        id = RegionIdentifier.Invalid;
        return false;
    }
    private static bool SearchInRegion(int regionX, int regionY, uint instanceId, ref byte x, ref byte y, out ushort index)
    {
        if (regionX < 0 || regionY < 0 || regionX > Regions.WORLD_SIZE || regionY > Regions.WORLD_SIZE)
        {
            index = ushort.MaxValue;
            return false;
        }
        if (SearchInRegion((byte)regionX, (byte)regionY, instanceId, out index))
        {
            x = (byte)regionX;
            y = (byte)regionY;
            return true;
        }

        return false;
    }
    [Pure]
    private static LevelObject? SearchInRegion(Transform transform, int regionX, int regionY, bool checkSkyboxAndPlaceholder)
    {
        if (regionX < 0 || regionY < 0 || regionX > Regions.WORLD_SIZE || regionY > Regions.WORLD_SIZE)
            return null;
        List<LevelObject> region = LevelObjects.objects[regionX, regionY];
        for (int i = 0; i < region.Count; ++i)
        {
            if (ReferenceEquals(region[i].transform, transform))
                return region[i];
        }
        if (checkSkyboxAndPlaceholder)
        {
            for (int i = 0; i < region.Count; ++i)
            {
                if (ReferenceEquals(region[i].skybox, transform))
                    return region[i];
            }
            for (int i = 0; i < region.Count; ++i)
            {
                if (ReferenceEquals(region[i].placeholderTransform, transform))
                    return region[i];
            }
        }

        return null;
    }
    private static bool SearchInRegion(Transform transform, int regionX, int regionY, ref byte x, ref byte y, out ushort index, bool checkSkyboxAndPlaceholder)
    {
        if (regionX < 0 || regionY < 0 || regionX > Regions.WORLD_SIZE || regionY > Regions.WORLD_SIZE)
        {
            index = ushort.MaxValue;
            return false;
        }
        List<LevelObject> region = LevelObjects.objects[regionX, regionY];
        int c = Math.Min(ushort.MaxValue, region.Count);
        for (int i = 0; i < c; ++i)
        {
            if (ReferenceEquals(region[i].transform, transform))
            {
                x = (byte)regionX;
                y = (byte)regionY;
                index = (ushort)i;
                return true;
            }
        }
        if (checkSkyboxAndPlaceholder)
        {
            for (int i = 0; i < c; ++i)
            {
                if (ReferenceEquals(region[i].skybox, transform))
                {
                    x = (byte)regionX;
                    y = (byte)regionY;
                    index = (ushort)i;
                    return true;
                }
            }
            for (int i = 0; i < c; ++i)
            {
                if (ReferenceEquals(region[i].placeholderTransform, transform))
                {
                    x = (byte)regionX;
                    y = (byte)regionY;
                    index = (ushort)i;
                    return true;
                }
            }
        }

        index = ushort.MaxValue;
        return false;
    }
    private static bool SearchInRegion(byte regionX, byte regionY, uint instanceId, out ushort index)
    {
        if (regionX > Regions.WORLD_SIZE || regionY > Regions.WORLD_SIZE)
        {
            index = ushort.MaxValue;
            return false;
        }
        List<LevelObject> region = LevelObjects.objects[regionX, regionY];
        int c = Math.Min(region.Count, ushort.MaxValue - 1);
        for (int i = 0; i < c; ++i)
        {
            if (region[i].instanceID == instanceId)
            {
                index = (ushort)i;
                return true;
            }
        }

        index = ushort.MaxValue;
        return false;
    }
    [Pure]
    public static LevelBuildableObject? FindBuildable(Transform transform)
    {
        ThreadUtil.assertIsGameThread();
        if (transform == null)
            return null;

        bool r = false;
        if (Regions.tryGetCoordinate(transform.position, out byte x, out byte y))
        {
            LevelBuildableObject? obj =
                SearchInBuildableRegion(transform, x, y) ??
                SearchInBuildableRegion(transform, x + 1, y) ??
                SearchInBuildableRegion(transform, x - 1, y) ??
                SearchInBuildableRegion(transform, x, y + 1) ??
                SearchInBuildableRegion(transform, x + 1, y + 1) ??
                SearchInBuildableRegion(transform, x + 1, y - 1) ??
                SearchInBuildableRegion(transform, x - 1, y - 1) ??
                SearchInBuildableRegion(transform, x - 1, y + 1);

            if (obj != null)
                return obj;

            r = true;
        }

        for (int x2 = 0; x2 < Regions.WORLD_SIZE; ++x2)
        {
            for (int y2 = 0; y2 < Regions.WORLD_SIZE; ++y2)
            {
                if (r && x2 <= x + 1 && x2 >= x - 1 && y2 <= y + 1 && y2 >= y - 1)
                    continue;
                List<LevelBuildableObject> region = LevelObjects.buildables[x2, y2];
                int c = Math.Min(region.Count, ushort.MaxValue);
                for (int i = 0; i < c; ++i)
                {
                    if (ReferenceEquals(region[i].transform, transform))
                        return region[i];
                }
            }
        }

        return null;
    }
    public static bool TryFindBuildable(Transform transform, out RegionIdentifier id) => TryFindBuildable(transform, transform.position, out id);
    public static bool TryFindBuildable(Transform transform, Vector3 savedPosition, out RegionIdentifier id)
    {
        ThreadUtil.assertIsGameThread();
        if (transform != null)
        {
            bool r = false;
            if (Regions.tryGetCoordinate(savedPosition, out byte x, out byte y))
            {
                int[] offsets = LandscapeUtil.SurroundingOffsets;
                for (int i = 0; i < offsets.Length; i += 2)
                {
                    if (SearchInBuildableRegion(transform, x + offsets[i], y + offsets[i + 1], ref x, ref y, out ushort index))
                    {
                        id = new RegionIdentifier(x, y, index);
                        return true;
                    }
                }

                r = true;
            }

            for (int x2 = 0; x2 < Regions.WORLD_SIZE; ++x2)
            {
                for (int y2 = 0; y2 < Regions.WORLD_SIZE; ++y2)
                {
                    if (r && x2 <= x + 1 && x2 >= x - 1 && y2 <= y + 1 && y2 >= y - 1)
                        continue;
                    List<LevelBuildableObject> region = LevelObjects.buildables[x2, y2];
                    int c = Math.Min(region.Count, ushort.MaxValue);
                    for (int i = 0; i < c; ++i)
                    {
                        if (ReferenceEquals(region[i].transform, transform))
                        {
                            id = new RegionIdentifier((byte)x2, (byte)y2, (ushort)i);
                            return true;
                        }
                    }
                }
            }
        }
        
        id = RegionIdentifier.Invalid;
        return false;
    }
    [Pure]
    private static LevelBuildableObject? SearchInBuildableRegion(Transform transform, int regionX, int regionY)
    {
        if (regionX < 0 || regionY < 0 || regionX > Regions.WORLD_SIZE || regionY > Regions.WORLD_SIZE)
            return null;
        List<LevelBuildableObject> region = LevelObjects.buildables[regionX, regionY];
        for (int i = 0; i < region.Count; ++i)
        {
            if (ReferenceEquals(region[i].transform, transform))
                return region[i];
        }

        return null;
    }
    private static bool SearchInBuildableRegion(Transform transform, int regionX, int regionY, ref byte x, ref byte y, out ushort index)
    {
        if (regionX < 0 || regionY < 0 || regionX > Regions.WORLD_SIZE || regionY > Regions.WORLD_SIZE)
        {
            index = ushort.MaxValue;
            return false;
        }
        List<LevelBuildableObject> region = LevelObjects.buildables[regionX, regionY];
        for (int i = 0; i < region.Count; ++i)
        {
            if (ReferenceEquals(region[i].transform, transform))
            {
                x = (byte)regionX;
                y = (byte)regionY;
                index = (ushort)i;
                return true;
            }
        }

        index = ushort.MaxValue;
        return false;
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
    public static bool TryGetObjectOrBuildable(Transform transform, out LevelObject? @object, out LevelBuildableObject? buildable)
    {
        @object = null;
        buildable = null;
        if (TryFindObject(transform, out RegionIdentifier id))
        {
            @object = ObjectManager.getObject(id.X, id.Y, id.Index);
        }
        if (@object == null)
        {
            if (TryFindBuildable(transform, out id))
                buildable = GetBuildable(id);
        }

        return @object != null || buildable != null;
    }
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