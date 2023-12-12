using System.Text.Json.Serialization;
using DevkitServer.Configuration;
using DevkitServer.Configuration.Converters;

namespace DevkitServer.API.Cartography;
public class JsonChartColorData : SchemaConfiguration
{
    private static JsonChartColorData? _defaultData;
    protected override string GetSchemaURI() => DevkitServerModule.GetRelativeRepositoryUrl("Module/Schemas/chart_config.json", true);
    public static JsonChartColorData Washington => _defaultData ??= new JsonChartColorData
    {
        HighwayColor     = new Color32(227, 119, 40,  255),
        RoadColor        = new Color32(217, 162, 54,  255),
        ObjectRoadColor  = new Color32(191, 191, 191, 255),
        PathColor        = new Color32(143, 132, 111, 255),
        CliffColor       = new Color32(127, 127, 127, 255),
        ResourceColor    = new Color32(94,  74,  54,  255),
        LargeColor       = new Color32(90,  90,  90,  255),
        MediumColor      = new Color32(120, 120, 120, 255),

        WaterColor       = new Color32(48,  90,  89,  255),

        HeightColorGraph =
        [
            new ColorGraphPoint
            {
                Height = 0f,
                Color = new Color32(85, 113, 78, 255),
                InterpolationMode = ColorGraphPointInterpolationMode.Constant,
                HeightUnit = ColorGraphPointHeightUnit.Relative
            },
            new ColorGraphPoint
            {
                Height = 0.03225806f,
                Color = new Color32(119, 119, 119, 255),
                InterpolationMode = ColorGraphPointInterpolationMode.Constant,
                HeightUnit = ColorGraphPointHeightUnit.Relative
            },
            new ColorGraphPoint
            {
                Height = 0.1612903f,
                Color = new Color32(90, 119, 82, 255),
                InterpolationMode = ColorGraphPointInterpolationMode.Constant,
                HeightUnit = ColorGraphPointHeightUnit.Relative
            },
            new ColorGraphPoint
            {
                Height = 0.2903226f,
                Color = new Color32(94, 125, 86, 255),
                InterpolationMode = ColorGraphPointInterpolationMode.Constant,
                HeightUnit = ColorGraphPointHeightUnit.Relative
            },
            new ColorGraphPoint
            {
                Height = 0.4193548f,
                Color = new Color32(100, 132, 91, 255),
                InterpolationMode = ColorGraphPointInterpolationMode.Constant,
                HeightUnit = ColorGraphPointHeightUnit.Relative
            },
            new ColorGraphPoint
            {
                Height = 0.516129f,
                Color = new Color32(102, 135, 92, 255),
                InterpolationMode = ColorGraphPointInterpolationMode.Constant,
                HeightUnit = ColorGraphPointHeightUnit.Relative
            },
            new ColorGraphPoint
            {
                Height = 0.6451613f,
                Color = new Color32(109, 144, 99, 255),
                InterpolationMode = ColorGraphPointInterpolationMode.Constant,
                HeightUnit = ColorGraphPointHeightUnit.Relative
            },
            new ColorGraphPoint
            {
                Height = 0.7741935f,
                Color = new Color32(114, 151, 104, 255),
                InterpolationMode = ColorGraphPointInterpolationMode.Constant,
                HeightUnit = ColorGraphPointHeightUnit.Relative
            },
            new ColorGraphPoint
            {
                Height = 0.9354839f,
                Color = new Color32(126, 160, 117, 255),
                InterpolationMode = ColorGraphPointInterpolationMode.Constant,
                HeightUnit = ColorGraphPointHeightUnit.Relative
            }
        ]
    };

    [JsonPropertyName("water_color")]
    [JsonConverter(typeof(ColorJsonConverter))]
    public Color WaterColor { get; set; }

    /// <summary>
    /// Concrete roads larger than 8m.
    /// </summary>
    [JsonPropertyName("highway_color")]
    [JsonConverter(typeof(ColorJsonConverter))]
    public Color HighwayColor { get; set; }

    /// <summary>
    /// Concrete roads smaller than 8m.
    /// </summary>
    [JsonPropertyName("road_color")]
    [JsonConverter(typeof(ColorJsonConverter))]
    public Color RoadColor { get; set; }

    /// <summary>
    /// Roads in object form.
    /// </summary>
    [JsonPropertyName("object_road_color")]
    [JsonConverter(typeof(ColorJsonConverter))]
    public Color ObjectRoadColor { get; set; }

    /// <summary>
    /// Non-concrete roads.
    /// </summary>
    [JsonPropertyName("path_color")]
    [JsonConverter(typeof(ColorJsonConverter))]
    public Color PathColor { get; set; }

    /// <summary>
    /// Cliffs and rocks in object form.
    /// </summary>
    [JsonPropertyName("cliff_color")]
    [JsonConverter(typeof(ColorJsonConverter))]
    public Color CliffColor { get; set; }

    /// <summary>
    /// Default color of resources.
    /// </summary>
    [JsonPropertyName("resource_color")]
    [JsonConverter(typeof(ColorJsonConverter))]
    public Color ResourceColor { get; set; }

    /// <summary>
    /// Objects of type 'LARGE'.
    /// </summary>
    [JsonPropertyName("object_large_color")]
    [JsonConverter(typeof(ColorJsonConverter))]
    public Color LargeColor { get; set; }

    /// <summary>
    /// Objects of type 'MEDIUM'.
    /// </summary>
    [JsonPropertyName("object_medium_color")]
    [JsonConverter(typeof(ColorJsonConverter))]
    public Color MediumColor { get; set; }

    /// <summary>
    /// Represents keyframes on a color graph.
    /// </summary>
    [JsonPropertyName("height_color_graph")]
    public ColorGraphPoint[]? HeightColorGraph { get; set; }
}

/// <summary>
/// Represents a point on a color graph.
/// </summary>
public class ColorGraphPoint
{
    [JsonPropertyName("height")]
    public float Height { get; set; }

    [JsonPropertyName("color")]
    [JsonConverter(typeof(ColorJsonConverter))]
    public Color Color { get; set; }

    [JsonPropertyName("interpolation_mode")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ColorGraphPointInterpolationMode InterpolationMode { get; set; }

    [JsonPropertyName("height_unit")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ColorGraphPointHeightUnit HeightUnit { get; set; }
}

public enum ColorGraphPointInterpolationMode
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

public enum ColorGraphPointHeightUnit
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