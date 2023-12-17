using DevkitServer.API;
using DevkitServer.API.Cartography;
using DevkitServer.API.Cartography.ChartColorProviders;
using DevkitServer.Configuration;
using DevkitServer.Util.Encoding;
using SDG.Framework.Landscapes;
using System.Text.Json;

namespace DevkitServer.Core.Cartography.ChartColorProviders;

[LoadPriority(-1)]
public class JsonChartColorProvider : RaycastChartColorProvider
{
    public JsonChartColorData Data { get; protected set; } = null!;

    protected Color32 HighwayColor;
    protected Color32 RoadColor;
    protected Color32 ObjectRoadColor;
    protected Color32 PathColor;
    protected Color32 CliffColor;
    protected Color32 ResourceColor;
    protected Color32 LargeColor;
    protected Color32 MediumColor;
    protected Color32 WaterColor;
    protected Color32 FallbackColor;

    public override bool TryInitialize(in CartographyCaptureData data, bool isExplicitlyDefined)
    {
        string path = Path.Combine(data.Level.path, "Editor", "chart_colors.json");
        if (!File.Exists(path))
        {
            path = Path.Combine(data.Level.path, "Editor", "Charts.json");
            if (!File.Exists(path))
            {
                path = Path.Combine(data.Level.path, "Charts.json");
                if (!File.Exists(path))
                {
                    path = Path.Combine(data.Level.path, "Charts.json");
                    if (!File.Exists(path))
                    {
                        if (isExplicitlyDefined)
                            Logger.LogInfo($"[{nameof(JsonChartColorProvider)}] Skipping because there is no chart data at {path.Format()}.");
                        else
                            Logger.LogDebug($"[{nameof(JsonChartColorProvider)}] Skipping because there is no chart data at {path.Format()}.");
                        return false;
                    }
                }
            }
        }

        try
        {
            ArraySegment<byte> bytes;

            using (Utf8JsonPreProcessingStream stream = new Utf8JsonPreProcessingStream(path))
                bytes = stream.ReadAllBytes();

            Utf8JsonReader reader = new Utf8JsonReader(bytes, DevkitServerConfig.ReaderOptions);
            Data = JsonSerializer.Deserialize<JsonChartColorData>(ref reader, DevkitServerConfig.SerializerSettings);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Unable to read {path.Format()} for chart data. Json parser exception. Using fallback renderer.", method: nameof(JsonChartColorProvider));
            Logger.LogInfo($"[{nameof(JsonChartColorProvider)}] Check {DevkitServerModule.GetRelativeRepositoryUrl("Documentation/JSON Chart Colors.md", false).Format(false)} for how to format your color provider config.");
            Logger.LogError(ex, method: nameof(JsonChartColorProvider));
            return false;
        }

        if (Data == null)
        {
            Logger.LogWarning($"Unable to read {path.Format()} for chart data. No value available. Using fallback renderer.", method: nameof(JsonChartColorProvider));
            Logger.LogInfo($"[{nameof(JsonChartColorProvider)}] Check {DevkitServerModule.GetRelativeRepositoryUrl("Documentation/JSON Chart Colors.md", false).Format(false)} for how to format your color provider config.");
            return false;
        }

        if (Data.HeightColorGraph is not { Length: > 0 })
        {
            Logger.LogInfo($"[{nameof(JsonChartColorProvider)}] No terrain data is available in JSON color data at {path.Format()}. They'll be replaced by {"Washington".Colorize(DevkitServerModule.UnturnedColor)} defaults.");
            Logger.LogInfo($"[{nameof(JsonChartColorProvider)}] Check {DevkitServerModule.GetRelativeRepositoryUrl("Documentation/JSON Chart Colors.md", false).Format(false)} for more info on how to set up custom colors.");

            Data.HeightColorGraph = JsonChartColorData.Defaults.HeightColorGraph!;
            for (int i = 0; i < Data.HeightColorGraph.Length; ++i)
            {
                ref ColorGraphKeyframe keyframe = ref Data.HeightColorGraph[i];
                keyframe = new ColorGraphKeyframe
                {
                    Color = keyframe.Color with { a = 255 },
                    Height = keyframe.Height,
                    HeightUnit = keyframe.HeightUnit,
                    InterpolationMode = keyframe.InterpolationMode
                };
            }
        }
        else
        {
            for (int i = 0; i < Data.HeightColorGraph.Length; ++i)
            {
                Data.HeightColorGraph[i].Color = Data.HeightColorGraph[i].Color with { a = 255 };
            }
        }

        Array.Sort(Data.HeightColorGraph, ColorGraphKeyframe.GetComparer(data.MinHeight, data.MaxHeight));

        if (Data is not
            {
                HighwayColor: not null,
                RoadColor: not null,
                ObjectRoadColor: not null,
                PathColor: not null,
                CliffColor: not null,
                ResourceColor: not null,
                LargeColor: not null,
                MediumColor: not null,
                WaterColor: not null
            })
        {
            Logger.LogInfo($"[{nameof(JsonChartColorProvider)}] Some values are missing from JSON color data at {path.Format()}. They'll be replaced by {"Washington".Colorize(DevkitServerModule.UnturnedColor)} defaults.");
        }

        HighwayColor = Data.HighwayColor.GetValueOrDefault(JsonChartColorData.Defaults.HighwayColor!.Value) with { a = 255 };
        RoadColor = Data.RoadColor.GetValueOrDefault(JsonChartColorData.Defaults.RoadColor!.Value) with { a = 255 };
        ObjectRoadColor = Data.ObjectRoadColor.GetValueOrDefault(JsonChartColorData.Defaults.ObjectRoadColor!.Value) with { a = 255 };
        PathColor = Data.PathColor.GetValueOrDefault(JsonChartColorData.Defaults.PathColor!.Value) with { a = 255 };
        CliffColor = Data.CliffColor.GetValueOrDefault(JsonChartColorData.Defaults.CliffColor!.Value) with { a = 255 };
        ResourceColor = Data.ResourceColor.GetValueOrDefault(JsonChartColorData.Defaults.ResourceColor!.Value) with { a = 255 };
        LargeColor = Data.LargeColor.GetValueOrDefault(JsonChartColorData.Defaults.LargeColor!.Value) with { a = 255 };
        MediumColor = Data.MediumColor.GetValueOrDefault(JsonChartColorData.Defaults.MediumColor!.Value) with { a = 255 };
        FallbackColor = Data.FallbackColor.GetValueOrDefault(JsonChartColorData.Defaults.FallbackColor!.Value) with { a = 255 };

        return base.TryInitialize(in data, isExplicitlyDefined);

    }
    public override Color32 GetColor(in CartographyCaptureData data, EObjectChart chartType, Transform? transform, int layer, ref RaycastHit hit)
    {
        if (chartType == EObjectChart.WATER)
            return WaterColor;

        if (chartType == EObjectChart.GROUND)
        {
            Vector3 terrainPoint = hit.point;
            if (layer == LayerMasks.GROUND) // otherwise it hit a 'Chart GROUND' object or resource.
                Landscape.getWorldHeight(terrainPoint, out terrainPoint.y);

            if (IsPointUnderwaterFast(terrainPoint))
                return WaterColor;

            return Data.InterpolateChart(data.MinHeight, data.MaxHeight, terrainPoint.y) ?? FallbackColor;
        }

        if (chartType == EObjectChart.HIGHWAY)
            return HighwayColor;

        if (chartType == EObjectChart.ROAD)
            return RoadColor;

        if (chartType == EObjectChart.STREET)
            return ObjectRoadColor;

        if (chartType == EObjectChart.PATH)
            return PathColor;

        if (chartType == EObjectChart.CLIFF)
            return CliffColor;

        if (layer == LayerMasks.RESOURCE && chartType == EObjectChart.NONE)
            return ResourceColor;

        if (chartType == EObjectChart.LARGE || layer == LayerMasks.LARGE)
            return LargeColor;

        if (chartType == EObjectChart.MEDIUM || layer == LayerMasks.MEDIUM)
            return MediumColor;

        return FallbackColor;
    }
}
