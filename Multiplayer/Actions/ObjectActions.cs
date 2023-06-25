using DevkitServer.Util.Encoding;
using DevkitServer.Models;
using DevkitServer.Multiplayer.Networking;
#if SERVER
using DevkitServer.API.Permissions;
using DevkitServer.Configuration;
using DevkitServer.Players;
using JetBrains.Annotations;
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
            Buildables = properties.Buildables,
            Objects = properties.Objects,
            DeltaTime = properties.DeltaTime
        }, true);
    }
    private void OnMoveLevelObjectsFinal(in MoveLevelObjectsFinalProperties properties)
    {
        EditorActions.QueueAction(new MoveLevelObjectsAction
        {
            Objects = properties.InstanceIds,
            ObjectTransformations = properties.ObjectTransformations,
            ObjectScales = properties.ObjectScales,
            OriginalObjectScales = properties.OriginalObjectScales,
            Buildables = properties.Buildables,
            BuildableTransformations = properties.BuildableTransformations,
            BuildableScales = properties.BuildableScales,
            OriginalBuildableScales = properties.OriginalBuildableScales,
            UseScale = properties.ObjectScales != null && properties.BuildableScales != null &&
                       properties.OriginalObjectScales != null && properties.OriginalBuildableScales != null,
            DeltaTime = properties.DeltaTime,
            IsFinal = true
        }, true);
    }
    private void OnMoveLevelObjectsPreview(in MoveLevelObjectsPreviewProperties properties)
    {
        EditorActions.QueueAction(new MoveLevelObjectsAction
        {
            Objects = properties.InstanceIds,
            ObjectTransformations = properties.ObjectTransformations,
            Buildables = properties.Buildables,
            BuildableTransformations = properties.BuildableTransformations,
            UseScale = false,
            DeltaTime = properties.DeltaTime,
            IsFinal = false
        }, true);
    }
#endif
}


