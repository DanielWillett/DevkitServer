using DevkitServer.API.Abstractions;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Reflection;
using System.Reflection.Emit;

namespace DanielWillett.ReflectionTools;

public static class EmitUtility
{
    private static DevkitServer.Util.PatternMatch? ConvertMatch(PatternMatch? match)
    {
        if (match == null)
            return null;

        return (DevkitServer.Util.PatternMatch)match.Method.CreateDelegate(typeof(DevkitServer.Util.PatternMatch));
    }
    private static DevkitServer.Util.PatternMatch?[]? ConvertMatches(PatternMatch?[]? matches)
    {
        if (matches == null)
            return null;

        DevkitServer.Util.PatternMatch?[] arr = new DevkitServer.Util.PatternMatch?[matches.Length];
        for (int i = 0; i < matches.Length; ++i)
            arr[i] = ConvertMatch(matches[i]);

        return arr;
    }

    [Pure]
    public static IEnumerable<CodeInstruction> Throw<TException>(string? message = null) where TException : Exception
    {
        return DevkitServer.Util.PatchUtil.Throw<TException>(message);
    }

    [Pure]
    public static bool FollowPattern(IList<CodeInstruction> instructions, ref int index, params PatternMatch?[] matches)
    {
        return DevkitServer.Util.PatchUtil.FollowPattern(instructions, ref index, ConvertMatches(matches)!);
    }

    [Pure]
    public static bool RemovePattern(IList<CodeInstruction> instructions, int index, params PatternMatch?[] matches)
    {
        return DevkitServer.Util.PatchUtil.RemovePattern(instructions, index, ConvertMatches(matches)!);
    }

    [Pure]
    public static bool MatchPattern(IList<CodeInstruction> instructions, int index, params PatternMatch?[] matches)
    {
        return DevkitServer.Util.PatchUtil.MatchPattern(instructions, index, ConvertMatches(matches)!);
    }

    public static void ReturnIfFalse(IList<CodeInstruction> instructions, ILGenerator generator, ref int index, Func<bool> checker, Label? @goto = null)
    {
        DevkitServer.Util.PatchUtil.ReturnIfFalse(instructions, generator, ref index, checker, @goto);
    }

    [Pure]
    public static int ContinueUntil(IList<CodeInstruction> instructions, ref int index, PatternMatch match, bool includeMatch = true)
    {
        return DevkitServer.Util.PatchUtil.ContinueUntil(instructions, ref index, ConvertMatch(match)!, includeMatch);
    }

    [Pure]
    public static int ContinueWhile(IList<CodeInstruction> instructions, ref int index, PatternMatch match, bool includeNext = true)
    {
        return DevkitServer.Util.PatchUtil.ContinueWhile(instructions, ref index, ConvertMatch(match)!, includeNext);
    }

    [Pure]
    public static bool LabelNext(IList<CodeInstruction> instructions, int index, Label label, PatternMatch match, int shift = 0, bool labelRtnIfFailure = false)
    {
        return DevkitServer.Util.PatchUtil.LabelNext(instructions, index, label, ConvertMatch(match)!, shift, labelRtnIfFailure);
    }

    [Pure]
    public static Label? LabelNext(IList<CodeInstruction> instructions, ILGenerator generator, int index, PatternMatch match, int shift = 0)
    {
        return DevkitServer.Util.PatchUtil.LabelNext(instructions, generator, index, ConvertMatch(match)!, shift);
    }

    [Pure]
    public static Label LabelNextOrReturn(IList<CodeInstruction> instructions, ILGenerator generator, int index, PatternMatch? match, int shift = 0, bool allowUseExisting = true)
    {
        return DevkitServer.Util.PatchUtil.LabelNextOrReturn(instructions, generator, index, ConvertMatch(match)!, shift, allowUseExisting);
    }

    [Pure]
    public static Label? GetNextBranchTarget(IList<CodeInstruction> instructions, int index)
    {
        return DevkitServer.Util.PatchUtil.GetNextBranchTarget(instructions, index);
    }

    [Pure]
    public static int FindLabelDestinationIndex(IList<CodeInstruction> instructions, Label label, int startIndex = 0)
    {
        return DevkitServer.Util.PatchUtil.FindLabelDestinationIndex(instructions, label, startIndex);
    }

    [Pure]
    public static int GetLocalIndex(CodeInstruction code, bool set)
    {
        return DevkitServer.Util.PatchUtil.GetLocalIndex(code, set);
    }

    [Pure]
    public static CodeInstruction LoadConstantI4(int number)
    {
        return DevkitServer.Util.PatchUtil.LoadConstantI4(number);
    }

    [Pure]
    public static CodeInstruction GetParameter(int index)
    {
        return DevkitServer.Util.PatchUtil.LoadParameter(index);
    }

    [Pure]
    public static LocalBuilder? GetLocal(CodeInstruction code, out int index, bool set)
    {
        return DevkitServer.Util.PatchUtil.GetLocal(code, out index, set);
    }
    
    [Pure]
    public static CodeInstruction CopyWithoutSpecial(this CodeInstruction instruction) => DevkitServer.Util.PatchUtil.CopyWithoutSpecial(instruction);
    
    public static void TransferEndingInstructionNeeds(CodeInstruction originalEnd, CodeInstruction newEnd)
    {
        DevkitServer.Util.PatchUtil.TransferEndingInstructionNeeds(originalEnd, newEnd);
    }

