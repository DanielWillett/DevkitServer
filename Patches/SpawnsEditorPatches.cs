#if CLIENT
using DevkitServer.API;
using DevkitServer.API.Devkit.Spawns;
using DevkitServer.API.Permissions;
using DevkitServer.API.UI;
using DevkitServer.Core.Permissions;
using DevkitServer.Core.Tools;
using DevkitServer.Multiplayer.Actions;
using DevkitServer.Players;
using HarmonyLib;
using SDG.Framework.Devkit;
using SDG.Framework.Utilities;
using System.Reflection;
using System.Reflection.Emit;
using DevkitServer.API.Abstractions;
using DevkitServer.Models;
using DevkitServer.Multiplayer.Levels;

namespace DevkitServer.Patches;

[HarmonyPatch]
internal static class SpawnsEditorPatches
{
    private const string Source = "SPAWN PATCHES";
    
    private static readonly StaticGetter<ISleekButton[]>? GetAnimalTableButtons
        = Accessor.GenerateStaticGetter<EditorSpawnsAnimalsUI, ISleekButton[]>("tableButtons", throwOnError: false);
    private static readonly StaticGetter<ISleekButton[]>? GetVehicleTableButtons
        = Accessor.GenerateStaticGetter<EditorSpawnsVehiclesUI, ISleekButton[]>("tableButtons", throwOnError: false);
    private static readonly StaticGetter<ISleekButton[]>? GetItemTableButtons
        = Accessor.GenerateStaticGetter<EditorSpawnsItemsUI, ISleekButton[]>("tableButtons", throwOnError: false);
    private static readonly StaticGetter<ISleekButton[]>? GetZombieTableButtons
        = Accessor.GenerateStaticGetter<EditorSpawnsZombiesUI, ISleekButton[]>("tableButtons", throwOnError: false);
    
    private static readonly StaticGetter<ISleekButton[]>? GetAnimalTierButtons
        = Accessor.GenerateStaticGetter<EditorSpawnsAnimalsUI, ISleekButton[]>("tierButtons", throwOnError: false);
    private static readonly StaticGetter<ISleekButton[]>? GetVehicleTierButtons
        = Accessor.GenerateStaticGetter<EditorSpawnsVehiclesUI, ISleekButton[]>("tierButtons", throwOnError: false);
    private static readonly StaticGetter<ISleekButton[]>? GetItemTierButtons
        = Accessor.GenerateStaticGetter<EditorSpawnsItemsUI, ISleekButton[]>("tierButtons", throwOnError: false);
    private static readonly StaticGetter<ISleekButton[]>? GetZombieTierButtons
        = Accessor.GenerateStaticGetter<EditorSpawnsZombiesUI, ISleekButton[]>("slotButtons", throwOnError: false);

    internal static void ManualPatches()
    {
        try
        {
            HarmonyMethod transpiler = new HarmonyMethod(Accessor.GetMethod(TranspileOpenAndCloseMethods));

            Type[] types = [ typeof(EditorSpawnsItemsUI), typeof(EditorSpawnsVehiclesUI), typeof(EditorSpawnsAnimalsUI), typeof(EditorSpawnsZombiesUI), typeof(EditorLevelPlayersUI) ];
            foreach (Type type in types)
            {
                MethodInfo? openMethod = type.GetMethod("open", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static, null, CallingConventions.Any, Type.EmptyTypes, null);
                MethodInfo? closeMethod = type.GetMethod("close", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static, null, CallingConventions.Any, Type.EmptyTypes, null);
                bool patch = true;
                if (openMethod == null)
                {
                    Logger.DevkitServer.LogWarning(Source, $"Method not found: {FormattingUtil.FormatMethod(typeof(void), type, "open", arguments: Type.EmptyTypes, isStatic: true)}.");
                    patch = false;
                }
                if (closeMethod == null)
                {
                    Logger.DevkitServer.LogWarning(Source, $"Method not found: {FormattingUtil.FormatMethod(typeof(void), type, "close", arguments: Type.EmptyTypes, isStatic: true)}.");
                    patch = false;
                }

                if (!patch)
                    continue;

                PatchesMain.Patcher.Patch(openMethod, transpiler: transpiler);
                PatchesMain.Patcher.Patch(closeMethod, transpiler: transpiler);
            }

            MethodInfo? clickMethod = typeof(EditorLevelUI).GetMethod("onClickedPlayersButton", BindingFlags.NonPublic | BindingFlags.Instance);

            if (clickMethod == null)
                Logger.DevkitServer.LogWarning(Source, $"Method not found: {FormattingUtil.FormatMethod(typeof(void), typeof(EditorLevelUI), "onClickedPlayersButton", namedArguments: [ (typeof(ISleekElement), "button") ])}.");
            else
                PatchesMain.Patcher.Patch(clickMethod, transpiler: new HarmonyMethod(Accessor.GetMethod(TranspileOnClickedPlayersButton)));
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogWarning(Source, ex, "Failed to patch patches for spawn editors.");
            DevkitServerModule.Fault();
        }
    }

