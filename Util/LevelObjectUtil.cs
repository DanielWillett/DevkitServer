using DevkitServer.API.Permissions;
using DevkitServer.Core.Permissions;
using DevkitServer.Models;
using DevkitServer.Multiplayer;
using DevkitServer.Multiplayer.Networking;
using JetBrains.Annotations;
using System.Reflection;
using DevkitServer.Players.UI;
#if CLIENT
using DevkitServer.Patches;
#endif
#if SERVER
using DevkitServer.Players;
#endif

namespace DevkitServer.Util;
public static class LevelObjectUtil
{
    private const string Source = "LEVEL OBJECTS";

    public static readonly int MaxSelectionSize = 64;
    public static readonly int MaxMoveSelectionSize = 32;
    public static readonly int MaxCopySelectionSize = 64;

#if CLIENT
    private static readonly StaticSetter<uint>? SetNextInstanceId = Accessor.GenerateStaticSetter<LevelObjects, uint>("availableInstanceID");
    private static readonly StaticGetter<uint>? GetNextInstanceId = Accessor.GenerateStaticGetter<LevelObjects, uint>("availableInstanceID");
#endif

    [UsedImplicitly]
    private static readonly NetCall<Guid, Vector3, Quaternion, Vector3> SendRequestInstantiation = new NetCall<Guid, Vector3, Quaternion, Vector3>(NetCalls.RequestLevelObjectInstantiation);

    [UsedImplicitly]
    private static readonly NetCall<Guid, uint, Vector3, Quaternion, Vector3, ulong> SendObjectInstantiation = new NetCall<Guid, uint, Vector3, Quaternion, Vector3, ulong>(NetCalls.SendLevelObjectInstantiation);

    [UsedImplicitly]
    private static readonly NetCall<Guid, int, Vector3, Quaternion, Vector3, ulong> SendBuildableInstantiation = new NetCall<Guid, int, Vector3, Quaternion, Vector3, ulong>(NetCalls.SendLevelBuildableObjectInstantiation);


