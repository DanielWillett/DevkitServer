using System.Runtime.CompilerServices;
using DanielWillett.SpeedBytes;

namespace DevkitServer.Util.Encoding;
public static unsafe class UnsafeBitConverter
{
    private static int[]? _decimalBuffer;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetBytes(byte* ptr, bool value, int offset = 0) => *(ptr + offset) = value ? (byte)1 : (byte)0;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetBytes(byte* ptr, byte value, int offset = 0) => *(ptr + offset) = value;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetBytes(byte* ptr, sbyte value, int offset = 0) => *(sbyte*)(ptr + offset) = value;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetBytes(byte* ptr, char value, int offset = 0)
    {
        *(short*)(ptr + offset) = unchecked((short)value);
        if (!BitConverter.IsLittleEndian)
            Reverse(ptr + offset, sizeof(short));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetBytes(byte* ptr, ushort value, int offset = 0)
    {
        *(short*)(ptr + offset) = unchecked((short)value);
        if (!BitConverter.IsLittleEndian)
            Reverse(ptr + offset, sizeof(ushort));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetBytes(byte* ptr, uint value, int offset = 0)
    {
        *(int*)(ptr + offset) = unchecked((int)value);
        if (!BitConverter.IsLittleEndian)
            Reverse(ptr + offset, sizeof(uint));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetBytes(byte* ptr, ulong value, int offset = 0)
    {
        *(long*)(ptr + offset) = unchecked((long)value);
        if (!BitConverter.IsLittleEndian)
            Reverse(ptr + offset, sizeof(ulong));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetBytes(byte* ptr, short value, int offset = 0)
    {
        *(short*)(ptr + offset) = value;
        if (!BitConverter.IsLittleEndian)
            Reverse(ptr + offset, sizeof(short));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetBytes(byte* ptr, int value, int offset = 0)
    {
        *(int*)(ptr + offset) = value;
        if (!BitConverter.IsLittleEndian)
            Reverse(ptr + offset, sizeof(int));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetBytes(byte* ptr, long value, int offset = 0)
    {
        *(long*)(ptr + offset) = value;
        if (!BitConverter.IsLittleEndian)
            Reverse(ptr + offset, sizeof(long));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetBytes(byte* ptr, float value, int offset = 0)
    {
        *(int*)(ptr + offset) = *(int*)&value;
        if (!BitConverter.IsLittleEndian)
            Reverse(ptr + offset, sizeof(float));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetBytes(byte* ptr, double value, int offset = 0)
    {
        *(long*)(ptr + offset) = *(long*)&value;
        if (!BitConverter.IsLittleEndian)
            Reverse(ptr + offset, sizeof(double));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetBytes(byte* ptr, decimal value, int offset = 0)
    {
        int[] ints = decimal.GetBits(value);
        *(int*)(ptr + offset) = ints[0];
        *(int*)(ptr + offset + sizeof(int)) = ints[1];
        *(int*)(ptr + offset + sizeof(int) * 2) = ints[2];
        *(int*)(ptr + offset + sizeof(int) * 3) = ints[3];
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetBytes(byte* ptr, Guid value, int offset = 0)
    {
        if (BitConverter.IsLittleEndian)
            *(Guid*)(ptr + offset) = value;
        else
        {
            byte[] bytes = value.ToByteArray();
            for (int i = 0; i < 16; ++i)
                ptr[offset + i] = bytes[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetBytes(byte[] arr, bool value, int offset = 0) { fixed (byte* ptr = arr) { *(ptr + offset) = value ? (byte)1 : (byte)0; } }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetBytes(byte[] arr, byte value, int offset = 0) { fixed (byte* ptr = arr) { *(ptr + offset) = value; } }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetBytes(byte[] arr, sbyte value, int offset = 0) { fixed (byte* ptr = arr) { *(sbyte*)(ptr + offset) = value; } }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetBytes(byte[] arr, char value, int offset = 0)
    {
        fixed (byte* ptr = arr)
        {
            *(short*)(ptr + offset) = unchecked((short)value);
            if (!BitConverter.IsLittleEndian)
                Reverse(ptr + offset, sizeof(short));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetBytes(byte[] arr, ushort value, int offset = 0)
    {
        fixed (byte* ptr = arr)
        {
            *(short*)(ptr + offset) = unchecked((short)value);
            if (!BitConverter.IsLittleEndian)
                Reverse(ptr + offset, sizeof(ushort));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetBytes(byte[] arr, uint value, int offset = 0)
    {
        fixed (byte* ptr = arr)
        {
            *(int*)(ptr + offset) = unchecked((int)value);
            if (!BitConverter.IsLittleEndian)
                Reverse(ptr + offset, sizeof(uint));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetBytes(byte[] arr, ulong value, int offset = 0)
    {
        fixed (byte* ptr = arr)
        {
            *(long*)(ptr + offset) = unchecked((long)value);
            if (!BitConverter.IsLittleEndian)
                Reverse(ptr + offset, sizeof(ulong));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetBytes(byte[] arr, short value, int offset = 0)
    {
        fixed (byte* ptr = arr)
        {
            *(short*)(ptr + offset) = value;
            if (!BitConverter.IsLittleEndian)
                Reverse(ptr + offset, sizeof(short));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetBytes(byte[] arr, int value, int offset = 0)
    {
        fixed (byte* ptr = arr)
        {
            *(int*)(ptr + offset) = value;
            if (!BitConverter.IsLittleEndian)
                Reverse(ptr + offset, sizeof(int));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetBytes(byte[] arr, long value, int offset = 0)
    {
        fixed (byte* ptr = arr)
        {
            *(long*)(ptr + offset) = value;
            if (!BitConverter.IsLittleEndian)
                Reverse(ptr + offset, sizeof(long));
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetBytes(byte[] arr, float value, int offset = 0)
    {
        fixed (byte* ptr = arr)
        {
            *(int*)(ptr + offset) = *(int*)&value;
            if (!BitConverter.IsLittleEndian)
                Reverse(ptr + offset, sizeof(float));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetBytes(byte[] arr, double value, int offset = 0)
    {
        fixed (byte* ptr = arr)
        {
            *(long*)(ptr + offset) = *(long*)&value;
            if (!BitConverter.IsLittleEndian)
                Reverse(ptr + offset, sizeof(double));
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetBytes(byte[] arr, decimal value, int offset = 0)
    {
        int[] ints = decimal.GetBits(value);
        fixed (byte* ptr = &arr[offset])
        {
            *(int*)ptr = ints[0];
            *(int*)(ptr + sizeof(int)) = ints[1];
            *(int*)(ptr + sizeof(int) * 2) = ints[2];
            *(int*)(ptr + sizeof(int) * 3) = ints[3];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetBytes(byte[] arr, Guid value, int offset = 0)
    {
        if (BitConverter.IsLittleEndian)
        {
            fixed (byte* ptr = arr)
                *(Guid*)(ptr + offset) = value;
        }
        else
        {
            byte[] bytes = value.ToByteArray();
            for (int i = 0; i < 16; ++i)
                arr[offset + i] = bytes[i];
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static decimal GetDecimal(byte* ptr, int offset = 0)
    {
        // ReSharper disable once InconsistentlySynchronizedField
        if (_decimalBuffer == null)
            Interlocked.CompareExchange(ref _decimalBuffer!, new int[4], null);
        lock (_decimalBuffer)
        {
            _decimalBuffer[0] = *(int*)(ptr + offset);
            _decimalBuffer[1] = *(int*)(ptr + offset + sizeof(int));
            _decimalBuffer[2] = *(int*)(ptr + offset + sizeof(int) * 2);
            _decimalBuffer[3] = *(int*)(ptr + offset + sizeof(int) * 3);
            return new decimal(_decimalBuffer);
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool GetBoolean(byte* ptr, int offset = 0) => *(ptr + offset) > 0;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static char GetChar(byte* ptr, int offset = 0) => (char)GetInt16(ptr, offset);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetFloat(byte* ptr, int offset = 0)
    {
        int val = GetInt32(ptr, offset);
        return *(float*)&val;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double GetDouble(byte* ptr, int offset = 0)
    {
        long val = GetInt64(ptr, offset);
        return *(double*)&val;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte GetInt8(byte* ptr, int offset = 0) => unchecked((sbyte)*(ptr + offset));
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte GetUInt8(byte* ptr, int offset = 0) => *(ptr + offset);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short GetInt16(byte* ptr, int offset = 0)
    {
        if (BitConverter.IsLittleEndian && (nint)ptr % sizeof(short) == 0) return *(short*)(ptr + offset);
        return BitConverter.IsLittleEndian
            ? (short)(*(ptr + offset) | (*(ptr + offset + 1) << 8))
            : (short)((*(ptr + offset) << 8) | *(ptr + offset + 1));
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort GetUInt16(byte* ptr, int offset = 0) => unchecked((ushort)GetInt16(ptr, offset));
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetInt32(byte* ptr, int offset = 0)
    {
        if (BitConverter.IsLittleEndian && (nint)ptr % sizeof(int) == 0) return *(int*)(ptr + offset);
        return BitConverter.IsLittleEndian
            ? *(ptr + offset) | (*(ptr + offset + 1) << 8) | (*(ptr + offset + 2) << 16) | (*(ptr + offset + 3) << 24)
            : (*(ptr + offset) << 24) | (*(ptr + offset + 1) << 16) | (*(ptr + offset + 2) << 8) | *(ptr + offset + 3);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint GetUInt32(byte* ptr, int offset = 0) => unchecked((uint)GetInt32(ptr, offset));
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long GetInt64(byte* ptr, int offset = 0)
    {
        if (BitConverter.IsLittleEndian && (nint)ptr % sizeof(long) == 0) return *(long*)(ptr + offset);
        return BitConverter.IsLittleEndian
            ? (uint)(*(ptr + offset) | (*(ptr + offset + 1) << 8) | (*(ptr + offset + 2) << 16) | (*(ptr + offset + 3) << 24)) | (long)(*(ptr + offset + 4) | (*(ptr + offset + 5) << 8) | (*(ptr + offset + 6) << 16) | (*(ptr + offset + 7) << 24)) << 32
            : (uint)((*(ptr + offset + 4) << 24) | (*(ptr + offset + 5) << 16) | (*(ptr + offset + 6) << 8) | *(ptr + offset + 7)) | (long)((*(ptr + offset) << 24) | (*(ptr + offset + 1) << 16) | (*(ptr + offset + 2) << 8) | *(ptr + offset + 3)) << 32;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong GetUInt64(byte* ptr, int offset = 0) => unchecked((ulong)GetInt64(ptr, offset));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guid GetGuid(byte* ptr, int offset = 0)
    {
        if (BitConverter.IsLittleEndian)
            return *(Guid*)(ptr + offset);

        byte[] data = new byte[16];
        for (int i = 0; i < 16; ++i)
            data[i] = ptr[i + offset];
        return new Guid(data);
    }
    private static void Reverse(byte* litEndStrt, int size)
    {
        byte* stack = stackalloc byte[size];
        for (int i = 0; i < size; ++i)
            stack[i] = litEndStrt[i];
        for (int i = 0; i < size; i++)
            litEndStrt[i] = stack[size - i - 1];
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void EndianCheck(byte* litEndStrt, int size)
    {
        if (size <= 1 || BitConverter.IsLittleEndian)
            return;
        Reverse(litEndStrt, size);
    }
}
