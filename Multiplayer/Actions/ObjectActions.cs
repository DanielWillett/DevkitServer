using DevkitServer.Util.Encoding;
using DevkitServer.Models;
using DevkitServer.Multiplayer.Levels;
#if SERVER
using DevkitServer.Multiplayer.Networking;
using DevkitServer.API.Permissions;
using DevkitServer.Players;
#endif

namespace DevkitServer.Multiplayer.Actions;
public sealed class ObjectActions
{
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
            ClientEvents.OnMoveLevelObjectsPreview += OnMoveLevelObjectsPreview;
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
            ClientEvents.OnMoveLevelObjectsPreview -= OnMoveLevelObjectsPreview;
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
    private void OnMoveLevelObjectsPreview(in MoveLevelObjectsPreviewProperties properties)
    {
        if (properties.Transformations.Length > LevelObjectUtil.MaxMovePreviewSelectionSize || DevkitServerModuleComponent.Ticks % 4 != 0)
            return;
        EditorActions.QueueAction(new MoveLevelObjectsPreviewAction
        {
            Transformations = properties.Transformations,
            DeltaTime = properties.DeltaTime
        }, true);
    }
#endif
}


[Action(ActionType.DeleteLevelObjects, 64 * 4 + 5, 0)]
[EarlyTypeInit]
public sealed class DeleteLevelObjectsAction : IAction
{
    public ActionType Type => ActionType.DeleteLevelObjects;
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
                    Logger.LogWarning($"Unknown object or buildable with NetId: {netId.Format()}.");
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
        }
    }
#if SERVER
    private Transform?[]? _objects;
    public bool CheckCanApply()
    {
        if (Permission.SuperuserPermission.Has(Instigator.m_SteamID, false))
            return true;
        _objects = new Transform[NetIds.Length];
        for (int i = 0; i < NetIds.Length; ++i)
        {
            NetId netId = NetIds[i];
            if (!LevelObjectNetIdDatabase.TryGetObjectOrBuildable(netId, out LevelObject? levelObject, out LevelBuildableObject? buildable, out RegionIdentifier buildableId))
            {
                Logger.LogWarning($"Unknown object or buildable with NetId: {netId.Format()}.");
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
        int objectCount = Math.Min(byte.MaxValue, NetIds.Length);

        writer.Write((byte)objectCount);

        for (int i = 0; i < objectCount; ++i)
            writer.Write(NetIds[i]);
    }
}

[Action(ActionType.MoveLevelObjectsPreview, PreviewTransformation.Capacity * 32 + 6, 0)]
[EarlyTypeInit]
public class MoveLevelObjectsPreviewAction : IAction
{
    public ActionType Type => ActionType.MoveLevelObjectsPreview;
    public CSteamID Instigator { get; set; }
    public PreviewTransformation[] Transformations { get; set; } = Array.Empty<PreviewTransformation>();
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
                    Logger.LogWarning($"Unknown object or buildable with NetId: {netId.Format()}.");
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
            ref PreviewTransformation transformation = ref Transformations[i];
            transformation.Transformation.ApplyTo(transform, true);
        }
    }
#if SERVER
    private Transform?[]? _objects;
    public bool CheckCanApply()
    {
        if (Transformations.Length > LevelObjectUtil.MaxMovePreviewSelectionSize)
            return false;
        if (Permission.SuperuserPermission.Has(Instigator.m_SteamID, false))
            return true;
        _objects = new Transform[Transformations.Length];
        for (int i = 0; i < Transformations.Length; ++i)
        {
            NetId netId = Transformations[i].NetId;
            if (!LevelObjectNetIdDatabase.TryGetObjectOrBuildable(netId, out LevelObject? levelObject, out LevelBuildableObject? buildable, out RegionIdentifier buildableId))
            {
                Logger.LogWarning($"Unknown object or buildable with NetId: {netId.Format()}.");
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

        Transformations = new PreviewTransformation[objectCount];

        for (int i = 0; i < objectCount; ++i)
            Transformations[i] = new PreviewTransformation(reader, false);
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        int objectCount = Math.Min(byte.MaxValue, Transformations.Length);

        writer.Write((byte)objectCount);

        for (int i = 0; i < objectCount; ++i)
            Transformations[i].Write(writer, false);
    }
}

[Action(ActionType.MoveLevelObjectsFinal, FinalTransformation.Capacity * 32 + 7, 0)]
[EarlyTypeInit]
public class MoveLevelObjectsFinalAction : IAction
{
    public ActionType Type => ActionType.MoveLevelObjectsFinal;
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
                    Logger.LogWarning($"Unknown object or buildable with NetId: {netId.Format()}.");
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
        if (Permission.SuperuserPermission.Has(Instigator.m_SteamID, false))
            return true;
        _objects = new Transform[Transformations.Length];
        for (int i = 0; i < Transformations.Length; ++i)
        {
            NetId netId = Transformations[i].NetId;
            if (!LevelObjectNetIdDatabase.TryGetObjectOrBuildable(netId, out LevelObject? levelObject, out LevelBuildableObject? buildable, out RegionIdentifier buildableId))
            {
                Logger.LogWarning($"Unknown object or buildable with NetId: {netId.Format()}.");
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
}

[Action(ActionType.InstantiateLevelObject, 47, 0)]
public class InstantiateLevelObjectAction : IServersideAction
{
    public ActionType Type => ActionType.InstantiateLevelObject;
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
            LevelObjectUtil.ReceiveLevelObjectInstantiation(
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
}