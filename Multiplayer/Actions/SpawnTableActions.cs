using DevkitServer.API.Devkit.Spawns;
using DevkitServer.Models;
using DevkitServer.Multiplayer.Levels;
using DevkitServer.Util.Encoding;
using System.Text;
#if SERVER
using DevkitServer.API.Permissions;
using DevkitServer.Core.Permissions;
#endif

namespace DevkitServer.Multiplayer.Actions;
public class SpawnTableActions
{
    internal const string Source = "SpawnTableActions";
    public EditorActions EditorActions { get; }
    internal SpawnTableActions(EditorActions actions)
    {
        EditorActions = actions;
    }
    public void Subscribe()
    {
#if CLIENT
        if (EditorActions.IsOwner)
        {
            ClientEvents.OnDeleteSpawnTable += OnDeleteSpawnTable;
            ClientEvents.OnDeleteSpawnTableTier += OnDeleteSpawnTableTier;
            ClientEvents.OnDeleteSpawnTableTierAsset += OnDeleteSpawnTableTierAsset;
            ClientEvents.OnSetSpawnTableColor += OnSetSpawnTableColor;
            ClientEvents.OnSetSpawnTableName += OnSetSpawnTableName;
            ClientEvents.OnSetSpawnTableSpawnAsset += OnSetSpawnTableSpawnAsset;
            ClientEvents.OnSetSpawnTableTierAsset += OnSetSpawnTableTierAsset;
            ClientEvents.OnSetSpawnTableTierChances += OnSetSpawnTableTierChances;
            ClientEvents.OnSetSpawnTableTierName += OnSetSpawnTableTierName;
            ClientEvents.OnSetZombieSpawnTableDamage += OnSetZombieSpawnTableDamage;
            ClientEvents.OnSetZombieSpawnTableDifficultyAsset += OnSetZombieSpawnTableDifficultyAsset;
            ClientEvents.OnSetZombieSpawnTableHealth += OnSetZombieSpawnTableHealth;
            ClientEvents.OnSetZombieSpawnTableIsMega += OnSetZombieSpawnTableIsMega;
            ClientEvents.OnSetZombieSpawnTableLootIndex += OnSetZombieSpawnTableLootIndex;
            ClientEvents.OnSetZombieSpawnTableRegen += OnSetZombieSpawnTableRegen;
            ClientEvents.OnSetZombieSpawnTableXP += OnSetZombieSpawnTableXP;
        }
#endif
    }
    public void Unsubscribe()
    {
#if CLIENT
        if (EditorActions.IsOwner)
        {
            ClientEvents.OnDeleteSpawnTable -= OnDeleteSpawnTable;
            ClientEvents.OnDeleteSpawnTableTier -= OnDeleteSpawnTableTier;
            ClientEvents.OnDeleteSpawnTableTierAsset -= OnDeleteSpawnTableTierAsset;
            ClientEvents.OnSetSpawnTableColor -= OnSetSpawnTableColor;
            ClientEvents.OnSetSpawnTableName -= OnSetSpawnTableName;
            ClientEvents.OnSetSpawnTableSpawnAsset -= OnSetSpawnTableSpawnAsset;
            ClientEvents.OnSetSpawnTableTierAsset -= OnSetSpawnTableTierAsset;
            ClientEvents.OnSetSpawnTableTierChances -= OnSetSpawnTableTierChances;
            ClientEvents.OnSetSpawnTableTierName -= OnSetSpawnTableTierName;
            ClientEvents.OnSetZombieSpawnTableDamage -= OnSetZombieSpawnTableDamage;
            ClientEvents.OnSetZombieSpawnTableDifficultyAsset -= OnSetZombieSpawnTableDifficultyAsset;
            ClientEvents.OnSetZombieSpawnTableHealth -= OnSetZombieSpawnTableHealth;
            ClientEvents.OnSetZombieSpawnTableIsMega -= OnSetZombieSpawnTableIsMega;
            ClientEvents.OnSetZombieSpawnTableLootIndex -= OnSetZombieSpawnTableLootIndex;
            ClientEvents.OnSetZombieSpawnTableRegen -= OnSetZombieSpawnTableRegen;
            ClientEvents.OnSetZombieSpawnTableXP -= OnSetZombieSpawnTableXP;
        }
#endif
    }
#if CLIENT
    private void OnDeleteSpawnTable(in DeleteSpawnTableProperties properties)
    {
        EditorActions.QueueAction(new DeleteSpawnTableAction
        {
            SpawnType = properties.SpawnType,
            NetId = properties.SpawnTableId,
            DeltaTime = properties.DeltaTime
        });
    }
    private void OnDeleteSpawnTableTier(in DeleteSpawnTableTierProperties properties)
    {
        EditorActions.QueueAction(new DeleteSpawnTableTierAction
        {
            SpawnType = properties.SpawnType,
            NetId = properties.SpawnTierId,
            DeltaTime = properties.DeltaTime
        });
    }
    private void OnDeleteSpawnTableTierAsset(in DeleteSpawnTableTierAssetProperties properties)
    {
        EditorActions.QueueAction(new DeleteSpawnTableTierAssetAction
        {
            SpawnType = properties.SpawnType,
            NetId = properties.SpawnAssetId,
            DeltaTime = properties.DeltaTime
        });
    }
    private void OnSetSpawnTableTierName(in SetSpawnTableTierNameProperties properties)
    {
        EditorActions.QueueAction(new SetSpawnTableTierNameAction
        {
            SpawnType = properties.SpawnType,
            NetId = properties.SpawnTierId,
            Name = properties.Name,
            DeltaTime = properties.DeltaTime
        });
    }
    private void OnSetSpawnTableTierChances(in SetSpawnTableTierChancesProperties properties)
    {
        SetSpawnTableTierChancesAction? existingAction = EditorActions.FindFirstPendingAction<SetSpawnTableTierChancesAction>();

        // try to apply to existing explicitly since this could be called quite often
        if (existingAction != null)
        {
            if (existingAction.NetIds.Length != properties.SpawnTierIds.Count)
                goto queue;
            
            for (int i = 0; i < existingAction.NetIds.Length; i++)
            {
                if (existingAction.NetIds[i] != properties.SpawnTierIds[i])
                    goto queue;
            }

            properties.SpawnTierIds.CopyTo(existingAction.NetIds);
            properties.Chances.CopyTo(existingAction.Chances);
            Logger.DevkitServer.LogConditional(Source, "SetSpawnTableTierChancesAction replaced.");
            return;
        }

        queue:
        EditorActions.QueueAction(new SetSpawnTableTierChancesAction
        {
            SpawnType = properties.SpawnType,
            NetIds = properties.SpawnTierIds.ToArray(),
            Chances = properties.Chances.ToArray(),
            DeltaTime = properties.DeltaTime
        });
    }
    private void OnSetSpawnTableTierAsset(in SetSpawnTableTierAssetProperties properties)
    {
        EditorActions.QueueAction(new SetSpawnTableTierAssetAction
        {
            SpawnType = properties.SpawnType,
            NetId = properties.SpawnTierId,
            Asset = properties.Asset,
            DeltaTime = properties.DeltaTime
        });
    }
    private void OnSetSpawnTableSpawnAsset(in SetSpawnTableSpawnAssetProperties properties)
    {
        EditorActions.QueueAction(new SetSpawnTableSpawnAssetAction
        {
            SpawnType = properties.SpawnType,
            NetId = properties.SpawnTableId,
            Asset = properties.Asset,
            DeltaTime = properties.DeltaTime
        });
    }
    private void OnSetSpawnTableName(in SetSpawnTableNameProperties properties)
    {
        EditorActions.QueueAction(new SetSpawnTableNameAction
        {
            SpawnType = properties.SpawnType,
            NetId = properties.SpawnTableId,
            Name = properties.Name,
            DeltaTime = properties.DeltaTime
        });
    }
    private void OnSetSpawnTableColor(in SetSpawnTableColorProperties properties)
    {
        EditorActions.QueueAction(new SetSpawnTableColorAction
        {
            SpawnType = properties.SpawnType,
            NetId = properties.SpawnTableId,
            Color = properties.Color,
            DeltaTime = properties.DeltaTime
        });
    }
    private void OnSetZombieSpawnTableXP(in SetZombieSpawnTableXPProperties properties)
    {
        EditorActions.QueueAction(new SetZombieSpawnTableXPAction
        {
            NetId = properties.SpawnTableId,
            XP = properties.XP,
            DeltaTime = properties.DeltaTime
        });
    }
    private void OnSetZombieSpawnTableRegen(in SetZombieSpawnTableRegenProperties properties)
    {
        EditorActions.QueueAction(new SetZombieSpawnTableRegenAction
        {
            NetId = properties.SpawnTableId,
            Regen = properties.Regen,
            DeltaTime = properties.DeltaTime
        });
    }
    private void OnSetZombieSpawnTableLootIndex(in SetZombieSpawnTableLootIndexProperties properties)
    {
        EditorActions.QueueAction(new SetZombieSpawnTableLootIndexAction
        {
            NetId = properties.SpawnTableId,
            LootTableNetId = properties.LootTableNetId,
            DeltaTime = properties.DeltaTime
        });
    }
    private void OnSetZombieSpawnTableIsMega(in SetZombieSpawnTableIsMegaProperties properties)
    {
        EditorActions.QueueAction(new SetZombieSpawnTableIsMegaAction
        {
            NetId = properties.SpawnTableId,
            IsMega = properties.IsMega,
            DeltaTime = properties.DeltaTime
        });
    }
    private void OnSetZombieSpawnTableHealth(in SetZombieSpawnTableHealthProperties properties)
    {
        EditorActions.QueueAction(new SetZombieSpawnTableHealthAction
        {
            NetId = properties.SpawnTableId,
            Health = properties.Health,
            DeltaTime = properties.DeltaTime
        });
    }
    private void OnSetZombieSpawnTableDifficultyAsset(in SetZombieSpawnTableDifficultyAssetProperties properties)
    {
        EditorActions.QueueAction(new SetZombieSpawnTableDifficultyAssetAction
        {
            NetId = properties.SpawnTableId,
            Asset = properties.DifficultyAsset,
            DeltaTime = properties.DeltaTime
        });
    }
    private void OnSetZombieSpawnTableDamage(in SetZombieSpawnTableDamageProperties properties)
    {
        EditorActions.QueueAction(new SetZombieSpawnTableDamageAction
        {
            NetId = properties.SpawnTableId,
            Damage = properties.Damage,
            DeltaTime = properties.DeltaTime
        });
    }
#endif
}

