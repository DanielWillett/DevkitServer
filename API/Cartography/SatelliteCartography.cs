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

        try
        {
            await FileUtil.EncodeAndSaveTextureWithRetry(texture, outputFile, JpegQuality, token: token);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(nameof(SatelliteCartography), ex, "Failed to write GPS texture.");
        }
        await UniTask.SwitchToMainThread();
        Object.DestroyImmediate(texture);

        return outputFile;
    }
    private static Texture2D? CaptureSatelliteSync(LevelInfo level, LevelCartographyConfigData? configData, string outputFile, [InstantHandle] CartographyConfigurationSource configurationSource)
    {
        // should be ran at end of frame

        CompositorPipeline? pipeline = configData as CompositorPipeline;

        // default size of texture
        Vector2 imgSizeUnrounded = CartographyTool.GetImageSizeCheckMaxTextureSize(out bool wasSizeOutOfBounds, configData, needsToSuperSample: true);
        Vector2Int imgSize = new Vector2Int((int)Math.Ceiling(imgSizeUnrounded.x), (int)Math.Ceiling(imgSizeUnrounded.y));
        Vector2Int imgOffset = Vector2Int.zero;

        // size of actual texture
        Vector2Int textureSize = imgSize;

#if CLIENT
        // apply PO2 scaling, etc
        pipeline?.TryApplyScalingAdjustments(ref wasSizeOutOfBounds, ref imgSize, ref imgOffset, ref textureSize);
#endif

        Logger.DevkitServer.LogDebug(nameof(SatelliteCartography), $"Image size (unrounded) : {imgSizeUnrounded.Format("F2")}");
        Logger.DevkitServer.LogDebug(nameof(SatelliteCartography), $"Image size (rounded)   : {imgSize.Format("F2")}");
        Logger.DevkitServer.LogDebug(nameof(SatelliteCartography), $"Texture size           : {textureSize.Format("F2")}");

        Vector2 captureSize = CartographyTool.ImageSizeUnrounded;
        Rect captureRect = new Rect(0, 0, captureSize.x, captureSize.y);
        RectInt imageRect = new RectInt(imgOffset.x, imgOffset.y, imgSize.x, imgSize.y);

        if (wasSizeOutOfBounds)
        {
            Logger.DevkitServer.LogWarning(nameof(SatelliteCartography), $"Render size was clamped to {imgSize.Format()} because " +
                                                                     $"it was more than the max texture size of this system " +
                                                                     $"(which is {DevkitServerUtility.MaxTextureDimensionSize.Format()}).");
        }

        Bounds captureBounds = CartographyTool.CaptureBounds;

        Logger.DevkitServer.LogDebug(nameof(SatelliteCartography), $"Capture size   : {captureSize.Format("F2")}");
        Logger.DevkitServer.LogDebug(nameof(SatelliteCartography), $"Image rect     : {imageRect.Format("F2")}");
        Logger.DevkitServer.LogDebug(nameof(SatelliteCartography), $"Capture bounds : {captureBounds.Format("F2")}");

        Logger.DevkitServer.LogConditional(nameof(SatelliteCartography), $"Capture rect: {captureRect.Format()}, imgSize: {imgSize.Format()}, captureSize: {captureSize.Format()}.");

        if (wasSizeOutOfBounds)
        {
            Logger.DevkitServer.LogWarning(nameof(SatelliteCartography), $"Render size was clamped to {imgSize.Format()} because the supersample " +
                                                                         $"(x2 rendering for maps) was more than the max texture size of this system " +
                                                                         $"(which is {DevkitServerUtility.MaxTextureDimensionSize.Format()}).");
        }

        Transform? mapper = Level.editing.Find("Mapper");
        Camera? renderCamera = mapper == null ? null : mapper.GetComponent<Camera>();

        if (renderCamera == null)
        {
            Logger.DevkitServer.LogError(nameof(SatelliteCartography), $"Capture camera not available to render satellite for {level.getLocalizedName().Format(false)}.");
            return null;
        }

        CartographyCaptureData data = new CartographyCaptureData(level, outputFile, textureSize, captureBounds.size, captureBounds.center, CartographyType.Satellite, configurationSource.Path, captureRect, imageRect);

        renderCamera.transform.SetPositionAndRotation(CartographyTool.CaptureBounds.center with
        {
            y = CartographyTool.LegacyMapping ? 1028f : CartographyTool.CaptureBounds.max.y
        }, CartographyTool.TransformMatrix.rotation);

        Vector2 captureSizeWorld = CartographyTool.CaptureSize;
        renderCamera.aspect = captureSizeWorld.x / captureSizeWorld.y;
        renderCamera.orthographicSize = captureSizeWorld.y * 0.5f;

        RenderTexture rt = RenderTexture.GetTemporary(imgSize.x * 2, imgSize.y * 2, 32);

        rt.name = "Satellite";
        rt.filterMode = FilterMode.Bilinear;

        renderCamera.targetTexture = rt;
        Color oldBkgrColor = renderCamera.backgroundColor;
        CameraClearFlags oldClearFlags = renderCamera.clearFlags;
        if (pipeline != null)
        {
            Color bkgr = pipeline.BackgroundColor;
            renderCamera.backgroundColor = bkgr;
            oldClearFlags = CameraClearFlags.Color;
        }

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

        CartographyTool.SavePreCaptureState();
        
        FieldInfo? eventDele = typeof(Level).GetField(nameof(Level.onSatellitePreCapture), BindingFlags.NonPublic | BindingFlags.Static);
        if (eventDele == null)
            Logger.DevkitServer.LogWarning(nameof(SatelliteCartography), "Failed to get Level.onSatellitePreCapture. Check for updates or report this as a bug.");

        Level.SatelliteCaptureDelegate? preCapture = (Level.SatelliteCaptureDelegate?)eventDele?.GetValue(null);

        preCapture?.Invoke();

        renderCamera.Render();

        if (pipeline != null)
        {
            renderCamera.backgroundColor = oldBkgrColor;
            renderCamera.clearFlags = oldClearFlags;
        }

        eventDele = typeof(Level).GetField(nameof(Level.onSatellitePostCapture), BindingFlags.NonPublic | BindingFlags.Static);
        if (eventDele == null)
            Logger.DevkitServer.LogWarning(nameof(SatelliteCartography), "Failed to get Level.onSatellitePostCapture. Check for updates or report this as a bug.");

        Level.SatelliteCaptureDelegate? postCapture = (Level.SatelliteCaptureDelegate?)eventDele?.GetValue(null);

        postCapture?.Invoke();

        CartographyTool.RestorePreCaptureState();

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

        Texture2D texture = new Texture2D(textureSize.x, textureSize.y)
        {
            hideFlags = HideFlags.HideAndDontSave
        };

        RenderTexture recaptureTarget = RenderTexture.GetTemporary(imgSize.x, imgSize.y);

        Graphics.Blit(rt, recaptureTarget);

        RenderTexture.ReleaseTemporary(rt);

        RenderTexture? oldActive = RenderTexture.active;
        RenderTexture.active = recaptureTarget;

        if (captureSize != imgSize && pipeline != null)
        {
            Color32[] pixels = new Color32[textureSize.x * textureSize.y];
            Array.Fill(pixels, pipeline.BackgroundColor);
            texture.SetPixels32(pixels);
        }

        texture.ReadPixels(
            new Rect(0, 0, imgSize.x, imgSize.y),
            data.ImageWriteArea.x,
            data.ImageWriteArea.y,
            false
        );

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