    [HarmonyPatch(typeof(EditorLevelPlayersUI), "onToggledAltToggle")]
    [HarmonyPostfix]
    [UsedImplicitly]
    private static void OnPlayerToggledAlt(ISleekToggle toggle, bool state)
    {
        if (DevkitServerModule.IsEditing && !VanillaPermissions.SpawnsPlayerEdit.Has())
        {
            EditorMessage.SendNoPermissionMessage(VanillaPermissions.SpawnsPlayerEdit);
            return;
        }

        bool batchListening = ClientEvents.ListeningOnSetPlayerSpawnpointsIsAlternate;
        bool singleListening = ClientEvents.ListeningOnSetPlayerSpawnpointIsAlternate || ClientEvents.ListeningOnSetPlayerSpawnpointIsAlternateRequested;

        if (!DevkitServerModule.IsEditing || !batchListening && !singleListening)
        {
            foreach (DevkitSelection selection in DevkitSelectionManager.selection)
            {
                if (selection.transform.TryGetComponent(out PlayerSpawnpointNode node) || node.Spawnpoint.isAlt == state)
                    node.Spawnpoint.SetIsAlternateLocal(state);
            }

            return;
        }

        float dt = CachedTime.DeltaTime;

        SetPlayerSpawnpointIsAlternateProperties singleProperties = default;
        List<NetId64>? toUpdate = batchListening ? ListPool<NetId64>.claim() : null;
        try
        {
            foreach (DevkitSelection selection in DevkitSelectionManager.selection)
            {
                if (!selection.gameObject.TryGetComponent(out PlayerSpawnpointNode node) || node.Spawnpoint.isAlt == state)
                    continue;

                if (singleListening)
                {
                    bool shouldAllow = true;
                    singleProperties = new SetPlayerSpawnpointIsAlternateProperties(node.NetId, state, dt);
                    ClientEvents.InvokeOnSetPlayerSpawnpointIsAlternateRequested(in singleProperties, ref shouldAllow);
                    if (!shouldAllow)
                        continue;
                }

                node.Spawnpoint.SetIsAlternateLocal(state);

                if (singleListening)
                    ClientEvents.InvokeOnSetPlayerSpawnpointIsAlternate(in singleProperties);

                if (!batchListening)
                    continue;

                toUpdate!.Add(node.NetId);
                if (toUpdate.Count >= SpawnUtil.MaxUpdateSpawnIsAlternateCount)
                    Flush(toUpdate, dt, state);
            }

            if (batchListening)
                Flush(toUpdate!, dt, state);

            static void Flush(List<NetId64> toUpdate, float dt, bool state)
            {
                SetPlayerSpawnpointsIsAlternateProperties properties = new SetPlayerSpawnpointsIsAlternateProperties(toUpdate.ToSpan(), state, dt);

                ClientEvents.InvokeOnSetPlayerSpawnpointsIsAlternate(in properties);

                toUpdate.Clear();
            }
        }
        finally
        {
            if (batchListening)
                ListPool<NetId64>.release(toUpdate);
        }
    }

    [HarmonyPatch(typeof(EditorSpawnsAnimalsUI), "onClickedTableButton")]
    [HarmonyPrefix]
    [UsedImplicitly]
    private static bool OnAnimalSelectionChanged(ISleekElement button)
    {
        if (GetAnimalTableButtons == null)
            return true;
        int index = Array.IndexOf(GetAnimalTableButtons(), (ISleekButton)button);
        if (index == -1)
            return true;
        SpawnTableUtil.SelectAnimalTable(index, true, true);
        return false;
    }

    [HarmonyPatch(typeof(EditorSpawnsVehiclesUI), "onClickedTableButton")]
    [HarmonyPrefix]
    [UsedImplicitly]
    private static bool OnVehicleSelectionChanged(ISleekElement button)
    {
        if (GetVehicleTableButtons == null)
            return true;
        int index = Array.IndexOf(GetVehicleTableButtons(), (ISleekButton)button);
        if (index == -1)
            return true;
        SpawnTableUtil.SelectVehicleTable(index, true, true);
        return false;
    }

    [HarmonyPatch(typeof(EditorSpawnsItemsUI), "onClickedTableButton")]
    [HarmonyPrefix]
    [UsedImplicitly]
    private static bool OnItemSelectionChanged(ISleekElement button)
    {
        if (GetItemTableButtons == null)
            return true;
        int index = Array.IndexOf(GetItemTableButtons(), (ISleekButton)button);
        if (index == -1)
            return true;
        SpawnTableUtil.SelectItemTable(index, true, true);
        return false;
    }

