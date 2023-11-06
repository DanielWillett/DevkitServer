using DevkitServer.API;
using DevkitServer.Multiplayer.Levels;
using DevkitServer.Util.Encoding;
#if SERVER
using DevkitServer.API.Permissions;
using DevkitServer.Core.Permissions;
#endif

namespace DevkitServer.Multiplayer.Actions;
public sealed class NavigationActions
{
    public EditorActions EditorActions { get; }
    internal NavigationActions(EditorActions actions)
    {
        EditorActions = actions;
    }
    public void Subscribe()
    {
#if CLIENT
        if (EditorActions.IsOwner)
        {
            ClientEvents.OnMoveNavigation += OnMoveNavigation;
            ClientEvents.OnDeleteNavigation += OnDeleteNavigation;

            ClientEvents.OnSetNavigationSize += OnSetNavigationSize;
            ClientEvents.OnSetNavigationDifficulty += OnSetNavigationDifficulty;
            ClientEvents.OnSetNavigationMaximumZombies += OnSetNavigationMaximumZombies;
            ClientEvents.OnSetNavigationMaximumBossZombies += OnSetNavigationMaximumBossZombies;
            ClientEvents.OnSetNavigationShouldSpawnZombies += OnSetNavigationShouldSpawnZombies;
            ClientEvents.OnSetNavigationInfiniteAgroDistance += OnSetNavigationInfiniteAgroDistance;
        }
#endif
    }

    public void Unsubscribe()
    {
#if CLIENT
        if (EditorActions.IsOwner)
        {
            ClientEvents.OnMoveNavigation -= OnMoveNavigation;
            ClientEvents.OnDeleteNavigation -= OnDeleteNavigation;

            ClientEvents.OnSetNavigationSize -= OnSetNavigationSize;
            ClientEvents.OnSetNavigationDifficulty -= OnSetNavigationDifficulty;
            ClientEvents.OnSetNavigationMaximumZombies -= OnSetNavigationMaximumZombies;
            ClientEvents.OnSetNavigationMaximumBossZombies -= OnSetNavigationMaximumBossZombies;
            ClientEvents.OnSetNavigationShouldSpawnZombies -= OnSetNavigationShouldSpawnZombies;
            ClientEvents.OnSetNavigationInfiniteAgroDistance -= OnSetNavigationInfiniteAgroDistance;
        }
#endif
    }

#if CLIENT
    private void OnMoveNavigation(in MoveNavigationProperties properties)
    {
        EditorActions.QueueAction(new MoveNavigationAction
        {
            InstanceId = properties.NavigationNetId.id,
            Position = properties.Position,
            DeltaTime = properties.DeltaTime
        }, true);
    }
    private void OnDeleteNavigation(in DeleteNavigationProperties properties)
    {
        EditorActions.QueueAction(new DeleteNavigationAction
        {
            InstanceId = properties.NavigationNetId.id,
            DeltaTime = properties.DeltaTime
        }, true);
    }
    private void OnSetNavigationSize(in SetNavigationSizeProperties properties)
    {
        EditorActions.QueueAction(new SetNavigationSizeAction
        {
            InstanceId = properties.NavigationNetId.id,
            Size = properties.Size,
            DeltaTime = properties.DeltaTime
        }, true);
    }
    private void OnSetNavigationDifficulty(in SetNavigationDifficultyProperties properties)
    {
        EditorActions.QueueAction(new SetNavigationDifficultyAction
        {
            InstanceId = properties.NavigationNetId.id,
            Difficulty = properties.Difficulty,
            DeltaTime = properties.DeltaTime
        }, true);
    }
    private void OnSetNavigationMaximumZombies(in SetNavigationMaximumZombiesProperties properties)
    {
        EditorActions.QueueAction(new SetNavigationMaximumZombiesAction
        {
            InstanceId = properties.NavigationNetId.id,
            MaximumZombies = properties.MaximumZombies,
            DeltaTime = properties.DeltaTime
        }, true);
    }
    private void OnSetNavigationMaximumBossZombies(in SetNavigationMaximumBossZombiesProperties properties)
    {
        EditorActions.QueueAction(new SetNavigationMaximumBossZombiesAction
        {
            InstanceId = properties.NavigationNetId.id,
            MaximumBossZombies = properties.MaximumBossZombies,
            DeltaTime = properties.DeltaTime
        }, true);
    }
    private void OnSetNavigationShouldSpawnZombies(in SetNavigationShouldSpawnZombiesProperties properties)
    {
        EditorActions.QueueAction(new SetNavigationShouldSpawnZombiesAction
        {
            InstanceId = properties.NavigationNetId.id,
            ShouldSpawnZombies = properties.ShouldSpawnZombies,
            DeltaTime = properties.DeltaTime
        }, true);
    }
    private void OnSetNavigationInfiniteAgroDistance(in SetNavigationInfiniteAgroDistanceProperties properties)
    {
        EditorActions.QueueAction(new SetNavigationInfiniteAgroDistanceAction
        {
            InstanceId = properties.NavigationNetId.id,
            InfiniteAgroDistance = properties.InfiniteAgroDistance,
            DeltaTime = properties.DeltaTime
        }, true);
    }
#endif
}

