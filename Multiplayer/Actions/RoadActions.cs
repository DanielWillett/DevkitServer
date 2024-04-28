using DanielWillett.SpeedBytes;
using DanielWillett.SpeedBytes.Unity;
using DevkitServer.API;
using DevkitServer.Models;
using DevkitServer.Multiplayer.Levels;
#if SERVER
using DevkitServer.API.Permissions;
using DevkitServer.Core.Permissions;
#endif

namespace DevkitServer.Multiplayer.Actions;
public sealed class RoadActions
{
    internal const string Source = "ROAD ACTIONS";
    public EditorActions EditorActions { get; }
    internal RoadActions(EditorActions actions)
    {
        EditorActions = actions;
    }
    public void Subscribe()
    {
#if CLIENT
        if (EditorActions.IsOwner)
        {
            ClientEvents.OnSetRoadIsLoop += OnSetRoadIsLoop;
            ClientEvents.OnSetRoadMaterial += OnSetRoadMaterial;
            ClientEvents.OnSetRoadVertexIgnoreTerrain += OnSetRoadVertexIgnoreTerrain;
            ClientEvents.OnSetRoadVertexVerticalOffset += OnSetRoadVertexVerticalOffset;
            ClientEvents.OnSetRoadVertexTangentHandleMode += OnSetRoadVertexTangentHandleMode;
            ClientEvents.OnSetRoadMaterialWidth += OnSetRoadMaterialWidth;
            ClientEvents.OnSetRoadMaterialHeight += OnSetRoadMaterialHeight;
            ClientEvents.OnSetRoadMaterialDepth += OnSetRoadMaterialDepth;
            ClientEvents.OnSetRoadMaterialVerticalOffset += OnSetRoadMaterialVerticalOffset;
            ClientEvents.OnSetRoadMaterialIsConcrete += OnSetRoadMaterialIsConcrete;

            ClientEvents.OnMoveRoadVertex += OnMoveRoadVertex;
            ClientEvents.OnMoveRoadTangentHandle += OnMoveRoadTangentHandle;
            ClientEvents.OnDeleteRoadVertex += OnDeleteRoadVertex;
            ClientEvents.OnDeleteRoad += OnDeleteRoad;
        }
#endif
    }

