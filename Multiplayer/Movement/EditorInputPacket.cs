using DevkitServer.API;
using SDG.NetPak;
using Version = System.Version;

namespace DevkitServer.Multiplayer.Movement;
internal struct EditorInputPacket
{
    public uint ClientInputFrame;
    public Vector3 Position;
    public Vector2 Rotation;
    public bool LastFrameBeforeChangingController;
    public byte LastTeleportId;
    // public float DeltaTime;

    private const int PosIntBitCt = 16;
    private const int PosDecBitCt = 8;
    private const int RotIntBitCt = 10;
    private const int RotDecBitCt = 7;
    // private const int DtIntBitCt = 4;
    // private const int DtDecBitCt = 11;
    public static bool Read(NetPakReader reader, Version sourceVersion, out EditorInputPacket packet)
    {
        _ = sourceVersion;
        packet = default;
        if (!reader.ReadUInt32(out packet.ClientInputFrame))
            return false;
        if (!reader.ReadClampedVector3(out packet.Position, PosIntBitCt, PosDecBitCt))
            return false;
        if (!reader.ReadClampedFloat(RotIntBitCt, RotDecBitCt, out packet.Rotation.x))
            return false;
        if (!reader.ReadClampedFloat(RotIntBitCt, RotDecBitCt, out packet.Rotation.y))
            return false;
        if (!reader.ReadUInt8(out packet.LastTeleportId))
            return false;

        return reader.ReadBit(out packet.LastFrameBeforeChangingController);
    }
    public static bool ReadVersioned(NetPakReader reader, out EditorInputPacket packet)
    {
        packet = default;
        if (!reader.ReadUInt32(out uint vNum))
            return false;

        Version version = new Version((int)((vNum >> 24) & 0xFF), (int)((vNum >> 16) & 0xFF), (int)((vNum >> 8) & 0xFF), (int)(vNum & 0xFF));
        return Read(reader, version, out packet);
    }
    public readonly void Write(NetPakWriter writer)
    {
        writer.WriteUInt32(ClientInputFrame);
        writer.WriteClampedVector3(Position, PosIntBitCt, PosDecBitCt);
        writer.WriteClampedFloat(Rotation.x, RotIntBitCt, RotDecBitCt);
        writer.WriteClampedFloat(Rotation.y, RotIntBitCt, RotDecBitCt);

        writer.WriteUInt8(LastTeleportId);

        writer.WriteBit(LastFrameBeforeChangingController);
    }
    public readonly void WriteVersioned(NetPakWriter writer)
    {
        Version v = Accessor.DevkitServer.GetName().Version;
        writer.WriteUInt32((uint)(((byte)v.Major << 24) | ((byte)v.Minor << 16) | ((byte)v.Build << 8) | (byte)v.Revision));

        Write(writer);
    }
}