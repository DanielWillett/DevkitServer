using DevkitServer.API;
using DevkitServer.API.Abstractions;
using DevkitServer.API.Permissions;
using DevkitServer.Models;
using DevkitServer.Multiplayer;
using DevkitServer.Multiplayer.Levels;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Multiplayer.Sync;
using DevkitServer.Players;
using SDG.Framework.Devkit;
#if SERVER
using DevkitServer.API.UI;
using DevkitServer.Core.Permissions;
#elif CLIENT
using DevkitServer.Multiplayer.Actions;
#endif

namespace DevkitServer.Util;
[EarlyTypeInit]
public static class HierarchyUtil
{
    internal static readonly List<IDevkitHierarchyItem> HierarchyItemBuffer = new List<IDevkitHierarchyItem>(64);
    
    [UsedImplicitly]
    private static readonly NetCallRaw<IHierarchyItemTypeIdentifier, Vector3, Quaternion, Vector3> SendRequestInstantiation =
        new NetCallRaw<IHierarchyItemTypeIdentifier, Vector3, Quaternion, Vector3>(DevkitServerNetCall.RequestHierarchyInstantiation,
            HierarchyItemTypeIdentifierEx.ReadIdentifier!, null, null, null,
            HierarchyItemTypeIdentifierEx.WriteIdentifier, null, null, null);

    [UsedImplicitly]
    private static readonly NetCallRaw<IHierarchyItemTypeIdentifier, Vector3, Quaternion, Vector3, ulong, NetId> SendInstantiation =
        new NetCallRaw<IHierarchyItemTypeIdentifier, Vector3, Quaternion, Vector3, ulong, NetId>(DevkitServerNetCall.SendHierarchyInstantiation,
            HierarchyItemTypeIdentifierEx.ReadIdentifier!, null, null, null, null, null,
            HierarchyItemTypeIdentifierEx.WriteIdentifier, null, null, null, null, null);
#if CLIENT
    public static void RequestInstantiation(IHierarchyItemTypeIdentifier type, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        SendRequestInstantiation.Invoke(type, position, rotation, scale);
    }

