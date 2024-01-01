﻿#if CLIENT
using DevkitServer.API;
using DevkitServer.API.Devkit.Spawns;
using DevkitServer.Core.Tools;
using DevkitServer.Players;
using HarmonyLib;
using SDG.Framework.Devkit;
using System.Reflection;
using System.Reflection.Emit;

namespace DevkitServer.Patches;

[HarmonyPatch]
internal static class SpawnsEditorPatches
{
    private const string Source = "SPAWN PATCHES";

    internal static void ManualPatches()
    {
        try
        {
            HarmonyMethod transpiler = new HarmonyMethod(Accessor.GetMethod(TranspileOpenAndCloseMethods));

            Type[] types = { typeof(EditorSpawnsItemsUI), typeof(EditorSpawnsVehiclesUI), typeof(EditorSpawnsAnimalsUI), typeof(EditorSpawnsZombiesUI), typeof(EditorLevelPlayersUI) };
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

                if (patch)
                {
                    PatchesMain.Patcher.Patch(openMethod, transpiler: transpiler);
                    PatchesMain.Patcher.Patch(closeMethod, transpiler: transpiler);
                }
            }

            MethodInfo? clickMethod = typeof(EditorLevelUI).GetMethod("onClickedPlayersButton", BindingFlags.NonPublic | BindingFlags.Instance);
            if (clickMethod == null)
            {
                Logger.DevkitServer.LogWarning(Source, $"Method not found: {FormattingUtil.FormatMethod(typeof(void), typeof(EditorLevelUI), "onClickedPlayersButton", namedArguments: [ (typeof(ISleekElement), "button") ])}.");
            }
            else PatchesMain.Patcher.Patch(clickMethod, transpiler: new HarmonyMethod(Accessor.GetMethod(TranspileOnClickedPlayersButton)));
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
        foreach (DevkitSelection selection in DevkitSelectionManager.selection)
        {
            if (selection.transform.TryGetComponent(out PlayerSpawnpointNode node))
            {
                node.Spawnpoint.SetIsAlternateLocal(state);
            }
        }
    }

    [HarmonyPatch(typeof(EditorSpawnsAnimalsUI), "onClickedTableButton")]
    [HarmonyPostfix]
    [UsedImplicitly]
    private static void OnAnimalSelectionChanged(ISleekElement button)
    {
        if (EditorSpawns.selectedAnimal >= LevelAnimals.tables.Count)
            return;
        AnimalTable table = LevelAnimals.tables[EditorSpawns.selectedAnimal];
        foreach (DevkitSelection selection in DevkitSelectionManager.selection)
        {
            if (selection.transform.TryGetComponent(out AnimalSpawnpointNode node))
            {
                node.Spawnpoint.type = EditorSpawns.selectedAnimal;
                node.Color = table.color;

                Logger.DevkitServer.LogDebug(Source, $"Spawn table updated for {node.Format()}.");
                SpawnUtil.EventOnAnimalSpawnTableChanged.TryInvoke(node.Spawnpoint, node.Index);
            }
        }
    }

    [HarmonyPatch(typeof(EditorSpawnsVehiclesUI), "onClickedTableButton")]
    [HarmonyPostfix]
    [UsedImplicitly]
    private static void OnVehicleSelectionChanged(ISleekElement button)
    {
        if (EditorSpawns.selectedVehicle >= LevelVehicles.tables.Count)
            return;
        VehicleTable table = LevelVehicles.tables[EditorSpawns.selectedVehicle];
        foreach (DevkitSelection selection in DevkitSelectionManager.selection)
        {
            if (selection.transform.TryGetComponent(out VehicleSpawnpointNode node))
            {
                node.Spawnpoint.type = EditorSpawns.selectedVehicle;
                node.Color = table.color;

                Logger.DevkitServer.LogDebug(Source, $"Spawn table updated for {node.Format()}.");
                SpawnUtil.EventOnVehicleSpawnTableChanged.TryInvoke(node.Spawnpoint, node.Index);
            }
        }
    }

