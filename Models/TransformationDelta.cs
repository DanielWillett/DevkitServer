using DevkitServer.Util.Encoding;

namespace DevkitServer.Models;

public readonly struct TransformationDelta
{
    public const int Capacity = 57;
    public const int CapacityHalfPrecision = 29;
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
    public TransformationDelta(ByteReader reader, bool halfPrecision = false)
    {
        TransformFlags flags = reader.ReadEnum<TransformFlags>();
        Flags = flags & ~(TransformFlags)8;
        if ((flags & TransformFlags.Position) != 0)
            Position = halfPrecision ? reader.ReadHalfPrecisionVector3() : reader.ReadVector3();
        else
            Position = Vector3.zero;
        if ((flags & TransformFlags.OriginalPosition) != 0)
            OriginalPosition = halfPrecision ? reader.ReadHalfPrecisionVector3() : reader.ReadVector3();
        else
            OriginalPosition = Vector3.zero;
        if ((flags & TransformFlags.Rotation) != 0)
            Rotation = halfPrecision ? reader.ReadHalfPrecisionQuaternion() : reader.ReadQuaternion();
        else
            Rotation = Quaternion.identity;
        if ((flags & TransformFlags.OriginalRotation) != 0)
            OriginalRotation = halfPrecision ? reader.ReadHalfPrecisionQuaternion() : reader.ReadQuaternion();
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
    public int CalculateSize(bool halfPrecision = false)
    {
        int size = 0;
        if ((Flags & TransformFlags.Position) != 0)
            size += 12;
        if ((Flags & TransformFlags.OriginalPosition) != 0)
            size += 12;
        if ((Flags & TransformFlags.Rotation) != 0)
            size += 16;
        if ((Flags & TransformFlags.OriginalRotation) != 0)
            size += 16;
        if (halfPrecision) size /= 2;
        return size + 1;
    }
    public static int CalculatePartialSize(TransformFlags flags, bool halfPrecision = false)
    {
        int size = 0;
        if ((flags & TransformFlags.Position) != 0)
            size += 12;
        if ((flags & TransformFlags.Rotation) != 0)
            size += 16;
        if (halfPrecision) size /= 2;
        return size;
    }
    public void WriteHalfPrecision(ByteWriter writer)
    {
        writer.Write(Flags);
        if ((Flags & TransformFlags.Position) != 0)
            writer.WriteHalfPrecision(Position);
        if ((Flags & TransformFlags.OriginalPosition) != 0)
            writer.WriteHalfPrecision(OriginalPosition);
        if ((Flags & TransformFlags.Rotation) != 0)
            writer.WriteHalfPrecision(Rotation);
        if ((Flags & TransformFlags.OriginalRotation) != 0)
            writer.WriteHalfPrecision(OriginalRotation);
    }
    public void WritePartial(ByteWriter writer, TransformFlags flags)
    {
        if ((flags & TransformFlags.OriginalPosition) != 0)
            writer.Write(OriginalPosition);
        if ((flags & TransformFlags.OriginalRotation) != 0)
            writer.Write(OriginalRotation);
    }
    public void ApplyTo(Transform transform, bool additive)
    {
        if ((Flags & TransformFlags.Rotation) == 0)
        {
            if ((Flags & TransformFlags.Position) != 0)
                transform.position = additive ? OriginalPosition + Position : Position;
        }
        else
        {
            if ((Flags & TransformFlags.Position) != 0)
                transform.SetPositionAndRotation(additive ? OriginalPosition + Position : Position, additive ? OriginalRotation * Rotation : Rotation);
            else
                transform.rotation = additive ? OriginalRotation * Rotation : Rotation;
        }
    }

    public override string ToString() => $"Moved: {Flags.Format()} (pos: {Position.Format()}, rot: {Rotation.eulerAngles.Format()}) from (pos: {OriginalRotation.Format()}, rot: {OriginalRotation.eulerAngles.Format()}).";
}
