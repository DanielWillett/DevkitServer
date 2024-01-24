#if CLIENT
using Cysharp.Threading.Tasks;
using DevkitServer.API;
using DevkitServer.API.Abstractions;
using DevkitServer.API.Devkit.Spawns;
using DevkitServer.API.Permissions;
using DevkitServer.API.UI;
using DevkitServer.Core.Permissions;
using DevkitServer.Core.Tools;
using DevkitServer.Models;
using DevkitServer.Multiplayer.Actions;
using DevkitServer.Multiplayer.Levels;
using DevkitServer.Players;
using HarmonyLib;
using SDG.Framework.Devkit;
using SDG.Framework.Utilities;
using System.Reflection;
using System.Reflection.Emit;

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
    
    private static readonly StaticGetter<ISleekField>? GetAnimalTableField
        = Accessor.GenerateStaticGetter<EditorSpawnsAnimalsUI, ISleekField>("tableNameField", throwOnError: false);
    private static readonly StaticGetter<ISleekField>? GetVehicleTableField
        = Accessor.GenerateStaticGetter<EditorSpawnsVehiclesUI, ISleekField>("tableNameField", throwOnError: false);
    private static readonly StaticGetter<ISleekField>? GetItemTableField
        = Accessor.GenerateStaticGetter<EditorSpawnsItemsUI, ISleekField>("tableNameField", throwOnError: false);
    private static readonly StaticGetter<ISleekField>? GetZombieTableField
        = Accessor.GenerateStaticGetter<EditorSpawnsZombiesUI, ISleekField>("tableNameField", throwOnError: false);
    
    private static readonly StaticGetter<ISleekField>? GetAnimalTierField
        = Accessor.GenerateStaticGetter<EditorSpawnsAnimalsUI, ISleekField>("tierNameField", throwOnError: false);
    private static readonly StaticGetter<ISleekField>? GetVehicleTierField
        = Accessor.GenerateStaticGetter<EditorSpawnsVehiclesUI, ISleekField>("tierNameField", throwOnError: false);
    private static readonly StaticGetter<ISleekField>? GetItemTierField
        = Accessor.GenerateStaticGetter<EditorSpawnsItemsUI, ISleekField>("tierNameField", throwOnError: false);

    private static readonly StaticGetter<ISleekUInt16Field>? GetAnimalAssetField
        = Accessor.GenerateStaticGetter<EditorSpawnsAnimalsUI, ISleekUInt16Field>("animalIDField", throwOnError: false);
    private static readonly StaticGetter<ISleekUInt16Field>? GetVehicleAssetField
        = Accessor.GenerateStaticGetter<EditorSpawnsVehiclesUI, ISleekUInt16Field>("vehicleIDField", throwOnError: false);
    private static readonly StaticGetter<ISleekUInt16Field>? GetItemAssetField
        = Accessor.GenerateStaticGetter<EditorSpawnsItemsUI, ISleekUInt16Field>("itemIDField", throwOnError: false);
    private static readonly StaticGetter<ISleekUInt16Field>? GetZombieAssetField
        = Accessor.GenerateStaticGetter<EditorSpawnsZombiesUI, ISleekUInt16Field>("itemIDField", throwOnError: false);

    private static readonly StaticGetter<ISleekScrollView>? GetAnimalTableScrollBox
        = Accessor.GenerateStaticGetter<EditorSpawnsAnimalsUI, ISleekScrollView>("tableScrollBox", throwOnError: false);
    private static readonly StaticGetter<ISleekScrollView>? GetVehicleTableScrollBox
        = Accessor.GenerateStaticGetter<EditorSpawnsVehiclesUI, ISleekScrollView>("tableScrollBox", throwOnError: false);
    private static readonly StaticGetter<ISleekScrollView>? GetItemTableScrollBox
        = Accessor.GenerateStaticGetter<EditorSpawnsItemsUI, ISleekScrollView>("tableScrollBox", throwOnError: false);
    private static readonly StaticGetter<ISleekScrollView>? GetZombieTableScrollBox
        = Accessor.GenerateStaticGetter<EditorSpawnsZombiesUI, ISleekScrollView>("tableScrollBox", throwOnError: false);

    private static readonly StaticGetter<ISleekScrollView>? GetAnimalAssetScrollBox
        = Accessor.GenerateStaticGetter<EditorSpawnsAnimalsUI, ISleekScrollView>("spawnsScrollBox", throwOnError: false);
    private static readonly StaticGetter<ISleekScrollView>? GetVehicleAssetScrollBox
        = Accessor.GenerateStaticGetter<EditorSpawnsVehiclesUI, ISleekScrollView>("spawnsScrollBox", throwOnError: false);
    private static readonly StaticGetter<ISleekScrollView>? GetItemAssetScrollBox
        = Accessor.GenerateStaticGetter<EditorSpawnsItemsUI, ISleekScrollView>("spawnsScrollBox", throwOnError: false);
    private static readonly StaticGetter<ISleekScrollView>? GetZombieAssetScrollBox
        = Accessor.GenerateStaticGetter<EditorSpawnsZombiesUI, ISleekScrollView>("spawnsScrollBox", throwOnError: false);

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
                    node.Spawnpoint.SetPlayerIsAlternateLocal(state);
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

                node.Spawnpoint.SetPlayerIsAlternateLocal(state);

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

    #region Select Table
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
    #endregion

    #region Table Name
    private static bool OnTableNameUpdated(string state, int index, SpawnType spawnType)
    {
        if (!SpawnTableUtil.CheckSpawnTableSafe(spawnType, index))
        {
            // could be typing in a new name
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
            Logger.DevkitServer.LogWarning(Source + "|" + nameof(OnTableNameUpdated), $"Unable to find NetId for {spawnType.GetLowercaseText()} spawn table: {index.Format()}.");
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

    #region Tier Name
    private static bool OnTierNameUpdated(string state, SpawnTierIdentifier identifier)
    {
        if (!identifier.CheckSafe())
        {
            // could be typing in a new name
            return true;
        }

        SpawnType spawnType = identifier.Type;

        if (DevkitServerModule.IsEditing && !VanillaPermissions.SpawnTablesEdit(spawnType).Has())
        {
            EditorMessage.SendNoPermissionMessage(VanillaPermissions.SpawnTablesEdit(spawnType));
            return false;
        }

        if (!DevkitServerModule.IsEditing)
        {
            SpawnTableUtil.SetSpawnTableTierNameLocal(identifier, state);
            return true;
        }

        if (!SpawnsNetIdDatabase.TryGetSpawnTierNetId(identifier, out NetId64 tierNetId))
        {
            Logger.DevkitServer.LogWarning(Source + "|" + nameof(OnTableNameUpdated), $"Unable to find NetId for spawn tier: {identifier.Format()}.");
            EditorMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource, "Error", [ "NetId Missing" ]);
            return false;
        }

        if (!SpawnsNetIdDatabase.TryGetSpawnTableNetId(spawnType, identifier.TableIndex, out NetId64 tableNetId))
        {
            Logger.DevkitServer.LogWarning(Source + "|" + nameof(OnTableNameUpdated), $"Unable to find NetId for {spawnType.GetLowercaseText()} spawn table: {identifier.TableIndex.Format()}.");
        }

        SetSpawnTableTierNameProperties properties = new SetSpawnTableTierNameProperties(tierNetId, tableNetId, spawnType, state, CachedTime.DeltaTime);
        bool shouldAllow = true;
        ClientEvents.InvokeOnSetSpawnTableTierNameRequested(in properties, ref shouldAllow);
        if (!shouldAllow)
            return false;

        SpawnTableUtil.SetSpawnTableTierNameLocal(identifier, state);
        ClientEvents.InvokeOnSetSpawnTableTierName(in properties);
        return true;
    }

    [HarmonyPatch(typeof(EditorSpawnsAnimalsUI), "onTypedTierNameField")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnAnimalTierNameUpdated(ISleekField field, string state)
    {
        if (!SpawnTableUtil.TryGetSelectedTier(SpawnType.Animal, out SpawnTierIdentifier? identifier) || !identifier.HasValue)
            return false;

        if (!OnTierNameUpdated(state, identifier.Value))
            field.Text = LevelAnimals.tables[EditorSpawns.selectedAnimal].name;

        return false;
    }

    [HarmonyPatch(typeof(EditorSpawnsVehiclesUI), "onTypedTierNameField")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnVehicleTierNameUpdated(ISleekField field, string state)
    {
        if (!SpawnTableUtil.TryGetSelectedTier(SpawnType.Vehicle, out SpawnTierIdentifier? identifier) || !identifier.HasValue)
            return false;

        if (!OnTierNameUpdated(state, identifier.Value))
            field.Text = LevelAnimals.tables[EditorSpawns.selectedVehicle].name;

        return false;
    }

    [HarmonyPatch(typeof(EditorSpawnsItemsUI), "onTypedTierNameField")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnItemTierNameUpdated(ISleekField field, string state)
    {
        if (!SpawnTableUtil.TryGetSelectedTier(SpawnType.Item, out SpawnTierIdentifier? identifier) || !identifier.HasValue)
            return false;

        if (!OnTierNameUpdated(state, identifier.Value))
            field.Text = LevelItems.tables[EditorSpawns.selectedItem].name;

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
            Logger.DevkitServer.LogWarning(Source + "|" + nameof(OnTableColorUpdated), $"Unable to find NetId for {spawnType.GetLowercaseText()} spawn table: {index.Format()}.");
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
            Logger.DevkitServer.LogWarning(Source + "|" + nameof(OnTableColorUpdated), $"Unable to find NetId for {spawnType.GetLowercaseText()} spawn table: {index.Format()}.");
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

        Logger.DevkitServer.LogConditional(nameof(OnChanceUpdated), $"Chance updated: {identifier.Format()} {chance.Format("F2")}.");

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
            Logger.DevkitServer.LogWarning(Source + "|" + nameof(OnChanceUpdated), $"Unable to find NetId for {spawnType.GetLowercaseText()} spawn table: {tableIndex.Format()}.");
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

            _chanceNetIds[0] = tierNetId;
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

        bool rtn = true;
        if (SpawnTableUtil.GetTierCountUnsafe(identifier.TableIndex, identifier.Type) == 1)
        {
            chance = 1f;
            rtn = false;
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
        return rtn;
    }

    [HarmonyPatch(typeof(EditorSpawnsAnimalsUI), "onDraggedChanceSlider")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnAnimalChanceUpdated(ISleekSlider slider, float state)
    {
        if (GetAnimalTierButtons == null)
            return false;

        if (slider.Parent is not ISleekButton button)
            return false;

        int tierIndex = Array.IndexOf(GetAnimalTierButtons(), button);

        if (tierIndex == -1 || !OnChanceUpdated(state, EditorSpawns.selectedAnimal, tierIndex, SpawnType.Animal))
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

        if (slider.Parent is not ISleekButton button)
            return false;

        int tierIndex = Array.IndexOf(GetVehicleTierButtons(), button);

        if (tierIndex == -1 || !OnChanceUpdated(state, EditorSpawns.selectedVehicle, tierIndex, SpawnType.Vehicle))
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

        if (slider.Parent is not ISleekButton button)
            return false;

        int tierIndex = Array.IndexOf(GetItemTierButtons(), button);

        if (tierIndex == -1 || !OnChanceUpdated(state, EditorSpawns.selectedItem, tierIndex, SpawnType.Item))
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

        if (slider.Parent is not ISleekButton button)
            return false;

        int tierIndex = Array.IndexOf(GetZombieTierButtons(), button);

        if (tierIndex == -1 || !OnChanceUpdated(state, EditorSpawns.selectedZombie, tierIndex, SpawnType.Zombie))
            slider.Value = LevelZombies.tables[EditorSpawns.selectedZombie].slots[tierIndex].chance;

        return false;
    }
    #endregion

    #region Add Table
    private static void OnAddTableClicked(ISleekElement button, SpawnType spawnType, string name)
    {
        if (!DevkitServerModule.IsEditing)
        {
            int spawnTableCount = SpawnTableUtil.GetTableCountUnsafe(spawnType);
            if (spawnTableCount >= byte.MaxValue - 1)
            {
                EditorMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource,
                    "TooMany" + spawnType + "SpawnTables", [byte.MaxValue - 1]);
                return;
            }

            int index = SpawnTableUtil.AddSpawnTableLocal(spawnType, name);

            SpawnTableUtil.SelectTable(spawnType, index, false, false);
            return;
        }

        button.SetIsClickable(false);
        UniTask.Create(async () =>
        {
            try
            {
                await SpawnTableUtil.RequestAddSpawnTable(spawnType, name);
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogWarning(nameof(OnAddTableClicked), ex, "Failed to add table.");
            }
            finally
            {
                DevkitServerUtility.QueueOnMainThread(() =>
                {
                    try
                    {
                        button.SetIsClickable(true);
                    }
                    catch
                    {
                        // ignored
                    }
                });
            }
        });
    }

    [HarmonyPatch(typeof(EditorSpawnsAnimalsUI), "onClickedAddTableButton")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnAddAnimalTableClicked(ISleekElement button)
    {
        ISleekField? field = GetAnimalTableField?.Invoke();

        string? name = field?.Text;

        if (name != null && LevelAnimals.tables.Any(x => x.name.Equals(name, StringComparison.InvariantCultureIgnoreCase)) && field != null)
        {
            field.Text = string.Empty;
            SpawnTableUtil.DeselectAnimalTable();
            return false;
        }

        if (string.IsNullOrEmpty(name))
            name = "Table " + LevelAnimals.tables.Count;

        OnAddTableClicked(button, SpawnType.Animal, name);

        return false;
    }

    [HarmonyPatch(typeof(EditorSpawnsVehiclesUI), "onClickedAddTableButton")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnAddVehicleTableClicked(ISleekElement button)
    {
        ISleekField? field = GetVehicleTableField?.Invoke();

        string? name = field?.Text;

        if (name != null && LevelVehicles.tables.Any(x => x.name.Equals(name, StringComparison.InvariantCultureIgnoreCase)) && field != null)
        {
            field.Text = string.Empty;
            SpawnTableUtil.DeselectVehicleTable();
            return false;
        }

        if (string.IsNullOrEmpty(name))
            name = "Table " + LevelVehicles.tables.Count;

        OnAddTableClicked(button, SpawnType.Vehicle, name);

        return false;
    }

    [HarmonyPatch(typeof(EditorSpawnsItemsUI), "onClickedAddTableButton")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnAddItemTableClicked(ISleekElement button)
    {
        ISleekField? field = GetItemTableField?.Invoke();

        string? name = field?.Text;

        if (name != null && LevelItems.tables.Any(x => x.name.Equals(name, StringComparison.InvariantCultureIgnoreCase)) && field != null)
        {
            field.Text = string.Empty;
            SpawnTableUtil.DeselectItemTable();
            return false;
        }

        if (string.IsNullOrEmpty(name))
            name = "Table " + LevelItems.tables.Count;

        OnAddTableClicked(button, SpawnType.Item, name);

        return false;
    }

    [HarmonyPatch(typeof(EditorSpawnsZombiesUI), "onClickedAddTableButton")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnAddZombieTableClicked(ISleekElement button)
    {
        ISleekField? field = GetZombieTableField?.Invoke();

        string? name = field?.Text;

        if (name != null && LevelZombies.tables.Any(x => x.name.Equals(name, StringComparison.InvariantCultureIgnoreCase)) && field != null)
        {
            field.Text = string.Empty;
            SpawnTableUtil.DeselectZombieTable();
            return false;
        }

        if (string.IsNullOrEmpty(name))
            name = "Table " + LevelZombies.tables.Count;

        OnAddTableClicked(button, SpawnType.Zombie, name);

        return false;
    }
    #endregion

    #region Delete Table
    private static bool OnRemoveTableClicked(SpawnType spawnType, int tableIndex)
    {
        if (!SpawnTableUtil.CheckSpawnTableSafe(spawnType, tableIndex))
        {
            SpawnTableUtil.DeselectTable(spawnType);
            return false;
        }

        if (!DevkitServerModule.IsEditing)
        {
            SpawnTableUtil.RemoveSpawnTableLocal(spawnType, tableIndex);
            return true;
        }

        if (!VanillaPermissions.SpawnTablesDelete(spawnType).Has())
        {
            EditorMessage.SendNoPermissionMessage(VanillaPermissions.SpawnTablesDelete(spawnType));
            return false;
        }

        if (!SpawnsNetIdDatabase.TryGetSpawnTableNetId(spawnType, tableIndex, out NetId64 spawnTableNetId))
        {
            Logger.DevkitServer.LogWarning(Source + "|" + nameof(OnRemoveTableClicked), $"Unable to find NetId for {spawnType.GetLowercaseText()} spawn table: {tableIndex.Format()}.");
            EditorMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource, "Error", ["NetId Missing"]);
            return false;
        }

        DeleteSpawnTableProperties properties = new DeleteSpawnTableProperties(spawnTableNetId, spawnType, CachedTime.DeltaTime);

        bool shouldAllow = true;
        ClientEvents.InvokeOnDeleteSpawnTableRequested(in properties, ref shouldAllow);
        if (!shouldAllow)
            return false;

        SpawnTableUtil.RemoveSpawnTableLocal(spawnType, tableIndex);
        ClientEvents.InvokeOnDeleteSpawnTable(in properties);
        return true;
    }

    [HarmonyPatch(typeof(EditorSpawnsAnimalsUI), "onClickedRemoveTableButton")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnRemoveAnimalTableClicked(ISleekElement button)
    {
        if (OnRemoveTableClicked(SpawnType.Animal, EditorSpawns.selectedAnimal))
            GetAnimalTableScrollBox?.Invoke()?.ScrollToBottom();

        return false;
    }

    [HarmonyPatch(typeof(EditorSpawnsVehiclesUI), "onClickedRemoveTableButton")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnRemoveVehicleTableClicked(ISleekElement button)
    {
        if (OnRemoveTableClicked(SpawnType.Vehicle, EditorSpawns.selectedVehicle))
            GetVehicleTableScrollBox?.Invoke()?.ScrollToBottom();

        return false;
    }

    [HarmonyPatch(typeof(EditorSpawnsItemsUI), "onClickedRemoveTableButton")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnRemoveItemTableClicked(ISleekElement button)
    {
        if (OnRemoveTableClicked(SpawnType.Item, EditorSpawns.selectedItem))
            GetItemTableScrollBox?.Invoke()?.ScrollToBottom();

        return false;
    }

    [HarmonyPatch(typeof(EditorSpawnsZombiesUI), "onClickedRemoveTableButton")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnRemoveZombieTableClicked(ISleekElement button)
    {
        if (OnRemoveTableClicked(SpawnType.Zombie, EditorSpawns.selectedZombie))
            GetZombieTableScrollBox?.Invoke()?.ScrollToBottom();

        return false;
    }
    #endregion

    #region Add Tier
    private static void OnAddTierClicked(ISleekElement button, SpawnType spawnType, int tableIndex, string name)
    {
        if (!DevkitServerModule.IsEditing)
        {
            int spawnTierCount = SpawnTableUtil.GetTierCountUnsafe(tableIndex, spawnType);
            if (spawnTierCount >= byte.MaxValue - 1)
            {
                EditorMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource,
                    "TooManySpawnTiers", [byte.MaxValue - 1]);
                return;
            }

            SpawnTierIdentifier? index = SpawnTableUtil.AddSpawnTableTierLocal(spawnType, tableIndex, name);

            if (index.HasValue)
                SpawnTableUtil.TrySelectTier(index.Value);
            return;
        }

        button.SetIsClickable(false);
        UniTask.Create(async () =>
        {
            try
            {
                await SpawnTableUtil.RequestAddSpawnTableTier(spawnType, tableIndex, name);
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogWarning(nameof(OnAddTierClicked), ex, "Failed to add tier.");
            }
            finally
            {
                DevkitServerUtility.QueueOnMainThread(() =>
                {
                    try
                    {
                        button.SetIsClickable(true);
                    }
                    catch
                    {
                        // ignored
                    }
                });
            }
        });
    }

    [HarmonyPatch(typeof(EditorSpawnsAnimalsUI), "onClickedAddTierButton")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnAddAnimalTierClicked(ISleekElement button)
    {
        if (EditorSpawns.selectedAnimal >= LevelAnimals.tables.Count)
            return false;

        ISleekField? field = GetAnimalTierField?.Invoke();

        string? name = field?.Text;

        AnimalTable animalTable = LevelAnimals.tables[EditorSpawns.selectedAnimal];
        if (name != null && animalTable.tiers.Any(x => x.name.Equals(name, StringComparison.InvariantCultureIgnoreCase)) && field != null)
        {
            field.Text = string.Empty;
            SpawnTableUtil.DeselectTier(SpawnType.Animal);
            return false;
        }

        if (string.IsNullOrEmpty(name))
            name = "Tier " + animalTable.tiers.Count;

        OnAddTierClicked(button, SpawnType.Animal, EditorSpawns.selectedAnimal, name);

        return false;
    }

    [HarmonyPatch(typeof(EditorSpawnsVehiclesUI), "onClickedAddTierButton")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnAddVehicleTierClicked(ISleekElement button)
    {
        if (EditorSpawns.selectedVehicle >= LevelVehicles.tables.Count)
            return false;

        ISleekField? field = GetVehicleTierField?.Invoke();

        string? name = field?.Text;

        VehicleTable vehicleTable = LevelVehicles.tables[EditorSpawns.selectedVehicle];
        if (name != null && vehicleTable.tiers.Any(x => x.name.Equals(name, StringComparison.InvariantCultureIgnoreCase)) && field != null)
        {
            field.Text = string.Empty;
            SpawnTableUtil.DeselectTier(SpawnType.Vehicle);
            return false;
        }

        if (string.IsNullOrEmpty(name))
            name = "Tier " + vehicleTable.tiers.Count;

        OnAddTierClicked(button, SpawnType.Vehicle, EditorSpawns.selectedVehicle, name);

        return false;
    }

    [HarmonyPatch(typeof(EditorSpawnsItemsUI), "onClickedAddTierButton")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnAddItemTierClicked(ISleekElement button)
    {
        if (EditorSpawns.selectedItem >= LevelItems.tables.Count)
            return false;

        ISleekField? field = GetItemTierField?.Invoke();

        string? name = field?.Text;

        ItemTable itemTable = LevelItems.tables[EditorSpawns.selectedItem];
        if (name != null && itemTable.tiers.Any(x => x.name.Equals(name, StringComparison.InvariantCultureIgnoreCase)) && field != null)
        {
            field.Text = string.Empty;
            SpawnTableUtil.DeselectTier(SpawnType.Item);
            return false;
        }

        if (string.IsNullOrEmpty(name))
            name = "Tier " + itemTable.tiers.Count;

        OnAddTierClicked(button, SpawnType.Item, EditorSpawns.selectedItem, name);

        return false;
    }
    #endregion

    #region Delete Tier
    private static void OnRemoveTierClicked(SpawnTierIdentifier identifier)
    {
        if (!identifier.CheckSafe())
        {
            SpawnTableUtil.DeselectTier(identifier.Type);
            return;
        }

        if (!DevkitServerModule.IsEditing)
        {
            SpawnTableUtil.RemoveSpawnTableTierLocal(identifier);
            return;
        }

        if (!VanillaPermissions.SpawnTablesEdit(identifier.Type).Has())
        {
            EditorMessage.SendNoPermissionMessage(VanillaPermissions.SpawnTablesEdit(identifier.Type));
            return;
        }

        if (!SpawnsNetIdDatabase.TryGetSpawnTierNetId(identifier, out NetId64 tierNetId))
        {
            Logger.DevkitServer.LogWarning(Source + "|" + nameof(OnRemoveTableClicked), $"Unable to find NetId for spawn table tier: {identifier.Format()}.");
            EditorMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource, "Error", ["NetId Missing"]);
            return;
        }

        if (!SpawnsNetIdDatabase.TryGetSpawnTableNetId(identifier.Type, identifier.TableIndex, out NetId64 tableNetId))
        {
            Logger.DevkitServer.LogWarning(Source + "|" + nameof(OnRemoveTableClicked), $"Unable to find NetId for spawn table tier: {identifier.Format()}.");
        }

        DeleteSpawnTableTierProperties properties = new DeleteSpawnTableTierProperties(tableNetId, tierNetId, identifier.Type, CachedTime.DeltaTime);

        bool shouldAllow = true;
        ClientEvents.InvokeOnDeleteSpawnTableTierRequested(in properties, ref shouldAllow);
        if (!shouldAllow)
            return;

        SpawnTableUtil.RemoveSpawnTableTierLocal(identifier);
        ClientEvents.InvokeOnDeleteSpawnTableTier(in properties);
    }

    [HarmonyPatch(typeof(EditorSpawnsAnimalsUI), "onClickedRemoveTierButton")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnRemoveAnimalTierClicked(ISleekElement button)
    {
        if (SpawnTableUtil.TryGetSelectedTier(SpawnType.Animal, out SpawnTierIdentifier? identifier) && identifier.HasValue)
            OnRemoveTierClicked(identifier.Value);

        return false;
    }

    [HarmonyPatch(typeof(EditorSpawnsVehiclesUI), "onClickedRemoveTierButton")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnRemoveVehicleTierClicked(ISleekElement button)
    {
        if (SpawnTableUtil.TryGetSelectedTier(SpawnType.Vehicle, out SpawnTierIdentifier? identifier) && identifier.HasValue)
            OnRemoveTierClicked(identifier.Value);

        return false;
    }

    [HarmonyPatch(typeof(EditorSpawnsItemsUI), "onClickedRemoveTierButton")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnRemoveItemTierClicked(ISleekElement button)
    {
        if (SpawnTableUtil.TryGetSelectedTier(SpawnType.Item, out SpawnTierIdentifier? identifier) && identifier.HasValue)
            OnRemoveTierClicked(identifier.Value);

        return false;
    }
    #endregion

    #region Add Asset
    private static void OnAddAssetClicked(ISleekElement button, SpawnTierIdentifier parentTier, ushort legacyId)
    {
        if (!DevkitServerModule.IsEditing)
        {
            int spawnAssetCount = SpawnTableUtil.GetAssetCountUnsafe(parentTier.TableIndex, parentTier.TierIndex, parentTier.Type);
            if (spawnAssetCount >= byte.MaxValue - 1)
            {
                EditorMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource, "TooManySpawnAssets", [byte.MaxValue - 1]);
                return;
            }

            SpawnAssetIdentifier? index = SpawnTableUtil.AddSpawnTableAssetLocal(parentTier, legacyId);

            if (index.HasValue)
                SpawnTableUtil.TrySelectTierAsset(index.Value);
            return;
        }

        button.SetIsClickable(false);
        UniTask.Create(async () =>
        {
            try
            {
                await SpawnTableUtil.RequestAddSpawnTableAsset(parentTier, legacyId);
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogWarning(nameof(OnAddTierClicked), ex, "Failed to add asset.");
            }
            finally
            {
                DevkitServerUtility.QueueOnMainThread(() =>
                {
                    try
                    {
                        button.SetIsClickable(true);
                    }
                    catch
                    {
                        // ignored
                    }
                });
            }
        });
    }

    [HarmonyPatch(typeof(EditorSpawnsAnimalsUI), "onClickedAddAnimalButton")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnAddAnimalAssetClicked(ISleekElement button)
    {
        if (!SpawnTableUtil.TryGetSelectedTier(SpawnType.Animal, out SpawnTierIdentifier? identifier) || !identifier.HasValue)
            return false;

        ISleekUInt16Field? field = GetAnimalAssetField?.Invoke();

        ushort legacyId = field?.Value ?? default;

        if (legacyId != default && Assets.find(EAssetType.ANIMAL, legacyId) is not AnimalAsset)
        {
            EditorMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource, "SpawnAssetNotFound", [ nameof(EAssetType.ANIMAL), legacyId ]);
            if (field != null)
                field.Value = default;
            return false;
        }

        OnAddAssetClicked(button, identifier.Value, legacyId);

        return false;
    }

    [HarmonyPatch(typeof(EditorSpawnsVehiclesUI), "onClickedAddVehicleButton")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnAddVehicleAssetClicked(ISleekElement button)
    {
        if (!SpawnTableUtil.TryGetSelectedTier(SpawnType.Vehicle, out SpawnTierIdentifier? identifier) || !identifier.HasValue)
            return false;

        ISleekUInt16Field? field = GetVehicleAssetField?.Invoke();

        ushort legacyId = field?.Value ?? default;

        if (legacyId != default && Assets.find(EAssetType.VEHICLE, legacyId) is not VehicleAsset)
        {
            EditorMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource, "SpawnAssetNotFound", [ nameof(EAssetType.VEHICLE), legacyId ]);
            if (field != null)
                field.Value = default;
            return false;
        }

        OnAddAssetClicked(button, identifier.Value, legacyId);

        return false;
    }

    [HarmonyPatch(typeof(EditorSpawnsItemsUI), "onClickedAddItemButton")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnAddItemAssetClicked(ISleekElement button)
    {
        if (!SpawnTableUtil.TryGetSelectedTier(SpawnType.Item, out SpawnTierIdentifier? identifier) || !identifier.HasValue)
            return false;

        ISleekUInt16Field? field = GetItemAssetField?.Invoke();

        ushort legacyId = field?.Value ?? default;

        if (legacyId != default && Assets.find(EAssetType.ITEM, legacyId) is not ItemAsset)
        {
            EditorMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource, "SpawnAssetNotFound", [ nameof(EAssetType.ITEM), legacyId ]);
            if (field != null)
                field.Value = default;
            return false;
        }

        OnAddAssetClicked(button, identifier.Value, legacyId);

        return false;
    }

    [HarmonyPatch(typeof(EditorSpawnsZombiesUI), "onClickedAddItemButton")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnAddZombieAssetClicked(ISleekElement button)
    {
        if (!SpawnTableUtil.TryGetSelectedTier(SpawnType.Zombie, out SpawnTierIdentifier? identifier) || !identifier.HasValue)
            return false;

        ISleekUInt16Field? field = GetZombieAssetField?.Invoke();

        ushort legacyId = field?.Value ?? default;

        if (legacyId != default && Assets.find(EAssetType.ITEM, legacyId) is not ItemAsset)
        {
            EditorMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource, "SpawnAssetNotFound", [ nameof(EAssetType.ITEM), legacyId ]);
            if (field != null)
                field.Value = default;
            return false;
        }

        OnAddAssetClicked(button, identifier.Value, legacyId);

        return false;
    }
    #endregion

    #region Delete Asset
    private static bool OnRemoveAssetClicked(SpawnAssetIdentifier identifier)
    {
        if (!identifier.CheckSafe())
        {
            SpawnTableUtil.DeselectTierAsset(identifier.Type);
            return false;
        }

        if (!DevkitServerModule.IsEditing)
        {
            SpawnTableUtil.RemoveSpawnTableTierAssetLocal(identifier);
            return true;
        }

        if (!VanillaPermissions.SpawnTablesEdit(identifier.Type).Has())
        {
            EditorMessage.SendNoPermissionMessage(VanillaPermissions.SpawnTablesEdit(identifier.Type));
            return false;
        }

        if (!SpawnsNetIdDatabase.TryGetSpawnAssetNetId(identifier, out NetId64 assetNetId))
        {
            Logger.DevkitServer.LogWarning(Source + "|" + nameof(OnRemoveAssetClicked), $"Unable to find NetId for spawn table tier asset: {identifier.Format()}.");
            EditorMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource, "Error", ["NetId Missing"]);
            return false;
        }

        if (!SpawnsNetIdDatabase.TryGetSpawnTierNetId(identifier.GetTier(), out NetId64 tierNetId))
        {
            Logger.DevkitServer.LogWarning(Source + "|" + nameof(OnRemoveAssetClicked), $"Unable to find NetId for spawn table tier: {identifier.GetTier().Format()}.");
        }

        if (!SpawnsNetIdDatabase.TryGetSpawnTableNetId(identifier.Type, identifier.TableIndex, out NetId64 tableNetId))
        {
            Logger.DevkitServer.LogWarning(Source + "|" + nameof(OnRemoveAssetClicked), $"Unable to find NetId for {identifier.Type.GetLowercaseText()} spawn table: {identifier.TableIndex.Format()}.");
        }

        DeleteSpawnTableTierAssetProperties properties = new DeleteSpawnTableTierAssetProperties(tableNetId, tierNetId, assetNetId, identifier.Type, CachedTime.DeltaTime);

        bool shouldAllow = true;
        ClientEvents.InvokeOnDeleteSpawnTableTierAssetRequested(in properties, ref shouldAllow);
        if (!shouldAllow)
            return false;

        SpawnTableUtil.RemoveSpawnTableTierAssetLocal(identifier);
        ClientEvents.InvokeOnDeleteSpawnTableTierAsset(in properties);
        return true;
    }

    [HarmonyPatch(typeof(EditorSpawnsAnimalsUI), "onClickedRemoveAnimalButton")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnRemoveAnimalAssetClicked(ISleekElement button)
    {
        if (SpawnTableUtil.TryGetSelectedTierAsset(SpawnType.Animal, out SpawnAssetIdentifier? identifier)
            && identifier.HasValue
            && OnRemoveAssetClicked(identifier.Value))
        {
            GetAnimalAssetScrollBox?.Invoke()?.ScrollToBottom();
        }

        return false;
    }

    [HarmonyPatch(typeof(EditorSpawnsVehiclesUI), "onClickedRemoveVehicleButton")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnRemoveVehicleAssetClicked(ISleekElement button)
    {
        if (SpawnTableUtil.TryGetSelectedTierAsset(SpawnType.Vehicle, out SpawnAssetIdentifier? identifier)
            && identifier.HasValue
            && OnRemoveAssetClicked(identifier.Value))
        {
            GetVehicleAssetScrollBox?.Invoke()?.ScrollToBottom();
        }

        return false;
    }

    [HarmonyPatch(typeof(EditorSpawnsItemsUI), "onClickedRemoveItemButton")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnRemoveItemAssetClicked(ISleekElement button)
    {
        if (SpawnTableUtil.TryGetSelectedTierAsset(SpawnType.Item, out SpawnAssetIdentifier? identifier)
            && identifier.HasValue
            && OnRemoveAssetClicked(identifier.Value))
        {
            GetItemAssetScrollBox?.Invoke()?.ScrollToBottom();
        }

        return false;
    }

    [HarmonyPatch(typeof(EditorSpawnsZombiesUI), "onClickedRemoveItemButton")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnRemoveZombieAssetClicked(ISleekElement button)
    {
        if (SpawnTableUtil.TryGetSelectedTierAsset(SpawnType.Zombie, out SpawnAssetIdentifier? identifier)
            && identifier.HasValue
            && OnRemoveAssetClicked(identifier.Value))
        {
            GetZombieAssetScrollBox?.Invoke()?.ScrollToBottom();
        }

        return false;
    }
    #endregion

    #region ZombieTable Properties

    [HarmonyPatch(typeof(EditorSpawnsZombiesUI), "onToggledMegaToggle")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnZombieToggledMegaToggle(ISleekToggle toggle, bool state)
    {
        int index = EditorSpawns.selectedZombie;

        if (!SpawnTableUtil.CheckSpawnTableSafe(SpawnType.Zombie, index))
        {
            EditorSpawnsZombiesUI.updateTables();
            EditorSpawnsZombiesUI.updateSelection();
            return false;
        }

        if (DevkitServerModule.IsEditing && !VanillaPermissions.SpawnTablesEdit(SpawnType.Zombie).Has())
        {
            EditorMessage.SendNoPermissionMessage(VanillaPermissions.SpawnTablesEdit(SpawnType.Zombie));
            toggle.Value = LevelZombies.tables[index].isMega;
            return false;
        }

        if (state == LevelZombies.tables[index].isMega)
            return false;

        if (!DevkitServerModule.IsEditing)
        {
            SpawnTableUtil.SetZombieSpawnTableIsMegaLocal(index, state);
            return false;
        }

        if (!SpawnsNetIdDatabase.TryGetSpawnTableNetId(SpawnType.Zombie, index, out NetId64 netId))
        {
            Logger.DevkitServer.LogWarning(Source + "|" + nameof(OnZombieToggledMegaToggle), $"Unable to find NetId for zombie spawn table: {index.Format()}.");
            EditorMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource, "Error", ["NetId Missing"]);
            toggle.Value = LevelZombies.tables[index].isMega;
            return false;
        }

        SetZombieSpawnTableIsMegaProperties properties = new SetZombieSpawnTableIsMegaProperties(netId, state, CachedTime.DeltaTime);
        bool shouldAllow = true;
        ClientEvents.InvokeOnSetZombieSpawnTableIsMegaRequested(in properties, ref shouldAllow);
        if (!shouldAllow)
        {
            toggle.Value = LevelZombies.tables[index].isMega;
            return false;
        }

        SpawnTableUtil.SetZombieSpawnTableIsMegaLocal(index, state);
        ClientEvents.InvokeOnSetZombieSpawnTableIsMega(in properties);
        return false;
    }

    [HarmonyPatch(typeof(EditorSpawnsZombiesUI), "onHealthFieldTyped")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnZombieTypedHealth(ISleekUInt16Field field, ushort state)
    {
        int index = EditorSpawns.selectedZombie;

        if (!SpawnTableUtil.CheckSpawnTableSafe(SpawnType.Zombie, index))
        {
            EditorSpawnsZombiesUI.updateTables();
            EditorSpawnsZombiesUI.updateSelection();
            return false;
        }

        if (DevkitServerModule.IsEditing && !VanillaPermissions.SpawnTablesEdit(SpawnType.Zombie).Has())
        {
            EditorMessage.SendNoPermissionMessage(VanillaPermissions.SpawnTablesEdit(SpawnType.Zombie));
            field.Value = LevelZombies.tables[index].health;
            return false;
        }

        if (state == LevelZombies.tables[index].health)
            return false;

        if (!DevkitServerModule.IsEditing)
        {
            SpawnTableUtil.SetZombieSpawnTableHealthLocal(index, state);
            return false;
        }

        if (!SpawnsNetIdDatabase.TryGetSpawnTableNetId(SpawnType.Zombie, index, out NetId64 netId))
        {
            Logger.DevkitServer.LogWarning(Source + "|" + nameof(OnZombieTypedHealth), $"Unable to find NetId for zombie spawn table: {index.Format()}.");
            EditorMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource, "Error", ["NetId Missing"]);
            field.Value = LevelZombies.tables[index].health;
            return false;
        }

        SetZombieSpawnTableHealthProperties properties = new SetZombieSpawnTableHealthProperties(netId, state, CachedTime.DeltaTime);
        bool shouldAllow = true;
        ClientEvents.InvokeOnSetZombieSpawnTableHealthRequested(in properties, ref shouldAllow);
        if (!shouldAllow)
        {
            field.Value = LevelZombies.tables[index].health;
            return false;
        }

        SpawnTableUtil.SetZombieSpawnTableHealthLocal(index, state);
        ClientEvents.InvokeOnSetZombieSpawnTableHealth(in properties);
        return false;
    }

    [HarmonyPatch(typeof(EditorSpawnsZombiesUI), "onDamageFieldTyped")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnZombieTypedDamage(ISleekUInt8Field field, byte state)
    {
        int index = EditorSpawns.selectedZombie;

        if (!SpawnTableUtil.CheckSpawnTableSafe(SpawnType.Zombie, index))
        {
            EditorSpawnsZombiesUI.updateTables();
            EditorSpawnsZombiesUI.updateSelection();
            return false;
        }

        if (DevkitServerModule.IsEditing && !VanillaPermissions.SpawnTablesEdit(SpawnType.Zombie).Has())
        {
            EditorMessage.SendNoPermissionMessage(VanillaPermissions.SpawnTablesEdit(SpawnType.Zombie));
            field.Value = LevelZombies.tables[index].damage;
            return false;
        }

        if (state == LevelZombies.tables[index].damage)
            return false;

        if (!DevkitServerModule.IsEditing)
        {
            SpawnTableUtil.SetZombieSpawnTableDamageLocal(index, state);
            return false;
        }

        if (!SpawnsNetIdDatabase.TryGetSpawnTableNetId(SpawnType.Zombie, index, out NetId64 netId))
        {
            Logger.DevkitServer.LogWarning(Source + "|" + nameof(OnZombieTypedDamage), $"Unable to find NetId for zombie spawn table: {index.Format()}.");
            EditorMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource, "Error", ["NetId Missing"]);
            field.Value = LevelZombies.tables[index].damage;
            return false;
        }

        SetZombieSpawnTableDamageProperties properties = new SetZombieSpawnTableDamageProperties(netId, state, CachedTime.DeltaTime);
        bool shouldAllow = true;
        ClientEvents.InvokeOnSetZombieSpawnTableDamageRequested(in properties, ref shouldAllow);
        if (!shouldAllow)
        {
            field.Value = LevelZombies.tables[index].damage;
            return false;
        }

        SpawnTableUtil.SetZombieSpawnTableDamageLocal(index, state);
        ClientEvents.InvokeOnSetZombieSpawnTableDamage(in properties);
        return false;
    }

    [HarmonyPatch(typeof(EditorSpawnsZombiesUI), "onLootIndexFieldTyped")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnZombieTypedLootIndex(ISleekUInt8Field field, byte state)
    {
        int index = EditorSpawns.selectedZombie;

        if (!SpawnTableUtil.CheckSpawnTableSafe(SpawnType.Zombie, index))
        {
            EditorSpawnsZombiesUI.updateTables();
            EditorSpawnsZombiesUI.updateSelection();
            return false;
        }

        if (DevkitServerModule.IsEditing && !VanillaPermissions.SpawnTablesEdit(SpawnType.Zombie).Has())
        {
            EditorMessage.SendNoPermissionMessage(VanillaPermissions.SpawnTablesEdit(SpawnType.Zombie));
            field.Value = LevelZombies.tables[index].lootIndex;
            return false;
        }

        if (state == LevelZombies.tables[index].lootIndex)
            return false;

        if (!DevkitServerModule.IsEditing)
        {
            SpawnTableUtil.SetZombieSpawnTableLootIndexLocal(index, state);
            return false;
        }

        if (!SpawnsNetIdDatabase.TryGetSpawnTableNetId(SpawnType.Zombie, index, out NetId64 netId))
        {
            Logger.DevkitServer.LogWarning(Source + "|" + nameof(OnZombieTypedLootIndex), $"Unable to find NetId for zombie spawn table: {index.Format()}.");
            EditorMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource, "Error", ["NetId Missing"]);
            field.Value = LevelZombies.tables[index].lootIndex;
            return false;
        }

        NetId64 other = default;

        if (state != 0)
            SpawnsNetIdDatabase.TryGetSpawnTableNetId(SpawnType.Item, state, out other);

        SetZombieSpawnTableLootIndexProperties properties = new SetZombieSpawnTableLootIndexProperties(netId, other, CachedTime.DeltaTime);
        bool shouldAllow = true;
        ClientEvents.InvokeOnSetZombieSpawnTableLootIndexRequested(in properties, ref shouldAllow);
        if (!shouldAllow)
        {
            field.Value = LevelZombies.tables[index].lootIndex;
            return false;
        }

        SpawnTableUtil.SetZombieSpawnTableLootIndexLocal(index, state);
        ClientEvents.InvokeOnSetZombieSpawnTableLootIndex(in properties);
        return false;
    }

    [HarmonyPatch(typeof(EditorSpawnsZombiesUI), "onXPFieldTyped")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnZombieTypedXP(ISleekUInt32Field field, uint state)
    {
        int index = EditorSpawns.selectedZombie;

        if (!SpawnTableUtil.CheckSpawnTableSafe(SpawnType.Zombie, index))
        {
            EditorSpawnsZombiesUI.updateTables();
            EditorSpawnsZombiesUI.updateSelection();
            return false;
        }

        if (DevkitServerModule.IsEditing && !VanillaPermissions.SpawnTablesEdit(SpawnType.Zombie).Has())
        {
            EditorMessage.SendNoPermissionMessage(VanillaPermissions.SpawnTablesEdit(SpawnType.Zombie));
            field.Value = LevelZombies.tables[index].xp;
            return false;
        }

        if (state == LevelZombies.tables[index].xp)
            return false;

        if (!DevkitServerModule.IsEditing)
        {
            SpawnTableUtil.SetZombieSpawnTableXPLocal(index, state);
            return false;
        }

        if (!SpawnsNetIdDatabase.TryGetSpawnTableNetId(SpawnType.Zombie, index, out NetId64 netId))
        {
            Logger.DevkitServer.LogWarning(Source + "|" + nameof(OnZombieTypedXP), $"Unable to find NetId for zombie spawn table: {index.Format()}.");
            EditorMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource, "Error", ["NetId Missing"]);
            field.Value = LevelZombies.tables[index].xp;
            return false;
        }

        SetZombieSpawnTableXPProperties properties = new SetZombieSpawnTableXPProperties(netId, state, CachedTime.DeltaTime);
        bool shouldAllow = true;
        ClientEvents.InvokeOnSetZombieSpawnTableXPRequested(in properties, ref shouldAllow);
        if (!shouldAllow)
        {
            field.Value = LevelZombies.tables[index].xp;
            return false;
        }

        SpawnTableUtil.SetZombieSpawnTableXPLocal(index, state);
        ClientEvents.InvokeOnSetZombieSpawnTableXP(in properties);
        return false;
    }

    [HarmonyPatch(typeof(EditorSpawnsZombiesUI), "onRegenFieldTyped")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnZombieTypedRegen(ISleekFloat32Field field, float state)
    {
        int index = EditorSpawns.selectedZombie;

        if (!SpawnTableUtil.CheckSpawnTableSafe(SpawnType.Zombie, index))
        {
            EditorSpawnsZombiesUI.updateTables();
            EditorSpawnsZombiesUI.updateSelection();
            return false;
        }

        if (DevkitServerModule.IsEditing && !VanillaPermissions.SpawnTablesEdit(SpawnType.Zombie).Has())
        {
            EditorMessage.SendNoPermissionMessage(VanillaPermissions.SpawnTablesEdit(SpawnType.Zombie));
            field.Value = LevelZombies.tables[index].regen;
            return false;
        }

        if (state == LevelZombies.tables[index].regen)
            return false;

        if (!DevkitServerModule.IsEditing)
        {
            SpawnTableUtil.SetZombieSpawnTableRegenLocal(index, state);
            return false;
        }

        if (!SpawnsNetIdDatabase.TryGetSpawnTableNetId(SpawnType.Zombie, index, out NetId64 netId))
        {
            Logger.DevkitServer.LogWarning(Source + "|" + nameof(OnZombieTypedRegen), $"Unable to find NetId for zombie spawn table: {index.Format()}.");
            EditorMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource, "Error", ["NetId Missing"]);
            field.Value = LevelZombies.tables[index].regen;
            return false;
        }

        SetZombieSpawnTableRegenProperties properties = new SetZombieSpawnTableRegenProperties(netId, state, CachedTime.DeltaTime);
        bool shouldAllow = true;
        ClientEvents.InvokeOnSetZombieSpawnTableRegenRequested(in properties, ref shouldAllow);
        if (!shouldAllow)
        {
            field.Value = LevelZombies.tables[index].regen;
            return false;
        }

        SpawnTableUtil.SetZombieSpawnTableRegenLocal(index, state);
        ClientEvents.InvokeOnSetZombieSpawnTableRegen(in properties);
        return false;
    }

    [HarmonyPatch(typeof(EditorSpawnsZombiesUI), "onDifficultyGUIDFieldTyped")]
    [HarmonyPrefix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static bool OnZombieTypedDifficultyAsset(ISleekField field, string state)
    {
        int index = EditorSpawns.selectedZombie;

        if (!SpawnTableUtil.CheckSpawnTableSafe(SpawnType.Zombie, index))
        {
            EditorSpawnsZombiesUI.updateTables();
            EditorSpawnsZombiesUI.updateSelection();
            return false;
        }

        if (DevkitServerModule.IsEditing && !VanillaPermissions.SpawnTablesEdit(SpawnType.Zombie).Has())
        {
            EditorMessage.SendNoPermissionMessage(VanillaPermissions.SpawnTablesEdit(SpawnType.Zombie));
            field.Text = LevelZombies.tables[index].difficultyGUID;
            return false;
        }

        Guid.TryParse(state, out Guid guid);
        if (guid == LevelZombies.tables[index].difficulty.GUID)
            return false;

        AssetReference<ZombieDifficultyAsset> difficultyAsset = new AssetReference<ZombieDifficultyAsset>(guid);
        if (!DevkitServerModule.IsEditing)
        {
            SpawnTableUtil.SetZombieSpawnTableDifficultyAssetLocal(index, difficultyAsset);
            return false;
        }

        if (!SpawnsNetIdDatabase.TryGetSpawnTableNetId(SpawnType.Zombie, index, out NetId64 netId))
        {
            Logger.DevkitServer.LogWarning(Source + "|" + nameof(OnZombieTypedRegen), $"Unable to find NetId for zombie spawn table: {index.Format()}.");
            EditorMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource, "Error", ["NetId Missing"]);
            field.Text = LevelZombies.tables[index].difficultyGUID;
            return false;
        }

        SetZombieSpawnTableDifficultyAssetProperties properties = new SetZombieSpawnTableDifficultyAssetProperties(netId, difficultyAsset, CachedTime.DeltaTime);
        bool shouldAllow = true;
        ClientEvents.InvokeOnSetZombieSpawnTableDifficultyAssetRequested(in properties, ref shouldAllow);
        if (!shouldAllow)
        {
            field.Text = LevelZombies.tables[index].difficultyGUID;
            return false;
        }

        SpawnTableUtil.SetZombieSpawnTableDifficultyAssetLocal(index, difficultyAsset);
        ClientEvents.InvokeOnSetZombieSpawnTableDifficultyAsset(in properties);
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