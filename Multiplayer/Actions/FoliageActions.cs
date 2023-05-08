using DevkitServer.Patches;
using DevkitServer.Util.Encoding;
using SDG.Framework.Devkit;
using SDG.Framework.Devkit.Tools;
using SDG.Framework.Foliage;

namespace DevkitServer.Multiplayer.Actions;
public sealed class FoliageActions
{
    public EditorActions EditorActions { get; }
    internal FoliageActions(EditorActions actions)
    {
        EditorActions = actions;
    }
    public void Subscribe()
    {
#if CLIENT
        if (EditorActions.IsOwner)
        {
            ClientEvents.OnFoliageAdded += OnFoliageAdded;
            ClientEvents.OnFoliageRemoved += OnFoliageRemoved;
            ClientEvents.OnResourceSpawnpointRemoved += OnResourceSpawnpointRemoved;
            ClientEvents.OnLevelObjectRemoved += OnLevelObjectRemoved;
        }
#endif
    }

    public void Unsubscribe()
    {
#if CLIENT
        if (EditorActions.IsOwner)
        {
            ClientEvents.OnFoliageAdded -= OnFoliageAdded;
            ClientEvents.OnFoliageRemoved -= OnFoliageRemoved;
            ClientEvents.OnResourceSpawnpointRemoved -= OnResourceSpawnpointRemoved;
            ClientEvents.OnLevelObjectRemoved -= OnLevelObjectRemoved;
        }
#endif
    }
#if CLIENT
    private void OnFoliageAdded(FoliageInfoAsset asset, Vector3 position, Quaternion rotation, Vector3 scale, bool clearWhenBaked)
    {
        EditorActions.QueueAction(new AddFoliageToSurfaceAction
        {
            Position = position,
            Rotation = rotation,
            Scale = scale,
            ClearWhenBaked = clearWhenBaked,
            FoliageAsset = new AssetReference<FoliageInfoAsset>(asset.GUID),
            DeltaTime = Time.deltaTime
        });
    }
    private void OnFoliageRemoved(Vector3 brushPosition, FoliageTile foliageTile, FoliageInstanceList list, float sqrBrushRadius, float sqrBrushFalloffRadius, bool allowRemoveBaked, int sampleCount)
    {
        if (sampleCount == 0)
            return;
        EditorActions.QueueAction(new RemoveFoliageInstancesAction
        {
            Coordinates = foliageTile.coord,
            FoliageAsset = list.assetReference,
            BrushPosition = brushPosition,
            BrushRadius = DevkitFoliageToolOptions.instance.brushRadius,
            BrushFalloff = DevkitFoliageToolOptions.instance.brushFalloff,
            AllowRemoveBaked = allowRemoveBaked,
            SampleCount = sampleCount,
            DeltaTime = Time.deltaTime
        });
    }
    private void OnResourceSpawnpointRemoved(ResourceSpawnpoint spawnpoint)
    {
        EditorActions.QueueAction(new RemoveResourceSpawnpointAction
        {
            ResourcePosition = spawnpoint.point,
            FoliageAsset = spawnpoint.asset.getReferenceTo<ResourceAsset>(),
            DeltaTime = Time.deltaTime
        });
    }
    private void OnLevelObjectRemoved(Vector3 position, LevelObject obj)
    {
        if (Regions.tryGetCoordinate(position, out byte x, out byte y))
        {
            EditorActions.QueueAction(new RemoveLevelObjectAction
            {
                CoordinateX = x,
                CoordinateY = y,
                InstanceId = obj.instanceID,
                DeltaTime = Time.deltaTime
            });
        }
        else
        {
            Logger.LogWarning("Unknown region for object " + obj.asset?.FriendlyName.Format() + " #" + obj.instanceID + ".");
        }
    }
#endif

}

