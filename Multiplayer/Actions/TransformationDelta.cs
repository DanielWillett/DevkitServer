using DevkitServer.Util.Encoding;

namespace DevkitServer.Multiplayer.Actions;

public readonly struct TransformationDelta
{
    public const int Capacity = 15;
    public TransformFlags Flags { get; }
    public Vector3 Position { get; }
    public Quaternion Rotation { get; }
    public Vector3 OriginalPosition { get; }
    public Quaternion OriginalRotation { get; }
    public TransformationDelta(ByteReader reader, TransformFlags flags, Vector3 position, Quaternion rotation)
    {
        Flags = flags;
        Position = position;
        Rotation = rotation;
        if ((flags & TransformFlags.OriginalPosition) != 0)
            OriginalPosition = reader.ReadVector3();
        else OriginalPosition = default;
        if ((flags & TransformFlags.Rotation) != 0)
            OriginalRotation = reader.ReadQuaternion();
        else OriginalRotation = Quaternion.identity;
    }
    public TransformationDelta(ByteReader reader)
    {
        TransformFlags flags = reader.ReadEnum<TransformFlags>();
        Flags = flags & ~(TransformFlags)8;
        if ((flags & TransformFlags.Position) != 0)
            Position = reader.ReadVector3();
        else
            Position = Vector3.zero;
        if ((flags & TransformFlags.OriginalPosition) != 0)
            OriginalPosition = reader.ReadVector3();
        else
            OriginalPosition = Vector3.zero;
        if ((flags & TransformFlags.Rotation) != 0)
            Rotation = reader.ReadQuaternion();
        else
            Rotation = Quaternion.identity;
        if ((flags & TransformFlags.OriginalRotation) != 0)
            OriginalRotation = reader.ReadQuaternion();
        else
            OriginalRotation = Quaternion.identity;
    }
    public TransformationDelta(TransformFlags flags, Vector3 position, Quaternion rotation, Vector3 originalPosition, Quaternion originalRotation)
    {
        Flags = flags;
        Position = position;
        Rotation = rotation;
        OriginalPosition = originalPosition;
        OriginalRotation = originalRotation;
    }

    [Flags]
    public enum TransformFlags : byte
    {
        Position = 1,
        Rotation = 2,
        OriginalPosition = 4,
        OriginalRotation = 8,
        AllNew = Position | Rotation,
        AllOriginal = OriginalPosition | OriginalRotation,
        All = AllNew | AllOriginal
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(Flags);
        if ((Flags & TransformFlags.Position) != 0)
            writer.Write(Position);
        if ((Flags & TransformFlags.OriginalPosition) != 0)
            writer.Write(OriginalPosition);
        if ((Flags & TransformFlags.Rotation) != 0)
            writer.Write(Rotation);
        if ((Flags & TransformFlags.OriginalRotation) != 0)
            writer.Write(OriginalRotation);
    }
    public void WritePartial(ByteWriter writer, TransformFlags flags)
    {
        if ((flags & TransformFlags.OriginalPosition) != 0)
            writer.Write(OriginalPosition);
        if ((flags & TransformFlags.OriginalRotation) != 0)
            writer.Write(OriginalRotation);
    }

    public override string ToString() => $"Moved: {Flags.Format()} (pos: {Position.Format()}, rot: {Rotation.eulerAngles.Format()}) from (pos: {OriginalRotation.Format()}, rot: {OriginalRotation.eulerAngles.Format()}).";
}