[Action(DevkitServerActionType.DeleteSpawnTable, 5, 8)]
public sealed class DeleteSpawnTableAction : IAction, INetId64Action
{
    public DevkitServerActionType Type => DevkitServerActionType.DeleteSpawnTable;
    public CSteamID Instigator { get; set; }
    public NetId64 NetId { get; set; }
    public SpawnType SpawnType { get; set; }
    public float DeltaTime { get; set; }
    public void Apply()
    {
        if (!SpawnsNetIdDatabase.TryGetSpawnTable(SpawnType, NetId, out int index))
        {
            Logger.DevkitServer.LogWarning(nameof(DeleteSpawnTableAction), $"Unknown {SpawnType.ToString().ToLowerInvariant()} spawn table with NetId: {NetId.Format()}.");
            return;
        }

        SpawnTableUtil.RemoveSpawnTableLocal(SpawnType, index);
    }
#if SERVER
    public bool CheckCanApply()
    {
        if (!SpawnsNetIdDatabase.TryGetSpawnTable(SpawnType, NetId, out _))
        {
            Logger.DevkitServer.LogWarning(nameof(DeleteSpawnTableAction), $"Unknown {SpawnType.ToString().ToLowerInvariant()} spawn table with NetId: {NetId.Format()}.");
            return false;
        }

        return VanillaPermissions.SpawnTablesDelete(SpawnType).Has(Instigator.m_SteamID, true);
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        SpawnType = (SpawnType)reader.ReadUInt8();
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write((byte)SpawnType);
    }
    public int CalculateSize() => 5;
}

