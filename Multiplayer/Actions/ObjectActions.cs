using DevkitServer.API;
using DevkitServer.Models;
using DevkitServer.Multiplayer.Levels;
using DevkitServer.Util.Encoding;
#if SERVER
using DevkitServer.API.Permissions;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Players;
#endif

namespace DevkitServer.Multiplayer.Actions;
public sealed class ObjectActions
{
    internal const string Source = "OBJECT ACTIONS";
    public EditorActions EditorActions { get; }
    internal ObjectActions(EditorActions actions)
    {
        EditorActions = actions;
    }
    public void Subscribe()
    {
#if CLIENT
        if (EditorActions.IsOwner)
        {
            ClientEvents.OnDeleteLevelObjects += OnDeleteLevelObjects;
            ClientEvents.OnMoveLevelObjectsFinal += OnMoveLevelObjectsFinal;
            ClientEvents.OnUpdateObjectsCustomMaterialPaletteOverride += OnUpdateObjectsCustomMaterialPaletteOverride;
            ClientEvents.OnUpdateObjectsMaterialIndexOverride += OnUpdateObjectsMaterialIndexOverride;
        }
#endif
    }

    public void Unsubscribe()
    {
#if CLIENT
        if (EditorActions.IsOwner)
        {
            ClientEvents.OnDeleteLevelObjects -= OnDeleteLevelObjects;
            ClientEvents.OnMoveLevelObjectsFinal -= OnMoveLevelObjectsFinal;
            ClientEvents.OnUpdateObjectsCustomMaterialPaletteOverride -= OnUpdateObjectsCustomMaterialPaletteOverride;
            ClientEvents.OnUpdateObjectsMaterialIndexOverride -= OnUpdateObjectsMaterialIndexOverride;
        }
#endif
    }
#if CLIENT
    private void OnDeleteLevelObjects(in DeleteLevelObjectsProperties properties)
    {
        EditorActions.QueueAction(new DeleteLevelObjectsAction
        {
            NetIds = properties.NetIds,
            DeltaTime = properties.DeltaTime
        }, true);
    }
    private void OnMoveLevelObjectsFinal(in MoveLevelObjectsFinalProperties properties)
    {
        EditorActions.QueueAction(new MoveLevelObjectsFinalAction
        {
            Transformations = properties.Transformations,
            UseScale = properties.UseScale,
            DeltaTime = properties.DeltaTime
        }, true);
    }
    private void OnUpdateObjectsCustomMaterialPaletteOverride(in UpdateObjectsCustomMaterialPaletteOverrideProperties properties)
    {
        EditorActions.QueueAction(new UpdateObjectsCustomMaterialPaletteOverrideAction
        {
            NetIds = properties.NetIds,
            Value = properties.Material,
            DeltaTime = properties.DeltaTime
        });
    }
    private void OnUpdateObjectsMaterialIndexOverride(in UpdateObjectsMaterialIndexOverrideProperties properties)
    {
        EditorActions.QueueAction(new UpdateObjectsMaterialIndexOverrideAction
        {
            NetIds = properties.NetIds,
            Value = properties.Index,
            DeltaTime = properties.DeltaTime
        });
    }
#endif
}


[Action(DevkitServerActionType.DeleteLevelObjects, 64 * 4 + 5, 0)]
[EarlyTypeInit]
public sealed class DeleteLevelObjectsAction : IAction
{
    public DevkitServerActionType Type => DevkitServerActionType.DeleteLevelObjects;
    public CSteamID Instigator { get; set; }
    public NetId[] NetIds { get; set; } = Array.Empty<NetId>();
    public float DeltaTime { get; set; }
    public void Apply()
    {
#if SERVER
        Transform?[]? objs = _objects;
#else
        Transform?[]? objs = null;
#endif
        if (objs == null)
        {
            objs = new Transform[NetIds.Length];
            for (int i = 0; i < objs.Length; ++i)
            {
                NetId netId = NetIds[i];
                if (!LevelObjectNetIdDatabase.TryGetObjectOrBuildable(netId, out LevelObject? levelObject, out LevelBuildableObject? buildable, out _))
                {
                    Logger.DevkitServer.LogWarning(ObjectActions.Source, $"Unknown object or buildable with NetId: {netId.Format()}.");
                    continue;
                }
                objs[i] = levelObject == null ? buildable!.transform : levelObject.transform;
            }
        }
        for (int i = 0; i < objs.Length; ++i)
        {
            Transform? transform = objs[i];
            if (transform == null)
                continue;
            LevelObjects.registerRemoveObject(transform);

            LevelObjectUtil.SyncIfAuthority(NetIds[i]);
        }
    }
#if SERVER
    private Transform?[]? _objects;
    public bool CheckCanApply()
    {
        if (default(PermissionLeaf).Has(Instigator.m_SteamID, false))
            return true;
        _objects = new Transform[NetIds.Length];
        for (int i = 0; i < NetIds.Length; ++i)
        {
            NetId netId = NetIds[i];
            if (!LevelObjectNetIdDatabase.TryGetObjectOrBuildable(netId, out LevelObject? levelObject, out LevelBuildableObject? buildable, out RegionIdentifier buildableId))
            {
                Logger.DevkitServer.LogWarning(ObjectActions.Source, $"Unknown object or buildable with NetId: {netId.Format()}.");
                continue;
            }

            if (levelObject != null)
            {
                if (!LevelObjectUtil.CheckDeletePermission(levelObject.instanceID, Instigator.m_SteamID))
                    return false;
                _objects[i] = levelObject.transform;
            }
            else
            {
                if (!LevelObjectUtil.CheckDeleteBuildablePermission(buildableId, Instigator.m_SteamID))
                    return false;
                _objects[i] = buildable!.transform;
            }
        }
        return true;
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        int objectCount = reader.ReadUInt8();

        NetIds = new NetId[objectCount];

        for (int i = 0; i < objectCount; ++i)
            NetIds[i] = reader.ReadNetId();
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        int objectCount = Math.Min(byte.MaxValue, NetIds == null ? 0 : NetIds.Length);

        writer.Write((byte)objectCount);

        for (int i = 0; i < objectCount; ++i)
            writer.Write(NetIds![i]);
    }
    public int CalculateSize() => 5 + Math.Min(byte.MaxValue, NetIds == null ? 0 : NetIds.Length) * 4;
}


