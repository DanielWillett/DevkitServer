using System.CodeDom;
using System.Diagnostics;
using HarmonyLib;
using Pathfinding;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using DevkitServer.API.Abstractions;
using DevkitServer.Players.UI;
using DevkitServer.Util.Region;
using Pathfinding.Voxels;
using SDG.Framework.Landscapes;
using SDG.Framework.Rendering;
using SDG.Framework.Utilities;

namespace DevkitServer.Patches;
[HarmonyPatch]
[HarmonyPriority(-1)]
internal class NavigationPatches
{
    private const string Source = "NAV PATCHES";

    private static NavigationBuilder? _currentBuilder;

    private static readonly FieldInfo CurrentBuilderField = typeof(NavigationPatches).GetField(nameof(_currentBuilder), BindingFlags.Static | BindingFlags.NonPublic)!;

    internal static readonly Action<RecastGraph, Voxelize, int, int>? BuildTileMeshMethod = Accessor.GenerateInstanceCaller<RecastGraph, Action<RecastGraph, Voxelize, int, int>>("BuildTileMesh");
    internal static readonly InstanceGetter<AstarPath, ThreadControlQueue>? GetActionQueue = Accessor.GenerateInstanceGetter<AstarPath, ThreadControlQueue>("pathQueue");

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
        ins.Insert(0, new CodeInstruction(OpCodes.Ldsfld, CurrentBuilderField));
        ins.Insert(1, new CodeInstruction(OpCodes.Brfalse, lbl));
        ins.Insert(2, new CodeInstruction(OpCodes.Call, Accessor.GetMethod(OnBakeNavigationWhileAlreadyBaking)));
        ins.Insert(3, new CodeInstruction(OpCodes.Ret));
        ins[4].labels.Add(lbl);

        if (replacementMethod != null && originalMethod != null && delegateConstructor != null)
        {
            for (int i = 0; i < ins.Count; ++i)
            {
                if (!ins[i].Calls(originalMethod))
                    continue;

                ins.Insert(i, new CodeInstruction(OpCodes.Ldnull).MoveLabelsFrom(ins[i]));
                ins.Insert(i + 1, new CodeInstruction(OpCodes.Ldftn, Accessor.GetMethod(OnBakeNavigationProgressUpdate)));
                ins.Insert(i + 2, new CodeInstruction(OpCodes.Newobj, delegateConstructor));
                ins[i + 3] = new CodeInstruction(replacementMethod.GetCall(), replacementMethod);
                patched = true;
            }
        }

        if (!patched)
        {
            Logger.LogWarning($"{method.Format()} - Failed to patch.", method: Source);
        }

        return ins;
    }

    private static void OnBakeNavigationProgressUpdate(Progress progress)
    {
        Logger.LogInfo("[A*] (" + progress.progress.Format("P") + ") " + progress.description.Colorize(ConsoleColor.Gray) + ".");
    }

    private static void OnBakeNavigationWhileAlreadyBaking()
    {
        Logger.LogWarning("Tried to bake navigation while it's already baking.", method: Source);
#if CLIENT
        UIMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource, "AlreadyBakingNavigation");
