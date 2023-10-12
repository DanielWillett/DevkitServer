using DevkitServer.Multiplayer.Networking;
using HarmonyLib;
using Pathfinding;
using System.Reflection;
using System.Reflection.Emit;
using Progress = Pathfinding.Progress;
#if CLIENT
using DevkitServer.API.Abstractions;
using DevkitServer.API.Permissions;
using DevkitServer.Core.Permissions;
using DevkitServer.Multiplayer.Levels;
using DevkitServer.Players.UI;
using DevkitServer.Util.Encoding;
#endif
#if SERVER
using Pathfinding.Voxels;
using System.Diagnostics;
using Cysharp.Threading.Tasks;
using DevkitServer.Multiplayer.Levels;
using DevkitServer.Players;
using DevkitServer.Util.Encoding;
#endif

namespace DevkitServer.Patches;
[HarmonyPatch]
[HarmonyPriority(-1)]
internal static class NavigationPatches
{
    [UsedImplicitly]
    private static readonly NetCall<NetId> SendBakeNavRequest = new NetCall<NetId>(DevkitServerNetCall.SendBakeNavRequest);

    [UsedImplicitly]
    private static readonly NetCallCustom SendNavigationGraph = new NetCallCustom(DevkitServerNetCall.SendRecastGraph);

#if SERVER
    private static readonly Action? CallListen = Accessor.GenerateStaticCaller<Provider, Action>("listen");
    private static DateTime _lastListen;
#endif

    private const string Source = "NAV PATCHES";
    internal static bool BlockBake;
#if SERVER
    internal static bool CanBake => true;
#else
    internal static bool CanBake => !DevkitServerModule.IsEditing || (!BlockBake && (VanillaPermissions.AllNavigation.Has() || VanillaPermissions.BakeNavigation.Has(false)));
#endif

    private static readonly MethodInfo CurrentCanBakeGetter = typeof(NavigationPatches).GetProperty(nameof(CanBake), BindingFlags.Static | BindingFlags.NonPublic)!.GetMethod;

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
        ins.Insert(0, new CodeInstruction(OpCodes.Call, CurrentCanBakeGetter));
        ins.Insert(1, new CodeInstruction(OpCodes.Brtrue, lbl));
        ins.Insert(2, new CodeInstruction(OpCodes.Call, Accessor.GetMethod(OnBakeNavigationWhileAlreadyBaking)));
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

#if CLIENT
    [NetCall(NetCallSource.FromServer, DevkitServerNetCall.SendRecastGraph)]
    private static void ReceiveRecastGraph(MessageContext ctx, ByteReader reader)
    {
        if (reader.Stream != null)
            throw new NotSupportedException("Not supported when using stream mode.");

        NetId netId = reader.ReadNetId();
        int byteCt = reader.ReadInt32();
        int pos = reader.Position;

        IReadOnlyList<Flag> list = NavigationUtil.NavigationFlags;

        if (!NavigationNetIdDatabase.TryGetNavigation(netId, out byte nav) || list.Count <= nav)
        {
            reader.Skip(byteCt);
            Logger.LogWarning($"Failed to find an incoming navigation flag's nav index: {netId.Format()}.");
            return;
        }

        Flag flag = list[nav];
        NavigationUtil.ReadRecastGraphDataTo(reader, flag.graph);
        int pos2 = reader.Position;
        int expectedPosition = pos + byteCt;
        if (pos2 != expectedPosition)
        {
            if (pos2 < expectedPosition)
            {
                int bytesSkipped = reader.Position - expectedPosition;
                reader.Skip(bytesSkipped);
                Logger.LogWarning($"Navigation flag {nav.Format()} (NetId {netId.Format()}) read {bytesSkipped.Format()} B less from the stream than were written ({byteCt.Format()} B).", method: Source);
            }
            else if (pos2 > expectedPosition)
            {
                int bytesSkipped = expectedPosition - reader.Position;
                reader.Goto(expectedPosition);
                Logger.LogWarning($"Navigation flag {nav.Format()} (NetId {netId.Format()}) read {bytesSkipped.Format()} B more from the stream than were written ({byteCt.Format()} B).", method: Source);
            }
        }

        LevelNavigation.updateBounds();
        flag.needsNavigationSave = true;
        flag.UpdateEditorNavmesh();
    }
#endif

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
        UniTask.Create(async () =>
        {
            // to keep the lock statement from holding throughout the entire frozen frame (could cause a deadlock).
            await UniTask.NextFrame();

            IReadOnlyList<Flag> list = NavigationUtil.NavigationFlags;

            if (!NavigationNetIdDatabase.TryGetNavigation(netId, out byte nav))
            {
                Logger.LogWarning($"Unknown navigvation flag NetId: {netId.Format()}.", method: Source);
                ctx.Acknowledge(StandardErrorCode.NotFound);
                return;
            }

            if (list.Count <= nav)
            {
                Logger.LogWarning($"Unknown flag: {netId.Format()}, nav: {nav.Format()}.", method: Source);
                ctx.Acknowledge(StandardErrorCode.NotFound);
                return;
            }

            Flag flag = list[nav];

            Logger.LogInfo($"[{Source}] Baking navigation: {netId.Format()}...");
            Stopwatch sw = Stopwatch.StartNew();

            flag.bakeNavigation();

            sw.Stop();
            Logger.LogInfo($"[{Source}] Done baking navigation {netId.Format()}, baking took {sw.GetElapsedMilliseconds():F2} ms.");

            ctx.Acknowledge(StandardErrorCode.Success);


            RecastGraphWriterData data = new RecastGraphWriterData(netId, flag);
            EditorUser? user = ctx.GetCaller();
            if (user != null && (ctx.Overhead.Flags & MessageFlags.Request) != 0)
            {
                ctx.ReplyLayered(SendNavigationGraph, data.Write);
                SendNavigationGraph.Invoke(Provider.GatherClientConnectionsMatchingPredicate(x => x.playerID.steamID.m_SteamID != user.SteamId.m_SteamID), data.Write);
            }
            else
            {
                SendNavigationGraph.Invoke(Provider.GatherClientConnections(), data.Write);
            }
        });
    }
    private class RecastGraphWriterData
    {
        private readonly NetId _netId;
        private readonly Flag _flag;
        public RecastGraphWriterData(NetId netId, Flag flag)
        {
            _netId = netId;
            _flag = flag;
        }
        public void Write(ByteWriter writer)
        {
            writer.Write(_netId);
            int pos = writer.Count;
            writer.Write(0);

            NavigationUtil.WriteRecastGraphData(writer, _flag.graph);
            int pos2 = writer.Count;

            writer.BackTrack(pos);
            writer.Write(pos2 - pos - 4);
            writer.Return();
        }
    }

#endif

    private static void OnBakeNavigationProgressUpdate(Progress progress)
    {
        Logger.LogInfo($"[{Source}] [A* PATHFINDING] (" + progress.progress.Format("P") + ") " + progress.description.Colorize(ConsoleColor.Gray) + ".");
    }
#if CLIENT
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
        Logger.LogWarning(BlockBake ? "Tried to bake navigation while it's already baking." : "You do not have permission to bake navigation.", method: Source);
#if CLIENT
        UIMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource, "AlreadyBakingNavigation");
#endif
    }
}