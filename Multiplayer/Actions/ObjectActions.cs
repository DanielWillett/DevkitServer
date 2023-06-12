using DevkitServer.Util.Encoding;
using DevkitServer.Models;
#if SERVER
using DevkitServer.API.Permissions;
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
        });
    }
    private void OnMoveLevelObjectsFinal(in MoveLevelObjectsFinalProperties properties)
    {
        EditorActions.QueueAction(new MoveLevelObjectsFinalAction
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
            DeltaTime = properties.DeltaTime
        });
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
        for (int i = 0; i < Objects.Length; ++i)
        {
            LevelObject? obj = LevelObjectUtil.FindObject(Objects[i]);
            _objects[i] = obj;
            if (obj == null)
            {
                Logger.LogWarning($"Unknown object: # {Objects[i]}.");
                return false;
            }
            if (!LevelObjectUtil.CheckDeletePermission(obj.instanceID, Instigator.m_SteamID))
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
        Buildables = objectCount == 0 ? Array.Empty<RegionIdentifier>() : new RegionIdentifier[buildableCount];

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

        for (int i = 0; i < objectCount; ++i)
            RegionIdentifier.Write(writer, Buildables[i]);
    }
}

[Action(ActionType.MoveLevelObjectsFinal, 262, 0)]
[EarlyTypeInit]
public class MoveLevelObjectsFinalAction : IAction
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
            LevelObjects.registerTransformObject(obj.transform, t.Position, t.Rotation, UseScale ? ObjectScales![i] : obj.transform.localScale, 
                t.OriginalPosition, t.OriginalRotation, UseScale ? OriginalObjectScales![i] : obj.transform.localScale);
        }
        for (int i = 0; i < buildables.Length; ++i)
        {
            if (buildables[i] is not { } buildable) continue;
            ref TransformationDelta t = ref BuildableTransformations[i];
            LevelObjects.registerTransformObject(buildable.transform, t.Position, t.Rotation, UseScale ? ObjectScales![i] : buildable.transform.localScale,
                t.OriginalPosition, t.OriginalRotation, UseScale ? OriginalObjectScales![i] : buildable.transform.localScale);
        }
    }
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        int objectCount = reader.ReadUInt8();
        int buildableCount = reader.ReadUInt8();
        bool useScale = reader.ReadBool();

        Objects = objectCount == 0 ? Array.Empty<uint>() : new uint[objectCount];
        Buildables = objectCount == 0 ? Array.Empty<RegionIdentifier>() : new RegionIdentifier[buildableCount];
        ObjectTransformations = objectCount == 0 ? Array.Empty<TransformationDelta>() : new TransformationDelta[objectCount];
        BuildableTransformations = objectCount == 0 ? Array.Empty<TransformationDelta>() : new TransformationDelta[buildableCount];

        if (useScale)
        {
            ObjectScales = objectCount == 0 ? Array.Empty<Vector3>() : new Vector3[objectCount];
            OriginalObjectScales = objectCount == 0 ? Array.Empty<Vector3>() : new Vector3[objectCount];
            BuildableScales = buildableCount == 0 ? Array.Empty<Vector3>() : new Vector3[buildableCount];
            OriginalBuildableScales = buildableCount == 0 ? Array.Empty<Vector3>() : new Vector3[buildableCount];
        }
        for (int i = 0; i < objectCount; ++i)
        {
            Objects[i] = reader.ReadUInt32();
            ObjectTransformations[i] = new TransformationDelta(reader);
            if (useScale)
            {
                ObjectScales![i] = reader.ReadVector3();
                OriginalObjectScales![i] = reader.ReadVector3();
            }
        }
        for (int i = 0; i < buildableCount; ++i)
        {
            Buildables[i] = RegionIdentifier.Read(reader);
            BuildableTransformations[i] = new TransformationDelta(reader);
            if (useScale)
            {
                BuildableScales![i] = reader.ReadVector3();
                OriginalBuildableScales![i] = reader.ReadVector3();
            }
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
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        int objectCount = Math.Min(byte.MaxValue, Objects.Length);
        int buildableCount = Math.Min(byte.MaxValue, Buildables.Length);

        writer.Write((byte)objectCount);
        writer.Write((byte)buildableCount);
        writer.Write(UseScale);

        for (int i = 0; i < objectCount; ++i)
        {
            writer.Write(Objects[i]);
            ObjectTransformations[i].Write(writer);
            if (UseScale)
            {
                writer.Write(ObjectScales![i]);
                writer.Write(OriginalObjectScales![i]);
            }
        }
        for (int i = 0; i < buildableCount; ++i)
        {
            RegionIdentifier.Write(writer, Buildables[i]);
            BuildableTransformations[i].Write(writer);
            if (UseScale)
            {
                writer.Write(BuildableScales![i]);
                writer.Write(OriginalBuildableScales![i]);
            }
        }
    }
}