[Action(DevkitServerActionType.DeleteSpawnTableTier, 5, 8)]
public sealed class DeleteSpawnTableTierAction : IAction, INetId64Action
{
    public DevkitServerActionType Type => DevkitServerActionType.DeleteSpawnTableTier;
    public CSteamID Instigator { get; set; }
    public NetId64 NetId { get; set; }
    public SpawnType SpawnType { get; set; }
    public float DeltaTime { get; set; }
    public void Apply()
    {
        if (SpawnType == SpawnType.Zombie)
        {
            Logger.DevkitServer.LogWarning(nameof(DeleteSpawnTableTierAction), "Tried to delete zombie tier.");
            return;
        }
        if (!SpawnsNetIdDatabase.TryGetSpawnTableTier(SpawnType, NetId, out SpawnTierIdentifier identifier))
        {
            Logger.DevkitServer.LogWarning(nameof(DeleteSpawnTableTierAction), $"Unknown {SpawnType.ToString().ToLowerInvariant()} spawn table tier with NetId: {NetId.Format()}.");
            return;
        }

        SpawnTableUtil.RemoveSpawnTableTierLocal(identifier);
    }
#if SERVER
    public bool CheckCanApply()
    {
        if (SpawnType == SpawnType.Zombie)
        {
            Logger.DevkitServer.LogWarning(nameof(DeleteSpawnTableTierAction), "Tried to delete zombie tier.");
            return false;
        }
        if (!SpawnsNetIdDatabase.TryGetSpawnTableTier(SpawnType, NetId, out _))
        {
            Logger.DevkitServer.LogWarning(nameof(DeleteSpawnTableTierAction), $"Unknown {SpawnType.ToString().ToLowerInvariant()} spawn table tier with NetId: {NetId.Format()}.");
            return false;
        }

        return VanillaPermissions.SpawnTablesEdit(SpawnType).Has(Instigator.m_SteamID, true);
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        SpawnType = (SpawnType)reader.ReadUInt8();
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write((byte)SpawnType);
    }
    public int CalculateSize() => 5;
}

[Action(DevkitServerActionType.DeleteSpawnTableTierAsset, 5, 8)]
public sealed class DeleteSpawnTableTierAssetAction : IAction, INetId64Action
{
    public DevkitServerActionType Type => DevkitServerActionType.DeleteSpawnTableTierAsset;
    public CSteamID Instigator { get; set; }
    public NetId64 NetId { get; set; }
    public SpawnType SpawnType { get; set; }
    public float DeltaTime { get; set; }
    public void Apply()
    {
        if (!SpawnsNetIdDatabase.TryGetSpawnTableTierAsset(SpawnType, NetId, out SpawnAssetIdentifier identifier))
        {
            Logger.DevkitServer.LogWarning(nameof(DeleteSpawnTableTierAssetAction), $"Unknown {SpawnType.ToString().ToLowerInvariant()} spawn table tier asset with NetId: {NetId.Format()}.");
            return;
        }

        SpawnTableUtil.RemoveSpawnTableTierAssetLocal(identifier);
    }
#if SERVER
    public bool CheckCanApply()
    {
        if (!SpawnsNetIdDatabase.TryGetSpawnTableTierAsset(SpawnType, NetId, out _))
        {
            Logger.DevkitServer.LogWarning(nameof(DeleteSpawnTableTierAssetAction), $"Unknown {SpawnType.ToString().ToLowerInvariant()} spawn table tier asset with NetId: {NetId.Format()}.");
            return false;
        }

        return VanillaPermissions.SpawnTablesEdit(SpawnType).Has(Instigator.m_SteamID, true);
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        SpawnType = (SpawnType)reader.ReadUInt8();
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write((byte)SpawnType);
    }
    public int CalculateSize() => 5;
}