[Action(ActionType.AddFoliageToSurface)]
[EarlyTypeInit]
public sealed class AddFoliageToSurfaceAction : IAction, IAsset
{
    private static readonly Action<FoliageInfoAsset, Vector3, Quaternion, Vector3, bool>? ExecuteAddFoliage
        = Accessor.GenerateInstanceCaller<FoliageInfoAsset, Action<FoliageInfoAsset, Vector3, Quaternion, Vector3, bool>>("addFoliage", throwOnError: false);
    public ActionType Type => ActionType.AddFoliageToSurface;
    public CSteamID Instigator { get; set; }
    public float DeltaTime { get; set; }
    public AssetReference<FoliageInfoAsset> FoliageAsset { get; set; }
    Guid IAsset.Asset
    {
        get => FoliageAsset.GUID;
        set => FoliageAsset = new AssetReference<FoliageInfoAsset>(value);
    }
    public Vector3 Position { get; set; }
    public Quaternion Rotation { get; set; }
    public Vector3 Scale { get; set; }
    public bool ClearWhenBaked { get; set; }
    public void Apply()
    {
        if (ExecuteAddFoliage == null)
        {
            Logger.LogWarning("Unable to add foliage asset: " + FoliageAsset.Format() + ", reflection failure on load.");
            return;
        }
        if (FoliageAsset.Find() is { } asset)
        {
            ExecuteAddFoliage(asset, Position, Rotation, Scale, ClearWhenBaked);
        }
        else
        {
            Logger.LogWarning("Unknown foliage asset: " + FoliageAsset.Format() + ".");
        }
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        bool nonUniformScale = Scale == Vector3.one;
        byte flag = (byte)((nonUniformScale ? 2 : 0) | (ClearWhenBaked ? 1 : 0));
        writer.Write(flag);
        writer.Write(Position);
        writer.Write(Rotation);
        if (nonUniformScale)
            writer.Write(Scale);
    }
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        byte flag = reader.ReadUInt8();
        ClearWhenBaked = (flag & 1) != 0;
        bool nonUniformScale = (flag & 2) != 0;
        Position = reader.ReadVector3();
        Rotation = reader.ReadQuaternion();
        Scale = nonUniformScale ? reader.ReadVector3() : Vector3.one;
    }
}

[Action(ActionType.RemoveFoliageInstances)]
[EarlyTypeInit]
public sealed class RemoveFoliageInstancesAction : IAction, ICoordinates, IBrushRadius, IBrushFalloff, IAsset
{
    public ActionType Type => ActionType.RemoveFoliageInstances;
    public CSteamID Instigator { get; set; }
    public Vector3 BrushPosition { get; set; }
    public AssetReference<FoliageInstancedMeshInfoAsset> FoliageAsset { get; set; }
    Guid IAsset.Asset
    {
        get => FoliageAsset.GUID;
        set => FoliageAsset = new AssetReference<FoliageInstancedMeshInfoAsset>(value);
    }
    public float DeltaTime { get; set; }
    public float BrushRadius { get; set; }
    public float BrushFalloff { get; set; }
    public FoliageCoord Coordinates { get; set; }
    public int SampleCount { get; set; }
    public bool AllowRemoveBaked { get; set; }
    int ICoordinates.CoordinateX
    {
        get => Coordinates.x;
        set => Coordinates = Coordinates with { x = value };
    }
    int ICoordinates.CoordinateY
    {
        get => Coordinates.y;
        set => Coordinates = Coordinates with { y = value };
    }
    public void Apply()
    {
        FoliageTile tile = FoliageSystem.getTile(Coordinates);

        if (tile == null)
        {
            Logger.LogWarning("Unknown foliage tile: " + Coordinates + ".");
            return;
        }
        if (!tile.instances.TryGetValue(FoliageAsset, out FoliageInstanceList list))
        {
            Logger.LogWarning("Tile missing foliage " + (FoliageAsset.Find()?.FriendlyName ?? "NULL") + ": " + Coordinates.Format() + ".");
            return;
        }
        bool flag1 = false;
        float sqrBrushRadius = BrushRadius * BrushRadius;
        float sqrBrushFalloffRadius = sqrBrushRadius * BrushFalloff * BrushFalloff;
        int sampleCount = SampleCount;

        // FoliageEditor.removeInstances
        for (int index1 = list.matrices.Count - 1; index1 >= 0; --index1)
        {
            List<Matrix4x4> matrix = list.matrices[index1];
            List<bool> boolList = list.clearWhenBaked[index1];
            for (int index2 = matrix.Count - 1; index2 >= 0; --index2)
            {
                if (!boolList[index2] || AllowRemoveBaked)
                {
                    Vector3 position = matrix[index2].GetPosition();
                    float sqrMagnitude = (position - BrushPosition).sqrMagnitude;
                    if (sqrMagnitude < sqrBrushRadius)
                    {
                        bool outOfFalloff = sqrMagnitude < sqrBrushFalloffRadius;
                        if (outOfFalloff && sampleCount > 0)
                        {
                            tile.removeInstance(list, index1, index2);
                            --sampleCount;
                            flag1 = true;
                        }
                    }
                }
            }
        }
        if (!flag1)
            return;
        LevelHierarchy.MarkDirty();
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
    }
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
    }
}

