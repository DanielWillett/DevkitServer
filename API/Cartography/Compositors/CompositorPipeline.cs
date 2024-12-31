#if CLIENT
using Cysharp.Threading.Tasks;
using DanielWillett.ReflectionTools;
using DevkitServer.Configuration;
using DevkitServer.Core.Cartography;
using SDG.Framework.Water;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace DevkitServer.API.Cartography.Compositors;

public class CompositorPipeline : LevelCartographyConfigData, IDisposable
{
    private JsonDocument? _doc;

    /// <summary>
    /// The display name of the pipeline.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// Ordered list of compositors to apply after rendering the map.
    /// </summary>
    public CartographyCompositorConfigurationInfo[] Compositors { get; private set; } = Array.Empty<CartographyCompositorConfigurationInfo>();

    /// <summary>
    /// The type of map to use as the base image.
    /// </summary>
    public CartographyType Type { get; private set; }

    /// <summary>
    /// The color to fill the texture with when <see cref="Type"/> is <see cref="CartographyType.None"/>.
    /// </summary>
    public Color32 BackgroundColor { get; private set; }

    /// <summary>
    /// Checks if the file should open immediately after compositing.
    /// </summary>
    public bool AutoOpen { get; private set; }

    /// <summary>
    /// Relative or absolute file path of the output file.
    /// </summary>
    public string? OutputFile { get; private set; }

    /// <summary>
    /// Override the size of the image, blank space will be filled with <see cref="BackgroundColor"/>.
    /// </summary>
    public Vector2Int? ImageSize { get; private set; }

    public CompositorPipeline(string name, Type chartColorProvider, params CartographyCompositorConfigurationInfo[] compositors)
        : this(name)
    {
        Type = CartographyType.Chart;
        PreferredChartColorProvider = chartColorProvider.Name;
        Compositors = compositors;
    }

    public CompositorPipeline(string name, params CartographyCompositorConfigurationInfo[] compositors)
        : this(name)
    {
        Type = CartographyType.Satellite;
        Compositors = compositors;
    }

    private CompositorPipeline(string name)
    {
        Name = name;
    }

    public async UniTask<string?> Composite(string? outputFile = null, LevelInfo? level = null, int jpegQuality = 95, CancellationToken token = default)
    {
        level ??= Level.info;

        outputFile ??= OutputFile ?? Path.Combine(level.path, "Editor", FileUtil.CleanFileName(Name));
        CartographyConfigurationSource configurationSource = new CartographyConfigurationSource(FilePath, _doc?.RootElement ?? default);
        if (Type == CartographyType.Satellite)
        {
            return await SatelliteCartography.CaptureSatellite(level, outputFile, configurationSource, token);
        }

        if (Type == CartographyType.Chart)
        {
            return await ChartCartography.CaptureChart(level, outputFile, configurationSource, token);
        }


        await UniTask.SwitchToMainThread(token);

        SyncTime(out float oldTime);

        await UniTask.WaitForEndOfFrame(DevkitServerModule.ComponentHost, token);

        Texture2D texture = CaptureNoneSync(level, outputFile);

        if (texture == null)
            return null;

        await FileUtil.EncodeAndSaveTexture(texture, outputFile, jpegQuality, token);
        await UniTask.SwitchToMainThread(token);
        Object.DestroyImmediate(texture);

        if (float.IsFinite(oldTime))
            LevelLighting.time = oldTime;

        return outputFile;
    }