[Action(DevkitServerActionType.SetSpawnTableTierName, 69, 8)]
public sealed class SetSpawnTableTierNameAction : IReplacableAction, INetId64Action
{
    public DevkitServerActionType Type => DevkitServerActionType.SetSpawnTableTierName;
    public CSteamID Instigator { get; set; }
    public NetId64 NetId { get; set; }
    public SpawnType SpawnType { get; set; }
    public float DeltaTime { get; set; }
    public string Name { get; set; } = null!;
    public void Apply()
    {
        if (SpawnType == SpawnType.Zombie)
        {
            Logger.DevkitServer.LogWarning(nameof(SetSpawnTableTierNameAction), "Tried to set name of a zombie tier.");
            return;
        }
        if (!SpawnsNetIdDatabase.TryGetSpawnTableTier(SpawnType, NetId, out SpawnTierIdentifier identifier))
        {
            Logger.DevkitServer.LogWarning(nameof(SetSpawnTableTierNameAction), $"Unknown {SpawnType.ToString().ToLowerInvariant()} spawn table tier with NetId: {NetId.Format()}.");
            return;
        }

        SpawnTableUtil.SetSpawnTableTierNameLocal(identifier, Name);
    }
#if SERVER
    public bool CheckCanApply()
    {
        if (SpawnType == SpawnType.Zombie)
        {
            Logger.DevkitServer.LogWarning(nameof(SetSpawnTableTierNameAction), "Tried to set name of a zombie tier.");
            return false;
        }
        if (!SpawnsNetIdDatabase.TryGetSpawnTableTier(SpawnType, NetId, out _))
        {
            Logger.DevkitServer.LogWarning(nameof(SetSpawnTableTierNameAction), $"Unknown {SpawnType.ToString().ToLowerInvariant()} spawn table tier with NetId: {NetId.Format()}.");
            return false;
        }

        return VanillaPermissions.SpawnTablesEdit(SpawnType).Has(Instigator.m_SteamID, true);
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        SpawnType = (SpawnType)reader.ReadUInt8();
        Name = reader.ReadShortString();
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write((byte)SpawnType);
        writer.WriteShort(Name);
    }
    public int CalculateSize() => Encoding.UTF8.GetByteCount(Name) + 6;

    public bool TryReplaceFrom(IReplacableAction action)
    {
        SetSpawnTableTierNameAction a = (SetSpawnTableTierNameAction)action;
        if (a.NetId.Id != NetId.Id)
            return false;

        Name = a.Name;
        return true;
    }
}

[Action(DevkitServerActionType.SetSpawnTableTierChances, 3052, 0)]
public sealed class SetSpawnTableTierChancesAction : IReplacableAction
{
    public DevkitServerActionType Type => DevkitServerActionType.SetSpawnTableTierChances;
    public CSteamID Instigator { get; set; }
    public NetId64[] NetIds { get; set; } = null!;
    public SpawnType SpawnType { get; set; }
    public float DeltaTime { get; set; }
    public float[] Chances { get; set; } = null!;
#if SERVER
    private SpawnTierIdentifier[]? _tiers;
#endif
    public void Apply()
    {
        int l = Math.Min(byte.MaxValue, Math.Min(NetIds.Length, Chances.Length));
        if (l == 0)
            return;
#if SERVER
        SpawnTierIdentifier[]? tiers = _tiers;
#else
        SpawnTierIdentifier[]? tiers = null;
#endif
        int table = -1;
        SpawnType spawnType = (SpawnType)byte.MaxValue;
        for (int i = 0; i < l; ++i)
        {
            SpawnTierIdentifier identifier;
            if (tiers != null)
                identifier = tiers[i];
            else if (!SpawnsNetIdDatabase.TryGetSpawnTableTier(SpawnType, NetIds[i], out identifier))
            {
                Logger.DevkitServer.LogWarning(nameof(SetSpawnTableTierChancesAction), $"Unknown {SpawnType.ToString().ToLowerInvariant()} spawn table tier with NetId: {NetIds[i].Format()}.");
                continue;
            }

            SpawnTableUtil.SetSpawnTableTierChanceLocal(identifier, Chances[i]);
            if (table == -1)
                table = identifier.TableIndex;
            else if (table != identifier.TableIndex)
                table = -2;
            if (spawnType == (SpawnType)byte.MaxValue)
                spawnType = identifier.Type;
            else if (spawnType != identifier.Type)
                spawnType = SpawnType.None;
        }

        if (spawnType == SpawnType.Zombie)
            return;

        // normalize if used all the chances in the table
        if (table > 0 && spawnType != SpawnType.None && l == SpawnTableUtil.GetTableCountUnsafe(spawnType))
        {
            SpawnTableUtil.NormalizeChancesLocal(spawnType, table);
            // todo sync chances if auth
        }
    }
#if SERVER
    public bool CheckCanApply()
    {
        int l = Math.Min(byte.MaxValue, Math.Min(NetIds.Length, Chances.Length));
        if (l == 0)
            return true;
        _tiers = new SpawnTierIdentifier[l];
        for (int i = 0; i < l; ++i)
        {
            if (!SpawnsNetIdDatabase.TryGetSpawnTableTier(SpawnType, NetIds[i], out SpawnTierIdentifier identifier))
            {
                Logger.DevkitServer.LogWarning(nameof(SetSpawnTableTierChancesAction), $"Unknown {SpawnType.ToString().ToLowerInvariant()} spawn table tier with NetId: {NetIds[i].Format()}.");
                _tiers = null;
                return false;
            }

            _tiers[i] = identifier;
        }

        return VanillaPermissions.SpawnTablesEdit(SpawnType).Has(Instigator.m_SteamID, true);
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();

        int l = reader.ReadUInt8();
        NetIds = new NetId64[l];
        Chances = new float[l];
        for (int i = 0; i < l; ++i)
        {
            NetIds[i] = new NetId64(reader.ReadUInt64());
            Chances[i] = reader.ReadFloat();
        }
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);

        int l = Math.Min(byte.MaxValue, Math.Min(NetIds.Length, Chances.Length));
        writer.Write((byte)l);
        for (int i = 0; i < l; ++i)
        {
            writer.Write(NetIds[i].Id);
            writer.Write(Chances[i]);
        }
    }
    public int CalculateSize() => 5 + NetIds.Length * 12;

    public bool TryReplaceFrom(IReplacableAction action)
    {
        SetSpawnTableTierChancesAction a = (SetSpawnTableTierChancesAction)action;

        if (NetIds.Length != a.NetIds.Length)
            return false;

        for (int i = 0; i < NetIds.Length; i++)
        {
            if (NetIds[i] != a.NetIds[i])
                return false;
        }

        NetIds = a.NetIds;
        Chances = a.Chances;
        return true;
    }
}

