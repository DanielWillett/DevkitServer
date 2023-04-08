using DevkitServer.Multiplayer;
using HarmonyLib;
using JetBrains.Annotations;
using System.Reflection;
#if CLIENT
using DevkitServer.Multiplayer.LevelData;
using SDG.Provider;
#endif
#if SERVER
using System.Reflection.Emit;
#endif

namespace DevkitServer.Patches;
[HarmonyPatch]
[EarlyTypeInit]
internal static class PatchesMain
{
    public const string HarmonyId = "dw.devkitserver";
    internal static Harmony Patcher { get; private set; } = null!;
    internal static void Init()
    {
        try
        {
            Patcher = new Harmony(HarmonyId);
            Patcher.PatchAll();
            Logger.LogInfo("Patched");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
            DevkitServerModule.Fault();
            Unpatch();
        }
    }
    internal static void Unpatch()
    {
        try
        {
            Patcher.UnpatchAll(HarmonyId);
            Logger.LogInfo("Unpatched");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
        }
    }

#if CLIENT
    private static readonly InstanceGetter<TempSteamworksWorkshop, List<PublishedFileId_t>> GetServerPendingIDs =
        Accessor.GenerateInstanceGetter<TempSteamworksWorkshop, List<PublishedFileId_t>>("serverPendingIDs", BindingFlags.NonPublic, true)!;

    private static readonly Action<LevelInfo, List<PublishedFileId_t>> ApplyServerAssetMapping =
        Accessor.GenerateStaticCaller<Assets, Action<LevelInfo, List<PublishedFileId_t>>>("ApplyServerAssetMapping", null, true)!;

    //private static readonly Action LoadGameMode = Accessor.GenerateStaticCaller<Provider, Action>("loadGameMode", throwOnError: true)!;

    [HarmonyPatch(typeof(Provider), nameof(Provider.launch))]
    [HarmonyPrefix]
    private static bool LaunchPatch()
    {
        Provider.provider.matchmakingService.refreshRules(Provider.currentServerInfo.ip, Provider.currentServerInfo.queryPort);
        Provider.provider.matchmakingService.onRulesQueryRefreshed += RulesReady;
        return false;
    }
    internal static void Launch()
    {
        if (!DevkitServerModule.IsEditing)
        {
            Provider.launch();
            return;
        }

        LevelInfo level = DevkitServerModule.PendingLevelInfo ?? Level.getLevel(Provider.map);
        DevkitServerModule.PendingLevelInfo = null;
        if (level == null)
        {
            Provider._connectionFailureInfo = ESteamConnectionFailureInfo.MAP;
            Provider.RequestDisconnect("could not find level \"" + Provider.map + "\"");
        }
        else
        {
            ApplyServerAssetMapping(level, GetServerPendingIDs(Provider.provider.workshopService));
            UnturnedLog.info("Loading server level ({0})", Provider.map);
            Level.load(level, false);
            Provider.gameMode = new DevkitServerGamemode();
        }

    }
    private static void RulesReady(Dictionary<string, string> rulesmap)
    {
        Provider.provider.matchmakingService.onRulesQueryRefreshed -= RulesReady;
#if DEBUG
        Logger.LogInfo("Server rules: ");
        foreach (KeyValuePair<string, string> kvp in rulesmap)
        {
            Logger.LogInfo($"{kvp.Key}: {kvp.Value}.");
        }
#endif
        if (rulesmap.TryGetValue(DevkitServerModule.ServerRule, out string val) && val.Equals("True", StringComparison.InvariantCultureIgnoreCase))
        {
            DevkitServerModule.IsEditing = true;
            Logger.LogDebug("Found tag '" + DevkitServerModule.ServerRule + "'.");
            EditorLevel.RequestLevel();
        }
        else
        {
            Logger.LogDebug("Did not find tag '" + DevkitServerModule.ServerRule + "'.");
            DevkitServerModule.IsEditing = false;
            Launch();
        }
    }

#endif

    [HarmonyPatch(typeof(Provider), "loadGameMode")]
    [HarmonyPrefix]
    [UsedImplicitly]
    private static bool PrefixLoadGameMode()
    {
        if (DevkitServerModule.IsEditing)
        {
            Provider.gameMode = new DevkitServerGamemode();
            return false;
        }

        return true;
    }

#if SERVER
    [UsedImplicitly]
    [HarmonyPatch(typeof(LevelLighting), nameof(LevelLighting.updateLocal), typeof(Vector3), typeof(float), typeof(IAmbianceNode))]
    [HarmonyPrefix]
    private static bool UpdateLighting(Vector3 point, float windOverride, IAmbianceNode effectNode) => !DevkitServerModule.IsEditing;