    [HarmonyPatch(typeof(EditorSpawnsZombiesUI), "onClickedTableButton")]
    [HarmonyPrefix]
    [UsedImplicitly]
    private static bool OnZombieSelectionChanged(ISleekElement button)
    {
        if (GetZombieTableButtons == null)
            return true;
        int index = Array.IndexOf(GetZombieTableButtons(), (ISleekButton)button);
        if (index == -1)
            return true;
        SpawnTableUtil.SelectZombieTable(index, true, true);
        return false;
    }

    #region Table Name
    private static bool OnTableNameUpdated(string state, int index, SpawnType spawnType)
    {
        if (!SpawnTableUtil.CheckSpawnTableSafe(spawnType, index))
        {
            SpawnTableUtil.UpdateUITable(spawnType);
            SpawnTableUtil.UpdateUISelection(spawnType);
            return true;
        }

        if (DevkitServerModule.IsEditing && !VanillaPermissions.SpawnTablesEdit(spawnType).Has())
        {
            EditorMessage.SendNoPermissionMessage(VanillaPermissions.SpawnTablesEdit(spawnType));
            return false;
        }

        if (!DevkitServerModule.IsEditing)
        {
            SpawnTableUtil.SetSpawnTableNameLocal(spawnType, index, state, false);
            return true;
        }

        if (!SpawnsNetIdDatabase.TryGetSpawnTableNetId(spawnType, index, out NetId64 netId))
        {
            Logger.DevkitServer.LogWarning(Source + "|" + nameof(OnTableNameUpdated), $"Unable to find NetId for {spawnType.ToString().ToLowerInvariant()} spawn table: {index.Format()}.");
            EditorMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource, "Error", [ "NetId Missing" ]);
            return false;
        }

        SetSpawnTableNameProperties properties = new SetSpawnTableNameProperties(netId, spawnType, state, CachedTime.DeltaTime);
        bool shouldAllow = true;
        ClientEvents.InvokeOnSetSpawnTableNameRequested(in properties, ref shouldAllow);
        if (!shouldAllow)
            return false;

