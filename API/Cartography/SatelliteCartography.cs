﻿using Cysharp.Threading.Tasks;
using DanielWillett.ReflectionTools;
using DevkitServer.Core.Cartography;
using SDG.Framework.Water;
using System.Diagnostics;
using System.Reflection;
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
    public static async UniTask<string?> CaptureSatellite(LevelInfo? level = null, string? outputFile = null, CancellationToken token = default)
    {
        await UniTask.SwitchToMainThread(token);
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

        Texture2D? texture = CaptureSatelliteSync(level, outputFile);

        if (texture == null)
            return null;

        Stopwatch sw = Stopwatch.StartNew();
        texture.Apply();
        sw.Stop();

        Logger.DevkitServer.LogDebug(nameof(SatelliteCartography), $"Apply texture: {sw.GetElapsedMilliseconds().Format("0.##")} ms.");

        await FileUtil.EncodeAndSaveTexture(texture, outputFile, JpegQuality, token);
#if DEBUG
        ThreadUtil.assertIsGameThread();
#endif
        await UniTask.SwitchToMainThread();
        Object.DestroyImmediate(texture);

        return outputFile;
    }
    private static Texture2D? CaptureSatelliteSync(LevelInfo level, string outputFile)
    {
        // should be ran at end of frame

        Vector2Int imgSize = CartographyTool.GetImageSizeCheckMaxTextureSize(out Vector2Int superSampleSize, out bool wasSizeOutOfBounds, out bool wasSuperSampleOutOfBounds);

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

        LevelCartographyConfigData? configData = LevelCartographyConfigData.ReadFromLevel(level);

        CartographyCaptureData data = new CartographyCaptureData(level, outputFile, imgSize, captureBounds.size, captureBounds.center, WaterVolumeManager.worldSeaLevel, false);

        renderCamera.transform.SetPositionAndRotation(CartographyTool.CaptureBounds.center with
        {
            y = CartographyTool.LegacyMapping ? 1028f : CartographyTool.CaptureBounds.max.y
        }, CartographyTool.TransformMatrix.rotation);

        renderCamera.aspect = CartographyTool.CaptureSize.x / CartographyTool.CaptureSize.y;
        renderCamera.orthographicSize = CartographyTool.CaptureSize.y * 0.5f;

        RenderTexture rt = RenderTexture.GetTemporary(superSampleSize.x, superSampleSize.y, 32);

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

        RenderTexture recaptureTarget = RenderTexture.GetTemporary(imgSize.x, imgSize.y);

        Graphics.Blit(rt, recaptureTarget);

        RenderTexture.ReleaseTemporary(rt);

        RenderTexture? oldActive = RenderTexture.active;
        RenderTexture.active = recaptureTarget;

        texture.ReadPixels(new Rect(0f, 0f, imgSize.x, imgSize.y), 0, 0);

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
        if (!CartographyCompositing.CompositeForeground(texture, configData, in data))
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
