using DanielWillett.ReflectionTools;
using DevkitServer.API;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;

namespace DevkitServer.Patches;
public static class PatchHelpers
{
    /// <summary>
    /// Remove instructions until <paramref name="match"/> is satisfied or the method ends.
    /// </summary>
    /// <returns>The amount of instructions removed.</returns>
    public static int RemoveUntil(IList<CodeInstruction> instructions, int index, PatternMatch match, bool includeMatch = true)
    {
        int amt = 0;
        for (int i = index; i < instructions.Count; ++i)
        {
            if (!match(instructions[i]))
                continue;

            amt = i - index + (includeMatch ? 1 : 0);
            if (instructions is List<CodeInstruction> list)
            {
                list.RemoveRange(index, amt);
            }
            else
            {
                for (int j = 0; j < amt; ++j)
                    instructions.RemoveAt(index);
            }

            break;
        }
        return amt;
    }
    internal static void CheckCopiedMethodPatchOutOfDate(ref MethodInfo original, MethodBase invoker)
    {
        if (original == null)
            return;
        if (invoker == null)
        {
            Logger.DevkitServer.LogWarning(nameof(CheckCopiedMethodPatchOutOfDate), "Method invoker not found: " + original.Format() + ".");
            original = null!;
            return;
        }
        ParameterInfo[] p = original.GetParameters();
        ParameterInfo[] p2 = invoker.GetParameters();
        bool instanceMatches = original is { IsStatic: false, DeclaringType: not null } && p2.Length > 0 &&
                               (p2[0].ParameterType.IsAssignableFrom(original.DeclaringType) ||
                                p2[0].ParameterType.IsByRef && p2[0].ParameterType.GetElementType()!
                                    .IsAssignableFrom(original.DeclaringType));
        if (p.Length != p2.Length)
        {
            if (!instanceMatches || p.Length != p2.Length - 1)
            {
                Logger.DevkitServer.LogWarning(nameof(CheckCopiedMethodPatchOutOfDate), "Method patch out of date: " + original.Format() + " vs. " + invoker.Format() + ".");

                original = null!;
                return;
            }
        }

        int invOffset = instanceMatches ? 1 : 0;

        for (int i = 0; i < p.Length; ++i)
        {
            if (!p2[i + invOffset].ParameterType.IsAssignableFrom(p[i].ParameterType))
            {
                if (p[i].ParameterType.IsByRef &&
                    p2[i + invOffset].ParameterType.IsAssignableFrom(p[i].ParameterType.GetElementType()))
                    continue;
                if (p2[i + invOffset].ParameterType.IsByRef &&
                    p2[i + invOffset].ParameterType.GetElementType()!.IsAssignableFrom(p[i].ParameterType))
                    continue;
                Logger.DevkitServer.LogWarning(nameof(CheckCopiedMethodPatchOutOfDate), "Method patch out of date: " + original.Format() + " vs. " + invoker.Format() + ".");
                original = null!;
                return;
            }
        }
    }

    /// <summary>
    /// Create a code instruction loading a local (by builder or index).
    /// </summary>
    [Pure]
    public static CodeInstruction GetLocalCodeInstruction(LocalBuilder? builder, int index, bool set, bool byRef = false)
    {
        return new CodeInstruction(GetLocalCode(builder?.LocalIndex ?? index, set, byRef), builder);
    }

