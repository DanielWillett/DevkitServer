using DevkitServer.API.Abstractions;
using DevkitServer.API.Permissions;
using DevkitServer.Core.Permissions;
using DevkitServer.Multiplayer;
using DevkitServer.Multiplayer.Networking;
#if SERVER
using DevkitServer.Players;
using DevkitServer.Players.UI;
#endif
using JetBrains.Annotations;
using SDG.Framework.Devkit;

namespace DevkitServer.Util;
[EarlyTypeInit]
public static class HierarchyUtil
{
    private const string Source = "LEVEL HIERARCHY";
    
    internal static readonly List<IDevkitHierarchyItem> HierarchyItemBuffer = new List<IDevkitHierarchyItem>(64);

#if CLIENT
    private static readonly StaticSetter<uint>? SetAvailableInstanceId = Accessor.GenerateStaticSetter<LevelHierarchy, uint>("availableInstanceID");
    private static readonly StaticGetter<uint>? GetAvailableInstanceId = Accessor.GenerateStaticGetter<LevelHierarchy, uint>("availableInstanceID");
#endif

    [UsedImplicitly]
    private static readonly NetCallRaw<IHierarchyItemTypeIdentifier, Vector3, Quaternion, Vector3> SendRequestInstantiation =
        new NetCallRaw<IHierarchyItemTypeIdentifier, Vector3, Quaternion, Vector3>(NetCalls.RequestHierarchyInstantiation,
            HierarchyItemTypeIdentifierEx.ReadIdentifier!, null, null, null,
            HierarchyItemTypeIdentifierEx.WriteIdentifier, null, null, null);

