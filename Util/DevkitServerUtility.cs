using DevkitServer.API;
using DevkitServer.Multiplayer.Levels;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Util.Encoding;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
#if SERVER
using DevkitServer.Configuration;
using DevkitServer.Patches;
using DevkitServer.Players;
using System.Net;
#endif

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
    public static Regex RemoveRichTextRegex { get; } =
        new Regex(@"(?<!(?:\<noparse\>(?!\<\/noparse\>)).*)\<\/{0,1}(?:(?:color=\""{0,1}[#a-z]{0,9}\""{0,1})|(?:color)|(?:size=\""{0,1}\d+\""{0,1})|(?:size)|(?:alpha)|(?:alpha=#[0-f]{1,2})|(?:#.{3,8})|(?:[isub])|(?:su[pb])|(?:lowercase)|(?:uppercase)|(?:smallcaps))\>", RegexOptions.IgnoreCase);
    public static Regex RemoveTMProRichTextRegex { get; } =
        new Regex(@"(?<!(?:\<noparse\>(?!\<\/noparse\>)).*)\<\/{0,1}(?:(?:noparse)|(?:alpha)|(?:alpha=#[0-f]{1,2})|(?:[su])|(?:su[pb])|(?:lowercase)|(?:uppercase)|(?:smallcaps))\>", RegexOptions.IgnoreCase);
    [Pure]
    public static string FormatBytes(long length, int decimals = 1)
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

        return len.ToString("N" + Math.Max(0, decimals).ToString(CultureInfo.InvariantCulture)) + " " + _sizeCodes[inc];
    }
    [Pure]
    public static Bounds InflateBounds(in Bounds bounds)
    {
        Vector3 c = bounds.center;
        Vector3 e = bounds.extents;
        return new Bounds(new Vector3(Mathf.Round(c.x), Mathf.Round(c.y), Mathf.Round(c.z)), new Vector3((e.x + 0.5f).CeilToIntIgnoreSign(), (e.y + 0.5f).CeilToIntIgnoreSign(), (e.z + 0.5f).CeilToIntIgnoreSign()));
    }
    [Pure]
    public static uint ReverseUInt32(uint val) => ((val >> 24) & 0xFF) | (((val >> 16) & 0xFF) << 8) | (((val >> 8) & 0xFF) << 16) | (val << 24);
    [Pure]
    public static Vector2 ToVector2(this in Vector3 v3) => new Vector2(v3.x, v3.z);
    [Pure]
    public static Vector3 ToVector3(this in Vector2 v2) => new Vector3(v2.x, 0f, v2.y);
    [Pure]
    public static Vector3 ToVector3(this in Vector2 v2, float y) => new Vector3(v2.x, y, v2.y);
    [Pure]
    public static bool IsNearlyEqual(this in Quaternion quaternion, in Quaternion other, float tolerance = 0.001f)
    {
        return
            Mathf.Abs(quaternion.x - other.x) < tolerance &&
            Mathf.Abs(quaternion.y - other.y) < tolerance &&
            Mathf.Abs(quaternion.z - other.z) < tolerance &&
            Mathf.Abs(quaternion.w - other.w) < tolerance;
    }
    public static bool TryParseSteamId(string str, out CSteamID steamId)
    {
        if (str.Length > 2 && str[0] is 'N' or 'n' or 'O' or 'o' or 'L' or 'l' or 'z' or 'Z')
        {
            if (str.Equals("Nil", StringComparison.InvariantCultureIgnoreCase) ||
                str.Equals("zero", StringComparison.InvariantCultureIgnoreCase) ||
                str.Equals("null", StringComparison.InvariantCultureIgnoreCase))
            {
                steamId = CSteamID.Nil;
                return true;
            }
            if (str.Equals("OutofDateGS", StringComparison.InvariantCultureIgnoreCase) ||
                str.Equals("out-of-date-gs", StringComparison.InvariantCultureIgnoreCase) ||
                str.Equals("out of date gs", StringComparison.InvariantCultureIgnoreCase) ||
                str.Equals("out_of_date_gs", StringComparison.InvariantCultureIgnoreCase))
            {
                steamId = CSteamID.OutofDateGS;
                return true;
            }
            if (str.Equals("LanModeGS", StringComparison.InvariantCultureIgnoreCase) ||
                str.Equals("lan-mode-gs", StringComparison.InvariantCultureIgnoreCase) ||
                str.Equals("lan mode gs", StringComparison.InvariantCultureIgnoreCase) ||
                str.Equals("lan_mode_gs", StringComparison.InvariantCultureIgnoreCase))
            {
                steamId = CSteamID.LanModeGS;
                return true;
            }
            if (str.Equals("NotInitYetGS", StringComparison.InvariantCultureIgnoreCase) ||
                str.Equals("not-init-yet-gs", StringComparison.InvariantCultureIgnoreCase) ||
                str.Equals("not init yet gs", StringComparison.InvariantCultureIgnoreCase) ||
                str.Equals("not_init_yet_gs", StringComparison.InvariantCultureIgnoreCase))
            {
                steamId = CSteamID.NotInitYetGS;
                return true;
            }
            if (str.Equals("NonSteamGS", StringComparison.InvariantCultureIgnoreCase) ||
                str.Equals("non-steam-gs", StringComparison.InvariantCultureIgnoreCase) ||
                str.Equals("non steam gs", StringComparison.InvariantCultureIgnoreCase) ||
                str.Equals("non_steam_gs", StringComparison.InvariantCultureIgnoreCase))
            {
                steamId = CSteamID.NonSteamGS;
                return true;
            }
        }

        if (uint.TryParse(str, NumberStyles.Number, CultureInfo.InvariantCulture, out uint acctId1))
        {
            steamId = new CSteamID(new AccountID_t(acctId1), EUniverse.k_EUniversePublic, EAccountType.k_EAccountTypeIndividual);
            return true;
        }

        if (ulong.TryParse(str, NumberStyles.Number, CultureInfo.InvariantCulture, out ulong id))
        {
            steamId = new CSteamID(id);

            // try parse as hex instead
            if (steamId.GetEAccountType() != EAccountType.k_EAccountTypeIndividual)
            {
                if (!ulong.TryParse(str, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out id))
                    return true;
                CSteamID steamid2 = new CSteamID(id);
                if (steamid2.GetEAccountType() == EAccountType.k_EAccountTypeIndividual)
                    steamId = steamid2;
            }
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

            steamId = new CSteamID(new AccountID_t((uint)(acctId * 2 + (y ? 1 : 0))), universe, EAccountType.k_EAccountTypeIndividual);
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
                if (!uint.TryParse(str.Substring(5, str.Length - 6), NumberStyles.Number, CultureInfo.InvariantCulture, out acctId))
                    goto fail;
            }
            else
            {
                if (!uint.TryParse(str.Substring(5, str.Length - 8), NumberStyles.Number, CultureInfo.InvariantCulture, out acctId))
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
                    return (byte)((c2 - 87) * 0x10 + (c2 - 87));
                if (c2 is > 64 and < 71)
                    return (byte)((c2 - 55) * 0x10 + (c2 - 55));
                if (c2 is > 47 and < 58)
                    return (byte)((c2 - 48) * 0x10 + (c2 - 48));
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
            float a = 255f;
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
                color = Color.HSVToRGB(r / 360f, g / 100f, b / 100f, false) with { a = a / 255f };
                return true;
            }
            
            r = Mathf.Clamp01(r / 255f);
            g = Mathf.Clamp01(g / 255f);
            b = Mathf.Clamp01(b / 255f);
            a = Mathf.Clamp01(a / 255f);
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
            byte a = byte.MaxValue;
            int ind = strs[0].IndexOf('(');
            if (ind != -1 && strs[0].Length > ind + 1) strs[0] = strs[0].Substring(ind + 1);
            if (!int.TryParse(strs[0], NumberStyles.Number, CultureInfo.InvariantCulture, out int r))
                goto fail;
            if (!byte.TryParse(strs[1], NumberStyles.Number, CultureInfo.InvariantCulture, out byte g))
                goto fail;
            if (!byte.TryParse(strs[2].Replace(')', ' '), NumberStyles.Number, CultureInfo.InvariantCulture, out byte b))
                goto fail;
            if (strs.Length > 3 && !byte.TryParse(strs[3].Replace(')', ' '), NumberStyles.Number, CultureInfo.InvariantCulture, out a))
                goto fail;

            if (hsv)
            {
                color = Color.HSVToRGB(r / 360f, g / 100f, b / 100f, false) with { a = a / 255f };
                return true;
            }

            color = new Color32((byte)(r > 255 ? 255 : (r < 0 ? 0 : r)), g, b, a);
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
#if SERVER
    [Pure]
    public static string GetUserSavedataLocation(ulong s64, string path, int characterId = 0)
    {
        string basePath;
        if (!string.IsNullOrEmpty(DevkitServerConfig.Config.UserSavedataLocationOverride))
        {
            basePath = DevkitServerConfig.Config.UserSavedataLocationOverride!;
            if (!Path.IsPathRooted(basePath))
                basePath = Path.Combine(ReadWrite.PATH, basePath);
        }
        else if (PlayerSavedata.hasSync)
            basePath = Path.Combine(ReadWrite.PATH, "Sync");
        else
            basePath = Path.Combine(ReadWrite.PATH, ServerSavedata.directory, Provider.serverID, "Players");

        // intentionally using cultured toString here since the base game also does
        return Path.Combine(basePath, s64 + "_" + characterId, Level.info.name, path);
    }
#endif

    public static void UpdateLocalizationFile(ref Local read, LocalDatDictionary @default, string directory)
    {
        DatDictionary @new = new DatDictionary();
        DatDictionary def2 = new DatDictionary();
        foreach (KeyValuePair<string, string> pair in @default)
        {
            @new.Add(pair.Key, new DatValue(read.has(pair.Key) ? read.format(pair.Key) : pair.Value));
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
    /// <remarks>Does not include &lt;#ffffff&gt; colors.</remarks>
    [Pure]
    public static string RemoveTMProRichText(string text)
    {
        return RemoveTMProRichTextRegex.Replace(text, string.Empty);
    }
    [Pure]
    public static Color GetColor(this IDevkitServerPlugin? plugin) => plugin == null ? DevkitServerModule.ModuleColor : (plugin is IDevkitServerColorPlugin p ? p.Color : Plugin.DefaultColor);

    public static void CustomDisconnect(
#if SERVER
        EditorUser user,
#endif
        string message) =>
        CustomDisconnect(
#if SERVER
            user,
#endif
        message, ESteamConnectionFailureInfo.KICKED
    );
    public static void CustomDisconnect(
#if SERVER
        EditorUser user,
#endif
        string message, ESteamConnectionFailureInfo failureType)
    {
#if CLIENT
        Provider.connectionFailureInfo = failureType;
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

    /// <remarks>Includes pending connections.</remarks>
    [Pure]
    public static PooledTransportConnectionList GetAllConnections()
    {
        ThreadUtil.assertIsGameThread();
        
        PooledTransportConnectionList list = NetFactory.GetPooledTransportConnectionList(Provider.clients.Count + EditorLevel.PendingToReceiveActions.Count);
        for (int i = 0; i < Provider.clients.Count; ++i)
            list.Add(Provider.clients[i].transportConnection);
        
        for (int i = 0; i < EditorLevel.PendingToReceiveActions.Count; ++i)
            list.Add(EditorLevel.PendingToReceiveActions[i]);

        return list;
    }
    /// <remarks>Includes pending connections.</remarks>
    [Pure]
    public static PooledTransportConnectionList GetAllConnections(ITransportConnection exclude)
    {
        ThreadUtil.assertIsGameThread();

        bool found = false;
        PooledTransportConnectionList list = NetFactory.GetPooledTransportConnectionList(Provider.clients.Count + EditorLevel.PendingToReceiveActions.Count);
        for (int i = 0; i < Provider.clients.Count; ++i)
        {
            ITransportConnection c = Provider.clients[i].transportConnection;
            if (!found && c.Equals(exclude))
            {
                found = true;
                continue;
            }
            list.Add(c);
        }
        
        for (int i = 0; i < EditorLevel.PendingToReceiveActions.Count; ++i)
        {
            ITransportConnection c = EditorLevel.PendingToReceiveActions[i];
            if (!found && c.Equals(exclude))
            {
                found = true;
                continue;
            }
            list.Add(c);
        }

        return list;
    }

    [Pure]
    public static string S(this int num) => num == 1 ? string.Empty : "s";
    [Pure]
    public static string UpperS(this int num) => num == 1 ? string.Empty : "S";
    [Pure]
    public static double GetElapsedMilliseconds(this Stopwatch stopwatch) => stopwatch.ElapsedTicks / (double)Stopwatch.Frequency * 1000d;
    [Pure]
    public static DateTime? FindNextSchedule(DateTime[] schedule, bool utc, ScheduleInterval interval)
    {
        if (schedule == null)
            throw new ArgumentNullException(nameof(schedule));
        if (schedule.Length == 0)
            return null;
        DateTime now = utc ? DateTime.UtcNow : DateTime.Now;
        for (int i = 0; i < schedule.Length; ++i)
        {
            DateTime contender = schedule[i];
            switch (interval)
            {
                case ScheduleInterval.Daily:
                    contender = new DateTime(now.Year, now.Month, now.Day, contender.Hour, contender.Minute, contender.Second, utc ? DateTimeKind.Utc : DateTimeKind.Local);
                    goto default;
                case ScheduleInterval.Monthly:
                    if (DateTime.DaysInMonth(now.Year, now.Month) > contender.Day)
                        continue;
                    contender = new DateTime(now.Year, now.Month, contender.Day, contender.Hour, contender.Minute, contender.Second, utc ? DateTimeKind.Utc : DateTimeKind.Local);
                    goto default;
                case ScheduleInterval.Yearly:
                    if (DateTime.DaysInMonth(now.Year, contender.Month) > contender.Day) // feb 29th
                        continue;
                    contender = new DateTime(now.Year, contender.Month, contender.Day, contender.Hour, contender.Minute, contender.Second, utc ? DateTimeKind.Utc : DateTimeKind.Local);
                    goto default;
                case ScheduleInterval.Weekly:
                    contender = new DateTime(now.Year, now.Month, now.Day, contender.Hour, contender.Minute, contender.Second, utc ? DateTimeKind.Utc : DateTimeKind.Local)
                        .AddDays(Math.Min(contender.Day, 7) - 1 - (int)now.DayOfWeek);
                    goto default;
                case ScheduleInterval.None:
                default:
                    if (now < contender)
                        return contender;
                    break;
            }
        }
        
        if (interval is ScheduleInterval.Daily or ScheduleInterval.Weekly or ScheduleInterval.Monthly or ScheduleInterval.Yearly)
        {
            DateTime next = schedule[0];
            return interval switch
            {
                ScheduleInterval.Daily => new DateTime(now.Year, now.Month, now.Day, next.Hour, next.Minute, next.Second, utc ? DateTimeKind.Utc : DateTimeKind.Local)
                    .AddDays(1d),
                ScheduleInterval.Monthly => new DateTime(now.Year, now.Month, next.Day, next.Hour, next.Minute, next.Second, utc ? DateTimeKind.Utc : DateTimeKind.Local)
                    .AddMonths(1),
                ScheduleInterval.Yearly => new DateTime(now.Year, next.Month, next.Day, next.Hour, next.Minute, next.Second, utc ? DateTimeKind.Utc : DateTimeKind.Local)
                    .AddYears(1),
                _ /* Weekly */ => new DateTime(now.Year, now.Month, now.Day, next.Hour, next.Minute, next.Second, utc ? DateTimeKind.Utc : DateTimeKind.Local)
                    .AddDays(Math.Min(next.Day, 7) + 6 - (int)now.DayOfWeek)
            };
        }
        return null;
    }

    [Pure]
    public static long ConvertTBToB(double tb) => (long)Math.Round(tb * 1000000000000d);
    [Pure]
    public static long ConvertGBToB(double gb) => (long)Math.Round(gb * 1000000000d);
    [Pure]
    public static long ConvertMBToB(double mb) => (long)Math.Round(mb * 1000000d);
    [Pure]
    public static long ConvertKBToB(double kb) => (long)Math.Round(kb * 1000d);
    [Pure]
    public static long ConvertTiBToB(double tib) => (long)Math.Round(tib * 1099511627776d);
    [Pure]
    public static long ConvertGiBToB(double gib) => (long)Math.Round(gib * 1073741824d);
    [Pure]
    public static long ConvertMiBToB(double mib) => (long)Math.Round(mib * 1048576d);
    [Pure]
    public static long ConvertKiBToB(double kib) => (long)Math.Round(kib * 1024d);
    [Pure]
    public static long GetDirectorySize(string directory)
    {
        DirectoryInfo dir = new DirectoryInfo(directory);
        return GetDirectorySize(dir);
    }
    [Pure]
    public static long GetDirectorySize(DirectoryInfo directory)
    {
        if (!directory.Exists)
            return 0L;
        FileSystemInfo[] files = directory.GetFileSystemInfos("*", SearchOption.TopDirectoryOnly);
        long ttl = 0;
        for (int i = 0; i < files.Length; ++i)
        {
            switch (files[i])
            {
                case FileInfo f:
                    ttl += f.Length;
                    break;
                case DirectoryInfo d:
                    ttl += GetDirectorySize(d);
                    break;
            }
        }

        return ttl;
    }
    public static int RemoveAll<T>(this IList<T> list, Predicate<T> selector)
    {
        int c = 0;
        for (int i = list.Count - 1; i >= 0; --i)
        {
            if (selector(list[i]))
            {
                list.RemoveAt(i);
                ++c;
            }
        }

        return c;
    }
    public static T[] ToArrayFast<T>(this List<T> list) => list.Count == 0 ? Array.Empty<T>() : list.ToArray();
    public static void IncreaseCapacity<T>(this List<T> list, int amount)
    {
        if (list.Capacity < amount)
            list.Capacity = amount;
    }
    [Pure]
    public static T? SingleOrDefaultSafe<T>(this IEnumerable<T> enumerable, Predicate<T> predicate)
    {
        /* Why tf does SingleOrDefault throw an exception... */
        bool found = false;
        T? rtn = default;
        if (enumerable is T[] array)
        {
            for (int i = 0; i < array.Length; ++i)
            {
                if (!predicate(array[i]))
                    continue;
                if (found)
                    return default;

                rtn = array[i];
                found = true;
            }
        }
        else if (enumerable is List<T> list)
        {
            for (int i = 0; i < list.Count; ++i)
            {
                if (!predicate(list[i]))
                    continue;
                if (found)
                    return default;

                rtn = list[i];
                found = true;
            }
        }
        else
        {
            foreach (T value in enumerable)
            {
                if (!predicate(value))
                    continue;
                if (found)
                    return default;

                rtn = value;
                found = true;
            }
        }

        return rtn;
    }
    [Pure]
    public static T? SingleOrDefaultSafe<T>(this IEnumerable<T> enumerable)
    {
        if (enumerable is IList<T> list)
            return list.Count == 1 ? list[0] : default;

        if (enumerable is ICollection<T> collection && collection.Count != 1)
            return default;

        bool found = false;
        T? rtn = default;
        foreach (T value in enumerable)
        {
            if (found)
                return default;

            rtn = value;
            found = true;
        }

        return rtn;
    }
    /// <summary>
    /// Creates a copy or moves a file, for example, 'OriginalName' to 'OriginalName Backup', and optionally assigns a number if there are duplicate files.
    /// </summary>
    /// <param name="originalFile">Path to the file to copy or move.</param>
    /// <param name="overwrite">Allows the copy or move operation to just overwrite existing files instead of incrementing a number.</param>
    /// <returns>The path to the newly created or moved file.</returns>
    public static string BackupFile(string originalFile, bool moveInsteadOfCopy, bool overwrite = true)
    {
        string ext = Path.GetExtension(originalFile);
        string? dir = Path.GetDirectoryName(originalFile);
        string fn = Path.GetFileNameWithoutExtension(originalFile) + " Backup";
        if (dir != null)
            fn = Path.Combine(dir, fn);
        if (File.Exists(fn + ext) && !overwrite)
        {
            int num = 0;
            fn += " ";
            while (File.Exists(fn + num.ToString(CultureInfo.InvariantCulture) + ext))
                ++num;
            fn += num.ToString(CultureInfo.InvariantCulture);
        }

        DateTime? lastModified = null;
        try
        {
            lastModified = File.GetLastWriteTimeUtc(originalFile);
        }
        catch
        {
            // ignored
        }

        fn += ext;
        if (moveInsteadOfCopy)
        {
            if (overwrite && File.Exists(fn))
                File.Delete(fn);

            File.Move(originalFile, fn);
        }
        else
        {
            File.Copy(originalFile, fn, overwrite);
        }

        try
        {
            File.SetCreationTimeUtc(fn, DateTime.UtcNow);

            if (lastModified.HasValue)
                File.SetLastWriteTimeUtc(fn, lastModified.Value);
        }
        catch
        {
            // ignored
        }

        return fn;
    }
    /// <summary>
    /// Checks to see if <paramref name="longerPath"/> is a child folder or file of the directory <paramref name="shorterPath"/>.
    /// </summary>
    [Pure]
    public static bool IsChildOf(string? shorterPath, string longerPath, bool includeSubDirectories = true)
    {
        if (string.IsNullOrEmpty(shorterPath))
            return true;
        if (string.IsNullOrEmpty(longerPath))
            return false;
        DirectoryInfo parent = new DirectoryInfo(shorterPath);
        DirectoryInfo child = new DirectoryInfo(longerPath);
        return IsChildOf(parent, child, includeSubDirectories);
    }
    /// <summary>
    /// Checks to see if <paramref name="longerPath"/> is a child folder or file of the directory <paramref name="shorterPath"/>.
    /// </summary>
    [Pure]
    public static bool IsChildOf(DirectoryInfo shorterPath, DirectoryInfo longerPath, bool includeSubDirectories = true)
    {
        if (!includeSubDirectories)
            return longerPath.Parent != null && longerPath.Parent.FullName.Equals(shorterPath.FullName, StringComparison.Ordinal);
        while (longerPath.Parent != null)
        {
            if (longerPath.Parent.FullName.Equals(shorterPath.FullName, StringComparison.Ordinal))
                return true;
            longerPath = longerPath.Parent;
        }

        return false;
    }
    /// <summary>
    /// Gets the path to a file or directory relative to <paramref name="relativeTo"/> of <paramref name="path"/>.
    /// </summary>
    // https://stackoverflow.com/questions/51179331/is-it-possible-to-use-path-getrelativepath-net-core2-in-winforms-proj-targeti
    [Pure]
    public static string GetRelativePath(string relativeTo, string path)
    {
        if (!IsChildOf(relativeTo, path))
            throw new ArgumentException("Path is not relative to parent", nameof(path));
        if (string.IsNullOrEmpty(relativeTo))
        {
            path = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            if (path.IndexOf(Path.DirectorySeparatorChar) == -1)
                path = "." + Path.DirectorySeparatorChar + path;
            return path;
        }
        path = Path.GetFullPath(path);
        relativeTo = Path.GetFullPath(relativeTo);
        Uri uri = new Uri(relativeTo);
        string rel = Uri.UnescapeDataString(uri.MakeRelativeUri(new Uri(path)).ToString()).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        int index = rel.IndexOf(Path.DirectorySeparatorChar);
        if (index == -1)
            rel = "." + Path.DirectorySeparatorChar + rel;
        else
        {
            if (index != rel.Length - 1)
            {
                rel = rel.Substring(index + 1);
                if (rel.IndexOf(Path.DirectorySeparatorChar) == -1)
                    rel = "." + Path.DirectorySeparatorChar + rel;
            }
        }

        return rel;
    }
    /// <summary>
    /// Changes directory separators to back slashes if they aren't already.
    /// </summary>
    public static string FormatUniversalPath(string path) => Path.DirectorySeparatorChar == '\\' ? path : path.Replace(Path.DirectorySeparatorChar, '\\');
    /// <summary>
    /// Changes directory separators to forward slashes if they aren't supposed to be back slashes.
    /// </summary>
    public static string UnformatUniversalPath(string path) => Path.DirectorySeparatorChar == '\\' ? path : path.Replace('\\', Path.DirectorySeparatorChar);
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class CreateDirectoryAttribute : Attribute
{
    private static List<Type>? _checkedTypes = new List<Type>(64);
    public bool RelativeToGameDir { get; set; }
    internal bool FaultOnFailure { get; set; } = true;
    internal static void DisposeLoadList() => _checkedTypes = null;
    public static void CreateInAssembly(Assembly assembly) => CreateInAssembly(assembly, false);
    public static void CreateInType(Type type) => CreateInType(type, false);
    internal static void CreateInAssembly(Assembly assembly, bool allowFault)
    {
        List<Type> types = Accessor.GetTypesSafe(assembly, false);
        for (int index = 0; index < types.Count; index++)
        {
            Type type = types[index];
            CreateInType(type, allowFault);
        }
    }
    internal static void CreateInType(Type type, bool allowFault)
    {
        if (_checkedTypes != null && _checkedTypes.Contains(type))
            return;
        FieldInfo[] fields = type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        for (int f = 0; f < fields.Length; ++f)
        {
            FieldInfo field = fields[f];
            if (!field.IsStatic || IsDefined(field, typeof(IgnoreAttribute)) || GetCustomAttribute(field, typeof(CreateDirectoryAttribute)) is not CreateDirectoryAttribute cdir)
                continue;
            if (typeof(string).IsAssignableFrom(field.FieldType))
            {
                try
                {
                    string? path = (string?)field.GetValue(null);
                    if (path == null)
                        Logger.LogWarning($"Unable to check directory for {field.Format()}, field returned {((object?)null).Format()}.", method: "CHECK DIR");
                    else DevkitServerUtility.CheckDirectory(cdir.RelativeToGameDir, allowFault && cdir.FaultOnFailure, path, field);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Unable to check directory for {field.Format()}, type initializer threw exception.", method: "CHECK DIR");
                    Logger.LogError(ex, method: "CHECK DIR");
                }
            }
            else if (typeof(FileInfo).IsAssignableFrom(field.FieldType))
            {
                try
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
                catch (Exception ex)
                {
                    Logger.LogWarning($"Unable to check directory for {field.Format()}, type initializer threw exception.", method: "CHECK DIR");
                    Logger.LogError(ex, method: "CHECK DIR");
                }
            }
            else if (typeof(DirectoryInfo).IsAssignableFrom(field.FieldType))
            {
                try
                {
                    string? dir = ((DirectoryInfo?)field.GetValue(null))?.FullName;
                    cdir.RelativeToGameDir = false;
                    if (dir == null)
                        Logger.LogWarning($"[CHECK DIR] Unable to check directory for {field.Format()}, field returned {((object?)null).Format()}.");
                    else DevkitServerUtility.CheckDirectory(false, allowFault && cdir.FaultOnFailure, dir, field);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Unable to check directory for {field.Format()}, type initializer threw exception.", method: "CHECK DIR");
                    Logger.LogError(ex, method: "CHECK DIR");
                }
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
                try
                {
                    string? path = (string?)property.GetMethod?.Invoke(null, Array.Empty<object>());
                    if (path == null)
                        Logger.LogWarning($"[CHECK DIR] Unable to check directory for {property.Format()}, field returned {((object?)null).Format()}.");
                    else DevkitServerUtility.CheckDirectory(cdir.RelativeToGameDir, allowFault && cdir.FaultOnFailure, path, property);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Unable to check directory for {property.Format()}, property getter or type initializer threw exception.", method: "CHECK DIR");
                    Logger.LogError(ex, method: "CHECK DIR");
                }
            }
            else if (typeof(FileInfo).IsAssignableFrom(property.PropertyType))
            {
                try
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
                catch (Exception ex)
                {
                    Logger.LogWarning($"Unable to check directory for {property.Format()}, property getter or type initializer threw exception.", method: "CHECK DIR");
                    Logger.LogError(ex, method: "CHECK DIR");
                }
            }
            else if (typeof(DirectoryInfo).IsAssignableFrom(property.PropertyType))
            {
                try
                {
                    string? dir = ((DirectoryInfo?)property.GetMethod?.Invoke(null, Array.Empty<object>()))?.FullName;
                    if (dir == null)
                        Logger.LogWarning($"[CHECK DIR] Unable to check directory for {property.Format()}, field returned {((object?)null).Format()}.");
                    else DevkitServerUtility.CheckDirectory(false, allowFault && cdir.FaultOnFailure, dir, property);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Unable to check directory for {property.Format()}, property getter or type initializer threw exception.", method: "CHECK DIR");
                    Logger.LogError(ex, method: "CHECK DIR");
                }
            }
            else
            {
                Logger.LogWarning($"[CHECK DIR] Unable to check directory for {property.Format()}, valid on types: " +
                                  $"{typeof(string).Format()}, {typeof(FileInfo).Format()}, or {typeof(DirectoryInfo).Format()}.");
            }
        }

        _checkedTypes?.Add(type);
    }
}
public enum ScheduleInterval
{
    /// <summary>
    /// Dates are taken literally. Schedule is only valid until the last date.
    /// </summary>
    None,
    /// <summary>
    /// Only hours, minutes, and seconds are considered from the dates.
    /// </summary>
    Daily,
    /// <summary>
    /// The day should be from 1 - 7, which is then added from the most recent Sunday.
    /// </summary>
    Weekly,
    /// <summary>
    /// Only days, hours, minutes, and seconds are considered from the dates.
    /// </summary>
    Monthly,
    /// <summary>
    /// Only months, days, hours, minutes, and seconds are considered from the dates.
    /// </summary>
    Yearly
}