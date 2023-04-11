using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;

namespace DevkitServer.Util;
public static class DevkitServerUtility
{
    public const int Int24Bounds = 8388607;
    public static string QuickFormat(string input, string? val)
    {
        int ind = input.IndexOf("{0}", StringComparison.Ordinal);
        if (ind != -1)
        {
            if (string.IsNullOrEmpty(val))
                return input.Substring(0, ind) + input.Substring(ind + 3, input.Length - ind - 3);
            return input.Substring(0, ind) + val + input.Substring(ind + 3, input.Length - ind - 3);
        }
        return input;
    }

    private static string[]? _sizeCodes;
    private static double[]? _sizeIncrements;
    public static string FormatBytes(long length)
    {
        _sizeCodes ??= new string[]
        {
            "B",
            "KiB",
            "MiB",
            "GiB",
            "TiB",
            "PiB",
            "EiB"
        };
        
        if (_sizeIncrements == null)
        {
            _sizeIncrements = new double[_sizeCodes.Length];
            for (int i = 0; i < _sizeCodes.Length; ++i)
                _sizeIncrements[i] = Math.Pow(1024, i);
        }

        if (length == 0)
            return length.ToString("N");

        bool neg = length < 0;
        length = Math.Abs(length);

        double incr = Math.Log(length, 1024);
        int inc;
        if ((incr % 1) > 0.8)
            inc = (int)Math.Ceiling(incr);
        else
            inc = (int)Math.Floor(incr);

        if (inc >= _sizeIncrements.Length)
            inc = _sizeIncrements.Length - 1;

        double len = length / _sizeIncrements[inc];
        if (neg) len = -len;

        return len.ToString("N1") + " " + _sizeCodes[inc];
    }
    public static unsafe int GetLabelId(this Label label) => *(int*)&label;
    public static void PrintBytesHex(byte[] bytes, int columnCount = 16, int len = -1)
    {
        Logger.LogInfo(Environment.NewLine + GetBytesHex(bytes, columnCount, len));
    }
    public static void PrintBytesDec(byte[] bytes, int columnCount = 16, int len = -1)
    {
        Logger.LogInfo(Environment.NewLine + GetBytesDec(bytes, columnCount, len));
    }
    public static string GetBytesHex(byte[] bytes, int columnCount = 16, int len = -1)
    {
        return BytesToString(bytes, columnCount, len, "X2");
    }
    public static string GetBytesDec(byte[] bytes, int columnCount = 16, int len = -1)
    {
        return BytesToString(bytes, columnCount, len, "000");
    }
    public static string BytesToString(byte[] bytes, int columnCount, int len, string fmt)
    {
        StringBuilder sb = new StringBuilder(bytes.Length * 4);
        len = len < 0 || len > bytes.Length ? bytes.Length : len;
        for (int i = 0; i < len; ++i)
        {
            if (i != 0 && i % columnCount == 0)
                sb.Append(Environment.NewLine);
            else if (i != 0)
                sb.Append(' ');
            sb.Append(bytes[i].ToString(fmt));
        }
        return sb.ToString();
    }
    public static CodeInstruction GetLocalCodeInstruction(LocalBuilder? builder, int index, bool set, bool byref = false)
    {
        return new CodeInstruction(GetLocalCode(builder != null ? builder.LocalIndex : index, set, byref), builder);
    }
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
    public static unsafe void ReverseFloat(byte* ptr, int index)
    {
        byte b = ptr[index + 1];
        ptr[index + 1] = ptr[index + 2];
        ptr[index + 2] = b;
        b = ptr[index];
        ptr[index] = ptr[index + 3];
        ptr[index + 3] = b;
    }
}
