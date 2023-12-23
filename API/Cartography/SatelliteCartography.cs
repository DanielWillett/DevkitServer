using Cysharp.Threading.Tasks;
using DevkitServer.Core.Cartography;
using System.Diagnostics;
using System.Reflection;
using UnityEngine.Rendering;
using GraphicsSettings = SDG.Unturned.GraphicsSettings;

namespace DevkitServer.API.Cartography;

/// <summary>
/// Contains replacement rendering code for satellite rendering implementing custom compositors/post-processors.
/// </summary>
[EarlyTypeInit]
public static class SatelliteCartography
{
#if CLIENT
    /// <summary>
    /// Captures a satellite render of <paramref name="level"/> (<see cref="Level.info"/> by default) and exports it to <paramref name="outputFile"/> (Level Path/Map.png by default).
    /// </summary>
    /// <remarks>This does not work on the server build. Passing a custom level info does not affect measurements so only do so if they represent the same level.</remarks>
    /// <returns>The path of the output file created, or <see langword="null"/> if the chart was not rendered.</returns>
    public static async UniTask<string?> CaptureSatellite(LevelInfo? level = null, string? outputFile = null, CancellationToken token = default)
    {
        await UniTask.SwitchToMainThread(token);
        await UniTask.WaitForEndOfFrame(DevkitServerModule.ComponentHost, token);

        return CaptureSatelliteIntl(level, outputFile);
    }
    private static string? CaptureSatelliteIntl(LevelInfo? level = null, string? outputFile = null)
    {
        // should be ran at the end of frame

        int imgX = CartographyTool.ImageWidth;
        int imgY = CartographyTool.ImageHeight;

        Bounds captureBounds = CartographyTool.CaptureBounds;

        level ??= Level.info;
        outputFile ??= Path.Combine(level.path, "Map.png");

        Transform? mapper = Level.editing.Find("Mapper");
        Camera? renderCamera = mapper == null ? null : mapper.GetComponent<Camera>();

        if (renderCamera == null)
        {
            Logger.DevkitServer.LogError(nameof(SatelliteCartography), $"Capture camera not available to render satellite for {level.getLocalizedName().Format(false)}.");
            return null;
        }

        LevelCartographyConfigData? configData = LevelCartographyConfigData.ReadFromLevel(level);

        CartographyCaptureData data = new CartographyCaptureData(level, outputFile, new Vector2Int(imgX, imgY), captureBounds.size, captureBounds.center, false);

        bool fog = RenderSettings.fog;
        AmbientMode ambientMode = RenderSettings.ambientMode;
        Color ambientSkyColor = RenderSettings.ambientSkyColor;
        Color ambientEquaterColor = RenderSettings.ambientEquatorColor;
        Color ambientGroundColor = RenderSettings.ambientGroundColor;
        float lodBias = QualitySettings.lodBias;
        float seaShinyness = LevelLighting.getSeaFloat("_Shininess");
        Color specularSeaColor = LevelLighting.getSeaColor("_SpecularColor");
        ERenderMode renderMode = GraphicsSettings.renderMode;

        object? captureState = CartographyTool.SavePreCaptureState();
        if (captureState == null)
            Logger.DevkitServer.LogWarning(nameof(ChartCartography), "Failed to save/load pre-capture state during satellite capture. Check for updates or report this as a bug.");

        FieldInfo? eventDele = typeof(Level).GetField(nameof(Level.onSatellitePreCapture), BindingFlags.NonPublic | BindingFlags.Static);
        if (eventDele == null)
            Logger.DevkitServer.LogWarning(nameof(ChartCartography), "Failed to get Level.onSatellitePreCapture. Check for updates or report this as a bug.");
        
        Level.SatelliteCaptureDelegate? preCapture = (Level.SatelliteCaptureDelegate?)eventDele?.GetValue(null);

        GraphicsSettings.renderMode = ERenderMode.FORWARD;
        RenderSettings.fog = false;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Palette.AMBIENT;
        RenderSettings.ambientEquatorColor = Palette.AMBIENT;
        RenderSettings.ambientGroundColor = Palette.AMBIENT;
        LevelLighting.setSeaFloat("_Shininess", 500f);
        LevelLighting.setSeaColor("_SpecularColor", Color.black);
        QualitySettings.lodBias = float.MaxValue;
        GraphicsSettings.apply($"Capture satellite for level {level.getLocalizedName()}.");

        renderCamera.transform.SetPositionAndRotation(CartographyTool.CaptureBounds.center with
        {
            y = CartographyTool.LegacyMapping ? 1028f : CartographyTool.CaptureBounds.max.y
        }, CartographyTool.TransformMatrix.rotation);

        renderCamera.aspect = CartographyTool.CaptureSize.x / CartographyTool.CaptureSize.y;
        renderCamera.orthographicSize = CartographyTool.CaptureSize.y * 0.5f;

        RenderTexture rt = RenderTexture.GetTemporary(imgX, imgY, 32);
        rt.name = "Satellite";
        rt.filterMode = FilterMode.Bilinear;

        renderCamera.targetTexture = rt;

        preCapture?.Invoke();

        Stopwatch sw = Stopwatch.StartNew();

        renderCamera.Render();

        sw.Stop();
        Logger.DevkitServer.LogDebug(nameof(SatelliteCartography), $"Render: {sw.GetElapsedMilliseconds().Format("0.##")} ms.");

        eventDele = typeof(Level).GetField(nameof(Level.onSatellitePostCapture), BindingFlags.NonPublic | BindingFlags.Static);
        if (eventDele == null)
            Logger.DevkitServer.LogWarning(nameof(ChartCartography), "Failed to get Level.onSatellitePostCapture. Check for updates or report this as a bug.");
        
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

        Texture2D texture = new Texture2D(imgX, imgY)
        {
            hideFlags = HideFlags.HideAndDontSave
        };

        RenderTexture? oldActive = RenderTexture.active;
        RenderTexture.active = rt;

        sw.Restart();
        texture.ReadPixels(new Rect(0f, 0f, imgX, imgY), 0, 0);
        sw.Stop();
        Logger.DevkitServer.LogDebug(nameof(SatelliteCartography), $"ReadPixels: {sw.GetElapsedMilliseconds().Format("0.##")} ms.");

        RenderTexture.active = oldActive;
        RenderTexture.ReleaseTemporary(rt);

        sw.Restart();
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

        sw.Stop();
        Logger.DevkitServer.LogDebug(nameof(SatelliteCartography), $"Apply transparency: {sw.GetElapsedMilliseconds().Format("0.##")} ms.");

        sw.Restart();
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

        sw.Restart();
        texture.Apply();
        sw.Stop();

        Logger.DevkitServer.LogDebug(nameof(SatelliteCartography), $"Apply texture: {sw.GetElapsedMilliseconds().Format("0.##")} ms.");

        sw.Restart();
        byte[] encoded = texture.EncodeToPNG();
        sw.Stop();
        Logger.DevkitServer.LogDebug(nameof(SatelliteCartography), $"Encode: {sw.GetElapsedMilliseconds().Format("0.##")} ms.");

        sw.Restart();
        File.WriteAllBytes(outputFile, encoded);
        sw.Stop();
        Logger.DevkitServer.LogDebug(nameof(SatelliteCartography), $"Write: {sw.GetElapsedMilliseconds().Format("0.##")} ms.");
        return outputFile;
    }
#endif
}