    public void Unsubscribe()
    {
#if CLIENT
        if (EditorActions.IsOwner)
        {
            ClientEvents.OnSetRoadIsLoop -= OnSetRoadIsLoop;
            ClientEvents.OnSetRoadMaterial -= OnSetRoadMaterial;
            ClientEvents.OnSetRoadVertexIgnoreTerrain -= OnSetRoadVertexIgnoreTerrain;
            ClientEvents.OnSetRoadVertexVerticalOffset -= OnSetRoadVertexVerticalOffset;
            ClientEvents.OnSetRoadVertexTangentHandleMode -= OnSetRoadVertexTangentHandleMode;
            ClientEvents.OnSetRoadMaterialWidth -= OnSetRoadMaterialWidth;
            ClientEvents.OnSetRoadMaterialHeight -= OnSetRoadMaterialHeight;
            ClientEvents.OnSetRoadMaterialDepth -= OnSetRoadMaterialDepth;
            ClientEvents.OnSetRoadMaterialVerticalOffset -= OnSetRoadMaterialVerticalOffset;
            ClientEvents.OnSetRoadMaterialIsConcrete -= OnSetRoadMaterialIsConcrete;

            ClientEvents.OnMoveRoadVertex -= OnMoveRoadVertex;
            ClientEvents.OnMoveRoadTangentHandle -= OnMoveRoadTangentHandle;
            ClientEvents.OnDeleteRoadVertex -= OnDeleteRoadVertex;
            ClientEvents.OnDeleteRoad -= OnDeleteRoad;
        }
#endif
    }

#if CLIENT
    private void OnSetRoadIsLoop(in SetRoadIsLoopProperties properties)
    {
        EditorActions.QueueAction(new SetRoadIsLoopAction
        {
            InstanceId = properties.RoadNetId.id,
            IsLoop = properties.IsLoop,
            DeltaTime = properties.DeltaTime
        }, true);
    }
    private void OnSetRoadMaterial(in SetRoadMaterialProperties properties)
    {
        EditorActions.QueueAction(new SetRoadMaterialAction
        {
            InstanceId = properties.RoadNetId.id,
            MaterialIndex = properties.MaterialIndex,
            DeltaTime = properties.DeltaTime
        }, true);
    }
    private void OnSetRoadVertexIgnoreTerrain(in SetRoadVertexIgnoreTerrainProperties properties)
    {
        EditorActions.QueueAction(new SetRoadVertexIgnoreTerrainAction
        {
            InstanceId = properties.VertexNetId.id,
            IgnoreTerrain = properties.IgnoreTerrain,
            DeltaTime = properties.DeltaTime
        }, true);
    }
    private void OnSetRoadVertexVerticalOffset(in SetRoadVertexVerticalOffsetProperties properties)
    {
        EditorActions.QueueAction(new SetRoadVertexVerticalOffsetAction
        {
            InstanceId = properties.VertexNetId.id,
            VerticalOffset = properties.VerticalOffset,
            DeltaTime = properties.DeltaTime
        }, true);
    }
    private void OnSetRoadVertexTangentHandleMode(in SetRoadVertexTangentHandleModeProperties properties)
    {
        EditorActions.QueueAction(new SetRoadVertexTangentHandleModeAction
        {
            InstanceId = properties.VertexNetId.id,
            TangentHandleMode = properties.TangentHandleMode,
            DeltaTime = properties.DeltaTime
        }, true);
    }
    private void OnSetRoadMaterialWidth(in SetRoadMaterialWidthProperties properties)
    {
        EditorActions.QueueAction(new SetRoadMaterialWidthAction
        {
            InstanceId = properties.MaterialIndex,
            Width = properties.Width,
            DeltaTime = properties.DeltaTime
        }, true);
    }
    private void OnSetRoadMaterialHeight(in SetRoadMaterialHeightProperties properties)
    {
        EditorActions.QueueAction(new SetRoadMaterialHeightAction
        {
            InstanceId = properties.MaterialIndex,
            Height = properties.Height,
            DeltaTime = properties.DeltaTime
        }, true);
    }
    private void OnSetRoadMaterialDepth(in SetRoadMaterialDepthProperties properties)
    {
        EditorActions.QueueAction(new SetRoadMaterialDepthAction
        {
            InstanceId = properties.MaterialIndex,
            Depth = properties.Depth,
            DeltaTime = properties.DeltaTime
        }, true);
    }
    private void OnSetRoadMaterialVerticalOffset(in SetRoadMaterialVerticalOffsetProperties properties)
    {
        EditorActions.QueueAction(new SetRoadMaterialVerticalOffsetAction
        {
            InstanceId = properties.MaterialIndex,
            VerticalOffset = properties.VerticalOffset,
            DeltaTime = properties.DeltaTime
        }, true);
    }
    private void OnSetRoadMaterialIsConcrete(in SetRoadMaterialIsConcreteProperties properties)
    {
        EditorActions.QueueAction(new SetRoadMaterialIsConcreteAction
        {
            InstanceId = properties.MaterialIndex,
            IsConcrete = properties.IsConcrete,
            DeltaTime = properties.DeltaTime
        }, true);
    }
    private void OnMoveRoadVertex(in MoveRoadVertexProperties properties)
    {
        EditorActions.QueueAction(new MoveRoadVertexAction
        {
            InstanceId = properties.VertexNetId.id,
            Position = properties.Position,
            DeltaTime = properties.DeltaTime
        }, true);
    }

    private void OnMoveRoadTangentHandle(in MoveRoadTangentHandleProperties properties)
    {
        EditorActions.QueueAction(new MoveRoadTangentHandleAction
        {
            InstanceId = properties.VertexNetId.id,
            Position = properties.Position,
            Handle = properties.Handle,
            DeltaTime = properties.DeltaTime
        }, true);
    }