[Action(DevkitServerActionType.SetNavigationSize, 12, 4)]
[EarlyTypeInit]
public sealed class SetNavigationSizeAction : IReplacableAction, IInstanceIdAction
{
    public DevkitServerActionType Type => DevkitServerActionType.SetNavigationSize;
    public CSteamID Instigator { get; set; }
    public uint InstanceId { get; set; }
    public float DeltaTime { get; set; }
    public Vector2 Size { get; set; }
    public bool TryReplaceFrom(IReplacableAction action)
    {
        SetNavigationSizeAction a = (SetNavigationSizeAction)action;
        if (a.InstanceId != InstanceId)
            return false;

        Size = a.Size;
        return true;
    }
    public void Apply()
    {
        NetId netId = new NetId(InstanceId);
        if (!NavigationNetIdDatabase.TryGetNavigation(netId, out byte nav))
        {
            Logger.LogWarning($"Unknown navigation flag with NetId: {netId.Format()}.");
            return;
        }

        NavigationUtil.SetFlagSizeLocal(nav, Size);
    }
#if SERVER
    public bool CheckCanApply()
    {
        NetId netId = new NetId(InstanceId);
        if (!NavigationNetIdDatabase.TryGetNavigation(netId, out _))
        {
            Logger.LogWarning($"Unknown navigation flag with NetId: {netId.Format()}.");
            return false;
        }

        return VanillaPermissions.EditNavigation.Has(Instigator.m_SteamID, true);
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        Size = reader.ReadVector2();
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write(Size);
    }
    public int CalculateSize() => 12;
}

[Action(DevkitServerActionType.SetNavigationDifficulty, 20, 4)]
[EarlyTypeInit]
public sealed class SetNavigationDifficultyAction : IReplacableAction, IInstanceIdAction
{
    public DevkitServerActionType Type => DevkitServerActionType.SetNavigationDifficulty;
    public CSteamID Instigator { get; set; }
    public uint InstanceId { get; set; }
    public float DeltaTime { get; set; }
    public AssetReference<ZombieDifficultyAsset> Difficulty { get; set; }
    public bool TryReplaceFrom(IReplacableAction action)
    {
        SetNavigationDifficultyAction a = (SetNavigationDifficultyAction)action;
        if (a.InstanceId != InstanceId)
            return false;

        Difficulty = a.Difficulty;
        return true;
    }
    public void Apply()
    {
        NetId netId = new NetId(InstanceId);
        if (!NavigationNetIdDatabase.TryGetNavigation(netId, out byte nav))
        {
            Logger.LogWarning($"Unknown navigation flag with NetId: {netId.Format()}.");
            return;
        }

        NavigationUtil.SetFlagDifficultyLocal(nav, Difficulty);
    }
#if SERVER
    public bool CheckCanApply()
    {
        NetId netId = new NetId(InstanceId);
        if (!NavigationNetIdDatabase.TryGetNavigation(netId, out _))
        {
            Logger.LogWarning($"Unknown navigation flag with NetId: {netId.Format()}.");
            return false;
        }

        return VanillaPermissions.EditNavigation.Has(Instigator.m_SteamID, true);
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        Difficulty = new AssetReference<ZombieDifficultyAsset>(reader.ReadGuid());
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write(Difficulty.GUID);
    }
    public int CalculateSize() => 20;
}