        SpawnTableUtil.SetSpawnTableNameLocal(spawnType, index, state, false);
        ClientEvents.InvokeOnSetSpawnTableName(in properties);
        return true;
    }

    [HarmonyPatch(typeof(EditorSpawnsAnimalsUI), "onTypedNameField")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnAnimalNameUpdated(ISleekField field, string state)
    {
        if (!OnTableNameUpdated(state, EditorSpawns.selectedAnimal, SpawnType.Animal))
            field.Text = LevelAnimals.tables[EditorSpawns.selectedAnimal].name;

        return false;
    }

    [HarmonyPatch(typeof(EditorSpawnsVehiclesUI), "onTypedNameField")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnVehicleNameUpdated(ISleekField field, string state)
    {
        if (!OnTableNameUpdated(state, EditorSpawns.selectedVehicle, SpawnType.Vehicle))
            field.Text = LevelAnimals.tables[EditorSpawns.selectedVehicle].name;

        return false;
    }

    [HarmonyPatch(typeof(EditorSpawnsItemsUI), "onTypedTableNameField")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnItemNameUpdated(ISleekField field, string state)
    {
        if (!OnTableNameUpdated(state, EditorSpawns.selectedItem, SpawnType.Item))
            field.Text = LevelItems.tables[EditorSpawns.selectedItem].name;

        return false;
    }

    [HarmonyPatch(typeof(EditorSpawnsZombiesUI), "onTypedNameField")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnZombieNameUpdated(ISleekField field, string state)
    {
        if (!OnTableNameUpdated(state, EditorSpawns.selectedZombie, SpawnType.Zombie))
            field.Text = LevelZombies.tables[EditorSpawns.selectedZombie].name;

        return false;
    }

    #endregion

    #region Color
    private static bool OnTableColorUpdated(Color color, int index, SpawnType spawnType)
    {
        if (!SpawnTableUtil.CheckSpawnTableSafe(spawnType, index))
        {
            SpawnTableUtil.UpdateUITable(spawnType);
            SpawnTableUtil.UpdateUISelection(spawnType);
            return true;
        }

        if (DevkitServerModule.IsEditing && !VanillaPermissions.SpawnTablesEdit(spawnType).Has())
        {
            EditorMessage.SendNoPermissionMessage(VanillaPermissions.SpawnTablesEdit(spawnType));
            return false;
        }

        if (!DevkitServerModule.IsEditing)
        {
            SpawnTableUtil.SetSpawnTableColorLocal(spawnType, index, color);
            return true;
        }

        if (!SpawnsNetIdDatabase.TryGetSpawnTableNetId(spawnType, index, out NetId64 netId))
        {
            Logger.DevkitServer.LogWarning(Source + "|" + nameof(OnTableColorUpdated), $"Unable to find NetId for {spawnType.ToString().ToLowerInvariant()} spawn table: {index.Format()}.");
            EditorMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource, "Error", ["NetId Missing"]);
            return false;
        }

        SetSpawnTableColorProperties properties = new SetSpawnTableColorProperties(netId, spawnType, color, CachedTime.DeltaTime);
        bool shouldAllow = true;
        ClientEvents.InvokeOnSetSpawnTableColorRequested(in properties, ref shouldAllow);
        if (!shouldAllow)
            return false;

        SpawnTableUtil.SetSpawnTableColorLocal(spawnType, index, color);
        ClientEvents.InvokeOnSetSpawnTableColor(in properties);
        return true;
    }

    [HarmonyPatch(typeof(EditorSpawnsAnimalsUI), "onAnimalColorPicked")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnAnimalColorUpdated(SleekColorPicker picker, Color color)
    {
        if (!OnTableColorUpdated(color, EditorSpawns.selectedAnimal, SpawnType.Animal))
            picker.state = LevelAnimals.tables[EditorSpawns.selectedAnimal].color;

        return false;
    }

    [HarmonyPatch(typeof(EditorSpawnsVehiclesUI), "onVehicleColorPicked")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnVehicleColorUpdated(SleekColorPicker picker, Color color)
    {
        if (!OnTableColorUpdated(color, EditorSpawns.selectedVehicle, SpawnType.Vehicle))
            picker.state = LevelVehicles.tables[EditorSpawns.selectedVehicle].color;

        return false;
    }

    [HarmonyPatch(typeof(EditorSpawnsItemsUI), "onItemColorPicked")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnItemColorUpdated(SleekColorPicker picker, Color color)
    {
        if (!OnTableColorUpdated(color, EditorSpawns.selectedItem, SpawnType.Item))
            picker.state = LevelItems.tables[EditorSpawns.selectedItem].color;

        return false;
    }

    [HarmonyPatch(typeof(EditorSpawnsZombiesUI), "onZombieColorPicked")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnZombieColorUpdated(SleekColorPicker picker, Color color)
    {
        if (!OnTableColorUpdated(color, EditorSpawns.selectedZombie, SpawnType.Zombie))
            picker.state = LevelZombies.tables[EditorSpawns.selectedZombie].color;

        return false;
    }

    #endregion

    #region Table ID
    private static bool OnTableIdUpdated(ushort state, int index, SpawnType spawnType)
    {
        if (!SpawnTableUtil.CheckSpawnTableSafe(spawnType, index))
        {
            SpawnTableUtil.UpdateUITable(spawnType);
            SpawnTableUtil.UpdateUISelection(spawnType);
            return true;
        }

        if (DevkitServerModule.IsEditing && !VanillaPermissions.SpawnTablesEdit(spawnType).Has())
        {
            EditorMessage.SendNoPermissionMessage(VanillaPermissions.SpawnTablesEdit(spawnType));
            return false;
        }

        AssetReference<SpawnAsset> reference;

        if (Assets.find(EAssetType.SPAWN, state) is SpawnAsset spawnAsset)
        {
            reference = spawnAsset.getReferenceTo<SpawnAsset>();
        }
        else
        {
            bool alreadyNull = spawnType switch
            {
                SpawnType.Animal => LevelAnimals.tables[index].tableID == 0,
                SpawnType.Vehicle => LevelVehicles.tables[index].tableID == 0,
                SpawnType.Item => LevelItems.tables[index].tableID == 0,
                SpawnType.Zombie => LevelZombies.tables[index].lootID == 0,
                _ => false
            };

            if (alreadyNull)
                return true;

            reference = default;
        }


        if (!DevkitServerModule.IsEditing)
        {
            SpawnTableUtil.SetSpawnTableSpawnAssetLocal(spawnType, index, reference);
            return true;
        }

        if (!SpawnsNetIdDatabase.TryGetSpawnTableNetId(spawnType, index, out NetId64 netId))
        {
            Logger.DevkitServer.LogWarning(Source + "|" + nameof(OnTableColorUpdated), $"Unable to find NetId for {spawnType.ToString().ToLowerInvariant()} spawn table: {index.Format()}.");
            EditorMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource, "Error", ["NetId Missing"]);
            return false;
        }

        SetSpawnTableSpawnAssetProperties properties = new SetSpawnTableSpawnAssetProperties(netId, spawnType, reference, CachedTime.DeltaTime);
        bool shouldAllow = true;
        ClientEvents.InvokeOnSetSpawnTableSpawnAssetRequested(in properties, ref shouldAllow);
        if (!shouldAllow)
            return false;

        SpawnTableUtil.SetSpawnTableSpawnAssetLocal(spawnType, index, reference);
        ClientEvents.InvokeOnSetSpawnTableSpawnAsset(in properties);
        return true;
    }

    [HarmonyPatch(typeof(EditorSpawnsAnimalsUI), "onTableIDFieldTyped")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnAnimalColorUpdated(ISleekUInt16Field field, ushort state)
    {
        if (!OnTableIdUpdated(state, EditorSpawns.selectedAnimal, SpawnType.Animal))
            field.Value = LevelAnimals.tables[EditorSpawns.selectedAnimal].tableID;

        return false;
    }

    [HarmonyPatch(typeof(EditorSpawnsVehiclesUI), "onTableIDFieldTyped")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnVehicleColorUpdated(ISleekUInt16Field field, ushort state)
    {
        if (!OnTableIdUpdated(state, EditorSpawns.selectedVehicle, SpawnType.Vehicle))
            field.Value = LevelVehicles.tables[EditorSpawns.selectedVehicle].tableID;

        return false;
    }

    [HarmonyPatch(typeof(EditorSpawnsItemsUI), "onTableIDFieldTyped")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnItemColorUpdated(ISleekUInt16Field field, ushort state)
    {
        if (!OnTableIdUpdated(state, EditorSpawns.selectedItem, SpawnType.Item))
            field.Value = LevelItems.tables[EditorSpawns.selectedItem].tableID;

        return false;
    }

    [HarmonyPatch(typeof(EditorSpawnsZombiesUI), "onLootIDFieldTyped")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnZombieColorUpdated(ISleekUInt16Field field, ushort state)
    {
        if (!OnTableIdUpdated(state, EditorSpawns.selectedZombie, SpawnType.Zombie))
            field.Value = LevelZombies.tables[EditorSpawns.selectedZombie].lootID;

        return false;
    }

    #endregion

    #region Chance

    private static NetId64[]? _chanceNetIds;
    private static float[]? _oldChances;
    private static float[]? _newChances;
    private static bool OnChanceUpdated(float chance, int tableIndex, int tierIndex, SpawnType spawnType)
    {
        SpawnTierIdentifier identifier = new SpawnTierIdentifier(spawnType, (byte)tableIndex, (byte)tierIndex);

        Logger.DevkitServer.LogConditional(nameof(OnChanceUpdated), $"Chance updated: {identifier.Format()}: {chance.Format("F2")}.");

        if (!identifier.CheckSafe())
        {
            SpawnTableUtil.UpdateUITable(spawnType);
            SpawnTableUtil.UpdateUISelection(spawnType);
            return true;
        }

        if (DevkitServerModule.IsEditing && !VanillaPermissions.SpawnTablesEdit(spawnType).Has())
        {
            EditorMessage.SendNoPermissionMessage(VanillaPermissions.SpawnTablesEdit(spawnType));
            return false;
        }

        if (!DevkitServerModule.IsEditing)
        {
            switch (spawnType)
            {
                case SpawnType.Animal:
                    AnimalTable animalTable = LevelAnimals.tables[tableIndex];
                    animalTable.updateChance(tierIndex, chance);
                    for (int i = 0; i < animalTable.tiers.Count; ++i)
                    {
                        SpawnTableUtil.SetSpawnTableTierChanceLocal(
                            new SpawnTierIdentifier(SpawnType.Animal, (byte)tableIndex, (byte)i), animalTable.tiers[i].chance,
                            updateSlider: tierIndex != i);
                    }
                    break;
                case SpawnType.Vehicle:
                    VehicleTable vehicleTable = LevelVehicles.tables[tableIndex];
                    vehicleTable.updateChance(tierIndex, chance);
                    for (int i = 0; i < vehicleTable.tiers.Count; ++i)
                    {
                        SpawnTableUtil.SetSpawnTableTierChanceLocal(
                            new SpawnTierIdentifier(SpawnType.Vehicle, (byte)tableIndex, (byte)i), vehicleTable.tiers[i].chance,
                            updateSlider: tierIndex != i);
                    }
                    break;
                case SpawnType.Item:
                    ItemTable itemTable = LevelItems.tables[tableIndex];
                    itemTable.updateChance(tierIndex, chance);
                    for (int i = 0; i < itemTable.tiers.Count; ++i)
                    {
                        SpawnTableUtil.SetSpawnTableTierChanceLocal(
                            new SpawnTierIdentifier(SpawnType.Item, (byte)tableIndex, (byte)i), itemTable.tiers[i].chance,
                            updateSlider: tierIndex != i);
                    }
                    break;
                case SpawnType.Zombie:
                    SpawnTableUtil.SetSpawnTableTierChanceLocal(identifier, chance, updateSlider: false);
                    break;
            }
            return true;
        }

        if (!SpawnsNetIdDatabase.TryGetSpawnTableNetId(spawnType, tableIndex, out NetId64 tableNetId))
        {
            Logger.DevkitServer.LogWarning(Source + "|" + nameof(OnChanceUpdated), $"Unable to find NetId for zombie spawn table: {tableIndex.Format()}.");
            EditorMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource, "Error", ["NetId Missing"]);
            return false;
        }

        NetId64 tierNetId;
        bool shouldAllow;
        SetSpawnTableTierChancesProperties properties;
        if (identifier.Type == SpawnType.Zombie)
        {
            _chanceNetIds ??= new NetId64[16];
            _newChances ??= new float[16];

            if (!SpawnsNetIdDatabase.TryGetSpawnTierNetId(identifier, out tierNetId))
            {
                Logger.DevkitServer.LogWarning(Source + "|" + nameof(OnChanceUpdated), $"Unable to find NetId for {identifier.Format()}.");
                EditorMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource, "Error", ["NetId Missing"]);
                return false;
            }

            _chanceNetIds[0] = tableNetId;
            _newChances[0] = chance;

            properties = new SetSpawnTableTierChancesProperties(tableNetId, new ArraySegment<NetId64>(_chanceNetIds, 0, 1),
                SpawnType.Zombie, new ArraySegment<float>(_newChances, 0, 1), CachedTime.DeltaTime);

            shouldAllow = true;
            ClientEvents.InvokeOnSetSpawnTableTierChancesRequested(in properties, ref shouldAllow);
            if (!shouldAllow)
                return false;

            SpawnTableUtil.SetSpawnTableTierChanceLocal(identifier, chance, false);
            ClientEvents.InvokeOnSetSpawnTableTierChances(in properties);
            return true;
        }

        NetId64[]? netIds = _chanceNetIds;
        float[]? oldChances = _oldChances;
        float[]? newChances = _newChances;

        int c = 0;
        switch (spawnType)
        {
            case SpawnType.Animal:
                AnimalTable animalTable = LevelAnimals.tables[tableIndex];
                c = Math.Min(byte.MaxValue, animalTable.tiers.Count);
                if (netIds == null || netIds.Length < c)
                    _chanceNetIds = netIds = new NetId64[c];
                if (oldChances == null || oldChances.Length < c)
                    _oldChances = oldChances = new float[c];
                if (newChances == null || newChances.Length < c)
                    _newChances = newChances = new float[c];
                for (int i = 0; i < c; ++i)
                    oldChances[i] = animalTable.tiers[i].chance;
                animalTable.updateChance(tierIndex, chance);
                for (int i = 0; i < c; ++i)
                {
                    SpawnTierIdentifier tierIdentifier = new SpawnTierIdentifier(SpawnType.Animal, (byte)tableIndex, (byte)i);
                    if (SpawnsNetIdDatabase.TryGetSpawnTierNetId(tierIdentifier, out tierNetId))
                    {
                        netIds[i] = tierNetId;
                        newChances[i] = animalTable.tiers[i].chance;
                        continue;
                    }
                    Logger.DevkitServer.LogWarning(Source + "|" + nameof(OnChanceUpdated), $"Unable to find NetId for {tierIdentifier.Format()}.");
                    EditorMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource, "Error", ["NetId Missing"]);
                    return false;
                }
                break;
                
            case SpawnType.Vehicle:
                VehicleTable vehicleTable = LevelVehicles.tables[tableIndex];
                c = Math.Min(byte.MaxValue, vehicleTable.tiers.Count);
                if (netIds == null || netIds.Length < c)
                    _chanceNetIds = netIds = new NetId64[c];
                if (oldChances == null || oldChances.Length < c)
                    _oldChances = oldChances = new float[c];
                if (newChances == null || newChances.Length < c)
                    _newChances = newChances = new float[c];
                for (int i = 0; i < c; ++i)
                    oldChances[i] = vehicleTable.tiers[i].chance;
                vehicleTable.updateChance(tierIndex, chance);
                for (int i = 0; i < c; ++i)
                {
                    SpawnTierIdentifier tierIdentifier = new SpawnTierIdentifier(SpawnType.Vehicle, (byte)tableIndex, (byte)i);
                    if (SpawnsNetIdDatabase.TryGetSpawnTierNetId(tierIdentifier, out tierNetId))
                    {
                        netIds[i] = tierNetId;
                        newChances[i] = vehicleTable.tiers[i].chance;
                        continue;
                    }
                    Logger.DevkitServer.LogWarning(Source + "|" + nameof(OnChanceUpdated), $"Unable to find NetId for {tierIdentifier.Format()}.");
                    EditorMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource, "Error", ["NetId Missing"]);
                    return false;
                }
                break;
                
            case SpawnType.Item:
                ItemTable itemTable = LevelItems.tables[tableIndex];
                c = Math.Min(byte.MaxValue, itemTable.tiers.Count);
                if (netIds == null || netIds.Length < c)
                    _chanceNetIds = netIds = new NetId64[c];
                if (oldChances == null || oldChances.Length < c)
                    _oldChances = oldChances = new float[c];
                if (newChances == null || newChances.Length < c)
                    _newChances = newChances = new float[c];
                for (int i = 0; i < c; ++i)
                    oldChances[i] = itemTable.tiers[i].chance;
                itemTable.updateChance(tierIndex, chance);
                for (int i = 0; i < c; ++i)
                {
                    SpawnTierIdentifier tierIdentifier = new SpawnTierIdentifier(SpawnType.Item, (byte)tableIndex, (byte)i);
                    if (SpawnsNetIdDatabase.TryGetSpawnTierNetId(tierIdentifier, out tierNetId))
                    {
                        netIds[i] = tierNetId;
                        newChances[i] = itemTable.tiers[i].chance;
                        continue;
                    }
                    Logger.DevkitServer.LogWarning(Source + "|" + nameof(OnChanceUpdated), $"Unable to find NetId for {tierIdentifier.Format()}.");
                    EditorMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource, "Error", ["NetId Missing"]);
                    return false;
                }
                break;
        }

        if (c == 0)
            return false;

        properties = new SetSpawnTableTierChancesProperties(tableNetId, new ArraySegment<NetId64>(netIds!, 0, c), spawnType, new ArraySegment<float>(newChances!, 0, c), CachedTime.DeltaTime);
        shouldAllow = true;
        ClientEvents.InvokeOnSetSpawnTableTierChancesRequested(in properties, ref shouldAllow);
        if (!shouldAllow)
        {
            // rollback changes
            switch (spawnType)
            {
                case SpawnType.Animal:
                    AnimalTable animalTable = LevelAnimals.tables[tableIndex];
                    for (int i = 0; i < c; ++i)
                        animalTable.tiers[i].chance = oldChances![i];
                    break;

                case SpawnType.Vehicle:
                    VehicleTable vehicleTable = LevelVehicles.tables[tableIndex];
                    for (int i = 0; i < c; ++i)
                        vehicleTable.tiers[i].chance = oldChances![i];
                    break;

                case SpawnType.Item:
                    ItemTable itemTable = LevelItems.tables[tableIndex];
                    for (int i = 0; i < c; ++i)
                        itemTable.tiers[i].chance = oldChances![i];
                    break;
            }
            return false;
        }

        switch (spawnType)
        {
            case SpawnType.Animal:
                AnimalTable animalTable = LevelAnimals.tables[tableIndex];
                for (int i = 0; i < c; ++i)
                {
                    SpawnTableUtil.SetSpawnTableTierChanceLocal(
                        new SpawnTierIdentifier(SpawnType.Animal, (byte)tableIndex, (byte)i), animalTable.tiers[i].chance,
                        updateSlider: tierIndex != i);
                }
                break;

            case SpawnType.Vehicle:
                VehicleTable vehicleTable = LevelVehicles.tables[tableIndex];
                for (int i = 0; i < c; ++i)
                {
                    SpawnTableUtil.SetSpawnTableTierChanceLocal(
                        new SpawnTierIdentifier(SpawnType.Vehicle, (byte)tableIndex, (byte)i), vehicleTable.tiers[i].chance,
                        updateSlider: tierIndex != i);
                }
                break;

            case SpawnType.Item:
                ItemTable itemTable = LevelItems.tables[tableIndex];
                for (int i = 0; i < c; ++i)
                {
                    SpawnTableUtil.SetSpawnTableTierChanceLocal(
                        new SpawnTierIdentifier(SpawnType.Item, (byte)tableIndex, (byte)i), itemTable.tiers[i].chance,
                        updateSlider: tierIndex != i);
                }
                break;
        }
        ClientEvents.InvokeOnSetSpawnTableTierChances(in properties);
        return true;
    }

    [HarmonyPatch(typeof(EditorSpawnsAnimalsUI), "onDraggedChanceSlider")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnAnimalChanceUpdated(ISleekSlider slider, float state)
    {
        if (GetAnimalTierButtons == null)
            return false;

        int tierIndex = Array.IndexOf(GetAnimalTierButtons(), slider);

        if (!OnChanceUpdated(state, EditorSpawns.selectedAnimal, tierIndex, SpawnType.Animal))
            slider.Value = LevelAnimals.tables[EditorSpawns.selectedAnimal].tiers[tierIndex].chance;

        return false;
    }

    [HarmonyPatch(typeof(EditorSpawnsVehiclesUI), "onDraggedChanceSlider")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnVehicleChanceUpdated(ISleekSlider slider, float state)
    {
        if (GetVehicleTierButtons == null)
            return false;

        int tierIndex = Array.IndexOf(GetVehicleTierButtons(), slider);

        if (!OnChanceUpdated(state, EditorSpawns.selectedVehicle, tierIndex, SpawnType.Vehicle))
            slider.Value = LevelVehicles.tables[EditorSpawns.selectedVehicle].tiers[tierIndex].chance;

        return false;
    }

    [HarmonyPatch(typeof(EditorSpawnsItemsUI), "onDraggedChanceSlider")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnItemChanceUpdated(ISleekSlider slider, float state)
    {
        if (GetItemTierButtons == null)
            return false;

        int tierIndex = Array.IndexOf(GetItemTierButtons(), slider);

        if (!OnChanceUpdated(state, EditorSpawns.selectedItem, tierIndex, SpawnType.Item))
            slider.Value = LevelItems.tables[EditorSpawns.selectedItem].tiers[tierIndex].chance;

        return false;
    }

    [HarmonyPatch(typeof(EditorSpawnsZombiesUI), "onDraggedChanceSlider")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnZombieChanceUpdated(ISleekSlider slider, float state)
    {
        if (GetZombieTierButtons == null)
            return false;

        int tierIndex = Array.IndexOf(GetZombieTierButtons(), slider);

        if (!OnChanceUpdated(state, EditorSpawns.selectedZombie, tierIndex, SpawnType.Zombie))
            slider.Value = LevelZombies.tables[EditorSpawns.selectedZombie].slots[tierIndex].chance;

        return false;
    }
    #endregion

    private static IEnumerable<CodeInstruction> TranspileOnClickedPlayersButton(IEnumerable<CodeInstruction> instructions, MethodBase method)
    {
        List<CodeInstruction> ins = [..instructions];

        MethodInfo? open = typeof(EditorLevelPlayersUI).GetMethod("open", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if (open == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to find method: EditorLevelPlayersUI.open");
            DevkitServerModule.Fault();
        }

        bool patched = false;
        for (int i = 0; i < ins.Count; ++i)
        {
            if (open == null || !ins[i].Calls(open))
                continue;

            CodeInstruction instruction = ins[i];
            ins.RemoveAt(i);
            ins.Insert(ins[^1].opcode == OpCodes.Ret ? ins.Count - 1 : ins.Count, instruction);
            patched = true;
            break;
        }

        if (!patched)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to move call to open.");
            DevkitServerModule.Fault();
        }

        return ins;
    }
    private static IEnumerable<CodeInstruction> TranspileOpenAndCloseMethods(IEnumerable<CodeInstruction> instructions, MethodBase method)
    {
        Type? declaringType = method.DeclaringType;
        SpawnType spawnType = SpawnType.None;
        if (declaringType != null)
        {
            if (declaringType == typeof(EditorSpawnsItemsUI))
                spawnType = SpawnType.Item;
            else if (declaringType == typeof(EditorSpawnsVehiclesUI))
                spawnType = SpawnType.Vehicle;
            else if (declaringType == typeof(EditorSpawnsAnimalsUI))
                spawnType = SpawnType.Animal;
            else if (declaringType == typeof(EditorSpawnsZombiesUI))
                spawnType = SpawnType.Zombie;
            else if (declaringType == typeof(EditorLevelPlayersUI))
                spawnType = SpawnType.Player;
        }

        if (spawnType == SpawnType.None)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unknown or null spawn UI type: {declaringType.Format()}.");
        }

        MethodInfo? setIsSpawning = typeof(EditorSpawns).GetProperty(nameof(EditorSpawns.isSpawning),
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetSetMethod(true);
        if (setIsSpawning == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to find property setter: EditorSpawns.isSpawning.");
            DevkitServerModule.Fault();
        }

        List<CodeInstruction> ins = [..instructions];
        bool patched = false;
        for (int i = 0; i < ins.Count; ++i)
        {
            if (spawnType == SpawnType.None || setIsSpawning == null || !PatchUtil.MatchPattern(ins, i,
                    x => x.LoadsConstant(),
                    x => x.Calls(setIsSpawning)))
            {
                continue;
            }

            if (!ins[i].LoadsConstant(0))
            {
                CodeInstruction newInst = PatchUtil.LoadConstantI4((int)spawnType);
                ins[i].MoveBlocksAndLabels(newInst);
                ins[i] = newInst;
                ins[i + 1] = new CodeInstruction(OpCodes.Call, Accessor.GetMethod(Open)!);
                Logger.DevkitServer.LogDebug(Source, $"{method.Format()} - Patched open spawn menu.");
            }
            else
            {
                CodeInstruction newInst = new CodeInstruction(OpCodes.Call, Accessor.GetMethod(Close)!);
                ins[i].MoveBlocksAndLabels(newInst);
                ins[i] = newInst;
                ins.RemoveAt(i + 1);
                Logger.DevkitServer.LogDebug(Source, $"{method.Format()} - Patched close spawn menu.");
            }
            patched = true;
            break;
        }
        if (!patched)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to patch open/close spawn menu.");
            DevkitServerModule.Fault();
        }

        return ins;
    }
    private static void Open(SpawnType type)
    {
        if (UserInput.ActiveTool is DevkitServerSpawnsTool tool)
            tool.Type = type;
        else
            UserInput.ActiveTool = new DevkitServerSpawnsTool { Type = type };
        Logger.DevkitServer.LogDebug(Source, $"Activated {type.Format()} spawn tool.");
    }
    private static void Close()
    {
        if (UserInput.ActiveTool is not DevkitServerSpawnsTool tool)
        {
            Logger.DevkitServer.LogDebug(Source, "Spawn tool already deactivated.");
            return;
        }

        Logger.DevkitServer.LogDebug(Source, $"Deactivated {tool.Type.Format()} spawn tool.");
        UserInput.ActiveTool = null;
    }
}
#endif