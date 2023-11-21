using DevkitServer.Multiplayer.Networking;

namespace DevkitServer.Util.Encoding;
internal class LargeMessageTransmissionCommunications(LargeMessageTransmission transmission, bool isServer)
{
    internal static NetCallCustom SendStart = new NetCallCustom(ReceiveStart, capacity: LargeMessageTransmission.HeaderCapacity);
    internal static NetCallCustom SendSlowPacket = new NetCallCustom(ReceiveSlowPacket, capacity: LargeMessageTransmission.PacketCapacity);
    internal static NetCallCustom SendSlowEnd = new NetCallCustom(ReceiveSlowEnd, capacity: LargeMessageTransmission.FooterCapacity);
    internal static NetCallCustom SendSlowMissedPackets = new NetCallCustom(ReceiveSlowMissedPackets, capacity: 256);
    internal static NetCall<Guid, int, int> SendFastCheckup = new NetCall<Guid, int, int>(ReceiveFastCheckup);
    internal static NetCallCustom SendFullData = new NetCallCustom(ReceiveFullData, capacity: 8388608 /* 8 MiB */);

    private static readonly Dictionary<Guid, LargeMessageTransmission> ActiveMessages = new Dictionary<Guid, LargeMessageTransmission>(4);

    private readonly LargeMessageTransmission _transmission = transmission;
    private readonly bool _isServer = isServer;
#if SERVER
    private readonly ITransportConnection _connection = transmission.Connection;
#endif

    private void HandleSlowPacket(in MessageContext ctx, ByteReader reader, byte verison)
    {
        ctx.Acknowledge(StandardErrorCode.Success);
    }
    private void HandleSlowEnd(in MessageContext ctx, ByteReader reader, byte verison)
    {
        ctx.Acknowledge(StandardErrorCode.Success);
    }
    private void HandleSlowMissedPackets(in MessageContext ctx, int[] missingPackets)
    {
        ctx.Acknowledge(StandardErrorCode.Success);
    }
    private int HandleFastCheckup(in MessageContext ctx, int checkupStartIndex, int checkupEndIndex)
    {
        // return the amount of messages missing.
        return 0;
    }
    private void HandleFullData(in MessageContext ctx, ByteReader reader, byte verison)
    {

        ctx.Acknowledge(StandardErrorCode.Success);
    }


    #region Receivers
    [NetCall(NetCallSource.FromEither, DevkitServerNetCall.SendStartLargeTransmission)]
    private static void ReceiveStart(MessageContext ctx, ByteReader reader)
    {
        LargeMessageTransmission transmission = new LargeMessageTransmission(
#if SERVER
            ctx.Connection, 
#endif
            reader);

        if (!ActiveMessages.TryAdd(transmission.TransmissionId, transmission))
        {
            Logger.LogWarning($"Received duplicate transmission: {transmission.TransmissionId.Format()}.", method: transmission.LogSource);
            ctx.Acknowledge(StandardErrorCode.InvalidData);
        }
        else
        {
            ctx.Acknowledge(StandardErrorCode.Success);
        }
    }

    [NetCall(NetCallSource.FromEither, DevkitServerNetCall.SendLargeTransmissionPacket)]
    private static void ReceiveSlowPacket(MessageContext ctx, ByteReader reader)
    {
        Guid guid = reader.ReadGuid();
        byte v = reader.ReadUInt8();

        if (!ActiveMessages.TryGetValue(guid, out LargeMessageTransmission transmission))
        {
            Logger.LogWarning($"Received unknown transmission: {guid.Format()}.", method: "LARGE MSG");
            ctx.Acknowledge(StandardErrorCode.NotFound);
            return;
        }

        if (transmission.Comms._isServer)
        {
            Logger.LogWarning($"Received server transmission: {guid.Format()}, but expected client.", method: "LARGE MSG");
            ctx.Acknowledge(StandardErrorCode.AccessViolation);
            return;
        }

        transmission.Comms.HandleSlowPacket(in ctx, reader, v);
    }

    [NetCall(NetCallSource.FromEither, DevkitServerNetCall.SendEndLargeTransmission)]
    private static void ReceiveSlowEnd(MessageContext ctx, ByteReader reader)
    {
        Guid guid = reader.ReadGuid();
        byte v = reader.ReadUInt8();

        if (!ActiveMessages.TryGetValue(guid, out LargeMessageTransmission transmission))
        {
            Logger.LogWarning($"Received unknown transmission: {guid.Format()}.", method: "LARGE MSG");
            return;
        }

        if (transmission.Comms._isServer)
        {
            Logger.LogWarning($"Received server transmission: {guid.Format()}, but expected client.", method: "LARGE MSG");
            return;
        }

        transmission.Comms.HandleSlowEnd(in ctx, reader, v);
    }

    [NetCall(NetCallSource.FromEither, DevkitServerNetCall.SendMissedLargeTransmissionPackets)]
    private static void ReceiveSlowMissedPackets(MessageContext ctx, ByteReader reader)
    {
        Guid guid = reader.ReadGuid();
        _ = reader.ReadUInt8();

        int len = reader.ReadInt32();
        int[] missingPackets = new int[len];

        for (int i = 0; i < len; ++i)
            missingPackets[i] = reader.ReadInt32();

        if (!ActiveMessages.TryGetValue(guid, out LargeMessageTransmission transmission))
        {
            Logger.LogWarning($"Received unknown transmission: {guid.Format()}.", method: "LARGE MSG");
            return;
        }

        if (!transmission.Comms._isServer)
        {
            Logger.LogWarning($"Received client transmission: {guid.Format()}, but expected server.", method: "LARGE MSG");
            return;
        }

        transmission.Comms.HandleSlowMissedPackets(in ctx, missingPackets);
    }

    [NetCall(NetCallSource.FromEither, DevkitServerNetCall.SendLargeTransmissionCheckup, HighSpeed = true)]
    private static void ReceiveFastCheckup(MessageContext ctx, Guid guid, int start, int end)
    {
        if (!ActiveMessages.TryGetValue(guid, out LargeMessageTransmission transmission))
        {
            Logger.LogWarning($"Received unknown transmission: {guid.Format()}.", method: "LARGE MSG");
            ctx.Acknowledge(-1);
            return;
        }

        if (transmission.Comms._isServer)
        {
            Logger.LogWarning($"Received server transmission: {guid.Format()}, but expected client.", method: "LARGE MSG");
            ctx.Acknowledge(-2);
            return;
        }

        ctx.Acknowledge(transmission.Comms.HandleFastCheckup(in ctx, start, end));
    }

    [NetCall(NetCallSource.FromEither, (ushort)HighSpeedNetCall.SendFullLargeTransmission, HighSpeed = true)]
    private static void ReceiveFullData(MessageContext ctx, ByteReader reader)
    {
        Guid guid = reader.ReadGuid();
        byte v = reader.ReadUInt8();

        if (!ActiveMessages.TryGetValue(guid, out LargeMessageTransmission transmission))
        {
            Logger.LogWarning($"Received unknown transmission: {guid.Format()}.", method: "LARGE MSG");
            ctx.Acknowledge(StandardErrorCode.NotFound);
            return;
        }

        if (transmission.Comms._isServer)
        {
            Logger.LogWarning($"Received server transmission: {guid.Format()}, but expected client.", method: "LARGE MSG");
            ctx.Acknowledge(StandardErrorCode.AccessViolation);
            return;
        }

        transmission.Comms.HandleFullData(in ctx, reader, v);
    }
    #endregion
}