[Action(DevkitServerActionType.SetNavigationMaximumZombies, 5, 4)]
[EarlyTypeInit]
public sealed class SetNavigationMaximumZombiesAction : IReplacableAction, IInstanceIdAction
{
    public DevkitServerActionType Type => DevkitServerActionType.SetNavigationMaximumZombies;
    public CSteamID Instigator { get; set; }
    public uint InstanceId { get; set; }
    public float DeltaTime { get; set; }
    public byte MaximumZombies { get; set; }
    public bool TryReplaceFrom(IReplacableAction action)
    {
        SetNavigationMaximumZombiesAction a = (SetNavigationMaximumZombiesAction)action;
        if (a.InstanceId != InstanceId)
            return false;

        MaximumZombies = a.MaximumZombies;
        return true;
    }
    public void Apply()
    {
        NetId netId = new NetId(InstanceId);
        if (!NavigationNetIdDatabase.TryGetNavigation(netId, out byte nav))
        {
            Logger.LogWarning($"Unknown navigation flag with NetId: {netId.Format()}.");
            return;
        }

        NavigationUtil.SetFlagMaximumZombiesLocal(nav, MaximumZombies);
    }
#if SERVER
    public bool CheckCanApply()
    {
        NetId netId = new NetId(InstanceId);
        if (!NavigationNetIdDatabase.TryGetNavigation(netId, out _))
        {
            Logger.LogWarning($"Unknown navigation flag with NetId: {netId.Format()}.");
            return false;
        }

        return VanillaPermissions.EditNavigation.Has(Instigator.m_SteamID, true);
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        MaximumZombies = reader.ReadUInt8();
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write(MaximumZombies);
    }
    public int CalculateSize() => 5;
}

[Action(DevkitServerActionType.SetNavigationMaximumBossZombies, 8, 4)]
[EarlyTypeInit]
public sealed class SetNavigationMaximumBossZombiesAction : IReplacableAction, IInstanceIdAction
{
    public DevkitServerActionType Type => DevkitServerActionType.SetNavigationMaximumBossZombies;
    public CSteamID Instigator { get; set; }
    public uint InstanceId { get; set; }
    public float DeltaTime { get; set; }
    public int MaximumBossZombies { get; set; }
    public bool TryReplaceFrom(IReplacableAction action)
    {
        SetNavigationMaximumBossZombiesAction a = (SetNavigationMaximumBossZombiesAction)action;
        if (a.InstanceId != InstanceId)
            return false;

        MaximumBossZombies = a.MaximumBossZombies;
        return true;
    }
    public void Apply()
    {
        NetId netId = new NetId(InstanceId);
        if (!NavigationNetIdDatabase.TryGetNavigation(netId, out byte nav))
        {
            Logger.LogWarning($"Unknown navigation flag with NetId: {netId.Format()}.");
            return;
        }

        NavigationUtil.SetFlagMaximumBossZombiesLocal(nav, MaximumBossZombies);
    }
#if SERVER
    public bool CheckCanApply()
    {
        NetId netId = new NetId(InstanceId);
        if (!NavigationNetIdDatabase.TryGetNavigation(netId, out _))
        {
            Logger.LogWarning($"Unknown navigation flag with NetId: {netId.Format()}.");
            return false;
        }

        return VanillaPermissions.EditNavigation.Has(Instigator.m_SteamID, true);
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        MaximumBossZombies = reader.ReadUInt8();
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write(MaximumBossZombies);
    }
    public int CalculateSize() => 8;
}

[Action(DevkitServerActionType.SetNavigationShouldSpawnZombies, 5, 4)]
[EarlyTypeInit]
public sealed class SetNavigationShouldSpawnZombiesAction : IReplacableAction, IInstanceIdAction
{
    public DevkitServerActionType Type => DevkitServerActionType.SetNavigationShouldSpawnZombies;
    public CSteamID Instigator { get; set; }
    public uint InstanceId { get; set; }
    public float DeltaTime { get; set; }
    public bool ShouldSpawnZombies { get; set; }
    public bool TryReplaceFrom(IReplacableAction action)
    {
        SetNavigationShouldSpawnZombiesAction a = (SetNavigationShouldSpawnZombiesAction)action;
        if (a.InstanceId != InstanceId)
            return false;

        ShouldSpawnZombies = a.ShouldSpawnZombies;
        return true;
    }
    public void Apply()
    {
        NetId netId = new NetId(InstanceId);
        if (!NavigationNetIdDatabase.TryGetNavigation(netId, out byte nav))
        {
            Logger.LogWarning($"Unknown navigation flag with NetId: {netId.Format()}.");
            return;
        }

        NavigationUtil.SetFlagShouldSpawnZombiesLocal(nav, ShouldSpawnZombies);
    }
#if SERVER
    public bool CheckCanApply()
    {
        NetId netId = new NetId(InstanceId);
        if (!NavigationNetIdDatabase.TryGetNavigation(netId, out _))
        {
            Logger.LogWarning($"Unknown navigation flag with NetId: {netId.Format()}.");
            return false;
        }

        return VanillaPermissions.EditNavigation.Has(Instigator.m_SteamID, true);
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        ShouldSpawnZombies = reader.ReadBool();
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write(ShouldSpawnZombies);
    }
    public int CalculateSize() => 5;
}

