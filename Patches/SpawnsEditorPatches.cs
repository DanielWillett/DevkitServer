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

namespace DevkitServer.Patches;

[HarmonyPatch]
internal static class SpawnsEditorPatches
{
    private const string Source = "SPAWN PATCHES";
    
    private static readonly StaticGetter<ISleekButton[]>? GetAnimalTableButtons = Accessor.GenerateStaticGetter<EditorSpawnsAnimalsUI, ISleekButton[]>("tableButtons", throwOnError: false);
    private static readonly StaticGetter<ISleekButton[]>? GetVehicleTableButtons = Accessor.GenerateStaticGetter<EditorSpawnsVehiclesUI, ISleekButton[]>("tableButtons", throwOnError: false);
    private static readonly StaticGetter<ISleekButton[]>? GetItemTableButtons = Accessor.GenerateStaticGetter<EditorSpawnsItemsUI, ISleekButton[]>("tableButtons", throwOnError: false);
    private static readonly StaticGetter<ISleekButton[]>? GetZombieTableButtons = Accessor.GenerateStaticGetter<EditorSpawnsZombiesUI, ISleekButton[]>("tableButtons", throwOnError: false);

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

    [HarmonyPatch(typeof(EditorSpawnsAnimalsUI), "onTypedNameField")]
    [HarmonyPostfix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static void OnAnimalNameUpdated(ISleekField field, string state)
    {
        if (EditorSpawns.selectedAnimal >= LevelAnimals.tables.Count)
            return;

        LevelAnimals.tables[EditorSpawns.selectedAnimal].SetSpawnTableNameLocal(state);
    }

    [HarmonyPatch(typeof(EditorSpawnsVehiclesUI), "onTypedNameField")]
    [HarmonyPostfix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static void OnVehicleNameUpdated(ISleekField field, string state)
    {
        if (EditorSpawns.selectedVehicle >= LevelVehicles.tables.Count)
            return;

        LevelVehicles.tables[EditorSpawns.selectedVehicle].SetSpawnTableNameLocal(state);
    }

    [HarmonyPatch(typeof(EditorSpawnsItemsUI), "onTypedTableNameField")]
    [HarmonyPostfix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static void OnItemNameUpdated(ISleekField field, string state)
    {
        if (EditorSpawns.selectedItem >= LevelItems.tables.Count)
            return;

        LevelItems.tables[EditorSpawns.selectedItem].SetSpawnTableNameLocal(state);
    }

    [HarmonyPatch(typeof(EditorSpawnsZombiesUI), "onTypedNameField")]
    [HarmonyPostfix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static void OnZombieNameUpdated(ISleekField field, string state)
    {
        if (EditorSpawns.selectedZombie >= LevelZombies.tables.Count)
            return;

        LevelZombies.tables[EditorSpawns.selectedZombie].SetSpawnTableNameLocal(state);
    }
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