    private void OnDeleteRoadVertex(in DeleteRoadVertexProperties properties)
    {
        EditorActions.QueueAction(new DeleteRoadVertexAction
        {
            InstanceId = properties.VertexNetId.id,
            DeltaTime = properties.DeltaTime
        }, true);
    }

    private void OnDeleteRoad(in DeleteRoadProperties properties)
    {
        EditorActions.QueueAction(new DeleteRoadAction
        {
            InstanceId = properties.RoadNetId.id,
            DeltaTime = properties.DeltaTime
        }, true);
    }
#endif
}

[Action(DevkitServerActionType.SetRoadIsLoop, 5, 4)]
public sealed class SetRoadIsLoopAction : IReplacableAction, IInstanceIdAction
{
    public DevkitServerActionType Type => DevkitServerActionType.SetRoadIsLoop;
    public CSteamID Instigator { get; set; }
    public uint InstanceId { get; set; }
    public float DeltaTime { get; set; }
    public bool IsLoop { get; set; }
    public bool TryReplaceFrom(IReplacableAction action)
    {
        SetRoadIsLoopAction a = (SetRoadIsLoopAction)action;
        if (a.InstanceId != InstanceId)
            return false;

        IsLoop = a.IsLoop;
        return true;
    }
    public void Apply()
    {
        NetId netId = new NetId(InstanceId);
        if (!RoadNetIdDatabase.TryGetRoad(netId, out int roadIndex))
        {
            Logger.DevkitServer.LogWarning(RoadActions.Source, $"Unknown road with NetId: {netId.Format()}.");
            return;
        }

        RoadUtil.SetIsLoopLocal(roadIndex, IsLoop);
    }
#if SERVER
    public bool CheckCanApply()
    {
        NetId netId = new NetId(InstanceId);
        if (!RoadNetIdDatabase.TryGetRoad(netId, out int _))
        {
            Logger.DevkitServer.LogWarning(RoadActions.Source, $"Unknown road with NetId: {netId.Format()}.");
            return false;
        }
        return VanillaPermissions.EditRoads.Has(Instigator.m_SteamID, true);
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        IsLoop = reader.ReadBool();
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write(IsLoop);
    }
    public int CalculateSize() => 5;
}
[Action(DevkitServerActionType.SetRoadMaterial, 5, 4)]
public sealed class SetRoadMaterialAction : IReplacableAction, IInstanceIdAction
{
    public DevkitServerActionType Type => DevkitServerActionType.SetRoadMaterial;
    public CSteamID Instigator { get; set; }
    public uint InstanceId { get; set; }
    public float DeltaTime { get; set; }
    public byte MaterialIndex { get; set; }
    public bool TryReplaceFrom(IReplacableAction action)
    {
        SetRoadMaterialAction a = (SetRoadMaterialAction)action;
        if (a.InstanceId != InstanceId)
            return false;

        MaterialIndex = a.MaterialIndex;
        return true;
    }
    public void Apply()
    {
        NetId netId = new NetId(InstanceId);
        if (!RoadNetIdDatabase.TryGetRoad(netId, out int roadIndex))
        {
            Logger.DevkitServer.LogWarning(RoadActions.Source, $"Unknown road with NetId: {netId.Format()}.");
            return;
        }

        RoadUtil.SetMaterialLocal(roadIndex, MaterialIndex);
    }
#if SERVER
    public bool CheckCanApply()
    {
        NetId netId = new NetId(InstanceId);
        if (!RoadNetIdDatabase.TryGetRoad(netId, out int _))
        {
            Logger.DevkitServer.LogWarning(RoadActions.Source, $"Unknown road with NetId: {netId.Format()}.");
            return false;
        }
        return VanillaPermissions.EditRoads.Has(Instigator.m_SteamID, true);
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        MaterialIndex = reader.ReadUInt8();
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write(MaterialIndex);
    }
    public int CalculateSize() => 5;
}