    [UsedImplicitly]
    private static readonly NetCallRaw<IHierarchyItemTypeIdentifier, uint, Vector3, Quaternion, Vector3, ulong> SendInstantiation =
        new NetCallRaw<IHierarchyItemTypeIdentifier, uint, Vector3, Quaternion, Vector3, ulong>(NetCalls.SendHierarchyInstantiation,
            HierarchyItemTypeIdentifierEx.ReadIdentifier!, null, null, null, null, null,
            HierarchyItemTypeIdentifierEx.WriteIdentifier, null, null, null, null, null);
#if CLIENT
    public static void RequestInstantiation(IHierarchyItemTypeIdentifier type, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        SendRequestInstantiation.Invoke(type, position, rotation, scale);
    }
    // todo queue in TemporaryActions if joining
    [NetCall(NetCallSource.FromServer, NetCalls.SendHierarchyInstantiation)]
    public static StandardErrorCode ReceiveHierarchyInstantiation(MessageContext ctx, IHierarchyItemTypeIdentifier? type, uint instanceId, Vector3 position, Quaternion rotation, Vector3 scale, ulong owner)
    {
        if (type == null)
            return StandardErrorCode.InvalidData;
        uint lastInstanceId = uint.MaxValue;
        if (GetAvailableInstanceId != null && SetAvailableInstanceId != null)
        {
            int existing = FindItemIndex(instanceId);
            if (existing > -1)
            {
                IDevkitHierarchyItem existingItem = LevelHierarchy.instance.items[existing];
                Logger.LogWarning($"Received instantiation of {type.FormatToString()} with overlapping instance ID with {existingItem.Format()} ({instanceId.Format()}). " +
                                  "Conflict destroyed".Colorize(ConsoleColor.Red) + ".", method: Source);
                RemoveItem(existingItem);
            }

            lastInstanceId = GetAvailableInstanceId();
            if (lastInstanceId != instanceId)
            {
                SetAvailableInstanceId(lastInstanceId);
                Logger.LogDebug($"Instance ID mismatch, resolving. New: {instanceId.Format()}, Expected: {lastInstanceId.Format()}.");
            }
        }

        try
        {
            type.Instantiate(position, rotation, scale);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error instantiating {type.FormatToString()} at instance ID {instanceId.Format()}.", method: Source);
            Logger.LogError(ex, method: Source);
            return StandardErrorCode.GenericError;
        }
        finally
        {
            if (lastInstanceId != instanceId && lastInstanceId != uint.MaxValue)
                SetAvailableInstanceId!(lastInstanceId);
        }

        IDevkitHierarchyItem? newItem = LevelHierarchy.instance.items.Count > 0 ? LevelHierarchy.instance.items.GetTail() : null;
        if (newItem != null && type.Type.IsInstanceOfType(newItem))
        {
            if (newItem.instanceID != instanceId)
            {
                Logger.LogError($"Failed to assign correct instance ID: {instanceId.Format()}, to {type.FormatToString()}.", method: Source);
                RemoveItem(newItem);
            }
            else
            {
                if (owner == Provider.client.m_SteamID)
                    HierarchyResponsibilities.Set(newItem.instanceID);
                
                return StandardErrorCode.Success;
            }
        }
        else
            Logger.LogError($"Failed to create {type.FormatToString()}, instance ID: {instanceId.Format()}.", method: Source);


        return StandardErrorCode.GenericError;
    }
#elif SERVER
    [NetCall(NetCallSource.FromClient, NetCalls.RequestHierarchyInstantiation)]
    public static void ReceiveRequestHierarchyInstantiation(MessageContext ctx, IHierarchyItemTypeIdentifier? type, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        EditorUser? user = ctx.GetCaller();
        if (user == null || !user.IsOnline)
        {
            Logger.LogError("Unable to get user from hierarchy instantiation request.", method: Source);
            return;
        }
        if (type == null)
        {
            UIMessage.SendEditorMessage(user, DevkitServerModule.MessageLocalization.Translate("UnknownError"));
            return;
        }

        try
        {
            type.Instantiate(position, rotation, scale);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error instantiating {type.FormatToString()}.", method: Source);
            Logger.LogError(ex, method: Source);
            UIMessage.SendEditorMessage(user, DevkitServerModule.MessageLocalization.Translate("Error", ex.Message));
            return;
        }

        IDevkitHierarchyItem? newItem = LevelHierarchy.instance.items.Count > 0 ? LevelHierarchy.instance.items.GetTail() : null;
        if (newItem != null && type.Type.IsInstanceOfType(newItem))
        {
            if (newItem is Component comp)
            {
                position = comp.transform.position;
                rotation = comp.transform.rotation;
                scale = comp.transform.localScale;
            }

            HierarchyResponsibilities.Set(newItem.instanceID, user.SteamId.m_SteamID);

            PooledTransportConnectionList list;
            if (ctx.IsRequest)
            {
                ctx.ReplyLayered(SendInstantiation, type, newItem.instanceID, position, rotation, scale, user.SteamId.m_SteamID);
                list = DevkitServerUtility.GetAllConnections(ctx.Connection);
            }
            else list = DevkitServerUtility.GetAllConnections();
            SendInstantiation.Invoke(list, type, newItem.instanceID, position, rotation, scale, user.SteamId.m_SteamID);
            Logger.LogDebug($"[{Source}] Granted request for instantiation of {type.FormatToString()}, instance ID: {newItem.instanceID.Format()} ({newItem.Format()}) from {user.SteamId.Format()}.");
        }
        else
        {
            Logger.LogError($"Failed to create {type.FormatToString()}.", method: Source);
            UIMessage.SendEditorMessage(user, DevkitServerModule.MessageLocalization.Translate("UnknownError"));
        }
    }
#endif
    public static void RemoveItem(IDevkitHierarchyItem item)
    {
        if (item is Component o2)
            Object.Destroy(o2.gameObject);
        else if (item is Object o)
            Object.Destroy(o);
        else LevelHierarchy.removeItem(item);
    }
    [Pure]
    public static IDevkitHierarchyItem? FindItem(uint instanceId)
    {
        int ind = FindItemIndex(instanceId);
        return ind < 0 ? null : LevelHierarchy.instance.items[ind];
    }
    /// <returns>Inverse of binary search if not found, will always be &lt; 0 when not found.</returns>
    public static int FindItemIndex(uint instanceId)
    {
        ThreadUtil.assertIsGameThread();

        List<IDevkitHierarchyItem> items = LevelHierarchy.instance.items;
        int min = 0;
        int max = items.Count - 1;

        // binary search because it should be mostly in order.
        while (min <= max)
        {
            int index = min + (max - min >> 1);
            int comparison = items[index].instanceID.CompareTo(instanceId);
            if (comparison == 0)
                return index;
            if (comparison < 0)
                min = index + 1;
            else
                max = index - 1;
        }

        // then slow loop
        for (int i = 0; i < items.Count; ++i)
            if (instanceId == items[i].instanceID)
                return i;
        return ~min;
    }
#if SERVER
    [Pure]
    public static bool CheckMovePermission(IDevkitHierarchyItem item, ulong user)
    {
        if (Permission.SuperuserPermission.Has(user))
            return true;

        return item switch
        {
            TempNodeBase => VanillaPermissions.EditNodes.Has(user, false) ||
                            VanillaPermissions.MoveSavedNodes.Has(user, false) ||
                            VanillaPermissions.PlaceNodes.Has(user, false) && HierarchyResponsibilities.IsPlacer(item.instanceID, user),

            CartographyVolume => VanillaPermissions.EditCartographyVolumes.Has(user, false) ||
                                 VanillaPermissions.MoveSavedCartographyVolumes.Has(user, false) ||
                                 VanillaPermissions.PlaceCartographyVolumes.Has(user, false) && HierarchyResponsibilities.IsPlacer(item.instanceID, user),

            VolumeBase =>   VanillaPermissions.EditVolumes.Has(user, false) ||
                            VanillaPermissions.MoveSavedVolumes.Has(user, false) ||
                            VanillaPermissions.PlaceVolumes.Has(user, false) && HierarchyResponsibilities.IsPlacer(item.instanceID, user),

            _ => false
        };
    }
    [Pure]
    public static bool CheckPlacePermission(IHierarchyItemTypeIdentifier type, ulong user)
    {
        if (Permission.SuperuserPermission.Has(user))
            return true;

        return type switch
        {
            NodeItemTypeIdentifier => VanillaPermissions.EditNodes.Has(user, false) ||
                                      VanillaPermissions.PlaceNodes.Has(user, false),

            VolumeItemTypeIdentifier v => typeof(CartographyVolume).IsAssignableFrom(v.Type)
                ? (VanillaPermissions.EditCartographyVolumes.Has(user, false) ||
                   VanillaPermissions.PlaceCartographyVolumes.Has(user, false))
                : (VanillaPermissions.EditVolumes.Has(user, false) ||
                   VanillaPermissions.PlaceVolumes.Has(user, false)),

            _ => false
        };
    }
    [Pure]
    public static bool CheckDeletePermission(IDevkitHierarchyItem item, ulong user)
    {
        if (Permission.SuperuserPermission.Has(user))
            return true;

        return item switch
        {
            TempNodeBase => VanillaPermissions.EditNodes.Has(user, false) ||
                            VanillaPermissions.RemoveSavedNodes.Has(user, false) ||
                            VanillaPermissions.PlaceNodes.Has(user, false) && HierarchyResponsibilities.IsPlacer(item.instanceID, user),

            CartographyVolume => VanillaPermissions.EditCartographyVolumes.Has(user, false) ||
                                 VanillaPermissions.RemoveSavedCartographyVolumes.Has(user, false) ||
                                 VanillaPermissions.PlaceCartographyVolumes.Has(user, false) && HierarchyResponsibilities.IsPlacer(item.instanceID, user),

            VolumeBase => VanillaPermissions.EditVolumes.Has(user, false) ||
                          VanillaPermissions.RemoveSavedVolumes.Has(user, false) ||
                          VanillaPermissions.PlaceVolumes.Has(user, false) && HierarchyResponsibilities.IsPlacer(item.instanceID, user),

            _ => false
        };
    }
#elif CLIENT
    [Pure]
    public static bool CheckMovePermission(IDevkitHierarchyItem item)
    {
        if (Permission.SuperuserPermission.Has())
            return true;

        return item switch
        {
            TempNodeBase => VanillaPermissions.EditNodes.Has(false) ||
                            VanillaPermissions.MoveSavedNodes.Has(false) ||
                            VanillaPermissions.PlaceNodes.Has(false) && HierarchyResponsibilities.IsPlacer(item.instanceID),

            CartographyVolume => VanillaPermissions.EditCartographyVolumes.Has(false) ||
                                 VanillaPermissions.MoveSavedCartographyVolumes.Has(false) ||
                                 VanillaPermissions.PlaceCartographyVolumes.Has(false) && HierarchyResponsibilities.IsPlacer(item.instanceID),

            VolumeBase => VanillaPermissions.EditVolumes.Has(false) ||
                            VanillaPermissions.MoveSavedVolumes.Has(false) ||
                            VanillaPermissions.PlaceVolumes.Has(false) && HierarchyResponsibilities.IsPlacer(item.instanceID),

            _ => false
        };
    }
    [Pure]
    public static bool CheckPlacePermission(IHierarchyItemTypeIdentifier type)
    {
        if (Permission.SuperuserPermission.Has())
            return true;

        return type switch
        {
            NodeItemTypeIdentifier => VanillaPermissions.EditNodes.Has(false) ||
                                      VanillaPermissions.PlaceNodes.Has(false),

            VolumeItemTypeIdentifier v => typeof(CartographyVolume).IsAssignableFrom(v.Type)
                ? (VanillaPermissions.EditCartographyVolumes.Has(false) ||
                   VanillaPermissions.PlaceCartographyVolumes.Has(false))
                : (VanillaPermissions.EditVolumes.Has(false) ||
                   VanillaPermissions.PlaceVolumes.Has(false)),

            _ => false
        };
    }
    [Pure]
    public static Permission? GetPlacePermission(IHierarchyItemTypeIdentifier type)
    {
        return type switch
        {
            NodeItemTypeIdentifier => VanillaPermissions.PlaceNodes,

            VolumeItemTypeIdentifier v => typeof(CartographyVolume).IsAssignableFrom(v.Type)
                ? VanillaPermissions.PlaceCartographyVolumes
                : VanillaPermissions.PlaceVolumes,

            _ => null
        };
    }
    [Pure]
    public static bool CheckDeletePermission(IDevkitHierarchyItem item)
    {
        if (Permission.SuperuserPermission.Has())
            return true;

        return item switch
        {
            TempNodeBase => VanillaPermissions.EditNodes.Has(false) ||
                            VanillaPermissions.RemoveSavedNodes.Has(false) ||
                            VanillaPermissions.PlaceNodes.Has(false) && HierarchyResponsibilities.IsPlacer(item.instanceID),

            CartographyVolume => VanillaPermissions.EditCartographyVolumes.Has(false) ||
                                 VanillaPermissions.RemoveSavedCartographyVolumes.Has(false) ||
                                 VanillaPermissions.PlaceCartographyVolumes.Has(false) && HierarchyResponsibilities.IsPlacer(item.instanceID),

            VolumeBase => VanillaPermissions.EditVolumes.Has(false) ||
                          VanillaPermissions.RemoveSavedVolumes.Has(false) ||
                          VanillaPermissions.PlaceVolumes.Has(false) && HierarchyResponsibilities.IsPlacer(item.instanceID),

            _ => false
        };
    }
#endif
}