[Action(ActionType.DeleteLevelObjects, 262, 0)]
[EarlyTypeInit]
public sealed class DeleteLevelObjectsAction : IAction
{
    public ActionType Type => ActionType.DeleteLevelObjects;
    public CSteamID Instigator { get; set; }
    public uint[] Objects { get; set; } = Array.Empty<uint>();
    public RegionIdentifier[] Buildables { get; set; } = Array.Empty<RegionIdentifier>();
    public float DeltaTime { get; set; }
    public void Apply()
    {
#if SERVER
        LevelObject?[]? objs = _objects;
#else
        LevelObject?[]? objs = null;
#endif
#if SERVER
        LevelBuildableObject?[]? buildables = _buildables;
#else
        LevelBuildableObject?[]? buildables = null;
#endif
        if (objs == null)
        {
            objs = Objects.Length == 0 ? Array.Empty<LevelObject>() : new LevelObject[Objects.Length];
            Vector3 last = Vector3.zero;
            for (int i = 0; i < Objects.Length; ++i)
            {
                LevelObject? obj = i == 0 ? LevelObjectUtil.FindObject(Objects[i]) : LevelObjectUtil.FindObject(last, Objects[i]);
                objs[i] = obj;
                if (obj == null)
                    Logger.LogWarning($"Unknown object: # {Objects[i]}.");
                else
                    last = obj.transform.position;
            }
        }
        if (buildables == null)
        {
            buildables = Buildables.Length == 0 ? Array.Empty<LevelBuildableObject>() : new LevelBuildableObject[Buildables.Length];
            for (int i = 0; i < Buildables.Length; ++i)
            {
                LevelBuildableObject? buildable = LevelObjectUtil.GetBuildable(Buildables[i]);
                buildables[i] = buildable;
                if (buildable == null)
                    Logger.LogWarning($"Unknown buildable: {Buildables[i]}.");
            }
        }
        for (int i = 0; i < objs.Length; ++i)
        {
            if (objs[i] is not { } obj) continue;
            LevelObjects.registerRemoveObject(obj.transform);
        }
        for (int i = 0; i < buildables.Length; ++i)
        {
            if (buildables[i] is not { } buildable) continue;
            LevelObjects.registerRemoveObject(buildable.transform);
        }
    }
#if SERVER
    private LevelObject?[]? _objects;
    private LevelBuildableObject?[]? _buildables;
    public bool CheckCanApply()
    {
        if (Permission.SuperuserPermission.Has(Instigator.m_SteamID, false))
            return true;
        if (Objects.Length > byte.MaxValue || Buildables.Length > byte.MaxValue)
            return false;
        _objects = Objects.Length == 0 ? Array.Empty<LevelObject>() : new LevelObject[Objects.Length];
        _buildables = Buildables.Length == 0 ? Array.Empty<LevelBuildableObject>() : new LevelBuildableObject[Objects.Length];
        Vector3 last = Vector3.zero;
        for (int i = 0; i < Objects.Length; ++i)
        {
            LevelObject? obj = i == 0 ? LevelObjectUtil.FindObject(Objects[i]) : LevelObjectUtil.FindObject(last, Objects[i]);
            _objects[i] = obj;
            if (obj == null)
            {
                Logger.LogWarning($"Unknown object: # {Objects[i]}.");
                return false;
            }
            if (!LevelObjectUtil.CheckDeletePermission(obj.instanceID, Instigator.m_SteamID))
                return false;
            last = obj.transform.position;
        }
        for (int i = 0; i < Buildables.Length; ++i)
        {
            RegionIdentifier identifier = Buildables[i];
            LevelBuildableObject? obj = LevelObjectUtil.GetBuildable(identifier);
            _buildables[i] = obj;
            if (obj == null)
            {
                Logger.LogWarning($"Unknown buildable: {Buildables[i]}.");
                return false;
            }
            if (!LevelObjectUtil.CheckDeleteBuildablePermission(identifier, Instigator.m_SteamID))
                return false;
        }
        return true;
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        int objectCount = reader.ReadUInt8();
        int buildableCount = reader.ReadUInt8();

        Objects = objectCount == 0 ? Array.Empty<uint>() : new uint[objectCount];
        Buildables = buildableCount == 0 ? Array.Empty<RegionIdentifier>() : new RegionIdentifier[buildableCount];

        for (int i = 0; i < objectCount; ++i)
            Objects[i] = reader.ReadUInt32();

        for (int i = 0; i < buildableCount; ++i)
            Buildables[i] = RegionIdentifier.Read(reader);
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        int objectCount = Math.Min(byte.MaxValue, Objects.Length);
        int buildableCount = Math.Min(byte.MaxValue, Buildables.Length);

        writer.Write((byte)objectCount);
        writer.Write((byte)buildableCount);

        for (int i = 0; i < objectCount; ++i)
            writer.Write(Objects[i]);

        for (int i = 0; i < buildableCount; ++i)
            RegionIdentifier.Write(writer, Buildables[i]);
    }
}

