using DevkitServer.Configuration;
using System.Text.Json.Serialization;

namespace DevkitServer.Core.Cartography.ChartColorProviders;

public class JsonChartColorData : SchemaConfiguration
{
    private static JsonChartColorData? _defaultData;
    protected override string GetSchemaURI() => DevkitServerModule.GetRelativeRepositoryUrl("Module/Schemas/chart_config.json", true);

    /// <summary>
    /// Default config (also the values for the Washington map).
    /// </summary>
    public static JsonChartColorData Defaults => _defaultData ??= new JsonChartColorData
    {
        FallbackColor = new Color32(255, 0, 255, 255),

        HighwayColor = new Color32(227, 119, 40, 255),
        RoadColor = new Color32(217, 162, 54, 255),
        ObjectRoadColor = new Color32(191, 191, 191, 255),
        PathColor = new Color32(143, 132, 111, 255),
        CliffColor = new Color32(127, 127, 127, 255),
        ResourceColor = new Color32(94, 74, 54, 255),
        LargeColor = new Color32(90, 90, 90, 255),
        MediumColor = new Color32(120, 120, 120, 255),

        WaterColor = new Color32(48, 90, 89, 255),

        HeightColorGraph =
        [
            new ColorGraphKeyframe
            {
                Height = 0f,
                Color = new Color32(119, 119, 119, 255),
                InterpolationMode = ColorGraphKeyframeInterpolationMode.Constant,
                HeightUnit = ColorGraphKeyframeHeightUnit.Relative
            },
            new ColorGraphKeyframe
            {
                Height = 0.03225806f,
                Color = new Color32(85, 113, 78, 255),
                InterpolationMode = ColorGraphKeyframeInterpolationMode.Constant,
                HeightUnit = ColorGraphKeyframeHeightUnit.Relative
            },
            new ColorGraphKeyframe
            {
                Height = 0.1612903f,
                Color = new Color32(90, 119, 82, 255),
                InterpolationMode = ColorGraphKeyframeInterpolationMode.Constant,
                HeightUnit = ColorGraphKeyframeHeightUnit.Relative
            },
            new ColorGraphKeyframe
            {
                Height = 0.2903226f,
                Color = new Color32(94, 125, 86, 255),
                InterpolationMode = ColorGraphKeyframeInterpolationMode.Constant,
                HeightUnit = ColorGraphKeyframeHeightUnit.Relative
            },
            new ColorGraphKeyframe
            {
                Height = 0.4193548f,
                Color = new Color32(100, 132, 91, 255),
                InterpolationMode = ColorGraphKeyframeInterpolationMode.Constant,
                HeightUnit = ColorGraphKeyframeHeightUnit.Relative
            },
            new ColorGraphKeyframe
            {
                Height = 0.516129f,
                Color = new Color32(102, 135, 92, 255),
                InterpolationMode = ColorGraphKeyframeInterpolationMode.Constant,
                HeightUnit = ColorGraphKeyframeHeightUnit.Relative
            },
            new ColorGraphKeyframe
            {
                Height = 0.6451613f,
                Color = new Color32(109, 144, 99, 255),
                InterpolationMode = ColorGraphKeyframeInterpolationMode.Constant,
                HeightUnit = ColorGraphKeyframeHeightUnit.Relative
            },
            new ColorGraphKeyframe
            {
                Height = 0.7741935f,
                Color = new Color32(114, 151, 104, 255),
                InterpolationMode = ColorGraphKeyframeInterpolationMode.Constant,
                HeightUnit = ColorGraphKeyframeHeightUnit.Relative
            },
            new ColorGraphKeyframe
            {
                Height = 0.9354839f,
                Color = new Color32(126, 160, 117, 255),
                InterpolationMode = ColorGraphKeyframeInterpolationMode.Constant,
                HeightUnit = ColorGraphKeyframeHeightUnit.Relative
            }
        ]
    };

    [JsonPropertyName("water_color")]
    public Color32? WaterColor { get; set; }

