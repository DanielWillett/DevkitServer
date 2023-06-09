using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using System.Reflection.Emit;

namespace DevkitServer.Util;
public static class PatchUtil
{
    [Pure]
    public static CodeInstruction GetLocalCodeInstruction(LocalBuilder? builder, int index, bool set, bool byref = false)
    {
        return new CodeInstruction(GetLocalCode(builder != null ? builder.LocalIndex : index, set, byref), builder);
    }
    [Pure]
    public static OpCode GetLocalCode(int index, bool set, bool byref = false)
    {
        if (index > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (!set && byref)
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
    public static OpCode GetArgumentCode(int index, bool set, bool byref = false)
    {
        if (index > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (set)
            return index > byte.MaxValue ? OpCodes.Starg : OpCodes.Starg_S;
        if (byref)
            return index > byte.MaxValue ? OpCodes.Ldarga : OpCodes.Ldarga_S;
        
        return index switch
        {
            0 => OpCodes.Ldarg_0,
            1 => OpCodes.Ldarg_1,
            2 => OpCodes.Ldarg_2,
            3 => OpCodes.Ldarg_3,
            _ => index > byte.MaxValue ? OpCodes.Ldarg : OpCodes.Ldarg_S
        };
    }
    public static void EmitArgument(ILGenerator il, int index, bool set, bool byref = false)
    {
        if (index > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (set)
        {
            il.Emit(index > byte.MaxValue ? OpCodes.Starg : OpCodes.Starg_S, index);
            return;
        }
        if (byref)
        {
            il.Emit(index > byte.MaxValue ? OpCodes.Ldarga : OpCodes.Ldarga_S, index);
            return;
        }
        
        if (index is < 4 and > -1)
        {
            il.Emit(index switch
            {
                0 => OpCodes.Ldarg_0,
                1 => OpCodes.Ldarg_1,
                2 => OpCodes.Ldarg_2,
                _ => OpCodes.Ldarg_3
            });
            return;
        }

        il.Emit(index > byte.MaxValue ? OpCodes.Ldarg : OpCodes.Ldarg_S, index);
    }
    [Pure]
    public static int GetLocalIndex(CodeInstruction code, bool set)
    {
        if (code.opcode.OperandType == OperandType.ShortInlineVar &&
            (set && code.opcode == OpCodes.Stloc_S ||
             !set && code.opcode == OpCodes.Ldloc_S || !set && code.opcode == OpCodes.Ldloca_S))
            return ((LocalBuilder)code.operand).LocalIndex;
        if (code.opcode.OperandType == OperandType.InlineVar &&
            (set && code.opcode == OpCodes.Stloc ||
             !set && code.opcode == OpCodes.Ldloc || !set && code.opcode == OpCodes.Ldloca))
            return ((LocalBuilder)code.operand).LocalIndex;
        if (set)
        {
            if (code.opcode == OpCodes.Stloc_0)
                return 0;
            if (code.opcode == OpCodes.Stloc_1)
                return 1;
            if (code.opcode == OpCodes.Stloc_2)
                return 2;
            if (code.opcode == OpCodes.Stloc_3)
                return 3;
        }
        else
        {
            if (code.opcode == OpCodes.Ldloc_0)
                return 0;
            if (code.opcode == OpCodes.Ldloc_1)
                return 1;
            if (code.opcode == OpCodes.Ldloc_2)
                return 2;
            if (code.opcode == OpCodes.Ldloc_3)
                return 3;
        }

        return -1;
    }
    [Pure]
    public static CodeInstruction LoadConstantI4(int number)
    {
        return number switch
        {
            -1 => new CodeInstruction(OpCodes.Ldc_I4_M1),
            0 => new CodeInstruction(OpCodes.Ldc_I4_0),
            1 => new CodeInstruction(OpCodes.Ldc_I4_1),
            2 => new CodeInstruction(OpCodes.Ldc_I4_2),
            3 => new CodeInstruction(OpCodes.Ldc_I4_3),
            4 => new CodeInstruction(OpCodes.Ldc_I4_4),
            5 => new CodeInstruction(OpCodes.Ldc_I4_5),
            6 => new CodeInstruction(OpCodes.Ldc_I4_6),
            7 => new CodeInstruction(OpCodes.Ldc_I4_7),
            8 => new CodeInstruction(OpCodes.Ldc_I4_8),
            _ => new CodeInstruction(OpCodes.Ldc_I4, number),
        };
    }
    public static void LoadConstantI4(ILGenerator generator, int number)
    {
        OpCode code = number switch
        {
            -1 => OpCodes.Ldc_I4_M1,
            0 => OpCodes.Ldc_I4_0,
            1 => OpCodes.Ldc_I4_1,
            2 => OpCodes.Ldc_I4_2,
            3 => OpCodes.Ldc_I4_3,
            4 => OpCodes.Ldc_I4_4,
            5 => OpCodes.Ldc_I4_5,
            6 => OpCodes.Ldc_I4_6,
            7 => OpCodes.Ldc_I4_7,
            8 => OpCodes.Ldc_I4_8,
            _ => OpCodes.Ldc_I4
        };
        if (number is < -1 or > 8)
            generator.Emit(code, number);
        else
            generator.Emit(code);
    }
    public static void LoadConstantI4(DebuggableEmitter generator, int number)
    {
        OpCode code = number switch
        {
            -1 => OpCodes.Ldc_I4_M1,
            0 => OpCodes.Ldc_I4_0,
            1 => OpCodes.Ldc_I4_1,
            2 => OpCodes.Ldc_I4_2,
            3 => OpCodes.Ldc_I4_3,
            4 => OpCodes.Ldc_I4_4,
            5 => OpCodes.Ldc_I4_5,
            6 => OpCodes.Ldc_I4_6,
            7 => OpCodes.Ldc_I4_7,
            8 => OpCodes.Ldc_I4_8,
            _ => OpCodes.Ldc_I4
        };
        if (number is < -1 or > 8)
            generator.Emit(code, number);
        else
            generator.Emit(code);
    }
    [Pure]
    public static CodeInstruction LoadParameter(int index)
    {
        return index switch
        {
            0 => new CodeInstruction(OpCodes.Ldarg_0),
            1 => new CodeInstruction(OpCodes.Ldarg_1),
            2 => new CodeInstruction(OpCodes.Ldarg_2),
            3 => new CodeInstruction(OpCodes.Ldarg_3),
            < ushort.MaxValue => new CodeInstruction(OpCodes.Ldarg_S, index),
            _ => new CodeInstruction(OpCodes.Ldarg, index)
        };
    }
    public static void LoadParameter(this ILGenerator generator, int index, bool byref = false)
    {
        if (byref)
        {
            generator.Emit(index > ushort.MaxValue ? OpCodes.Ldarga : OpCodes.Ldarga_S, index);
            return;
        }
        OpCode code = index switch
        {
            0 => OpCodes.Ldarg_0,
            1 => OpCodes.Ldarg_1,
            2 => OpCodes.Ldarg_2,
            3 => OpCodes.Ldarg_3,
            <= ushort.MaxValue => OpCodes.Ldarg_S,
            _ => OpCodes.Ldarg
        };
        if (index > 3)
            generator.Emit(code, index);
        else
            generator.Emit(code);
    }
    [Pure]
    public static LocalBuilder? GetLocal(CodeInstruction code, out int index, bool set)
    {
        if (code.opcode.OperandType == OperandType.ShortInlineVar &&
            (set && code.opcode == OpCodes.Stloc_S ||
             !set && code.opcode == OpCodes.Ldloc_S || !set && code.opcode == OpCodes.Ldloca_S))
        {
            LocalBuilder bld = (LocalBuilder)code.operand;
            index = bld.LocalIndex;
            return bld;
        }
        if (code.opcode.OperandType == OperandType.InlineVar &&
            (set && code.opcode == OpCodes.Stloc ||
             !set && code.opcode == OpCodes.Ldloc || !set && code.opcode == OpCodes.Ldloca))
        {
            LocalBuilder bld = (LocalBuilder)code.operand;
            index = bld.LocalIndex;
            return bld;
        }
        if (set)
        {
            if (code.opcode == OpCodes.Stloc_0)
            {
                index = 0;
                return null;
            }
            if (code.opcode == OpCodes.Stloc_1)
            {
                index = 1;
                return null;
            }
            if (code.opcode == OpCodes.Stloc_2)
            {
                index = 2;
                return null;
            }
            if (code.opcode == OpCodes.Stloc_3)
            {
                index = 3;
                return null;
            }
        }
        else
        {
            if (code.opcode == OpCodes.Ldloc_0)
            {
                index = 0;
                return null;
            }
            if (code.opcode == OpCodes.Ldloc_1)
            {
                index = 1;
                return null;
            }
            if (code.opcode == OpCodes.Ldloc_2)
            {
                index = 2;
                return null;
            }
            if (code.opcode == OpCodes.Ldloc_3)
            {
                index = 3;
                return null;
            }
        }

        index = -1;
        return null;
    }
    [Pure]
    public static CodeInstruction CopyWithoutSpecial(this CodeInstruction instruction) => new CodeInstruction(instruction.opcode, instruction.operand);
    [Pure]
    public static bool IsOfType(this OpCode opcode, OpCode comparand, bool fuzzy = false)
    {
        if (opcode == comparand)
            return true;
        if (opcode.IsStArg())
            return comparand.IsStArg();
        if (opcode.IsStLoc())
            return comparand.IsStLoc();
        if (!fuzzy)
        {
            if (opcode.IsLdArg())
                return comparand.IsLdArg();
            if (opcode.IsLdArg(true))
                return comparand.IsLdArg(true);
            if (opcode.IsLdLoc())
                return comparand.IsLdLoc();
            if (opcode.IsLdLoc(true))
                return comparand.IsLdLoc(true);
            if (opcode.IsLdc())
                return comparand.IsLdc();
            if (opcode.IsLdc(false, true))
                return comparand.IsLdc(false, true);
            if (opcode.IsLdc(false, false, true))
                return comparand.IsLdc(false, false, true);
            if (opcode.IsLdc(false, false, false, true))
                return comparand.IsLdc(false, false, false, true);
            if (opcode.IsLdc(false, false, false, false, true))
                return comparand.IsLdc(false, false, false, false, true);
            if (opcode.IsLdc(false, false, false, false, false, true))
                return comparand.IsLdc(false, false, false, false, false, true);
            if (opcode.IsBr())
                return comparand.IsBr();
            if (opcode.IsBr(false, true))
                return comparand.IsBr(false, true);
            if (opcode.IsBr(false, false, true))
                return comparand.IsBr(false, false, true);
            if (opcode.IsBr(false, false, false, true))
                return comparand.IsBr(false, false, false, true);
            if (opcode.IsBr(false, false, false, false, true))
                return comparand.IsBr(false, false, false, false, true);
            if (opcode.IsBr(false, false, false, false, false, true))
                return comparand.IsBr(false, false, false, false, false, true);
            if (opcode.IsBr(false, false, false, false, false, false, true))
                return comparand.IsBr(false, false, false, false, false, false, true);
            if (opcode.IsBr(false, false, false, false, false, false, false, true))
                return comparand.IsBr(false, false, false, false, false, false, false, true);
            if (opcode.IsBr(false, false, false, false, false, false, false, false, true))
                return comparand.IsBr(false, false, false, false, false, false, false, false, true);
        }
        else
        {
            if (opcode.IsLdArg(true, true))
                return comparand.IsLdArg(true, true);
            if (opcode.IsLdLoc(true, true))
                return comparand.IsLdLoc(true, true);
            if (opcode.IsLdc(true, true, true, true, true, true))
                return comparand.IsLdc(true, true, true, true, true, true);
            if (opcode.IsBr(true, true, true, true, true, true, true, true, true))
                return comparand.IsBr(true, true, true, true, true, true, true, true, true);
            if (opcode.IsConv(true, false, false, false, false, false, false))
                return comparand.IsConv(true, false, false, false, false, false, false);
            if (opcode.IsConv(false, true, false, false, false, false, false))
                return comparand.IsConv(false, true, false, false, false, false, false);
            if (opcode.IsConv(false, false, true, false, false, false, false))
                return comparand.IsConv(false, false, true, false, false, false, false);
            if (opcode.IsConv(false, false, false, true, false, false, false))
                return comparand.IsConv(false, false, false, true, false, false, false);
            if (opcode.IsConv(false, false, false, false, true, false, false))
                return comparand.IsConv(false, false, false, false, true, false, false);
            if (opcode.IsConv(false, false, false, false, false, true, false))
                return comparand.IsConv(false, false, false, false, false, true, false);
            if (opcode.IsConv(false, false, false, false, false, false, true))
                return comparand.IsConv(false, false, false, false, false, false, true);
        }

        return false;
    }

    [Pure]
    public static bool IsStLoc(this OpCode opcode)
    {
        return opcode == OpCodes.Stloc || opcode == OpCodes.Stloc_S || opcode == OpCodes.Stloc_0 || opcode == OpCodes.Stloc_1 || opcode == OpCodes.Stloc_2 || opcode == OpCodes.Stloc_3;
    }
    [Pure]
    public static bool IsLdLoc(this OpCode opcode, bool address = false, bool either = false)
    {
        if (opcode == OpCodes.Ldloc_S || opcode == OpCodes.Ldloc_0 || opcode == OpCodes.Ldloc_1 || opcode == OpCodes.Ldloc_2 || opcode == OpCodes.Ldloc_3 || opcode == OpCodes.Ldloc)
            return !address || either;
        if (opcode == OpCodes.Ldloca_S || opcode == OpCodes.Ldloca)
            return address || either;

        return false;
    }
    [Pure]
    public static bool IsStArg(this OpCode opcode)
    {
        return opcode == OpCodes.Starg || opcode == OpCodes.Starg_S;
    }
    [Pure]
    public static bool IsLdArg(this OpCode opcode, bool address = false, bool either = false)
    {
        if (opcode == OpCodes.Ldarg_S || opcode == OpCodes.Ldarg_0 || opcode == OpCodes.Ldarg_1 || opcode == OpCodes.Ldarg_2 || opcode == OpCodes.Ldarg_3 || opcode == OpCodes.Ldarg)
            return !address || either;
        if (opcode == OpCodes.Ldarga_S || opcode == OpCodes.Ldarga)
            return address || either;

        return false;
    }

    [Pure]
    public static bool IsBr(this OpCode opcode, bool br = true, bool brtrue = false, bool brfalse = false, bool beq = false, bool bne = false, bool bge = false, bool ble = false, bool bgt = false, bool blt = false)
    {
        if (opcode == OpCodes.Br_S || opcode == OpCodes.Br)
            return br;
        if (opcode == OpCodes.Brtrue_S || opcode == OpCodes.Brtrue)
            return brtrue;
        if (opcode == OpCodes.Brfalse_S || opcode == OpCodes.Brfalse)
            return brfalse;
        if (opcode == OpCodes.Beq_S || opcode == OpCodes.Beq)
            return brfalse;
        if (opcode == OpCodes.Bne_Un_S || opcode == OpCodes.Bne_Un)
            return bne;
        if (opcode == OpCodes.Bge || opcode == OpCodes.Bge || opcode == OpCodes.Bge_Un_S || opcode == OpCodes.Bge_Un)
            return bge;
        if (opcode == OpCodes.Ble || opcode == OpCodes.Ble || opcode == OpCodes.Ble_Un_S || opcode == OpCodes.Ble_Un)
            return ble;
        if (opcode == OpCodes.Bgt || opcode == OpCodes.Bgt || opcode == OpCodes.Bgt_Un_S || opcode == OpCodes.Bgt_Un)
            return bgt;
        if (opcode == OpCodes.Blt || opcode == OpCodes.Blt || opcode == OpCodes.Blt_Un_S || opcode == OpCodes.Blt_Un)
            return blt;

        return false;
    }
    [Pure]
    public static bool IsLdc(this OpCode opcode, bool @int = true, bool @long = false, bool @float = false, bool @double = false, bool @string = false, bool @null = false)
    {
        if (opcode == OpCodes.Ldc_I4_0 || opcode == OpCodes.Ldc_I4_1 || opcode == OpCodes.Ldc_I4_S ||
            opcode == OpCodes.Ldc_I4 || opcode == OpCodes.Ldc_I4_2 || opcode == OpCodes.Ldc_I4_3 ||
            opcode == OpCodes.Ldc_I4_4 || opcode == OpCodes.Ldc_I4_5 || opcode == OpCodes.Ldc_I4_6 ||
            opcode == OpCodes.Ldc_I4_7 || opcode == OpCodes.Ldc_I4_8 || opcode == OpCodes.Ldc_I4_M1)
            return @int;
        if (opcode == OpCodes.Ldc_R4)
            return @float;
        if (opcode == OpCodes.Ldc_R8)
            return @double;
        if (opcode == OpCodes.Ldc_I8)
            return @long;
        if (opcode == OpCodes.Ldstr)
            return @string;
        if (opcode == OpCodes.Ldnull)
            return @null;

        return false;
    }
    [Pure]
    public static bool IsConv(this OpCode opcode, bool @nint = true, bool @byte = true, bool @short = true, bool @int = true, bool @long = true, bool @float = true, bool @double = true, bool fromUnsigned = true, bool toUnsigned = true, bool signed = true, bool overflowCheck = true, bool noOverflowCheck = true)
    {
        if (noOverflowCheck && (signed && opcode == OpCodes.Conv_I || toUnsigned && opcode == OpCodes.Conv_U) || overflowCheck && (signed && opcode == OpCodes.Conv_Ovf_I || fromUnsigned && opcode == OpCodes.Conv_Ovf_I_Un))
            return @nint;
        if (noOverflowCheck && (signed && opcode == OpCodes.Conv_I1 || toUnsigned && opcode == OpCodes.Conv_U1) || overflowCheck && (signed && opcode == OpCodes.Conv_Ovf_I1 || fromUnsigned && opcode == OpCodes.Conv_Ovf_I1_Un))
            return @byte;
        if (noOverflowCheck && (signed && opcode == OpCodes.Conv_I2 || toUnsigned && opcode == OpCodes.Conv_U2) || overflowCheck && (signed && opcode == OpCodes.Conv_Ovf_I2 || fromUnsigned && opcode == OpCodes.Conv_Ovf_I2_Un))
            return @short;
        if (noOverflowCheck && (signed && opcode == OpCodes.Conv_I4 || toUnsigned && opcode == OpCodes.Conv_U4) || overflowCheck && (signed && opcode == OpCodes.Conv_Ovf_I4 || fromUnsigned && opcode == OpCodes.Conv_Ovf_I4_Un))
            return @int;
        if (noOverflowCheck && (signed && opcode == OpCodes.Conv_I8 || toUnsigned && opcode == OpCodes.Conv_U8) || overflowCheck && (signed && opcode == OpCodes.Conv_Ovf_I8 || fromUnsigned && opcode == OpCodes.Conv_Ovf_I8_Un))
            return @long;
        if (noOverflowCheck && (opcode == OpCodes.Conv_R4 || fromUnsigned && opcode == OpCodes.Conv_R_Un))
            return @float;
        if (noOverflowCheck && opcode == OpCodes.Conv_R8)
            return @double;

        return false;
    }
    [Pure]
    public static bool ShouldCallvirt(this MethodInfo method)
    {
        return method.DeclaringType is { IsInterface: true } || method.IsVirtual || method.IsAbstract;
    }
    [Pure]
    public static OpCode GetCall(this MethodInfo method)
    {
        return method.ShouldCallvirt() ? OpCodes.Callvirt : OpCodes.Call;
    }
    public static void CheckCopiedMethodPatchOutOfDate(ref MethodInfo original, MethodBase invoker)
    {
        if (original == null)
            return;
        if (invoker == null)
        {
            Logger.LogWarning("Method invoker not found: " + original.Format() + ".", method: "CLIENT EVENTS");
            original = null!;
            return;
        }
        ParameterInfo[] p = original.GetParameters();
        ParameterInfo[] p2 = invoker.GetParameters();
        bool instanceMatches = !original.IsStatic && original.DeclaringType != null && p2.Length > 0 &&
                               (p2[0].ParameterType.IsAssignableFrom(original.DeclaringType) ||
                                p2[0].ParameterType.IsByRef && p2[0].ParameterType.GetElementType()!
                                    .IsAssignableFrom(original.DeclaringType));
        if (p.Length != p2.Length)
        {
            if (!instanceMatches || p.Length != p2.Length - 1)
            {
                Logger.LogWarning("Method patch out of date: " + original.Format() + " vs. " + invoker.Format() + ".", method: "CLIENT EVENTS");

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
                Logger.LogWarning("Method patch out of date: " + original.Format() + " vs. " + invoker.Format() + ".", method: "CLIENT EVENTS");
                original = null!;
                return;
            }
        }
    }
}