[Action(ActionType.MoveLevelObjectsFinal, 262, 0)]
[EarlyTypeInit]
public class MoveLevelObjectsAction : IAction
{
    public ActionType Type => ActionType.MoveLevelObjectsFinal;
    public CSteamID Instigator { get; set; }
    public uint[] Objects { get; set; } = Array.Empty<uint>();
    public RegionIdentifier[] Buildables { get; set; } = Array.Empty<RegionIdentifier>();
    public TransformationDelta[] ObjectTransformations { get; set; } = Array.Empty<TransformationDelta>();
    public TransformationDelta[] BuildableTransformations { get; set; } = Array.Empty<TransformationDelta>();
    public Vector3[]? OriginalObjectScales { get; set; }
    public Vector3[]? ObjectScales { get; set; }
    public Vector3[]? OriginalBuildableScales { get; set; }
    public Vector3[]? BuildableScales { get; set; }
    public bool UseScale { get; set; }
    public bool IsFinal { get; set; }
    public float DeltaTime { get; set; }
    public void Apply()
    {
#if SERVER
        LevelObject?[]? objs = _objects;
#else
        LevelObject?[]? objs = null;
#endif
#if SERVER
        LevelBuildableObject?[]? buildables = _buildables;
#else
        LevelBuildableObject?[]? buildables = null;
#endif
        if (objs == null)
        {
            objs = Objects.Length == 0 ? Array.Empty<LevelObject>() : new LevelObject[Objects.Length];
            for (int i = 0; i < Objects.Length; ++i)
            {
                LevelObject? obj = LevelObjectUtil.FindObject(Objects[i]);
                objs[i] = obj;
                if (obj == null)
                    Logger.LogWarning($"Unknown object: # {Objects[i]}.");
            }
        }
        if (buildables == null)
        {
            buildables = Buildables.Length == 0 ? Array.Empty<LevelBuildableObject>() : new LevelBuildableObject[Buildables.Length];
            for (int i = 0; i < Buildables.Length; ++i)
            {
                LevelBuildableObject? buildable = LevelObjectUtil.GetBuildable(Buildables[i]);
                buildables[i] = buildable;
                if (buildable == null)
                    Logger.LogWarning($"Unknown buildable: {Buildables[i]}.");
            }
        }
        for (int i = 0; i < objs.Length; ++i)
        {
            if (objs[i] is not { } obj) continue;
            ref TransformationDelta t = ref ObjectTransformations[i];
            if (IsFinal)
            {
                Vector3 lclScale = !UseScale ? obj.transform.localScale : default;
                LevelObjects.registerTransformObject(obj.transform, t.Position, t.Rotation, UseScale ? ObjectScales![i] : lclScale,
                    t.OriginalPosition, t.OriginalRotation, UseScale ? OriginalObjectScales![i] : lclScale);
            }
            else
                t.ApplyTo(obj.transform, true);
        }
        for (int i = 0; i < buildables.Length; ++i)
        {
            if (buildables[i] is not { } buildable) continue;
            ref TransformationDelta t = ref BuildableTransformations[i];
            if (IsFinal)
            {
                Vector3 lclScale = !UseScale ? buildable.transform.localScale : default;
                LevelObjects.registerTransformObject(buildable.transform, t.Position, t.Rotation, UseScale ? ObjectScales![i] : lclScale,
                    t.OriginalPosition, t.OriginalRotation, UseScale ? OriginalObjectScales![i] : lclScale);
            }
            else
            {
                t.ApplyTo(buildable.transform, true);
            }
        }
    }
#if SERVER
    private LevelObject?[]? _objects;
    private LevelBuildableObject?[]? _buildables;
    public bool CheckCanApply()
    {
        if (!IsFinal && DevkitServerConfig.Config.RemoveCosmeticImprovements)
            return false;
        if (Permission.SuperuserPermission.Has(Instigator.m_SteamID, false))
            return true;
        if (Objects.Length > byte.MaxValue || Buildables.Length > byte.MaxValue)
            return false;
        _objects = Objects.Length == 0 ? Array.Empty<LevelObject>() : new LevelObject[Objects.Length];
        _buildables = Buildables.Length == 0 ? Array.Empty<LevelBuildableObject>() : new LevelBuildableObject[Objects.Length];
        for (int i = 0; i < Objects.Length; ++i)
        {
            LevelObject? obj = LevelObjectUtil.FindObject(Objects[i]);
            _objects[i] = obj;
            if (obj == null)
            {
                Logger.LogWarning($"Unknown object: # {Objects[i]}.");
                return false;
            }
            if (!LevelObjectUtil.CheckMovePermission(obj.instanceID, Instigator.m_SteamID))
                return false;
        }
        for (int i = 0; i < Buildables.Length; ++i)
        {
            RegionIdentifier identifier = Buildables[i];
            LevelBuildableObject? obj = LevelObjectUtil.GetBuildable(identifier);
            _buildables[i] = obj;
            if (obj == null)
            {
                Logger.LogWarning($"Unknown buildable: {Buildables[i]}.");
                return false;
            }
            if (!LevelObjectUtil.CheckMoveBuildablePermission(identifier, Instigator.m_SteamID))
                return false;
        }
        return true;
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        int objectCount = reader.ReadUInt8();
        int buildableCount = reader.ReadUInt8();
        byte flag = reader.ReadUInt8();
        UseScale = (flag & 1) != 0;
        IsFinal = (flag & 2) != 0;

        Objects = objectCount == 0 ? Array.Empty<uint>() : new uint[objectCount];
        Buildables = buildableCount == 0 ? Array.Empty<RegionIdentifier>() : new RegionIdentifier[buildableCount];
        ObjectTransformations = objectCount == 0 ? Array.Empty<TransformationDelta>() : new TransformationDelta[objectCount];
        BuildableTransformations = buildableCount == 0 ? Array.Empty<TransformationDelta>() : new TransformationDelta[buildableCount];

        if (UseScale)
        {
            ObjectScales = objectCount == 0 ? Array.Empty<Vector3>() : new Vector3[objectCount];
            BuildableScales = buildableCount == 0 ? Array.Empty<Vector3>() : new Vector3[buildableCount];
            OriginalObjectScales = objectCount == 0 ? Array.Empty<Vector3>() : new Vector3[objectCount];
            OriginalBuildableScales = buildableCount == 0 ? Array.Empty<Vector3>() : new Vector3[buildableCount];
        }
        for (int i = 0; i < objectCount; ++i)
        {
            Objects[i] = reader.ReadUInt32();
            ObjectTransformations[i] = new TransformationDelta(reader, !IsFinal);
            if (UseScale)
            {
                ObjectScales![i] = IsFinal ? reader.ReadVector3() : reader.ReadHalfPrecisionVector3();
                OriginalObjectScales![i] = IsFinal ? reader.ReadVector3() : reader.ReadHalfPrecisionVector3();
            }
        }
        for (int i = 0; i < buildableCount; ++i)
        {
            Buildables[i] = RegionIdentifier.Read(reader);
            BuildableTransformations[i] = new TransformationDelta(reader, !IsFinal);
            if (UseScale)
            {
                BuildableScales![i] = IsFinal ? reader.ReadVector3() : reader.ReadHalfPrecisionVector3();
                OriginalBuildableScales![i] = IsFinal ? reader.ReadVector3() : reader.ReadHalfPrecisionVector3();
            }
        }
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        int objectCount = Math.Min(byte.MaxValue, Objects.Length);
        int buildableCount = Math.Min(byte.MaxValue, Buildables.Length);

        writer.Write((byte)objectCount);
        writer.Write((byte)buildableCount);
        writer.Write((byte)((UseScale ? 1 : 0) | (IsFinal ? 2 : 0)));

        for (int i = 0; i < objectCount; ++i)
        {
            writer.Write(Objects[i]);
            if (IsFinal)
                ObjectTransformations[i].Write(writer);
            else
                ObjectTransformations[i].WriteHalfPrecision(writer);
            if (UseScale)
            {
                if (IsFinal)
                {
                    writer.Write(ObjectScales![i]);
                    writer.Write(OriginalObjectScales![i]);
                }
                else
                {
                    writer.WriteHalfPrecision(ObjectScales![i]);
                    writer.WriteHalfPrecision(OriginalObjectScales![i]);
                }
            }
        }
        for (int i = 0; i < buildableCount; ++i)
        {
            RegionIdentifier.Write(writer, Buildables[i]);
            if (IsFinal)
                BuildableTransformations[i].Write(writer);
            else
                BuildableTransformations[i].WriteHalfPrecision(writer);
            if (UseScale)
            {
                if (IsFinal)
                {
                    writer.Write(BuildableScales![i]);
                    writer.Write(OriginalBuildableScales![i]);
                }
                else
                {
                    writer.WriteHalfPrecision(BuildableScales![i]);
                    writer.WriteHalfPrecision(OriginalBuildableScales![i]);
                }
            }
        }
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
    public bool CheckCanApply()
    {
        EditorUser? user = UserManager.FromId(Instigator);
        if (user != null && user.Actions != null)
            user.Actions.DontSendPermissionMesssage();
        return false;
    }
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