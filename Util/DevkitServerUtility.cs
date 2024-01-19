using DevkitServer.API;
using DevkitServer.Multiplayer.Levels;
using DevkitServer.Multiplayer.Networking;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Reflection;
using Unturned.SystemEx;
#if SERVER
using DevkitServer.Patches;
using DevkitServer.Players;
#endif

namespace DevkitServer.Util;
public static class DevkitServerUtility
{
    private static int? _maxTextureSize;
    public static int MaxTextureDimensionSize => _maxTextureSize ??= Math.Min(16384, SystemInfo.maxTextureSize);
    public static bool LanguageIsEnglish => Provider.language.Length > 0 && Provider.language[0] == 'E' && Provider.language.Equals("English", StringComparison.Ordinal);
    [Pure]
    public static Bounds InflateBounds(ref Bounds bounds)
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
    public static float SqrDist2D(this in Vector3 v1, in Vector3 v2)
    {
        float x = v1.x - v2.x, z = v1.z - v2.z;
        return x * x + z * z;
    }

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
            if (str[2] != ':' || str[4] != ':' || str[^1] != ']')
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
            if (str[^3] != ':')
            {
                if (!uint.TryParse(str.Substring(5, str.Length - 6), NumberStyles.Number, CultureInfo.InvariantCulture, out acctId))
                    goto fail;
            }
            else
            {
                if (!uint.TryParse(str.Substring(5, str.Length - 8), NumberStyles.Number, CultureInfo.InvariantCulture, out acctId))
                    goto fail;
                acctId *= 2;
                uv = str[^2];
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
            Logger.DevkitServer.LogDebug(nameof(TryGetConnections), Provider.clients[i].transportConnection.GetAddress().Format() + " vs " + ip.Format() + ":");
            if (Provider.clients[i].transportConnection.GetAddress().MapToIPv4().Equals(ip))
            {
                Logger.DevkitServer.LogDebug(nameof(TryGetConnections), " Matches");
                results.Add(Provider.clients[i].transportConnection);
                break;
            }
        }
        
        for (int i = 0; i < Provider.pending.Count; ++i)
        {
            Logger.DevkitServer.LogDebug(nameof(TryGetConnections), Provider.pending[i].transportConnection.GetAddress().Format() + " vs " + ip.Format() + ":");
            if (Provider.pending[i].transportConnection.GetAddress().MapToIPv4().Equals(ip))
            {
                Logger.DevkitServer.LogDebug(nameof(TryGetConnections), " Matches");
                results.Add(Provider.pending[i].transportConnection);
                break;
            }
        }

