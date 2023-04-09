using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace DevkitServer.Util;
public static class DevkitServerUtility
{
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
    private static string BytesToString(byte[] bytes, int columnCount, int len, string fmt)
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
}
