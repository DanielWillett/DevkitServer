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
#endif
    public static void SetAzimuthLocal(float azimuthDeg)
    {
        LevelLighting.azimuth = (azimuthDeg % 360 + 360) % 360;
#if CLIENT
        LightingPatches.UpdateAzimuth();
#endif
    }
    public static void SetBiasLocal(float bias)
    {
        LevelLighting.bias = bias;
#if CLIENT
        LightingPatches.UpdateBias();
#endif
    }
    public static void SetFadeLocal(float fade)
    {
        LevelLighting.fade = fade;
#if CLIENT
        LightingPatches.UpdateFade();
#endif
    }
    public static void SetRainDurationLocal(float rainDurationSec)
    {
        LevelLighting.rainDur = rainDurationSec;
#if CLIENT
        LightingPatches.UpdateRainDuration();
#endif
    }
    public static void SetRainFrequencyLocal(float rainFrequencySec)
    {
        LevelLighting.rainFreq = rainFrequencySec;
#if CLIENT
        LightingPatches.UpdateRainFrequency();
#endif
    }
    public static void SetSnowDurationLocal(float snowDurationSec)
    {
        LevelLighting.snowDur = snowDurationSec;
#if CLIENT
        LightingPatches.UpdateSnowDuration();
#endif
    }
    public static void SetSnowFrequencyLocal(float snowFrequencySec)
    {
        LevelLighting.snowFreq = snowFrequencySec;
#if CLIENT
        LightingPatches.UpdateSnowFrequency();
#endif
    }
    public static void SetSeaLevelLocal(float seaLevel)
    {
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
    public static void SetSnowLevelLocal(float snowLevel)
    {
        LevelLighting.snowLevel = snowLevel;
#if CLIENT
        LightingPatches.UpdateSnowLevel();
#endif
    }
    public static void SetTimeLocal(float time)
    {
        LevelLighting.time = time;
#if CLIENT
        LightingPatches.UpdateTime();
#endif
    }
    public static void SetMoonCycleLocal(byte moonCycle)
    {
        LevelLighting.moon = moonCycle;
#if CLIENT
        LightingPatches.UpdateMoonCycle();
#endif
    }
    public static void SetCanRainLocal(bool canRain)
    {
        LevelLighting.canRain = canRain;
#if CLIENT
        LightingPatches.UpdateCanRain();
#endif
    }
    public static void SetCanSnowLocal(bool canSnow)
    {
        LevelLighting.canSnow = canSnow;
#if CLIENT
        LightingPatches.UpdateCanSnow();
#endif
    }
    public static void SetPreviewWeatherAssetLocal(WeatherAssetBase? asset, float blendAlpha = 0f)
    {
        LightingManager.ReceiveLightingActiveWeather(asset?.GUID ?? Guid.Empty, blendAlpha, default);
#if CLIENT
        LightingPatches.UpdateWeatherAsset();
#endif
    }
    public static void SetTimeColor(ELightingTime time, int index, Color color)
    {
        if (!CheckTimeColorSafe(time, index))
            throw new ArgumentOutOfRangeException((int)time < LevelLighting.times.Length ? nameof(index) : nameof(time), "Time and/or index is out of range.");

        LevelLighting.times[(int)time].colors[index] = color with { a = 1f };
#if CLIENT
        LightingPatches.UpdateTimeColor(time, index);
#endif
    }
    public static void SetTimeSingle(ELightingTime time, int index, float single)
    {
        if (!CheckTimeSingleSafe(time, index))
            throw new ArgumentOutOfRangeException((int)time < LevelLighting.times.Length ? nameof(index) : nameof(time), "Time and/or index is out of range.");

        LevelLighting.times[(int)time].singles[index] = single;
#if CLIENT
        LightingPatches.UpdateTimeSingle(time, index);
#endif
    }

    public static bool CheckTimeColorSafe(ELightingTime time, int index)
    {
        return time >= 0 && index >= 0 && (int)time < LevelLighting.times.Length && index < LevelLighting.times[(int)time].colors.Length;
    }
    public static bool CheckTimeSingleSafe(ELightingTime time, int index)
    {
        return time >= 0 && index >= 0 && (int)time < LevelLighting.times.Length && index < LevelLighting.times[(int)time].singles.Length;
    }
}