using DevkitServer.API.Permissions;
using DevkitServer.API.UI;
using DevkitServer.Multiplayer.Networking;
using HarmonyLib;
using Pathfinding;
using System.Reflection;
using System.Reflection.Emit;
using Progress = Pathfinding.Progress;
using DevkitServer.API;

#if CLIENT
using DevkitServer.API.Abstractions;
using DevkitServer.API.UI.Extensions;
using DevkitServer.Core.Permissions;
using DevkitServer.Core.UI.Extensions;
using DevkitServer.Multiplayer.Levels;
using DevkitServer.Multiplayer.Sync;
#endif
#if SERVER
using Cysharp.Threading.Tasks;
using DevkitServer.API.Abstractions;
using DevkitServer.Core.Permissions;
using DevkitServer.Multiplayer.Levels;
using DevkitServer.Players;
using Pathfinding.Voxels;
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
    internal static bool CanBake => !DevkitServerModule.IsEditing || (VanillaPermissions.AllNavigation.Has() || VanillaPermissions.BakeNavigation.Has(false));
#endif

    private static readonly MethodInfo CurrentCanBakeGetter = typeof(NavigationPatches).GetProperty(nameof(CanBake), BindingFlags.Static | BindingFlags.NonPublic)!.GetMethod;

#if SERVER
    [HarmonyPatch(typeof(Flag), nameof(Flag.bakeNavigation))]
    [HarmonyPrefix]
    [UsedImplicitly]
    private static void OnPreBakingNav(Flag __instance)
    {
        Logger.LogDebug($"[{Source}] Baking: {__instance.point.Format()}.");
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
        Logger.LogDebug($"[{Source}] Done baking: {__instance.point.Format()}.");
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
        MethodInfo? originalMethod = typeof(AstarPath).GetMethod(nameof(AstarPath.ScanSpecific),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(NavGraph) },
            null);
        if (originalMethod == null)
        {
            Logger.LogWarning($"{method.Format()} - Unable to find method: " +
                FormattingUtil.FormatMethod(typeof(void), typeof(AstarPath), nameof(AstarPath.ScanSpecific),
                    new (Type type, string? name)[] { (typeof(NavGraph), "graph") }) + ".", method: Source);
        }

        MethodInfo? replacementMethod = typeof(AstarPath).GetMethod(nameof(AstarPath.ScanSpecific),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(NavGraph), typeof(OnScanStatus) },
            null);
        if (replacementMethod == null)
        {
            Logger.LogWarning($"{method.Format()} - Unable to find method: " +
                FormattingUtil.FormatMethod(typeof(void), typeof(AstarPath), nameof(AstarPath.ScanSpecific),
                    new (Type type, string? name)[] { (typeof(NavGraph), "graph"), (typeof(OnScanStatus), "statusCallback") }) + ".", method: Source);
        }

        ConstructorInfo? delegateConstructor = typeof(OnScanStatus)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(x => x.GetParameters() is { Length: 2 } p && !p[0].ParameterType.IsValueType && p[1].ParameterType == typeof(nint));
        if (delegateConstructor == null)
        {
            Logger.LogWarning($"{method.Format()} - Unable to find constructor for {typeof(OnScanStatus).Format()}.", method: Source);
        }

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
        ins.Insert(4, new CodeInstruction(OpCodes.Call, Accessor.IsDevkitServerGetter));
#endif
        ins[4].labels.Add(lbl);
#if CLIENT
        ins.Insert(5, new CodeInstruction(OpCodes.Brfalse, lbl2));
        ins.Insert(6, new CodeInstruction(OpCodes.Ldarg_0));
        ins.Insert(7, new CodeInstruction(OpCodes.Call, Accessor.GetMethod(OnBakeNavigationRequest)));
        ins.Insert(8, new CodeInstruction(OpCodes.Ret));
        ins[9].labels.Add(lbl2);
#endif

        if (replacementMethod != null && originalMethod != null && delegateConstructor != null)
        {
            for (int i = 4; i < ins.Count; ++i)
            {
                if (!ins[i].Calls(originalMethod))
                    continue;

                ins.Insert(i, new CodeInstruction(OpCodes.Ldnull).MoveLabelsFrom(ins[i]));
                ins.Insert(i + 1, new CodeInstruction(OpCodes.Ldftn, Accessor.GetMethod(OnBakeNavigationProgressUpdate)));
                ins.Insert(i + 2, new CodeInstruction(OpCodes.Newobj, delegateConstructor));
                ins[i + 3] = new CodeInstruction(replacementMethod.GetCallRuntime(), replacementMethod);
                patched = true;
            }
        }

        if (!patched)
        {
            Logger.LogWarning($"{method.Format()} - Failed to add a scan handler to A* scan method.", method: Source);
        }

        return ins;
    }

