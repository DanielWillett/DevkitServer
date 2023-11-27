using DevkitServer.API;
using DevkitServer.Multiplayer.Levels;
using DevkitServer.Util.Encoding;
using SDG.Framework.Devkit;
using SDG.Framework.Foliage;
#if SERVER
using DevkitServer.API.Permissions;
using DevkitServer.Core.Permissions;
using DevkitServer.Players;
#endif

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
            ClientEvents.OnAddFoliage += OnAddFoliage;
            ClientEvents.OnRemoveFoliage += OnRemoveFoliage;
            ClientEvents.OnRemoveResourceSpawnpointFoliage += OnRemoveResourceSpawnpointFoliage;
            ClientEvents.OnRemoveLevelObjectFoliage += OnRemoveLevelObjectFoliage;
        }
#endif
    }

    public void Unsubscribe()
    {
#if CLIENT
        if (EditorActions.IsOwner)
        {
            ClientEvents.OnAddFoliage -= OnAddFoliage;
            ClientEvents.OnRemoveFoliage -= OnRemoveFoliage;
            ClientEvents.OnRemoveResourceSpawnpointFoliage -= OnRemoveResourceSpawnpointFoliage;
            ClientEvents.OnRemoveLevelObjectFoliage -= OnRemoveLevelObjectFoliage;
        }
#endif
    }
#if CLIENT
    private void OnAddFoliage(in AddFoliageProperties properties)
    {
        EditorActions.QueueAction(new AddFoliageToSurfaceAction
        {
            Position = properties.Position,
            Rotation = properties.Rotation,
            Scale = properties.Scale,
            ClearWhenBaked = properties.ClearWhenBaked,
            FoliageAsset = new AssetReference<FoliageInfoAsset>(properties.Asset.GUID),
            DeltaTime = properties.DeltaTime
        });
    }
    private void OnRemoveFoliage(in RemoveFoliageProperties properties)
    {
        if (properties.SampleCount == 0)
            return;
        EditorActions.QueueAction(new RemoveFoliageInstancesAction
        {
            CoordinateX = properties.Tile.coord.x,
            CoordinateY = properties.Tile.coord.y,
            FoliageAsset = properties.FoliageInstances.assetReference,
            BrushPosition = properties.BrushPosition,
            BrushRadius = properties.BrushRadius,
            BrushFalloff = properties.BrushFalloff,
            AllowRemoveBaked = properties.AllowRemovingBakedFoliage,
            SampleCount = properties.SampleCount,
            DeltaTime = properties.DeltaTime
        });
    }
    private void OnRemoveResourceSpawnpointFoliage(in RemoveResourceSpawnpointFoliageProperties properties)
    {
        EditorActions.QueueAction(new RemoveResourceSpawnpointAction
        {
            ResourcePosition = properties.Spawnpoint.point,
            FoliageAsset = properties.Spawnpoint.asset.getReferenceTo<ResourceAsset>(),
            DeltaTime = properties.DeltaTime
        });
    }
    private void OnRemoveLevelObjectFoliage(in RemoveLevelObjectFoliageProperties properties)
    {
        if (LevelObjectNetIdDatabase.TryGetObjectNetId(properties.LevelObject, out NetId netId))
        {
            EditorActions.QueueAction(new DeleteLevelObjectsAction
            {
                NetIds = new NetId[] { netId },
                DeltaTime = properties.DeltaTime
            });
        }
        else
        {
            Logger.LogWarning("Unknown region for object " + properties.LevelObject.asset?.FriendlyName.Format() + " #" + properties.LevelObject.instanceID + ".");
        }
    }
#endif

}

[Action(DevkitServerActionType.AddFoliageToSurface, 45, 16)]
[EarlyTypeInit]
public sealed class AddFoliageToSurfaceAction : IServersideAction, IAssetAction
{
    private static readonly Action<FoliageInfoAsset, Vector3, Quaternion, Vector3, bool>? ExecuteAddFoliage
        = Accessor.GenerateInstanceCaller<FoliageInfoAsset, Action<FoliageInfoAsset, Vector3, Quaternion, Vector3, bool>>("addFoliage", throwOnError: false, allowUnsafeTypeBinding: true);
    public DevkitServerActionType Type => DevkitServerActionType.AddFoliageToSurface;
    public CSteamID Instigator { get; set; }
    public float DeltaTime { get; set; }
    public AssetReference<FoliageInfoAsset> FoliageAsset { get; set; }
    Guid IAssetAction.Asset
    {
        get => FoliageAsset.GUID;
        set => FoliageAsset = new AssetReference<FoliageInfoAsset>(value);
    }
    public Vector3 Position { get; set; }
    public Quaternion Rotation { get; set; }
    public Vector3 Scale { get; set; }
    public bool ClearWhenBaked { get; set; }
    public uint SendBackInstanceId { get; set; }
    public NetId NetId { get; set; }
#if SERVER
    public bool IsObject { get; set; }
#endif
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
            if (asset is FoliageObjectInfoAsset objAsset
                && Regions.tryGetCoordinate(Position, out byte x, out byte y)
                && LevelObjects.objects[x, y] is { Count: > 0 and <= ushort.MaxValue + 1 } region)
            {
                LevelObject obj = ObjectManager.getObject(x, y, (ushort)(region.Count - 1));
                if (obj == null || obj.asset.GUID != objAsset.obj.GUID)
                {
                    Logger.LogWarning($"Unable to find object placed by {Instigator.Format()} in add foliage action.");
                    return;
                }
                if (!NetId.IsNull())
                {
#if CLIENT
                    if (Instigator.m_SteamID == Provider.client.m_SteamID)
                        LevelObjectResponsibilities.Set(obj.instanceID, false);
#else
                    LevelObjectResponsibilities.Set(obj.instanceID, Instigator.m_SteamID, false);
#endif
                    LevelObjectNetIdDatabase.RegisterObject(obj, NetId);
                }
            }
        }
        else
        {
            Logger.LogWarning("Unknown foliage asset: " + FoliageAsset.Format() + ".");
        }
    }
