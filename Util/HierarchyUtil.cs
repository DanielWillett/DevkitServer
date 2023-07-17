﻿using DevkitServer.API.Abstractions;
using DevkitServer.API.Permissions;
using DevkitServer.Core.Permissions;
using DevkitServer.Models;
using DevkitServer.Multiplayer;
using DevkitServer.Multiplayer.Levels;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Multiplayer.Sync;
#if SERVER
using DevkitServer.Players;
using DevkitServer.Players.UI;
#elif CLIENT
using DevkitServer.Multiplayer.Actions;
using DevkitServer.Players;
#endif
using SDG.Framework.Devkit;

namespace DevkitServer.Util;
[EarlyTypeInit]
public static class HierarchyUtil
{
    private const string Source = "LEVEL HIERARCHY";

    internal static readonly List<IDevkitHierarchyItem> HierarchyItemBuffer = new List<IDevkitHierarchyItem>(64);
    
    [UsedImplicitly]
    private static readonly NetCallRaw<IHierarchyItemTypeIdentifier, Vector3, Quaternion, Vector3> SendRequestInstantiation =
        new NetCallRaw<IHierarchyItemTypeIdentifier, Vector3, Quaternion, Vector3>(NetCalls.RequestHierarchyInstantiation,
            HierarchyItemTypeIdentifierEx.ReadIdentifier!, null, null, null,
            HierarchyItemTypeIdentifierEx.WriteIdentifier, null, null, null);

    [UsedImplicitly]
    private static readonly NetCallRaw<IHierarchyItemTypeIdentifier, Vector3, Quaternion, Vector3, ulong, NetId> SendInstantiation =
        new NetCallRaw<IHierarchyItemTypeIdentifier, Vector3, Quaternion, Vector3, ulong, NetId>(NetCalls.SendHierarchyInstantiation,
            HierarchyItemTypeIdentifierEx.ReadIdentifier!, null, null, null, null, null,
            HierarchyItemTypeIdentifierEx.WriteIdentifier, null, null, null, null, null);
#if CLIENT
    public static void RequestInstantiation(IHierarchyItemTypeIdentifier type, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        SendRequestInstantiation.Invoke(type, position, rotation, scale);
    }

    [NetCall(NetCallSource.FromServer, NetCalls.SendHierarchyInstantiation)]
    public static StandardErrorCode ReceiveHierarchyInstantiation(MessageContext ctx, IHierarchyItemTypeIdentifier? type, Vector3 position, Quaternion rotation, Vector3 scale, ulong owner, NetId netId)
    {
        if (type == null)
            return StandardErrorCode.InvalidData;
        if (!EditorActions.HasProcessedPendingHierarchyObjects)
        {
            EditorActions.TemporaryEditorActions?.QueueInstantiation(type, position, rotation, scale, owner, netId);
            return StandardErrorCode.Success;
        }

        try
        {
            type.Instantiate(position, rotation, scale);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error instantiating {type.Format()} at Net ID {netId.Format()}.", method: Source);
            Logger.LogError(ex, method: Source);
            return StandardErrorCode.GenericError;
        }

        IDevkitHierarchyItem? newItem = LevelHierarchy.instance.items.Count > 0 ? LevelHierarchy.instance.items.GetTail() : null;
        if (newItem != null && type.Type.IsInstanceOfType(newItem))
        {
            if (owner == Provider.client.m_SteamID)
                HierarchyResponsibilities.Set(newItem.instanceID);

            HierarchyItemNetIdDatabase.RegisterHierarchyItem(newItem, netId);
            SyncIfAuthority(newItem);
            return StandardErrorCode.Success;
        }

        Logger.LogError($"Failed to create {type.Format()}, at Net ID {netId.Format()}.", method: Source);
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
            Logger.LogError($"Error instantiating {type.Format()}.", method: Source);
            Logger.LogError(ex, method: Source);
            UIMessage.SendEditorMessage(user, DevkitServerModule.MessageLocalization.Translate("Error", ex.Message));
            return;
        }

