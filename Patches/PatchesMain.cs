using DevkitServer.Multiplayer;
using HarmonyLib;
using System.Reflection;
using System.Text;
using DevkitServer.Configuration;
using DevkitServer.Players;
using SDG.Framework.Landscapes;
using System.Reflection.Emit;
using Version = System.Version;
#if CLIENT
using DevkitServer.Multiplayer.Levels;
#endif
#if SERVER
using DevkitServer.Multiplayer.Networking;
using SDG.NetPak;
#endif

namespace DevkitServer.Patches;
[HarmonyPatch]
[EarlyTypeInit]
internal static class PatchesMain
{
    public const string HarmonyId = "dw.devkitserver";
    private const string Source = "PATCHING";
    internal static Harmony Patcher { get; private set; } = null!;
    internal static void Init()
    {
        try
        {
#if DEBUG
            string path = Path.Combine(DevkitServerConfig.Directory, "harmony.log");
            Environment.SetEnvironmentVariable("HARMONY_LOG_FILE", path);
            DevkitServerUtility.CheckDirectory(false, true, DevkitServerConfig.Directory, null);
            try
            {
                using FileStream str = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                byte[] bytes = Encoding.UTF8.GetBytes(DateTimeOffset.UtcNow.ToString("R") + Environment.NewLine);
                str.Write(bytes, 0, bytes.Length);
                str.Flush();
            }
            catch (Exception ex)
            {
                Logger.LogError("Unable to clear previous harmony log.");
                Logger.LogError(ex);
            }
            Harmony.DEBUG = true;
#endif
            Patcher = new Harmony(HarmonyId);
            Patcher.PatchAll();
#if SERVER
            ServerGizmoPatches.Patch();
#endif
#if CLIENT
            LevelObjectPatches.OptionalPatches();
#endif
            DoManualPatches();
            
            Logger.LogInfo($"Finished patching {"Unturned".Colorize(DevkitServerModule.UnturnedColor)}.");
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
            DoManualUnpatches();
            Logger.LogInfo($"Finished unpatching {"Unturned".Colorize(DevkitServerModule.UnturnedColor)}.");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
        }
    }
    private static void DoManualPatches()
    {
        try
        {
            MethodInfo? method = typeof(Level).GetMethod(nameof(Level.includeHash), BindingFlags.Public | BindingFlags.Static);
            if (method != null)
            {
                Patcher.Patch(method, prefix: new HarmonyMethod(Accessor.GetMethod(PatchLevelIncludeHatch)));
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Patcher error: Level.includeHash.");
            Logger.LogError(ex);
        }
    }
    private static void DoManualUnpatches()
    {
        try
        {
            MethodInfo? method = typeof(Level).GetMethod(nameof(Level.includeHash), BindingFlags.Public | BindingFlags.Static);
            if (method != null)
            {
                Patcher.Unpatch(method, Accessor.GetMethod(PatchLevelIncludeHatch));
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Patcher unpatching error: Level.includeHash.");
            Logger.LogError(ex);
        }
    }

    private static bool PatchLevelIncludeHatch(string id, byte[] pendingHash)
    {
        return !Level.isLoaded;
    }

    private static bool IsEditorMode(PlayerCaller caller)
    {
        if (!DevkitServerModule.IsEditing)
            return false;
#if CLIENT
        if (caller.channel.isOwner)
            return UserInput.LocalController == CameraController.Editor;
#endif
        EditorUser? user = UserManager.FromId(caller.player.channel.owner.playerID.steamID.m_SteamID);
        return !(user != null && user.Input.Controller == CameraController.Player);
    }
#if CLIENT

    [HarmonyPatch(typeof(LoadingUI), "Update")]
    [HarmonyTranspiler]
    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> TranspileInitializePlayer(IEnumerable<CodeInstruction> instructions, MethodBase method, ILGenerator generator)
    {
        FieldInfo? playerUIInstance = typeof(PlayerUI).GetField("instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (playerUIInstance == null)
        {
            Logger.LogWarning($"{method.Format()} - Unable to find field: PlayerUI.instance.", method: Source);
            DevkitServerModule.Fault();
        }
        FieldInfo? editorUIInstance = typeof(EditorUI).GetField("instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (playerUIInstance == null)
        {
            Logger.LogWarning($"{method.Format()} - Unable to find field: EditorUI.instance.", method: Source);
            DevkitServerModule.Fault();
        }

        MethodInfo getController = UserInput.GetLocalControllerMethod;

        List<CodeInstruction> ins = new List<CodeInstruction>(instructions);
        bool patchedOutPlayerUI = false;
        bool patchedOutEditorUI = false;
        for (int i = 1; i < ins.Count; ++i)
        {
            if (PatchUtil.MatchPattern(ins, i,
                    x => playerUIInstance != null && x.LoadsField(playerUIInstance) ||
                         editorUIInstance != null && x.LoadsField(editorUIInstance),
                    x => x.opcode != OpCodes.Ldnull) && ins[i - 1].operand is Label label)
            {
                Label lbl = generator.DefineLabel();
                ins.Insert(i, new CodeInstruction(OpCodes.Call, Accessor.IsDevkitServerGetter));
                ins.Insert(i + 1, new CodeInstruction(OpCodes.Brfalse, lbl));
                ins.Insert(i + 2, new CodeInstruction(OpCodes.Call, getController));
                CameraController current = ins[i + 3].LoadsField(playerUIInstance) ? CameraController.Player : CameraController.Editor;
                ins.Insert(i + 3, PatchUtil.LoadConstantI4((int)current));
                ins.Insert(i + 4, new CodeInstruction(OpCodes.Bne_Un, label));
                i += 5;
                ins[i].labels.Add(lbl);
                patchedOutPlayerUI |= current == CameraController.Player;
                patchedOutEditorUI |= current == CameraController.Editor;
            }
        }

        if (!patchedOutPlayerUI)
        {
            Logger.LogWarning($"{method.Format()} - Unable to edit call to {FormattingUtil.FormatMethod(typeof(void), typeof(PlayerUI), "Player_OnGUI", arguments: Array.Empty<Type>())}.", method: Source);
            DevkitServerModule.Fault();
        }
        if (!patchedOutEditorUI)
        {
            Logger.LogWarning($"{method.Format()} - Unable to edit call to {FormattingUtil.FormatMethod(typeof(void), typeof(EditorUI), "Editor_OnGUI", arguments: Array.Empty<Type>())}.", method: Source);
            DevkitServerModule.Fault();
        }

        return ins;
    }

    
    [HarmonyPatch("SDG.Unturned.Level, Assembly-CSharp", nameof(Level.isLoading), MethodType.Getter)]
    [HarmonyPrefix]
    [UsedImplicitly]
    private static bool PrefixGetIsLevelLoading(ref bool __result)
    {
        if (!DevkitServerModule.IsEditing)
            return true;

        __result = Level.isEditor && Level.isLoadingContent;
        return false;
    }
    
    private static bool _shouldContinueToLaunch;
    [HarmonyPatch(typeof(Provider), nameof(Provider.launch))]
    [HarmonyPrefix]
    [UsedImplicitly]
    private static bool LaunchPatch()
    {
        if (_shouldContinueToLaunch)
            return true;
        Provider.provider.matchmakingService.refreshRules(Provider.currentServerInfo.ip, Provider.currentServerInfo.queryPort);
        Provider.provider.matchmakingService.onRulesQueryRefreshed += RulesReady;
        return false;
    }
    private static void ProceedWithLaunch()
    {
        _shouldContinueToLaunch = true;
        try
        {
            Provider.launch();
        }
        finally
        {
            _shouldContinueToLaunch = false;
        }
    }
    internal static void Launch(LevelInfo? overrideLevelInfo)
    {
        if (!DevkitServerModule.IsEditing)
        {
            ProceedWithLaunch();
            return;
        }

        LevelInfo level = overrideLevelInfo ?? Level.getLevel(Provider.map);
        if (level == null)
        {
            DevkitServerUtility.CustomDisconnect("Could not find level \"" + Provider.map + "\"", ESteamConnectionFailureInfo.MAP);
        }
        else
        {
            Logger.LogInfo($"Loading server level: {level.getLocalizedName().Format(false)}.");
            Level.edit(level);
            Provider.gameMode = new DevkitServerGamemode();
        }
    }
    private static void RulesReady(Dictionary<string, string> rulesmap)
    {
        Provider.provider.matchmakingService.onRulesQueryRefreshed -= RulesReady;
        if (rulesmap.TryGetValue(DevkitServerModule.ServerRule, out string val))
        {
            if (!Version.TryParse(val, out Version version) || !DevkitServerModule.IsCompatibleWith(version))
            {
                DevkitServerUtility.CustomDisconnect(DevkitServerModule.MainLocalization.Translate("VersionKickMessage",
                    version?.ToString(4) ?? "[Unspecified]", Accessor.DevkitServer.GetName().Version.ToString(4)), ESteamConnectionFailureInfo.CUSTOM);
                return;
            }
            DevkitServerModule.IsEditing = true;
            Logger.LogInfo($"Connecting to a server running {DevkitServerModule.ModuleName.Colorize(DevkitServerModule.ModuleColor)} " +
                           $"v{version.Format()} (You are running {Accessor.DevkitServer.GetName().Version.Format()})...");
            EditorLevel.RequestLevel();
        }
        else
        {
            Logger.LogDebug($"Did not find tag {DevkitServerModule.ServerRule.Format()}.");
            DevkitServerModule.IsEditing = false;
            ProceedWithLaunch();
        }
    }

    private static readonly MethodInfo IsPlayerControlledOrNotEditingMethod =
        typeof(PatchesMain).GetMethod(nameof(IsPlayerControlledOrNotEditing),
            BindingFlags.Static | BindingFlags.NonPublic)!;

    private static bool IsPlayerControlledOrNotEditing()
    {
        return !DevkitServerModule.IsEditing || !Level.isEditor || UserInput.LocalController == CameraController.Player;
    }
    private static bool IsEditorControlledOrNotEditing()
    {
        return !DevkitServerModule.IsEditing || !Level.isEditor || UserInput.LocalController == CameraController.Editor;
    }

    [UsedImplicitly]
    [HarmonyPatch(typeof(PlayerLook), "Update")]
    [HarmonyPrefix]
    private static bool PlayerLookUpdate(PlayerLook __instance) => !IsEditorMode(__instance);
    [UsedImplicitly]
    [HarmonyPatch(typeof(PlayerAnimator), "Update")]
    [HarmonyPrefix]
    private static bool PlayerAnimatorUpdate(PlayerAnimator __instance) => !IsEditorMode(__instance);
    [UsedImplicitly]
    [HarmonyPatch(typeof(PlayerAnimator), "LateUpdate")]
    [HarmonyPrefix]
    private static bool PlayerAnimatorLateUpdate(PlayerAnimator __instance) => !IsEditorMode(__instance);
    [UsedImplicitly]
    [HarmonyPatch(typeof(PlayerEquipment), "Update")]
    [HarmonyPrefix]
    private static bool PlayerEquipmentUpdate(PlayerEquipment __instance) => !IsEditorMode(__instance);
    [UsedImplicitly]
    [HarmonyPatch(typeof(PlayerMovement), "Update")]
    [HarmonyPrefix]
    private static bool PlayerMovementUpdate(PlayerMovement __instance) => !IsEditorMode(__instance);
    [UsedImplicitly]
    [HarmonyPatch(typeof(PlayerStance), "Update")]
    [HarmonyPrefix]
    private static bool PlayerStanceUpdate(PlayerStance __instance) => !IsEditorMode(__instance);
    [UsedImplicitly]
    [HarmonyPatch(typeof(PlayerInput), "FixedUpdate")]
    [HarmonyPrefix]
    private static bool PlayerInputFixedUpdate(PlayerInput __instance) => !IsEditorMode(__instance);
    [UsedImplicitly]
    [HarmonyPatch(typeof(PlayerInteract), "Update")]
    [HarmonyPrefix]
    private static bool PlayerInteractUpdate() => IsPlayerControlledOrNotEditing();
    [UsedImplicitly]
    [HarmonyPatch(typeof(PlayerUI), "Update")]
    [HarmonyPrefix]
    private static bool PlayerUIUpdate() => IsPlayerControlledOrNotEditing();
    [UsedImplicitly]
    [HarmonyPatch(typeof(EditorUI), "Update")]
    [HarmonyPrefix]
    private static bool EditorUIUpdate() => IsEditorControlledOrNotEditing();
    [UsedImplicitly]
    [HarmonyPatch(typeof(EditorPauseUI), "onClickedExitButton")]
    [HarmonyPrefix]
    private static bool EditorPauseUIExit()
    {
        if (!DevkitServerModule.IsEditing)
            return true;

        Provider.RequestDisconnect("clicked exit button from editor pause menu.");
        return false;
    }
    [UsedImplicitly]
    [HarmonyPatch(typeof(MenuConfigurationOptionsUI), MethodType.Constructor)]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> MenuConfigurationOptionsUITranspiler(IEnumerable<CodeInstruction> instructions)
        => MenuConfigurationUITranspiler(instructions);
    [UsedImplicitly]
    [HarmonyPatch(typeof(MenuConfigurationGraphicsUI), MethodType.Constructor)]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> MenuConfigurationGraphicsUITranspiler(IEnumerable<CodeInstruction> instructions)
        => MenuConfigurationUITranspiler(instructions);
    [UsedImplicitly]
    [HarmonyPatch(typeof(MenuConfigurationDisplayUI), MethodType.Constructor)]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> MenuConfigurationDisplayUITranspiler(IEnumerable<CodeInstruction> instructions)
        => MenuConfigurationUITranspiler(instructions);
    [UsedImplicitly]
    [HarmonyPatch(typeof(MenuConfigurationControlsUI), MethodType.Constructor)]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> MenuConfigurationControlsUITranspiler(IEnumerable<CodeInstruction> instructions)
        => MenuConfigurationUITranspiler(instructions);

    private static IEnumerable<CodeInstruction> MenuConfigurationUITranspiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo? isConnected = typeof(Provider).GetProperty(nameof(Provider.isConnected), BindingFlags.Static | BindingFlags.Public)?.GetMethod;
        if (isConnected == null)
        {
            Logger.LogWarning("Unable to find getter: Provider.isConnected.");
            DevkitServerModule.Fault();
        }
        List<CodeInstruction> inst = new List<CodeInstruction>(instructions);
        bool one = isConnected == null;
        for (int i = 0; i < inst.Count; ++i)
        {
            CodeInstruction c = inst[i];
            yield return c;
            if (!one && c.Calls(isConnected))
            {
                yield return new CodeInstruction(OpCodes.Call, IsPlayerControlledOrNotEditingMethod);
                yield return new CodeInstruction(OpCodes.And);
                one = true;
            }
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
    internal static List<ITransportConnection> PendingConnections = new List<ITransportConnection>(8);

    [HarmonyPatch(typeof(Provider), "host")]
    [HarmonyPrefix]
    [UsedImplicitly]
    private static bool PrefixHost()
    {
        if (DevkitServerModule.HasLoadedBundle)
            return true;
        DevkitServerModule.ComponentHost.StartCoroutine(DevkitServerModule.Instance.TryLoadBundle(DoHost));
        return false;
    }

    private static void DoHost()
    {
        if (DevkitServerModule.HasLoadedBundle)
            Provider.host();
        else
        {
            Logger.LogError($"Unable to host without {Path.Combine(DevkitServerConfig.BundlesFolder, "devkitserver.masterbundle").Format(false)}. Try redownloading from {DevkitServerModule.RepositoryUrl}.", method: "TryLoadBundle");
            DevkitServerModule.Fault();
        }
    }
    internal static void RemoveExpiredConnections()
    {
        for (int i = PendingConnections.Count - 1; i >= 0; --i)
        {
            if (!PendingConnections[i].TryGetPort(out _))
                PendingConnections.RemoveAt(i);
        }
    }
    internal static void RemoveConnection(ITransportConnection connection)
    {
        for (int i = PendingConnections.Count - 1; i >= 0; --i)
        {
            if (PendingConnections[i].Equals(connection))
                PendingConnections.RemoveAt(i);
        }
    }
    [NetCall(NetCallSource.FromClient, (ushort)NetCalls.SendPending)]
    [UsedImplicitly]
    private static void OnConnectionPending(MessageContext ctx)
    {
        OnConnectionPending(ctx.Connection, null!);
        ctx.Acknowledge();
    }

    [HarmonyPatch("ServerMessageHandler_GetWorkshopFiles", "ReadMessage", MethodType.Normal)]
    [HarmonyPrefix]
    private static void OnConnectionPending(ITransportConnection transportConnection, NetPakReader reader)
    {
        RemoveExpiredConnections();
        if (!PendingConnections.Contains(transportConnection))
        {
            RemoveConnection(transportConnection);
            PendingConnections.Add(transportConnection);
            Logger.LogDebug("Connection pending: " + transportConnection.Format() + ".");
        }
        else
            Logger.LogDebug("Connection already pending: " + transportConnection.Format() + ".");
    }
    [HarmonyPatch("ServerMessageHandler_ReadyToConnect", "ReadMessage", MethodType.Normal)]
    [HarmonyPrefix]
    [UsedImplicitly]
    private static void OnReadyToConnect(ITransportConnection transportConnection, NetPakReader reader)
    {
        RemoveExpiredConnections();
        RemoveConnection(transportConnection);
        Logger.LogDebug("Connection ready: " + transportConnection.Format() + ".");
    }
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
    [HarmonyPatch(typeof(PlayerStance), "Update")]
    [HarmonyPrefix]
    private static bool PlayerStanceUpdate(PlayerStance __instance) => !IsEditorMode(__instance);

    [UsedImplicitly]
    [HarmonyPatch(typeof(PlayerStance), "GetStealthDetectionRadius")]
    [HarmonyPrefix]
    private static bool GetStealthDetectionRadius(ref float __result)
    {
        if (!DevkitServerModule.IsEditing)
            return true;
        __result = 0f;
        return false;
    }

    [UsedImplicitly]
    [HarmonyPatch(typeof(Editor), "save")]
    [HarmonyPrefix]
    private static bool SaveEditorSettings() => !DevkitServerModule.IsEditing;

    [UsedImplicitly]
    [HarmonyPatch(typeof(Provider), "AdvertiseConfig", MethodType.Normal)]
    [HarmonyPrefix]
    private static void AdvertiseConfig()
    {
        Version version = Accessor.DevkitServer.GetName().Version;
        Logger.LogDebug($"Setting SteamGameServer KeyValue {DevkitServerModule.ServerRule.Format()} to {version.Format()}.");
        SteamGameServer.SetKeyValue(DevkitServerModule.ServerRule, version.ToString(4));
    }

    [UsedImplicitly]
    [HarmonyPatch(typeof(LandscapeTile), nameof(LandscapeTile.updatePrototypes))]
    [HarmonyPrefix]
    private static void UpdatePrototypesPrefix(LandscapeTile __instance, ref TerrainLayer?[] ___terrainLayers)
    {
        // this function is commented out on the server build

        ___terrainLayers ??= new TerrainLayer[Landscape.SPLATMAP_LAYERS];

        for (int index = 0; index < Landscape.SPLATMAP_LAYERS; ++index)
        {
            AssetReference<LandscapeMaterialAsset> material = __instance.materials[index];
            LandscapeMaterialAsset landscapeMaterialAsset = material.Find();
            ___terrainLayers[index] = landscapeMaterialAsset?.getOrCreateLayer();
        }

        __instance.data.terrainLayers = ___terrainLayers;
    }

    private static readonly MethodInfo lvlLoad = typeof(Level).GetMethod(nameof(Level.load))!;
    private static readonly MethodInfo lvlEdit = typeof(PatchesMain).GetMethod(nameof(OnLoadEdit), BindingFlags.NonPublic | BindingFlags.Static)!;

    [HarmonyPatch(typeof(Provider), "onDedicatedUGCInstalled")]
    [HarmonyTranspiler]
    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> DedicatedUgcLoadedTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase method)
    {
        bool one = false;
        yield return new CodeInstruction(OpCodes.Call, new System.Action(MapCreation.PrefixLoadingDedicatedUGC).Method);

        foreach (CodeInstruction instr in instructions)
        {
            if (!one && instr.Calls(lvlLoad))
            {
                yield return new CodeInstruction(OpCodes.Pop);
                yield return new CodeInstruction(OpCodes.Call, lvlEdit);
                Logger.LogDebug("Inserted patch to " + method.Format() + " to load editor instead of player.");
                one = true;
            }
            else
                yield return instr;
        }
        if (!one)
        {
            Logger.LogWarning("Failed to insert " + method.Format() + " patch to load editor instead of player.");
            DevkitServerModule.Fault();
        }
    }

    private static readonly MethodInfo? getLvlHash = typeof(Level).GetProperty(nameof(Level.hash), BindingFlags.Public | BindingFlags.Static)?.GetGetMethod(true);

    [UsedImplicitly]
    private static bool OnCheckingLevelHash(ITransportConnection connection)
    {
        Logger.LogInfo(connection.Format() + " checking hash...");
        return DevkitServerModule.IsEditing;
    }

    [HarmonyPatch("ServerMessageHandler_ReadyToConnect", "ReadMessage", MethodType.Normal)]
    [HarmonyTranspiler]
    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> OnVerifyingPlayerDataTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase method)
    {
        MethodInfo mtd = typeof(PatchesMain).GetMethod(nameof(OnCheckingLevelHash), BindingFlags.Static | BindingFlags.NonPublic)!;
        MethodInfo? reject = typeof(Provider).GetMethods(BindingFlags.Static | BindingFlags.Public).FirstOrDefault(x =>
        {
            if (!x.Name.Equals(nameof(Provider.reject), StringComparison.Ordinal))
                return false;
            ParameterInfo[] ps = x.GetParameters();
            return ps.Length == 2 && ps[0].ParameterType == typeof(ITransportConnection) && ps[1].ParameterType == typeof(ESteamRejection);
        });
        if (reject == null)
        {
            Logger.LogWarning("Unable to find method: Provider.reject(ITransportConnection, ESteamRejection).");
            DevkitServerModule.Fault();
        }
        List<CodeInstruction> list = new List<CodeInstruction>(instructions);
        int c = 0;
        for (int i = 0; i < list.Count; ++i)
        {
            CodeInstruction ins = list[i];
            if (c < 2 && i < list.Count - 5 && getLvlHash != null && mtd != null
                && Accessor.IsDevkitServerGetter != null &&
                list[i].opcode == OpCodes.Ldarg_0 && list[i + 1].LoadsConstant(ESteamRejection.WRONG_HASH_LEVEL) &&
                list[i + 2].Calls(reject) && list[i + 3].opcode == OpCodes.Ret)
            {
                Label label = generator.DefineLabel();
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Call, mtd);
                yield return new CodeInstruction(OpCodes.Brtrue_S, label);
                Logger.LogDebug("Inserted patch to " + method.Format() + " to skip level hash verification.");
                yield return ins;
                yield return list[i + 1];
                yield return list[i + 2];
                yield return list[i + 3];
                i += 3;
                list[i + 4].labels.Add(label);
                ++c;
                continue;
            }
            
            yield return ins;
        }
        if (c != 2)
        {
            Logger.LogWarning("Failed to insert two " + method.Format() + " patches to skip level hash verification.");
            DevkitServerModule.Fault();
        }
    }
    private static void OnLoadEdit(LevelInfo info)
    {
        Logger.LogInfo("Loading DevkitServerEditor for " + info.getLocalizedName() + ".");
        DevkitServerModule.IsEditing = true;
        Level.edit(info);
    }
    
    [HarmonyPatch(typeof(LevelZombies), nameof(LevelZombies.load))]
    [HarmonyPostfix]
    [UsedImplicitly]
    private static void PostfixLoadLevelZombies()
    {
        FieldInfo? field = typeof(LevelZombies).GetField("_zombies", BindingFlags.Static | BindingFlags.NonPublic);
        if (field == null || !field.FieldType.IsAssignableFrom(typeof(List<ZombieSpawnpoint>[])))
        {
            Logger.LogError($"Unable to find field: {typeof(LevelZombies).Format()}._zombies.");
            DevkitServerModule.Fault();
            return;
        }

        List<ZombieSpawnpoint>[] regions = new List<ZombieSpawnpoint>[LevelNavigation.bounds.Count];
        field.SetValue(null, regions);

        for (int index = 0; index < regions.Length; ++index)
        {
            regions[index] = new List<ZombieSpawnpoint>();
        }

        int c = 0;
        for (int x = 0; x < Regions.WORLD_SIZE; ++x)
        {
            for (int y = 0; y < Regions.WORLD_SIZE; ++y)
            {
                List<ZombieSpawnpoint> spawnpoints = LevelZombies.spawns[x, y];
                foreach (ZombieSpawnpoint spawnpoint in spawnpoints)
                {
                    if (LevelNavigation.tryGetBounds(spawnpoint.point, out byte bound) && LevelNavigation.checkNavigation(spawnpoint.point))
                    {
                        regions[bound].Add(spawnpoint);
                        ++c;
                    }
                }
            }
        }
        Logger.LogInfo($"Copied over {c.Format()} zombie spawn{c.S()} from {typeof(LevelZombies).Format()}.");
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

    // [HarmonyPatch(typeof(ObjectManager), "onLevelLoaded")]
    // [HarmonyTranspiler]
    // [UsedImplicitly]
    // private static IEnumerable<CodeInstruction> ObjectManagerLevelLoadedTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase __method) => Accessor.AddIsEditorCall(instructions, __method);

    [HarmonyPatch(typeof(StructureManager), "onLevelLoaded")]
    [HarmonyTranspiler]
    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> StructureManagerLevelLoadedTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase __method) => Accessor.AddIsEditorCall(instructions, __method);

    [HarmonyPatch(typeof(VehicleManager), "onPostLevelLoaded")]
    [HarmonyTranspiler]
    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> VehicleManagerPostLevelLoadedTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase __method) => Accessor.AddIsEditorCall(instructions, __method);
#endif
    #region Landscape.writeMaps

    [HarmonyPatch(typeof(Landscape), nameof(Landscape.writeHeightmap))]
    [HarmonyTranspiler]
    [UsedImplicitly]
    [HarmonyPriority(1)]
    private static IEnumerable<CodeInstruction> LandscapeWriteHeightmapTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase method)
        => LandscapeWriteTranspilerGeneric(instructions, method, "heightmapTransactions");

