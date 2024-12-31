using Cysharp.Threading.Tasks;
using DanielWillett.ReflectionTools;
using DevkitServer.Core.Cartography;
using SDG.Framework.Water;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using DevkitServer.API.Cartography.Compositors;
using UnityEngine.Rendering;
using GraphicsSettings = SDG.Unturned.GraphicsSettings;

namespace DevkitServer.API.Cartography;

/// <summary>
/// Contains replacement rendering code for satellite rendering implementing custom compositors/post-processors.
/// </summary>
public static class SatelliteCartography
{
    /// <summary>
    /// The quality of satellites when rendered to a JPEG image.
    /// </summary>
    public static int JpegQuality { get; set; } = 95;

#if CLIENT
    /// <summary>
    /// Captures a satellite render of <paramref name="level"/> (<see cref="Level.info"/> by default) and exports it to <paramref name="outputFile"/> (Level Path/Map.png by default). Supports PNG or JPEG depending on the extension of <paramref name="outputFile"/>.
    /// </summary>
    /// <remarks>This does not work on the server build. Passing a custom level info does not affect measurements so only do so if they represent the same level.</remarks>
    /// <returns>The path of the output file created, or <see langword="null"/> if the chart was not rendered.</returns>
    public static async UniTask<string?> CaptureSatellite(LevelInfo? level = null, string? outputFile = null, [InstantHandle] CartographyConfigurationSource configurationSource = default, CancellationToken token = default)
    {
        LevelCartographyConfigData? configData = null;
        if (configurationSource.Configuraiton.ValueKind == JsonValueKind.Undefined && configurationSource.Path == null)
        {
            configData = LevelCartographyConfigData.ReadFromLevel(level, out JsonDocument configDocument);
            configurationSource = new CartographyConfigurationSource(configData?.FilePath, configDocument.RootElement);
        }
        else if (configurationSource.Path != null)
        {
            JsonDocument? doc = null;
            configData = configurationSource.Path != null ? CompositorPipeline.FromFile(configurationSource.Path, out doc) : null;
            if (configData == null)
                doc?.Dispose();
        }

        await UniTask.SwitchToMainThread(token);

        float oldTime = float.NaN;
        configData?.SyncTime(out oldTime);

        await UniTask.WaitForEndOfFrame(DevkitServerModule.ComponentHost, token);

        level ??= Level.info;

        if (outputFile != null)
        {
            string ext = Path.GetExtension(outputFile);
            if (!ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
                && !ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                && !ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                outputFile += ".png";
            }
        }
        else
            outputFile = Path.Combine(level.path, "Map.png");

        Texture2D? texture = CaptureSatelliteSync(level, configData, outputFile, configurationSource);

        if (texture == null)
            return null;

        Stopwatch sw = Stopwatch.StartNew();
        texture.Apply();
        sw.Stop();

        Logger.DevkitServer.LogDebug(nameof(SatelliteCartography), $"Apply texture: {sw.GetElapsedMilliseconds().Format("0.##")} ms.");

        if (float.IsFinite(oldTime))
            LevelLighting.time = oldTime;

        await FileUtil.EncodeAndSaveTexture(texture, outputFile, JpegQuality, token);
#if DEBUG
        ThreadUtil.assertIsGameThread();
#endif
        await UniTask.SwitchToMainThread();
        Object.DestroyImmediate(texture);

        return outputFile;
    }
    private static Texture2D? CaptureSatelliteSync(LevelInfo level, LevelCartographyConfigData? configData, string outputFile, [InstantHandle] CartographyConfigurationSource configurationSource)
    {
        // should be ran at end of frame

        Vector2Int imgSize = CartographyTool.GetImageSizeCheckMaxTextureSize(out Vector2Int superSampleSize, out bool wasSizeOutOfBounds, out bool wasSuperSampleOutOfBounds, configData);

        Vector2Int captureSize = CartographyTool.GetImageSizeCheckMaxTextureSize(out _, out _, out _);

        int cx = 0, cy = 0, cw = captureSize.x, ch = captureSize.y;
        if (captureSize.x > imgSize.x)
            cw = imgSize.x;
        else
            cx = (imgSize.x - captureSize.x) / 2;

        if (captureSize.y > imgSize.y)
            ch = imgSize.y;
        else
            cy = (imgSize.y - captureSize.y) / 2;

        RectInt captureRect = new RectInt(cx, cy, cw, ch);

        cx = 0; cy = 0; cw = captureSize.x; ch = captureSize.y;
        if (captureSize.x > superSampleSize.x)
            cw = superSampleSize.x;
        else
            cx = (superSampleSize.x - captureSize.x) / 2;

        if (captureSize.y > superSampleSize.y)
            ch = superSampleSize.y;
        else
            cy = (superSampleSize.y - captureSize.y) / 2;
        RectInt superSampleRect = new RectInt(cx, cy, cw, ch);

        Logger.DevkitServer.LogConditional(nameof(SatelliteCartography), $"Capture rect: {captureRect.Format()}, imgSize: {imgSize.Format()}, captureSize: {captureSize.Format()}.");

        if (wasSizeOutOfBounds)
        {
            Logger.DevkitServer.LogWarning(nameof(SatelliteCartography), $"Render size was clamped to {imgSize.Format()} because " +
                                                                         $"it was more than the max texture size of this system " +
                                                                         $"(which is {DevkitServerUtility.MaxTextureDimensionSize.Format()}).");
        }
        else if (wasSuperSampleOutOfBounds)
        {
            Logger.DevkitServer.LogWarning(nameof(SatelliteCartography), $"Supersampling size was clamped to {superSampleSize.Format()} because " +
                                                                         $"it was more than the max texture size of this system " +
                                                                         $"(which is {DevkitServerUtility.MaxTextureDimensionSize.Format()}).");
        }

        Bounds captureBounds = CartographyTool.CaptureBounds;

        Transform? mapper = Level.editing.Find("Mapper");
        Camera? renderCamera = mapper == null ? null : mapper.GetComponent<Camera>();

        if (renderCamera == null)
        {
            Logger.DevkitServer.LogError(nameof(SatelliteCartography), $"Capture camera not available to render satellite for {level.getLocalizedName().Format(false)}.");
            return null;
        }

        CartographyCaptureData data = new CartographyCaptureData(level, outputFile, imgSize, captureBounds.size, captureBounds.center, WaterVolumeManager.worldSeaLevel, CartographyType.Satellite, configurationSource.Path, captureRect);

        renderCamera.transform.SetPositionAndRotation(CartographyTool.CaptureBounds.center with
        {
            y = CartographyTool.LegacyMapping ? 1028f : CartographyTool.CaptureBounds.max.y
        }, CartographyTool.TransformMatrix.rotation);

        renderCamera.aspect = CartographyTool.CaptureSize.x / CartographyTool.CaptureSize.y;
        renderCamera.orthographicSize = CartographyTool.CaptureSize.y * 0.5f;

        RenderTexture rt = RenderTexture.GetTemporary(superSampleRect.width, superSampleRect.height, 32);

        rt.name = "Satellite";
        rt.filterMode = FilterMode.Bilinear;

        renderCamera.targetTexture = rt;

        bool fog = RenderSettings.fog;
        AmbientMode ambientMode = RenderSettings.ambientMode;
        Color ambientSkyColor = RenderSettings.ambientSkyColor;
        Color ambientEquaterColor = RenderSettings.ambientEquatorColor;
        Color ambientGroundColor = RenderSettings.ambientGroundColor;
        float lodBias = QualitySettings.lodBias;
        float seaShinyness = LevelLighting.getSeaFloat("_Shininess");
        Color specularSeaColor = LevelLighting.getSeaColor("_SpecularColor");
        ERenderMode renderMode = GraphicsSettings.renderMode;

        GraphicsSettings.renderMode = ERenderMode.FORWARD;
        GraphicsSettings.apply($"Capture satellite for level {level.getLocalizedName()}.");

        RenderSettings.fog = false;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Palette.AMBIENT;
        RenderSettings.ambientEquatorColor = Palette.AMBIENT;
        RenderSettings.ambientGroundColor = Palette.AMBIENT;
        LevelLighting.setSeaFloat("_Shininess", 500f);
        LevelLighting.setSeaColor("_SpecularColor", Color.black);
        QualitySettings.lodBias = float.MaxValue;

        object? captureState = CartographyTool.SavePreCaptureState();
        if (captureState == null)
            Logger.DevkitServer.LogWarning(nameof(SatelliteCartography), "Failed to save/load pre-capture state during satellite capture. Check for updates or report this as a bug.");

        FieldInfo? eventDele = typeof(Level).GetField(nameof(Level.onSatellitePreCapture), BindingFlags.NonPublic | BindingFlags.Static);
        if (eventDele == null)
            Logger.DevkitServer.LogWarning(nameof(SatelliteCartography), "Failed to get Level.onSatellitePreCapture. Check for updates or report this as a bug.");

        Level.SatelliteCaptureDelegate? preCapture = (Level.SatelliteCaptureDelegate?)eventDele?.GetValue(null);

        preCapture?.Invoke();

        renderCamera.Render();

        eventDele = typeof(Level).GetField(nameof(Level.onSatellitePostCapture), BindingFlags.NonPublic | BindingFlags.Static);
        if (eventDele == null)
            Logger.DevkitServer.LogWarning(nameof(SatelliteCartography), "Failed to get Level.onSatellitePostCapture. Check for updates or report this as a bug.");

        Level.SatelliteCaptureDelegate? postCapture = (Level.SatelliteCaptureDelegate?)eventDele?.GetValue(null);

        postCapture?.Invoke();

        CartographyTool.RestorePreCaptureState(captureState);

        GraphicsSettings.renderMode = renderMode;
        RenderSettings.fog = fog;
        RenderSettings.ambientMode = ambientMode;
        RenderSettings.ambientSkyColor = ambientSkyColor;
        RenderSettings.ambientEquatorColor = ambientEquaterColor;
        RenderSettings.ambientGroundColor = ambientGroundColor;
        LevelLighting.setSeaFloat("_Shininess", seaShinyness);
        LevelLighting.setSeaColor("_SpecularColor", specularSeaColor);
        QualitySettings.lodBias = lodBias;
        GraphicsSettings.apply($"Finished capturing satellite for level {level.getLocalizedName()}.");

        Texture2D texture = new Texture2D(imgSize.x, imgSize.y)
        {
            hideFlags = HideFlags.HideAndDontSave
        };

        RenderTexture recaptureTarget = RenderTexture.GetTemporary(captureRect.width, captureRect.height);

        Graphics.Blit(rt, recaptureTarget);

        RenderTexture.ReleaseTemporary(rt);

        RenderTexture? oldActive = RenderTexture.active;
        RenderTexture.active = recaptureTarget;

        Vector2Int capturePos = default;

        if (imgSize.x > captureSize.x)
            capturePos.x = (imgSize.x - captureSize.x) / 2;

        if (captureSize.y < imgSize.y)
            capturePos.y = (imgSize.y - captureSize.y) / 2;

        if (captureSize != imgSize && configData is CompositorPipeline pipeline)
        {
            Color32[] pixels = new Color32[imgSize.x * imgSize.y];
            Array.Fill(pixels, pipeline.BackgroundColor);
            texture.SetPixels32(pixels);
        }

        texture.ReadPixels(new Rect(0, 0, captureRect.width, captureRect.height), capturePos.x, capturePos.y, false);

        RenderTexture.active = oldActive;
        RenderTexture.ReleaseTemporary(recaptureTarget);

        Color32[] c32 = texture.GetPixels32();

        bool anyChanged = false;
        for (int i = 0; i < c32.Length; ++i)
        {
            if (c32[i].a == 255)
                continue;

            c32[i].a = 255;
            anyChanged = true;
        }

        if (anyChanged)
            texture.SetPixels32(c32);

        Stopwatch sw = Stopwatch.StartNew();
        if (!CartographyCompositing.CompositeForeground(texture, configData?.GetActiveCompositors(), in data))
        {
            sw.Stop();
            Logger.DevkitServer.LogInfo(nameof(SatelliteCartography), "No compositing was done.");
        }
        else
        {
            sw.Stop();
            Logger.DevkitServer.LogInfo(nameof(SatelliteCartography), $"Composited satellite in {sw.GetElapsedMilliseconds().Format("F2")} ms.");
        }

        return texture;
    }
#endif
    }
