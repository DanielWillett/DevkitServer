extern alias NSJ;
using DevkitServer.API.Cartography.ChartColorProviders;
using DevkitServer.Core.Cartography.ChartColorProviders;
using DevkitServer.Core.Cartography.Jobs;
using DevkitServer.Plugins;
using System.Diagnostics;
using Cysharp.Threading.Tasks;
using DevkitServer.Core.Cartography;
using Unity.Collections;
using Unity.Jobs;
using TransformCacheEntry = (UnityEngine.Transform transform, int layer);

namespace DevkitServer.API.Cartography;

/// <summary>
/// Contains replacement rendering code for charts implementing custom color providers and compositors/post-processors.
/// </summary>
[EarlyTypeInit]
public static class ChartCartography
{
    private static readonly Func<object>? CallGetObjectState = Accessor.GenerateStaticCaller<Level, Func<object>>("GetObjectState", allowUnsafeTypeBinding: true);
    private static readonly Action<object>? RestorePreCaptureState = Accessor.GenerateStaticCaller<Level, Action<object>>("RestorePreCaptureState", allowUnsafeTypeBinding: true);

    private static readonly ChartColorProviderInfo[] DefaultChartColorProviders =
    [
        new ChartColorProviderInfo(typeof(BundledStripChartColorProvider), null!, -2),
        new ChartColorProviderInfo(typeof(JsonChartColorProvider), null!, -1)
    ];

    /// <summary>
    /// Captures a chart render of <paramref name="level"/> (<see cref="Level.info"/> by default) and exports it to <paramref name="outputFile"/> (Level Path/Chart.png by default).
    /// </summary>
    /// <remarks>This technically works on the server build but has limited functionality (no compositing). Passing a custom level info does not affect measurements so only do so if they represent the same level.</remarks>
    /// <returns>The path of the output file created, or <see langword="null"/> if the chart was not rendered.</returns>
    public static async UniTask<string?> CaptureChart(LevelInfo? level = null, string? outputFile = null, CancellationToken token = default)
    {
        await UniTask.SwitchToMainThread(token);
        await UniTask.WaitForEndOfFrame(DevkitServerModule.ComponentHost, token);

        return CaptureChartIntl(level, outputFile);
    }
    private static unsafe string? CaptureChartIntl(LevelInfo? level = null, string? outputFile = null)
    {
        // must be ran at the end of frame

        int imgX = CartographyTool.ImageWidth;
        int imgY = CartographyTool.ImageHeight;

        Bounds captureBounds = CartographyTool.CaptureBounds;

        level ??= Level.info;
        outputFile ??= Path.Combine(level.path, "Chart.png");

        LevelCartographyConfigData? configData = LevelCartographyConfigData.ReadFromLevel(level);

        CartographyCaptureData data = new CartographyCaptureData(level, outputFile, new Vector2Int(imgX, imgY), captureBounds.size, captureBounds.center, true);

        byte[] outputRGB24Image = new byte[imgX * imgY * 3];

        IChartColorProvider? colorProvider = GetChartColorProvider(in data, configData, out ChartColorProviderInfo providerInfo);

        if (colorProvider == null)
        {
            Logger.LogError($"No available color providers. See {DevkitServerModule.GetRelativeRepositoryUrl("Documentation/Cartography Rendering.md", false).Format(false)} for how to set up a chart color provider.");
            return null;
        }

        if (CallGetObjectState == null || RestorePreCaptureState == null)
        {
            Logger.LogWarning("Failed to save/load pre-capture state during chart capture. Check for updates or report this as a bug.");
        }

        object? captureState = CallGetObjectState?.Invoke();
        Stopwatch sw = new Stopwatch();
        try
        {
            sw.Start();
            fixed (byte* ptr = outputRGB24Image)
            {
                switch (colorProvider)
                {
                    case RaycastChartColorProvider simpleColorProvider:
                        CaptureBackgroundUsingJobs(simpleColorProvider, ptr, in data, providerInfo);
                        break;
                    case ISamplingChartColorProvider sampleColorProvider:
                        CaptureBackground(sampleColorProvider, ptr, in data, providerInfo);
                        break;
                    case IFullChartColorProvider fullColorProvider:
                        fullColorProvider.CaptureChart(in data, ptr);
                        break;
                    default:
                        if (providerInfo.Plugin != null)
                        {
                            providerInfo.Plugin.LogInfo($"Chart color provider {providerInfo.Type.Format()} did not perform any action.");
                            providerInfo.Plugin.LogInfo($"Recommended to implement one of the following parents: {typeof(RaycastChartColorProvider).Format()}, {typeof(ISamplingChartColorProvider).Format()}, {typeof(IFullChartColorProvider).Format()}.");
                        }
                        else
                            Logger.LogError($"Color provider {providerInfo.Type.Format()} did not perform any action.");

                        break;
                }
            }
            sw.Stop();

            Logger.LogInfo($"[{nameof(CaptureChart)}] Captured chart background in {sw.GetElapsedMilliseconds().Format("F2")} ms.");
        }
        finally
        {
            if (colorProvider is IDisposable disp)
                disp.Dispose();
        }

        Texture2D outputTexture = new Texture2D(imgX, imgY, TextureFormat.RGB24, 1, false)
        {
            name = "Chart",
            hideFlags = HideFlags.HideAndDontSave,
            requestedMipmapLevel = 0
        };

        outputTexture.SetPixelData(outputRGB24Image, 0, 0);

#if CLIENT
        sw.Restart();
        if (!CartographyCompositing.CompositeForeground(outputTexture, configData, in data))
        {
            sw.Stop();
            Logger.LogInfo($"[{nameof(CaptureChart)}] No compositing was done.");
        }
        else
        {
            sw.Stop();
            Logger.LogInfo($"[{nameof(CaptureChart)}] Composited chart in {sw.GetElapsedMilliseconds().Format("F2")} ms.");
        }
#else
        Logger.LogInfo($"[{nameof(CaptureChart)}] No compositing was done (because this is a server build).");
#endif

        if (captureState != null)
            RestorePreCaptureState?.Invoke(captureState);

        outputTexture.Apply(false);

        byte[] encoded = Path.GetExtension(outputFile).Equals(".png", StringComparison.OrdinalIgnoreCase) ? outputTexture.EncodeToPNG() : outputTexture.EncodeToJPG();

        File.WriteAllBytes(outputFile, encoded);
        Object.DestroyImmediate(outputTexture);

        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, false, true);

