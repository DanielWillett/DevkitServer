using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;
using DevkitServer.API;
using DevkitServer.Util.Encoding;
using JetBrains.Annotations;
using Action = System.Action;
#if SERVER
using DevkitServer.Patches;
using DevkitServer.Players;
using System.Net;
#endif

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
    public static Regex RemoveRichTextRegex { get; } =
        new Regex(@"(?<!(?:\<noparse\>(?!\<\/noparse\>)).*)\<\/{0,1}(?:(?:color=\""{0,1}[#a-z]{0,9}\""{0,1})|(?:color)|(?:size=\""{0,1}\d+\""{0,1})|(?:size)|(?:alpha)|(?:alpha=#[0-f]{1,2})|(?:#.{3,8})|(?:[isub])|(?:su[pb])|(?:lowercase)|(?:uppercase)|(?:smallcaps))\>", RegexOptions.IgnoreCase);
    public static Regex RemoveTMProRichTextRegex { get; } =
        new Regex(@"(?<!(?:\<noparse\>(?!\<\/noparse\>)).*)\<\/{0,1}(?:(?:noparse)|(?:alpha)|(?:alpha=#[0-f]{1,2})|(?:[su])|(?:su[pb])|(?:lowercase)|(?:uppercase)|(?:smallcaps))\>", RegexOptions.IgnoreCase);
    [Pure]
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
    [Pure]
    public static Bounds InflateBounds(in Bounds bounds)
    {
        Vector3 c = bounds.center;
        Vector3 e = bounds.extents;
        return new Bounds(new Vector3(Mathf.Round(c.x), Mathf.Round(c.y), Mathf.Round(c.z)), new Vector3((e.x + 0.5f).CeilToIntIgnoreSign(), (e.y + 0.5f).CeilToIntIgnoreSign(), (e.z + 0.5f).CeilToIntIgnoreSign()));
    }
    /// <summary>Convert an HTMLColor string to a actual color.</summary>
    /// <param name="htmlColorCode">A hexadecimal/HTML color key.</param>
    [Pure]
    public static Color Hex(this string htmlColorCode)
    {
        if (htmlColorCode.Length == 0)
            return Color.white;
        if (htmlColorCode[0] != '#')
            htmlColorCode = "#" + htmlColorCode;
        return ColorUtility.TryParseHtmlString(htmlColorCode, out Color color) ? color : Color.white;
    }
    /// <summary>Convert an HTMLColor string to a actual color.</summary>
    /// <param name="htmlColorCode">A hexadecimal/HTML color key.</param>
    public static bool TryParseHex(this string htmlColorCode, out Color color)
    {
        if (htmlColorCode.Length == 0)
        {
            color = Color.white;
            return false;
        }

        if (htmlColorCode[0] != '#')
            htmlColorCode = "#" + htmlColorCode;

        if (!ColorUtility.TryParseHtmlString(htmlColorCode, out color))
        {
            color = Color.white;
            return false;
        }

        return true;
    }
    [Pure]
    public static unsafe int GetLabelId(this Label label) => *(int*)&label;
    public static void PrintBytesHex(byte[] bytes, int columnCount = 64, int offset = 0, int len = -1)
    {
        Logger.LogInfo(Environment.NewLine + GetBytesHex(bytes, columnCount, offset, len));
    }
    public static void PrintBytesDec(byte[] bytes, int columnCount = 64, int offset = 0, int len = -1)
    {
        Logger.LogInfo(Environment.NewLine + GetBytesDec(bytes, columnCount, offset, len));
    }
    [Pure]
    public static string GetBytesHex(byte[] bytes, int columnCount = 64, int offset = 0, int len = -1)
    {
        return BytesToString(bytes, columnCount, offset, len, "X2");
    }
    [Pure]
    public static string GetBytesDec(byte[] bytes, int columnCount = 64, int offset = 0, int len = -1)
    {
        return BytesToString(bytes, columnCount, offset, len, "000");
    }
    public static unsafe void PrintBytesHex(byte* bytes, int len, int columnCount = 64, int offset = 0)
    {
        Logger.LogInfo(Environment.NewLine + GetBytesHex(bytes, len, columnCount, offset));
    }
    public static unsafe void PrintBytesDec(byte* bytes, int len, int columnCount = 64, int offset = 0)
    {
        Logger.LogInfo(Environment.NewLine + GetBytesDec(bytes, len, columnCount, offset));
    }
    [Pure]
    public static unsafe string GetBytesHex(byte* bytes, int len, int columnCount = 64, int offset = 0)
    {
        return BytesToString(bytes, columnCount, offset, len, "X2");
    }
    [Pure]
    public static unsafe string GetBytesDec(byte* bytes, int len, int columnCount = 64, int offset = 0)
    {
        return BytesToString(bytes, columnCount, offset, len, "000");
    }
    public static unsafe void PrintBytesHex<T>(T* bytes, int len, int columnCount = 64, int offset = 0) where T : unmanaged
    {
        Logger.LogInfo(Environment.NewLine + GetBytesHex(bytes, len, columnCount, offset));
    }
    public static unsafe void PrintBytesDec<T>(T* bytes, int len, int columnCount = 64, int offset = 0) where T : unmanaged
    {
        Logger.LogInfo(Environment.NewLine + GetBytesDec(bytes, len, columnCount, offset));
    }
    [Pure]
    public static unsafe string GetBytesHex<T>(T* bytes, int len, int columnCount = 64, int offset = 0) where T : unmanaged
    {
        return BytesToString(bytes, columnCount, offset, len);
    }
    [Pure]
    public static unsafe string GetBytesDec<T>(T* bytes, int len, int columnCount = 64, int offset = 0) where T : unmanaged
    {
        return BytesToString(bytes, columnCount, offset, len);
    }
    [Pure]
    public static string BytesToString(byte[] bytes, int columnCount, int offset, int len, string fmt)
    {
        if (offset >= bytes.Length)
            offset = bytes.Length - 1;
        if (len < 0 || len + offset < 0 || len + offset > bytes.Length)
            len = bytes.Length - offset;
        StringBuilder sb = new StringBuilder(len * 4);
        for (int i = 0; i < len; ++i)
        {
            if (i != 0 && i % columnCount == 0)
                sb.Append(Environment.NewLine);
            else if (i != 0)
                sb.Append(' ');
            sb.Append(bytes[i + offset].ToString(fmt));
        }
        return sb.ToString();
    }
    [Pure]
    public static unsafe string BytesToString(byte* bytes, int columnCount, int offset, int len, string fmt)
    {
        if (offset >= len)
            offset = len - 1;
        StringBuilder sb = new StringBuilder(len * 4);
        for (int i = 0; i < len; ++i)
        {
            if (i != 0 && i % columnCount == 0)
                sb.Append(Environment.NewLine);
            else if (i != 0)
                sb.Append(' ');
            sb.Append(bytes[i + offset].ToString(fmt));
        }
        return sb.ToString();
    }
    [Pure]
    public static unsafe string BytesToString<T>(T* bytes, int columnCount, int offset, int len) where T : unmanaged
    {
        if (offset >= len)
            offset = len - 1;
        StringBuilder sb = new StringBuilder(len * 4);
        for (int i = 0; i < len; ++i)
        {
            if (i != 0 && i % columnCount == 0)
                sb.Append(Environment.NewLine);
            else if (i != 0)
                sb.Append(' ');
            sb.Append(bytes[i + offset].ToString());
        }
        return sb.ToString();
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
    [Pure]
    public static uint ReverseUInt32(uint val) => ((val >> 24) & 0xFF) | (((val >> 16) & 0xFF) << 8) | (((val >> 8) & 0xFF) << 16) | (val << 24);
    [Pure]
    public static Vector2 ToVector2(this in Vector3 v3) => new Vector2(v3.x, v3.z);
    [Pure]
    public static Vector3 ToVector3(this in Vector2 v2) => new Vector3(v2.x, 0f, v2.y);
    [Pure]
    public static Vector3 ToVector3(this in Vector2 v2, float y) => new Vector3(v2.x, y, v2.y);
    public static bool TryParseSteamId(string str, out CSteamID steamId)
    {
        if (str.Equals("Nil", StringComparison.InvariantCultureIgnoreCase) ||
            str.Equals("null", StringComparison.InvariantCultureIgnoreCase))
        {
            steamId = CSteamID.Nil;
            return true;
        }
        if (str.Equals("OutofDateGS", StringComparison.InvariantCultureIgnoreCase) ||
            str.Equals("out-of-date-gs", StringComparison.InvariantCultureIgnoreCase) ||
            str.Equals("out_of_date_gs", StringComparison.InvariantCultureIgnoreCase))
        {
            steamId = CSteamID.OutofDateGS;
            return true;
        }
        if (str.Equals("LanModeGS", StringComparison.InvariantCultureIgnoreCase) ||
            str.Equals("lan-mode-gs", StringComparison.InvariantCultureIgnoreCase) ||
            str.Equals("lan_mode_gs", StringComparison.InvariantCultureIgnoreCase))
        {
            steamId = CSteamID.LanModeGS;
            return true;
        }
        if (str.Equals("NotInitYetGS", StringComparison.InvariantCultureIgnoreCase) ||
            str.Equals("not-init-yet-gs", StringComparison.InvariantCultureIgnoreCase) ||
            str.Equals("not_init_yet_gs", StringComparison.InvariantCultureIgnoreCase))
        {
            steamId = CSteamID.NotInitYetGS;
            return true;
        }
        if (str.Equals("NonSteamGS", StringComparison.InvariantCultureIgnoreCase) ||
            str.Equals("non-steam-gs", StringComparison.InvariantCultureIgnoreCase) ||
            str.Equals("non_steam_gs", StringComparison.InvariantCultureIgnoreCase))
        {
            steamId = CSteamID.NonSteamGS;
            return true;
        }

        if (ulong.TryParse(str, NumberStyles.Number, CultureInfo.InvariantCulture, out ulong id))
        {
            steamId = new CSteamID(id);
            return true;
        }

        if (uint.TryParse(str, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint acctId1))
        {
            steamId = new CSteamID(new AccountID_t(acctId1), EUniverse.k_EUniversePublic, EAccountType.k_EAccountTypeIndividual);
            return true;
        }

        if (ulong.TryParse(str, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong acctId2))
        {
            steamId = new CSteamID(acctId2);
            return true;
        }

        if (str.StartsWith("STEAM_", StringComparison.InvariantCultureIgnoreCase) && str.Length > 10)
        {
            if (str[7] != ':' || str[9] != ':')
                goto fail;
            char uv = str[6];
            if (!char.IsDigit(uv))
                goto fail;
            EUniverse universe = (EUniverse)(uv - 48);
            if (universe == EUniverse.k_EUniverseInvalid)
                universe = EUniverse.k_EUniversePublic;

            bool y;
            if (str[8] == '1')
                y = true;
            else if (str[8] == '0')
                y = false;
            else goto fail;
            if (!uint.TryParse(str.Substring(10), NumberStyles.Number, CultureInfo.InvariantCulture, out uint acctId))
                goto fail;

            steamId = new CSteamID(new AccountID_t((uint)(acctId * 2 + (y ? 1 : 0))), universe,
                EAccountType.k_EAccountTypeIndividual);
            return true;
        }

        if (str.Length > 8 && str[0] == '[')
        {
            if (str[2] != ':' || str[4] != ':' || str[str.Length - 1] != ']')
                goto fail;
            EAccountType type;
            char c = str[1];
            if (c is 'I' or 'i')
                type = EAccountType.k_EAccountTypeInvalid;
            else if (c == 'U')
                type = EAccountType.k_EAccountTypeIndividual;
            else if (c == 'M')
                type = EAccountType.k_EAccountTypeMultiseat;
            else if (c == 'G')
                type = EAccountType.k_EAccountTypeGameServer;
            else if (c == 'A')
                type = EAccountType.k_EAccountTypeAnonGameServer;
            else if (c == 'P')
                type = EAccountType.k_EAccountTypePending;
            else if (c == 'C')
                type = EAccountType.k_EAccountTypeContentServer;
            else if (c == 'g')
                type = EAccountType.k_EAccountTypeClan;
            else if (c is 'T' or 'L' or 'c')
                type = EAccountType.k_EAccountTypeChat;
            else if (c == 'a')
                type = EAccountType.k_EAccountTypeAnonUser;
            else goto fail;
            char uv = str[3];
            if (!char.IsDigit(uv))
                goto fail;
            uint acctId;
            if (str[str.Length - 3] != ':')
            {
                if (!uint.TryParse(str.Substring(5, str.Length - 6), NumberStyles.Number, CultureInfo.InvariantCulture,
                        out acctId))
                    goto fail;
            }
            else
            {
                if (!uint.TryParse(str.Substring(5, str.Length - 8), NumberStyles.Number, CultureInfo.InvariantCulture,
                        out acctId))
                    goto fail;
                acctId *= 2;
                uv = str[str.Length - 2];
                if (uv == '1')
                    ++acctId;
                else if (uv != '0')
                    goto fail;
            }

            EUniverse universe = (EUniverse)(uv - 48);
            if (universe == EUniverse.k_EUniverseInvalid)
                universe = EUniverse.k_EUniversePublic;

            steamId = new CSteamID(new AccountID_t(acctId), universe, type);
            return true;
        }

        fail:
        steamId = CSteamID.Nil;
        return false;
    }
    public static unsafe bool TryParseHexColor32(string hex, out Color32 color)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            color = default;
            return false;
        }

        bool res = true;
        fixed (char* ptr2 = hex)
        {
            int offset = *ptr2 == '#' ? 1 : 0;
            char* ptr = ptr2 + offset;
            switch (hex.Length - offset)
            {
                case 1: // w
                    byte r = CharToHex(ptr, false);
                    color = new Color32(r, r, r, byte.MaxValue);
                    return res;
                case 2: // wa
                    r = CharToHex(ptr, false);
                    byte a = CharToHex(ptr + 1, false);
                    color = new Color32(r, r, r, a);
                    return res;
                case 3: // rgb
                    r = CharToHex(ptr, false);
                    byte g = CharToHex(ptr + 1, false);
                    byte b = CharToHex(ptr + 2, false);
                    color = new Color32(r, g, b, byte.MaxValue);
                    return res;
                case 4: // rgba
                    r = CharToHex(ptr, false);
                    g = CharToHex(ptr + 1, false);
                    b = CharToHex(ptr + 2, false);
                    a = CharToHex(ptr + 3, false);
                    color = new Color32(r, g, b, a);
                    return res;
                case 6: // rrggbb
                    r = CharToHex(ptr, true);
                    g = CharToHex(ptr + 2, true);
                    b = CharToHex(ptr + 4, true);
                    color = new Color32(r, g, b, byte.MaxValue);
                    return res;
                case 8: // rrggbbaa
                    r = CharToHex(ptr, true);
                    g = CharToHex(ptr + 2, true);
                    b = CharToHex(ptr + 4, true);
                    a = CharToHex(ptr + 6, true);
                    color = new Color32(r, g, b, a);
                    return res;
            }
        }

        color = default;
        return false;

        byte CharToHex(char* c, bool dual)
        {
            if (dual)
            {
                int c2 = *c;
                byte b1;
                if (c2 is > 96 and < 103)
                    b1 = (byte)((c2 - 87) * 0x10);
                else if (c2 is > 64 and < 71)
                    b1 = (byte)((c2 - 55) * 0x10);
                else if (c2 is > 47 and < 58)
                    b1 = (byte)((c2 - 48) * 0x10);
                else
                {
                    res = false;
                    return 0;
                }

                c2 = *(c + 1);
                if (c2 is > 96 and < 103)
                    return (byte)(b1 + (c2 - 87));
                if (c2 is > 64 and < 71)
                    return (byte)(b1 + (c2 - 55));
                if (c2 is > 47 and < 58)
                    return (byte)(b1 + (c2 - 48));
                res = false;
            }
            else
            {
                int c2 = *c;
                if (c2 is > 96 and < 103)
                    return (byte)((c2 - 87) * 0x10);
                if (c2 is > 64 and < 71)
                    return (byte)((c2 - 55) * 0x10);
                if (c2 is > 47 and < 58)
                    return (byte)((c2 - 48) * 0x10);
                res = false;
            }

            return 0;
        }
    }
    private static readonly char[] SplitChars = { ',' };
    private static KeyValuePair<string, Color>[]? _presets;
    private static void CheckPresets()
    {
        if (_presets != null)
            return;
        PropertyInfo[] props = typeof(Color).GetProperties(BindingFlags.Static | BindingFlags.Public)
            .Where(x => x.PropertyType == typeof(Color)).Where(x => x.GetMethod != null).ToArray();
        _presets = new KeyValuePair<string, Color>[props.Length];
        for (int i = 0; i < props.Length; ++i)
            _presets[i] = new KeyValuePair<string, Color>(props[i].Name.ToLowerInvariant(), (Color)props[i].GetMethod.Invoke(null, Array.Empty<object>()));
    }
    [Pure]
    public static bool TryParseColor(string str, out Color color)
    {
        Color32 color32;
        if (str.Length > 0 && str[0] == '#')
        {
            if (TryParseHexColor32(str, out color32))
            {
                color = color32;
                return true;
            }

            color = default;
            return false;
        }
        string[] strs = str.Split(SplitChars, StringSplitOptions.RemoveEmptyEntries);
        if (strs.Length is 3 or 4)
        {
            bool hsv = strs[0].StartsWith("hsv");
            float a = 1f;
            int ind = strs[0].IndexOf('(');
            if (ind != -1 && strs[0].Length > ind + 1) strs[0] = strs[0].Substring(ind + 1);
            if (!float.TryParse(strs[0], NumberStyles.Number, CultureInfo.InvariantCulture, out float r))
                goto fail;
            if (!float.TryParse(strs[1], NumberStyles.Number, CultureInfo.InvariantCulture, out float g))
                goto fail;
            if (!float.TryParse(strs[2].Replace(')', ' '), NumberStyles.Number, CultureInfo.InvariantCulture, out float b))
                goto fail;
            if (strs.Length > 3 && !float.TryParse(strs[3].Replace(')', ' '), NumberStyles.Number, CultureInfo.InvariantCulture, out a))
                goto fail;

            if (hsv)
            {
                color = Color.HSVToRGB(r, g, b, false) with { a = a };
                return true;
            }
            
            r = Mathf.Clamp01(r);
            g = Mathf.Clamp01(g);
            b = Mathf.Clamp01(b);
            color = new Color(r, g, b, a);
            return true;
            fail:
            color = default;
            return false;
        }
        
        if (TryParseHexColor32(str, out color32))
        {
            color = color32;
            return true;
        }

        CheckPresets();
        for (int i = 0; i < _presets!.Length; ++i)
        {
            if (string.Compare(_presets[i].Key, str, CultureInfo.InvariantCulture,
                CompareOptions.IgnoreCase | CompareOptions.IgnoreKanaType | CompareOptions.IgnoreWidth | CompareOptions.IgnoreNonSpace) == 0)
            {
                color = _presets[i].Value;
                return true;
            }
        }

        color = default;
        return false;
    }
    [Pure]
    public static bool TryParseColor32(string str, out Color32 color)
    {
        if (str.Length > 0 && str[0] == '#')
        {
            if (TryParseHexColor32(str, out color))
                return true;

            color = default;
            return false;
        }
        string[] strs = str.Split(SplitChars, StringSplitOptions.RemoveEmptyEntries);
        if (strs.Length is 3 or 4)
        {
            bool hsv = strs[0].StartsWith("hsv");
            byte a = 1;
            int ind = strs[0].IndexOf('(');
            if (ind != -1 && strs[0].Length > ind + 1) strs[0] = strs[0].Substring(ind + 1);
            if (!byte.TryParse(strs[0], NumberStyles.Number, CultureInfo.InvariantCulture, out byte r))
                goto fail;
            if (!byte.TryParse(strs[1], NumberStyles.Number, CultureInfo.InvariantCulture, out byte g))
                goto fail;
            if (!byte.TryParse(strs[2].Replace(')', ' '), NumberStyles.Number, CultureInfo.InvariantCulture, out byte b))
                goto fail;
            if (strs.Length > 3 && !byte.TryParse(strs[3].Replace(')', ' '), NumberStyles.Number, CultureInfo.InvariantCulture, out a))
                goto fail;

            if (hsv)
            {
                color = Color.HSVToRGB(r, g, b, false) with { a = a };
                return true;
            }

            color = new Color32(r, g, b, a);
            return true;
            fail:
            color = default;
            return false;
        }
        
        if (TryParseHexColor32(str, out color))
            return true;

        CheckPresets();
        for (int i = 0; i < _presets!.Length; ++i)
        {
            if (string.Compare(_presets[i].Key, str, CultureInfo.InvariantCulture,
                    CompareOptions.IgnoreCase | CompareOptions.IgnoreKanaType | CompareOptions.IgnoreWidth | CompareOptions.IgnoreNonSpace) == 0)
            {
                color = _presets[i].Value;
                return true;
            }
        }

        color = default;
        return false;
    }

    public static bool QueueOnMainThread(Action action, bool skipFrame = false, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (!skipFrame && DevkitServerModule.IsMainThread)
        {
            action();
            return false;
        }

        MainThreadTask.MainThreadResult res = new MainThreadTask.MainThreadResult(new MainThreadTask(skipFrame, token));
        res.OnCompleted(action);
        return true;
    }
    public static MainThreadTask ToUpdate(CancellationToken token = default) => DevkitServerModule.IsMainThread ? MainThreadTask.CompletedNoSkip : new MainThreadTask(false, token);
    public static MainThreadTask SkipFrame(CancellationToken token = default) => new MainThreadTask(true, token);
#if SERVER
    public static bool TryGetConnections(IPAddress ip, out List<ITransportConnection> results)
    {
        ip = ip.MapToIPv4();
        results = new List<ITransportConnection>();
        for (int i = 0; i < Provider.clients.Count; ++i)
        {
            Logger.LogDebug("[GET CONNECTION FROM IP] " + Provider.clients[i].transportConnection.GetAddress().Format() + " vs " + ip.Format() + ":");
            if (Provider.clients[i].transportConnection.GetAddress().MapToIPv4().Equals(ip))
            {
                Logger.LogDebug("[GET CONNECTION FROM IP]  Matches");
                results.Add(Provider.clients[i].transportConnection);
                break;
            }
        }
        
        for (int i = 0; i < Provider.pending.Count; ++i)
        {
            Logger.LogDebug("[GET CONNECTION FROM IP] " + Provider.pending[i].transportConnection.GetAddress().Format() + " vs " + ip.Format() + ":");
            if (Provider.pending[i].transportConnection.GetAddress().MapToIPv4().Equals(ip))
            {
                Logger.LogDebug("[GET CONNECTION FROM IP]  Matches");
                results.Add(Provider.pending[i].transportConnection);
                break;
            }
        }

        for (int i = 0; i < PatchesMain.PendingConnections.Count; ++i)
        {
            Logger.LogDebug("[GET CONNECTION FROM IP] " + PatchesMain.PendingConnections[i].GetAddress().MapToIPv4().Format() + " vs " + ip.Format() + ":");
            if (PatchesMain.PendingConnections[i].GetAddress().MapToIPv4().Equals(ip))
            {
                Logger.LogDebug("[GET CONNECTION FROM IP]  Matches");
                results.Add(PatchesMain.PendingConnections[i]);
                break;
            }
        }
        
        return results.Count != 0;
    }
#endif
    public static void WriteData(string path, IDatNode data, System.Text.Encoding? encoding = null)
    {
        encoding ??= System.Text.Encoding.UTF8;
        string? dir = Path.GetDirectoryName(path);
        if (dir != null)
            Directory.CreateDirectory(dir);
        using FileStream str = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        using StreamWriter writer = new StreamWriter(str, encoding);
        using (DatWriter datWriter = new DatWriter(writer))
        {
            datWriter.WriteNode(data);
        }
        writer.Flush();
    }
    [Pure]
    public static unsafe bool UserSteam64(this ulong s64) => ((CSteamID*)&s64)->GetEAccountType() == EAccountType.k_EAccountTypeIndividual;
    [Pure]
    public static bool UserSteam64(this CSteamID s64) => s64.GetEAccountType() == EAccountType.k_EAccountTypeIndividual;
    [Pure]
    public static string GetPlayerSavedataLocation(ulong s64, string path, int characterId = 0) => PlayerSavedata.hasSync
        ? (Path.Combine(ReadWrite.PATH, "Sync", s64 + "_" + characterId, Level.info.name, path))
        : (Path.Combine(ReadWrite.PATH, ServerSavedata.directory, Provider.serverID, "Players", s64 + "_" + characterId, Level.info.name, path));
    public static void UpdateLocalizationFile(ref Local read, LocalDatDictionary @default, string directory)
    {
        LocalDatDictionary def = @default;
        DatDictionary @new = new DatDictionary();
        DatDictionary def2 = new DatDictionary();
        foreach (KeyValuePair<string, string> pair in def)
        {
            if (!read.has(pair.Key))
                @new.Add(pair.Key, new DatValue(pair.Value));
            else @new.Add(pair.Key, new DatValue(read.format(pair.Key)));
            def2.Add(pair.Key, new DatValue(pair.Value));
        }

        read = new Local(@new, def2);
        string path = Path.Combine(directory, "English.dat");
        bool eng = Provider.language.Equals("English", StringComparison.InvariantCultureIgnoreCase);
        if (!File.Exists(path))
        {
            WriteData(path, eng ? @new : def2);
            if (eng) return;
        }

        path = Path.Combine(directory, Provider.language + ".dat");
        WriteData(path, @new);
    }
    private const string NullValue = "null";
    [Pure]
    public static string Translate(this Local local, string format) => local.format(format);
    [Pure]
    public static string Translate(this Local local, string format, object? arg0) => local.format(format, arg0 ?? NullValue);
    [Pure]
    public static string Translate(this Local local, string format, object? arg0, object? arg1) => local.format(format, arg0 ?? NullValue, arg1 ?? NullValue);
    [Pure]
    public static string Translate(this Local local, string format, object? arg0, object? arg1, object? arg2) => local.format(format, arg0 ?? NullValue, arg1 ?? NullValue, arg2 ?? NullValue);
    [Pure]
    public static string Translate(this Local local, string format, params object?[] args)
    {
        for (int i = 0; i < args.Length; ++i)
            args[i] ??= NullValue;
        
        return local.format(format, args);
    }
    [Pure]
    public static string RemoveRichText(string text)
    {
        return RemoveRichTextRegex.Replace(text, string.Empty);
    }
    /// <remarks>Does not include &lt;#ffffff&gt; colors.</remarks>
    [Pure]
    public static string RemoveTMProRichText(string text)
    {
        return RemoveTMProRichTextRegex.Replace(text, string.Empty);
    }
    [Pure]
    public static Color GetColor(this IDevkitServerPlugin? plugin) => plugin == null ? DevkitServerModule.PluginColor : (plugin is IDevkitServerColorPlugin p ? p.Color : Plugin.DefaultColor);
    [Pure]
    public static LevelObject? FindLevelObject(Transform t)
    {
        if (t == null || !Regions.tryGetCoordinate(t.position, out byte x, out byte y))
            return null;
        List<LevelObject> region = LevelObjects.objects[x, y];
        for (int i = 0; i < region.Count; ++i)
        {
            if (region[i].transform == t)
            {
                return region[i];
            }
        }
        for (int i = 0; i < region.Count; ++i)
        {
            if (region[i].placeholderTransform == t)
            {
                return region[i];
            }
        }
        for (int i = 0; i < region.Count; ++i)
        {
            if (region[i].skybox == t)
            {
                return region[i];
            }
        }

        int ws = Regions.WORLD_SIZE;
        for (int x2 = 0; x2 < ws; ++x2)
        {
            for (int y2 = 0; y2 < ws; ++y2)
            {
                if (x2 == x && y2 == y) continue;
                region = LevelObjects.objects[x, y];
                for (int i = 0; i < region.Count; ++i)
                {
                    if (region[i].transform == t)
                        return region[i];
                }
            }
        }
        
        for (int x2 = 0; x2 < ws; ++x2)
        {
            for (int y2 = 0; y2 < ws; ++y2)
            {
                if (x2 == x && y2 == y) continue;
                region = LevelObjects.objects[x, y];
                for (int i = 0; i < region.Count; ++i)
                {
                    if (region[i].placeholderTransform == t)
                        return region[i];
                }
            }
        }
        
        for (int x2 = 0; x2 < ws; ++x2)
        {
            for (int y2 = 0; y2 < ws; ++y2)
            {
                if (x2 == x && y2 == y) continue;
                region = LevelObjects.objects[x, y];
                for (int i = 0; i < region.Count; ++i)
                {
                    if (region[i].skybox == t)
                        return region[i];
                }
            }
        }

        return null;
    }

    [Pure]
    public static Transform? GetTransform(this LevelObject obj)
    {
        if (obj.transform != null)
            return obj.transform;
        
        if (obj.skybox != null)
            return obj.skybox;

        if (obj.placeholderTransform != null)
            return obj.placeholderTransform;

        return null;
    }

    public static void CustomDisconnect(
#if SERVER
        EditorUser user,
#endif
        string message)
    {
#if CLIENT
        Provider.connectionFailureInfo = ESteamConnectionFailureInfo.KICKED;
        Provider.RequestDisconnect(Provider.connectionFailureReason = message);
#else
        if (Provider.pending.Any(x => x.playerID.steamID.m_SteamID == user.SteamId.m_SteamID))
            Provider.reject(user.SteamId, ESteamRejection.PLUGIN, message);
        else
            Provider.kick(user.SteamId, message);
#endif
    }
    /// <summary>
    /// Tries to create a directory.
    /// </summary>
    /// <remarks>Will not throw an exception. Use <see cref="Directory.CreateDirectory(string)"/> if you want an exception.</remarks>
    /// <param name="relative">If the path is relative to <see cref="ReadWrite.PATH"/>.</param>
    /// <param name="path">Relative or absolute path to a directory.</param>
    /// <returns><see langword="true"/> if the directory is created or already existed, otherwise false.</returns>
    public static bool CheckDirectory(bool relative, string path) => CheckDirectory(relative, false, path, null);
    internal static bool CheckDirectory(bool relative, bool fault, string path, MemberInfo? member)
    {
        if (path == null)
            return false;
        try
        {
            if (relative)
                path = Path.Combine(ReadWrite.PATH, path);
            if (Directory.Exists(path))
            {
                if (member == null)
                    Logger.LogDebug($"[CHECK DIR] Directory checked: {path.Format(false)}.");
                else
                    Logger.LogDebug($"[CHECK DIR] Directory checked: {path.Format(false)} from {member.Format()}.");
                return true;
            }

            Directory.CreateDirectory(path);
            if (member == null)
                Logger.LogInfo($"[CHECK DIR] Directory created: {path.Format(false)}.");
            else
                Logger.LogInfo($"[CHECK DIR] Directory created: {path.Format(false)} from {member.Format()}.");
            return true;
        }
        catch (Exception ex)
        {
            if (member == null)
                Logger.LogError($"[CHECK DIR] Unable to create directory: {path.Format(false)}.");
            else
                Logger.LogError($"[CHECK DIR] Unable to create directory: {path.Format(false)} from {member.Format()}.");
            Logger.LogError(ex);
            if (fault)
                DevkitServerModule.Fault();
            return false;
        }
    }
    [Pure]
    public static bool IsNearlyIdentity(this Quaternion q, float tolerance = 0.001f)
    {
        return q.x > -tolerance && q.x < tolerance && q.y > -tolerance && q.y < tolerance && q.z > -tolerance && q.z < tolerance && q.w - 1f > -tolerance && q.w - 1f < tolerance;
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class CreateDirectoryAttribute : Attribute
{
    public bool RelativeToGameDir { get; set; }
    internal bool FaultOnFailure { get; set; } = true;
    public static void CreateInAssembly(Assembly assembly) => CreateInAssembly(assembly, false);
    internal static void CreateInAssembly(Assembly assembly, bool allowFault)
    {
        List<Type> types = Accessor.GetTypesSafe(assembly, false);
        for (int index = 0; index < types.Count; index++)
        {
            Type type = types[index];
            FieldInfo[] fields = type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            for (int f = 0; f < fields.Length; ++f)
            {
                FieldInfo field = fields[f];
                if (!field.IsStatic || IsDefined(field, typeof(IgnoreAttribute)) || GetCustomAttribute(field, typeof(CreateDirectoryAttribute)) is not CreateDirectoryAttribute cdir)
                    continue;
                if (typeof(string).IsAssignableFrom(field.FieldType))
                {
                    string? path = (string?)field.GetValue(null);
                    if (path == null)
                        Logger.LogWarning($"[CHECK DIR] Unable to check directory for {field.Format()}, field returned {((object?)null).Format()}.");
                    else DevkitServerUtility.CheckDirectory(cdir.RelativeToGameDir, allowFault && cdir.FaultOnFailure, path, field);
                }
                else if (typeof(FileInfo).IsAssignableFrom(field.FieldType))
                {
                    FileInfo? fileInfo = (FileInfo?)field.GetValue(null);
                    cdir.RelativeToGameDir = false;
                    string? file = fileInfo?.DirectoryName;
                    if (file == null)
                    {
                        if (fileInfo == null)
                            Logger.LogWarning($"[CHECK DIR] Unable to check directory for {field.Format()}, field returned {((object?)null).Format()}.");
                    }
                    else DevkitServerUtility.CheckDirectory(false, allowFault && cdir.FaultOnFailure, file, field);
                }
                else if (typeof(DirectoryInfo).IsAssignableFrom(field.FieldType))
                {
                    string? dir = ((DirectoryInfo?)field.GetValue(null))?.FullName;
                    cdir.RelativeToGameDir = false;
                    if (dir == null)
                        Logger.LogWarning($"[CHECK DIR] Unable to check directory for {field.Format()}, field returned {((object?)null).Format()}.");
                    else DevkitServerUtility.CheckDirectory(false, allowFault && cdir.FaultOnFailure, dir, field);
                }
                else
                {
                    Logger.LogWarning($"[CHECK DIR] Unable to check directory for {field.Format()}, valid on types: " +
                                      $"{typeof(string).Format()}, {typeof(FileInfo).Format()}, or {typeof(DirectoryInfo).Format()}.");
                }
            }
            PropertyInfo[] properties = type.GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            for (int p = 0; p < properties.Length; ++p)
            {
                PropertyInfo property = properties[p];
                if (property.GetMethod == null || property.GetMethod.IsStatic || property.GetIndexParameters() is { Length: > 0 } ||
                    IsDefined(property, typeof(IgnoreAttribute)) || GetCustomAttribute(property, typeof(CreateDirectoryAttribute)) is not CreateDirectoryAttribute cdir)
                    continue;
                if (typeof(string).IsAssignableFrom(property.PropertyType))
                {
                    string? path = (string?)property.GetMethod?.Invoke(null, Array.Empty<object>());
                    if (path == null)
                        Logger.LogWarning($"[CHECK DIR] Unable to check directory for {property.Format()}, field returned {((object?)null).Format()}.");
                    else DevkitServerUtility.CheckDirectory(cdir.RelativeToGameDir, allowFault && cdir.FaultOnFailure, path, property);
                }
                else if (typeof(FileInfo).IsAssignableFrom(property.PropertyType))
                {
                    FileInfo? fileInfo = (FileInfo?)property.GetMethod?.Invoke(null, Array.Empty<object>());
                    string? file = fileInfo?.DirectoryName;
                    if (file == null)
                    {
                        if (fileInfo == null)
                            Logger.LogWarning($"[CHECK DIR] Unable to check directory for {property.Format()}, field returned {((object?)null).Format()}.");
                    }
                    else DevkitServerUtility.CheckDirectory(false, allowFault && cdir.FaultOnFailure, file, property);
                }
                else if (typeof(DirectoryInfo).IsAssignableFrom(property.PropertyType))
                {
                    string? dir = ((DirectoryInfo?)property.GetMethod?.Invoke(null, Array.Empty<object>()))?.FullName;
                    if (dir == null)
                        Logger.LogWarning($"[CHECK DIR] Unable to check directory for {property.Format()}, field returned {((object?)null).Format()}.");
                    else DevkitServerUtility.CheckDirectory(false, allowFault && cdir.FaultOnFailure, dir, property);
                }
                else
                {
                    Logger.LogWarning($"[CHECK DIR] Unable to check directory for {property.Format()}, valid on types: " +
                                      $"{typeof(string).Format()}, {typeof(FileInfo).Format()}, or {typeof(DirectoryInfo).Format()}.");
                }
            }
        }
    }
}

public static class AssetTypeHelper<TAsset>
{
    public static readonly EAssetType Type = GetAssetType();
    private static EAssetType GetAssetType()
    {
        Type c = typeof(TAsset);
        if (typeof(ItemAsset).IsAssignableFrom(c))
            return EAssetType.ITEM;
        if (typeof(EffectAsset).IsAssignableFrom(c))
            return EAssetType.EFFECT;
        if (typeof(VehicleAsset).IsAssignableFrom(c))
            return EAssetType.VEHICLE;
        if (typeof(ObjectAsset).IsAssignableFrom(c))
            return EAssetType.OBJECT;
        if (typeof(ResourceAsset).IsAssignableFrom(c))
            return EAssetType.RESOURCE;
        if (typeof(AnimalAsset).IsAssignableFrom(c))
            return EAssetType.ANIMAL;
        if (typeof(MythicAsset).IsAssignableFrom(c))
            return EAssetType.MYTHIC;
        if (typeof(SkinAsset).IsAssignableFrom(c))
            return EAssetType.SKIN;
        if (typeof(SpawnAsset).IsAssignableFrom(c))
            return EAssetType.SPAWN;
        return typeof(DialogueAsset).IsAssignableFrom(c) || typeof(VendorAsset).IsAssignableFrom(c) || typeof(QuestAsset).IsAssignableFrom(c) ? EAssetType.NPC : EAssetType.NONE;
    }
}