#endif
    }

    [HarmonyPatch(typeof(RecastGraph), "ScanAllTiles", typeof(OnScanStatus))]
    [HarmonyTranspiler]
    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> ScanAllTilesTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase method, ILGenerator generator)
    {
        if (BuildTileMeshMethod == null)
        {
            return instructions;
        }

        List<CodeInstruction> ins = new List<CodeInstruction>(128);

        LocalBuilder navBuilderLocal = generator.DeclareLocal(typeof(NavigationBuilder));
        Label lblGoToCompleted = generator.DefineLabel();
        Label lblNoCurrentBuilderSet = generator.DefineLabel();
        ins.Add(new CodeInstruction(OpCodes.Ldsfld, CurrentBuilderField));
        ins.Add(new CodeInstruction(OpCodes.Dup));
        ins.Add(new CodeInstruction(OpCodes.Stloc, navBuilderLocal));
        ins.Add(new CodeInstruction(OpCodes.Brfalse, lblNoCurrentBuilderSet));
        ins.Add(new CodeInstruction(OpCodes.Ldloc, navBuilderLocal));
        ins.Add(new CodeInstruction(OpCodes.Ldfld, NavigationBuilder.CompletedField));
        ins.Add(new CodeInstruction(OpCodes.Brtrue, lblGoToCompleted));
        int ind = ins.Count;

        ins.AddRange(instructions);
        ins[ind].labels.Add(lblNoCurrentBuilderSet);

        bool patched = false;

        for (int i = 5; i < ins.Count; ++i)
        {
            if (!patched && PatchUtil.MatchPattern(ins, i,
                    x => x.operand is ConstructorInfo ctor && ctor.DeclaringType == typeof(Stopwatch)
                    ))
            {
                PatchUtil.RemoveUntil(ins, i, x => x.LoadsConstant("Assigning Graph Indices"), false);

                LocalBuilder? lcl = null;
                int lclInd = -1;
                for (int j = i; j >= 0; --j)
                {
                    if (ins[j].opcode.IsLdLoc())
                    {
                        lcl = PatchUtil.GetLocal(ins[j], out lclInd, false);
                        break;
                    }
                }

                ins.Insert(i, new CodeInstruction(OpCodes.Ldloc, navBuilderLocal).MoveLabelsFrom(ins[i]));
                ins.Insert(i + 1, new CodeInstruction(OpCodes.Dup));
                ins.Insert(i + 2, PatchUtil.GetLocalCodeInstruction(lcl, lclInd, false));
                ins.Insert(i + 3, new CodeInstruction(OpCodes.Stfld, NavigationBuilder.VoxField));

                ins.Insert(i + 4, new CodeInstruction(OpCodes.Call, NavigationBuilder.StartMethod));
                ins.Insert(i + 5, new CodeInstruction(OpCodes.Ret));
                ins[i + 6].labels.Add(lblGoToCompleted);
                patched = true;
            }
        }

        if (!patched)
        {
            Logger.LogWarning($"{method.Format()} - Failed to split.", method: Source);
            return instructions;
        }

        return ins;
    }

    
    [HarmonyPatch(typeof(AstarPath), "ScanSpecific", typeof(NavGraph), typeof(OnScanStatus))]
    [HarmonyTranspiler]
    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> ScanSpecificTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase method, ILGenerator generator)
    {
        if (BuildTileMeshMethod == null)
        {
            return instructions;
        }

        MethodInfo? scanInternal = typeof(NavGraph).GetMethod("ScanInternal", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(OnScanStatus) }, null);
        if (scanInternal == null)
        {
            Logger.LogWarning($"{method.Format()} - Unable to find method: {FormattingUtil.FormatMethod(typeof(void), typeof(NavGraph), "ScanInternal", new (Type type, string? name)[]
            {
                (typeof(OnScanStatus), "statusCallback")
            })}.", method: Source);
            return instructions;
        }

        MethodInfo? applyLinks = typeof(AstarPath).GetMethod("ApplyLinks", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null, Array.Empty<Type>(), null);
        if (applyLinks == null)
        {
            Logger.LogWarning($"{method.Format()} - Unable to find method: {FormattingUtil.FormatMethod(typeof(void), typeof(AstarPath), "ApplyLinks", arguments: Array.Empty<Type>())}.", method: Source);
        }

        List<CodeInstruction> ins = new List<CodeInstruction>(instructions);
        
        Label lblGoToCompleted = generator.DefineLabel();
        Label lblNoCurrentBuilderSet = generator.DefineLabel();

        bool patched = false;
        FieldInfo? iField = null, forField = null;
        Type? dispClass1 = null;
        ConstructorInfo? ctorDispClass2 = null;
        LocalBuilder? dispClass2Lcl = null;
        int dispClass2LclInd = -1;
        bool init = false, init2 = false, remP = false;
        for (int i = 0; i < ins.Count; ++i)
        {
            if (!remP && applyLinks != null && PatchUtil.MatchPattern(ins, i,
                    x => x.opcode == OpCodes.Ldarg_0,
                    x => x.Calls(applyLinks)))
            {
                PatchUtil.TransferStartingInstructionNeeds(ins[i], ins[i + 2]);
                ins[i + 2].blocks.AddRange(ins[i + 1].blocks);
                ins.RemoveRange(i, 2);
                --i;
                remP = true;
            }
            if (!init && PatchUtil.FollowPattern(ins, ref i,
                    x => x.opcode == OpCodes.Newobj,
                    x => x.opcode == OpCodes.Stloc_0
                ))
            {
                ins.Insert(i, new CodeInstruction(OpCodes.Ldsfld, CurrentBuilderField));
                ins.Insert(i + 1, new CodeInstruction(OpCodes.Brfalse, lblNoCurrentBuilderSet));
                ins.Insert(i + 2, new CodeInstruction(OpCodes.Ldsfld, CurrentBuilderField));
                ins.Insert(i + 3, new CodeInstruction(OpCodes.Ldfld, NavigationBuilder.CompletedField));
                ins.Insert(i + 4, new CodeInstruction(OpCodes.Brtrue, lblGoToCompleted));
                ins[i + 5].labels.Add(lblNoCurrentBuilderSet);
                dispClass1 = ((ConstructorInfo)ins[i - 2].operand).DeclaringType;
                init = true;
                i += 4;
                Logger.LogDebug($"{method.Format()} - Found display class.");
            }
            if (init && !init2 && PatchUtil.MatchPattern(ins, i,
                         x => x.opcode == OpCodes.Newobj && Attribute.IsDefined(((ConstructorInfo)x.operand).DeclaringType, typeof(CompilerGeneratedAttribute)),
                         x => x.opcode.IsStLoc()))
            {
                ctorDispClass2 = (ConstructorInfo)ins[i].operand;
                dispClass2Lcl = PatchUtil.GetLocal(ins[i + 1], out dispClass2LclInd, true);
                forField = ctorDispClass2.DeclaringType!.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).First(x => x.FieldType == dispClass1);
                init2 = true;
                Logger.LogDebug($"{method.Format()} - Found for loop display class.");
            }
            if (init && iField == null && PatchUtil.MatchPattern(ins, i,
                      x => x.opcode == OpCodes.Ldloc_0,
                      x => x.opcode == OpCodes.Ldc_I4_0,
                      x => x.opcode == OpCodes.Stfld
                  ))
            {
                iField = ins[i + 2].operand as FieldInfo;
                Logger.LogDebug($"{method.Format()} - Found 'i' field.");
            }
            if (init2 && !patched && iField != null && PatchUtil.MatchPattern(ins, i,
                        x => x.IsLdarg(),
                        x => x.IsLdloc(),
                        x => x.Calls(scanInternal)
                        ))
            {
                ins.Insert(i, new CodeInstruction(OpCodes.Ldarg_1).MoveLabelsFrom(ins[i]));
                ins.Insert(i + 1, new CodeInstruction(OpCodes.Castclass, typeof(RecastGraph)));
                ins.Insert(i + 2, new CodeInstruction(OpCodes.Ldarg_2));
                ins.Insert(i + 3, new CodeInstruction(OpCodes.Ldloc_0));
                ins.Insert(i + 4, new CodeInstruction(OpCodes.Ldfld, iField));
                ins.Insert(i + 5, new CodeInstruction(OpCodes.Newobj, AccessTools.Constructor(typeof(NavigationBuilder), new Type[] { typeof(RecastGraph), typeof(OnScanStatus), typeof(int) })));
                ins.Insert(i + 6, new CodeInstruction(OpCodes.Stsfld, CurrentBuilderField));

                ins.Insert(i + 10, new CodeInstruction(OpCodes.Ret));
                ins.Insert(i + 11, new CodeInstruction(OpCodes.Ldsfld, CurrentBuilderField));
                ins[i + 11].labels.Add(lblGoToCompleted);
                ins.Insert(i + 12, new CodeInstruction(OpCodes.Ldarg_0));
                ins.Insert(i + 13, new CodeInstruction(OpCodes.Newobj, ctorDispClass2));
                ins.Insert(i + 14, new CodeInstruction(OpCodes.Dup));
                ins.Insert(i + 15, PatchUtil.GetLocalCodeInstruction(dispClass2Lcl, dispClass2LclInd, true));
                ins.Insert(i + 16, new CodeInstruction(OpCodes.Ldarg_0));
                ins.Insert(i + 17, new CodeInstruction(OpCodes.Stfld, forField));
                ins.Insert(i + 18, new CodeInstruction(OpCodes.Ldfld, NavigationBuilder.GraphIndexField));
                ins.Insert(i + 19, new CodeInstruction(OpCodes.Stfld, iField));
                ins.Insert(i + 20, new CodeInstruction(OpCodes.Ldnull));
                ins.Insert(i + 21, new CodeInstruction(OpCodes.Stsfld, CurrentBuilderField));
                i += 21;
                foreach (FieldInfo field in dispClass1
                             .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                             .Where(x => typeof(Delegate).IsAssignableFrom(x.FieldType)))
                {
                    ins.Insert(++i, new CodeInstruction(OpCodes.Ldarg_0));
                    ins.Insert(++i, new CodeInstruction(OpCodes.Ldnull));
                    ins.Insert(++i, new CodeInstruction(OpCodes.Stfld, field));
                }
                patched = true;
                Logger.LogDebug($"{method.Format()} - Split method.");
            }
        }

        if (!patched)
        {
            Logger.LogWarning($"{method.Format()} - Failed to split.", method: Source);
            return instructions;
        }

        // Accessor.AddFunctionStepthrough(ins, method);

        return ins;
    }
}