[Action(DevkitServerActionType.SetRoadVertexIgnoreTerrain, 5, 4)]
public sealed class SetRoadVertexIgnoreTerrainAction : IReplacableAction, IInstanceIdAction
{
    public DevkitServerActionType Type => DevkitServerActionType.SetRoadVertexIgnoreTerrain;
    public CSteamID Instigator { get; set; }
    public uint InstanceId { get; set; }
    public float DeltaTime { get; set; }
    public bool IgnoreTerrain { get; set; }
    public bool TryReplaceFrom(IReplacableAction action)
    {
        SetRoadVertexIgnoreTerrainAction a = (SetRoadVertexIgnoreTerrainAction)action;
        if (a.InstanceId != InstanceId)
            return false;

        IgnoreTerrain = a.IgnoreTerrain;
        return true;
    }
    public void Apply()
    {
        NetId netId = new NetId(InstanceId);
        if (!RoadNetIdDatabase.TryGetVertex(netId, out RoadVertexIdentifier vertex))
        {
            Logger.DevkitServer.LogWarning(RoadActions.Source, $"Unknown vertex with NetId: {netId.Format()}.");
            return;
        }

        RoadUtil.SetVertexIgnoreTerrainLocal(vertex.Road, vertex.Vertex, IgnoreTerrain);
    }
#if SERVER
    public bool CheckCanApply()
    {
        NetId netId = new NetId(InstanceId);
        if (!RoadNetIdDatabase.TryGetVertex(netId, out RoadVertexIdentifier _))
        {
            Logger.DevkitServer.LogWarning(RoadActions.Source, $"Unknown vertex with NetId: {netId.Format()}.");
            return false;
        }
        return VanillaPermissions.EditRoads.Has(Instigator.m_SteamID, true);
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        IgnoreTerrain = reader.ReadBool();
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write(IgnoreTerrain);
    }
    public int CalculateSize() => 5;
}

[Action(DevkitServerActionType.SetRoadVertexVerticalOffset, 8, 4)]
public sealed class SetRoadVertexVerticalOffsetAction : IReplacableAction, IInstanceIdAction
{
    public DevkitServerActionType Type => DevkitServerActionType.SetRoadVertexVerticalOffset;
    public CSteamID Instigator { get; set; }
    public uint InstanceId { get; set; }
    public float DeltaTime { get; set; }
    public float VerticalOffset { get; set; }
    public bool TryReplaceFrom(IReplacableAction action)
    {
        SetRoadVertexVerticalOffsetAction a = (SetRoadVertexVerticalOffsetAction)action;
        if (a.InstanceId != InstanceId)
            return false;

        VerticalOffset = a.VerticalOffset;
        return true;
    }
    public void Apply()
    {
        NetId netId = new NetId(InstanceId);
        if (!RoadNetIdDatabase.TryGetVertex(netId, out RoadVertexIdentifier vertex))
        {
            Logger.DevkitServer.LogWarning(RoadActions.Source, $"Unknown vertex with NetId: {netId.Format()}.");
            return;
        }

        RoadUtil.SetVertexVerticalOffsetLocal(vertex.Road, vertex.Vertex, VerticalOffset);
    }
#if SERVER
    public bool CheckCanApply()
    {
        NetId netId = new NetId(InstanceId);
        if (!RoadNetIdDatabase.TryGetVertex(netId, out RoadVertexIdentifier _))
        {
            Logger.DevkitServer.LogWarning(RoadActions.Source, $"Unknown vertex with NetId: {netId.Format()}.");
            return false;
        }
        return VanillaPermissions.EditRoads.Has(Instigator.m_SteamID, true);
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        VerticalOffset = reader.ReadFloat();
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write(VerticalOffset);
    }
    public int CalculateSize() => 8;
}

