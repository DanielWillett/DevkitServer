
using System.Diagnostics.CodeAnalysis;
using DevkitServer.Players;
#if SERVER
using System.Collections.Concurrent;
using DevkitServer.Patches;
using Unturned.SystemEx;
#endif
#if CLIENT
using System.Security.Cryptography;
#endif

namespace DevkitServer.Multiplayer.Cryptography;
internal static class UserCryptographyStore
{
#if SERVER
    private static readonly ConcurrentDictionary<ulong, byte[]> RsaKeysSteam64 = new ConcurrentDictionary<ulong, byte[]>();
    private static readonly ConcurrentDictionary<uint, byte[]> RsaKeysIPv4 = new ConcurrentDictionary<uint, byte[]>();

    internal static void Initialize()
    {
        UserManager.OnUserConnected += UserConnected;
        UserManager.OnUserDisconnected += UserDisconnected;
        TransportPatcher.OnTransportConnectionDestroyed += RemoveUser;
    }

    internal static void Shutdown()
    {
        UserManager.OnUserConnected -= UserConnected;
        UserManager.OnUserDisconnected -= UserDisconnected;
        TransportPatcher.OnTransportConnectionDestroyed -= RemoveUser;
    }

    private static void UserDisconnected(EditorUser obj)
    {
        RsaKeysSteam64.TryRemove(obj.SteamId.m_SteamID, out _);
        if (obj.Connection.TryGetIPv4Address(out uint ip))
        {
            RsaKeysIPv4.TryRemove(ip, out _);
        }
    }

    private static void UserConnected(EditorUser obj)
    {
        uint? ipAddress = obj.Connection.TryGetIPv4Address(out uint addr) ? addr : null;
        ulong steam64 = obj.SteamId.m_SteamID;
        if (RsaKeysSteam64.TryGetValue(steam64, out byte[] rsaKey))
        {
            if (!ipAddress.HasValue)
                return;

            RsaKeysIPv4[ipAddress.Value] = rsaKey;
        }
        else if (ipAddress.HasValue && RsaKeysIPv4.TryGetValue(ipAddress.Value, out rsaKey))
        {
            RsaKeysSteam64[steam64] = rsaKey;
        }
    }

    /// <summary>
    /// Get the user's cached RSA public key used for encrypted transmissions.
    /// </summary>
    /// <remarks>Thread-safe</remarks>
    internal static bool TryGetUser(ITransportConnection connection, [MaybeNullWhen(false)] out byte[] publicKey)
    {
        if (connection.TryGetSteamId(out ulong s64) && new CSteamID(s64).GetEAccountType() == EAccountType.k_EAccountTypeIndividual)
        {
            if (RsaKeysSteam64.TryGetValue(s64, out publicKey))
                return true;
        }

        if (connection.TryGetIPv4Address(out uint ip))
        {
            return RsaKeysIPv4.TryGetValue(ip, out publicKey);
        }

        publicKey = null;
        return false;
    }

    internal static void AddUser(CSteamID steam64, IPv4Address ipv4, byte[] publicKey)
    {
        if (steam64.UserSteam64())
            RsaKeysSteam64[steam64.m_SteamID] = publicKey;
        if (!ipv4.IsZero)
            RsaKeysIPv4[ipv4.value] = publicKey;
    }

    internal static void RemoveUser(ITransportConnection connection)
    {
        if (connection.TryGetIPv4Address(out uint ipv4))
        {
            RsaKeysIPv4.TryRemove(ipv4, out _);
        }

        if (connection.TryGetSteamId(out ulong s64) && new CSteamID(s64).GetEAccountType() == EAccountType.k_EAccountTypeIndividual)
        {
            RsaKeysSteam64.TryRemove(s64, out _);
        }
    }
#else
    private static RSACryptoServiceProvider? _localRsa;
    private static byte[]? _publicKey;

    internal static RSAOAEPKeyExchangeDeformatter GetDeformatter()
    {
        _localRsa ??= new RSACryptoServiceProvider();
        return new RSAOAEPKeyExchangeDeformatter(_localRsa);
    }
    
    internal static byte[] GetPublicKey()
    {
        ThreadUtil.assertIsGameThread();

        if (_localRsa == null)
        {
            _localRsa = new RSACryptoServiceProvider();
            _publicKey = _localRsa.ExportCspBlob(false);
        }
        else
        {
            _publicKey ??= _localRsa.ExportCspBlob(false);
        }

        return _publicKey;
    }

    internal static byte[] ResetAndGetKey()
    {
        ThreadUtil.assertIsGameThread();

        _publicKey = null;
        _localRsa?.Dispose();
        _localRsa = new RSACryptoServiceProvider();
        _publicKey = _localRsa.ExportCspBlob(false);

        return _publicKey;
    }

#endif
}
