#if SERVER
using DevkitServer.API;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Players;
using SDG.Framework.Utilities;
using SDG.NetPak;
using Version = System.Version;

namespace DevkitServer.Multiplayer.Movement;
public static class ServerUserMovement
{
    private static bool _init;
    private static bool _subbed;
    internal static bool HasPendingMovement;
    internal static void StartPlayingOnEditorServer()
    {
        if (_init)
            return;

        _init = true;

        if (_subbed)
            return;

        TimeUtility.physicsUpdated += OnFixedUpdate;
        _subbed = true;
    }

    internal static void StopPlayingOnEditorServer()
    {
        if (!_init)
            return;

        _init = false;

        if (!_subbed)
            return;

        TimeUtility.physicsUpdated -= OnFixedUpdate;
        _subbed = false;
    }

    private static void OnFixedUpdate()
    {
        if (!HasPendingMovement)
            return;

        NetFactory.SendGeneric(DevkitServerMessage.MovementRelay, SendUserMovementPackets, Provider.GatherRemoteClientConnections(), reliable: false);
    }

    private static void SendUserMovementPackets(NetPakWriter writer)
    {
        int c = 0;
        foreach (EditorUser user in UserManager.Users)
        {
            if (user.IsOwner)
                continue;

            UserMovement? movement = user.Movement;
            if (movement is null || movement.SendPackets.Length == 0)
                continue;

            ++c;
            if (c == byte.MaxValue)
                break;
        }

        writer.WriteUInt8((byte)c);

        Version v = Accessor.DevkitServer.GetName().Version;
        writer.WriteUInt32((uint)(((byte)v.Major << 24) | ((byte)v.Minor << 16) | ((byte)v.Build << 8) | (byte)v.Revision));

        foreach (EditorUser user in UserManager.Users)
        {
            if (user.IsOwner)
                continue;

            UserMovement? movement = user.Movement;
            if (movement is null || movement.SendPackets.Length == 0)
                continue;

            int len = Math.Min(byte.MaxValue, movement.SendPackets.Length);

            writer.WriteUInt8((byte)len);
            writer.WriteUInt32(user.SteamId.GetAccountID().m_AccountID);

            for (int i = 0; i < len; ++i)
            {
                ref EditorInputPacket packet = ref movement.SendPackets[i];
                packet.Write(writer);
            }

            movement.SendPackets.Clear();
        }
    }
    internal static void ReceiveUserMovementPacket(ITransportConnection transportConnection, NetPakReader reader)
    {
        EditorUser? user = UserManager.FromConnection(transportConnection);
        if (user == null)
        {
            Logger.DevkitServer.LogError(nameof(ServerUserMovement), $"Failed to find user for movement packet from transport connection: {transportConnection.Format()}.");
            return;
        }

        int sizeStart = reader.readByteIndex;
        if (!EditorInputPacket.ReadVersioned(reader, out EditorInputPacket packet))
        {
            Logger.DevkitServer.LogError(nameof(ServerUserMovement), $"Failed to read movement packet from {user.SteamId.Format()}.");
            return;
        }

        user.Movement?.ReceivePacket(in packet);

        NetFactory.IncrementByteCount(DevkitServerMessage.MovementRelay, false, reader.readByteIndex - sizeStart);
    }
}
#endif