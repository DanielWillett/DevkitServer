using DanielWillett.ReflectionTools;
using DanielWillett.ReflectionTools.Emit;
using DevkitServer.API.Abstractions;
using DevkitServer.API.Permissions;
using DevkitServer.API.UI;
using DevkitServer.Core.Permissions;
using DevkitServer.Multiplayer.Levels;
using DevkitServer.Multiplayer.Networking;
using HarmonyLib;
using Pathfinding;
using System.Reflection;
using System.Reflection.Emit;
using DanielWillett.UITools;
using Progress = Pathfinding.Progress;
#if CLIENT
using DevkitServer.API;
using DevkitServer.Core.UI.Extensions;
using DevkitServer.Multiplayer.Actions;
using DevkitServer.Multiplayer.Sync;
#endif
#if SERVER
using Cysharp.Threading.Tasks;
using DevkitServer.Players;
using System.Diagnostics;
using System.Globalization;
#endif

namespace DevkitServer.Patches;
[HarmonyPatch]
[HarmonyPriority(-1)]
internal static class NavigationPatches
{
    [UsedImplicitly]
    private static readonly NetCall<NetId> SendBakeNavRequest = new NetCall<NetId>(DevkitServerNetCall.SendBakeNavRequest);

    [UsedImplicitly]
    private static readonly NetCall<NetId, float, string, bool> SendNavBakeProgressUpdate = new NetCall<NetId, float, string, bool>(DevkitServerNetCall.SendNavBakeProgressUpdate);

#if SERVER
    private static readonly Action? CallListen = Accessor.GenerateStaticCaller<Provider, Action>("listen");
    private static DateTime _lastListen;
    private static Flag? _baking;
    private static bool _hasStartedBakingTiles;
#endif

    private const string Source = "NAV PATCHES";
    internal static int BlockBake;
#if SERVER
    internal static bool CanBake => true;
#else
    internal static bool CanBake => !DevkitServerModule.IsEditing || VanillaPermissions.BakeNavigation.Has(true);
#endif

    private static readonly MethodInfo CurrentCanBakeGetter = typeof(NavigationPatches).GetProperty(nameof(CanBake), BindingFlags.Static | BindingFlags.NonPublic)!.GetMethod;

#if SERVER
    [HarmonyPatch(typeof(Flag), nameof(Flag.bakeNavigation))]
    [HarmonyPrefix]
    [UsedImplicitly]
    private static void OnPreBakingNav(Flag __instance)
    {
        Logger.DevkitServer.LogDebug(Source, $"Baking: {__instance.point.Format()}.");
        _baking = __instance;
        _hasStartedBakingTiles = false;
        if (Provider.clients.Count > 0 && __instance.TryGetIndex(out byte nav) && NavigationNetIdDatabase.TryGetNavigationNetId(nav, out NetId netId))
        {
            SendNavBakeProgressUpdate.Invoke(Provider.GatherClientConnections(), netId, 0f, string.Empty, true);
        }
    }

    [HarmonyPatch(typeof(Flag), nameof(Flag.bakeNavigation))]
    [HarmonyPostfix]
    [UsedImplicitly]
    private static void OnPostBakingNav(Flag __instance)
    {
        Logger.DevkitServer.LogDebug(Source, $"Done baking: {__instance.point.Format()}.");
        if (Provider.clients.Count > 0 && __instance.TryGetIndex(out byte nav) && NavigationNetIdDatabase.TryGetNavigationNetId(nav, out NetId netId))
        {
            SendNavBakeProgressUpdate.Invoke(Provider.GatherClientConnections(), netId, 1f, string.Empty, false);
        }

        if (_baking == __instance)
        {
            _hasStartedBakingTiles = false;
            _baking = null; 
        }
    }
#endif

    [HarmonyPatch(typeof(Flag), nameof(Flag.bakeNavigation))]
    [HarmonyTranspiler]
    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> TranspileBakeNavigation(IEnumerable<CodeInstruction> instructions, MethodBase method, ILGenerator generator)
    {
        MethodInfo? originalMethod = typeof(AstarPath).GetMethod(nameof(AstarPath.Scan),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [ typeof(NavGraph) ],
            null);
        if (originalMethod == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to find method: " +
                                                   FormattingUtil.FormatMethod(typeof(void), typeof(AstarPath), nameof(AstarPath.Scan),
                                                       [ (typeof(NavGraph), "graph") ]) + ".");
        }

        MethodInfo replacementMethod = Accessor.GetMethod(ScanAndListen)!;

        List<CodeInstruction> ins = instructions.ToList();