        for (int i = 0; i < PatchesMain.PendingConnections.Count; ++i)
        {
            Logger.DevkitServer.LogDebug(nameof(TryGetConnections), PatchesMain.PendingConnections[i].GetAddress().MapToIPv4().Format() + " vs " + ip.Format() + ":");
            if (PatchesMain.PendingConnections[i].GetAddress().MapToIPv4().Equals(ip))
            {
                Logger.DevkitServer.LogDebug(nameof(TryGetConnections), " Matches");
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
    public static Local ReadLocalFromFileOrFolder(string filePath, out string? primaryWritePath, out string? englishWritePath)
    {
        filePath = Path.GetFullPath(filePath);

        if (Directory.Exists(filePath))
        {
            englishWritePath = Path.Combine(filePath, "English.dat");

            if (!File.Exists(englishWritePath))
                englishWritePath = null;

            if (!LanguageIsEnglish)
            {
                primaryWritePath = Path.Combine(filePath, Provider.language + ".dat");
                if (!File.Exists(primaryWritePath))
                    primaryWritePath = null;
            }
            else
                primaryWritePath = englishWritePath;

            return Localization.tryRead(filePath, false);
        }

        string datFile = filePath + ".dat";
        if (!File.Exists(datFile))
        {
            englishWritePath = null;
            primaryWritePath = null;
            return new Local();
        }

        englishWritePath = datFile;
        primaryWritePath = LanguageIsEnglish ? datFile : null;

        return Localization.read(datFile);
    }
    [Pure]
    public static unsafe bool UserSteam64(this ulong s64) => ((CSteamID*)&s64)->GetEAccountType() == EAccountType.k_EAccountTypeIndividual;
    [Pure]
    public static bool UserSteam64(this CSteamID s64) => s64.GetEAccountType() == EAccountType.k_EAccountTypeIndividual;
    public static void UpdateLocalizationFile(ref Local read, LocalDatDictionary @default, string directory, string? primaryPath, string? englishPath)
    {
        DatDictionary @new = new DatDictionary();
        DatDictionary def2 = new DatDictionary();
        foreach (KeyValuePair<string, string> pair in @default)
        {
            @new.Add(pair.Key, new DatValue(read.has(pair.Key) ? read.format(pair.Key) : pair.Value));
            def2.Add(pair.Key, new DatValue(pair.Value));
        }

        read = new Local(@new, def2);
        bool eng = LanguageIsEnglish;
        if (Directory.Exists(directory))
        {
            if (englishPath != null && !File.Exists(englishPath))
            {
                WriteData(englishPath, eng ? @new : def2);
                if (eng) return;
            }

            if (!eng && primaryPath != null)
                WriteData(primaryPath, @new);
            else if (eng && englishPath != null)
                WriteData(englishPath, @new);
            return;
        }

        if (eng && englishPath != null)
            WriteData(englishPath, @new);
        else if (primaryPath != null && !File.Exists(primaryPath))
            WriteData(primaryPath, eng ? @new : def2);
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

    /// <summary>
    /// Disconnect a user with a custom message using the <see cref="ESteamConnectionFailureInfo.KICKED"/> or <see cref="ESteamRejection.PLUGIN"/> failure type. Works on client or server.
    /// </summary>
    /// <remarks>Clientside will gracefully disconnect, server will reject or kick.</remarks>
    public static void CustomDisconnect(
#if SERVER
        EditorUser user,
#endif
        string message) =>
        CustomDisconnect(
#if SERVER
            user, message, ESteamRejection.PLUGIN
#else
            message, ESteamConnectionFailureInfo.KICKED
#endif
    );

    /// <summary>
    /// Disconnect a user with a custom failure type (and no message). Works on client or server.
    /// </summary>
    /// <remarks>Clientside will gracefully disconnect, server will reject or kick.</remarks>
    public static void CustomDisconnect(
#if SERVER
        EditorUser user, ESteamRejection failureType
#else
        ESteamConnectionFailureInfo failureType
#endif
        ) =>
        CustomDisconnect(
#if SERVER
            user,
#endif
        string.Empty, failureType);

    /// <summary>
    /// Disconnect a user with a custom message and failure type. Works on client or server.
    /// </summary>
    /// <remarks>Clientside will gracefully disconnect, server will reject or kick.</remarks>
    public static void CustomDisconnect(
#if SERVER
        EditorUser user,
#endif
        string message,
#if SERVER
        ESteamRejection failureType
#else
        ESteamConnectionFailureInfo failureType
#endif
        )
    {
#if CLIENT
        Provider.connectionFailureInfo = failureType;
        Provider.RequestDisconnect(Provider.connectionFailureReason = message);
        DevkitServerModule.IsEditing = false;
#else
        if (Provider.pending.Any(x => x.playerID.steamID.m_SteamID == user.SteamId.m_SteamID))
            Provider.reject(user.SteamId, failureType, message);
        else
            Provider.kick(user.SteamId, message);
#endif
    }
#if SERVER
    /// <summary>
    /// Disconnect a connection with a custom message using the <see cref="ESteamRejection.PLUGIN"/> failure type. Works on client or server.
    /// </summary>
    /// <remarks>Clientside will gracefully disconnect, server will reject or kick.</remarks>
    public static void CustomDisconnect(ITransportConnection user, string message) =>
        CustomDisconnect(user, message, ESteamRejection.PLUGIN);

    /// <summary>
    /// Disconnect a connection with a custom failure type (and no message). Works on client or server.
    /// </summary>
    /// <remarks>Clientside will gracefully disconnect, server will reject or kick.</remarks>
    public static void CustomDisconnect(ITransportConnection user, ESteamRejection failureType) =>
        CustomDisconnect(user, string.Empty, failureType);

    /// <summary>
    /// Disconnect a connection with a custom message and failure type. Works on client or server.
    /// </summary>
    /// <remarks>Clientside will gracefully disconnect, server will reject or kick.</remarks>
    public static void CustomDisconnect(ITransportConnection user, string message, ESteamRejection failureType)
    {
        if (user is HighSpeedConnection c)
        {
            HighSpeedServer.Instance.Disconnect(c);
            return;
        }

        SteamPending? pending = Provider.pending.Find(x => ReferenceEquals(x.transportConnection, user));
        if (pending != null)
        {
            Provider.reject(pending.playerID.steamID, failureType, message);
        }
        else
        {
            SteamPlayer? player = Provider.clients.Find(x => ReferenceEquals(x.transportConnection, user));
            if (player != null)
                Provider.kick(player.playerID.steamID, message);
        }
    }
#endif
    /// <summary>
    /// Compares a <see cref="Quaternion"/> to <see cref="Quaternion.identity"/> within <paramref name="tolerance"/>.
    /// </summary>
    [Pure]
    public static bool IsNearlyIdentity(this Quaternion q, float tolerance = 0.001f)
    {
        return q.x > -tolerance && q.x < tolerance && q.y > -tolerance && q.y < tolerance && q.z > -tolerance && q.z < tolerance && q.w - 1f > -tolerance && q.w - 1f < tolerance;
    }

    /// <summary>
    /// Gets a pooled transport connection list of all connected clients.
    /// </summary>
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
    /// <summary>
    /// Gets a pooled transport connection list of all connected clients, excluding <paramref name="exclude"/>.
    /// </summary>
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

    /// <returns>'s' if <paramref name="num"/> != 1, otherwise an empty string.</returns>
    [Pure]
    public static string S(this int num) => num == 1 ? string.Empty : "s";

    /// <returns>'S' if <paramref name="num"/> != 1, otherwise an empty string.</returns>
    [Pure]
    public static string UpperS(this int num) => num == 1 ? string.Empty : "S";

    /// <summary>
    /// Gets the elapsed milliseconds from a <see cref="Stopwatch"/> as a <see cref="double"/> instead of <see cref="long"/>.
    /// </summary>
    [Pure]
    public static double GetElapsedMilliseconds(this Stopwatch stopwatch) => stopwatch.ElapsedTicks / (double)Stopwatch.Frequency * 1000d;

    /// <summary>
    /// From a schedule and interval, chooses the next date time based on the current time.
    /// </summary>
    /// <param name="utc">Schedule is in UTC instead of local time.</param>
    /// <returns>The selected scheduled time, or <see langword="null"/> if there are no future elements.</returns>
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

    /// <summary>
    /// Converts terabytes to bytes.
    /// </summary>
    /// <remarks>XiB units are power of 2 based, XB units are power of 10 based.</remarks>
    [Pure]
    public static long ConvertTBToB(double tb) => (long)Math.Round(tb * 1000000000000d);

    /// <summary>
    /// Converts gigabytes to bytes.
    /// </summary>
    /// <remarks>XiB units are power of 2 based, XB units are power of 10 based.</remarks>
    [Pure]
    public static long ConvertGBToB(double gb) => (long)Math.Round(gb * 1000000000d);

    /// <summary>
    /// Converts megabytes to bytes.
    /// </summary>
    /// <remarks>XiB units are power of 2 based, XB units are power of 10 based.</remarks>
    [Pure]
    public static long ConvertMBToB(double mb) => (long)Math.Round(mb * 1000000d);

    /// <summary>
    /// Converts kilobytes to bytes.
    /// </summary>
    /// <remarks>XiB units are power of 2 based, XB units are power of 10 based.</remarks>
    [Pure]
    public static long ConvertKBToB(double kb) => (long)Math.Round(kb * 1000d);

    /// <summary>
    /// Converts tebibytes to bytes.
    /// </summary>
    /// <remarks>XiB units are power of 2 based, XB units are power of 10 based.</remarks>
    [Pure]
    public static long ConvertTiBToB(double tib) => (long)Math.Round(tib * 1099511627776d);

    /// <summary>
    /// Converts gibibytes to bytes.
    /// </summary>
    /// <remarks>XiB units are power of 2 based, XB units are power of 10 based.</remarks>
    [Pure]
    public static long ConvertGiBToB(double gib) => (long)Math.Round(gib * 1073741824d);

    /// <summary>
    /// Converts mebibytes to bytes.
    /// </summary>
    /// <remarks>XiB units are power of 2 based, XB units are power of 10 based.</remarks>
    [Pure]
    public static long ConvertMiBToB(double mib) => (long)Math.Round(mib * 1048576d);

    /// <summary>
    /// Converts kibibytes to bytes.
    /// </summary>
    /// <remarks>XiB units are power of 2 based, XB units are power of 10 based.</remarks>
    [Pure]
    public static long ConvertKiBToB(double kib) => (long)Math.Round(kib * 1024d);


    /// <summary>
    /// Removes all matches in a list.
    /// </summary>
    /// <remarks>Runs backwards.</remarks>
    /// <returns>The amount of elements removed.</returns>
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

    /// <summary>
    /// Converts a list to array, or uses <see cref="Array.Empty"/> if the count is zero.
    /// </summary>
    public static T[] ToArrayFast<T>(this List<T> list) => list.Count == 0 ? Array.Empty<T>() : list.ToArray();

    /// <summary>
    /// Converts a list to a <see cref="ReadOnlySpan{T}"/> without copying the data.
    /// </summary>
    public static Span<T> ToSpan<T>(this List<T> list)
    {
        if (list.Count == 0)
            return default;

        return Accessor.TryGetUnderlyingArray(list, out T[] underlying) ? underlying.AsSpan(0, list.Count) : list.ToArrayFast();
    }

    /// <summary>
    /// Increases the capacity of a list if it is less than <paramref name="capacity"/>.
    /// </summary>
    public static void IncreaseCapacity<T>(this List<T> list, int capacity)
    {
        if (list.Capacity < capacity)
            list.Capacity = capacity;
    }

    /// <summary>
    /// Returns the first matching value only if there are no other matching values.
    /// </summary>
    /// <remarks>Doesn't throw an error when there are no matches (unlike the normal linq version).</remarks>
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

    /// <summary>
    /// Creates a copy of an array and adds <paramref name="value"/> to it.
    /// </summary>
    public static void AddToArray<T>(ref T[]? array, T value, int index = -1)
    {
        if (array == null || array.Length == 0)
        {
            array = new T[] { value };
            return;
        }
        if (index < 0)
            index = array.Length;
        T[] old = array;
        array = new T[old.Length + 1];
        if (index != 0)
            Array.Copy(old, array, index);
        if (index != old.Length)
            Array.Copy(old, index, array, index + 1, old.Length - index);
        array[index] = value;
    }

    /// <summary>
    /// Creates a copy of an array after removing the element at <paramref name="index"/> from it.
    /// </summary>
    public static void RemoveFromArray<T>(ref T[] array, int index)
    {
        if (index < 0 || index >= array.Length)
            throw new ArgumentOutOfRangeException(nameof(index), "Index out of bounds of the array.");

        T[] old = array;
        array = new T[old.Length - 1];
        if (index != 0)
            Array.Copy(old, 0, array, 0, index);
        if (index != array.Length)
            Array.Copy(old, index + 1, array, index, array.Length - index);
    }
    /// <summary>
    /// Returns the first value only if the count is one.
    /// </summary>
    /// <remarks>Doesn't throw an error when there are no elements (unlike the normal linq version).</remarks>
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
    /// Ceils positive numbers, floors negative numbers.
    /// </summary>
    public static int CeilToIntIgnoreSign(this float val) => val < 0 ? Mathf.FloorToInt(val) : Mathf.CeilToInt(val);

    /// <summary>
    /// Floors positive numbers, ceils negative numbers.
    /// </summary>
    public static int FloorToIntIgnoreSign(this float val) => val < 0 ? Mathf.CeilToInt(val) : Mathf.FloorToInt(val);

    /// <summary>
    /// Adds <paramref name="by"/> to each coordinates' magnitudes (adds when positive, subtracts when negative).
    /// </summary>
    public static void Expand(this ref Vector3 v3, float by)
    {
        if (v3.x < 0)
            v3.x -= by;
        else if (v3.x > 0)
            v3.x += by;

        if (v3.y < 0)
            v3.y -= by;
        else if (v3.y > 0)
            v3.y += by;

        if (v3.z < 0)
            v3.z -= by;
        else if (v3.z > 0)
            v3.z += by;
    }

    /// <summary>
    /// Check if a <see cref="ITransportConnection"/> is valid.
    /// </summary>
    public static bool IsConnected(this ITransportConnection connection)
    {
        if (connection is HighSpeedConnection conn)
            return conn.Client.Connected;

        return connection.GetAddress() != null;
    }

    /// <summary>
    /// Converts a <see cref="IPv4Address"/> to an <see cref="IPAddress"/>.
    /// </summary>
    public static IPAddress Unpack(IPv4Address address) => Unpack(address.value);

    /// <summary>
    /// Converts a <see cref="uint"/> IPv4 to an <see cref="IPAddress"/>.
    /// </summary>
    public static IPAddress Unpack(uint address)
    {
        uint newAddr = address << 24 | ((address >> 8) & 0xFF) << 16 | ((address >> 16) & 0xFF) << 8 | (address >> 24);
        return new IPAddress(newAddr);
    }

    /// <summary>
    /// Converts a <see cref="IPAddress"/> to an <see cref="IPv4Address"/>.
    /// </summary>
    public static IPv4Address PackToIPv4(IPAddress address) => new IPv4Address(Pack(address));

    /// <summary>
    /// Converts a <see cref="IPAddress"/> to an <see cref="uint"/> IPv4.
    /// </summary>
    public static uint Pack(IPAddress address)
    {
        byte[] ipv4 = address.MapToIPv4().GetAddressBytes();
        return ((uint)ipv4[0] << 24) | ((uint)ipv4[1] << 16) | ((uint)ipv4[2] << 8) | ipv4[3];
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