[Action(DevkitServerActionType.SetRoadVertexTangentHandleMode, 5, 4)]
public sealed class SetRoadVertexTangentHandleModeAction : IAction, IInstanceIdAction
{
    public DevkitServerActionType Type => DevkitServerActionType.SetRoadVertexTangentHandleMode;
    public CSteamID Instigator { get; set; }
    public uint InstanceId { get; set; }
    public float DeltaTime { get; set; }
    public ERoadMode TangentHandleMode { get; set; }
    public void Apply()
    {
        NetId netId = new NetId(InstanceId);
        if (!RoadNetIdDatabase.TryGetVertex(netId, out RoadVertexIdentifier vertex))
        {
            Logger.DevkitServer.LogWarning(RoadActions.Source, $"Unknown vertex with NetId: {netId.Format()}.");
            return;
        }

        RoadUtil.SetVertexTangentHandleModeLocal(vertex.Road, vertex.Vertex, TangentHandleMode);
    }
#if SERVER
    public bool CheckCanApply()
    {
        NetId netId = new NetId(InstanceId);
        if (!RoadNetIdDatabase.TryGetVertex(netId, out RoadVertexIdentifier _))
        {
            Logger.DevkitServer.LogWarning(RoadActions.Source, $"Unknown vertex with NetId: {netId.Format()}.");
            return false;
        }
        return VanillaPermissions.EditRoads.Has(Instigator.m_SteamID, true);
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        TangentHandleMode = (ERoadMode)reader.ReadUInt8();
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write((byte)TangentHandleMode);
    }
    public int CalculateSize() => 5;
}

[Action(DevkitServerActionType.SetRoadMaterialWidth, 8, 4)]
public sealed class SetRoadMaterialWidthAction : IReplacableAction, IInstanceIdAction
{
    public DevkitServerActionType Type => DevkitServerActionType.SetRoadMaterialWidth;
    public CSteamID Instigator { get; set; }
    public uint InstanceId { get; set; }
    public float DeltaTime { get; set; }
    public float Width { get; set; }
    public bool TryReplaceFrom(IReplacableAction action)
    {
        SetRoadMaterialWidthAction a = (SetRoadMaterialWidthAction)action;
        if (a.InstanceId != InstanceId)
            return false;

        Width = a.Width;
        return true;
    }
    public void Apply()
    {
        if (InstanceId > byte.MaxValue || InstanceId >= LevelRoads.materials.Length)
        {
            Logger.DevkitServer.LogWarning(RoadActions.Source, $"Unknown road material at index: {InstanceId.Format()}.");
            return;
        }

        RoadUtil.SetMaterialWidthLocal((int)InstanceId, Width);
    }
#if SERVER
    public bool CheckCanApply()
    {
        if (InstanceId > byte.MaxValue || InstanceId >= LevelRoads.materials.Length)
        {
            Logger.DevkitServer.LogWarning(RoadActions.Source, $"Unknown road material at index: {InstanceId.Format()}.");
            return false;
        }
        return VanillaPermissions.EditRoadMaterials.Has(Instigator.m_SteamID, true);
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        Width = reader.ReadFloat();
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write(Width);
    }
    public int CalculateSize() => 8;
}

[Action(DevkitServerActionType.SetRoadMaterialHeight, 8, 4)]
public sealed class SetRoadMaterialHeightAction : IReplacableAction, IInstanceIdAction
{
    public DevkitServerActionType Type => DevkitServerActionType.SetRoadMaterialHeight;
    public CSteamID Instigator { get; set; }
    public uint InstanceId { get; set; }
    public float DeltaTime { get; set; }
    public float Height { get; set; }
    public bool TryReplaceFrom(IReplacableAction action)
    {
        SetRoadMaterialHeightAction a = (SetRoadMaterialHeightAction)action;
        if (a.InstanceId != InstanceId)
            return false;

        Height = a.Height;
        return true;
    }
    public void Apply()
    {
        if (InstanceId > byte.MaxValue || InstanceId >= LevelRoads.materials.Length)
        {
            Logger.DevkitServer.LogWarning(RoadActions.Source, $"Unknown road material at index: {InstanceId.Format()}.");
            return;
        }

        RoadUtil.SetMaterialHeightLocal((int)InstanceId, Height);
    }
#if SERVER
    public bool CheckCanApply()
    {
        if (InstanceId > byte.MaxValue || InstanceId >= LevelRoads.materials.Length)
        {
            Logger.DevkitServer.LogWarning(RoadActions.Source, $"Unknown road material at index: {InstanceId.Format()}.");
            return false;
        }
        return VanillaPermissions.EditRoadMaterials.Has(Instigator.m_SteamID, true);
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        Height = reader.ReadFloat();
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write(Height);
    }
    public int CalculateSize() => 8;
}

