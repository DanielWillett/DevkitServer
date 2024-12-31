using Cysharp.Threading.Tasks;
using DanielWillett.ReflectionTools;
using DevkitServer.API.Cartography.ChartColorProviders;
using DevkitServer.API.Cartography.Compositors;
using DevkitServer.Configuration;
using DevkitServer.Core.Cartography;
using DevkitServer.Core.Cartography.ChartColorProviders;
using DevkitServer.Core.Cartography.Jobs;
using DevkitServer.Plugins;
using SDG.Framework.Water;
using System.Diagnostics;
using System.Text.Json;
using Unity.Collections;
using Unity.Jobs;
using TransformCacheEntry = (UnityEngine.Transform transform, int layer);

namespace DevkitServer.API.Cartography;

/// <summary>
/// Contains replacement rendering code for charts implementing custom color providers and compositors/post-processors.
/// </summary>
public static class ChartCartography
{
    private static readonly ChartColorProviderInfo[] DefaultChartColorProviders =
    [
        new ChartColorProviderInfo(typeof(BundledStripChartColorProvider), null!, -2),
        new ChartColorProviderInfo(typeof(JsonChartColorProvider), null!, -1)
    ];

#if CLIENT
    private static double _lastCartoKeepalive;
#endif

    /// <summary>
    /// The quality of charts when rendered to a JPEG image.
    /// </summary>
    public static int JpegQuality { get; set; } = 85;

    /// <summary>
    /// Captures a chart render of <paramref name="level"/> (<see cref="Level.info"/> by default) and exports it to <paramref name="outputFile"/> (Level Path/Chart.png by default). Supports PNG or JPEG depending on the extension of <paramref name="outputFile"/>.
    /// </summary>
    /// <remarks>This technically works on the server build but has limited functionality (no compositing). Passing a custom level info does not affect measurements so only do so if they represent the same level.</remarks>
    /// <returns>The path of the output file created, or <see langword="null"/> if the chart was not rendered.</returns>
    public static async UniTask<string?> CaptureChart(LevelInfo? level = null, string? outputFile = null, [InstantHandle] CartographyConfigurationSource configurationSource = default, CancellationToken token = default)
    {
        string? colorProviderName = null;
        LevelCartographyConfigData? configData = null;
        if (configurationSource.Configuraiton.ValueKind == JsonValueKind.Undefined && configurationSource.Path == null)
        {
            configData = LevelCartographyConfigData.ReadFromLevel(level, out JsonDocument configDocument);
            colorProviderName = configData?.PreferredChartColorProvider;
            configurationSource = new CartographyConfigurationSource(configData?.FilePath, configDocument.RootElement);
        }
        else if (configurationSource.Path != null)
        {
            if (configurationSource.Configuraiton.ValueKind != JsonValueKind.Undefined
                && configurationSource.Configuraiton.TryGetProperty("chart_color_provider", out JsonElement colorProviderElement)
                && colorProviderElement.ValueKind == JsonValueKind.String)
            {
                colorProviderName = colorProviderElement.GetString();
            }
            configData = configurationSource.Path != null ? CompositorPipeline.FromFile(configurationSource.Path) : null;
            if (configData != null && !string.IsNullOrWhiteSpace(configData.PreferredChartColorProvider))
                colorProviderName = configData.PreferredChartColorProvider;
        }

        await UniTask.SwitchToMainThread(token);

        float oldTime = float.NaN;
        configData?.SyncTime(out oldTime);

        await UniTask.WaitForEndOfFrame(DevkitServerModule.ComponentHost, token);

        level ??= Level.info;

        if (outputFile != null)
        {
            if (!Path.GetExtension(outputFile).Equals(".png", StringComparison.OrdinalIgnoreCase)
                && !Path.GetExtension(outputFile).Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                && !Path.GetExtension(outputFile).Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                outputFile += ".png";
            }
        }
        else
            outputFile = Path.Combine(level.path, "Chart.png");

        Texture2D? texture;
        try
        {
            texture = CaptureChartSync(level, colorProviderName, configData, outputFile, configurationSource);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(nameof(ChartCartography), ex, "Failed to capture chart.");
            return null;
        }

        if (float.IsFinite(oldTime))
            LevelLighting.time = oldTime;

        if (texture == null)
            return null;

        await FileUtil.EncodeAndSaveTexture(texture, outputFile, JpegQuality, token);
#if DEBUG
        ThreadUtil.assertIsGameThread();
#endif
        await UniTask.SwitchToMainThread(token);
        Object.DestroyImmediate(texture);

        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, false, true);

