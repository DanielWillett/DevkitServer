using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Reflection.Emit;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Patches;
using DevkitServer.Util.Encoding;
using HarmonyLib;
using SDG.Framework.Debug;
using SDG.Framework.Landscapes;
using Action = System.Action;

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
    public static Bounds InflateBounds(in Bounds bounds)
    {
        Vector3 c = bounds.center;
        Vector3 e = bounds.extents;
        return new Bounds(new Vector3(Mathf.Round(c.x), Mathf.Round(c.y), Mathf.Round(c.z)), new Vector3((e.x + 0.5f).CeilToIntIgnoreSign(), (e.y + 0.5f).CeilToIntIgnoreSign(), (e.z + 0.5f).CeilToIntIgnoreSign()));
    }
    /// <summary>Convert an HTMLColor string to a actual color.</summary>
    /// <param name="htmlColorCode">A hexadecimal/HTML color key.</param>
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
    public static uint ReverseUInt32(uint val) => ((val >> 24) & 0xFF) | (((val >> 16) & 0xFF) << 8) | (((val >> 8) & 0xFF) << 16) | (val << 24);
    public static bool Encapsulates(this in LandscapeBounds outer, in LandscapeBounds inner) =>
        outer.min.x < inner.min.x && outer.min.y < inner.min.y && outer.max.x > inner.max.x && outer.max.y > inner.max.y;
    public static bool Encapsulates(this in HeightmapBounds outer, in HeightmapBounds inner) =>
        outer.min.x < inner.min.x && outer.min.y < inner.min.y && outer.max.x > inner.max.x && outer.max.y > inner.max.y;
    public static bool Encapsulates(this in SplatmapBounds outer, in SplatmapBounds inner) =>
        outer.min.x < inner.min.x && outer.min.y < inner.min.y && outer.max.x > inner.max.x && outer.max.y > inner.max.y;
    public static bool Overlaps(this in LandscapeBounds left, in LandscapeBounds right) =>
        !(left.max.x < right.min.x || left.max.y < right.min.y || left.min.x > right.max.x || left.min.y > right.max.y);
    public static bool Overlaps(this in HeightmapBounds left, in HeightmapBounds right) =>
        !(left.max.x < right.min.x || left.max.y < right.min.y || left.min.x > right.max.x || left.min.y > right.max.y);
    public static bool Overlaps(this in SplatmapBounds left, in SplatmapBounds right) =>
        !(left.max.x < right.min.x || left.max.y < right.min.y || left.min.x > right.max.x || left.min.y > right.max.y);
    public static void Encapsulate(this ref LandscapeBounds left, in LandscapeBounds right)
    {
        if (left.min.x > right.min.x)
            left.min.x = right.min.x;
        if (left.min.y > right.min.y)
            left.min.y = right.min.y;
        if (left.max.x < right.max.x)
            left.max.x = right.max.x;
        if (left.max.y < right.max.y)
            left.max.y = right.max.y;
    }
    public static void Encapsulate(this ref HeightmapBounds left, in HeightmapBounds right)
    {
        if (left.min.x > right.min.x)
            left.min.x = right.min.x;
        if (left.min.y > right.min.y)
            left.min.y = right.min.y;
        if (left.max.x < right.max.x)
            left.max.x = right.max.x;
        if (left.max.y < right.max.y)
            left.max.y = right.max.y;
    }
    public static void Encapsulate(this ref SplatmapBounds left, in SplatmapBounds right)
    {
        if (left.min.x > right.min.x)
            left.min.x = right.min.x;
        if (left.min.y > right.min.y)
            left.min.y = right.min.y;
        if (left.max.x < right.max.x)
            left.max.x = right.max.x;
        if (left.max.y < right.max.y)
            left.max.y = right.max.y;
    }
    public static Vector2 ToVector2(this in Vector3 v3) => new Vector2(v3.x, v3.z);
    public static Vector3 ToVector3(this in Vector2 v2) => new Vector3(v2.x, 0f, v2.y);
    public static Vector3 ToVector3(this in Vector2 v2, float y) => new Vector3(v2.x, y, v2.y);
    public static CodeInstruction CopyWithoutSpecial(this CodeInstruction instruction) => new CodeInstruction(instruction.opcode, instruction.operand);
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
    public static MainThreadTask SkipFrame(CancellationToken token = default) => DevkitServerModule.IsMainThread ? MainThreadTask.CompletedSkip : new MainThreadTask(true, token);
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
        WriteData(writer, data, false);
        writer.Flush();
    }
    public static void WriteData(StreamWriter writer, IDatNode data, bool writeBrackets = false, int indent = 0)
    {
        if (data is DatValue value)
        {
            writer.WriteLine(writeBrackets ? (" " + value) : value);
            return;
        }
        string ind = indent < 1 ? string.Empty : new string('\t', indent);
        switch (data)
        {
            case DatDictionary dict:
                if (writeBrackets)
                {
                    writer.WriteLine();
                    writer.WriteLine(ind + "{");
                }
                foreach (KeyValuePair<string, IDatNode> node in dict)
                {
                    writer.Write((writeBrackets ? ind + "\t" : string.Empty) + (node.Key.IndexOf(' ') != -1 ? ("\"" + node.Key + "\"") : node.Key));
                    WriteData(writer, node.Value, true, writeBrackets ? indent + 1 : indent);
                }
                if (writeBrackets)
                    writer.WriteLine(ind + "}");
                break;
            case DatList list:
                if (writeBrackets)
                {
                    writer.WriteLine();
                    writer.WriteLine(ind + "[");
                }
                for (int i = 0; i < list.Count; ++i)
                {
                    IDatNode node = list[i];
                    WriteData(writer, node, true, writeBrackets ? indent + 1 : indent);
                }
                if (writeBrackets)
                    writer.WriteLine(ind + "]");
                break;
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