[Action(DevkitServerActionType.SetRoadMaterialDepth, 8, 4)]
public sealed class SetRoadMaterialDepthAction : IReplacableAction, IInstanceIdAction
{
    public DevkitServerActionType Type => DevkitServerActionType.SetRoadMaterialDepth;
    public CSteamID Instigator { get; set; }
    public uint InstanceId { get; set; }
    public float DeltaTime { get; set; }
    public float Depth { get; set; }
    public bool TryReplaceFrom(IReplacableAction action)
    {
        SetRoadMaterialDepthAction a = (SetRoadMaterialDepthAction)action;
        if (a.InstanceId != InstanceId)
            return false;

        Depth = a.Depth;
        return true;
    }
    public void Apply()
    {
        if (InstanceId > byte.MaxValue || InstanceId >= LevelRoads.materials.Length)
        {
            Logger.DevkitServer.LogWarning(RoadActions.Source, $"Unknown road material at index: {InstanceId.Format()}.");
            return;
        }

        RoadUtil.SetMaterialDepthLocal((int)InstanceId, Depth);
    }
#if SERVER
    public bool CheckCanApply()
    {
        if (InstanceId > byte.MaxValue || InstanceId >= LevelRoads.materials.Length)
        {
            Logger.DevkitServer.LogWarning(RoadActions.Source, $"Unknown road material at index: {InstanceId.Format()}.");
            return false;
        }
        return VanillaPermissions.EditRoadMaterials.Has(Instigator.m_SteamID, true);
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        Depth = reader.ReadFloat();
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write(Depth);
    }
    public int CalculateSize() => 8;
}

[Action(DevkitServerActionType.SetRoadMaterialVerticalOffset, 8, 4)]
public sealed class SetRoadMaterialVerticalOffsetAction : IReplacableAction, IInstanceIdAction
{
    public DevkitServerActionType Type => DevkitServerActionType.SetRoadMaterialVerticalOffset;
    public CSteamID Instigator { get; set; }
    public uint InstanceId { get; set; }
    public float DeltaTime { get; set; }
    public float VerticalOffset { get; set; }
    public bool TryReplaceFrom(IReplacableAction action)
    {
        SetRoadMaterialVerticalOffsetAction a = (SetRoadMaterialVerticalOffsetAction)action;
        if (a.InstanceId != InstanceId)
            return false;

        VerticalOffset = a.VerticalOffset;
        return true;
    }
    public void Apply()
    {
        if (InstanceId > byte.MaxValue || InstanceId >= LevelRoads.materials.Length)
        {
            Logger.DevkitServer.LogWarning(RoadActions.Source, $"Unknown road material at index: {InstanceId.Format()}.");
            return;
        }

        RoadUtil.SetMaterialVerticalOffsetLocal((int)InstanceId, VerticalOffset);
    }
#if SERVER
    public bool CheckCanApply()
    {
        if (InstanceId > byte.MaxValue || InstanceId >= LevelRoads.materials.Length)
        {
            Logger.DevkitServer.LogWarning(RoadActions.Source, $"Unknown road material at index: {InstanceId.Format()}.");
            return false;
        }
        return VanillaPermissions.EditRoadMaterials.Has(Instigator.m_SteamID, true);
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        VerticalOffset = reader.ReadFloat();
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write(VerticalOffset);
    }
    public int CalculateSize() => 8;
}

