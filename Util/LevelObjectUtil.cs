using System.Reflection;
using DevkitServer.API.Permissions;
using DevkitServer.Core.Permissions;
using DevkitServer.Multiplayer;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Players.UI;
#if SERVER
using DevkitServer.Players;
#endif
using JetBrains.Annotations;
using SDG.Framework.Devkit;

namespace DevkitServer.Util;
public static class LevelObjectUtil
{
    private const string Source = "LEVEL OBJECTS";

#if CLIENT
    private static readonly StaticSetter<uint>? SetNextInstanceId = Accessor.GenerateStaticSetter<LevelObjects, uint>("availableInstanceID");
    private static readonly StaticGetter<uint>? GetNextInstanceId = Accessor.GenerateStaticGetter<LevelObjects, uint>("availableInstanceID");
#endif

    [UsedImplicitly]
    private static readonly NetCall<Guid, Vector3, Quaternion, Vector3> SendRequestInstantiation = new NetCall<Guid, Vector3, Quaternion, Vector3>(NetCalls.RequestLevelObjectInstantiation);

    [UsedImplicitly]
    private static readonly NetCall<Guid, uint, Vector3, Quaternion, Vector3, ulong> SendObjectInstantiation = new NetCall<Guid, uint, Vector3, Quaternion, Vector3, ulong>(NetCalls.SendLevelObjectInstantiation);