[Action(DevkitServerActionType.MoveLevelObjectsFinal, FinalTransformation.Capacity * 32 + 7, 0)]
[EarlyTypeInit]
public class MoveLevelObjectsFinalAction : IAction
{
    public DevkitServerActionType Type => DevkitServerActionType.MoveLevelObjectsFinal;
    public CSteamID Instigator { get; set; }
    public FinalTransformation[] Transformations { get; set; } = Array.Empty<FinalTransformation>();
    public bool UseScale { get; set; }
    public float DeltaTime { get; set; }
    public void Apply()
    {
#if SERVER
        Transform?[]? objs = _objects;
#else
        Transform?[]? objs = null;
#endif
        if (objs == null)
        {
            objs = new Transform[Transformations.Length];
            for (int i = 0; i < objs.Length; ++i)
            {
                NetId netId = Transformations[i].NetId;
                if (!LevelObjectNetIdDatabase.TryGetObjectOrBuildable(netId, out LevelObject? levelObject, out LevelBuildableObject? buildable, out _))
                {
                    Logger.DevkitServer.LogWarning(NavigationActions.Source, $"Unknown object or buildable with NetId: {netId.Format()}.");
                    continue;
                }
                objs[i] = levelObject == null ? buildable!.transform : levelObject.transform;
            }
        }
        for (int i = 0; i < objs.Length; ++i)
        {
            Transform? transform = objs[i];
            if (transform == null)
                continue;
            ref FinalTransformation transformation = ref Transformations[i];
            Vector3 lclScale = !UseScale ? transform.localScale : default;
            LevelObjects.registerTransformObject(transform, transformation.Transformation.Position, transformation.Transformation.Rotation, UseScale ? transformation.Scale : lclScale,
                transformation.Transformation.OriginalPosition, transformation.Transformation.OriginalRotation, UseScale ? transformation.OriginalScale : lclScale);
        }
    }
#if SERVER
    private Transform?[]? _objects;
    public bool CheckCanApply()
    {
        if (default(PermissionLeaf).Has(Instigator.m_SteamID, false))
            return true;
        _objects = new Transform[Transformations.Length];
        for (int i = 0; i < Transformations.Length; ++i)
        {
            NetId netId = Transformations[i].NetId;
            if (!LevelObjectNetIdDatabase.TryGetObjectOrBuildable(netId, out LevelObject? levelObject, out LevelBuildableObject? buildable, out RegionIdentifier buildableId))
            {
                Logger.DevkitServer.LogWarning(ObjectActions.Source, $"Unknown object or buildable with NetId: {netId.Format()}.");
                continue;
            }

            if (levelObject != null)
            {
                if (!LevelObjectUtil.CheckMovePermission(levelObject.instanceID, Instigator.m_SteamID))
                    return false;
                _objects[i] = levelObject.transform;
            }
            else
            {
                if (!LevelObjectUtil.CheckMoveBuildablePermission(buildableId, Instigator.m_SteamID))
                    return false;
                _objects[i] = buildable!.transform;
            }
        }
        return true;
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        int objectCount = reader.ReadUInt8();
        byte flag = reader.ReadUInt8();
        UseScale = (flag & 1) != 0;

        Transformations = new FinalTransformation[objectCount];
        
        for (int i = 0; i < objectCount; ++i)
            Transformations[i] = new FinalTransformation(reader, UseScale);
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        int objectCount = Math.Min(byte.MaxValue, Transformations.Length);

        writer.Write((byte)objectCount);
        writer.Write((byte)(UseScale ? 1 : 0));

        for (int i = 0; i < objectCount; ++i)
            Transformations[i].Write(writer, UseScale);
    }
    public int CalculateSize()
    {
        int size = 5;
        int objectCount = Math.Min(byte.MaxValue, Transformations.Length);
        for (int i = 0; i < objectCount; ++i)
            size += Transformations[i].CalculateSize(UseScale);
        return size;
    }
}