    [NetCall(NetCallSource.FromServer, DevkitServerNetCall.SendHierarchyInstantiation)]
    public static StandardErrorCode ReceiveHierarchyInstantiation(MessageContext ctx, IHierarchyItemTypeIdentifier? type, Vector3 position, Quaternion rotation, Vector3 scale, ulong owner, NetId netId)
    {
        if (type == null)
            return StandardErrorCode.InvalidData;
        if (!EditorActions.HasProcessedPendingHierarchyObjects)
        {
            EditorActions.TemporaryEditorActions?.QueueHierarchyItemInstantiation(type, position, rotation, scale, owner, netId);
            return StandardErrorCode.Success;
        }

        try
        {
            type.Instantiate(position, rotation, scale);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(nameof(ReceiveHierarchyInstantiation), ex, $"Error instantiating {type.Format()} at Net ID {netId.Format()}.");
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

        Logger.DevkitServer.LogError(nameof(ReceiveHierarchyInstantiation), $"Failed to create {type.Format()}, at Net ID {netId.Format()}.");
        return StandardErrorCode.GenericError;
    }
#elif SERVER
    [NetCall(NetCallSource.FromClient, DevkitServerNetCall.RequestHierarchyInstantiation)]
    public static void ReceiveRequestHierarchyInstantiation(MessageContext ctx, IHierarchyItemTypeIdentifier? type, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        EditorUser? user = ctx.GetCaller();
        if (user == null || !user.IsOnline)
        {
            Logger.DevkitServer.LogError(nameof(ReceiveRequestHierarchyInstantiation), "Unable to get user from hierarchy instantiation request.");
            ctx.Acknowledge(StandardErrorCode.NoPermissions);
            return;
        }
        if (type == null)
        {
            EditorMessage.SendEditorMessage(user, DevkitServerModule.MessageLocalization.Translate("UnknownError"));
            return;
        }

        PermissionLeaf leaf = VanillaPermissions.GetNodeVolumePlace(type.Type);

        if (!leaf.Has(user))
        {
            ctx.Acknowledge(StandardErrorCode.NoPermissions);
            EditorMessage.SendNoPermissionMessage(user, leaf);
            return;
        }

        try
        {
            type.Instantiate(position, rotation, scale);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(nameof(ReceiveRequestHierarchyInstantiation), ex, $"Error instantiating {type.Format()}.");
            EditorMessage.SendEditorMessage(user, DevkitServerModule.MessageLocalization.Translate("Error", ex.Message));
            return;
        }

        IDevkitHierarchyItem? newItem = LevelHierarchy.instance.items.Count > 0 ? LevelHierarchy.instance.items.GetTail() : null;
        if (newItem == null || !type.Type.IsInstanceOfType(newItem))
        {
            Logger.DevkitServer.LogError(nameof(ReceiveRequestHierarchyInstantiation), $"Failed to create {type.Format()}.");
            EditorMessage.SendEditorMessage(user, DevkitServerModule.MessageLocalization.Translate("UnknownError"));
            return;
        }

        NetId netId = HierarchyItemNetIdDatabase.AddHierarchyItem(newItem);

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
        Logger.DevkitServer.LogDebug(nameof(ReceiveRequestHierarchyInstantiation), $"Granted request for instantiation of {type.Format()}, instance ID: {newItem.instanceID.Format()} ({newItem.Format()}) from {user.SteamId.Format()}.");
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
        LevelHierarchy.MarkDirty();
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
        LevelHierarchy.MarkDirty();
    }
    public static bool TryGetTransform(IDevkitHierarchyItem item, out Transform transform)
    {
        ThreadUtil.assertIsGameThread();

        if (item is Component { gameObject.activeInHierarchy: true } component)
        {
            transform = component.transform;
            return true;
        }

        transform = null!;
        return false;
    }
    [Pure]
    public static Transform? GetTransform(IDevkitHierarchyItem item)
    {
        ThreadUtil.assertIsGameThread();

        if (item is Component { gameObject.activeInHierarchy: true } component)
            return component.transform;

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
            return null;

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
    internal static bool CheckSync(out HierarchySync sync)
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

    /// <summary>
    /// Gets the closest node of type <typeparamref name="TNode"/>, or <see langword="null"/> if there are none (or in the case of a reflection failure).
    /// </summary>
    [Pure]
    public static TNode? GetNearestNode<TNode>(Vector3 position) where TNode : TempNodeBase
    {
        GameObject? closestNode = null;
        float dist = 0f;
        TempNodeSystemBase? system = NodeItemTypeIdentifier.Get(typeof(TNode)).System;

        if (system == null)
            return null;

        foreach (GameObject node in NodeItemTypeIdentifier.EnumerateSystem(system))
        {
            float distSqr = (position - node.transform.position).sqrMagnitude;
            if (closestNode == null || distSqr < dist)
            {
                closestNode = node;
                dist = distSqr;
            }
        }

#if DEBUG
        if (closestNode == null)
        {
            Logger.DevkitServer.LogDebug(nameof(GetNearestNode), $"No nodes available in {FormattingUtil.FormatMethod(typeof(TNode), typeof(HierarchyUtil), nameof(GetNearestNode),
                [ ( typeof(Vector3), nameof(position)) ], null, [ typeof(TNode) ], true)}");
        }
#endif

        return closestNode == null ? null : closestNode.GetComponent<TNode>();
    }

    /// <summary>
    /// Gets the closest volume of type <typeparamref name="TVolume"/>, or <see langword="null"/> if there are none (or in the case of a reflection failure).
    /// </summary>
    [Pure]
    public static TVolume? GetNearestVolume<TVolume>(Vector3 position) where TVolume : VolumeBase
    {
        VolumeBase? closestNode = null;
        float dist = 0f;
        VolumeManagerBase? manager = VolumeItemTypeIdentifier.Get(typeof(TVolume)).Manager;

        if (manager == null)
            return null;

        foreach (VolumeBase node in manager.EnumerateAllVolumes())
        {
            float distSqr = (position - node.transform.position).sqrMagnitude;
            if (closestNode == null || distSqr < dist)
            {
                closestNode = node;
                dist = distSqr;
            }
        }

#if DEBUG
        if (closestNode == null)
        {
            Logger.DevkitServer.LogDebug(nameof(GetNearestVolume), $"No volumes available in {FormattingUtil.FormatMethod(typeof(TVolume),
                typeof(HierarchyUtil), nameof(GetNearestVolume), [ (typeof(Vector3), nameof(position)) ], null, [ typeof(TVolume) ], true)}");
        }
#endif

        return closestNode == null ? null : closestNode as TVolume;
    }
}