    [UsedImplicitly]
    private static readonly NetCall<Guid, Vector3, Quaternion, Vector3, ulong> SendBuildableInstantiation = new NetCall<Guid, Vector3, Quaternion, Vector3, ulong>(NetCalls.SendLevelBuildableObjectInstantiation);


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
    public static StandardErrorCode ReceiveBuildableInstantiation(MessageContext ctx, Guid asset, Vector3 position, Quaternion rotation, Vector3 scale, ulong owner)
    {
        if (Assets.find(asset) is not ItemAsset buildableAsset || buildableAsset is not ItemBarricadeAsset and not ItemStructureAsset)
        {
            Logger.LogError($"Asset not found for incoming buildable: {asset.Format()}.", method: Source);
            return StandardErrorCode.InvalidData;
        }

        try
        {
            Transform? newBuildable = LevelObjects.addBuildable(position, rotation, buildableAsset.id);
            if (newBuildable == null)
                return StandardErrorCode.GenericError;

            InitializeBuildable(newBuildable, out LevelBuildableObject? buildable);
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

        if (!GetObjectOrBuildableAsset(asset, out ObjectAsset? @object, out ItemAsset? buildable))
        {
            Logger.LogError("Unable to get user from level object instantiation request.", method: Source);
            UIMessage.SendEditorMessage(user, DevkitServerModule.MessageLocalization.Translate("Error", asset.ToString("N") + " Invalid Asset"));
            return;
        }

        string displayName = @object != null ? @object.objectName : buildable!.itemName;
        LevelObject? newObject = null;
        LevelBuildableObject? newBuildable = null;
        try
        {
            Transform lvlObject = LevelObjects.registerAddObject(position, rotation, scale, @object, buildable);

            if (@object != null)
                InitializeLevelObject(lvlObject, out newObject);
            else
                InitializeBuildable(lvlObject, out newBuildable);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error instantiating {displayName.Format(false)}.", method: Source);
            Logger.LogError(ex, method: Source);
            UIMessage.SendEditorMessage(user, DevkitServerModule.MessageLocalization.Translate("Error", ex.Message));
            return;
        }

        if (newObject != null)
        {
            Transform? transform = newObject.transform;
            if (transform != null)
            {
                position = transform.position;
                rotation = transform.rotation;
                scale = transform.localScale;
            }

            LevelObjectResponsibilities.Set(newObject.instanceID, user.SteamId.m_SteamID);

            PooledTransportConnectionList list;
            if (ctx.IsRequest)
            {
                ctx.ReplyLayered(SendObjectInstantiation, newObject.GUID, newObject.instanceID, position, rotation, scale, user.SteamId.m_SteamID);
                list = DevkitServerUtility.GetAllConnections(ctx.Connection);
            }
            else list = DevkitServerUtility.GetAllConnections();
            SendObjectInstantiation.Invoke(list, newObject.GUID, newObject.instanceID, position, rotation, scale, user.SteamId.m_SteamID);
            Logger.LogDebug($"[{Source}] Granted request for instantiation of {displayName.Format(false)} {newObject.GUID.Format()}, instance ID: {newObject.instanceID.Format()} from {user.SteamId.Format()}.");
        }
        else if (newBuildable != null)
        {
            Transform? transform = newBuildable.transform;
            if (transform != null)
            {
                position = transform.position;
                rotation = transform.rotation;
                scale = transform.localScale;
            }

            PooledTransportConnectionList list;
            if (ctx.IsRequest)
            {
                ctx.ReplyLayered(SendBuildableInstantiation, newBuildable.asset.GUID, position, rotation, scale, user.SteamId.m_SteamID);
                list = DevkitServerUtility.GetAllConnections(ctx.Connection);
            }
            else list = DevkitServerUtility.GetAllConnections();
            SendBuildableInstantiation.Invoke(list, newBuildable.asset.GUID, position, rotation, scale, user.SteamId.m_SteamID);
            Logger.LogDebug($"[{Source}] Granted request for instantiation of buildable {displayName.Format(false)} {newObject.GUID.Format()} from {user.SteamId.Format()}.");
        }
        else
        {
            Logger.LogError($"Failed to create {displayName.Format(false)} {asset.Format()}.", method: Source);
            UIMessage.SendEditorMessage(user, DevkitServerModule.MessageLocalization.Translate("UnknownError"));
        }
    }
#endif
    private static void InitializeLevelObject(Transform transform, out LevelObject? @object)
    {
        if (TryFindObject(transform, out byte x, out byte y, out ushort index))
        {
            @object = ObjectManager.getObject(x, y, index);

            if (GetRegularObjectNetIdImpl == null)
                return;

            NetId netId = GetRegularObjectNetId(x, y, index);
            if (netId.IsNull())
                return;

            NetIdRegistry.AssignTransform(netId, transform);
            Logger.LogDebug($"[{Source}] Assigned NetId: {netId.Format()}.");
        }
        else
            @object = null;
    }
    private static void InitializeBuildable(Transform transform, out LevelBuildableObject? buildable)
    {
        if (TryFindBuildable(transform, out byte x, out byte y, out ushort index))
            buildable = GetBuildable(x, y, index);
        else buildable = null;
    }
    public static bool GetObjectOrBuildableAsset(Guid guid, out ObjectAsset? @object, out ItemAsset? buildable)
    {
        Asset asset = Assets.find(guid);
        @object = asset as ObjectAsset;
        buildable = asset as ItemAsset;
        return @object != null || buildable is ItemStructureAsset or ItemBarricadeAsset;
    }
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
    public static bool TryFindObject(Transform transform, out byte x, out byte y, out ushort index)
    {
        ThreadUtil.assertIsGameThread();
        if (transform != null)
        {
            bool r = false;
            if (Regions.tryGetCoordinate(transform.position, out x, out y))
            {
                if (SearchInRegion(transform, x, y, ref x, ref y, out index))
                    return true;
                if (SearchInRegion(transform, x + 1, y, ref x, ref y, out index))
                    return true;
                if (SearchInRegion(transform, x, y - 1, ref x, ref y, out index))
                    return true;
                if (SearchInRegion(transform, x - 1, y, ref x, ref y, out index))
                    return true;
                if (SearchInRegion(transform, x, y + 1, ref x, ref y, out index))
                    return true;
                if (SearchInRegion(transform, x + 1, y + 1, ref x, ref y, out index))
                    return true;
                if (SearchInRegion(transform, x + 1, y - 1, ref x, ref y, out index))
                    return true;
                if (SearchInRegion(transform, x - 1, y - 1, ref x, ref y, out index))
                    return true;
                if (SearchInRegion(transform, x - 1, y + 1, ref x, ref y, out index))
                    return true;

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
                        {
                            x = (byte)x2;
                            y = (byte)y2;
                            index = (ushort)i;
                            return true;
                        }
                    }
                }
            }
        }

