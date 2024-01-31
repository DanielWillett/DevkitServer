using DevkitServer.API.Devkit.Spawns;
using DevkitServer.Models;
using DevkitServer.Multiplayer.Levels;
using DevkitServer.Util.Encoding;
#if SERVER
using DevkitServer.API.Permissions;
using DevkitServer.Core.Permissions;
#endif

namespace DevkitServer.Multiplayer.Actions;
public class SpawnActions
{
    public EditorActions EditorActions { get; }
    internal SpawnActions(EditorActions actions)
    {
        EditorActions = actions;
    }
    public void Subscribe()
    {
#if CLIENT
        if (EditorActions.IsOwner)
        {
            ClientEvents.OnSetPlayerSpawnpointsIsAlternate += OnSetPlayerSpawnpointsIsAlternate;
            ClientEvents.OnMoveSpawnsFinal += OnMoveSpawnsFinal;
            ClientEvents.OnDeleteSpawns += OnDeleteSpawns;
        }
#endif
    }
    public void Unsubscribe()
    {
#if CLIENT
        if (EditorActions.IsOwner)
        {
            ClientEvents.OnSetPlayerSpawnpointsIsAlternate -= OnSetPlayerSpawnpointsIsAlternate;
            ClientEvents.OnMoveSpawnsFinal -= OnMoveSpawnsFinal;
            ClientEvents.OnDeleteSpawns -= OnDeleteSpawns;
        }
#endif
    }
#if CLIENT
    private void OnSetPlayerSpawnpointsIsAlternate(in SetPlayerSpawnpointsIsAlternateProperties properties)
    {
        EditorActions.QueueAction(new SetPlayerSpawnpointsIsAlternateAction
        {
            DeltaTime = properties.DeltaTime,
            IsAlternate = properties.IsAlternate,
            NetIds = properties.SpawnNetIds.ToArray()
        });
    }
    private void OnMoveSpawnsFinal(in MoveSpawnsFinalProperties properties)
    {
        EditorActions.QueueAction(new MoveSpawnsFinalAction
        {
            DeltaTime = properties.DeltaTime,
            Transformations = properties.Transformations.ToArray(),
            NetIds = properties.SpawnNetIds.ToArray(),
            SpawnType = properties.SpawnType
        });
    }
    private void OnDeleteSpawns(in DeleteSpawnsProperties properties)
    {
        EditorActions.QueueAction(new DeleteSpawnsAction
        {
            DeltaTime = properties.DeltaTime,
            NetIds = properties.SpawnNetIds.ToArray(),
            SpawnType = properties.SpawnType
        });
    }
#endif
}


[Action(DevkitServerActionType.SetPlayerSpawnpointsIsAlternate, 262, 0)]
public sealed class SetPlayerSpawnpointsIsAlternateAction : IAction
{
    public DevkitServerActionType Type => DevkitServerActionType.SetPlayerSpawnpointsIsAlternate;
    public CSteamID Instigator { get; set; }
    public NetId64[] NetIds { get; set; } = null!;
    public bool IsAlternate { get; set; }
    public float DeltaTime { get; set; }
    public void Apply()
    {
        for (int i = 0; i < NetIds.Length; ++i)
        {
            if (!SpawnsNetIdDatabase.TryGetPlayerSpawnpoint(NetIds[i], out int index))
            {
                Logger.DevkitServer.LogWarning(nameof(SetPlayerSpawnpointsIsAlternateAction), $"Unknown player spawnpoint with NetId: {NetIds[i].Format()}.");
                continue;
            }

            SpawnUtil.SetPlayerIsAlternateLocal(index, IsAlternate);
        }
    }
#if SERVER
    public bool CheckCanApply()
    {
        for (int i = 0; i < NetIds.Length; ++i)
        {
            if (SpawnsNetIdDatabase.TryGetPlayerSpawnpoint(NetIds[i], out int _))
                continue;

            Logger.DevkitServer.LogWarning(nameof(SetPlayerSpawnpointsIsAlternateAction), $"Unknown player spawnpoint with NetId: {NetIds[i].Format()}.");
            return false;
        }

        return VanillaPermissions.SpawnsPlayerEdit.Has(Instigator.m_SteamID, true);
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        IsAlternate = reader.ReadBool();

        int ct = reader.ReadUInt8();
        NetIds = new NetId64[ct];
        for (int i = 0; i < ct; ++i)
            NetIds[i] = reader.ReadNetId64();
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write(IsAlternate);

        int ct = Math.Min(byte.MaxValue, NetIds.Length);
        writer.Write((byte)ct);
        for (int i = 0; i < ct; ++i)
            writer.Write(NetIds[i]);
    }
    public int CalculateSize() => NetIds.Length * 8 + 6;
}

