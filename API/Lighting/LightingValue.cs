namespace DevkitServer.API.Lighting;

/// <summary>
/// Represents a <see cref="bool"/>, <see cref="float"/>, asset reference, <see cref="Color"/> per <see cref="ELightingTime"/>, or <see cref="float"/> per <see cref="ELightingTime"/> in the lighting menu.
/// </summary>
public enum LightingValue
{
    /// <summary><see cref="float"/></summary>
    Azimuth,
    /// <summary><see cref="float"/></summary>
    Bias,
    /// <summary><see cref="float"/></summary>
    Fade,
    /// <summary><see cref="float"/></summary>
    SeaLevel,
    /// <summary><see cref="float"/></summary>
    SnowLevel,
    /// <summary><see cref="bool"/></summary>
    HasRain,
    /// <summary><see cref="bool"/></summary>
    HasSnow,
    /// <summary><see cref="float"/></summary>
    RainFrequency,
    /// <summary><see cref="float"/></summary>
    RainDuration,
    /// <summary><see cref="float"/></summary>
    SnowFrequency,
    /// <summary><see cref="float"/></summary>
    SnowDuration,
    /// <summary><see cref="byte"/></summary>
    Moon,
    /// <summary><see cref="float"/></summary>
    Time,
    /// <summary><see cref="WeatherAssetBase"/></summary>
    WeatherAsset,
    /// <summary><see cref="ELightingTime"/>, <see cref="byte"/>, <see cref="Color"/></summary>
    TimeColor,
    /// <summary><see cref="ELightingTime"/>, <see cref="byte"/>, <see cref="float"/></summary>
    TimeSingle
}