    [HarmonyPatch(typeof(EditorSpawnsItemsUI), "onClickedTableButton")]
    [HarmonyPostfix]
    [UsedImplicitly]
    private static void OnItemSelectionChanged(ISleekElement button)
    {
        if (EditorSpawns.selectedItem >= LevelItems.tables.Count)
            return;
        ItemTable table = LevelItems.tables[EditorSpawns.selectedItem];
        foreach (DevkitSelection selection in DevkitSelectionManager.selection)
        {
            if (selection.transform.TryGetComponent(out ItemSpawnpointNode node))
            {
                node.Spawnpoint.type = EditorSpawns.selectedItem;
                node.Color = table.color;

                Logger.DevkitServer.LogDebug(Source, $"Spawn table updated for {node.Format()}.");
                SpawnUtil.EventOnItemSpawnTableChanged.TryInvoke(node.Spawnpoint, node.Region);
            }
        }
    }

    [HarmonyPatch(typeof(EditorSpawnsZombiesUI), "onClickedTableButton")]
    [HarmonyPostfix]
    [UsedImplicitly]
    private static void OnZombieSelectionChanged(ISleekElement button)
    {
        if (EditorSpawns.selectedZombie >= LevelZombies.tables.Count)
            return;
        ZombieTable table = LevelZombies.tables[EditorSpawns.selectedZombie];
        foreach (DevkitSelection selection in DevkitSelectionManager.selection)
        {
            if (selection.transform.TryGetComponent(out ZombieSpawnpointNode node))
            {
                node.Spawnpoint.type = EditorSpawns.selectedZombie;
                node.Color = table.color;

                Logger.DevkitServer.LogDebug(Source, $"Spawn table updated for {node.Format()}.");
                SpawnUtil.EventOnZombieSpawnTableChanged.TryInvoke(node.Spawnpoint, node.Region);
            }
        }
    }

    [HarmonyPatch(typeof(EditorSpawnsItemsUI), "onTypedTableNameField")]
    [HarmonyPostfix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static void OnItemNameUpdated(ISleekField field, string state)
    {
        if (EditorSpawns.selectedItem >= LevelItems.tables.Count)
            return;

        SpawnTableUtil.EventOnItemSpawnTableNameUpdated.TryInvoke(LevelItems.tables[EditorSpawns.selectedItem], EditorSpawns.selectedItem);
    }

    [HarmonyPatch(typeof(EditorSpawnsAnimalsUI), "onTypedNameField")]
    [HarmonyPostfix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static void OnAnimalNameUpdated(ISleekField field, string state)
    {
        if (EditorSpawns.selectedAnimal >= LevelAnimals.tables.Count)
            return;

        SpawnTableUtil.EventOnAnimalSpawnTableNameUpdated.TryInvoke(LevelAnimals.tables[EditorSpawns.selectedAnimal], EditorSpawns.selectedAnimal);
    }

    [HarmonyPatch(typeof(EditorSpawnsZombiesUI), "onTypedNameField")]
    [HarmonyPostfix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static void OnZombieNameUpdated(ISleekField field, string state)
    {
        if (EditorSpawns.selectedZombie >= LevelZombies.tables.Count)
            return;

        SpawnTableUtil.EventOnZombieSpawnTableNameUpdated.TryInvoke(LevelZombies.tables[EditorSpawns.selectedZombie], EditorSpawns.selectedZombie);
    }

    [HarmonyPatch(typeof(EditorSpawnsVehiclesUI), "onTypedNameField")]
    [HarmonyPostfix]
    [UsedImplicitly]
    [HarmonyPriority(-1)]
    private static void OnVehicleNameUpdated(ISleekField field, string state)
    {
        if (EditorSpawns.selectedVehicle >= LevelVehicles.tables.Count)
            return;

        SpawnTableUtil.EventOnVehicleSpawnTableNameUpdated.TryInvoke(LevelVehicles.tables[EditorSpawns.selectedVehicle], EditorSpawns.selectedVehicle);
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