[EarlyTypeInit]
internal class NavigationBuilder
{
    static NavigationBuilder()
    {
        // Accessor.AddFunctionStepthrough(typeof(RecastGraph).GetMethod("GetNodes", BindingFlags.Instance | BindingFlags.Public));
    }
    internal static readonly MethodInfo StartMethod = typeof(NavigationBuilder).GetMethod(nameof(Start), BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)!;
    internal static readonly FieldInfo VoxField = typeof(NavigationBuilder).GetField(nameof(_vox), BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)!;
    internal static readonly FieldInfo CompletedField = typeof(NavigationBuilder).GetField(nameof(_completed), BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)!;
    internal static readonly FieldInfo GraphIndexField = typeof(NavigationBuilder).GetField(nameof(_graphIndex), BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)!;
    

    private SurroundingTilesIterator _iterator;
    private readonly Stopwatch _sw;
    private int _c;
    private readonly int _total;
    private readonly OnScanStatus? _callback;
    private int _x, _z;
    private int _graphIndex;
    [UsedImplicitly]
    private bool _completed;
    [UsedImplicitly]
    private Voxelize? _vox;
#if CLIENT
    private readonly int[] _lastXYs;
#endif
    internal RecastGraph Graph { get; }
    internal Flag? Flag { get; }
    internal float Progress => (float)_c / _total;
    public NavigationBuilder(RecastGraph graph, OnScanStatus? callback, int index)
    {
        Graph = graph;
        _graphIndex = index;
        _callback = callback;
        Flag = NavigationUtil.NavigationFlags.FirstOrDefault(x => x.graph == graph);
        _sw = new Stopwatch();
        _total = graph.tileXCount * graph.tileZCount;
        _iterator = new SurroundingTilesIterator(graph.tileXCount / 2, graph.tileZCount / 2, TileIteratorMode.Other, Math.Max(graph.tileXCount, graph.tileZCount), -1);
#if CLIENT
        _lastXYs = new int[32];
#endif
    }
    internal void Start()
    {
        NavigationPatches.GetActionQueue?.Invoke(AstarPath.active)?.Unblock();
        TimeUtility.updated += OnUpdate;
#if CLIENT
        DevkitServerGLUtility.OnRenderAny += OnRenderGL;
#endif
    }

#if CLIENT
    private void OnRenderGL()
    {
        if (_c <= 0)
            return;

        GLUtility.matrix = Matrix4x4.identity;
        GLUtility.LINE_FLAT_COLOR.SetPass(0);
        GL.Begin(GL.LINES);

        int ct = Math.Min(_c, _lastXYs.Length / 2);
        for (int i = 0; i < ct; i += 2)
        {
            Bounds bounds = Graph.GetTileBounds(_lastXYs[i], _lastXYs[i + 1]);

            Vector3 c1 = new Vector3(bounds.min.x, 0f, bounds.max.z);
            Vector3 c2 = new Vector3(bounds.max.x, 0f, bounds.min.z);

            Landscape.getWorldHeight(bounds.min, out float minH);
            Landscape.getWorldHeight(bounds.max, out float maxH);
            Landscape.getWorldHeight(c1, out c1.y);
            Landscape.getWorldHeight(c2, out c2.y);

            GL.Color(Color.yellow with { a = (float)(ct - i) / ct });
            GLUtility.line(bounds.min with { y = minH }, c1);
            GLUtility.line(c1, bounds.max with { y = maxH });
            GLUtility.line(bounds.max with { y = maxH }, c2);
            GLUtility.line(c2, bounds.min with { y = minH });
        }

        GL.End();
    }
#endif