        x = byte.MaxValue;
        y = byte.MaxValue;
        index = ushort.MaxValue;
        return false;
    }
    public static LevelObject? FindObject(uint instanceId)
    {
        if (TryFindObjectCoordinates(instanceId, out byte x, out byte y, out ushort index))
            return LevelObjects.objects[x, y][index];

        return null;
    }
    public static LevelObject? FindObject(Vector3 pos, uint instanceId)
    {
        if (TryFindObjectCoordinates(pos, instanceId, out byte x, out byte y, out ushort index))
            return LevelObjects.objects[x, y][index];

        return null;
    }

    public static bool TryFindObjectCoordinates(uint instanceId, out byte x, out byte y, out ushort index)
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
                        x = (byte)x2;
                        y = (byte)y2;
                        index = (ushort)i;
                        return true;
                    }
                }
            }
        }

        x = byte.MaxValue;
        y = byte.MaxValue;
        index = ushort.MaxValue;
        return false;
    }

    public static bool TryFindObjectCoordinates(Vector3 expectedPosition, uint instanceId, out byte x, out byte y, out ushort index)
    {
        ThreadUtil.assertIsGameThread();

        bool r = false;
        if (Regions.tryGetCoordinate(expectedPosition, out x, out y))
        {
            if (SearchInRegion(x, y, instanceId, ref x, ref y, out index))
                return true;
            if (SearchInRegion(x + 1, y, instanceId, ref x, ref y, out index))
                return true;
            if (SearchInRegion(x, y - 1, instanceId, ref x, ref y, out index))
                return true;
            if (SearchInRegion(x - 1, y, instanceId, ref x, ref y, out index))
                return true;
            if (SearchInRegion(x, y + 1, instanceId, ref x, ref y, out index))
                return true;
            if (SearchInRegion(x + 1, y + 1, instanceId, ref x, ref y, out index))
                return true;
            if (SearchInRegion(x + 1, y - 1, instanceId, ref x, ref y, out index))
                return true;
            if (SearchInRegion(x - 1, y - 1, instanceId, ref x, ref y, out index))
                return true;
            if (SearchInRegion(x - 1, y + 1, instanceId, ref x, ref y, out index))
                return true;

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
                        x = (byte)x2;
                        y = (byte)y2;
                        index = (ushort)i;
                        return true;
                    }
                }
            }
        }

        x = byte.MaxValue;
        y = byte.MaxValue;
        index = ushort.MaxValue;
        return false;
    }
    public static bool SearchInRegion(int regionX, int regionY, uint instanceId, ref byte x, ref byte y, out ushort index)
    {
        if (regionX is < 0 or > byte.MaxValue || regionY is < 0 or > byte.MaxValue)
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
    public static LevelObject? SearchInRegion(Transform transform, int regionX, int regionY)
    {
        List<LevelObject> region = LevelObjects.objects[regionX, regionY];
        for (int i = 0; i < region.Count; ++i)
        {
            if (ReferenceEquals(region[i].transform, transform))
                return region[i];
        }

        return null;
    }
    public static bool SearchInRegion(Transform transform, int regionX, int regionY, ref byte x, ref byte y, out ushort index)
    {
        List<LevelObject> region = LevelObjects.objects[regionX, regionY];
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
    public static bool SearchInRegion(byte regionX, byte regionY, uint instanceId, out ushort index)
    {
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
    public static bool TryFindBuildable(Transform transform, out byte x, out byte y, out ushort index)
    {
        ThreadUtil.assertIsGameThread();
        if (transform != null)
        {
            bool r = false;
            if (Regions.tryGetCoordinate(transform.position, out x, out y))
            {
                if (SearchInBuildableRegion(transform, x, y, ref x, ref y, out index))
                    return true;
                if (SearchInBuildableRegion(transform, x + 1, y, ref x, ref y, out index))
                    return true;
                if (SearchInBuildableRegion(transform, x, y - 1, ref x, ref y, out index))
                    return true;
                if (SearchInBuildableRegion(transform, x - 1, y, ref x, ref y, out index))
                    return true;
                if (SearchInBuildableRegion(transform, x, y + 1, ref x, ref y, out index))
                    return true;
                if (SearchInBuildableRegion(transform, x + 1, y + 1, ref x, ref y, out index))
                    return true;
                if (SearchInBuildableRegion(transform, x + 1, y - 1, ref x, ref y, out index))
                    return true;
                if (SearchInBuildableRegion(transform, x - 1, y - 1, ref x, ref y, out index))
                    return true;
                if (SearchInBuildableRegion(transform, x - 1, y + 1, ref x, ref y, out index))
                    return true;

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
                            x = (byte)x2;
                            y = (byte)y2;
                            index = (ushort)i;
                            return true;
                        }
                    }
                }
            }
        }

        x = byte.MaxValue;
        y = byte.MaxValue;
        index = ushort.MaxValue;
        return false;
    }
    public static LevelBuildableObject? SearchInBuildableRegion(Transform transform, int regionX, int regionY)
    {
        List<LevelBuildableObject> region = LevelObjects.buildables[regionX, regionY];
        for (int i = 0; i < region.Count; ++i)
        {
            if (ReferenceEquals(region[i].transform, transform))
                return region[i];
        }

        return null;
    }
    public static bool SearchInBuildableRegion(Transform transform, int regionX, int regionY, ref byte x, ref byte y, out ushort index)
    {
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
    public static bool CheckMovePermission(uint instanceId, ulong user)
    {
        return VanillaPermissions.EditObjects.Has(user, true) ||
               VanillaPermissions.MoveSavedObjects.Has(user, false) ||
               VanillaPermissions.PlaceObjects.Has(user, false) &&
               LevelObjectResponsibilities.IsPlacer(instanceId, user);
    }
    public static bool CheckPlacePermission(ulong user)
    {
        return VanillaPermissions.EditObjects.Has(user, true) ||
               VanillaPermissions.PlaceObjects.Has(user, false);
    }
    public static bool CheckDeletePermission(uint instanceId, ulong user)
    {
        return VanillaPermissions.EditObjects.Has(user, true) ||
               VanillaPermissions.RemoveSavedObjects.Has(user, false) ||
               VanillaPermissions.PlaceObjects.Has(user, false) && HierarchyResponsibilities.IsPlacer(instanceId, user);
    }
#elif CLIENT
    public static bool CheckMovePermission(uint instanceId)
    {
        return VanillaPermissions.EditObjects.Has(true) ||
               VanillaPermissions.MoveSavedObjects.Has(false) ||
               VanillaPermissions.PlaceObjects.Has(false) &&
               LevelObjectResponsibilities.IsPlacer(instanceId);
    }
    public static bool CheckPlacePermission()
    {
        return VanillaPermissions.EditObjects.Has(true) ||
               VanillaPermissions.PlaceObjects.Has(false);
    }
    public static bool CheckDeletePermission(uint instanceId)
    {
        return VanillaPermissions.EditObjects.Has(true) ||
               VanillaPermissions.RemoveSavedObjects.Has(false) ||
               VanillaPermissions.PlaceObjects.Has(false) && HierarchyResponsibilities.IsPlacer(instanceId);
    }
#endif
    public static NetId GetTreeNetId(byte x, byte y, ushort index) => GetTreeNetIdImpl == null ? NetId.INVALID : GetTreeNetIdImpl(x, y, index);
    public static NetId GetRegularObjectNetId(byte x, byte y, ushort index) => GetRegularObjectNetIdImpl == null ? NetId.INVALID : GetRegularObjectNetIdImpl(x, y, index);

    [Obsolete("Devkit Objects are no longer used.")]
    public static NetId GetDevkitObjectNetId(uint instanceId) => GetDevkitObjectNetIdImpl == null ? NetId.INVALID : GetDevkitObjectNetIdImpl(instanceId);
    public static LevelBuildableObject? GetBuildable(byte x, byte y, ushort index)
    {
        if (!Regions.checkSafe(x, y))
            return null;
        List<LevelBuildableObject> buildables = LevelObjects.buildables[x, y];
        return buildables.Count > index ? buildables[index] : null;
    }
}