    /// <summary>
    /// Concrete roads larger than 8m.
    /// </summary>
    [JsonPropertyName("highway_color")]
    public Color32? HighwayColor { get; set; }

    /// <summary>
    /// Concrete roads smaller than 8m.
    /// </summary>
    [JsonPropertyName("road_color")]
    public Color32? RoadColor { get; set; }

    /// <summary>
    /// Roads in object form.
    /// </summary>
    [JsonPropertyName("object_road_color")]
    public Color32? ObjectRoadColor { get; set; }

    /// <summary>
    /// Non-concrete roads.
    /// </summary>
    [JsonPropertyName("path_color")]
    public Color32? PathColor { get; set; }

    /// <summary>
    /// Cliffs and rocks in object form.
    /// </summary>
    [JsonPropertyName("cliff_color")]
    public Color32? CliffColor { get; set; }

    /// <summary>
    /// Default color of resources.
    /// </summary>
    [JsonPropertyName("resource_color")]
    public Color32? ResourceColor { get; set; }

    /// <summary>
    /// Objects of type 'LARGE'.
    /// </summary>
    [JsonPropertyName("object_large_color")]
    public Color32? LargeColor { get; set; }

    /// <summary>
    /// Objects of type 'MEDIUM'.
    /// </summary>
    [JsonPropertyName("object_medium_color")]
    public Color32? MediumColor { get; set; }

    /// <summary>
    /// Any other colors. Usually not used.
    /// </summary>
    [JsonPropertyName("fallback_color")]
    public Color32? FallbackColor { get; set; }

    /// <summary>
    /// Represents keyframes on a color graph.
    /// </summary>
    [JsonPropertyName("height_color_graph")]
    public ColorGraphKeyframe[]? HeightColorGraph { get; set; }

    /// <summary>
    /// Solve <see cref="HeightColorGraph"/> for the given minimum height, maximum height, and y value all in world coordinates.
    /// </summary>
    public Color32? InterpolateChart(float minimumHeight, float maximumHeight, float worldHeight)
    {
        if (HeightColorGraph is not { Length: > 0 })
            return null;

        float heightAlpha = Mathf.InverseLerp(minimumHeight, maximumHeight, worldHeight);
        int keyFrameIndex = -1;
        for (int i = 0; i < HeightColorGraph.Length; ++i)
        {
            ColorGraphKeyframe keyFrame = HeightColorGraph[i];

            float h = keyFrame.Height;
            if (keyFrame.HeightUnit == ColorGraphKeyframeHeightUnit.Absolute)
                h = Mathf.Lerp(minimumHeight, maximumHeight, keyFrame.Height);

            if (h <= heightAlpha)
                continue;

            keyFrameIndex = i == 0 ? 0 : i - 1;
            break;
        }

        if (keyFrameIndex == -1)
            keyFrameIndex = HeightColorGraph.Length - 1;

        ColorGraphKeyframe lowerKeyFrame = HeightColorGraph[keyFrameIndex];
        ColorGraphKeyframe? higherKeyFrame = keyFrameIndex == HeightColorGraph.Length - 1 ? null : HeightColorGraph[keyFrameIndex + 1];

        if (higherKeyFrame == null || lowerKeyFrame.InterpolationMode is not ColorGraphKeyframeInterpolationMode.Linear and not ColorGraphKeyframeInterpolationMode.Smooth)
            return lowerKeyFrame.Color with { a = 255 };

        float h1 = lowerKeyFrame.Height, h2 = higherKeyFrame.Height;
        bool isRelative = false;
        if (lowerKeyFrame.HeightUnit != higherKeyFrame.HeightUnit)
        {
            isRelative = true;
            if (lowerKeyFrame.HeightUnit == ColorGraphKeyframeHeightUnit.Absolute)
                h1 = Mathf.Lerp(minimumHeight, maximumHeight, lowerKeyFrame.Height);
            else if (higherKeyFrame.HeightUnit == ColorGraphKeyframeHeightUnit.Absolute)
                h2 = Mathf.Lerp(minimumHeight, maximumHeight, higherKeyFrame.Height);
        }

        float t = Mathf.InverseLerp(h1, h2, !isRelative && lowerKeyFrame.HeightUnit == ColorGraphKeyframeHeightUnit.Absolute ? worldHeight : heightAlpha);

        if (lowerKeyFrame.InterpolationMode == ColorGraphKeyframeInterpolationMode.Smooth)
        {
            // https://easings.net/#easeInOutQuad
            float f2 = -2 * t + 2;
            t = t < 0.5f ? 2 * t * t : 1 - f2 * f2 / 2f;
        }

        return Color32.Lerp(lowerKeyFrame.Color, higherKeyFrame.Color, t) with { a = 255 };
    }
}

