using SDG.NetPak;
using Version = System.Version;

namespace DevkitServer.Multiplayer.Movement;
internal struct EditorInputPacket
{
    public uint ClientInputFrame;
    public Vector3 Position;
    public Vector2 Rotation;
    public bool LastFrameBeforeChangingController;
    public float DeltaTime;

    private const int PosIntBitCt = 16;
    private const int PosDecBitCt = 8;
    private const int RotIntBitCt = 9;
    private const int RotDecBitCt = 7;
    private const int DtIntBitCt = 4;
    private const int DtDecBitCt = 11;
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
        if (!reader.ReadClampedFloat(DtIntBitCt, DtDecBitCt, out packet.DeltaTime))
            return false;

        return reader.ReadBit(out packet.LastFrameBeforeChangingController);
    }
    public readonly void Write(NetPakWriter writer)
    {
        writer.WriteUInt32(ClientInputFrame);
        writer.WriteClampedVector3(Position, PosIntBitCt, PosDecBitCt);
        writer.WriteClampedFloat(Rotation.x, RotIntBitCt, RotDecBitCt);
        writer.WriteClampedFloat(Rotation.y, RotIntBitCt, RotDecBitCt);
        writer.WriteClampedFloat(DeltaTime, DtIntBitCt, DtDecBitCt);

        writer.WriteBit(LastFrameBeforeChangingController);
    }
}