[Action(DevkitServerActionType.SetSpawnTableTierAsset, 21, 8)]
public sealed class SetSpawnTableTierAssetAction : IReplacableAction, INetId64Action
{
    public DevkitServerActionType Type => DevkitServerActionType.SetSpawnTableTierAsset;
    public CSteamID Instigator { get; set; }
    public NetId64 NetId { get; set; }
    public SpawnType SpawnType { get; set; }
    public float DeltaTime { get; set; }
    public AssetReference<Asset> Asset { get; set; }
    public void Apply()
    {
        if (!SpawnsNetIdDatabase.TryGetSpawnTableTierAsset(SpawnType, NetId, out SpawnAssetIdentifier identifier))
        {
            Logger.DevkitServer.LogWarning(nameof(SetSpawnTableTierAssetAction), $"Unknown {SpawnType.ToString().ToLowerInvariant()} spawn table tier asset with NetId: {NetId.Format()}.");
            return;
        }

        if (!Asset.isNull && Asset.Find() == null)
            Logger.DevkitServer.LogWarning(nameof(SetSpawnTableTierAssetAction), $"Unknown spawn asset while setting {SpawnType.ToString().ToLowerInvariant()} spawn table asset ({Asset.GUID.Format()})");

        SpawnTableUtil.SetSpawnTableTierAssetLocal(identifier, Asset);
    }
#if SERVER
    public bool CheckCanApply()
    {
        if (!SpawnsNetIdDatabase.TryGetSpawnTableTierAsset(SpawnType, NetId, out _))
        {
            Logger.DevkitServer.LogWarning(nameof(SetSpawnTableTierAssetAction), $"Unknown {SpawnType.ToString().ToLowerInvariant()} spawn table tier asset with NetId: {NetId.Format()}.");
            return false;
        }

        return VanillaPermissions.SpawnTablesEdit(SpawnType).Has(Instigator.m_SteamID, true);
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        SpawnType = (SpawnType)reader.ReadUInt8();
        Asset = new AssetReference<Asset>(reader.ReadGuid());
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write((byte)SpawnType);
        writer.Write(Asset.GUID);
    }
    public int CalculateSize() => 21;
    public bool TryReplaceFrom(IReplacableAction action)
    {
        SetSpawnTableTierAssetAction a = (SetSpawnTableTierAssetAction)action;
        if (a.NetId.Id != NetId.Id)
            return false;

        Asset = a.Asset;
        return true;
    }
}

[Action(DevkitServerActionType.SetSpawnTableSpawnAsset, 21, 8)]
public sealed class SetSpawnTableSpawnAssetAction : IReplacableAction, INetId64Action
{
    public DevkitServerActionType Type => DevkitServerActionType.SetSpawnTableSpawnAsset;
    public CSteamID Instigator { get; set; }
    public NetId64 NetId { get; set; }
    public SpawnType SpawnType { get; set; }
    public float DeltaTime { get; set; }
    public AssetReference<SpawnAsset> Asset { get; set; }
    public void Apply()
    {
        if (!SpawnsNetIdDatabase.TryGetSpawnTable(SpawnType, NetId, out int index))
        {
            Logger.DevkitServer.LogWarning(nameof(SetSpawnTableSpawnAssetAction), $"Unknown {SpawnType.ToString().ToLowerInvariant()} spawn table with NetId: {NetId.Format()}.");
            return;
        }

        if (!Asset.isNull && Asset.Find() == null)
            Logger.DevkitServer.LogWarning(nameof(SetSpawnTableSpawnAssetAction), $"Unknown spawn asset while setting {SpawnType.ToString().ToLowerInvariant()} spawn table asset ({Asset.GUID.Format()})");

        SpawnTableUtil.SetSpawnTableSpawnAssetLocal(SpawnType, index, Asset);
    }
#if SERVER
    public bool CheckCanApply()
    {
        if (!SpawnsNetIdDatabase.TryGetSpawnTable(SpawnType, NetId, out _))
        {
            Logger.DevkitServer.LogWarning(nameof(SetSpawnTableSpawnAssetAction), $"Unknown {SpawnType.ToString().ToLowerInvariant()} spawn table with NetId: {NetId.Format()}.");
            return false;
        }
        return VanillaPermissions.SpawnTablesEdit(SpawnType).Has(Instigator.m_SteamID, true);
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        SpawnType = (SpawnType)reader.ReadUInt8();
        Asset = new AssetReference<SpawnAsset>(reader.ReadGuid());
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write((byte)SpawnType);
        writer.Write(Asset.GUID);
    }
    public int CalculateSize() => 21;
    public bool TryReplaceFrom(IReplacableAction action)
    {
        SetSpawnTableSpawnAssetAction a = (SetSpawnTableSpawnAssetAction)action;
        if (a.NetId.Id != NetId.Id)
            return false;

        Asset = a.Asset;
        return true;
    }
}