        bool patched = false;
        Label lbl = generator.DefineLabel();
#if CLIENT
        Label lbl2 = generator.DefineLabel();
#endif
        ins.Insert(0, new CodeInstruction(CurrentCanBakeGetter.GetCallRuntime(), CurrentCanBakeGetter));
        ins.Insert(1, new CodeInstruction(OpCodes.Brtrue, lbl));
        MethodInfo methodInfo = Accessor.GetMethod(OnBakeNavigationWhileAlreadyBaking)!;
        ins.Insert(2, new CodeInstruction(methodInfo.GetCallRuntime(), methodInfo));
        ins.Insert(3, new CodeInstruction(OpCodes.Ret));
#if CLIENT
        ins.Insert(4, new CodeInstruction(OpCodes.Call, AccessorExtensions.IsDevkitServerGetter));
#endif
        ins[4].labels.Add(lbl);
#if CLIENT
        ins.Insert(5, new CodeInstruction(OpCodes.Brfalse, lbl2));
        ins.Insert(6, new CodeInstruction(OpCodes.Ldarg_0));
        ins.Insert(7, new CodeInstruction(OpCodes.Call, Accessor.GetMethod(OnBakeNavigationRequest)));
        ins.Insert(8, new CodeInstruction(OpCodes.Ret));
        ins[9].labels.Add(lbl2);
#endif

        if (originalMethod != null)
        {
            for (int i = 4; i < ins.Count; ++i)
            {
                if (!ins[i].Calls(originalMethod))
                    continue;

                ins[i] = new CodeInstruction(replacementMethod.GetCallRuntime(), replacementMethod);
                patched = true;
            }
        }

        if (!patched)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Failed to add a scan handler to A* scan method.");
        }

        return ins;
    }

    private static void ScanAndListen(AstarPath activePath, RecastGraph graph)
    {
        IEnumerable<Progress> progress = activePath.ScanAsync(graph);
        bool hasListened = false;
        foreach (Progress p in progress)
        {
            OnBakeNavigationProgressUpdate(p);
#if SERVER
            // Makes the server still ping clients while building the nav mesh to keep them from being kicked.
            // I'm basing it on Time.deltaTime to keep anything using it for timing as accurate as possible.

            if (!hasListened)
                _lastListen = DateTime.UtcNow;
            else if ((DateTime.UtcNow - _lastListen).TotalSeconds > CachedTime.DeltaTime && CallListen != null)
                CallListen.Invoke();
#endif
        }
    }

#if SERVER

    [NetCall(NetCallSource.FromClient, DevkitServerNetCall.SendBakeNavRequest)]
    private static void ReceiveBakeNavRequest(MessageContext ctx, NetId netId)
    {
        EditorUser? caller = ctx.GetCaller();
        if (caller == null)
        {
            ctx.Acknowledge(StandardErrorCode.InvalidData);
            return;
        }
        if (!VanillaPermissions.BakeNavigation.Has(caller.SteamId.m_SteamID))
        {
            EditorMessage.SendNoPermissionMessage(caller, VanillaPermissions.BakeNavigation);
            ctx.Acknowledge(StandardErrorCode.NoPermissions);
            return;
        }

        if (!NavigationNetIdDatabase.TryGetNavigation(netId, out byte nav))
        {
            Logger.DevkitServer.LogWarning(Source, $"Unknown navigvation flag NetId: {netId.Format()}.");
            ctx.Acknowledge(StandardErrorCode.NotFound);
            return;
        }

        if (!NavigationUtil.CheckSync(out _))
        {
            Logger.DevkitServer.LogWarning(Source, $"Unable to bake navigation NetId: {netId.Format()}. Not sync authority.");
            ctx.Acknowledge(StandardErrorCode.NotSupported);
            return;
        }

        IReadOnlyList<Flag> list = NavigationUtil.NavigationFlags;

        if (list.Count <= nav)
        {
            Logger.DevkitServer.LogWarning(Source, $"Unknown flag: {netId.Format()}, nav: {nav.Format()}.");
            ctx.Acknowledge(StandardErrorCode.NotFound);
            return;
        }

        int old = Interlocked.CompareExchange(ref BlockBake, nav + 1, 0);
        if (old != 0)
        {
            ctx.Acknowledge(StandardErrorCode.AccessViolation);

            if (old > 0 && NavigationUtil.TryGetFlag((byte)(old - 1), out Flag oldFlag))
            {
                string? navName = HierarchyUtil.GetNearestNode<LocationDevkitNode>(oldFlag.point)?.locationName;
                if (navName != null)
                {
                    EditorMessage.SendEditorMessage(caller, TranslationSource.DevkitServerMessageLocalizationSource, "AlreadyBakingNavigationName", [ navName, (byte)(old - 1) ]);
                    return;
                }
            }

            EditorMessage.SendEditorMessage(caller, TranslationSource.DevkitServerMessageLocalizationSource, "AlreadyBakingNavigationIndex", [ (byte)(old - 1) ]);
            return;
        }

        Flag flag = list[nav];

        UniTask.Create(async () =>
        {
            // to keep the lock statement in the message reader from holding throughout the entire frozen frame (could cause a deadlock).
            await UniTask.NextFrame();

            try
            {
                Logger.DevkitServer.LogInfo(Source, $"Baking navigation: {netId.Format()}...");
                Stopwatch sw = Stopwatch.StartNew();

                flag.bakeNavigation();

                sw.Stop();
                Logger.DevkitServer.LogInfo(Source, $"Done baking navigation {netId.Format()}, baking took {sw.GetElapsedMilliseconds():F2} ms.");
            }
            finally
            {
                if (Interlocked.CompareExchange(ref BlockBake, 0, nav + 1) != nav + 1)
                {
                    Logger.DevkitServer.LogWarning(Source, $"Synchronization fault when syncing navigation flag {nav.Format()}.");
                }
            }

            ctx.Acknowledge(StandardErrorCode.Success);

            NavigationUtil.SyncGraphIfAuthority(netId);
        });
    }

