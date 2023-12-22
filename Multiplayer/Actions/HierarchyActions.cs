using DevkitServer.API;
using DevkitServer.Models;
using DevkitServer.Multiplayer.Levels;
using DevkitServer.Util.Encoding;
using SDG.Framework.Devkit;
#if SERVER
using DevkitServer.API.Permissions;
using DevkitServer.Core.Permissions;
#endif

namespace DevkitServer.Multiplayer.Actions;

public sealed class HierarchyActions
{
    internal const string Source = "HIERARCHY ACTIONS";
    public EditorActions EditorActions { get; }
    internal HierarchyActions(EditorActions actions)
    {
        EditorActions = actions;
    }
    public void Subscribe()
    {
#if CLIENT
        if (EditorActions.IsOwner)
        {
            ClientEvents.OnDeleteHierarchyObjects += OnDeleteHierarchyObjects;
            ClientEvents.OnRequestInstantiateHierarchyObject += OnRequestInstantiateHierarchyObject;
            ClientEvents.OnMoveHierarchyObjectsFinal += OnMoveHierarchyObjectsFinal;
        }
#endif
    }
    public void Unsubscribe()
    {
#if CLIENT
        if (EditorActions.IsOwner)
        {
            ClientEvents.OnDeleteHierarchyObjects -= OnDeleteHierarchyObjects;
            ClientEvents.OnRequestInstantiateHierarchyObject -= OnRequestInstantiateHierarchyObject;
            ClientEvents.OnMoveHierarchyObjectsFinal -= OnMoveHierarchyObjectsFinal;
        }
#endif
    }
#if CLIENT
    private void OnDeleteHierarchyObjects(in DeleteHierarchyObjectsProperties properties)
    {
        EditorActions.QueueAction(new DeleteHierarchyItemsAction
        {
            DeltaTime = properties.DeltaTime,
            NetIds = properties.NetIds
        });
    }
    private static void OnRequestInstantiateHierarchyObject(in InstantiateHierarchyObjectProperties properties)
    {
        HierarchyUtil.RequestInstantiation(properties.Type, properties.Position, Quaternion.identity, Vector3.one);
    }
    private void OnMoveHierarchyObjectsFinal(in MoveHierarchyObjectsFinalProperties properties)
    {
        EditorActions.QueueAction(new MovedHierarchyObjectsAction
        {
            DeltaTime = properties.DeltaTime,
            Transformations = properties.Transformations,
            UseScale = properties.UseScale
        });
    }
#endif
}

[Action(DevkitServerActionType.MoveHierarchyItems, FinalTransformation.Capacity * 32 + 7, 0)]
[EarlyTypeInit]
public sealed class MovedHierarchyObjectsAction : IAction
{
    public DevkitServerActionType Type => DevkitServerActionType.MoveHierarchyItems;
    public CSteamID Instigator { get; set; }
    public float DeltaTime { get; set; }
    public FinalTransformation[] Transformations { get; set; } = Array.Empty<FinalTransformation>();
    public bool UseScale { get; set; }

    public void Apply()
    {
        if (Transformations == null)
            return;
        FinalTransformation[] transformations = Transformations;
        for (int i = 0; i < transformations.Length; ++i)
        {
            ref FinalTransformation transformation = ref transformations[i];

            if (!HierarchyItemNetIdDatabase.TryGetHierarchyItem(transformation.NetId, out IDevkitHierarchyItem item))
            {
                Logger.DevkitServer.LogWarning(HierarchyActions.Source, $"Unknown hierarchy item: {transformation.NetId.Format()}.");
                continue;
            }

            HierarchyUtil.LocalTranslate(item, in transformation, UseScale);
            HierarchyUtil.SyncIfAuthority(transformation.NetId);
        }
    }
#if SERVER
    public bool CheckCanApply()
    {
        if (default(PermissionLeaf).Has(Instigator.m_SteamID))
            return true;
        for (int i = 0; i < Transformations.Length; ++i)
        {
            ref FinalTransformation transformation = ref Transformations[i];
            if (!HierarchyItemNetIdDatabase.TryGetHierarchyItem(transformation.NetId, out IDevkitHierarchyItem item))
                continue;
            if (!VanillaPermissions.GetNodeVolumeMove(item.GetType()).Has(Instigator.m_SteamID))
                return false;
        }

        return true;
    }
#endif
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);

        byte flag = (byte)(UseScale ? 1 : 0);
        writer.Write(flag);

        int len = Math.Min(byte.MaxValue, Transformations.Length);
        writer.Write((byte)len);

        for (int i = 0; i < len; ++i)
        {
            ref FinalTransformation transformation = ref Transformations[i];
            transformation.Write(writer, UseScale, false);
        }
    }
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();

        UseScale = (reader.ReadUInt8() & 1) != 0;

        int len = reader.ReadUInt8();
        Transformations = new FinalTransformation[len];

        for (int i = 0; i < len; ++i)
        {
            ref FinalTransformation transformation = ref Transformations[i];
            transformation = new FinalTransformation(reader, UseScale, false);
        }
    }
    public int CalculateSize()
    {
        int size = 6;
        int objectCount = Math.Min(byte.MaxValue, Transformations.Length);
        for (int i = 0; i < objectCount; ++i)
            size += Transformations[i].CalculateSize(UseScale);
        return size;
    }
}
[Action(DevkitServerActionType.DeleteHierarchyItems, 68, 0)]
[EarlyTypeInit]
public sealed class DeleteHierarchyItemsAction : IAction
{
    public DevkitServerActionType Type => DevkitServerActionType.DeleteHierarchyItems;
    public CSteamID Instigator { get; set; }
    public float DeltaTime { get; set; }
    public NetId[] NetIds { get; set; } = Array.Empty<NetId>();

    public void Apply()
    {
        if (NetIds == null)
            return;

        for (int i = 0; i < NetIds.Length; ++i)
        {
            NetId netId = NetIds[i];
            if (!HierarchyItemNetIdDatabase.TryGetHierarchyItem(netId, out IDevkitHierarchyItem item) || item == null)
                continue;

            HierarchyUtil.LocalRemoveItem(item);
            HierarchyUtil.SyncIfAuthority(netId);
        }
    }
#if SERVER
    public bool CheckCanApply()
    {
        if (default(PermissionLeaf).Has(Instigator.m_SteamID, false))
            return true;
        for (int i = 0; i < NetIds.Length; ++i)
        {
            if (!HierarchyItemNetIdDatabase.TryGetHierarchyItem(NetIds[i], out IDevkitHierarchyItem item))
                continue;
            if (!VanillaPermissions.GetNodeVolumeRemove(item.GetType()).Has(Instigator.m_SteamID))
                return false;
        }
        return true;
    }
#endif
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        int len = NetIds.Length;
        writer.Write(len);
        for (int i = 0; i < len; ++i)
            writer.Write(NetIds[i]);
    }
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        int len = reader.ReadInt32();
        if (NetIds == null || NetIds.Length != len)
            NetIds = new NetId[len];
        for (int i = 0; i < len; ++i)
            NetIds[i] = new NetId(reader.ReadUInt32());
    }
    public int CalculateSize() => 8 + NetIds.Length * 4;
}