    [UsedImplicitly]
    [HarmonyPatch("EditorInteract", "Update", MethodType.Normal)]
    [HarmonyPrefix]
    private static bool EditorInteractUpdate() => !DevkitServerModule.IsEditing;

    [UsedImplicitly]
    [HarmonyPatch("EditorUI", "Start", MethodType.Normal)]
    [HarmonyPrefix]
    private static bool EditorUIStart() => !DevkitServerModule.IsEditing;

    [UsedImplicitly]
    [HarmonyPatch("MainCamera", "Awake", MethodType.Normal)]
    [HarmonyPrefix]
    private static bool MainCameraAwake() => !DevkitServerModule.IsEditing;

    [UsedImplicitly]
    [HarmonyPatch(typeof(ObjectManager), "Update")]
    [HarmonyPrefix]
    private static bool ObjectManagerUpdate() => !DevkitServerModule.IsEditing;

    [UsedImplicitly]
    [HarmonyPatch(typeof(Editor), "save")]
    [HarmonyPrefix]
    private static bool SaveEditorSettings() => !DevkitServerModule.IsEditing;

    [UsedImplicitly]
    [HarmonyPatch(typeof(Provider), "AdvertiseConfig", MethodType.Normal)]
    [HarmonyPrefix]
    private static void AdvertiseConfig()
    {
        Logger.LogDebug("Setting SteamGameServer KeyValue '" + DevkitServerModule.ServerRule + "' to 'True'.");
        SteamGameServer.SetKeyValue(DevkitServerModule.ServerRule, "True");
    }
    private static readonly MethodInfo lvlLoad = typeof(Level).GetMethod(nameof(Level.load))!;
    private static readonly MethodInfo lvlEdit = typeof(PatchesMain).GetMethod(nameof(OnLoadEdit), BindingFlags.NonPublic | BindingFlags.Static)!;

    [HarmonyPatch(typeof(Provider), "onDedicatedUGCInstalled")]
    [HarmonyTranspiler]
    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> DedicatedUgcLoadedTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (CodeInstruction instr in instructions)
        {
            if (instr.Calls(lvlLoad))
            {
                yield return new CodeInstruction(OpCodes.Pop);
                yield return new CodeInstruction(OpCodes.Call, lvlEdit);
            }
            else
                yield return instr;
        }
    }
    private static void OnLoadEdit(LevelInfo info)
    {
        Logger.LogInfo("Loading DevkitServerEditor for " + info.getLocalizedName() + ".");
        DevkitServerModule.IsEditing = true;
        Level.edit(info);
    }

    [HarmonyPatch(typeof(ZombieManager), "onLevelLoaded")]
    [HarmonyPrefix]
    [UsedImplicitly]
    private static bool PrefixZombieManagerOnLevelLoaded(int level)
    {
        return false;
    }

    [HarmonyPatch(typeof(VehicleManager), "onLevelLoaded")]
    [HarmonyTranspiler]
    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> VehicleManagerLevelLoadedTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase __method) => Accessor.AddIsEditorCall(instructions, __method);

    [HarmonyPatch(typeof(BarricadeManager), "onLevelLoaded")]
    [HarmonyTranspiler]
    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> BarricadeManagerLevelLoadedTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase __method) => Accessor.AddIsEditorCall(instructions, __method);

    [HarmonyPatch(typeof(AnimalManager), "onLevelLoaded")]
    [HarmonyTranspiler]
    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> AnimalManagerLevelLoadedTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase __method) => Accessor.AddIsEditorCall(instructions, __method);

    [HarmonyPatch(typeof(GroupManager), "onLevelLoaded")]
    [HarmonyTranspiler]
    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> GroupManagerLevelLoadedTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase __method) => Accessor.AddIsEditorCall(instructions, __method);

    [HarmonyPatch(typeof(ObjectManager), "onLevelLoaded")]
    [HarmonyTranspiler]
    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> ObjectManagerLevelLoadedTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase __method) => Accessor.AddIsEditorCall(instructions, __method);

    [HarmonyPatch(typeof(StructureManager), "onLevelLoaded")]
    [HarmonyTranspiler]
    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> StructureManagerLevelLoadedTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase __method) => Accessor.AddIsEditorCall(instructions, __method);

    [HarmonyPatch(typeof(VehicleManager), "onPostLevelLoaded")]
    [HarmonyTranspiler]
    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> VehicleManagerPostLevelLoadedTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase __method) => Accessor.AddIsEditorCall(instructions, __method);
#endif
}
