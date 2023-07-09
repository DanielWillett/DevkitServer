using DevkitServer.Util.Encoding;

namespace DevkitServer.Models;
public readonly struct PreviewTransformation
{
    public const int CapacityHalfPrecision = 4 + TransformationDelta.CapacityHalfPrecision;
    public const int Capacity = 4 + TransformationDelta.Capacity;
    public NetId NetId { get; }
    public TransformationDelta Transformation { get; }
    public Vector3 Pivot { get; }
    public PreviewTransformation(NetId netId, TransformationDelta transformation, Vector3 pivot)
    {
        NetId = netId;
        Transformation = transformation;
        Pivot = pivot;
    }
    public PreviewTransformation(ByteReader reader, bool halfPrecision = true)
    {
        NetId = reader.ReadNetId();
        Transformation = new TransformationDelta(reader, halfPrecision);
        Pivot = halfPrecision ? reader.ReadHalfPrecisionVector3() : reader.ReadVector3();
    }
    public void Write(ByteWriter writer, bool halfPrecision = true)
    {
        writer.Write(NetId);
        if (halfPrecision)
        {
            Transformation.WriteHalfPrecision(writer);
            writer.WriteHalfPrecision(Pivot);
        }
        else
        {
            Transformation.Write(writer);
            writer.Write(Pivot);
        }
    }
    public void Apply(Transform transform)
    {
        TransformationDelta transformationDelta = Transformation;
        if ((transformationDelta.Flags & TransformationDelta.TransformFlags.Position) != 0 && (transformationDelta.Flags & TransformationDelta.TransformFlags.Rotation) != 0)
        {
            Vector3 deltaPos = transformationDelta.OriginalPosition - Pivot;
            Vector3 position = deltaPos.IsNearlyZero() ? transformationDelta.OriginalPosition + transformationDelta.Position : Pivot + transformationDelta.Rotation * deltaPos + transformationDelta.Position;
            Quaternion rotation = Transformation.Rotation * Transformation.OriginalRotation;
            transform.SetPositionAndRotation(position, rotation);
        }
        else if ((transformationDelta.Flags & TransformationDelta.TransformFlags.Position) != 0)
            transform.position = transformationDelta.OriginalPosition + transformationDelta.Position;
        else if ((transformationDelta.Flags & TransformationDelta.TransformFlags.Rotation) != 0)
            transform.rotation = Transformation.Rotation * Transformation.OriginalRotation;
    }
    public int CalculateSize(bool halfPrecision = true) => 4 + Transformation.CalculateSize(halfPrecision) + (halfPrecision ? 6 : 12);
}

public readonly struct FinalTransformation
{
    public const int Capacity = 28 + TransformationDelta.Capacity;
    public const int CapacityHalfPrecision = 16 + TransformationDelta.CapacityHalfPrecision;
    public NetId NetId { get; }
    public TransformationDelta Transformation { get; }
    public Vector3 Scale { get; }
    public Vector3 OriginalScale { get; }
    public FinalTransformation(NetId netId, TransformationDelta transformation, Vector3 scale, Vector3 originalScale)
    {
        NetId = netId;
        Transformation = transformation;
        Scale = scale;
        OriginalScale = originalScale;
    }
    public FinalTransformation(NetId netId, TransformationDelta transformation)
    {
        NetId = netId;
        Transformation = transformation;
        Scale = Vector3.one;
        OriginalScale = Vector3.one;
    }
    public FinalTransformation(ByteReader reader, bool useScale, bool halfPrecision = false)
    {
        NetId = reader.ReadNetId();
        Transformation = new TransformationDelta(reader, halfPrecision);
        if (!useScale)
        {
            Scale = Vector3.one;
            OriginalScale = Vector3.one;
            return;
        }
        if (halfPrecision)
        {
            Scale = reader.ReadHalfPrecisionVector3();
            OriginalScale = reader.ReadHalfPrecisionVector3();
        }
        else
        {
            Scale = reader.ReadVector3();
            OriginalScale = reader.ReadVector3();
        }
    }
    public void Write(ByteWriter writer, bool useScale, bool halfPrecision = false)
    {
        writer.Write(NetId);
        if (halfPrecision)
            Transformation.WriteHalfPrecision(writer);
        else
            Transformation.Write(writer);
        if (!useScale)
            return;
        if (halfPrecision)
        {
            writer.WriteHalfPrecision(Scale);
            writer.WriteHalfPrecision(OriginalScale);
        }
        else
        {
            writer.Write(Scale);
            writer.Write(OriginalScale);
        }
    }
    public int CalculateSize(bool useScale, bool halfPrecision = false)
    {
        int size = 4 + Transformation.CalculateSize(halfPrecision);
        if (useScale)
            size += halfPrecision ? 12 : 24;
        return size;
    }
}