    /// <summary>
    /// Get the <see cref="OpCode"/> for a local.
    /// </summary>
    [Pure]
    public static OpCode GetLocalCode(int index, bool set, bool byRef = false)
    {
        if (index > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (!set && byRef)
        {
            return index > byte.MaxValue ? OpCodes.Ldloca : OpCodes.Ldloca_S;
        }
        return index switch
        {
            0 => set ? OpCodes.Stloc_0 : OpCodes.Ldloc_0,
            1 => set ? OpCodes.Stloc_1 : OpCodes.Ldloc_1,
            2 => set ? OpCodes.Stloc_2 : OpCodes.Ldloc_2,
            3 => set ? OpCodes.Stloc_3 : OpCodes.Ldloc_3,
            _ => set ? (index > byte.MaxValue ? OpCodes.Stloc : OpCodes.Stloc_S) : (index > byte.MaxValue ? OpCodes.Ldloc : OpCodes.Ldloc_S)
        };
    }

    [Pure]
    internal static IEnumerable<CodeInstruction> AddIsEditorCall(this IAccessor accessor, [InstantHandle] IEnumerable<CodeInstruction> instructions, MethodBase __method)
    {
        if (AccessorExtensions.IsServerGetter == null || AccessorExtensions.IsEditorGetter == null)
        {
            foreach (CodeInstruction instr in instructions)
                yield return instr;

            yield break;
        }
        foreach (CodeInstruction instr in instructions)
        {
            if (instr.Calls(AccessorExtensions.IsServerGetter))
            {
                yield return instr;
                yield return new CodeInstruction(OpCodes.Not);
                yield return new CodeInstruction(accessor.GetCallRuntime(AccessorExtensions.IsEditorGetter), AccessorExtensions.IsEditorGetter);
                yield return new CodeInstruction(OpCodes.Or);
                yield return new CodeInstruction(OpCodes.Not);
                if (__method != null)
                    Logger.DevkitServer.LogDebug("Accessor.AddIsEditorCall", $"Inserted editor call to {__method.Format()}.");
                else
                    Logger.DevkitServer.LogDebug("Accessor.AddIsEditorCall", "Inserted editor call to unknown method.");
            }
            else
                yield return instr;
        }
    }

    /// <summary>
    /// Transpiles a method to add logging for each instruction.
    /// </summary>
    public static bool AddFunctionStepthrough(MethodBase method)
    {
        if (method == null)
        {
            Logger.DevkitServer.LogError("AccessorExtensions.AddFunctionStepthrough", "Error adding function stepthrough to method, not found.");
            return false;
        }
        try
        {
            PatchesMain.Patcher.Patch(method,
                transpiler: new HarmonyMethod(typeof(AccessorExtensions).GetMethod(nameof(AddFunctionStepthroughTranspiler),
                    BindingFlags.NonPublic | BindingFlags.Static)));
            Logger.DevkitServer.LogInfo("AccessorExtensions.AddFunctionStepthrough", $"Added stepthrough to: {method.Format()}.");
            return true;
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("AccessorExtensions.AddFunctionStepthrough", ex, $"Error adding function stepthrough to {method.Format()}.");
            return false;
        }
    }
    /// <summary>
    /// Transpiles a method to add logging for entry and exit of the method.
    /// </summary>
    public static bool AddFunctionIOLogging(MethodBase method)
    {
        if (method == null)
        {
            Logger.DevkitServer.LogError("Accessor.AddFunctionIOLogging", "Error adding function IO logging to method, not found.");
            return false;
        }
        try
        {
            PatchesMain.Patcher.Patch(method,
                transpiler: new HarmonyMethod(typeof(Accessor).GetMethod(nameof(AddFunctionIOTranspiler),
                    BindingFlags.NonPublic | BindingFlags.Static)));
            Logger.DevkitServer.LogInfo("Accessor.AddFunctionIOLogging", $"Added function IO logging to: {method.Format()}.");
            return true;
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("Accessor.AddFunctionIOLogging", ex, $"Error adding function IO logging to {method.Format()}.");
            return false;
        }
    }

    private static IEnumerable<CodeInstruction> AddFunctionIOTranspiler([InstantHandle] IEnumerable<CodeInstruction> instructions, MethodBase method)
    {
        yield return new CodeInstruction(OpCodes.Ldstr, "In method: " + method.Format() + " (basic entry)");
        yield return new CodeInstruction(OpCodes.Ldc_I4, (int)ConsoleColor.Green);
        yield return new CodeInstruction(AccessorExtensions.LogInfo.GetCallRuntime(), AccessorExtensions.LogInfo);

        foreach (CodeInstruction instr in instructions)
        {
            if (instr.opcode == OpCodes.Ret || instr.opcode == OpCodes.Throw)
            {
                CodeInstruction logInstr = new CodeInstruction(OpCodes.Ldstr, "Out method: " + method.Format() + (instr.opcode == OpCodes.Ret ? " (returned)" : " (exception)"));
                logInstr.WithStartBlocksFrom(instr);
                yield return logInstr;
                yield return new CodeInstruction(OpCodes.Ldc_I4, (int)ConsoleColor.Green);
                yield return new CodeInstruction(AccessorExtensions.LogInfo.GetCallRuntime(), AccessorExtensions.LogInfo);
            }
            yield return instr;
        }
    }
    private static IEnumerable<CodeInstruction> AddFunctionStepthroughTranspiler([InstantHandle] IEnumerable<CodeInstruction> instructions, MethodBase method)
    {
        List<CodeInstruction> ins = [.. instructions];
        AddFunctionStepthrough(ins, method);
        return ins;
    }
    internal static void AddFunctionStepthrough(List<CodeInstruction> ins, MethodBase method)
    {
        ins.Insert(0, new CodeInstruction(OpCodes.Ldstr, "Stepping through Method: " + method.Format() + ":"));
        ins.Insert(1, new CodeInstruction(OpCodes.Ldc_I4, (int)ConsoleColor.Green));
        ins.Insert(2, new CodeInstruction(AccessorExtensions.LogInfo.GetCallRuntime(), AccessorExtensions.LogInfo));
        ins[0].WithStartBlocksFrom(ins[3]);
        for (int i = 3; i < ins.Count; i++)
        {
            CodeInstruction instr = ins[i];
            CodeInstruction? start = null;
            foreach (ExceptionBlock block in instr.blocks)
            {
                CodeInstruction blockInst = new CodeInstruction(OpCodes.Ldstr, "  " + block.Format());
                start ??= blockInst;
                ins.Insert(i, blockInst);
                ins.Insert(i + 1, new CodeInstruction(OpCodes.Ldc_I4, (int)ConsoleColor.Green));
                ins.Insert(i + 2, new CodeInstruction(AccessorExtensions.LogInfo.GetCallRuntime(), AccessorExtensions.LogInfo));
                i += 3;
            }

            foreach (Label label in instr.labels)
            {
                CodeInstruction lblInst = new CodeInstruction(OpCodes.Ldstr, "  " + label.Format() + ":");
                start ??= lblInst;
                ins.Insert(i, lblInst);
                ins.Insert(i + 1, new CodeInstruction(OpCodes.Ldc_I4, (int)ConsoleColor.Green));
                ins.Insert(i + 2, new CodeInstruction(AccessorExtensions.LogInfo.GetCallRuntime(), AccessorExtensions.LogInfo));
                i += 3;
            }

            CodeInstruction mainInst = new CodeInstruction(OpCodes.Ldstr, "  " + instr.Format());
            start ??= mainInst;
            ins.Insert(i, mainInst);
            ins.Insert(i + 1, new CodeInstruction(OpCodes.Ldc_I4, (int)ConsoleColor.Green));
            ins.Insert(i + 2, new CodeInstruction(AccessorExtensions.LogInfo.GetCallRuntime(), AccessorExtensions.LogInfo));
            i += 3;

            start.WithStartBlocksFrom(instr);
        }
    }
}