[Action(DevkitServerActionType.InstantiateLevelObject, 68, 0)]
public class InstantiateLevelObjectAction : IServersideAction
{
    public DevkitServerActionType Type => DevkitServerActionType.InstantiateLevelObject;
    public float DeltaTime { get; set; }
    public CSteamID Instigator { get; set; }
    public Guid Asset { get; set; }
    public Vector3 Position { get; set; }
    public Quaternion Rotation { get; set; }
    public Vector3 Scale { get; set; }
    public long RequestKey { get; set; }
    public void Apply()
    {
#if SERVER
        EditorUser? user = UserManager.FromId(Instigator);
        if (user != null)
        {
            LevelObjectUtil.ReceiveLevelObjectInstantiationRequest(
                new MessageContext(user.Connection, new MessageOverhead(MessageFlags.Request, 0, 0, RequestKey, 0), false),
                Asset, Position, Rotation, Scale);
        }
#endif
    }
#if SERVER
    public bool CheckCanApply() => throw new NotImplementedException();
#endif

    public void Read(ByteReader reader)
    {
        Asset = reader.ReadGuid();
        Position = reader.ReadVector3();
        Rotation = reader.ReadQuaternion();
        Scale = reader.ReadVector3();
        RequestKey = reader.ReadInt64();
        DeltaTime = reader.ReadFloat();
    }

    public void Write(ByteWriter writer)
    {
        writer.Write(Asset);
        writer.Write(Position);
        writer.Write(Rotation);
        writer.Write(Scale);
        writer.Write(RequestKey);
        writer.Write(DeltaTime);
    }

    public int CalculateSize() => 68;
}

[Action(DevkitServerActionType.UpdateObjectsCustomMaterialPaletteOverride, 64 * 4 + 21, 0)]
[EarlyTypeInit]
public sealed class UpdateObjectsCustomMaterialPaletteOverrideAction : IReplacableAction
{
    public DevkitServerActionType Type => DevkitServerActionType.UpdateObjectsCustomMaterialPaletteOverride;
    public CSteamID Instigator { get; set; }
    public NetId[] NetIds { get; set; } = Array.Empty<NetId>();
    public float DeltaTime { get; set; }
    public AssetReference<MaterialPaletteAsset> Value { get; set; }
    public bool TryReplaceFrom(IReplacableAction action)
    {
        UpdateObjectsCustomMaterialPaletteOverrideAction a = (UpdateObjectsCustomMaterialPaletteOverrideAction)action;
        if (a.NetIds.Length != NetIds.Length)
            return false;
        for (int i = 0; i < NetIds.Length; ++i)
        {
            if (NetIds[i].id != a.NetIds[i].id)
                return false;
        }

        Value = a.Value;
        return true;
    }
    public void Apply()
    {
#if SERVER
        LevelObject?[]? objs = _objects;
#else
        LevelObject?[]? objs = null;
#endif
        if (objs == null)
        {
            objs = new LevelObject[NetIds.Length];
            for (int i = 0; i < objs.Length; ++i)
            {
                NetId netId = NetIds[i];
                if (!LevelObjectNetIdDatabase.TryGetObjectOrBuildable(netId, out LevelObject? levelObject, out _, out _) || levelObject == null)
                {
                    Logger.DevkitServer.LogWarning(NavigationActions.Source, $"Unknown object with NetId: {netId.Format()}.");
                    continue;
                }
                objs[i] = levelObject;
            }
        }
        for (int i = 0; i < objs.Length; ++i)
        {
            LevelObject? obj = objs[i];
            if (obj == null)
                continue;

            LevelObjectUtil.SetCustomMaterialPaletteOverrideLocal(obj, Value);

            LevelObjectUtil.SyncIfAuthority(NetIds[i]);
        }
    }
#if SERVER
    private LevelObject?[]? _objects;
    public bool CheckCanApply()
    {
        if (default(PermissionLeaf).Has(Instigator.m_SteamID, false))
            return true;
        _objects = new LevelObject[NetIds.Length];
        for (int i = 0; i < NetIds.Length; ++i)
        {
            NetId netId = NetIds[i];
            if (!LevelObjectNetIdDatabase.TryGetObjectOrBuildable(netId, out LevelObject? levelObject, out _, out _) || levelObject == null)
            {
                Logger.DevkitServer.LogWarning(ObjectActions.Source, $"Unknown object with NetId: {netId.Format()}.");
                continue;
            }
            
            if (!LevelObjectUtil.CheckMovePermission(levelObject.instanceID, Instigator.m_SteamID))
                return false;
            _objects[i] = levelObject;
        }
        return true;
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        Value = new AssetReference<MaterialPaletteAsset>(reader.ReadGuid());

        int objectCount = reader.ReadUInt8();

        NetIds = new NetId[objectCount];

        for (int i = 0; i < objectCount; ++i)
            NetIds[i] = reader.ReadNetId();
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write(Value.GUID);

        int objectCount = Math.Min(byte.MaxValue, NetIds == null ? 0 : NetIds.Length);

        writer.Write((byte)objectCount);

        for (int i = 0; i < objectCount; ++i)
            writer.Write(NetIds![i]);
    }
    public int CalculateSize() => 21 + Math.Min(byte.MaxValue, NetIds == null ? 0 : NetIds.Length) * 4;
}