    private Texture2D CaptureNoneSync(LevelInfo level, string outputFile)
    {
        // should be ran at end of frame

        Vector2Int imgSize = CartographyTool.GetImageSizeCheckMaxTextureSize(out bool wasSizeOutOfBounds, this);

        RectInt captureRect = new RectInt(0, 0, imgSize.x, imgSize.y);

        if (wasSizeOutOfBounds)
        {
            Logger.DevkitServer.LogWarning(nameof(ChartCartography), $"Render size was clamped to {imgSize.Format()} because " +
                                                                     $"it was more than the max texture size of this system " +
                                                                     $"(which is {DevkitServerUtility.MaxTextureDimensionSize.Format()}).");
        }

        Texture2D outputTexture = new Texture2D(imgSize.x, imgSize.y, TextureFormat.RGB24, 1, false)
        {
            name = "Chart",
            hideFlags = HideFlags.HideAndDontSave,
            requestedMipmapLevel = 0
        };

        Color32[] pixels = new Color32[imgSize.x * imgSize.y];
        Array.Fill(pixels, BackgroundColor);
        outputTexture.SetPixels32(pixels);

        Stopwatch sw = Stopwatch.StartNew();

        Bounds captureBounds = CartographyTool.CaptureBounds;
        CartographyCaptureData data = new CartographyCaptureData(level, outputFile, imgSize, captureBounds.size, captureBounds.center, WaterVolumeManager.worldSeaLevel, Type, FilePath, captureRect);
        if (!CartographyCompositing.CompositeForeground(outputTexture, Compositors, in data))
        {
            sw.Stop();
            Logger.DevkitServer.LogInfo(nameof(ChartCartography), "No compositing was done.");
        }
        else
        {
            sw.Stop();
            Logger.DevkitServer.LogInfo(nameof(ChartCartography), $"Composited chart in {sw.GetElapsedMilliseconds().Format("F2")} ms.");
        }

        outputTexture.Apply(false);

        return outputTexture;
    }

    /// <summary>
    /// Read a pipeline from a file.
    /// </summary>
    public static CompositorPipeline? FromFile(string path, out JsonDocument document)
    {
        if (!File.Exists(path))
        {
            document = null!;
            return null;
        }

        try
        {
            using FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 2048, FileOptions.SequentialScan);
            document = JsonDocument.Parse(fs, DevkitServerConfig.DocumentSettings);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogWarning(nameof(CompositorPipeline), ex, $"Failed to load pipeline file {path.Format()}.");
            document = JsonDocument.Parse("{}");
            return null;
        }

        CompositorPipeline pipeline = new CompositorPipeline(Path.GetFileNameWithoutExtension(path))
        {
            _doc = document,
            FilePath = path
        };

        try
        {
            ReadJson(document.RootElement, pipeline);
        }
        catch (Exception ex)
        {
            document.Dispose();
            document = JsonDocument.Parse("{}");
            Logger.DevkitServer.LogWarning(nameof(CompositorPipeline), ex, $"Failed to read pipeline file {path.Format()}.");
            return null;
        }

        if (Level.info != null && (pipeline.ChartOverrides == null || string.IsNullOrWhiteSpace(pipeline.PreferredChartColorProvider)))
        {
            LevelCartographyConfigData? config = ReadFromLevel(Level.info, out _, false);
            pipeline.ChartOverrides ??= config?.ChartOverrides;

            if (string.IsNullOrWhiteSpace(pipeline.PreferredChartColorProvider))
                pipeline.PreferredChartColorProvider = config?.PreferredChartColorProvider;
        }