    [HarmonyPatch(typeof(Landscape), nameof(Landscape.writeSplatmap))]
    [HarmonyTranspiler]
    [UsedImplicitly]
    [HarmonyPriority(1)]
    private static IEnumerable<CodeInstruction> LandscapeWriteSplatmapTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase method)
        => LandscapeWriteTranspilerGeneric(instructions, method, "splatmapTransactions");

    [HarmonyPatch(typeof(Landscape), nameof(Landscape.writeHoles))]
    [HarmonyTranspiler]
    [UsedImplicitly]
    [HarmonyPriority(1)]
    private static IEnumerable<CodeInstruction> LandscapeWriteHolesTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase method)
        => LandscapeWriteTranspilerGeneric(instructions, method, "holeTransactions");

    private static IEnumerable<CodeInstruction> LandscapeWriteTranspilerGeneric(IEnumerable<CodeInstruction> instructions, MethodBase method, string fieldName)
    {
        FieldInfo? transactions = typeof(Landscape).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
        if (transactions == null)
        {
            Logger.LogWarning("Unable to find field: Landscape." + fieldName.Format(false) + " in " + method.Format() + ".");
            DevkitServerModule.Fault();
        }

        List<CodeInstruction> ins = new List<CodeInstruction>(instructions);
        bool ld = false;
        for (int i = 0; i < ins.Count; ++i)
        {
            CodeInstruction c = ins[i];
            if (!ld && c.LoadsField(transactions))
            {
                ld = true;
                for (int j = i + 1; j < ins.Count; ++j)
                {
                    if (ins[j].Branches(out Label? lbl) && lbl.HasValue)
                    {
                        yield return new CodeInstruction(OpCodes.Ldsfld, LandscapeUtil.SaveTransactionsField);
                        yield return new CodeInstruction(OpCodes.Brfalse_S, lbl);
                        Logger.LogDebug("Inserted save transactions check in " + method.Format() + ".");
                        break;
                    }
                }
            }

            yield return c;
        }
        if (!ld)
        {
            Logger.LogWarning("Patching error for " + method.Format() + ". Invalid transpiler operation.");
            DevkitServerModule.Fault();
        }
    }
    #endregion
}