[Action(DevkitServerActionType.UpdateObjectsMaterialIndexOverride, 64 * 4 + 9, 0)]
[EarlyTypeInit]
public sealed class UpdateObjectsMaterialIndexOverrideAction : IReplacableAction
{
    public DevkitServerActionType Type => DevkitServerActionType.UpdateObjectsMaterialIndexOverride;
    public CSteamID Instigator { get; set; }
    public NetId[] NetIds { get; set; } = Array.Empty<NetId>();
    public float DeltaTime { get; set; }
    public int Value { get; set; }
    public bool TryReplaceFrom(IReplacableAction action)
    {
        UpdateObjectsMaterialIndexOverrideAction a = (UpdateObjectsMaterialIndexOverrideAction)action;
        if (a.NetIds.Length != NetIds.Length)
            return false;
        for (int i = 0; i < NetIds.Length; ++i)
        {
            if (NetIds[i].id != a.NetIds[i].id)
                return false;
        }

        Value = a.Value;
        return true;
    }
    public void Apply()
    {
#if SERVER
        LevelObject?[]? objs = _objects;
#else
        LevelObject?[]? objs = null;
#endif
        if (objs == null)
        {
            objs = new LevelObject[NetIds.Length];
            for (int i = 0; i < objs.Length; ++i)
            {
                NetId netId = NetIds[i];
                if (!LevelObjectNetIdDatabase.TryGetObjectOrBuildable(netId, out LevelObject? levelObject, out _, out _) || levelObject == null)
                {
                    Logger.DevkitServer.LogWarning(NavigationActions.Source, $"Unknown object with NetId: {netId.Format()}.");
                    continue;
                }
                objs[i] = levelObject;
            }
        }
        for (int i = 0; i < objs.Length; ++i)
        {
            LevelObject? obj = objs[i];
            if (obj == null)
                continue;

            LevelObjectUtil.SetMaterialIndexOverrideLocal(obj, Value);

            LevelObjectUtil.SyncIfAuthority(NetIds[i]);
        }
    }
#if SERVER
    private LevelObject?[]? _objects;
    public bool CheckCanApply()
    {
        if (default(PermissionLeaf).Has(Instigator.m_SteamID, false))
            return true;
        _objects = new LevelObject[NetIds.Length];
        for (int i = 0; i < NetIds.Length; ++i)
        {
            NetId netId = NetIds[i];
            if (!LevelObjectNetIdDatabase.TryGetObjectOrBuildable(netId, out LevelObject? levelObject, out _, out _) || levelObject == null)
            {
                Logger.DevkitServer.LogWarning(ObjectActions.Source, $"Unknown object with NetId: {netId.Format()}.");
                continue;
            }
            
            if (!LevelObjectUtil.CheckMovePermission(levelObject.instanceID, Instigator.m_SteamID))
                return false;
            _objects[i] = levelObject;
        }
        return true;
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        Value = reader.ReadInt32();

        int objectCount = reader.ReadUInt8();

        NetIds = new NetId[objectCount];

        for (int i = 0; i < objectCount; ++i)
            NetIds[i] = reader.ReadNetId();
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write(Value);

        int objectCount = Math.Min(byte.MaxValue, NetIds == null ? 0 : NetIds.Length);

        writer.Write((byte)objectCount);

        for (int i = 0; i < objectCount; ++i)
            writer.Write(NetIds![i]);
    }
    public int CalculateSize() => 9 + Math.Min(byte.MaxValue, NetIds == null ? 0 : NetIds.Length) * 4;
}