    private static readonly Func<byte, byte, ushort, NetId>? GetTreeNetIdImpl =
        Accessor.GenerateStaticCaller<Func<byte, byte, ushort, NetId>>(Accessor.AssemblyCSharp
            .GetType("SDG.Unturned.LevelNetIdRegistry", false, false)?
            .GetMethod("GetTreeNetId", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!);

    private static readonly Func<byte, byte, ushort, NetId>? GetRegularObjectNetIdImpl =
        Accessor.GenerateStaticCaller<Func<byte, byte, ushort, NetId>>(Accessor.AssemblyCSharp
            .GetType("SDG.Unturned.LevelNetIdRegistry", false, false)?
            .GetMethod("GetRegularObjectNetId", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!);

    private static readonly Func<uint, NetId>? GetDevkitObjectNetIdImpl =
        Accessor.GenerateStaticCaller<Func<uint, NetId>>(Accessor.AssemblyCSharp
            .GetType("SDG.Unturned.LevelNetIdRegistry", false, false)?
            .GetMethod("GetDevkitObjectNetId", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!);

#if CLIENT
    public static void RequestInstantiation(Guid asset, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        SendRequestInstantiation.Invoke(asset, position, rotation, scale);
    }
    // todo queue in TemporaryActions if joining
    [NetCall(NetCallSource.FromServer, NetCalls.SendLevelObjectInstantiation)]
    public static StandardErrorCode ReceiveInstantiation(MessageContext ctx, Guid asset, uint instanceId, Vector3 position, Quaternion rotation, Vector3 scale, ulong owner)
    {
        if (Assets.find(asset) is not ObjectAsset objAsset)
        {
            Logger.LogError($"Asset not found for incoming object: {asset.Format()}.", method: Source);
            if (SetNextInstanceId != null && GetNextInstanceId != null)
            {
                // make sure nothing will take this instance ID.
                if (GetNextInstanceId() <= instanceId)
                    SetNextInstanceId(instanceId + 1);
            }

            return StandardErrorCode.InvalidData;
        }

        uint revertInstanceId = uint.MaxValue;
        if (SetNextInstanceId != null && GetNextInstanceId != null)
        {
            uint nextInstanceId = GetNextInstanceId();
            if (nextInstanceId < instanceId)
                SetNextInstanceId(instanceId);
            else if (nextInstanceId > instanceId)
            {
                if (nextInstanceId != instanceId + 1)
                    revertInstanceId = nextInstanceId;
                SetNextInstanceId(instanceId);
                LevelObject? existing = FindObject(position, instanceId);
                if (existing != null)
                {
                    LevelObjects.removeObject(existing.transform);
                    Logger.LogWarning($"Instance ID taken by {existing.asset.objectName.Format()}: {instanceId.Format()}, replacing existing object.", method: Source);
                }
            }
        }

        LevelObject? lvlObject;
        try
        {
            Transform newObject = LevelObjects.registerAddObject(position, rotation, scale, objAsset, null);
            if (newObject == null)
                return StandardErrorCode.GenericError;
            
            InitializeLevelObject(newObject, out lvlObject);
            if (lvlObject == null)
                return StandardErrorCode.GenericError;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to initialize object: {objAsset.objectName.Format()} {asset.Format()}.", method: Source);
            Logger.LogError(ex, method: Source);
            return StandardErrorCode.GenericError;
        }
        finally
        {
            if (revertInstanceId != uint.MaxValue)
                SetNextInstanceId!(revertInstanceId);
        }

        if (owner == Provider.client.m_SteamID)
            LevelObjectResponsibilities.Set(lvlObject.instanceID);

        return StandardErrorCode.Success;
    }
    [NetCall(NetCallSource.FromServer, NetCalls.SendLevelBuildableObjectInstantiation)]
    public static StandardErrorCode ReceiveBuildableInstantiation(MessageContext ctx, Guid asset, int regionIdentifier, Vector3 position, Quaternion rotation, Vector3 scale, ulong owner)
    {
        if (Assets.find(asset) is not ItemAsset buildableAsset || buildableAsset is not ItemBarricadeAsset and not ItemStructureAsset)
        {
            Logger.LogError($"Asset not found for incoming buildable: {asset.Format()}.", method: Source);
            return StandardErrorCode.InvalidData;
        }

        RegionIdentifier expectedId = GetIdentifier(regionIdentifier);

        try
        {
            Transform? newBuildable = LevelObjects.addBuildable(position, rotation, buildableAsset.id);
            if (newBuildable == null)
                return StandardErrorCode.GenericError;

            InitializeBuildable(newBuildable, out LevelBuildableObject? buildable, out RegionIdentifier id);
            if (buildable != null && expectedId != id)
            {
                if (expectedId.IsSameRegionAs(id))
                {
                    List<LevelBuildableObject> buildables = LevelObjects.buildables[id.X, id.Y];
                    LevelBuildableObject newObj = buildables[id.Index];
                    buildables.RemoveAt(id.Index);
                    Logger.LogWarning($"Inconsistant region index, expected: # {expectedId.Index.Format()}, actual: # {id.Index.Format()}.", method: Source);
                    if (id.Index < expectedId.Index)
                    {
                        Logger.LogWarning($"Index not occupied: {expectedId.Index.Format()}.", method: Source);
                        Vector3 pos = new Vector3(0f, -1024f, 0f);
                        for (int i = id.Index; i < expectedId.Index; ++i)
                        {
                            Logger.LogWarning($" Adding filler buildable @ {pos.Format()}.", method: Source);
                            LevelObjects.addBuildable(pos, Quaternion.identity, 0);
                        }

                        if (buildables.Count != expectedId.Index)
                            Logger.LogError($" Unable to fill to index: {expectedId.Index.Format()}.", method: Source);
                        
                        buildables.Add(newObj);
                    }
                    else
                    {
                        LevelObjects.removeBuildable(GetBuildable(expectedId)!.transform);
                        LevelBuildableObject oldObj = buildables[expectedId.Index];
                        buildables[expectedId.Index] = newObj;
                        Logger.LogWarning($"Index taken by {oldObj.asset.itemName.Format()}: {expectedId.Index.Format()}, replacing existing object.", method: Source);
                        buildables.Add(oldObj);
                        RegionIdentifier oldId = new RegionIdentifier(expectedId.X, expectedId.Y, (ushort)(buildables.Count - 1));
                        BuildableResponsibilities.Set(oldId, BuildableResponsibilities.IsPlacer(oldId));
                    }
                }
                else
                {
                    List<LevelBuildableObject> buildables = LevelObjects.buildables[id.X, id.Y];
                    LevelBuildableObject newObj = buildables[id.Index];
                    buildables.RemoveAt(id.Index);
                    buildables = LevelObjects.buildables[expectedId.X, expectedId.Y];
                    Logger.LogWarning($"Inconsistant region identifier, expected: {expectedId.Format()}, actual: {id.Format()}.", method: Source);
                    if (buildables.Count == expectedId.Index)
                    {
                        buildables.Add(newObj);
                        Logger.LogInfo($"[{Source}]  Moved to new region successfully");
                    }
                    else if (id.Index < expectedId.Index)
                    {
                        Logger.LogWarning($" Index not occupied: {expectedId.Index.Format()}.", method: Source);
                        Vector3 pos = new Vector3(0f, -1024f, 0f);
                        for (int i = id.Index; i < expectedId.Index; ++i)
                        {
                            Logger.LogWarning($"  Adding filler buildable @ {pos.Format()}.", method: Source);
                            LevelObjects.addBuildable(pos, Quaternion.identity, 0);
                        }

                        if (buildables.Count != expectedId.Index)
                            Logger.LogError($"  Unable to fill to index: {expectedId.Index.Format()}.", method: Source);

                        buildables.Add(newObj);
                    }
                    else
                    {
                        LevelObjects.removeBuildable(GetBuildable(expectedId)!.transform);
                        LevelBuildableObject oldObj = buildables[expectedId.Index];
                        buildables[expectedId.Index] = newObj;
                        Logger.LogWarning($" Index taken by {oldObj.asset.itemName.Format()}: {expectedId.Index.Format()}, replacing existing object.", method: Source);
                        buildables.Add(oldObj);
                        RegionIdentifier oldId = new RegionIdentifier(expectedId.X, expectedId.Y, (ushort)(buildables.Count - 1));
                        BuildableResponsibilities.Set(oldId, BuildableResponsibilities.IsPlacer(oldId));
                    }
                }
            }

            if (owner == Provider.client.m_SteamID)
                BuildableResponsibilities.Set(expectedId, true);
            return buildable != null ? StandardErrorCode.Success : StandardErrorCode.GenericError;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to initialize buildable: {buildableAsset.itemName.Format()} {asset.Format()}.", method: Source);
            Logger.LogError(ex, method: Source);
            return StandardErrorCode.GenericError;
        }
    }
#elif SERVER
    [NetCall(NetCallSource.FromClient, NetCalls.RequestLevelObjectInstantiation)]
    public static void ReceiveLevelObjectInstantiation(MessageContext ctx, Guid asset, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        EditorUser? user = ctx.GetCaller();
        if (user == null || !user.IsOnline)
        {
            Logger.LogError("Unable to get user from level object instantiation request.", method: Source);
            return;
        }

        if (!GetObjectOrBuildableAsset(asset, out ObjectAsset? objectAsset, out ItemAsset? buildableAsset))
        {
            Logger.LogError($"Unable to get asset for level object instantiation request from {user.Format()}.", method: Source);
            UIMessage.SendEditorMessage(user, DevkitServerModule.MessageLocalization.Translate("Error", asset.ToString("N") + " Invalid Asset"));
            return;
        }

        string displayName = objectAsset != null ? objectAsset.objectName : buildableAsset!.itemName;
        LevelObject? levelObject = null;
        LevelBuildableObject? buildable = null;
        RegionIdentifier id = RegionIdentifier.Invalid;
        try
        {
            Transform transform = LevelObjects.registerAddObject(position, rotation, scale, objectAsset, buildableAsset);
            if (transform == null)
            {
                Logger.LogError($"Failed to create object: {(objectAsset ?? (Asset?)buildableAsset).Format()}, registerAddObject returned {((object?)null).Format()}.");
                return;
            }
            if (objectAsset != null)
                InitializeLevelObject(transform, out levelObject);
            else
                InitializeBuildable(transform, out buildable, out id);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error instantiating {displayName.Format(false)}.", method: Source);
            Logger.LogError(ex, method: Source);
            UIMessage.SendEditorMessage(user, DevkitServerModule.MessageLocalization.Translate("Error", ex.Message));
            return;
        }

        if (levelObject != null)
        {
            Transform? transform = levelObject.transform;
            if (transform != null)
            {
                position = transform.position;
                rotation = transform.rotation;
                scale = transform.localScale;
            }

            LevelObjectResponsibilities.Set(levelObject.instanceID, user.SteamId.m_SteamID);

            PooledTransportConnectionList list;
            if (ctx.IsRequest)
            {
                ctx.ReplyLayered(SendObjectInstantiation, levelObject.GUID, levelObject.instanceID, position, rotation, scale, user.SteamId.m_SteamID);
                list = DevkitServerUtility.GetAllConnections(ctx.Connection);
            }
            else list = DevkitServerUtility.GetAllConnections();
            SendObjectInstantiation.Invoke(list, levelObject.GUID, levelObject.instanceID, position, rotation, scale, user.SteamId.m_SteamID);
            Logger.LogDebug($"[{Source}] Granted request for instantiation of {displayName.Format(false)} {levelObject.GUID.Format()}, instance ID: {levelObject.instanceID.Format()} from {user.SteamId.Format()}.");
        }
        else if (buildable != null)
        {
            Transform? transform = buildable.transform;
            if (transform != null)
            {
                position = transform.position;
                rotation = transform.rotation;
                scale = transform.localScale;
            }

            PooledTransportConnectionList list;
            int regionId;
            unsafe
            {
                regionId = *(int*)&id;
            }
            if (ctx.IsRequest)
            {
                ctx.ReplyLayered(SendBuildableInstantiation, buildable.asset.GUID, regionId, position, rotation, scale, user.SteamId.m_SteamID);
                list = DevkitServerUtility.GetAllConnections(ctx.Connection);
            }
            else list = DevkitServerUtility.GetAllConnections();
            SendBuildableInstantiation.Invoke(list, buildable.asset.GUID, regionId, position, rotation, scale, user.SteamId.m_SteamID);
            Logger.LogDebug($"[{Source}] Granted request for instantiation of buildable {displayName.Format(false)} {buildable.asset.GUID.Format()} from {user.SteamId.Format()}.");
        }
        else
        {
            Logger.LogError($"Failed to create {displayName.Format(false)} {asset.Format()}.", method: Source);
            UIMessage.SendEditorMessage(user, DevkitServerModule.MessageLocalization.Translate("UnknownError"));
        }
    }
#endif
    private static void InitializeLevelObject(Transform transform, out LevelObject? levelObject)
    {
        if (TryFindObject(transform, out RegionIdentifier id) &&
            (levelObject = ObjectManager.getObject(id.X, id.Y, id.Index)) != null)
        {
            if (GetRegularObjectNetIdImpl == null)
                return;

            NetId netId = GetRegularObjectNetId(id);
            if (netId.IsNull())
                return;

            NetIdRegistry.ReleaseTransform(NetId.INVALID, transform);
            NetIdRegistry.AssignTransform(netId, transform);
            Logger.LogDebug($"[{Source}] Assigned NetId: {netId.Format()}.");
            return;
        }

        levelObject = null;
        Logger.LogWarning($"Did not find object of transform {transform.name.Format()}.", method: Source);
    }
    private static void InitializeBuildable(Transform transform, out LevelBuildableObject? buildable, out RegionIdentifier id)
    {
        if (TryFindBuildable(transform, out id))
        {
            if ((buildable = GetBuildable(id)) != null) return;
        }

        buildable = null;
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
    public static LevelObject? FindObject(Transform transform)
    {
        ThreadUtil.assertIsGameThread();
        if (transform == null)
            return null;

        bool r = false;
        if (Regions.tryGetCoordinate(transform.position, out byte x, out byte y))
        {
            LevelObject? obj =
                SearchInRegion(transform, x, y) ??
                SearchInRegion(transform, x + 1, y) ??
                SearchInRegion(transform, x - 1, y) ??
                SearchInRegion(transform, x, y + 1) ??
                SearchInRegion(transform, x + 1, y + 1) ??
                SearchInRegion(transform, x + 1, y - 1) ??
                SearchInRegion(transform, x - 1, y - 1) ??
                SearchInRegion(transform, x - 1, y + 1);

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
                List<LevelObject> region = LevelObjects.objects[x2, y2];
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
    public static bool TryFindObject(Transform transform, out RegionIdentifier id)
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
                    if (SearchInRegion(transform, x + offsets[i], y + offsets[i + 1], ref x, ref y, out ushort index))
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
                    if (SearchInRegion(transform, x2, y2, ref x, ref y, out ushort index))
                    {
                        id = new RegionIdentifier((byte)x2, (byte)y2, index);
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
    private static LevelObject? SearchInRegion(Transform transform, int regionX, int regionY)
    {
        if (regionX < 0 || regionY < 0 || regionX > Regions.WORLD_SIZE || regionY > Regions.WORLD_SIZE)
            return null;
        List<LevelObject> region = LevelObjects.objects[regionX, regionY];
        for (int i = 0; i < region.Count; ++i)
        {
            if (ReferenceEquals(region[i].transform, transform))
                return region[i];
        }
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

        return null;
    }
    private static bool SearchInRegion(Transform transform, int regionX, int regionY, ref byte x, ref byte y, out ushort index)
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
            if (region[i].transform == transform)
            {
                x = (byte)regionX;
                y = (byte)regionY;
                index = (ushort)i;
                return true;
            }
        }
        for (int i = 0; i < c; ++i)
        {
            if (region[i].skybox == transform)
            {
                x = (byte)regionX;
                y = (byte)regionY;
                index = (ushort)i;
                return true;
            }
        }
        for (int i = 0; i < c; ++i)
        {
            if (region[i].placeholderTransform == transform)
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
    public static bool TryFindBuildable(Transform transform, out RegionIdentifier id)
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
               VanillaPermissions.MoveSavedObjects.Has(user, false) ||
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
               VanillaPermissions.RemoveSavedObjects.Has(user, false) ||
               VanillaPermissions.PlaceObjects.Has(user, false) && HierarchyResponsibilities.IsPlacer(instanceId, user);
    }
    [Pure]
    public static bool CheckMoveBuildablePermission(RegionIdentifier id, ulong user)
    {
        return VanillaPermissions.EditObjects.Has(user, true) ||
               VanillaPermissions.MoveSavedObjects.Has(user, false) ||
               VanillaPermissions.PlaceObjects.Has(user, false) &&
               BuildableResponsibilities.IsPlacer(id, user);
    }
    [Pure]
    public static bool CheckDeleteBuildablePermission(RegionIdentifier id, ulong user)
    {
        return VanillaPermissions.EditObjects.Has(user, true) ||
               VanillaPermissions.RemoveSavedObjects.Has(user, false) ||
               VanillaPermissions.PlaceObjects.Has(user, false) &&
               BuildableResponsibilities.IsPlacer(id, user);
    }
#elif CLIENT
    [Pure]
    public static bool CheckMovePermission(uint instanceId)
    {
        return VanillaPermissions.EditObjects.Has(true) ||
               VanillaPermissions.MoveSavedObjects.Has(false) ||
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
               VanillaPermissions.RemoveSavedObjects.Has(false) ||
               VanillaPermissions.PlaceObjects.Has(false) && HierarchyResponsibilities.IsPlacer(instanceId);
    }
    [Pure]
    public static bool CheckMoveBuildablePermission(RegionIdentifier id)
    {
        return VanillaPermissions.EditObjects.Has(true) ||
               VanillaPermissions.MoveSavedObjects.Has(false) ||
               VanillaPermissions.PlaceObjects.Has(false) &&
               BuildableResponsibilities.IsPlacer(id);
    }
    [Pure]
    public static bool CheckDeleteBuildablePermission(RegionIdentifier id)
    {
        return VanillaPermissions.EditObjects.Has(true) ||
               VanillaPermissions.RemoveSavedObjects.Has(false) ||
               VanillaPermissions.PlaceObjects.Has(false) &&
               BuildableResponsibilities.IsPlacer(id);
    }
#endif
    [Pure]
    public static NetId GetTreeNetId(RegionIdentifier id) => GetTreeNetIdImpl == null ? NetId.INVALID : GetTreeNetIdImpl(id.X, id.Y, id.Index);
    [Pure]
    public static NetId GetRegularObjectNetId(RegionIdentifier id) => GetRegularObjectNetIdImpl == null ? NetId.INVALID : GetRegularObjectNetIdImpl(id.X, id.Y, id.Index);

    [Pure]
    [Obsolete("Devkit Objects are no longer used.")]
    public static NetId GetDevkitObjectNetId(uint instanceId) => GetDevkitObjectNetIdImpl == null ? NetId.INVALID : GetDevkitObjectNetIdImpl(instanceId);
    [Pure]
    public static LevelBuildableObject? GetBuildable(RegionIdentifier id)
    {
        if (id.X >= Regions.WORLD_SIZE || id.Y >= Regions.WORLD_SIZE)
            return null;
        List<LevelBuildableObject> buildables = LevelObjects.buildables[id.X, id.Y];
        return buildables.Count > id.Index ? buildables[id.Index] : null;
    }
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
    internal static unsafe RegionIdentifier GetIdentifier(int input) => *(RegionIdentifier*)input;
#if CLIENT
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
            for (int i = 0; i < copies.Length && DevkitServerModule.IsEditing && Level.isEditor; ++i)
            {
                EditorCopy copy = copies[i];
                bool retry = false;
                doRetry:
                NetTask instantiateRequest;
                bool buildable = false;
                if (copy.objectAsset != null)
                {
                    instantiateRequest = SendRequestInstantiation.Request(SendObjectInstantiation, copy.objectAsset.GUID, copy.position, copy.rotation, copy.scale, 5000);
                    yield return instantiateRequest;
                }
                else if (copy.itemAsset != null)
                {
                    buildable = true;
                    instantiateRequest = SendRequestInstantiation.Request(SendBuildableInstantiation, copy.itemAsset.GUID, copy.position, copy.rotation, copy.scale, 5000);
                    yield return instantiateRequest;
                }
                else continue;
                if (!instantiateRequest.Parameters.Success)
                {
                    Logger.LogWarning($"Failed to instantiate {copy.GetAsset().Format()} at {copy.position.Format()} (#{i.Format()}). Retried yet: {retry.Format()}.");
                    if (!retry)
                    {
                        retry = true;
                        goto doRetry;
                    }
                    continue;
                }

                if (buildable)
                {
                    if (!instantiateRequest.Parameters.TryGetParameter(1, out int idData))
                        continue;
                    RegionIdentifier id = GetIdentifier(idData);
                    if (GetBuildable(id) is not { } placedBuildable)
                    {
                        Logger.LogWarning($"Failed to find instantiated buildable {copy.GetAsset().Format()} at {copy.position.Format()} (#{i.Format()}).");
                        continue;
                    }
                    EditorObjects.addSelection(placedBuildable.transform);
                }
                else
                {
                    if (!instantiateRequest.Parameters.TryGetParameter(1, out uint instanceId))
                        continue;
                    if (FindObject(instanceId) is not { } placedObject)
                    {
                        Logger.LogWarning($"Failed to find instantiated object {copy.GetAsset().Format()} at {copy.position.Format()} (#{i.Format()}).");
                        continue;
                    }
                    EditorObjects.addSelection(placedObject.transform);
                }
            }
        }
        finally
        {
            LevelObjectPatches.IsSyncing = false;
        }
    }
#endif
}