[Action(DevkitServerActionType.SetNavigationInfiniteAgroDistance, 5, 4)]
[EarlyTypeInit]
public sealed class SetNavigationInfiniteAgroDistanceAction : IReplacableAction, IInstanceIdAction
{
    public DevkitServerActionType Type => DevkitServerActionType.SetNavigationInfiniteAgroDistance;
    public CSteamID Instigator { get; set; }
    public uint InstanceId { get; set; }
    public float DeltaTime { get; set; }
    public bool InfiniteAgroDistance { get; set; }
    public bool TryReplaceFrom(IReplacableAction action)
    {
        SetNavigationInfiniteAgroDistanceAction a = (SetNavigationInfiniteAgroDistanceAction)action;
        if (a.InstanceId != InstanceId)
            return false;

        InfiniteAgroDistance = a.InfiniteAgroDistance;
        return true;
    }
    public void Apply()
    {
        NetId netId = new NetId(InstanceId);
        if (!NavigationNetIdDatabase.TryGetNavigation(netId, out byte nav))
        {
            Logger.LogWarning($"Unknown navigation flag with NetId: {netId.Format()}.");
            return;
        }

        NavigationUtil.SetFlagInfiniteAgroDistanceLocal(nav, InfiniteAgroDistance);
    }
#if SERVER
    public bool CheckCanApply()
    {
        NetId netId = new NetId(InstanceId);
        if (!NavigationNetIdDatabase.TryGetNavigation(netId, out _))
        {
            Logger.LogWarning($"Unknown navigation flag with NetId: {netId.Format()}.");
            return false;
        }

        return VanillaPermissions.EditNavigation.Has(Instigator.m_SteamID, true);
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        InfiniteAgroDistance = reader.ReadBool();
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write(InfiniteAgroDistance);
    }
    public int CalculateSize() => 5;
}

[Action(DevkitServerActionType.MoveNavigation, 16, 4)]
[EarlyTypeInit]
public sealed class MoveNavigationAction : IAction, IInstanceIdAction
{
    public DevkitServerActionType Type => DevkitServerActionType.MoveNavigation;
    public CSteamID Instigator { get; set; }
    public uint InstanceId { get; set; }
    public float DeltaTime { get; set; }
    public Vector3 Position { get; set; }
    public void Apply()
    {
        NetId netId = new NetId(InstanceId);
        if (!NavigationNetIdDatabase.TryGetNavigation(netId, out byte nav))
        {
            Logger.LogWarning($"Unknown navigation flag with NetId: {netId.Format()}.");
            return;
        }

        NavigationUtil.SetFlagPositionLocal(nav, Position);
    }
#if SERVER
    public bool CheckCanApply()
    {
        NetId netId = new NetId(InstanceId);
        if (!NavigationNetIdDatabase.TryGetNavigation(netId, out _))
        {
            Logger.LogWarning($"Unknown navigation flag with NetId: {netId.Format()}.");
            return false;
        }

        return VanillaPermissions.EditNavigation.Has(Instigator.m_SteamID, true);
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

[Action(DevkitServerActionType.DeleteNavigation, 4, 4)]
[EarlyTypeInit]
public sealed class DeleteNavigationAction : IAction, IInstanceIdAction
{
    public DevkitServerActionType Type => DevkitServerActionType.DeleteNavigation;
    public CSteamID Instigator { get; set; }
    public uint InstanceId { get; set; }
    public float DeltaTime { get; set; }
    public void Apply()
    {
        NetId netId = new NetId(InstanceId);
        if (!NavigationNetIdDatabase.TryGetNavigation(netId, out byte nav))
        {
            Logger.LogWarning($"Unknown navigation flag with NetId: {netId.Format()}.");
            return;
        }

        NavigationUtil.RemoveFlagLocal(nav);
    }
#if SERVER
    public bool CheckCanApply()
    {
        NetId netId = new NetId(InstanceId);
        if (!NavigationNetIdDatabase.TryGetNavigation(netId, out _))
        {
            Logger.LogWarning($"Unknown navigation flag with NetId: {netId.Format()}.");
            return false;
        }

        return VanillaPermissions.EditNavigation.Has(Instigator.m_SteamID, true);
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