#if SERVER
    public bool CheckCanApply()
    {
        if (!VanillaPermissions.EditFoliage.Has(Instigator.m_SteamID))
            return false;
        
        if (FoliageAsset.Find() is { } asset && asset is FoliageObjectInfoAsset)
        {
            IsObject = true;
            NetId = NetIdRegistry.Claim();
            EditorUser? user = UserManager.FromId(Instigator.m_SteamID);
            if (user != null)
                LevelObjectNetIdDatabase.SendBindObject.Invoke(user.Connection, SendBackInstanceId, NetId);
        }
        return true;
    }
#endif
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        bool nonUniformScale = Scale != Vector3.one;
        byte flag = (byte)((nonUniformScale ? 2 : 0) | (ClearWhenBaked ? 1 : 0));
#if SERVER
        if (IsObject) flag |= 4;
#endif
        writer.Write(flag);
        writer.Write(Position);
        writer.Write(Rotation);
        if (nonUniformScale)
            writer.Write(Scale);
#if CLIENT
        writer.Write(SendBackInstanceId);
#endif
#if SERVER
        if (IsObject)
            writer.Write(NetId);
#endif
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
#if SERVER
        SendBackInstanceId = reader.ReadUInt32();
#endif
#if CLIENT
        bool isObject = (flag & 4) != 0;
        NetId = isObject ? reader.ReadNetId() : NetId.INVALID;
#endif
    }

    public int CalculateSize() => 37 + (Scale != Vector3.one ? 12 : 0)
#if SERVER
      + (IsObject ? 4 : 0)
#endif
    ;
}

[Action(DevkitServerActionType.RemoveFoliageInstances, 16, 36)]
[EarlyTypeInit]
public sealed class RemoveFoliageInstancesAction : IAction, ICoordinatesAction, IBrushRadiusAction, IBrushFalloffAction, IAssetAction
{
    public DevkitServerActionType Type => DevkitServerActionType.RemoveFoliageInstances;
    public CSteamID Instigator { get; set; }
    public Vector3 BrushPosition { get; set; }
    public AssetReference<FoliageInstancedMeshInfoAsset> FoliageAsset { get; set; }
    Guid IAssetAction.Asset
    {
        get => FoliageAsset.GUID;
        set => FoliageAsset = new AssetReference<FoliageInstancedMeshInfoAsset>(value);
    }
    public float DeltaTime { get; set; }
    public float BrushRadius { get; set; }
    public float BrushFalloff { get; set; }
    public int CoordinateX { get; set; }
    public int CoordinateY { get; set; }
    public int SampleCount { get; set; }
    public bool AllowRemoveBaked { get; set; }
    public void Apply()
    {
        FoliageTile tile = FoliageSystem.getTile(new FoliageCoord(CoordinateX, CoordinateY));

        if (tile == null)
        {
            Logger.LogWarning("Unknown foliage tile: (" + CoordinateX.Format() + ", " + CoordinateY.Format() + ").");
            return;
        }
        if (!tile.instances.TryGetValue(FoliageAsset, out FoliageInstanceList list))
        {
            Logger.LogWarning("Tile missing foliage " + (FoliageAsset.Find()?.FriendlyName ?? "NULL") + ": (" + CoordinateX.Format() + ", " + CoordinateY.Format() + ").");
            return;
        }
        bool hierarchyIsDirty = false;
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
                            hierarchyIsDirty = true;
                        }
                    }
                }
            }
        }
        if (hierarchyIsDirty)
            LevelHierarchy.MarkDirty();
    }
#if SERVER
    public bool CheckCanApply()
    {
        return VanillaPermissions.EditFoliage.Has(Instigator.m_SteamID);
    }
#endif
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write(BrushPosition);
    }
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        BrushPosition = reader.ReadVector3();
    }
    public int CalculateSize() => 16;
}

[Action(DevkitServerActionType.RemoveResourceSpawnpoint, 16, 16)]
[EarlyTypeInit]
public sealed class RemoveResourceSpawnpointAction : IAction, IAssetAction
{
    public DevkitServerActionType Type => DevkitServerActionType.RemoveResourceSpawnpoint;
    public CSteamID Instigator { get; set; }
    public float DeltaTime { get; set; }
    public Vector3 ResourcePosition { get; set; }
    public AssetReference<ResourceAsset> FoliageAsset { get; set; }
    Guid IAssetAction.Asset
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
                if (sp.point.IsNearlyEqual(pos))
                {
                    if (sp.asset.GUID == FoliageAsset.GUID)
                    {
                        sp.destroy();
                        region.RemoveAt(i);
                        return;
                    }

                    Logger.LogWarning("Found matching position but different GUID: " + (FoliageAsset.Find()?.FriendlyName ?? FoliageAsset.ToString()).Format() + " vs " + sp.asset.FriendlyName.Format() + ".");
                }
            }

            Logger.LogWarning("Resource not found: " + (FoliageAsset.Find()?.FriendlyName ?? FoliageAsset.ToString()).Format() + " at " + ResourcePosition.Format() + ".");
        }
        else
        {
            Logger.LogWarning("Resource out of bounds: " + (FoliageAsset.Find()?.FriendlyName ?? FoliageAsset.ToString()).Format() + " at " + ResourcePosition.Format() + ".");
        }
    }
#if SERVER
    public bool CheckCanApply()
    {
        return VanillaPermissions.EditFoliage.Has(Instigator.m_SteamID);
    }
#endif
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
    public int CalculateSize() => 16;
}