        IDevkitHierarchyItem? newItem = LevelHierarchy.instance.items.Count > 0 ? LevelHierarchy.instance.items.GetTail() : null;
        if (newItem != null && type.Type.IsInstanceOfType(newItem))
        {
            DevkitServerModule.ComponentHost.StartCoroutine(InstantiateHierarchyItemCoroutine(ctx, newItem, type, user, position, rotation, scale));
        }
        else
        {
            Logger.LogError($"Failed to create {type.Format()}.", method: Source);
            UIMessage.SendEditorMessage(user, DevkitServerModule.MessageLocalization.Translate("UnknownError"));
        }
    }
    private static IEnumerator InstantiateHierarchyItemCoroutine(MessageContext ctx, IDevkitHierarchyItem newItem, IHierarchyItemTypeIdentifier type, EditorUser user, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        // have to wait for OnEnable to run
        yield return null;
        yield return new WaitForEndOfFrame();

        if (!HierarchyItemNetIdDatabase.TryGetHierarchyItemNetId(newItem, out NetId netId)) // todo failing
        {
            Logger.LogError($"Failed to assign NetId to {type.Format()}.", method: Source);
            UIMessage.SendEditorMessage(user, DevkitServerModule.MessageLocalization.Translate("UnknownError"));
            yield break;
        }

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
            ctx.ReplyLayered(SendInstantiation, type, position, rotation, scale, user.SteamId.m_SteamID, netId);
            list = DevkitServerUtility.GetAllConnections(ctx.Connection);
        }
        else list = DevkitServerUtility.GetAllConnections();

        SendInstantiation.Invoke(list, type, position, rotation, scale, user.SteamId.m_SteamID, netId);
        Logger.LogDebug($"[{Source}] Granted request for instantiation of {type.Format()}, instance ID: {newItem.instanceID.Format()} ({newItem.Format()}) from {user.SteamId.Format()}.");
        SyncIfAuthority(newItem);
    }