[Action(DevkitServerActionType.SetRoadMaterialIsConcrete, 5, 4)]
public sealed class SetRoadMaterialIsConcreteAction : IReplacableAction, IInstanceIdAction
{
    public DevkitServerActionType Type => DevkitServerActionType.SetRoadMaterialIsConcrete;
    public CSteamID Instigator { get; set; }
    public uint InstanceId { get; set; }
    public float DeltaTime { get; set; }
    public bool IsConcrete { get; set; }
    public bool TryReplaceFrom(IReplacableAction action)
    {
        SetRoadMaterialIsConcreteAction a = (SetRoadMaterialIsConcreteAction)action;
        if (a.InstanceId != InstanceId)
            return false;

        IsConcrete = a.IsConcrete;
        return true;
    }
    public void Apply()
    {
        if (InstanceId > byte.MaxValue || InstanceId >= LevelRoads.materials.Length)
        {
            Logger.DevkitServer.LogWarning(RoadActions.Source, $"Unknown road material at index: {InstanceId.Format()}.");
            return;
        }

        RoadUtil.SetMaterialIsConcreteLocal((int)InstanceId, IsConcrete);
    }
#if SERVER
    public bool CheckCanApply()
    {
        if (InstanceId > byte.MaxValue || InstanceId >= LevelRoads.materials.Length)
        {
            Logger.DevkitServer.LogWarning(RoadActions.Source, $"Unknown road material at index: {InstanceId.Format()}.");
            return false;
        }
        return VanillaPermissions.EditRoadMaterials.Has(Instigator.m_SteamID, true);
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        IsConcrete = reader.ReadBool();
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write(IsConcrete);
    }
    public int CalculateSize() => 5;
}

[Action(DevkitServerActionType.MoveRoadVertex, 16, 4)]
public sealed class MoveRoadVertexAction : IAction, IInstanceIdAction
{
    public DevkitServerActionType Type => DevkitServerActionType.MoveRoadVertex;
    public CSteamID Instigator { get; set; }
    public uint InstanceId { get; set; }
    public float DeltaTime { get; set; }
    public Vector3 Position { get; set; }
    public void Apply()
    {
        NetId netId = new NetId(InstanceId);
        if (!RoadNetIdDatabase.TryGetVertex(netId, out RoadVertexIdentifier vertex))
        {
            Logger.DevkitServer.LogWarning(RoadActions.Source, $"Unknown vertex with NetId: {netId.Format()}.");
            return;
        }

        RoadUtil.SetVertexPositionLocal(vertex.Road, vertex.Vertex, Position);
    }
#if SERVER
    public bool CheckCanApply()
    {
        NetId netId = new NetId(InstanceId);
        if (!RoadNetIdDatabase.TryGetVertex(netId, out RoadVertexIdentifier _))
        {
            Logger.DevkitServer.LogWarning(RoadActions.Source, $"Unknown vertex with NetId: {netId.Format()}.");
            return false;
        }
        return VanillaPermissions.EditRoads.Has(Instigator.m_SteamID, true);
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        Position = reader.ReadVector3();
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write(Position);
    }
    public int CalculateSize() => 16;
}