/// <summary>
/// Represents a keyframe on a color graph.
/// </summary>
public class ColorGraphKeyframe
{
    /// <summary>
    /// The height (absolute or relative, depending on <seealso cref="HeightUnit"/>) of the start of this keyframe.
    /// </summary>
    [JsonPropertyName("height")]
    public float Height { get; set; }

    /// <summary>
    /// The color value of the keyframe.
    /// </summary>
    [JsonPropertyName("color")]
    public Color32 Color { get; set; }

    /// <summary>
    /// How the keyframe is interpolated between this and the next keyframe.
    /// </summary>
    [JsonPropertyName("interpolation_mode")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ColorGraphKeyframeInterpolationMode InterpolationMode { get; set; }

    /// <summary>
    /// How to interpret <see cref="Height"/>.
    /// <para><see cref="ColorGraphKeyframeHeightUnit.Relative"/> height is a value from zero to one spanning from sea level to the max possible terrain height (not the max currently visible).</para>
    /// <para><see cref="ColorGraphKeyframeHeightUnit.Absolute"/> height is an absolute Y value in world coordinates.</para>
    /// </summary>
    [JsonPropertyName("height_unit")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ColorGraphKeyframeHeightUnit HeightUnit { get; set; }

    /// <summary>
    /// Get a unit-invariant comparer for <see cref="ColorGraphKeyframe"/>s.
    /// </summary>
    /// <param name="min">Minimum height in world coordinates.</param>
    /// <param name="max">Maximum height in world coordinates.</param>
    public static IComparer<ColorGraphKeyframe> GetComparer(float min, float max) => new Comparer(min, max);
    private class Comparer(float min, float max) : IComparer<ColorGraphKeyframe>
    {
        int IComparer<ColorGraphKeyframe>.Compare(ColorGraphKeyframe? x, ColorGraphKeyframe? y)
        {
            if (x == null)
                return -1;
            if (y == null)
                return 1;

            if (x.HeightUnit == y.HeightUnit)
                return x.Height.CompareTo(y.Height);

            float heightA = x.HeightUnit == ColorGraphKeyframeHeightUnit.Absolute ? Mathf.Lerp(min, max, x.Height) : x.Height;
            float heightB = y.HeightUnit == ColorGraphKeyframeHeightUnit.Absolute ? Mathf.Lerp(min, max, y.Height) : y.Height;
            return heightA.CompareTo(heightB);
        }
    }
}

/// <summary>
/// Defines a type of interpolation for <see cref="ColorGraphKeyframe"/>.
/// </summary>
public enum ColorGraphKeyframeInterpolationMode
{
    /// <summary>
    /// Linear interpolation from this graph point to the next one.
    /// </summary>
    Linear,

    /// <summary>
    /// Smooth interpolation from this graph point to the next one.
    /// </summary>
    Smooth,

    /// <summary>
    /// Constant color until the next graph point.
    /// </summary>
    Constant
}

/// <summary>
/// Defines absolute or relative height for <see cref="ColorGraphKeyframe"/>.
/// </summary>
public enum ColorGraphKeyframeHeightUnit
{
    /// <summary>
    /// Values is from zero to one.
    /// </summary>
    Relative,

    /// <summary>
    /// Value is an absolute Y value.
    /// </summary>
    Absolute
}