#endif
    /// <summary>
    /// Does not replicate. Properly delete a hierarchy item, removing it from <see cref="LevelHierarchy"/>.
    /// </summary>
    public static void LocalRemoveItem(IDevkitHierarchyItem item)
    {
        if (item is Component o2)
            Object.Destroy(o2.gameObject);
        else if (item is Object o)
            Object.Destroy(o);
        else LevelHierarchy.removeItem(item);
    }
    /// <summary>
    /// Does not replicate. Properly move a hierarchy item.
    /// </summary>
    public static void LocalTranslate(IDevkitHierarchyItem item, in FinalTransformation transformation, bool useScale)
    {
        if (item is not Component comp)
            return;
        Transform transform = comp.transform;
        TransformationDelta t = transformation.Transformation;
        if (comp.gameObject.TryGetComponent(out ITransformedHandler handler))
        {
            handler.OnTransformed(
                (t.Flags & TransformationDelta.TransformFlags.OriginalPosition) != 0 ? t.OriginalPosition : transform.position,
                (t.Flags & TransformationDelta.TransformFlags.OriginalRotation) != 0 ? t.OriginalRotation : transform.rotation,
                useScale ? transformation.OriginalScale : Vector3.zero,
                (t.Flags & TransformationDelta.TransformFlags.Position) != 0 ? t.Position : transform.position,
                (t.Flags & TransformationDelta.TransformFlags.Rotation) != 0 ? t.Rotation : transform.rotation,
                useScale ? transformation.Scale : Vector3.zero,
                (t.Flags & TransformationDelta.TransformFlags.Rotation) != 0,
                useScale
            );
        }
        else
        {
            if ((t.Flags & TransformationDelta.TransformFlags.Rotation) != 0)
            {
                if ((t.Flags & TransformationDelta.TransformFlags.Position) != 0)
                    comp.transform.SetPositionAndRotation(t.Position, t.Rotation);
                else
                    comp.transform.rotation = t.Rotation;
            }
            else if ((t.Flags & TransformationDelta.TransformFlags.Position) != 0)
                comp.transform.position = t.Position;
            if (useScale)
                comp.transform.localScale = transformation.Scale;
        }
    }
    public static bool TryGetTransform(IDevkitHierarchyItem item, out Transform transform)
    {
        ThreadUtil.assertIsGameThread();

        if (item is MonoBehaviour { isActiveAndEnabled: true } monoBehaviour)
        {
            transform = monoBehaviour.transform;
            return true;
        }

        transform = null!;
        return false;
    }
    [Pure]
    public static Transform? GetTransform(IDevkitHierarchyItem item)
    {
        ThreadUtil.assertIsGameThread();

        if (item is MonoBehaviour { isActiveAndEnabled: true } monoBehaviour)
            return monoBehaviour.transform;

        return null;
    }
    public static bool TryGetItem(Transform transform, out IDevkitHierarchyItem item)
    {
        item = GetItem(transform)!;
        return item != null;
    }
    public static bool TryFindItem(uint instanceId, out IDevkitHierarchyItem item)
    {
        item = FindItem(instanceId)!;
        return item != null;
    }
    public static bool TryFindItemIndex(uint instanceId, out int index)
    {
        index = FindItemIndex(instanceId);
        return index >= 0;
    }
    [Pure]
    public static IDevkitHierarchyItem? GetItem(Transform transform)
    {
        ThreadUtil.assertIsGameThread();

        if (transform == null)
            throw new ArgumentNullException(nameof(transform));

        transform.GetComponents(HierarchyItemBuffer);
        try
        {
            if (HierarchyItemBuffer.Count == 0)
                return null;
            
            if (HierarchyItemBuffer.Count == 1)
                return HierarchyItemBuffer[0];
            
            return HierarchyItemBuffer.Aggregate((a, b) => a.instanceID > b.instanceID ? a : b);
        }
        finally
        {
            HierarchyItemBuffer.Clear();
        }
    }
    [Pure]
    public static IDevkitHierarchyItem[] GetItems(Transform transform)
    {
        ThreadUtil.assertIsGameThread();

        if (transform == null)
            throw new ArgumentNullException(nameof(transform));

        transform.GetComponents(HierarchyItemBuffer);
        try
        {
            if (HierarchyItemBuffer.Count == 0)
                return Array.Empty<IDevkitHierarchyItem>();
            
            if (HierarchyItemBuffer.Count == 1)
                return new IDevkitHierarchyItem[] { HierarchyItemBuffer[0] };

            return HierarchyItemBuffer.OrderByDescending(x => x.instanceID).ToArray();
        }
        finally
        {
            HierarchyItemBuffer.Clear();
        }
    }
    [Pure]
    public static IDevkitHierarchyItem? FindItem(uint instanceId)
    {
        int ind = FindItemIndex(instanceId);
        return ind < 0 ? null : LevelHierarchy.instance.items[ind];
    }
    /// <returns>Inverse of binary search if not found, will always be &lt; 0 when not found.</returns>
    [Pure]
    public static int FindItemIndex(uint instanceId)
    {
        ThreadUtil.assertIsGameThread();

        List<IDevkitHierarchyItem> items = LevelHierarchy.instance.items;
        int min = 0;
        int max = items.Count - 1;

        // binary search because it should be mostly in order.
        while (min <= max)
        {
            int index = min + (max - min) / 2;
            uint instId = items[index].instanceID;
            if (instId == instanceId)
                return index;
            if (instId < instanceId)
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
    private static bool CheckSync(out HierarchySync sync)
    {
        sync = null!;
#if CLIENT
        if (!DevkitServerModule.IsEditing || EditorUser.User == null || EditorUser.User.HierarchySync == null || !EditorUser.User.HierarchySync.HasAuthority)
            return false;
        sync = EditorUser.User.HierarchySync;
#elif SERVER
        if (!DevkitServerModule.IsEditing || HierarchySync.ServersideAuthority == null || !HierarchySync.ServersideAuthority.HasAuthority)
            return false;
        sync = HierarchySync.ServersideAuthority;
#endif
        return true;
    }
    public static bool SyncIfAuthority(IDevkitHierarchyItem item)
    {
        if (!CheckSync(out HierarchySync sync))
            return false;
        sync.EnqueueSync(item);
        return true;
    }
    public static bool SyncIfAuthority(uint instanceId)
    {
        if (!CheckSync(out HierarchySync sync))
            return false;
        sync.EnqueueSync(instanceId);
        return true;
    }
    public static bool SyncIfAuthority(NetId netId)
    {
        if (!CheckSync(out HierarchySync sync))
            return false;
        sync.EnqueueSync(netId);
        return true;
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
                            VanillaPermissions.MoveUnownedNodes.Has(user, false) ||
                            VanillaPermissions.PlaceNodes.Has(user, false) && HierarchyResponsibilities.IsPlacer(item.instanceID, user),

            CartographyVolume => VanillaPermissions.EditCartographyVolumes.Has(user, false) ||
                                 VanillaPermissions.MoveUnownedCartographyVolumes.Has(user, false) ||
                                 VanillaPermissions.PlaceCartographyVolumes.Has(user, false) && HierarchyResponsibilities.IsPlacer(item.instanceID, user),

            VolumeBase =>   VanillaPermissions.EditVolumes.Has(user, false) ||
                            VanillaPermissions.MoveUnownedVolumes.Has(user, false) ||
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
                            VanillaPermissions.RemoveUnownedNodes.Has(user, false) ||
                            VanillaPermissions.PlaceNodes.Has(user, false) && HierarchyResponsibilities.IsPlacer(item.instanceID, user),

            CartographyVolume => VanillaPermissions.EditCartographyVolumes.Has(user, false) ||
                                 VanillaPermissions.RemoveUnownedCartographyVolumes.Has(user, false) ||
                                 VanillaPermissions.PlaceCartographyVolumes.Has(user, false) && HierarchyResponsibilities.IsPlacer(item.instanceID, user),

            VolumeBase => VanillaPermissions.EditVolumes.Has(user, false) ||
                          VanillaPermissions.RemoveUnownedVolumes.Has(user, false) ||
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
                            VanillaPermissions.MoveUnownedNodes.Has(false) ||
                            VanillaPermissions.PlaceNodes.Has(false) && HierarchyResponsibilities.IsPlacer(item.instanceID),

            CartographyVolume => VanillaPermissions.EditCartographyVolumes.Has(false) ||
                                 VanillaPermissions.MoveUnownedCartographyVolumes.Has(false) ||
                                 VanillaPermissions.PlaceCartographyVolumes.Has(false) && HierarchyResponsibilities.IsPlacer(item.instanceID),

            VolumeBase => VanillaPermissions.EditVolumes.Has(false) ||
                            VanillaPermissions.MoveUnownedVolumes.Has(false) ||
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
                            VanillaPermissions.RemoveUnownedNodes.Has(false) ||
                            VanillaPermissions.PlaceNodes.Has(false) && HierarchyResponsibilities.IsPlacer(item.instanceID),

            CartographyVolume => VanillaPermissions.EditCartographyVolumes.Has(false) ||
                                 VanillaPermissions.RemoveUnownedCartographyVolumes.Has(false) ||
                                 VanillaPermissions.PlaceCartographyVolumes.Has(false) && HierarchyResponsibilities.IsPlacer(item.instanceID),

            VolumeBase => VanillaPermissions.EditVolumes.Has(false) ||
                          VanillaPermissions.RemoveUnownedVolumes.Has(false) ||
                          VanillaPermissions.PlaceVolumes.Has(false) && HierarchyResponsibilities.IsPlacer(item.instanceID),

            _ => false
        };
    }
#endif
}