    internal bool MoveNext()
    {
        do
        {
            if (!_iterator.MoveNext())
                return false;
            _x = _iterator.CurrentX; _z = _iterator.CurrentY;
        }
        while (_x >= Graph.tileXCount || _z >= Graph.tileZCount);
#if CLIENT
        for (int i = _lastXYs.Length - 1; i > 1; --i)
        {
            _lastXYs[i - 2] = _lastXYs[i];
        }

        _lastXYs[0] = _x;
        _lastXYs[1] = _z;
#endif
        ++_c;
        NavigationPatches.BuildTileMeshMethod!.Invoke(Graph, _vox!, _x, _z);
        Logger.LogDebug($"[A*] Building tile @ ({_x.Format()}, {_z.Format()}).");
        return true;
    }
    private void OnUpdate()
    {
        _sw.Start();
        bool broken;
        ThreadControlQueue? tq = NavigationPatches.GetActionQueue?.Invoke(AstarPath.active);
        tq?.Block();
        do
        {
            broken = !MoveNext();
            if (broken)
                break;
        }
        while (_sw.ElapsedMilliseconds < 0.035f);
        Logger.LogInfo($"[A*] ({Progress.Format("P")}) Built Tiles ({_c.Format()} / {_total.Format()})");
        _sw.Stop();
        if (broken)
        {
            TimeUtility.updated -= OnUpdate;
#if CLIENT
            DevkitServerGLUtility.OnRenderAny -= OnRenderGL;
#endif
            _completed = true;
            try
            {
                AstarPath.active.ScanSpecific(Graph, _callback);
            }
            catch (Exception ex)
            {
                throw;
                Logger.LogError("Error finishing building nav mesh.", method: "NAV BUILDER");
                Logger.LogError(ex, method: "NAV BUILDER");
            }
        }
        else
            tq?.Unblock();
    }
}