        return outputFile;
    }

    private static unsafe void CaptureBackgroundUsingJobs(RaycastChartColorProvider colorProvider, byte* outputRgb24Image, in CartographyCaptureData data, ChartColorProviderInfo providerInfo)
    {
        int imageSizeX = data.ImageSize.x, imageSizeY = data.ImageSize.y;

        int pixelCount = imageSizeX * imageSizeY;

        NativeArray<RaycastCommand> commands = new NativeArray<RaycastCommand>(pixelCount * 4, Allocator.TempJob);
        NativeArray<Vector2> raycastPoints = new NativeArray<Vector2>(pixelCount, Allocator.TempJob);
        NativeArray<RaycastHit> results = new NativeArray<RaycastHit>(pixelCount * 4, Allocator.TempJob);

        int i = -1;
        PhysicsScene physx;
        try
        {
            _transformCache = new Dictionary<int, TransformCacheEntry>(16384);

            Vector2 v2 = default;

            for (int imgX = 0; imgX < imageSizeX; ++imgX)
            {
                for (int imgY = 0; imgY < imageSizeY; ++imgY)
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

            handle.Complete();

            physx = Physics.defaultPhysicsScene;
        }
        catch
        {
            results.Dispose();
            throw;
        }
        finally
        {
            commands.Dispose();
            raycastPoints.Dispose();
        }
        
        i = -1;
        try
        {
            for (int imgX = 0; imgX < imageSizeX; ++imgX)
            {
                for (int imgY = 0; imgY < imageSizeY; ++imgY)
                {
                    RaycastHit hit1 = results[++i];
                    RaycastHit hit2 = results[++i];
                    RaycastHit hit3 = results[++i];
                    RaycastHit hit4 = results[++i];

                    Color32 c1 = Sample(colorProvider, in data, ref hit1, ref physx);
                    Color32 c2 = Sample(colorProvider, in data, ref hit2, ref physx);
                    Color32 c3 = Sample(colorProvider, in data, ref hit3, ref physx);
                    Color32 c4 = Sample(colorProvider, in data, ref hit4, ref physx);

                    Color32 color = new Color32(
                        (byte)((c1.r + c2.r + c3.r + c4.r) / 4),
                        (byte)((c1.g + c2.g + c3.g + c4.g) / 4),
                        (byte)((c1.b + c2.b + c3.b + c4.b) / 4),
                        255);

                    int index = (imgX + imgY * imageSizeX) * 3;
                    outputRgb24Image[index] = color.r;
                    outputRgb24Image[index + 1] = color.g;
                    outputRgb24Image[index + 2] = color.b;
                }
            }
        }
        catch (Exception ex)
        {
            if (providerInfo.Plugin != null)
            {
                providerInfo.Plugin.LogError($"Exception thrown while rendering chart for {data.Level.name.Format(false)} in color provider {colorProvider.GetType().Format()}.");
                providerInfo.Plugin.LogError(ex);
            }
            else
            {
                Logger.LogError($"Exception thrown while rendering chart for {data.Level.name.Format(false)} in color provider {colorProvider.GetType().Format()}.", method: colorProvider.GetType().Name);
                Logger.LogError(ex, method: colorProvider.GetType().Name);
            }
        }
        finally
        {
            _transformCache.Clear();
            _transformCache = null;

            results.Dispose();
        }
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
    private static Color32 Sample(RaycastChartColorProvider prov, in CartographyCaptureData data, ref RaycastHit hit, ref PhysicsScene physx)
    {
        while (true)
        {
            Transform? transform = GetTransformAndLayerFast(ref hit, out int layer);

            if (layer == LayerMasks.ENVIRONMENT)
                layer = RaycastChartColorProvider.GetRoadLayer(transform!);
            
            EObjectChart chartType = prov.GetChartType(ref hit, transform, layer);

            if (chartType != EObjectChart.IGNORE)
                return prov.GetColor(in data, chartType, transform, layer, ref hit);

            Vector3 rayOrigin = hit.point;
            rayOrigin = new Vector3(rayOrigin.x, rayOrigin.y - 0.01f, rayOrigin.z);

            physx.Raycast(rayOrigin, new Vector3(0f, -1f, 0f), out hit, Math.Max(data.CaptureSize.y, Level.HEIGHT - (data.CaptureCenter.y - data.CaptureSize.y / 2f)), RayMasks.CHART, QueryTriggerInteraction.Ignore);
        }
    }

    private static unsafe void CaptureBackground(ISamplingChartColorProvider colorProvider, byte* outputRgb24Image, in CartographyCaptureData data, ChartColorProviderInfo providerInfo)
    {
        int imageSizeX = data.ImageSize.x, imageSizeY = data.ImageSize.y;

        const int rgb24Size = 3;

        Vector2 v2 = default;

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

                    Color32 color = colorProvider.SampleChartPosition(in data, v2);
                    int index = (x + y * imageSizeX) * rgb24Size;
                    outputRgb24Image[index] = color.r;
                    outputRgb24Image[index + 1] = color.g;
                    outputRgb24Image[index + 2] = color.b;
                }
            }
        }
        catch (Exception ex)
        {
            if (providerInfo.Plugin != null)
            {
                providerInfo.Plugin.LogError($"Exception thrown while rendering chart for {data.Level.name.Format(false)} in color provider {colorProvider.GetType().Format()}.");
                providerInfo.Plugin.LogError(ex);
            }
            else
            {
                Logger.LogError($"Exception thrown while rendering chart for {data.Level.name.Format(false)} in color provider {colorProvider.GetType().Format()}.", method: colorProvider.GetType().Name);
                Logger.LogError(ex, method: colorProvider.GetType().Name);
            }
        }
    }


    private static IChartColorProvider? GetChartColorProvider(in CartographyCaptureData data, LevelCartographyConfigData? config, out ChartColorProviderInfo providerInfo)
    {
        if (config != null && !string.IsNullOrWhiteSpace(config.PreferredChartColorProvider))
        {
            string colorProvider = config.PreferredChartColorProvider;

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
                Logger.LogWarning($"Unknown chart provider: {colorProvider.Format()}, using defaults. Make sure your provider implements {typeof(IChartColorProvider).Format()}.");
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
                            Logger.LogError("Requested chart provider did not initialize correctly. Check logs above for more details.");
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
                        info.Plugin.LogDebug($"Using color provider of type {info.Type.Format()} as requested by config.");
                    else
                        Logger.LogDebug($"[{nameof(GetChartColorProvider)}] Using color provider of type {info.Type.Format()} as requested by config.");

                    providerInfo = info;
                    return provider;
                }
                catch (Exception ex)
                {
                    if (info.Plugin != null)
                    {
                        info.Plugin.LogError($"Exception thrown while initializing color provider of type {info.Type.Format()}.");
                        info.Plugin.LogError(ex);
                    }
                    else
                    {
                        Logger.LogError($"Exception thrown while initializing color provider of type {info.Type.Format()}.", method: info.Type.Name);
                        Logger.LogError(ex, method: info.Type.Name);
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
                    info.Plugin.LogDebug($"Using color provider of type {info.Type.Format()}.");
                else
                    Logger.LogDebug($"[{nameof(GetChartColorProvider)}] Using color provider of type {info.Type.Format()}.");

                providerInfo = info;
                return provider;
            }
            catch (Exception ex)
            {
                if (info.Plugin != null)
                {
                    info.Plugin.LogError($"Exception thrown while initializing color provider of type {info.Type.Format()}.");
                    info.Plugin.LogError(ex);
                }
                else
                {
                    Logger.LogError($"Exception thrown while initializing color provider of type {info.Type.Format()}.", method: info.Type.Name);
                    Logger.LogError(ex, method: info.Type.Name);
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
                    info.Plugin.LogError($"Exception thrown while disposing color provider of type {info.Type.Format()}. Providers are disposed even if they're not sucessfully initialized.");
                    info.Plugin.LogError(ex);
                }
                else
                {
                    Logger.LogError($"Exception thrown while disposing color provider of type {info.Type.Format()}. Providers are disposed even if they're not sucessfully initialized.", method: info.Type.Name);
                    Logger.LogError(ex, method: info.Type.Name);
                }
            }
        }
    }
}