[Action(DevkitServerActionType.MoveSpawnsFinal, 582, 0)]
public sealed class MoveSpawnsFinalAction : IAction
{
    public DevkitServerActionType Type => DevkitServerActionType.MoveSpawnsFinal;
    public CSteamID Instigator { get; set; }
    public NetId64[] NetIds { get; set; } = null!;
    public TransformationDelta[] Transformations { get; set; } = null!;
    public SpawnType SpawnType { get; set; }
    public float DeltaTime { get; set; }
    public void Apply()
    {
        bool supportsRotation = SpawnType is SpawnType.Vehicle or SpawnType.Player;
        if (supportsRotation || SpawnType == SpawnType.Animal)
        {
            for (int i = 0; i < NetIds.Length; ++i)
            {
                if (!SpawnsNetIdDatabase.TryGetSpawnpoint(SpawnType, NetIds[i], out int index))
                {
                    Logger.DevkitServer.LogWarning(nameof(MoveSpawnsFinalAction), $"Unknown {SpawnType.GetLowercaseText()} spawnpoint with NetId: {NetIds[i].Format()}.");
                    continue;
                }

                ref TransformationDelta t = ref Transformations[i];
                if (supportsRotation && (t.Flags & TransformationDelta.TransformFlags.Rotation) != 0)
                {
                    if ((t.Flags & TransformationDelta.TransformFlags.Position) != 0)
                    {
                        SpawnUtil.TransformSpawnpointLocal(SpawnType, index, t.Position, (t.Flags & TransformationDelta.TransformFlags.YawOnly) == 0 ? t.Rotation.eulerAngles.y : t.Rotation.y);
                    }
                    else
                    {
                        SpawnUtil.RotateSpawnpointLocal(SpawnType, index, (t.Flags & TransformationDelta.TransformFlags.YawOnly) == 0 ? t.Rotation.eulerAngles.y : t.Rotation.y);
                    }
                }
                else if ((t.Flags & TransformationDelta.TransformFlags.Position) != 0)
                {
                    SpawnUtil.MoveSpawnpointLocal(SpawnType, index, t.Position);
                }
                else
                {
                    Logger.DevkitServer.LogWarning(nameof(MoveSpawnsFinalAction), $"{SpawnType.GetPropercaseText()} spawnpoint had invalid/empty transformation delta: {NetIds[i].Format()}.");
                }
            }
        }
        else
        {
            for (int i = 0; i < NetIds.Length; ++i)
            {
                if (!SpawnsNetIdDatabase.TryGetSpawnpoint(SpawnType, NetIds[i], out RegionIdentifier id))
                {
                    Logger.DevkitServer.LogWarning(nameof(MoveSpawnsFinalAction), $"Unknown {SpawnType.GetLowercaseText()} spawnpoint with NetId: {NetIds[i].Format()}.");
                    continue;
                }

                ref TransformationDelta t = ref Transformations[i];
                if ((t.Flags & TransformationDelta.TransformFlags.Position) != 0)
                {
                    SpawnUtil.MoveSpawnpointLocal(SpawnType, id, t.Position);
                }
                else
                {
                    Logger.DevkitServer.LogWarning(nameof(MoveSpawnsFinalAction), $"{SpawnType.GetPropercaseText()} spawnpoint had invalid/empty transformation delta: {NetIds[i].Format()}.");
                }
            }
        }
    }
#if SERVER
    public bool CheckCanApply()
    {
        if (SpawnType is SpawnType.Animal or SpawnType.Player or SpawnType.Vehicle)
        {
            for (int i = 0; i < NetIds.Length; ++i)
            {
                if (SpawnsNetIdDatabase.TryGetSpawnpoint(SpawnType, NetIds[i], out int _))
                    continue;

                Logger.DevkitServer.LogWarning(nameof(MoveSpawnsFinalAction), $"Unknown {SpawnType.GetLowercaseText()} spawnpoint with NetId: {NetIds[i].Format()}.");
                return false;
            }
        }
        else
        {
            for (int i = 0; i < NetIds.Length; ++i)
            {
                if (SpawnsNetIdDatabase.TryGetSpawnpoint(SpawnType, NetIds[i], out RegionIdentifier _))
                    continue;

                Logger.DevkitServer.LogWarning(nameof(MoveSpawnsFinalAction), $"Unknown {SpawnType.GetLowercaseText()} spawnpoint with NetId: {NetIds[i].Format()}.");
                return false;
            }
        }

        return VanillaPermissions.SpawnsMove(SpawnType).Has(Instigator.m_SteamID, true);
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        SpawnType = (SpawnType)reader.ReadUInt8();

        int ct = reader.ReadUInt8();
        NetIds = new NetId64[ct];
        Transformations = new TransformationDelta[ct];
        for (int i = 0; i < ct; ++i)
        {
            NetIds[i] = reader.ReadNetId64();
            Transformations[i] = new TransformationDelta(reader);
        }
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write((byte)SpawnType);

        int ct = Math.Min(byte.MaxValue, Math.Min(NetIds.Length, Transformations.Length));
        writer.Write((byte)ct);
        for (int i = 0; i < ct; ++i)
        {
            ref TransformationDelta t = ref Transformations[i];

            writer.Write(NetIds[i]);
            t.Write(writer);
        }
    }
    public int CalculateSize() => NetIds.Length * 8 + 6 + (Transformations.Length == 0 ? 0 : (Transformations[0].CalculateSize() * Transformations.Length));
}