#endif

    private static void OnBakeNavigationProgressUpdate(Progress progress)
    {
        string description = progress.ToString();
        Logger.DevkitServer.LogInfo(Source, $"[A* PATHFINDING] ({progress.progress.Format("P")}) {description.Colorize(ConsoleColor.Gray)}.");
#if SERVER

        // remove "'XX% ' ..." prefix
        string descriptionWithoutPercent;
        int pInd = description.IndexOf('%');
        if (pInd >= 0 && pInd < description.Length - 1 && description[pInd + 1] == ' ')
            ++pInd;
        if (pInd >= 0 && pInd < description.Length - 1)
            descriptionWithoutPercent = description[(pInd + 1)..];
        else descriptionWithoutPercent = description;

        if (Provider.clients.Count > 0 && _baking != null && _baking.TryGetIndex(out byte nav) && NavigationNetIdDatabase.TryGetNavigationNetId(nav, out NetId netId))
        {
            float progressPercentage;
            if (descriptionWithoutPercent.StartsWith("Scanning graph ", StringComparison.Ordinal))
            {
                _hasStartedBakingTiles = true;
                int i1 = descriptionWithoutPercent.IndexOf(" of ", 15, StringComparison.OrdinalIgnoreCase);
                if (i1 == -1 ||
                    !int.TryParse(descriptionWithoutPercent.AsSpan(15, i1 - 15), NumberStyles.Number, CultureInfo.InvariantCulture, out int partialSmall) ||
                    !int.TryParse(descriptionWithoutPercent.AsSpan(i1 + 4), NumberStyles.Number, CultureInfo.InvariantCulture, out int partialLarge))
                    progressPercentage = 0.5f;
                else
                    progressPercentage = ((float)partialSmall / partialLarge) * 0.8f + 0.1f;
            }
            else if (_hasStartedBakingTiles)
                progressPercentage = 0.9f + progress.progress / 10f;
            else
                progressPercentage = progress.progress / 10f;
            SendNavBakeProgressUpdate.Invoke(Provider.GatherClientConnections(), netId, progressPercentage, description, true);
        }
#endif
    }
#if CLIENT
    [NetCall(NetCallSource.FromServer, DevkitServerNetCall.SendNavBakeProgressUpdate)]
    private static void ReceiveNavigationBakeProgress(MessageContext ctx, NetId netId, float progress, string desc, bool isActive)
    {
        string? name = null;

        if (NavigationNetIdDatabase.TryGetNavigation(netId, out byte nav) && NavigationUtil.TryGetFlag(nav, out Flag flag))
            name = HierarchyUtil.GetNearestNode<LocationDevkitNode>(flag.point)?.locationName;
        
        if (!isActive)
        {
            if (name != null)
                Logger.DevkitServer.LogInfo(Source, $"[SERVER / A* PATHFINDING] [{name.Format()} # {nav.Format()}] {"Done baking navigation".Colorize(ConsoleColor.Gray)}.");
            else
                Logger.DevkitServer.LogInfo(Source, $"[SERVER / A* PATHFINDING] [{netId.Format()}] {"Done baking navigation".Colorize(ConsoleColor.Gray)}.");
        }
        else if (name != null)
            Logger.DevkitServer.LogInfo(Source, $"[SERVER / A* PATHFINDING] [{name.Format()} # {nav.Format()}] {progress.Format("P")} - {desc.Colorize(ConsoleColor.Gray)}.");
        else
            Logger.DevkitServer.LogInfo(Source, $"[SERVER / A* PATHFINDING] [{netId.Format()}] {progress.Format("P")} - {desc.Colorize(ConsoleColor.Gray)}.");

        EditorUIExtension? editorUi = UnturnedUIToolsNexus.UIExtensionManager.GetInstance<EditorUIExtension>();
        if (editorUi != null)
        {
            if (!string.IsNullOrEmpty(desc))
                editorUi.UpdateLoadingBarDescription(desc);

            editorUi.UpdateLoadingBarProgress(progress);
            if (!isActive)
            {
                if (NavigationSync.Authority == null)
                {
                    editorUi.UpdateLoadingBarVisibility(false);
                    return;
                }

                NavigationSync.Authority.StartWaitingToUpdateLoadingBar(editorUi, netId);
            }
            editorUi.UpdateLoadingBarVisibility(true);
        }
    }
    private static void OnBakeNavigationRequest(Flag flag)
    {
        DevkitServerModule.AssertIsDevkitServerClient();

        IReadOnlyList<Flag> list = NavigationUtil.NavigationFlags;
        int index = -1;
        for (int i = 0; i < list.Count; ++i)
        {
            if (list[i] == flag)
            {
                index = i;
                break;
            }
        }

        byte nav = unchecked((byte)index);
        if (NavigationNetIdDatabase.TryGetNavigationNetId(nav, out NetId netId))
            SendBakeNavRequest.Invoke(netId);
        else
        {
            Logger.DevkitServer.LogWarning(Source, $"Unable to find NetId for navigation flag: {nav.Format()}.");
        }
    }