    public static void TransferStartingInstructionNeeds(CodeInstruction originalStart, CodeInstruction newStart)
    {
        DevkitServer.Util.PatchUtil.TransferStartingInstructionNeeds(originalStart, newStart);
    }

    public static void MoveBlocksAndLabels(this CodeInstruction from, CodeInstruction to)
    {
        DevkitServer.Util.PatchUtil.MoveBlocksAndLabels(from, to);
    }

    [Pure]
    public static bool IsBeginBlockType(this ExceptionBlockType type) =>
        DevkitServer.Util.PatchUtil.IsBeginBlockType(type);
    
    [Pure]
    public static bool IsEndBlockType(this ExceptionBlockType type) =>
        DevkitServer.Util.PatchUtil.IsEndBlockType(type);
    
    [Pure]
    public static int GetLabelId(this Label label) => DevkitServer.Util.PatchUtil.GetLabelId(label);
    
    public static void EmitArgument(ILGenerator il, int index, bool set, bool byref = false)
    {
        DevkitServer.Util.PatchUtil.EmitArgument(il.AsEmitter(), index, set, byref);
    }

    public static void LoadConstantI4(ILGenerator generator, int number)
    {
        DevkitServer.Util.PatchUtil.LoadConstantI4(generator.AsEmitter(), number);
    }
    
    public static void EmitParameter(this ILGenerator generator, int index, bool byref = false, Type? type = null, Type? targetType = null)
    {
        DevkitServer.Util.PatchUtil.EmitParameter(generator.AsEmitter(), index, byref, type, targetType);
    }
    public static void EmitParameter(this ILGenerator generator, int index, string? castErrorMessage, bool byref = false, Type? type = null, Type? targetType = null)
    {
        DevkitServer.Util.PatchUtil.EmitParameter(generator.AsEmitter(), index, castErrorMessage, byref, type, targetType);
    }
    internal static void EmitParameter(this ILGenerator generator, string? logSource, int index, string? castErrorMessage, bool byref = false, Type? type = null, Type? targetType = null)
    {
        DevkitServer.Util.PatchUtil.EmitParameter(generator.AsEmitter(), logSource, index, castErrorMessage, byref, type, targetType);
    }
    
    [Pure]
    public static bool IsOfType(this OpCode opcode, OpCode comparand, bool fuzzy = false)
    {
        return DevkitServer.Util.PatchUtil.IsOfType(opcode, comparand, fuzzy);
    }

    [Pure]
    public static bool IsStLoc(this OpCode opcode)
    {
        return DevkitServer.Util.PatchUtil.IsStLoc(opcode);
    }

    [Pure]
    public static bool IsLdLoc(this OpCode opcode, bool byRef = false, bool either = false)
    {
        return DevkitServer.Util.PatchUtil.IsLdLoc(opcode, byRef, either);
    }

    [Pure]
    public static bool IsLdFld(this OpCode opcode, bool byRef = false, bool either = false, bool @static = false, bool staticOrInstance = false)
    {
        return DevkitServer.Util.PatchUtil.IsLdFld(opcode, byRef, either, @static, staticOrInstance);
    }

    [Pure]
    public static bool IsStArg(this OpCode opcode)
    {
        return DevkitServer.Util.PatchUtil.IsStArg(opcode);
    }

    [Pure]
    public static bool IsLdArg(this OpCode opcode, bool byRef = false, bool either = false)
    {
        return DevkitServer.Util.PatchUtil.IsLdArg(opcode, byRef, either);
    }

    [Pure]
    public static bool IsBrAny(this OpCode opcode, bool br = true, bool brtrue = true, bool brfalse = true,
        bool beq = true, bool bne = true, bool bge = true, bool ble = true, bool bgt = true, bool blt = true)
        => DevkitServer.Util.PatchUtil.IsBrAny(opcode, br, brtrue, brfalse, beq, bne, bge, ble, bgt, blt);

    [Pure]
    public static bool IsBr(this OpCode opcode, bool br = false, bool brtrue = false, bool brfalse = false,
        bool beq = false, bool bne = false, bool bge = false, bool ble = false, bool bgt = false, bool blt = false)
        => DevkitServer.Util.PatchUtil.IsBr(opcode, br, brtrue, brfalse, beq, bne, bge, ble, bgt, blt);

    [Pure]
    public static bool IsLdc(this OpCode opcode, bool @int = true, bool @long = false, bool @float = false, bool @double = false, bool @string = false, bool @null = false)
    {
        return DevkitServer.Util.PatchUtil.IsLdc(opcode, @int, @long, @float, @double, @string, @null);
    }

    [Pure]
    public static bool IsConv(this OpCode opcode, bool nint = true, bool @byte = true, bool @short = true, bool @int = true, bool @long = true, bool @float = true, bool @double = true,
        bool fromUnsigned = true, bool toUnsigned = true, bool signed = true, bool overflowCheck = true, bool noOverflowCheck = true)
    {
        return DevkitServer.Util.PatchUtil.IsConv(opcode, @nint, @byte, @short, @int, @long, @float, @double, fromUnsigned, toUnsigned, signed, overflowCheck, noOverflowCheck);
    }

    [Pure]
    public static OpCode GetCall(this MethodBase method)
    {
        return DevkitServer.Util.PatchUtil.GetCall(method);
    }

    [Pure]
    public static OpCode GetCallRuntime(this MethodBase method)
    {
        return DevkitServer.Util.PatchUtil.GetCallRuntime(method);
    }
}

public delegate bool PatternMatch(CodeInstruction instruction);