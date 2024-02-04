using DanielWillett.ReflectionTools;
#if CLIENT
using System.Globalization;
using DevkitServer.Patches;
#endif

namespace DevkitServer.Util;
public static class LightingUtil
{
#if SERVER
    private static readonly StaticSetter<float>? SetSeaLevelUnderlyingField = Accessor.GenerateStaticSetter<LevelLighting, float>("_seaLevel");
    private static readonly Action? UpdateLegacyWaterTransform = Accessor.GenerateStaticCaller<LevelLighting, Action>("UpdateLegacyWaterTransform");
#endif
#if CLIENT
    private static readonly StaticGetter<ELightingTime>? GetLightingTime = Accessor.GenerateStaticGetter<EditorEnvironmentLightingUI, ELightingTime>("selectedTime");
    private static readonly StaticSetter<ELightingTime>? SetLightingTime = Accessor.GenerateStaticSetter<EditorEnvironmentLightingUI, ELightingTime>("selectedTime");
    private static readonly Action? CallUpdateLightingTime = Accessor.GenerateStaticCaller<EditorEnvironmentLightingUI, Action>("updateSelection");

    /// <summary>
    /// The selected time in the <see cref="EditorEnvironmentLightingUI"/>.
    /// </summary>
    /// <exception cref="MemberAccessException">The corresponding getter or setter failed to generate for EditorEnvironmentLightingUI.selectedTime.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static ELightingTime SelectedLightingTime
    {
        get
        {
            ThreadUtil.assertIsGameThread();

            return GetLightingTime?.Invoke() ?? throw new MemberAccessException("Unable to create getter for EditorEnvironmentLightingUI.selectedTime");
        }
        set
        {
            ThreadUtil.assertIsGameThread();

            if (SetLightingTime != null)
                SetLightingTime(value);
            else
                throw new MemberAccessException("Unable to create setter for EditorEnvironmentLightingUI.selectedTime");

            CallUpdateLightingTime?.Invoke();
        }
    }

    /// <summary>
    /// Safely gets the selected time in the <see cref="EditorEnvironmentLightingUI"/>.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool TryGetSelectedLightingTime(out ELightingTime lightingTime)
    {
        ThreadUtil.assertIsGameThread();

        if (GetLightingTime == null)
        {
            lightingTime = default;
            return false;
        }

        lightingTime = GetLightingTime();
        return true;
    }

    /// <summary>
    /// Safely sets the selected time in the <see cref="EditorEnvironmentLightingUI"/>.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool TrySetSelectedLightingTime(ELightingTime lightingTime)
    {
        ThreadUtil.assertIsGameThread();

        if (SetLightingTime == null)
            return false;

        SetLightingTime(lightingTime);
        CallUpdateLightingTime?.Invoke();
        return true;
    }
#endif

    /// <summary>
    /// Gets an English display string for a time's color value at the given index.
    /// </summary>
    public static string GetTimeColorDisplayString(int index)
    {
        return index switch
        {
            0 => "Sun",
            1 => "Sea",
            2 => "Fog",
            3 => "Top Sky",
            4 => "Middle Sky",
            5 => "Bottom Sky",
            6 => "Top Ambient",
            7 => "Middle Ambient",
            8 => "Bottom Ambient",
            9 => "Clouds",
            10 => "Rays",
            11 => "Particle Lighting",
            _ => index.ToString(CultureInfo.InvariantCulture)
        };
    }

    /// <summary>
    /// Gets an English display string for a time's floating point value at the given index.
    /// </summary>
    public static string GetTimeSingleDisplayString(int index)
    {
        return index switch
        {
            0 => "Intensity",
            1 => "Fog",
            2 => "Clouds",
            3 => "Shadows",
            4 => "Rays",
            _ => index.ToString(CultureInfo.InvariantCulture)
        };
    }

    /// <summary>
    /// Locally sets <see cref="LevelLighting.azimuth"/> and updates the UI.
    /// </summary>
    /// <remarks><paramref name="azimuthDeg"/> will be converted into a 0° to 360° range.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void SetAzimuthLocal(float azimuthDeg)
    {
        ThreadUtil.assertIsGameThread();

        LevelLighting.azimuth = (azimuthDeg % 360 + 360) % 360;
#if CLIENT
        LightingPatches.UpdateAzimuth();
#endif
    }

    /// <summary>
    /// Locally sets <see cref="LevelLighting.bias"/> and updates the UI.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void SetBiasLocal(float bias)
    {
        ThreadUtil.assertIsGameThread();

        LevelLighting.bias = bias;
#if CLIENT
        LightingPatches.UpdateBias();
#endif
    }

    /// <summary>
    /// Locally sets <see cref="LevelLighting.fade"/> and updates the UI.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void SetFadeLocal(float fade)
    {
        ThreadUtil.assertIsGameThread();

        LevelLighting.fade = fade;
#if CLIENT
        LightingPatches.UpdateFade();
#endif
    }

    /// <summary>
    /// Locally sets <see cref="LevelLighting.rainDur"/> and updates the UI.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void SetRainDurationLocal(float rainDurationSec)
    {
        ThreadUtil.assertIsGameThread();

        LevelLighting.rainDur = rainDurationSec;
#if CLIENT
        LightingPatches.UpdateRainDuration();
#endif
    }