[Action(DevkitServerActionType.DeleteSpawns, 262, 0)]
public sealed class DeleteSpawnsAction : IAction
{
    public DevkitServerActionType Type => DevkitServerActionType.DeleteSpawns;
    public CSteamID Instigator { get; set; }
    public NetId64[] NetIds { get; set; } = null!;
    public SpawnType SpawnType { get; set; }
    public float DeltaTime { get; set; }
    public void Apply()
    {
        if (SpawnType is SpawnType.Vehicle or SpawnType.Player or SpawnType.Animal)
        {
            for (int i = 0; i < NetIds.Length; ++i)
            {
                if (!SpawnsNetIdDatabase.TryGetSpawnpoint(SpawnType, NetIds[i], out int index))
                {
                    Logger.DevkitServer.LogWarning(nameof(DeleteSpawnsAction), $"Unknown {SpawnType.GetLowercaseText()} spawnpoint with NetId: {NetIds[i].Format()}.");
                    continue;
                }

                SpawnUtil.RemoveSpawnLocal(SpawnType, index);
            }
        }
        else
        {
            for (int i = 0; i < NetIds.Length; ++i)
            {
                if (!SpawnsNetIdDatabase.TryGetSpawnpoint(SpawnType, NetIds[i], out RegionIdentifier id))
                {
                    Logger.DevkitServer.LogWarning(nameof(DeleteSpawnsAction), $"Unknown {SpawnType.GetLowercaseText()} spawnpoint with NetId: {NetIds[i].Format()}.");
                    continue;
                }

                SpawnUtil.RemoveSpawnLocal(SpawnType, id);
            }
        }
    }
#if SERVER
    public bool CheckCanApply()
    {
        if (SpawnType is SpawnType.Animal or SpawnType.Player or SpawnType.Vehicle)
        {
            for (int i = 0; i < NetIds.Length; ++i)
            {
                if (SpawnsNetIdDatabase.TryGetSpawnpoint(SpawnType, NetIds[i], out int _))
                    continue;

                Logger.DevkitServer.LogWarning(nameof(DeleteSpawnsAction), $"Unknown {SpawnType.GetLowercaseText()} spawnpoint with NetId: {NetIds[i].Format()}.");
                return false;
            }
        }
        else
        {
            for (int i = 0; i < NetIds.Length; ++i)
            {
                if (SpawnsNetIdDatabase.TryGetSpawnpoint(SpawnType, NetIds[i], out RegionIdentifier _))
                    continue;

                Logger.DevkitServer.LogWarning(nameof(DeleteSpawnsAction), $"Unknown {SpawnType.GetLowercaseText()} spawnpoint with NetId: {NetIds[i].Format()}.");
                return false;
            }
        }

        return VanillaPermissions.SpawnsMove(SpawnType).Has(Instigator.m_SteamID, true);
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        SpawnType = (SpawnType)reader.ReadUInt8();

        int ct = reader.ReadUInt8();
        NetIds = new NetId64[ct];
        for (int i = 0; i < ct; ++i)
            NetIds[i] = reader.ReadNetId64();
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write((byte)SpawnType);

        int ct = Math.Min(byte.MaxValue, NetIds.Length);
        writer.Write((byte)ct);
        for (int i = 0; i < ct; ++i)
            writer.Write(NetIds[i]);
    }
    public int CalculateSize() => NetIds.Length * 8 + 6;
}