#endif
    private static void OnBakeNavigationWhileAlreadyBaking()
    {
        Logger.DevkitServer.LogWarning(Source, BlockBake == 0 ? "You do not have permission to bake navigation." : "Tried to bake navigation while it's already baking.");
#if CLIENT
        int old = BlockBake;
        if (old > 0 && NavigationUtil.TryGetFlag((byte)(old - 1), out Flag oldFlag))
        {
            string? navName = HierarchyUtil.GetNearestNode<LocationDevkitNode>(oldFlag.point)?.locationName;
            if (navName != null)
            {
                EditorMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource, "AlreadyBakingNavigationName", new object[] { navName, (byte)(old - 1) });
                return;
            }
        }

        if (old <= 0)
            EditorMessage.SendNoPermissionMessage(VanillaPermissions.BakeNavigation);
        else
            EditorMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource, "AlreadyBakingNavigationIndex", new object[] { (byte)(old - 1) });
#endif
    }
#if CLIENT

    private static bool _changing;
    private static void Change(Action action)
    {
        _changing = true;
        try
        {
            action();
        }
        finally
        {
            _changing = false;
        }
    }
    internal static void OptionalPatches()
    {
        MethodInfo? method = typeof(EditorEnvironmentNavigationUI).GetMethod("onDraggedWidthSlider", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        if (method == null)
        {
            Logger.DevkitServer.LogWarning(Source, "Unable to find method: EditorEnvironmentNavigationUI.onDraggedWidthSlider.");
        }
        else
        {
            try
            {
                PatchesMain.Patcher.Patch(method, prefix: new HarmonyMethod(Accessor.GetMethod(OnChangeWidth)!));
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogError(Source, ex, "Error patching EditorEnvironmentNavigationUI.onDraggedWidthSlider.");
            }
        }
        method = typeof(EditorEnvironmentNavigationUI).GetMethod("onDraggedHeightSlider", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        if (method == null)
        {
            Logger.DevkitServer.LogWarning(Source, "Unable to find method: EditorEnvironmentNavigationUI.onDraggedHeightSlider.");
        }
        else
        {
            try
            {
                PatchesMain.Patcher.Patch(method, prefix: new HarmonyMethod(Accessor.GetMethod(OnChangeHeight)!));
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogError(Source, ex, "Error patching EditorEnvironmentNavigationUI.onDraggedHeightSlider.");
            }
        }
        method = typeof(EditorEnvironmentNavigationUI).GetMethod("onDifficultyGUIDFieldTyped", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        if (method == null)
        {
            Logger.DevkitServer.LogWarning(Source, "Unable to find method: EditorEnvironmentNavigationUI.onDifficultyGUIDFieldTyped.");
        }
        else
        {
            try
            {
                PatchesMain.Patcher.Patch(method, prefix: new HarmonyMethod(Accessor.GetMethod(OnChangeDifficulty)!));
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogError(Source, ex, "Error patching EditorEnvironmentNavigationUI.onDifficultyGUIDFieldTyped.");
            }
        }
        method = typeof(EditorEnvironmentNavigationUI).GetMethod("onMaxZombiesFieldTyped", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        if (method == null)
        {
            Logger.DevkitServer.LogWarning(Source, "Unable to find method: EditorEnvironmentNavigationUI.onMaxZombiesFieldTyped.");
        }
        else
        {
            try
            {
                PatchesMain.Patcher.Patch(method, prefix: new HarmonyMethod(Accessor.GetMethod(OnChangeMaxZombies)!));
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogError(Source, ex, "Error patching EditorEnvironmentNavigationUI.onMaxZombiesFieldTyped.");
            }
        }
        method = typeof(EditorEnvironmentNavigationUI).GetMethod("onMaxBossZombiesFieldTyped", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        if (method == null)
        {
            Logger.DevkitServer.LogWarning(Source, "Unable to find method: EditorEnvironmentNavigationUI.onMaxBossZombiesFieldTyped.");
        }
        else
        {
            try
            {
                PatchesMain.Patcher.Patch(method, prefix: new HarmonyMethod(Accessor.GetMethod(OnChangeMaxBossZombies)!));
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogError(Source, ex, "Error patching EditorEnvironmentNavigationUI.onMaxBossZombiesFieldTyped.");
            }
        }
        method = typeof(EditorEnvironmentNavigationUI).GetMethod("onToggledSpawnZombiesToggle", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        if (method == null)
        {
            Logger.DevkitServer.LogWarning(Source, "Unable to find method: EditorEnvironmentNavigationUI.onToggledSpawnZombiesToggle.");
        }
        else
        {
            try
            {
                PatchesMain.Patcher.Patch(method, prefix: new HarmonyMethod(Accessor.GetMethod(OnChangeShouldSpawnZombies)!));
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogError(Source, ex, "Error patching EditorEnvironmentNavigationUI.onToggledSpawnZombiesToggle.");
            }
        }
        method = typeof(EditorEnvironmentNavigationUI).GetMethod("onToggledHyperAgroToggle", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        if (method == null)
        {
            Logger.DevkitServer.LogWarning(Source, "Unable to find method: EditorEnvironmentNavigationUI.onToggledHyperAgroToggle.");
        }
        else
        {
            try
            {
                PatchesMain.Patcher.Patch(method, prefix: new HarmonyMethod(Accessor.GetMethod(OnChangeInfiniteAgroDistance)!));
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogError(Source, ex, "Error patching EditorEnvironmentNavigationUI.onToggledHyperAgroToggle.");
            }
        }
    }

    [UsedImplicitly]
    private static bool OnChangeWidth(ISleekSlider slider, float state)
    {
        Flag? flag = EditorNavigation.flag;
        if (_changing || flag == null)
            return false;

        float oldWidth = flag.width;
        if (DevkitServerModule.IsEditing && !CanEditFlags())
        {
            Change(() => slider.Value = oldWidth);
            return false;
        }

        if (oldWidth == state)
            return false;

        int nav = flag.GetIndex();
        if (nav < 0)
            return false;

        Vector2 size = new Vector2(state, flag.height);

        SetNavigationSizeProperties properties = new SetNavigationSizeProperties(GetNetIdOrInvalid((byte)nav), size, new Vector2(oldWidth, flag.height), CachedTime.DeltaTime);
        if (DevkitServerModule.IsEditing && properties.NavigationNetId.id != 0)
        {
            if (ClientEvents.ListeningOnSetNavigationSizeRequested)
            {
                bool shouldAllow = true;
                ClientEvents.InvokeOnSetNavigationSizeRequested(in properties, ref shouldAllow);
                if (!shouldAllow)
                {
                    Change(() => slider.Value = oldWidth);
                    return false;
                }
            }
        }

        NavigationUtil.SetFlagSizeLocal((byte)nav, size);
        if (DevkitServerModule.IsEditing && properties.NavigationNetId.id != 0)
            ClientEvents.InvokeOnSetNavigationSize(in properties);
        return false;
    }

    [UsedImplicitly]
    private static bool OnChangeHeight(ISleekSlider slider, float state)
    {
        Flag? flag = EditorNavigation.flag;
        if (_changing || flag == null)
            return false;

        float oldHeight = flag.height;
        if (DevkitServerModule.IsEditing && !CanEditFlags())
        {
            Change(() => slider.Value = oldHeight);
            return false;
        }

        if (oldHeight == state)
            return false;

        int nav = flag.GetIndex();
        if (nav < 0)
            return false;

        Vector2 size = new Vector2(flag.width, state);

        SetNavigationSizeProperties properties = new SetNavigationSizeProperties(GetNetIdOrInvalid((byte)nav), size, new Vector2(flag.width, oldHeight), CachedTime.DeltaTime);
        if (DevkitServerModule.IsEditing && properties.NavigationNetId.id != 0)
        {
            if (ClientEvents.ListeningOnSetNavigationSizeRequested)
            {
                bool shouldAllow = true;
                ClientEvents.InvokeOnSetNavigationSizeRequested(in properties, ref shouldAllow);
                if (!shouldAllow)
                {
                    Change(() => slider.Value = oldHeight);
                    return false;
                }
            }
        }

        NavigationUtil.SetFlagSizeLocal((byte)nav, size);
        if (DevkitServerModule.IsEditing && properties.NavigationNetId.id != 0)
            ClientEvents.InvokeOnSetNavigationSize(in properties);
        return false;
    }

    [UsedImplicitly]
    private static bool OnChangeDifficulty(ISleekField field, string state)
    {
        Flag? flag = EditorNavigation.flag;
        if (_changing || flag == null)
            return false;

        if (DevkitServerModule.IsEditing && !CanEditFlags())
        {
            Change(() => field.Text = flag.data.difficultyGUID);
            return false;
        }

        state = state.Trim();

        if (!Guid.TryParse(state, out Guid guid) && flag.data.difficulty.GUID == Guid.Empty)
        {
            if (state.Length >= 32)
                Change(() => field.Text = flag.data.difficultyGUID);
            return false;
        }

        if (flag.data.difficulty.GUID == guid)
            return false;

        AssetReference<ZombieDifficultyAsset> oldDifficulty = flag.data.difficulty;

        int nav = flag.GetIndex();
        if (nav < 0)
            return false;

        AssetReference<ZombieDifficultyAsset> difficulty = new AssetReference<ZombieDifficultyAsset>(guid);

        SetNavigationDifficultyProperties properties = new SetNavigationDifficultyProperties(GetNetIdOrInvalid((byte)nav), difficulty, oldDifficulty, CachedTime.DeltaTime);
        if (DevkitServerModule.IsEditing && properties.NavigationNetId.id != 0)
        {
            if (ClientEvents.ListeningOnSetNavigationDifficultyRequested)
            {
                bool shouldAllow = true;
                ClientEvents.InvokeOnSetNavigationDifficultyRequested(in properties, ref shouldAllow);
                if (!shouldAllow)
                {
                    Change(() => field.Text = flag.data.difficultyGUID);
                    return false;
                }
            }
        }

        NavigationUtil.SetFlagDifficultyLocal((byte)nav, difficulty);
        if (DevkitServerModule.IsEditing && properties.NavigationNetId.id != 0)
            ClientEvents.InvokeOnSetNavigationDifficulty(in properties);
        return false;
    }
    
    [UsedImplicitly]
    private static bool OnChangeMaxZombies(ISleekUInt8Field field, byte state)
    {
        Flag? flag = EditorNavigation.flag;
        if (_changing || flag == null)
            return false;

        byte oldMaxZombies = flag.data.maxZombies;
        if (DevkitServerModule.IsEditing && !CanEditFlags())
        {
            Change(() => field.Value = oldMaxZombies);
            return false;
        }

        if (oldMaxZombies == state)
            return false;

        int nav = flag.GetIndex();
        if (nav < 0)
            return false;

        SetNavigationMaximumZombiesProperties properties = new SetNavigationMaximumZombiesProperties(GetNetIdOrInvalid((byte)nav), state, oldMaxZombies, CachedTime.DeltaTime);
        if (DevkitServerModule.IsEditing && properties.NavigationNetId.id != 0)
        {
            if (ClientEvents.ListeningOnSetNavigationMaximumZombiesRequested)
            {
                bool shouldAllow = true;
                ClientEvents.InvokeOnSetNavigationMaximumZombiesRequested(in properties, ref shouldAllow);
                if (!shouldAllow)
                {
                    Change(() => field.Value = oldMaxZombies);
                    return false;
                }
            }
        }

        NavigationUtil.SetFlagMaximumZombiesLocal((byte)nav, state);
        if (DevkitServerModule.IsEditing && properties.NavigationNetId.id != 0)
            ClientEvents.InvokeOnSetNavigationMaximumZombies(in properties);
        return false;
    }

    [UsedImplicitly]
    private static bool OnChangeMaxBossZombies(ISleekInt32Field field, int state)
    {
        Flag? flag = EditorNavigation.flag;
        if (_changing || flag == null)
            return false;

        int oldMaxBossZombies = flag.data.maxBossZombies;
        if (DevkitServerModule.IsEditing && !CanEditFlags())
        {
            Change(() => field.Value = oldMaxBossZombies);
            return false;
        }

        if (oldMaxBossZombies == state)
            return false;

        int nav = flag.GetIndex();
        if (nav < 0)
            return false;

        SetNavigationMaximumBossZombiesProperties properties = new SetNavigationMaximumBossZombiesProperties(GetNetIdOrInvalid((byte)nav), state, oldMaxBossZombies, CachedTime.DeltaTime);
        if (DevkitServerModule.IsEditing && properties.NavigationNetId.id != 0)
        {
            if (ClientEvents.ListeningOnSetNavigationMaximumBossZombiesRequested)
            {
                bool shouldAllow = true;
                ClientEvents.InvokeOnSetNavigationMaximumBossZombiesRequested(in properties, ref shouldAllow);
                if (!shouldAllow)
                {
                    Change(() => field.Value = oldMaxBossZombies);
                    return false;
                }
            }
        }

        NavigationUtil.SetFlagMaximumBossZombiesLocal((byte)nav, state);
        if (DevkitServerModule.IsEditing && properties.NavigationNetId.id != 0)
            ClientEvents.InvokeOnSetNavigationMaximumBossZombies(in properties);
        return false;
    }

    [UsedImplicitly]
    private static bool OnChangeShouldSpawnZombies(ISleekToggle toggle, bool state)
    {
        Flag? flag = EditorNavigation.flag;
        if (_changing || flag == null)
            return false;

        if (DevkitServerModule.IsEditing && !CanEditFlags())
        {
            Change(() => toggle.Value = !state);
            return false;
        }

        if (flag.data.spawnZombies == state)
            return false;

        int nav = flag.GetIndex();
        if (nav < 0)
            return false;

        SetNavigationShouldSpawnZombiesProperties properties = new SetNavigationShouldSpawnZombiesProperties(GetNetIdOrInvalid((byte)nav), state, CachedTime.DeltaTime);
        if (DevkitServerModule.IsEditing && properties.NavigationNetId.id != 0)
        {
            if (ClientEvents.ListeningOnSetNavigationShouldSpawnZombiesRequested)
            {
                bool shouldAllow = true;
                ClientEvents.InvokeOnSetNavigationShouldSpawnZombiesRequested(in properties, ref shouldAllow);
                if (!shouldAllow)
                {
                    Change(() => toggle.Value = !state);
                    return false;
                }
            }
        }

        NavigationUtil.SetFlagShouldSpawnZombiesLocal((byte)nav, state);
        if (DevkitServerModule.IsEditing && properties.NavigationNetId.id != 0)
            ClientEvents.InvokeOnSetNavigationShouldSpawnZombies(in properties);
        return false;
    }

    [UsedImplicitly]
    private static bool OnChangeInfiniteAgroDistance(ISleekToggle toggle, bool state)
    {
        Flag? flag = EditorNavigation.flag;
        if (_changing || flag == null)
            return false;

        if (DevkitServerModule.IsEditing && !CanEditFlags())
        {
            Change(() => toggle.Value = !state);
            return false;
        }

        if (flag.data.hyperAgro == state)
            return false;

        int nav = flag.GetIndex();
        if (nav < 0)
            return false;

        SetNavigationInfiniteAgroDistanceProperties properties = new SetNavigationInfiniteAgroDistanceProperties(GetNetIdOrInvalid((byte)nav), state, CachedTime.DeltaTime);
        if (DevkitServerModule.IsEditing && properties.NavigationNetId.id != 0)
        {
            if (ClientEvents.ListeningOnSetNavigationInfiniteAgroDistanceRequested)
            {
                bool shouldAllow = true;
                ClientEvents.InvokeOnSetNavigationInfiniteAgroDistanceRequested(in properties, ref shouldAllow);
                if (!shouldAllow)
                {
                    Change(() => toggle.Value = !state);
                    return false;
                }
            }
        }

        NavigationUtil.SetFlagInfiniteAgroDistanceLocal((byte)nav, state);
        if (DevkitServerModule.IsEditing && properties.NavigationNetId.id != 0)
            ClientEvents.InvokeOnSetNavigationInfiniteAgroDistance(in properties);
        return false;
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(EditorNavigation), "Update")]
    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> TranspileEditorNavigationUpdate(IEnumerable<CodeInstruction> instructions, MethodBase method, ILGenerator generator)
    {
        List<CodeInstruction> ins = [..instructions];

        MethodInfo? select = typeof(EditorNavigation).GetMethod("select", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(Transform) }, null);
        if (select == null)
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to find method: EditorNavigation.select.");

        MethodInfo? removeFlag = typeof(LevelNavigation).GetMethod("removeFlag", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(Transform) }, null);
        if (removeFlag == null)
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to find method: LevelNavigation.removeFlag.");

        MethodInfo? addFlag = typeof(LevelNavigation).GetMethod("addFlag", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(Vector3) }, null);
        if (addFlag == null)
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to find method: LevelNavigation.addFlag.");

        MethodInfo? moveFlag = typeof(Flag).GetMethod("move", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(Vector3) }, null);
        if (moveFlag == null)
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to find method: LevelNavigation.move.");

        bool remove = false, move = false, add = false;

        for (int i = 0; i < ins.Count; ++i)
        {
            if (!remove && select != null && removeFlag != null && PatchUtility.MatchPattern(ins, i,
                    x => x.opcode.IsOfType(OpCodes.Ldfld, fuzzy: true),
                    x => x.opcode.IsLdc(@null: true),
                    x => x.Calls(select),
                    x => x.Calls(removeFlag)
                    ))
            {
                ins[i + 1] = new CodeInstruction(OpCodes.Call, Accessor.GetMethod(RemoveFlag)).WithEndBlocksFrom(ins[i + 3]);
                ins.RemoveRange(i + 2, 2);
                remove = true;
                i -= 2;
            }
            else if (!remove && select != null && removeFlag != null && PatchUtility.MatchPattern(ins, i,
                    x => x.opcode.IsLdc(@null: true),
                    x => x.Calls(select),
                    x => x.opcode.IsOfType(OpCodes.Ldfld, fuzzy: true),
                    x => x.Calls(removeFlag)
                    ))
            {
                ins[i + 3] = new CodeInstruction(OpCodes.Call, Accessor.GetMethod(RemoveFlag)).WithEndBlocksFrom(ins[i + 3]);
                ins.RemoveRange(i, 2);
                remove = true;
                i -= 2;
            }
            else if (!remove && PatchUtility.MatchPattern(ins, i, x => x.Calls(removeFlag)))
            {
                ins[i] = new CodeInstruction(OpCodes.Call, Accessor.GetMethod(RemoveFlag)).WithEndBlocksFrom(ins[i]);
                remove = true;
            }
            
            if (!move && moveFlag != null && PatchUtility.MatchPattern(ins, i, x => x.Calls(moveFlag)))
            {
                ins[i] = new CodeInstruction(OpCodes.Call, Accessor.GetMethod(MoveFlag)).WithEndBlocksFrom(ins[i]);
                move = true;
            }
            
            if (!add && addFlag != null && PatchUtility.MatchPattern(ins, i, x => x.Calls(addFlag)))
            {
                ins[i] = new CodeInstruction(OpCodes.Call, Accessor.GetMethod(AddFlag)).WithEndBlocksFrom(ins[i]);
                add = true;
            }
        }

        if (!remove)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to patch {removeFlag.Format()} call.");
        }
        if (!add)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to patch {addFlag.Format()} call.");
        }
        if (!move)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to patch {moveFlag.Format()} call.");
        }

        return ins;
    }

    [UsedImplicitly]
    private static void RemoveFlag(Transform select)
    {
        if (DevkitServerModule.IsEditing && !CanEditFlags())
            return;

        Flag flag = LevelNavigation.getFlag(select);
        int nav = flag.GetIndex();
        if (nav < 0)
            return;
        
        DeleteNavigationProperties properties = new DeleteNavigationProperties(GetNetIdOrInvalid((byte)nav), flag.point, CachedTime.DeltaTime);
        if (DevkitServerModule.IsEditing && properties.NavigationNetId.id != 0)
        {
            if (ClientEvents.ListeningOnDeleteNavigationRequested)
            {
                bool shouldAllow = true;
                ClientEvents.InvokeOnDeleteNavigationRequested(in properties, ref shouldAllow);
                if (!shouldAllow)
                    return;
            }
        }

        NavigationUtil.RemoveFlagLocal((byte)nav);

        if (DevkitServerModule.IsEditing && properties.NavigationNetId.id != 0)
            ClientEvents.InvokeOnDeleteNavigation(in properties);
    }

    [UsedImplicitly]
    private static void MoveFlag(Flag flag, Vector3 newPoint)
    {
        if (DevkitServerModule.IsEditing && !CanEditFlags())
            return;
        
        int nav = flag.GetIndex();
        if (nav < 0)
            return;
        
        MoveNavigationProperties properties = new MoveNavigationProperties(GetNetIdOrInvalid((byte)nav), newPoint, flag.point, CachedTime.DeltaTime);
        if (DevkitServerModule.IsEditing && properties.NavigationNetId.id != 0)
        {
            if (ClientEvents.ListeningOnMoveNavigationRequested)
            {
                bool shouldAllow = true;
                ClientEvents.InvokeOnMoveNavigationRequested(in properties, ref shouldAllow);
                if (!shouldAllow)
                    return;
            }
        }

        NavigationUtil.SetFlagPositionLocal((byte)nav, newPoint);

        if (DevkitServerModule.IsEditing && properties.NavigationNetId.id != 0)
            ClientEvents.InvokeOnMoveNavigation(in properties);
    }

    [UsedImplicitly]
    private static Transform? AddFlag(Vector3 point)
    {
        if (NavigationUtil.NavigationFlags.Count >= byte.MaxValue - 1)
        {
            EditorMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource, "TooManyNavigationFlags", new object[] { (byte)(byte.MaxValue - 1) });
            return null!;
        }

        if (DevkitServerModule.IsEditing)
        {
            if (!CanEditFlags())
                return null;

            RequestInstantiateNavigationProperties properties = new RequestInstantiateNavigationProperties(point);
            if (ClientEvents.ListeningOnRequestInstantiateNavigationRequested)
            {
                bool shouldAllow = true;
                ClientEvents.InvokeOnRequestInstantiateNavigationRequested(in properties, ref shouldAllow);
                if (!shouldAllow)
                    return null;
            }

            NavigationUtil.RequestFlagInstantiation(point);

            ClientEvents.EventOnRequestInstantiateNavigation.TryInvoke(in properties);

            return null;
        }

        return NavigationUtil.AddFlagLocal(point).model;
    }
    
    private static bool CanEditFlags()
    {
        if (!VanillaPermissions.EditNavigation.Has())
        {
            EditorMessage.SendNoPermissionMessage(VanillaPermissions.EditNavigation);
            return false;
        }

        return true;
    }

    private static NetId GetNetIdOrInvalid(byte nav)
    {
        if (!DevkitServerModule.IsEditing)
            return NetId.INVALID;
        if (!NavigationNetIdDatabase.TryGetNavigationNetId(nav, out NetId netId))
        {
            Logger.DevkitServer.LogWarning(Source, $"Unable to find NetId for flag: {nav.Format()}.");
            return NetId.INVALID;
        }

        return netId;
    }
#endif
}