        return outputFile;
    }

    private static unsafe Texture2D? CaptureChartSync(LevelInfo level, string? colorProviderName, LevelCartographyConfigData? configData, string outputFile, [InstantHandle] CartographyConfigurationSource configurationSource)
    {
        // must be ran at the end of frame
#if CLIENT
        _lastCartoKeepalive = 0;
#endif

        Vector2Int imgSize = CartographyTool.GetImageSizeCheckMaxTextureSize(out bool wasSizeOutOfBounds);

        if (wasSizeOutOfBounds)
        {
            Logger.DevkitServer.LogWarning(nameof(ChartCartography), $"Render size was clamped to {imgSize.Format()} because " +
                                                                   $"it was more than the max texture size of this system " +
                                                                   $"(which is {DevkitServerUtility.MaxTextureDimensionSize.Format()}).");
        }

        Bounds captureBounds = CartographyTool.CaptureBounds;

        CartographyCaptureData data = new CartographyCaptureData(level, outputFile, imgSize, captureBounds.size, captureBounds.center, WaterVolumeManager.worldSeaLevel, CartographyType.Chart, configurationSource.Path);

        byte[] outputRGB24Image = new byte[imgSize.x * imgSize.y * 3];

        IChartColorProvider? colorProvider = GetChartColorProvider(in data, colorProviderName, out ChartColorProviderInfo providerInfo);

        if (colorProvider == null)
        {
            Logger.DevkitServer.LogWarning(nameof(ChartCartography), $"No available color providers. See {DevkitServerModule.GetRelativeRepositoryUrl("Documentation/Cartography/Charts.md", false).Format(false)} for how to set up a chart color provider. Defaulting to Washington vanilla.");
            colorProvider = new FallbackChartColorProvider();
            colorProvider.TryInitialize(in data, true);
        }

        object? captureState = CartographyTool.SavePreCaptureState();
        if (captureState == null)
            Logger.DevkitServer.LogWarning(nameof(ChartCartography), "Failed to save/load pre-capture state during chart capture. Check for updates or report this as a bug.");

        Logger.DevkitServer.LogDebug(nameof(ChartCartography), $"Creating chart with image size {data.ImageSize.x.Format()}x{data.ImageSize.y.Format()} = {(data.ImageSize.x * data.ImageSize.y).Format()}px.");

        Stopwatch sw = new Stopwatch();
        try
        {
            sw.Start();
            fixed (byte* ptr = outputRGB24Image)
            {
                switch (colorProvider)
                {
                    case RaycastChartColorProvider simpleColorProvider:
                        CaptureBackgroundUsingJobs(simpleColorProvider, configData, ptr, in data, providerInfo, sw);
                        break;
                    case ISamplingChartColorProvider sampleColorProvider:
                        CaptureBackground(sampleColorProvider, configData, ptr, in data, providerInfo, sw);
                        break;
                    case IFullChartColorProvider fullColorProvider:
                        fullColorProvider.CaptureChart(in data, configData, ptr, sw);
                        break;
                    default:
                        if (providerInfo.Plugin != null)
                        {
                            providerInfo.Plugin.LogInfo(nameof(ChartCartography), $"Chart color provider {providerInfo.Type.Format()} did not perform any action.");
                            providerInfo.Plugin.LogInfo(nameof(ChartCartography), $"Recommended to implement one of the following parents: {typeof(RaycastChartColorProvider).Format()}, {typeof(ISamplingChartColorProvider).Format()}, {typeof(IFullChartColorProvider).Format()}.");
                        }
                        else
                            Logger.DevkitServer.LogError(nameof(ChartCartography), $"Color provider {providerInfo.Type.Format()} did not perform any action.");

                        break;
                }
            }
            sw.Stop();

            Logger.DevkitServer.LogInfo(nameof(ChartCartography), $"Captured chart background in {sw.GetElapsedMilliseconds().Format("F2")} ms.");
        }
        catch (OperationCanceledException)
        {
            Logger.DevkitServer.LogWarning(nameof(ChartCartography), "Chart capture was cancelled early because you either disconnected from or connected to a server while the chart was rendering.");
            return null;
        }
        finally
        {
            if (colorProvider is IDisposable disp)
                disp.Dispose();
        }

        Texture2D outputTexture = new Texture2D(imgSize.x, imgSize.y, TextureFormat.RGB24, 1, false)
        {
            name = "Chart",
            hideFlags = HideFlags.HideAndDontSave,
            requestedMipmapLevel = 0
        };

        outputTexture.SetPixelData(outputRGB24Image, 0, 0);

#if CLIENT
        sw.Restart();
        if (!CartographyCompositing.CompositeForeground(outputTexture, configData?.GetActiveCompositors(), in data))
        {
            sw.Stop();
            Logger.DevkitServer.LogInfo(nameof(ChartCartography), "No compositing was done.");
        }
        else
        {
            sw.Stop();
            Logger.DevkitServer.LogInfo(nameof(ChartCartography), $"Composited chart in {sw.GetElapsedMilliseconds().Format("F2")} ms.");
        }
#else
        Logger.DevkitServer.LogInfo(nameof(ChartCartography), "No compositing was done (because this is a server build).");
#endif

        if (captureState != null)
            CartographyTool.RestorePreCaptureState(captureState);

        outputTexture.Apply(false);

        return outputTexture;
    }

    private readonly ref struct CartographyChunkData
    {
        public readonly int StartX;
        public readonly int StartY;
        public readonly int EndX;
        public readonly int EndY;
        public CartographyChunkData(int startX, int startY, int endX, int endY)
        {
            StartX = startX;
            StartY = startY;
            EndX = endX;
            EndY = endY;
        }
    }
    private static unsafe void CaptureBackgroundUsingJobs(RaycastChartColorProvider colorProvider, LevelCartographyConfigData? config, byte* outputRgb24Image, in CartographyCaptureData data, ChartColorProviderInfo providerInfo, Stopwatch jobStopwatch)
    {
        int imageSizeX = data.ImageSize.x, imageSizeY = data.ImageSize.y;

        int maxChunkSize = DevkitServerConfig.Config?.MaxChartRenderChunkSize ?? 4096;
        if (maxChunkSize <= 0)
            maxChunkSize = 4096;
        
        if (imageSizeX > maxChunkSize || imageSizeY > maxChunkSize)
        {
            // split into chunks to prevent running out of memory
            int chunkSizeX = imageSizeX / 2 < maxChunkSize ? (imageSizeX - 1) / 2 + 1 : maxChunkSize;
            int chunkSizeY = imageSizeY / 2 < maxChunkSize ? (imageSizeY - 1) / 2 + 1 : maxChunkSize;
            Logger.DevkitServer.LogDebug(nameof(ChartCartography), $"Chunks: {chunkSizeX.Format()}x{chunkSizeY.Format()}.");
            int x = 0;
            do
            {
                int y = 0;
                do
                {
                    CartographyChunkData chunk = new CartographyChunkData(x, y, Math.Min(x + chunkSizeX, imageSizeX) - 1, Math.Min(y + chunkSizeY, imageSizeY) - 1);
                    Logger.DevkitServer.LogDebug(nameof(ChartCartography), $" Chunk: ({chunk.StartX.Format()} to {chunk.EndX.Format()}, {chunk.StartY.Format()} to {chunk.EndY.Format()}).");

                    if (!CaptureBackgroundUsingJobsChunk(colorProvider, config, outputRgb24Image, in data, in chunk, providerInfo, jobStopwatch))
                        return;

                    y += chunkSizeY;
                }
                while (y + chunkSizeY <= imageSizeY);
                x += chunkSizeX;
            }
            while (x + chunkSizeX <= imageSizeX);
            return;
        }

        CartographyChunkData fullChunk = new CartographyChunkData(0, 0, imageSizeX - 1, imageSizeY - 1);
        CaptureBackgroundUsingJobsChunk(colorProvider, config, outputRgb24Image, in data, in fullChunk, providerInfo, jobStopwatch);
    }
    private static unsafe bool CaptureBackgroundUsingJobsChunk(RaycastChartColorProvider colorProvider, LevelCartographyConfigData? config, byte* outputRgb24Image, in CartographyCaptureData data, in CartographyChunkData chunk, ChartColorProviderInfo providerInfo, [UsedImplicitly] Stopwatch jobStopwatch)
    {
        int imageSizeX = chunk.EndX - chunk.StartX + 1;
        int imageSizeY = chunk.EndY - chunk.StartY + 1;
        int pixelCount = imageSizeX * imageSizeY;
#if CLIENT
        float dt = Math.Min(0.1f, CachedTime.DeltaTime);
        bool wasConnected = Provider.isConnected;
#endif

        NativeArray<RaycastCommand> commands = new NativeArray<RaycastCommand>(pixelCount * 4, Allocator.TempJob);
        NativeArray<Vector2> raycastPoints = new NativeArray<Vector2>(pixelCount, Allocator.TempJob);
        NativeArray<RaycastHit> results = new NativeArray<RaycastHit>(pixelCount * 4, Allocator.TempJob);

        int i = -1;
        PhysicsScene physx;
        try
        {
#if CLIENT
            CheckKeepAlive(jobStopwatch, dt, wasConnected);
#endif
            _transformCache = new Dictionary<int, TransformCacheEntry>(16384);

            Vector2 v2 = default;

            for (int imgX = chunk.StartX; imgX <= chunk.EndX; ++imgX)
            {
                for (int imgY = chunk.StartY; imgY <= chunk.EndY; ++imgY)
                {
                    v2.x = imgX;
                    v2.y = imgY;
                    Vector3 worldCoordinates = CartographyTool.MapCoordsToWorldCoords(v2);

                    float x = worldCoordinates.x;
                    float y = worldCoordinates.z;

                    raycastPoints[++i] = new Vector2(x, y);
                }
            }

            SetupChartRaycastsJob job = new SetupChartRaycastsJob
            {
                Commands = commands,
                Casts = raycastPoints,
                Height = data.CaptureCenter.y + data.CaptureSize.y / 2f,
                Direction = Vector3.down,
                Length = Mathf.Max(data.CaptureSize.y, Level.HEIGHT - (data.CaptureCenter.y - data.CaptureSize.y / 2f))
            };

            JobHandle handle = job.Schedule(pixelCount, 256);

            handle = RaycastCommand.ScheduleBatch(commands, results, 8192, handle);

#if CLIENT
            CheckKeepAlive(jobStopwatch, dt, wasConnected);
#endif

            handle.Complete();

#if CLIENT
            CheckKeepAlive(jobStopwatch, dt, wasConnected);
#endif

            physx = Physics.defaultPhysicsScene;
        }
        catch (Exception ex)
        {
            results.Dispose();
            Logger.DevkitServer.LogConditional(nameof(ChartCartography), ex);
            throw;
        }
        finally
        {
            commands.Dispose();
            raycastPoints.Dispose();
        }

#if CLIENT
        CheckKeepAlive(jobStopwatch, dt, wasConnected);
#endif

        i = -1;
        try
        {
            int fullImgSizeX = data.ImageSize.x;
            for (int imgX = chunk.StartX; imgX <= chunk.EndX; ++imgX)
            {
                for (int imgY = chunk.StartY; imgY <= chunk.EndY; ++imgY)
                {
                    RaycastHit hit1 = results[++i];
                    RaycastHit hit2 = results[++i];
                    RaycastHit hit3 = results[++i];
                    RaycastHit hit4 = results[++i];

                    Color32 c1 = Sample(colorProvider, config, in data, ref hit1, ref physx);
                    Color32 c2 = Sample(colorProvider, config, in data, ref hit2, ref physx);
                    Color32 c3 = Sample(colorProvider, config, in data, ref hit3, ref physx);
                    Color32 c4 = Sample(colorProvider, config, in data, ref hit4, ref physx);

                    Color32 color = new Color32(
                        (byte)((c1.r + c2.r + c3.r + c4.r) / 4),
                        (byte)((c1.g + c2.g + c3.g + c4.g) / 4),
                        (byte)((c1.b + c2.b + c3.b + c4.b) / 4),
                        255);

                    int index = (imgX + imgY * fullImgSizeX) * 3;
                    outputRgb24Image[index] = color.r;
                    outputRgb24Image[index + 1] = color.g;
                    outputRgb24Image[index + 2] = color.b;
                }

#if CLIENT
                CheckKeepAlive(jobStopwatch, dt, wasConnected);
#endif
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            if (providerInfo.Plugin != null)
            {
                providerInfo.Plugin.LogError(nameof(ChartCartography), ex, $"Exception thrown while rendering chart for {data.Level.name.Format(false)} in color provider {colorProvider.GetType().Format()}.");
            }
            else
            {
                Logger.DevkitServer.LogError(nameof(ChartCartography), ex, $"Exception thrown while rendering chart for {data.Level.name.Format(false)} in color provider {colorProvider.GetType().Format()}.");
            }
        }
        finally
        {
            _transformCache?.Clear();
            _transformCache = null;

            results.Dispose();
        }

#if CLIENT
        static void CheckKeepAlive(Stopwatch jobStopwatch, float dt, bool wasConnected)
        {
            if (Provider.isConnected)
            {
                double elapsedSeconds = jobStopwatch.GetElapsedMilliseconds() * 1000d;

                if (elapsedSeconds - _lastCartoKeepalive <= dt)
                    return;

                _lastCartoKeepalive = elapsedSeconds;
                CartographyHelper.KeepClientAlive();
            }

            if (wasConnected != Provider.isConnected)
                throw new OperationCanceledException("Disconnected from or connected to a server mid-render.");
        }
#endif
        return true;
    }

#nullable disable
    private static Dictionary<int, TransformCacheEntry> _transformCache;
#nullable restore
    private static Transform? GetTransformAndLayerFast(ref RaycastHit hit, out int layer)
    {
        int colliderId = hit.colliderInstanceID;
        if (colliderId == default)
        {
            layer = LayerMasks.DEFAULT;
            return null;
        }

        if (_transformCache.TryGetValue(colliderId, out TransformCacheEntry transformAndLayer))
        {
            layer = transformAndLayer.layer;
            return transformAndLayer.transform;
        }

        Collider? collider = hit.collider;
        if (collider is null)
        {
            layer = LayerMasks.DEFAULT;
            return null;
        }

        Transform transform = collider.transform;
        layer = transform.gameObject.layer;
        _transformCache.Add(colliderId, (transform, layer));
        return transform;
    }

    private static readonly RaycastHit[] IgnoreHitBuffer = new RaycastHit[32];
    private static readonly IComparer<RaycastHit> HeightComparer = new RaycastHeightComparer();

    private static Color32 Sample(RaycastChartColorProvider prov, LevelCartographyConfigData? config, in CartographyCaptureData data, ref RaycastHit hit, ref PhysicsScene physx)
    {
        Transform? transform = GetTransformAndLayerFast(ref hit, out int layer);

        EObjectChart chartType = EObjectChart.NONE;
        if (layer == LayerMasks.ENVIRONMENT)
            layer = RaycastChartColorProvider.GetRoadLayer(transform!, config, out chartType);

        if (chartType == EObjectChart.NONE)
            chartType = prov.GetChartType(ref hit, config, transform, layer);

        if (chartType != EObjectChart.IGNORE)
            return prov.GetColor(in data, chartType, transform, layer, ref hit);

        int ct = physx.Raycast(
            hit.point,
            new Vector3(0f, -1f, 0f),
            IgnoreHitBuffer,
            maxDistance: Mathf.Max(data.CaptureSize.y, Level.HEIGHT - (data.CaptureCenter.y - data.CaptureSize.y / 2f)),
            layerMask: RayMasks.CHART,
            queryTriggerInteraction: QueryTriggerInteraction.Ignore
        );

        Array.Sort(IgnoreHitBuffer, 0, ct, HeightComparer);

        for (int i = 0; i < ct; ++i)
        {
            ref RaycastHit secondaryHit = ref IgnoreHitBuffer[i];
            Transform? transform2 = GetTransformAndLayerFast(ref secondaryHit, out layer);

            if (ReferenceEquals(transform2, transform))
                continue;

            chartType = EObjectChart.NONE;
            if (layer == LayerMasks.ENVIRONMENT)
                layer = RaycastChartColorProvider.GetRoadLayer(transform2!, config, out chartType);

            if (chartType == EObjectChart.NONE)
                chartType = prov.GetChartType(ref secondaryHit, config, transform2, layer);

            if (chartType != EObjectChart.IGNORE)
                return prov.GetColor(in data, chartType, transform2, layer, ref secondaryHit);
        }

        transform = null;
        return prov.GetColor(in data, EObjectChart.GROUND, transform, layer, ref hit);
    }

    private static unsafe void CaptureBackground(ISamplingChartColorProvider colorProvider, LevelCartographyConfigData? config, byte* outputRgb24Image, in CartographyCaptureData data, ChartColorProviderInfo providerInfo, [UsedImplicitly] Stopwatch jobStopwatch)
    {
        int imageSizeX = data.ImageSize.x, imageSizeY = data.ImageSize.y;

        const int rgb24Size = 3;

        Vector2 v2 = default;
#if CLIENT
        float dt = Math.Min(0.1f, CachedTime.DeltaTime);
        bool wasConnected = Provider.isConnected;
#endif

        try
        {
            for (int x = 0; x < imageSizeX; ++x)
            {
                for (int y = 0; y < imageSizeY; ++y)
                {
                    v2.x = x;
                    v2.y = y;
                    Vector3 worldCoordinates = CartographyTool.MapCoordsToWorldCoords(v2);

                    v2.x = worldCoordinates.x;
                    v2.y = worldCoordinates.z;

                    Color32 color = colorProvider.SampleChartPosition(in data, config, v2);
                    int index = (x + y * imageSizeX) * rgb24Size;
                    outputRgb24Image[index] = color.r;
                    outputRgb24Image[index + 1] = color.g;
                    outputRgb24Image[index + 2] = color.b;
                }

#if CLIENT
                double elapsedSeconds = jobStopwatch.GetElapsedMilliseconds() * 1000d;
                if (elapsedSeconds - _lastCartoKeepalive > dt)
                {
                    _lastCartoKeepalive = elapsedSeconds;
                    CartographyHelper.KeepClientAlive();
                }

                if (wasConnected != Provider.isConnected)
                    throw new OperationCanceledException("Disconnected from or connected to a server mid-render.");
#endif
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            if (providerInfo.Plugin != null)
            {
                providerInfo.Plugin.LogError(nameof(ChartCartography), ex, $"Exception thrown while rendering chart for {data.Level.name.Format(false)} in color provider {colorProvider.GetType().Format()}.");
            }
            else
            {
                Logger.DevkitServer.LogError(nameof(ChartCartography), ex, $"Exception thrown while rendering chart for {data.Level.name.Format(false)} in color provider {colorProvider.GetType().Format()}.");
            }
        }
    }


    private static IChartColorProvider? GetChartColorProvider(in CartographyCaptureData data, string? chartColorProvider, out ChartColorProviderInfo providerInfo)
    {
        if (!string.IsNullOrWhiteSpace(chartColorProvider))
        {
            string colorProvider = chartColorProvider;

            // vanilla
            ChartColorProviderInfo info = DefaultChartColorProviders
                        .FirstOrDefault(x => x.Type.Name.Equals(colorProvider, StringComparison.InvariantCultureIgnoreCase));

            // all plugins
            if (info.Type == null)
            {
                info = PluginLoader.Assemblies
                    .SelectMany(x => x.ChartColorProviders)
                    .OrderByDescending(x => x.Priority)
                    .FirstOrDefault(x => x.Type.Name.Equals(colorProvider, StringComparison.InvariantCultureIgnoreCase));
            }

            if (info.Type == null || !typeof(IChartColorProvider).IsAssignableFrom(info.Type) || info.Type.IsAbstract || info.Type.IsInterface)
            {
                Logger.DevkitServer.LogWarning(nameof(ChartCartography), $"Unknown chart provider: {colorProvider.Format()}, using defaults. Make sure your provider implements {typeof(IChartColorProvider).Format()}.");
            }
            else
            {
                try
                {
                    IChartColorProvider provider = (IChartColorProvider)Activator.CreateInstance(info.Type, true);

                    try
                    {
                        if (!provider.TryInitialize(in data, true))
                        {
                            Dispose(provider, info);
                            Logger.DevkitServer.LogError(nameof(ChartCartography), "Requested chart provider did not initialize correctly. Check logs above for more details.");
                            providerInfo = info;
                            return null;
                        }
                    }
                    catch
                    {
                        Dispose(provider, info);
                        throw;
                    }

                    if (info.Plugin != null)
                        info.Plugin.LogDebug(nameof(ChartCartography), $"Using color provider of type {info.Type.Format()} as requested by config.");
                    else
                        Logger.DevkitServer.LogDebug(nameof(ChartCartography), $"Using color provider of type {info.Type.Format()} as requested by config.");

                    providerInfo = info;
                    return provider;
                }
                catch (Exception ex)
                {
                    if (info.Plugin != null)
                    {
                        info.Plugin.LogError(nameof(ChartCartography), ex, $"Exception thrown while initializing color provider of type {info.Type.Format()}.");
                    }
                    else
                    {
                        Logger.DevkitServer.LogError(nameof(ChartCartography), ex, $"Exception thrown while initializing color provider of type {info.Type.Format()}.");
                    }

                    providerInfo = default;
                    return null;
                }
            }
        }

        List<ChartColorProviderInfo> types = new List<ChartColorProviderInfo>(4);

        // plugin providers
        types.AddRange(
            PluginLoader.Assemblies
            .SelectMany(x => x.ChartColorProviders)
        );

        // 'vanilla' providers
        types.AddRange(DefaultChartColorProviders);

        types.Sort((a, b) => b.Priority.CompareTo(a.Priority));

        foreach (ChartColorProviderInfo info in types)
        {
            try
            {
                IChartColorProvider provider = (IChartColorProvider)Activator.CreateInstance(info.Type, true);

                try
                {
                    if (!provider.TryInitialize(in data, false))
                    {
                        Dispose(provider, info);
                        continue;
                    }
                }
                catch
                {
                    Dispose(provider, info);
                    throw;
                }

                if (info.Plugin != null)
                    info.Plugin.LogDebug(nameof(ChartCartography), $"Using color provider of type {info.Type.Format()}.");
                else
                    Logger.DevkitServer.LogDebug(nameof(ChartCartography), $"Using color provider of type {info.Type.Format()}.");

                providerInfo = info;
                return provider;
            }
            catch (Exception ex)
            {
                if (info.Plugin != null)
                {
                    info.Plugin.LogError(nameof(ChartCartography), ex, $"Exception thrown while initializing color provider of type {info.Type.Format()}.");
                }
                else
                {
                    Logger.DevkitServer.LogError(nameof(ChartCartography), ex, $"Exception thrown while initializing color provider of type {info.Type.Format()}.");
                }
            }
        }

        providerInfo = default;
        return null;

        void Dispose(IChartColorProvider provider, ChartColorProviderInfo info)
        {
            if (provider is not IDisposable disp)
                return;

            try
            {
                disp.Dispose();
            }
            catch (Exception ex)
            {
                if (info.Plugin != null)
                {
                    info.Plugin.LogError(nameof(ChartCartography), ex, $"Exception thrown while disposing color provider of type {info.Type.Format()}. Providers are disposed even if they're not sucessfully initialized.");
                }
                else
                {
                    Logger.DevkitServer.LogError(nameof(ChartCartography), ex, $"Exception thrown while disposing color provider of type {info.Type.Format()}. Providers are disposed even if they're not sucessfully initialized.");
                }
            }
        }
    }

    private class RaycastHeightComparer : IComparer<RaycastHit>
    {
        public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
    }
}