#if SERVER

    // Makes the server still ping clients while building the nav mesh to keep them from being kicked.
    // I'm basing it on Time.deltaTime to keep anything using it for timing as accurate as possible.

    [HarmonyPatch(typeof(RecastGraph), "BuildTileMesh")]
    [HarmonyPostfix]
    [UsedImplicitly]
    private static void BuildTileMeshPostfix(Voxelize vox, int x, int z)
    {
        if (x == 0 && z == 0)
            _lastListen = DateTime.UtcNow;
        else if ((DateTime.UtcNow - _lastListen).TotalSeconds > CachedTime.DeltaTime && CallListen != null)
            CallListen.Invoke();
    }

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
            Logger.LogWarning($"Unknown navigvation flag NetId: {netId.Format()}.", method: Source);
            ctx.Acknowledge(StandardErrorCode.NotFound);
            return;
        }

        if (!NavigationUtil.CheckSync(out _))
        {
            Logger.LogWarning($"Unable to bake navigation NetId: {netId.Format()}. Not sync authority.", method: Source);
            ctx.Acknowledge(StandardErrorCode.NotSupported);
            return;
        }

        IReadOnlyList<Flag> list = NavigationUtil.NavigationFlags;

        if (list.Count <= nav)
        {
            Logger.LogWarning($"Unknown flag: {netId.Format()}, nav: {nav.Format()}.", method: Source);
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
                    EditorMessage.SendEditorMessage(caller, TranslationSource.DevkitServerMessageLocalizationSource, "AlreadyBakingNavigationName", new object[] { navName, (byte)(old - 1) });
                    return;
                }
            }

            EditorMessage.SendEditorMessage(caller, TranslationSource.DevkitServerMessageLocalizationSource, "AlreadyBakingNavigationIndex", new object[] { (byte)(old - 1) });
            return;
        }

        Flag flag = list[nav];

        UniTask.Create(async () =>
        {
            // to keep the lock statement in the message reader from holding throughout the entire frozen frame (could cause a deadlock).
            await UniTask.NextFrame();

            try
            {
                Logger.LogInfo($"[{Source}] Baking navigation: {netId.Format()}...");
                Stopwatch sw = Stopwatch.StartNew();

                flag.bakeNavigation();

                sw.Stop();
                Logger.LogInfo($"[{Source}] Done baking navigation {netId.Format()}, baking took {sw.GetElapsedMilliseconds():F2} ms.");
            }
            finally
            {
                if (Interlocked.CompareExchange(ref BlockBake, 0, nav + 1) != nav + 1)
                {
                    Logger.LogWarning($"Synchronization fault when syncing navigation flag {nav.Format()}.", method: Source);
                }
            }

            ctx.Acknowledge(StandardErrorCode.Success);

            NavigationUtil.SyncIfAuthority(netId);
        });
    }

#endif

    private static void OnBakeNavigationProgressUpdate(Progress progress)
    {
        Logger.LogInfo($"[{Source}] [A* PATHFINDING] (" + progress.progress.Format("P") + ") " + progress.description.Colorize(ConsoleColor.Gray) + ".");
#if SERVER       
        if (Provider.clients.Count > 0 && _baking != null && _baking.TryGetIndex(out byte nav) && NavigationNetIdDatabase.TryGetNavigationNetId(nav, out NetId netId))
        {
            float progressPercentage = 0f;
            if (progress.description.StartsWith("Building Tile ", StringComparison.Ordinal))
            {
                _hasStartedBakingTiles = true;
                int i1 = progress.description.IndexOf('/', 14);
                if (i1 == -1 ||
                    !int.TryParse(progress.description.Substring(14, i1 - 14), NumberStyles.Number, CultureInfo.InvariantCulture, out int partialSmall) ||
                    !int.TryParse(progress.description.Substring(i1 + 1), NumberStyles.Number, CultureInfo.InvariantCulture, out int partialLarge))
                    progressPercentage = 0.5f;
                else
                    progressPercentage = ((float)partialSmall / partialLarge) * 0.8f + 0.1f;
            }
            else if (_hasStartedBakingTiles)
                progressPercentage = 0.9f + progress.progress / 10f;
            else
                progressPercentage = progress.progress / 10f;
            SendNavBakeProgressUpdate.Invoke(Provider.GatherClientConnections(), netId, progressPercentage, progress.description, true);
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
                Logger.LogInfo($"[{Source}] [SERVER / A* PATHFINDING] [{name.Format()} # {nav.Format()}] {"Done baking navigation".Colorize(ConsoleColor.Gray)}.");
            else
                Logger.LogInfo($"[{Source}] [SERVER / A* PATHFINDING] [{netId.Format()}] {"Done baking navigation".Colorize(ConsoleColor.Gray)}.");
        }
        else if (name != null)
            Logger.LogInfo($"[{Source}] [SERVER / A* PATHFINDING] [{name.Format()} # {nav.Format()}] {progress.Format("P")} - {desc.Colorize(ConsoleColor.Gray)}.");
        else
            Logger.LogInfo($"[{Source}] [SERVER / A* PATHFINDING] [{netId.Format()}] {progress.Format("P")} - {desc.Colorize(ConsoleColor.Gray)}.");

        EditorUIExtension? editorUi = UIExtensionManager.GetInstance<EditorUIExtension>();
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
            Logger.LogWarning($"Unable to find NetId for navigation flag: {nav.Format()}.", method: Source);
        }
    }
#endif
    private static void OnBakeNavigationWhileAlreadyBaking()
    {
        Logger.LogWarning(BlockBake == 0 ? "Tried to bake navigation while it's already baking." : "You do not have permission to bake navigation.", method: Source);
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
}