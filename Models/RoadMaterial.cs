using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using DanielWillett.SpeedBytes;

namespace DevkitServer.Models;

/// <summary>
/// A reference to a <see cref="RoadMaterial"/> or <see cref="RoadAsset"/>.
/// </summary>
public readonly struct RoadMaterialOrAsset :
    IEquatable<RoadMaterialOrAsset>,
    IEquatable<RoadMaterial?>,
    IEquatable<RoadAsset?>,
    IEquatable<byte>,
    IEquatable<Guid>,
    IEquatable<AssetReference<RoadAsset>>,
    IEquatable<CachingAssetRef>,
    IEquatable<CachingBcAssetRef>
{
    public static readonly RoadMaterialOrAsset Empty = new RoadMaterialOrAsset(byte.MaxValue);

    /// <summary>
    /// Guid of a <see cref="RoadAsset"/>. Only used when <see cref="LegacyIndex"/> is 255.
    /// </summary>
    [JsonInclude]
    public readonly Guid Guid;

    /// <summary>
    /// Index of a material in <see cref="LevelRoads.materials"/>, or 255 if <see cref="Guid"/> should be used instead.
    /// </summary>
    [JsonInclude]
    public readonly byte LegacyIndex;

    /// <summary>
    /// If this value represents a <see cref="RoadMaterial"/>.
    /// </summary>
    public bool IsLegacyMaterial => LegacyIndex != byte.MaxValue;

    /// <summary>
    /// The currently selected road asset.
    /// </summary>
    public static RoadMaterialOrAsset Selected
    {
        get
        {
            ThreadUtil.assertIsGameThread();

            RoadAsset? roadAsset = EditorRoads.selectedAssetRef.Get<RoadAsset>();
            if (roadAsset == null)
                return new RoadMaterialOrAsset(EditorRoads.selected);

            return new RoadMaterialOrAsset(roadAsset.GUID);
        }
    }

    public RoadMaterialOrAsset(Guid guid)
    {
        Guid = guid;
        LegacyIndex = byte.MaxValue;
    }

    public RoadMaterialOrAsset(byte material)
    {
        LegacyIndex = material;
    }

    public RoadMaterialOrAsset(ByteReader reader)
    {
        LegacyIndex = reader.ReadUInt8();
        if (LegacyIndex == byte.MaxValue)
            Guid = reader.ReadGuid();
    }

    public void Write(ByteWriter writer)
    {
        writer.Write(LegacyIndex);
        if (LegacyIndex == byte.MaxValue)
            writer.Write(Guid);
    }

    public bool CheckValid()
    {
        return IsLegacyMaterial ? LegacyIndex < LevelRoads.materials.Length : Assets.find<RoadAsset>(Guid) != null;
    }

    public bool TryGetAsset([MaybeNullWhen(false)] out RoadAsset roadAsset)
    {
        if (LegacyIndex != byte.MaxValue)
        {
            roadAsset = null;
            return false;
        }

        roadAsset = Assets.find<RoadAsset>(Guid);
        return true;
    }

    public bool TryGetMaterial([MaybeNullWhen(false)] out RoadMaterial material)
    {
        if (LegacyIndex == byte.MaxValue || LegacyIndex >= LevelRoads.materials.Length)
        {
            material = null;
            return false;
        }

        material = LevelRoads.materials[LegacyIndex];
        return true;
    }

    public bool IsSameAsMaterialOf(Road road)
    {
        if (road == null)
            return false;

        RoadAsset? asset = road.GetRoadAsset();
        return asset != null ? Equals(asset) : Equals(road.material);
    }

    /// <inheritdoc />
    public bool Equals(RoadMaterialOrAsset other)
    {
        if (other.LegacyIndex == byte.MaxValue)
        {
            return LegacyIndex == byte.MaxValue && Guid == other.Guid;
        }

        return LegacyIndex == other.LegacyIndex;
    }

    /// <inheritdoc />
    public bool Equals(RoadMaterial? other)
    {
        if (other == null)
        {
            return LegacyIndex == byte.MaxValue && Guid == Guid.Empty;
        }

        return LegacyIndex != byte.MaxValue && LegacyIndex == Array.IndexOf(LevelRoads.materials, other);
    }

    /// <inheritdoc />
    public bool Equals(RoadAsset? other)
    {
        if (other == null)
        {
            return LegacyIndex == byte.MaxValue && Guid == Guid.Empty;
        }

        return LegacyIndex == byte.MaxValue && Guid == other.GUID;
    }

    /// <inheritdoc />
    public bool Equals(byte other)
    {
        return other != byte.MaxValue && LegacyIndex == other;
    }

    /// <inheritdoc />
    public bool Equals(Guid other)
    {
        return LegacyIndex == byte.MaxValue && Guid == other;
    }

    /// <inheritdoc />
    public bool Equals(AssetReference<RoadAsset> other)
    {
        return LegacyIndex == byte.MaxValue && Guid == other.GUID;
    }

    /// <inheritdoc />
    public bool Equals(CachingAssetRef other)
    {
        return LegacyIndex == byte.MaxValue && Guid == other.Guid;
    }

    /// <inheritdoc />
    public bool Equals(CachingBcAssetRef other)
    {
        return LegacyIndex == byte.MaxValue && other.LegacyId == 0 && Guid == other.Guid;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        if (IsLegacyMaterial)
        {
            if (TryGetMaterial(out RoadMaterial? mat))
            {
                return mat.MaterialToString();
            }

            return $"Material #{LegacyIndex}";
        }
        
        if (TryGetAsset(out RoadAsset? asset))
        {
            return asset.FriendlyName ?? asset.name;
        }

        return $"Material: {{{Guid:N}}}";
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is RoadMaterialOrAsset a && Equals(a);

    /// <inheritdoc />
    public override int GetHashCode() => LegacyIndex == byte.MaxValue ? Guid.GetHashCode() : LegacyIndex;

    public static bool operator ==(RoadMaterialOrAsset left, RoadMaterialOrAsset right) => left.Equals(right);
    public static bool operator !=(RoadMaterialOrAsset left, RoadMaterialOrAsset right) => !left.Equals(right);
}