using DevkitServer.Multiplayer;
using HarmonyLib;
using JetBrains.Annotations;
using System.Reflection;
using System.Text;
using DevkitServer.Configuration;
using DevkitServer.Players;
using SDG.Framework.Landscapes;
#if CLIENT
using System.Reflection.Emit;
using DevkitServer.Multiplayer.LevelData;
using SDG.Provider;
#endif
#if SERVER
using SDG.NetPak;
using System.Reflection.Emit;
using DevkitServer.Multiplayer.Networking;
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
#if DEBUG
            string path = Path.Combine(DevkitServerConfig.Directory, "harmony.log");
            Environment.SetEnvironmentVariable("HARMONY_LOG_FILE", path);
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

            // Accessor.AddFunctionBreakpoints(AccessTools.Method(typeof(ObjectManager), "ReceiveObjects"));
            // ConstructorInfo? info = typeof(MenuConfigurationOptionsUI).GetConstructors(BindingFlags.Instance | BindingFlags.Public).FirstOrDefault();
            // if (info != null)
            //     Accessor.AddFunctionBreakpoints(info);

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

    private static bool IsEditorMode(PlayerCaller caller)
    {
        if (!DevkitServerModule.IsEditing)
            return false;

        EditorUser? user = UserManager.FromId(caller.player.channel.owner.playerID.steamID.m_SteamID);
        return !(user != null && user.Input.Controller == CameraController.Player);
    }
#if CLIENT
    private static readonly InstanceGetter<TempSteamworksWorkshop, List<PublishedFileId_t>> GetServerPendingIDs =
        Accessor.GenerateInstanceGetter<TempSteamworksWorkshop, List<PublishedFileId_t>>("serverPendingIDs", BindingFlags.NonPublic, true)!;

    private static readonly Action<LevelInfo, List<PublishedFileId_t>> ApplyServerAssetMapping =
        Accessor.GenerateStaticCaller<Assets, Action<LevelInfo, List<PublishedFileId_t>>>("ApplyServerAssetMapping", null, true)!;

    //private static readonly Action LoadGameMode = Accessor.GenerateStaticCaller<Provider, Action>("loadGameMode", throwOnError: true)!;

    [HarmonyPatch(typeof(Provider), nameof(Provider.launch))]
    [HarmonyPrefix]
    [UsedImplicitly]
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
            Level.edit(level);
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

    private static readonly MethodInfo IsPlayerControlledOrNotEditingMethod =
        typeof(PatchesMain).GetMethod(nameof(IsPlayerControlledOrNotEditing),
            BindingFlags.Static | BindingFlags.NonPublic)!;

    private static bool IsPlayerControlledOrNotEditing()
    {
        return !DevkitServerModule.IsEditing || !Level.isEditor || EditorUser.User != null && EditorUser.User.Input.Controller == CameraController.Player;
    }
    private static bool IsEditorControlledOrNotEditing()
    {
        return !DevkitServerModule.IsEditing || !Level.isEditor || EditorUser.User != null && EditorUser.User.Input.Controller == CameraController.Editor;
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
        Logger.LogDebug("Setting SteamGameServer KeyValue '" + DevkitServerModule.ServerRule + "' to 'True'.");
        SteamGameServer.SetKeyValue(DevkitServerModule.ServerRule, "True");
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