    /// <summary>
    /// Locally sets <see cref="LevelLighting.rainFreq"/> and updates the UI.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void SetRainFrequencyLocal(float rainFrequencySec)
    {
        ThreadUtil.assertIsGameThread();

        LevelLighting.rainFreq = rainFrequencySec;
#if CLIENT
        LightingPatches.UpdateRainFrequency();
#endif
    }

    /// <summary>
    /// Locally sets <see cref="LevelLighting.snowDur"/> and updates the UI.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void SetSnowDurationLocal(float snowDurationSec)
    {
        ThreadUtil.assertIsGameThread();

        LevelLighting.snowDur = snowDurationSec;
#if CLIENT
        LightingPatches.UpdateSnowDuration();
#endif
    }

    /// <summary>
    /// Locally sets <see cref="LevelLighting.snowFreq"/> and updates the UI.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void SetSnowFrequencyLocal(float snowFrequencySec)
    {
        ThreadUtil.assertIsGameThread();

        LevelLighting.snowFreq = snowFrequencySec;
#if CLIENT
        LightingPatches.UpdateSnowFrequency();
#endif
    }

    /// <summary>
    /// Locally sets <see cref="LevelLighting.seaLevel"/> and updates the UI.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void SetSeaLevelLocal(float seaLevel)
    {
        ThreadUtil.assertIsGameThread();

#if SERVER
        // setting sea level on server throws an exception because it tries to update bubbles :/
        if (SetSeaLevelUnderlyingField != null)
        {
            SetSeaLevelUnderlyingField(seaLevel);
            UpdateLegacyWaterTransform?.Invoke();
            return;
        }
#endif
        LevelLighting.seaLevel = seaLevel;
#if CLIENT
        LightingPatches.UpdateSeaLevel();
#endif
    }

    /// <summary>
    /// Locally sets <see cref="LevelLighting.snowLevel"/> and updates the UI.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void SetSnowLevelLocal(float snowLevel)
    {
        ThreadUtil.assertIsGameThread();

        LevelLighting.snowLevel = snowLevel;
#if CLIENT
        LightingPatches.UpdateSnowLevel();
#endif
    }

    /// <summary>
    /// Locally sets <see cref="LevelLighting.time"/> and updates the UI.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void SetTimeLocal(float time)
    {
        ThreadUtil.assertIsGameThread();

        LevelLighting.time = time;
#if CLIENT
        LightingPatches.UpdateTime();
#endif
    }

    /// <summary>
    /// Locally sets <see cref="LevelLighting.moon"/> and updates the UI.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void SetMoonCycleLocal(byte moonCycle)
    {
        ThreadUtil.assertIsGameThread();

        LevelLighting.moon = moonCycle;
#if CLIENT
        LightingPatches.UpdateMoonCycle();
#endif
    }

    /// <summary>
    /// Locally sets <see cref="LevelLighting.canRain"/> and updates the UI.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void SetCanRainLocal(bool canRain)
    {
        ThreadUtil.assertIsGameThread();

        LevelLighting.canRain = canRain;
#if CLIENT
        LightingPatches.UpdateCanRain();
#endif
    }

    /// <summary>
    /// Locally sets <see cref="LevelLighting.canSnow"/> and updates the UI.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void SetCanSnowLocal(bool canSnow)
    {
        ThreadUtil.assertIsGameThread();

        LevelLighting.canSnow = canSnow;
#if CLIENT
        LightingPatches.UpdateCanSnow();
#endif
    }

    /// <summary>
    /// Locally sets the preview weather asset and updates the UI.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void SetPreviewWeatherAssetLocal(WeatherAssetBase? asset, float blendAlpha = 0f)
    {
        ThreadUtil.assertIsGameThread();

        LightingManager.ReceiveLightingActiveWeather(asset?.GUID ?? Guid.Empty, blendAlpha, default);
#if CLIENT
        LightingPatches.UpdateWeatherAsset();
#endif
    }

    /// <summary>
    /// Locally sets a color value at a specific time and updates the UI.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void SetTimeColor(ELightingTime time, int index, Color color)
    {
        if (!CheckTimeColorSafe(time, index))
            throw new ArgumentOutOfRangeException((int)time < LevelLighting.times.Length ? nameof(index) : nameof(time), "Time and/or index is out of range.");

        LevelLighting.times[(int)time].colors[index] = color with { a = 1f };
#if CLIENT
        LightingPatches.UpdateTimeColor(time, index);
#endif
    }

    /// <summary>
    /// Locally sets a single value at a specific time and updates the UI.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void SetTimeSingle(ELightingTime time, int index, float single)
    {
        if (!CheckTimeSingleSafe(time, index))
            throw new ArgumentOutOfRangeException((int)time < LevelLighting.times.Length ? nameof(index) : nameof(time), "Time and/or index is out of range.");

        LevelLighting.times[(int)time].singles[index] = single;
#if CLIENT
        LightingPatches.UpdateTimeSingle(time, index);
#endif
    }

    /// <summary>
    /// Checks if a time and index of a color value is safe.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool CheckTimeColorSafe(ELightingTime time, int index)
    {
        ThreadUtil.assertIsGameThread();

        return time >= 0 && index >= 0 && (int)time < LevelLighting.times.Length && index < LevelLighting.times[(int)time].colors.Length;
    }

    /// <summary>
    /// Checks if a time and index of a single value is safe.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool CheckTimeSingleSafe(ELightingTime time, int index)
    {
        ThreadUtil.assertIsGameThread();

        return time >= 0 && index >= 0 && (int)time < LevelLighting.times.Length && index < LevelLighting.times[(int)time].singles.Length;
    }
}