[Action(ActionType.RemoveResourceSpawnpoint)]
[EarlyTypeInit]
public sealed class RemoveResourceSpawnpointAction : IAction, IAsset
{
    public ActionType Type => ActionType.RemoveResourceSpawnpoint;
    public CSteamID Instigator { get; set; }
    public float DeltaTime { get; set; }
    public Vector3 ResourcePosition { get; set; }
    public AssetReference<ResourceAsset> FoliageAsset { get; set; }
    Guid IAsset.Asset
    {
        get => FoliageAsset.GUID;
        set => FoliageAsset = new AssetReference<ResourceAsset>(value);
    }
    public void Apply()
    {
        Vector3 pos = ResourcePosition;
        if (Regions.tryGetCoordinate(pos, out byte x, out byte y))
        {
            List<ResourceSpawnpoint> region = LevelGround.trees[x, y];
            for (int i = 0; i < region.Count; ++i)
            {
                ResourceSpawnpoint sp = region[i];
                if (sp.point.IsNearlyEqual(pos) && sp.asset.GUID == FoliageAsset.GUID)
                {
                    sp.destroy();
                    region.RemoveAt(i);
                    return;
                }
            }

            Logger.LogWarning("Resource not found: " + (FoliageAsset.Find()?.FriendlyName ?? "NULL").Format() + " at " + ResourcePosition.Format() + ".");
        }
        else
        {
            Logger.LogWarning("Resource out of bounds: " + (FoliageAsset.Find()?.FriendlyName ?? "NULL").Format() + " at " + ResourcePosition.Format() + ".");
        }
    }

    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write(ResourcePosition);
    }
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        ResourcePosition = reader.ReadVector3();
    }
}

[Action(ActionType.RemoveLevelObject)]
[EarlyTypeInit]
public sealed class RemoveLevelObjectAction : IAction, ICoordinates
{
    public ActionType Type => ActionType.RemoveLevelObject;
    public CSteamID Instigator { get; set; }
    public float DeltaTime { get; set; }
    public uint InstanceId { get; set; }
    public int CoordinateX { get; set; }
    public int CoordinateY { get; set; }
    public void Apply()
    {
        List<LevelObject> region = LevelObjects.objects[CoordinateX, CoordinateY];
        for (int i = 0; i < region.Count; ++i)
        {
            LevelObject obj = region[i];
            if (obj.instanceID == InstanceId)
            {
                LevelObjects.removeObject(obj.transform);
                return;
            }
        }

        Logger.LogWarning("Object not found: #" + InstanceId.Format() + " (" + CoordinateX.Format() + ", " + CoordinateY.Format() + ").");
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write(InstanceId);
    }
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        InstanceId = reader.ReadUInt32();
    }
}