[Action(DevkitServerActionType.SetSpawnTableName, 69, 8)]
public sealed class SetSpawnTableNameAction : IReplacableAction, INetId64Action
{
    public DevkitServerActionType Type => DevkitServerActionType.SetSpawnTableName;
    public CSteamID Instigator { get; set; }
    public NetId64 NetId { get; set; }
    public SpawnType SpawnType { get; set; }
    public float DeltaTime { get; set; }
    public string Name { get; set; } = null!;
    public void Apply()
    {
        if (!SpawnsNetIdDatabase.TryGetSpawnTable(SpawnType, NetId, out int index))
        {
            Logger.DevkitServer.LogWarning(nameof(SetSpawnTableNameAction), $"Unknown {SpawnType.ToString().ToLowerInvariant()} spawn table with NetId: {NetId.Format()}.");
            return;
        }

        SpawnTableUtil.SetSpawnTableNameLocal(SpawnType, index, Name);
    }
#if SERVER
    public bool CheckCanApply()
    {
        if (!SpawnsNetIdDatabase.TryGetSpawnTable(SpawnType, NetId, out _))
        {
            Logger.DevkitServer.LogWarning(nameof(SetSpawnTableNameAction), $"Unknown {SpawnType.ToString().ToLowerInvariant()} spawn table with NetId: {NetId.Format()}.");
            return false;
        }

        return VanillaPermissions.SpawnTablesEdit(SpawnType).Has(Instigator.m_SteamID, true);
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        SpawnType = (SpawnType)reader.ReadUInt8();
        Name = reader.ReadShortString();
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write((byte)SpawnType);
        writer.WriteShort(Name);
    }
    public int CalculateSize() => Encoding.UTF8.GetByteCount(Name) + 6;

    public bool TryReplaceFrom(IReplacableAction action)
    {
        SetSpawnTableNameAction a = (SetSpawnTableNameAction)action;
        if (a.NetId.Id != NetId.Id)
            return false;

        Name = a.Name;
        return true;
    }
}

[Action(DevkitServerActionType.SetSpawnTableColor, 17, 8)]
public sealed class SetSpawnTableColorAction : IReplacableAction, INetId64Action
{
    public DevkitServerActionType Type => DevkitServerActionType.SetSpawnTableColor;
    public CSteamID Instigator { get; set; }
    public NetId64 NetId { get; set; }
    public SpawnType SpawnType { get; set; }
    public float DeltaTime { get; set; }
    public Color Color { get; set; }
    public void Apply()
    {
        if (!SpawnsNetIdDatabase.TryGetSpawnTable(SpawnType, NetId, out int index))
        {
            Logger.DevkitServer.LogWarning(nameof(SetSpawnTableColorAction), $"Unknown {SpawnType.ToString().ToLowerInvariant()} spawn table with NetId: {NetId.Format()}.");
            return;
        }

        SpawnTableUtil.SetSpawnTableColorLocal(SpawnType, index, Color);
    }
#if SERVER
    public bool CheckCanApply()
    {
        if (!SpawnsNetIdDatabase.TryGetSpawnTable(SpawnType, NetId, out _))
        {
            Logger.DevkitServer.LogWarning(nameof(SetSpawnTableColorAction), $"Unknown {SpawnType.ToString().ToLowerInvariant()} spawn table with NetId: {NetId.Format()}.");
            return false;
        }

        return VanillaPermissions.SpawnTablesEdit(SpawnType).Has(Instigator.m_SteamID, true);
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        SpawnType = (SpawnType)reader.ReadUInt8();
        Vector3 c = reader.ReadVector3();
        Color = new Color(c.x, c.y, c.z, 1f);
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write((byte)SpawnType);
        writer.Write(new Vector3(Color.r, Color.g, Color.b));
    }
    public int CalculateSize() => 17;
    public bool TryReplaceFrom(IReplacableAction action)
    {
        SetSpawnTableColorAction a = (SetSpawnTableColorAction)action;
        if (a.NetId.Id != NetId.Id)
            return false;

        Color = a.Color;
        return true;
    }
}

[Action(DevkitServerActionType.SetZombieSpawnTableXP, 9, 8)]
public sealed class SetZombieSpawnTableXPAction : IReplacableAction, INetId64Action
{
    public DevkitServerActionType Type => DevkitServerActionType.SetZombieSpawnTableXP;
    public CSteamID Instigator { get; set; }
    public NetId64 NetId { get; set; }
    public float DeltaTime { get; set; }
    public uint XP { get; set; }
    public void Apply()
    {
        if (!SpawnsNetIdDatabase.TryGetZombieSpawnTable(NetId, out int index))
        {
            Logger.DevkitServer.LogWarning(nameof(SetZombieSpawnTableXPAction), $"Unknown zombie spawn table with NetId: {NetId.Format()}.");
            return;
        }

        SpawnTableUtil.SetZombieSpawnTableXPLocal(index, XP);
    }
#if SERVER
    public bool CheckCanApply()
    {
        if (!SpawnsNetIdDatabase.TryGetZombieSpawnTable(NetId, out int _))
        {
            Logger.DevkitServer.LogWarning(nameof(SetZombieSpawnTableXPAction), $"Unknown zombie spawn table with NetId: {NetId.Format()}.");
            return false;
        }

        return VanillaPermissions.SpawnTablesZombieEdit.Has(Instigator.m_SteamID, true);
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        XP = reader.ReadUInt32();
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write(XP);
    }
    public int CalculateSize() => 9;
    public bool TryReplaceFrom(IReplacableAction action)
    {
        SetZombieSpawnTableXPAction a = (SetZombieSpawnTableXPAction)action;
        if (a.NetId.Id != NetId.Id)
            return false;

        XP = a.XP;
        return true;
    }
}