[Action(DevkitServerActionType.MoveRoadTangentHandle, 17, 4)]
public sealed class MoveRoadTangentHandleAction : IAction, IInstanceIdAction
{
    public DevkitServerActionType Type => DevkitServerActionType.MoveRoadTangentHandle;
    public CSteamID Instigator { get; set; }
    public uint InstanceId { get; set; }
    public float DeltaTime { get; set; }
    public Vector3 Position { get; set; }
    public TangentHandle Handle { get; set; }
    public void Apply()
    {
        NetId netId = new NetId(InstanceId);
        if (!RoadNetIdDatabase.TryGetVertex(netId, out RoadVertexIdentifier vertex))
        {
            Logger.DevkitServer.LogWarning(RoadActions.Source, $"Unknown vertex with NetId: {netId.Format()}.");
            return;
        }
        if (Handle is not TangentHandle.Negative and not TangentHandle.Positive)
        {
            Logger.DevkitServer.LogWarning(RoadActions.Source, $"Handle not valid: {Handle.Format()}.");
            return;
        }

        RoadUtil.SetTangentHandlePositionLocal(vertex.Road, vertex.Vertex, Handle, Position);
    }
#if SERVER
    public bool CheckCanApply()
    {
        NetId netId = new NetId(InstanceId);
        if (!RoadNetIdDatabase.TryGetVertex(netId, out RoadVertexIdentifier _))
        {
            Logger.DevkitServer.LogWarning(RoadActions.Source, $"Unknown vertex with NetId: {netId.Format()}.");
            return false;
        }
        if (Handle is not TangentHandle.Negative and not TangentHandle.Positive)
        {
            Logger.DevkitServer.LogWarning(RoadActions.Source, $"Handle not valid: {Handle.Format()}.");
            return false;
        }

        return VanillaPermissions.EditRoads.Has(Instigator.m_SteamID, true);
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        Handle = reader.ReadBool() ? TangentHandle.Positive : TangentHandle.Negative;
        Position = reader.ReadVector3();
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write(Handle == TangentHandle.Positive);
        writer.Write(Position);
    }
    public int CalculateSize() => 17;
}

[Action(DevkitServerActionType.DeleteRoadVertex, 4, 4)]
public sealed class DeleteRoadVertexAction : IAction, IInstanceIdAction
{
    public DevkitServerActionType Type => DevkitServerActionType.DeleteRoadVertex;
    public CSteamID Instigator { get; set; }
    public uint InstanceId { get; set; }
    public float DeltaTime { get; set; }
    public void Apply()
    {
        NetId netId = new NetId(InstanceId);
        if (!RoadNetIdDatabase.TryGetVertex(netId, out RoadVertexIdentifier vertex))
        {
            Logger.DevkitServer.LogWarning(RoadActions.Source, $"Unknown vertex with NetId: {netId.Format()}.");
            return;
        }

        RoadUtil.RemoveVertexLocal(vertex.Road, vertex.Vertex);
    }
#if SERVER
    public bool CheckCanApply()
    {
        NetId netId = new NetId(InstanceId);
        if (!RoadNetIdDatabase.TryGetVertex(netId, out RoadVertexIdentifier _))
        {
            Logger.DevkitServer.LogWarning(RoadActions.Source, $"Unknown vertex with NetId: {netId.Format()}.");
            return false;
        }

        return VanillaPermissions.EditRoads.Has(Instigator.m_SteamID, true);
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
    }
    public int CalculateSize() => 4;
}

[Action(DevkitServerActionType.DeleteRoad, 4, 4)]
public sealed class DeleteRoadAction : IAction, IInstanceIdAction
{
    public DevkitServerActionType Type => DevkitServerActionType.DeleteRoad;
    public CSteamID Instigator { get; set; }
    public uint InstanceId { get; set; }
    public float DeltaTime { get; set; }
    public void Apply()
    {
        NetId netId = new NetId(InstanceId);
        if (!RoadNetIdDatabase.TryGetRoad(netId, out int roadIndex))
        {
            Logger.DevkitServer.LogWarning(RoadActions.Source, $"Unknown road with NetId: {netId.Format()}.");
            return;
        }

        RoadUtil.RemoveRoadLocal(roadIndex);
    }
#if SERVER
    public bool CheckCanApply()
    {
        NetId netId = new NetId(InstanceId);
        if (!RoadNetIdDatabase.TryGetRoad(netId, out int _))
        {
            Logger.DevkitServer.LogWarning(RoadActions.Source, $"Unknown road with NetId: {netId.Format()}.");
            return false;
        }
        return VanillaPermissions.EditRoads.Has(Instigator.m_SteamID, true);
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
    }
    public int CalculateSize() => 4;
}