        return pipeline;
    }

    private static void ReadJson(JsonElement root, CompositorPipeline value)
    {
        string? type = root.GetProperty("type").GetString();
        if (!Enum.TryParse(type, ignoreCase: true, out CartographyType typeEnum) || typeEnum is < CartographyType.None or > CartographyType.Chart)
        {
            if (!string.Equals(type, "GPS", StringComparison.InvariantCultureIgnoreCase))
                throw new InvalidOperationException("\"type\" property must be present and one of the following values: 'Chart', 'Satellite', or 'None'.");

            typeEnum = CartographyType.Satellite;
        }

        value.Type = typeEnum;

        if (root.TryGetProperty("name", out JsonElement nameElement))
        {
            string? name = nameElement.GetString();
            if (!string.IsNullOrWhiteSpace(name))
                value.Name = name;
        }

        if (root.TryGetProperty("output_file", out JsonElement pathElement) && pathElement.ValueKind == JsonValueKind.String)
        {
            string? path = pathElement.GetString();
            if (!string.IsNullOrWhiteSpace(path))
            {
                if (value.FilePath != null && !Path.IsPathRooted(path))
                    path = Path.GetFullPath(path, Path.GetDirectoryName(value.FilePath));
                value.OutputFile = path;
            }
        }

        if (root.TryGetProperty("background_color", out JsonElement backgroundColorElement)
            && backgroundColorElement.ValueKind == JsonValueKind.String
            && DevkitServerUtility.TryParseColor32(backgroundColorElement.GetString(), CultureInfo.InvariantCulture, out Color32 backgroundColor))
        {
            backgroundColor.a = 255;
        }
        else
        {
            backgroundColor = new Color32(0, 0, 0, 255);
        }

        value.BackgroundColor = backgroundColor;

        if (root.TryGetProperty("compositors", out JsonElement compositorsElement) && compositorsElement.ValueKind != JsonValueKind.Null)
        {
            int len = compositorsElement.GetArrayLength();
            CartographyCompositorConfigurationInfo[] typeArr = len == 0
                ? Array.Empty<CartographyCompositorConfigurationInfo>()
                : new CartographyCompositorConfigurationInfo[len];

            JsonElement.ArrayEnumerator enumerator = compositorsElement.EnumerateArray();
            for (int i = 0; i < len && enumerator.MoveNext(); ++i)
            {
                JsonElement current = enumerator.Current;
                ref CartographyCompositorConfigurationInfo v = ref typeArr[i];
                if (current.ValueKind == JsonValueKind.String)
                {
                    v.TypeName = current.GetString();
                }
                else if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty("type", out JsonElement typeElement) && typeElement.ValueKind == JsonValueKind.String)
                {
                    v.ExtraConfig = current;
                    v.TypeName = typeElement.GetString();
                }
            }
            enumerator.Dispose();

            value.Compositors = typeArr;
        }
        else
        {
            value.Compositors = Array.Empty<CartographyCompositorConfigurationInfo>();
        }

        value.AutoOpen = root.TryGetProperty("auto_open", out JsonElement autoOpenElement) && autoOpenElement.ValueKind == JsonValueKind.True;

        if (root.TryGetProperty("time", out JsonElement element) && element.ValueKind is JsonValueKind.Number or JsonValueKind.String)
        {
            if (element.ValueKind == JsonValueKind.Number)
                value.Time = ((int)MathF.Round(element.GetSingle())).ToString(CultureInfo.InvariantCulture);
            else
                value.Time = element.GetString();
        }

        if (root.TryGetProperty("image_size_x", out JsonElement sizeX)
            && sizeX.ValueKind == JsonValueKind.Number
            && root.TryGetProperty("image_size_y", out JsonElement sizeY)
            && sizeY.ValueKind == JsonValueKind.Number
            && sizeX.TryGetInt32(out int imageSizeX)
            && sizeY.TryGetInt32(out int imageSizeY))
        {
            value.ImageSize = new Vector2Int(imageSizeX, imageSizeY);
        }
        else
        {
            value.ImageSize = null;
        }

        if (typeEnum != CartographyType.Chart)
            return;

        if ((root.TryGetProperty("override_chart_color_provider", out JsonElement chartColor) || root.TryGetProperty("chart_color_provider", out chartColor))
            && chartColor.ValueKind == JsonValueKind.String)
        {
            value.PreferredChartColorProvider = chartColor.GetString();
        }
        else
        {
            value.PreferredChartColorProvider = null;
        }

        if (root.TryGetProperty("chart_type_overrides", out JsonElement typeOverrideElements))
        {
            if (typeOverrideElements.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("\"chart_type_overrides\" property should be an object (string dictionary).");
            value.ChartOverrides = typeOverrideElements.Deserialize<Dictionary<string, string>>();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Interlocked.Exchange(ref _doc, null)?.Dispose();
    }
}
#endif