[Action(DevkitServerActionType.SetZombieSpawnTableRegen, 9, 8)]
public sealed class SetZombieSpawnTableRegenAction : IReplacableAction, INetId64Action
{
    public DevkitServerActionType Type => DevkitServerActionType.SetZombieSpawnTableRegen;
    public CSteamID Instigator { get; set; }
    public NetId64 NetId { get; set; }
    public float DeltaTime { get; set; }
    public float Regen { get; set; }
    public void Apply()
    {
        if (!SpawnsNetIdDatabase.TryGetZombieSpawnTable(NetId, out int index))
        {
            Logger.DevkitServer.LogWarning(nameof(SetZombieSpawnTableRegenAction), $"Unknown zombie spawn table with NetId: {NetId.Format()}.");
            return;
        }

        SpawnTableUtil.SetZombieSpawnTableRegenLocal(index, Regen);
    }
#if SERVER
    public bool CheckCanApply()
    {
        if (!SpawnsNetIdDatabase.TryGetZombieSpawnTable(NetId, out int _))
        {
            Logger.DevkitServer.LogWarning(nameof(SetZombieSpawnTableRegenAction), $"Unknown zombie spawn table with NetId: {NetId.Format()}.");
            return false;
        }

        return VanillaPermissions.SpawnTablesZombieEdit.Has(Instigator.m_SteamID, true);
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        Regen = reader.ReadFloat();
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write(Regen);
    }
    public int CalculateSize() => 9;
    public bool TryReplaceFrom(IReplacableAction action)
    {
        SetZombieSpawnTableRegenAction a = (SetZombieSpawnTableRegenAction)action;
        if (a.NetId.Id != NetId.Id)
            return false;

        Regen = a.Regen;
        return true;
    }
}

[Action(DevkitServerActionType.SetZombieSpawnTableLootIndex, 13, 8)]
public sealed class SetZombieSpawnTableLootIndexAction : IReplacableAction, INetId64Action
{
    public DevkitServerActionType Type => DevkitServerActionType.SetZombieSpawnTableLootIndex;
    public CSteamID Instigator { get; set; }
    public NetId64 NetId { get; set; }
    public float DeltaTime { get; set; }
    public NetId64 LootTableNetId { get; set; }
    public void Apply()
    {
        if (!SpawnsNetIdDatabase.TryGetZombieSpawnTable(NetId, out int index))
        {
            Logger.DevkitServer.LogWarning(nameof(SetZombieSpawnTableLootIndexAction), $"Unknown zombie spawn table with NetId: {NetId.Format()}.");
            return;
        }
        if (!SpawnsNetIdDatabase.TryGetItemSpawnTable(LootTableNetId, out int itemIndex))
        {
            Logger.DevkitServer.LogWarning(nameof(SetZombieSpawnTableLootIndexAction), $"Unknown item spawn table with NetId: {LootTableNetId.Format()} while setting zombie table loot index.");
            return;
        }

        SpawnTableUtil.SetZombieSpawnTableLootIndexLocal(index, (byte)itemIndex);
    }
#if SERVER
    public bool CheckCanApply()
    {
        if (!SpawnsNetIdDatabase.TryGetZombieSpawnTable(NetId, out int _))
        {
            Logger.DevkitServer.LogWarning(nameof(SetZombieSpawnTableLootIndexAction), $"Unknown zombie spawn table with NetId: {NetId.Format()}.");
            return false;
        }
        if (!SpawnsNetIdDatabase.TryGetItemSpawnTable(LootTableNetId, out int _))
        {
            Logger.DevkitServer.LogWarning(nameof(SetZombieSpawnTableLootIndexAction), $"Unknown item spawn table with NetId: {LootTableNetId.Format()} while setting zombie table loot index.");
            return false;
        }

        return VanillaPermissions.SpawnTablesZombieEdit.Has(Instigator.m_SteamID, true);
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        LootTableNetId = reader.ReadNetId64();
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write(LootTableNetId);
    }
    public int CalculateSize() => 13;
    public bool TryReplaceFrom(IReplacableAction action)
    {
        SetZombieSpawnTableLootIndexAction a = (SetZombieSpawnTableLootIndexAction)action;
        if (a.NetId.Id != NetId.Id)
            return false;

        LootTableNetId = a.LootTableNetId;
        return true;
    }
}

[Action(DevkitServerActionType.SetZombieSpawnTableIsMega, 5, 8)]
public sealed class SetZombieSpawnTableIsMegaAction : IReplacableAction, INetId64Action
{
    public DevkitServerActionType Type => DevkitServerActionType.SetZombieSpawnTableIsMega;
    public CSteamID Instigator { get; set; }
    public NetId64 NetId { get; set; }
    public float DeltaTime { get; set; }
    public bool IsMega { get; set; }
    public void Apply()
    {
        if (!SpawnsNetIdDatabase.TryGetZombieSpawnTable(NetId, out int index))
        {
            Logger.DevkitServer.LogWarning(nameof(SetZombieSpawnTableIsMegaAction), $"Unknown zombie spawn table with NetId: {NetId.Format()}.");
            return;
        }

        SpawnTableUtil.SetZombieSpawnTableIsMegaLocal(index, IsMega);
    }
#if SERVER
    public bool CheckCanApply()
    {
        if (!SpawnsNetIdDatabase.TryGetZombieSpawnTable(NetId, out int _))
        {
            Logger.DevkitServer.LogWarning(nameof(SetZombieSpawnTableIsMegaAction), $"Unknown zombie spawn table with NetId: {NetId.Format()}.");
            return false;
        }

        return VanillaPermissions.SpawnTablesZombieEdit.Has(Instigator.m_SteamID, true);
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        IsMega = reader.ReadBool();
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write(IsMega);
    }
    public int CalculateSize() => 5;
    public bool TryReplaceFrom(IReplacableAction action)
    {
        SetZombieSpawnTableIsMegaAction a = (SetZombieSpawnTableIsMegaAction)action;
        if (a.NetId.Id != NetId.Id)
            return false;

        IsMega = a.IsMega;
        return true;
    }
}

