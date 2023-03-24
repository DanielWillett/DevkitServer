using HarmonyLib;
using SDG.Provider;
using System.Reflection;
using System.Reflection.Emit;
using DevkitServer.Multiplayer;
using JetBrains.Annotations;
using Action = System.Action;

namespace DevkitServer.Patches;
[HarmonyPatch]
internal static class PatchesMain
{
    // private static readonly MethodInfo logMethod = typeof(Logger).GetMethod(nameof(Logger.LogInfo))!;
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
        Accessor.GenerateInstanceGetter<TempSteamworksWorkshop, List<PublishedFileId_t>>("serverPendingIDs", BindingFlags.NonPublic);

    private static readonly Action<LevelInfo, List<PublishedFileId_t>> ApplyServerAssetMapping =
        Accessor.GetStaticMethod<Assets, Action<LevelInfo, List<PublishedFileId_t>>>("ApplyServerAssetMapping");

    private static readonly Action LoadGameMode = Accessor.GetStaticMethod<Provider, Action>("loadGameMode");

    [HarmonyPatch(typeof(Provider), nameof(Provider.launch))]
    [HarmonyPrefix]
    private static bool LaunchPatch()
    {
        Provider.provider.matchmakingService.refreshRules(Provider.currentServerInfo.ip, Provider.currentServerInfo.queryPort);
        Provider.provider.matchmakingService.onRulesQueryRefreshed += RulesReady;

        LevelInfo level = Level.getLevel(Provider.map);
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

        return false;
    }

    private static void RulesReady(Dictionary<string, string> rulesmap)
    {
        foreach (KeyValuePair<string, string> kvp in rulesmap)
        {
            Logger.LogInfo($"{kvp.Key}: {kvp.Value}.");
        }
        if (rulesmap.TryGetValue("DevkitServer", out string val) && val.Equals("True", StringComparison.Ordinal))
        {
            DevkitServerModule.IsEditing = true;
            Logger.LogInfo("Found tag 'DevkitServer'.");
        }
        else
        {
            Logger.LogInfo("Did not find tag 'DevkitServer'.");
            DevkitServerModule.IsEditing = false;
        }
        Provider.provider.matchmakingService.onRulesQueryRefreshed -= RulesReady;
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
    [HarmonyPatch(typeof(LevelLighting), nameof(LevelLighting.updateLocal), typeof(Vector3), typeof(float), typeof(IAmbianceNode))]
    [HarmonyPrefix]
    private static bool UpdateLighting(Vector3 point, float windOverride, IAmbianceNode effectNode) => !DevkitServerModule.IsEditing;
    [HarmonyPatch("EditorInteract", "Update", MethodType.Normal)]
    [HarmonyPrefix]
    private static bool EditorInteractUpdate() => !DevkitServerModule.IsEditing;
    [HarmonyPatch("EditorUI", "Start", MethodType.Normal)]
    [HarmonyPrefix]
    private static bool EditorUIStart() => !DevkitServerModule.IsEditing;
    [HarmonyPatch("MainCamera", "Awake", MethodType.Normal)]
    [HarmonyPrefix]
    private static bool MainCameraAwake() => !DevkitServerModule.IsEditing;

    [HarmonyPatch(typeof(Provider), "AdvertiseConfig", MethodType.Normal)]
    [HarmonyPrefix]
    private static void AdvertiseConfig()
    {
        Logger.LogInfo("Setting SteamGameServer KeyValue 'DevkitServer' to 'True'.");
        SteamGameServer.SetKeyValue("DevkitServer", "True");
    }
    private static readonly MethodInfo lvlLoad = typeof(Level).GetMethod(nameof(Level.load))!;
    private static readonly MethodInfo lvlEdit = typeof(PatchesMain).GetMethod(nameof(OnLoadEdit), BindingFlags.NonPublic | BindingFlags.Static)!;

    [HarmonyPatch(typeof(Provider), "onDedicatedUGCInstalled")]
    [HarmonyTranspiler]
    [UsedImplicitly]
    static IEnumerable<CodeInstruction> DedicatedUgcLoadedTranspiler(IEnumerable<CodeInstruction> instructions)
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