[Action(DevkitServerActionType.SetZombieSpawnTableHealth, 6, 8)]
public sealed class SetZombieSpawnTableHealthAction : IReplacableAction, INetId64Action
{
    public DevkitServerActionType Type => DevkitServerActionType.SetZombieSpawnTableHealth;
    public CSteamID Instigator { get; set; }
    public NetId64 NetId { get; set; }
    public float DeltaTime { get; set; }
    public ushort Health { get; set; }
    public void Apply()
    {
        if (!SpawnsNetIdDatabase.TryGetZombieSpawnTable(NetId, out int index))
        {
            Logger.DevkitServer.LogWarning(nameof(SetZombieSpawnTableHealthAction), $"Unknown zombie spawn table with NetId: {NetId.Format()}.");
            return;
        }

        SpawnTableUtil.SetZombieSpawnTableHealthLocal(index, Health);
    }
#if SERVER
    public bool CheckCanApply()
    {
        if (!SpawnsNetIdDatabase.TryGetZombieSpawnTable(NetId, out int _))
        {
            Logger.DevkitServer.LogWarning(nameof(SetZombieSpawnTableHealthAction), $"Unknown zombie spawn table with NetId: {NetId.Format()}.");
            return false;
        }

        return VanillaPermissions.SpawnTablesZombieEdit.Has(Instigator.m_SteamID, true);
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        Health = reader.ReadUInt16();
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write(Health);
    }
    public int CalculateSize() => 6;
    public bool TryReplaceFrom(IReplacableAction action)
    {
        SetZombieSpawnTableHealthAction a = (SetZombieSpawnTableHealthAction)action;
        if (a.NetId.Id != NetId.Id)
            return false;

        Health = a.Health;
        return true;
    }
}

[Action(DevkitServerActionType.SetZombieSpawnTableDifficultyAsset, 20, 8)]
public sealed class SetZombieSpawnTableDifficultyAssetAction : IReplacableAction, INetId64Action
{
    public DevkitServerActionType Type => DevkitServerActionType.SetZombieSpawnTableDifficultyAsset;
    public CSteamID Instigator { get; set; }
    public NetId64 NetId { get; set; }
    public float DeltaTime { get; set; }
    public AssetReference<ZombieDifficultyAsset> Asset { get; set; }
    public void Apply()
    {
        if (!SpawnsNetIdDatabase.TryGetZombieSpawnTable(NetId, out int index))
        {
            Logger.DevkitServer.LogWarning(nameof(SetZombieSpawnTableDifficultyAssetAction), $"Unknown zombie spawn table with NetId: {NetId.Format()}.");
            return;
        }

        SpawnTableUtil.SetZombieSpawnTableDifficultyAssetLocal(index, Asset);
    }
#if SERVER
    public bool CheckCanApply()
    {
        if (!SpawnsNetIdDatabase.TryGetZombieSpawnTable(NetId, out int _))
        {
            Logger.DevkitServer.LogWarning(nameof(SetZombieSpawnTableDifficultyAssetAction), $"Unknown zombie spawn table with NetId: {NetId.Format()}.");
            return false;
        }

        return VanillaPermissions.SpawnTablesZombieEdit.Has(Instigator.m_SteamID, true);
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        Asset = new AssetReference<ZombieDifficultyAsset>(reader.ReadGuid());
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write(Asset.GUID);
    }
    public int CalculateSize() => 6;
    public bool TryReplaceFrom(IReplacableAction action)
    {
        SetZombieSpawnTableDifficultyAssetAction a = (SetZombieSpawnTableDifficultyAssetAction)action;
        if (a.NetId.Id != NetId.Id)
            return false;

        Asset = a.Asset;
        return true;
    }
}

[Action(DevkitServerActionType.SetZombieSpawnTableDamage, 5, 8)]
public sealed class SetZombieSpawnTableDamageAction : IReplacableAction, INetId64Action
{
    public DevkitServerActionType Type => DevkitServerActionType.SetZombieSpawnTableDamage;
    public CSteamID Instigator { get; set; }
    public NetId64 NetId { get; set; }
    public float DeltaTime { get; set; }
    public byte Damage { get; set; }
    public void Apply()
    {
        if (!SpawnsNetIdDatabase.TryGetZombieSpawnTable(NetId, out int index))
        {
            Logger.DevkitServer.LogWarning(nameof(SetZombieSpawnTableDamageAction), $"Unknown zombie spawn table with NetId: {NetId.Format()}.");
            return;
        }

        SpawnTableUtil.SetZombieSpawnTableDamageLocal(index, Damage);
    }
#if SERVER
    public bool CheckCanApply()
    {
        if (!SpawnsNetIdDatabase.TryGetZombieSpawnTable(NetId, out int _))
        {
            Logger.DevkitServer.LogWarning(nameof(SetZombieSpawnTableDamageAction), $"Unknown zombie spawn table with NetId: {NetId.Format()}.");
            return false;
        }

        return VanillaPermissions.SpawnTablesZombieEdit.Has(Instigator.m_SteamID, true);
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        Damage = reader.ReadUInt8();
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write(Damage);
    }
    public int CalculateSize() => 6;
    public bool TryReplaceFrom(IReplacableAction action)
    {
        SetZombieSpawnTableDamageAction a = (SetZombieSpawnTableDamageAction)action;
        if (a.NetId.Id != NetId.Id)
            return false;

        Damage = a.Damage;
        return true;
    }
}