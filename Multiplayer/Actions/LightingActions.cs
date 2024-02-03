using DevkitServer.API.Lighting;
using DevkitServer.Util.Encoding;
#if SERVER
using DevkitServer.API.Permissions;
using DevkitServer.Core.Permissions;
#endif

namespace DevkitServer.Multiplayer.Actions;
public sealed class LightingActions
{
    public EditorActions EditorActions { get; }
    internal LightingActions(EditorActions actions)
    {
        EditorActions = actions;
    }
    public void Subscribe()
    {
#if CLIENT
        if (EditorActions.IsOwner)
        {
            ClientEvents.OnSetLightingFloat += OnSetLightingFloat;
            ClientEvents.OnSetLightingByte += OnSetLightingByte;
            ClientEvents.OnSetPreviewWeatherAsset += OnSetPreviewWeatherAsset;
            ClientEvents.OnSetTimeColor += OnSetTimeColor;
            ClientEvents.OnSetTimeSingle += OnSetTimeSingle;
        }
#endif
    }
    public void Unsubscribe()
    {
#if CLIENT
        if (EditorActions.IsOwner)
        {
            ClientEvents.OnSetLightingFloat -= OnSetLightingFloat;
            ClientEvents.OnSetLightingByte -= OnSetLightingByte;
            ClientEvents.OnSetPreviewWeatherAsset -= OnSetPreviewWeatherAsset;
            ClientEvents.OnSetTimeColor -= OnSetTimeColor;
            ClientEvents.OnSetTimeSingle -= OnSetTimeSingle;
        }
#endif
    }
    private void OnSetLightingFloat(in SetLightingFloatProperties properties)
    {
        EditorActions.QueueAction(new SetLightingFloatAction
        {
            DeltaTime = properties.DeltaTime,
            Value = properties.Value,
            ValueType = properties.ValueType
        });
    }
    private void OnSetLightingByte(in SetLightingByteProperties properties)
    {
        EditorActions.QueueAction(new SetLightingByteAction
        {
            DeltaTime = properties.DeltaTime,
            Value = properties.Value,
            ValueType = properties.ValueType
        });
    }
    private void OnSetPreviewWeatherAsset(in SetPreviewWeatherAssetProperties properties)
    {
        EditorActions.QueueAction(new SetPreviewWeatherAssetAction
        {
            DeltaTime = properties.DeltaTime,
            Value = properties.Asset
        });
    }
    private void OnSetTimeColor(in SetTimeColorProperties properties)
    {
        EditorActions.QueueAction(new SetTimeColorAction
        {
            DeltaTime = properties.DeltaTime,
            Color = properties.Color,
            Index = properties.Index,
            LightingTime = properties.Time
        });
    }
    private void OnSetTimeSingle(in SetTimeSingleProperties properties)
    {
        EditorActions.QueueAction(new SetTimeSingleAction
        {
            DeltaTime = properties.DeltaTime,
            Value = properties.Value,
            Index = properties.Index,
            LightingTime = properties.Time
        });
    }
}

[Action(DevkitServerActionType.SetLightingFloat, 9, 0)]
public sealed class SetLightingFloatAction : IReplacableAction
{
    public DevkitServerActionType Type => DevkitServerActionType.SetLightingFloat;
    public CSteamID Instigator { get; set; }
    public float DeltaTime { get; set; }
    public float Value { get; set; }
    public LightingValue ValueType { get; set; }
    public bool TryReplaceFrom(IReplacableAction action)
    {
        SetLightingFloatAction a = (SetLightingFloatAction)action;
        if (a.ValueType != ValueType)
            return false;

        Value = a.Value;
        return true;
    }
    public void Apply()
    {
        switch (ValueType)
        {
            case LightingValue.Azimuth:
                LightingUtil.SetAzimuthLocal(Value);
                break;
            case LightingValue.Bias:
                LightingUtil.SetBiasLocal(Value);
                break;
            case LightingValue.Fade:
                LightingUtil.SetFadeLocal(Value);
                break;
            case LightingValue.RainDuration:
                LightingUtil.SetRainDurationLocal(Value);
                break;
            case LightingValue.RainFrequency:
                LightingUtil.SetRainFrequencyLocal(Value);
                break;
            case LightingValue.SnowDuration:
                LightingUtil.SetSnowFrequencyLocal(Value);
                break;
            case LightingValue.SnowFrequency:
                LightingUtil.SetSnowFrequencyLocal(Value);
                break;
            case LightingValue.SeaLevel:
                LightingUtil.SetSeaLevelLocal(Value);
                break;
            case LightingValue.SnowLevel:
                LightingUtil.SetSnowLevelLocal(Value);
                break;
            case LightingValue.Time:
                LightingUtil.SetTimeLocal(Value);
                break;
            default:
                Logger.DevkitServer.LogWarning(nameof(SetLightingFloatAction), $"ValueType is out of range ({ValueType.Format()}).");
                break;
        }
    }
#if SERVER
    public bool CheckCanApply()
    {
        if (ValueType is LightingValue.Azimuth
            or LightingValue.Bias
            or LightingValue.Fade
            or LightingValue.RainDuration
            or LightingValue.RainFrequency
            or LightingValue.SnowDuration
            or LightingValue.SnowFrequency
            or LightingValue.SeaLevel
            or LightingValue.SnowLevel
            or LightingValue.Time)
        {
            return VanillaPermissions.EditLighting.Has(Instigator.m_SteamID);
        }

        Logger.DevkitServer.LogWarning(nameof(SetLightingFloatAction), $"ValueType is out of range ({ValueType.Format()}).");
        return false;
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        ValueType = (LightingValue)reader.ReadUInt8();
        Value = reader.ReadFloat();
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write((byte)ValueType);
        writer.Write(Value);
    }
    public int CalculateSize() => 9;
}

[Action(DevkitServerActionType.SetLightingByte, 6, 0)]
public sealed class SetLightingByteAction : IReplacableAction
{
    public DevkitServerActionType Type => DevkitServerActionType.SetLightingByte;
    public CSteamID Instigator { get; set; }
    public float DeltaTime { get; set; }
    public byte Value { get; set; }
    public LightingValue ValueType { get; set; }
    public bool TryReplaceFrom(IReplacableAction action)
    {
        SetLightingByteAction a = (SetLightingByteAction)action;
        if (a.ValueType != ValueType)
            return false;

        Value = a.Value;
        return true;
    }
    public void Apply()
    {
        switch (ValueType)
        {
            case LightingValue.Moon:
                LightingUtil.SetMoonCycleLocal(Value);
                break;
            case LightingValue.HasRain:
                LightingUtil.SetCanRainLocal(Value != 0);
                break;
            case LightingValue.HasSnow:
                LightingUtil.SetCanSnowLocal(Value != 0);
                break;
            default:
                Logger.DevkitServer.LogWarning(nameof(SetLightingByteAction), $"ValueType is out of range ({ValueType.Format()}).");
                break;
        }
    }
#if SERVER
    public bool CheckCanApply()
    {
        if (ValueType is LightingValue.Moon or LightingValue.HasRain or LightingValue.HasSnow)
            return VanillaPermissions.EditLighting.Has(Instigator.m_SteamID);

        Logger.DevkitServer.LogWarning(nameof(SetLightingByteAction), $"ValueType is out of range ({ValueType.Format()}).");
        return false;
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        ValueType = (LightingValue)reader.ReadUInt8();
        Value = reader.ReadUInt8();
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write((byte)ValueType);
        writer.Write(Value);
    }
    public int CalculateSize() => 6;
}

[Action(DevkitServerActionType.SetPreviewWeatherAsset, 20, 0)]
public sealed class SetPreviewWeatherAssetAction : IReplacableAction
{
    public DevkitServerActionType Type => DevkitServerActionType.SetPreviewWeatherAsset;
    public CSteamID Instigator { get; set; }
    public float DeltaTime { get; set; }
    public AssetReference<WeatherAssetBase> Value { get; set; }
    public bool TryReplaceFrom(IReplacableAction action)
    {
        SetPreviewWeatherAssetAction a = (SetPreviewWeatherAssetAction)action;
        Value = a.Value;
        return true;
    }
    public void Apply()
    {
        LightingUtil.SetPreviewWeatherAssetLocal(Value.Find());
    }
#if SERVER
    public bool CheckCanApply()
    {
        return VanillaPermissions.EditLighting.Has(Instigator.m_SteamID);
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        Value = new AssetReference<WeatherAssetBase>(reader.ReadGuid());
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write(Value.GUID);
    }
    public int CalculateSize() => 20;
}

[Action(DevkitServerActionType.SetTimeColor, 18, 0)]
public sealed class SetTimeColorAction : IReplacableAction
{
    public DevkitServerActionType Type => DevkitServerActionType.SetTimeColor;
    public CSteamID Instigator { get; set; }
    public float DeltaTime { get; set; }
    public Color Color { get; set; }
    public byte Index { get; set; }
    public ELightingTime LightingTime { get; set; }
    public bool TryReplaceFrom(IReplacableAction action)
    {
        SetTimeColorAction a = (SetTimeColorAction)action;
        if (a.Index != Index || a.LightingTime != LightingTime)
            return false;

        Color = a.Color;
        return true;
    }
    public void Apply()
    {
        if ((int)LightingTime >= LevelLighting.times.Length || Index >= LevelLighting.times[(int)LightingTime].colors.Length)
        {
            Logger.DevkitServer.LogWarning(nameof(SetTimeColorAction), "Lighting info out of range.");
            return;
        }

        LightingUtil.SetTimeColor(LightingTime, Index, Color);
    }
#if SERVER
    public bool CheckCanApply()
    {
        if ((int)LightingTime < LevelLighting.times.Length && Index < LevelLighting.times[(int)LightingTime].colors.Length)
            return VanillaPermissions.EditLighting.Has(Instigator.m_SteamID);

        Logger.DevkitServer.LogWarning(nameof(SetTimeColorAction), "Lighting info out of range.");
        return false;
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        LightingTime = (ELightingTime)reader.ReadUInt8();
        Index = reader.ReadUInt8();
        Color = new Color(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat(), 1f);
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write((byte)LightingTime);
        writer.Write(Index);
        writer.Write(Color.r);
        writer.Write(Color.g);
        writer.Write(Color.b);
    }
    public int CalculateSize() => 18;
}

[Action(DevkitServerActionType.SetTimeSingle, 10, 0)]
public sealed class SetTimeSingleAction : IReplacableAction
{
    public DevkitServerActionType Type => DevkitServerActionType.SetTimeSingle;
    public CSteamID Instigator { get; set; }
    public float DeltaTime { get; set; }
    public float Value { get; set; }
    public byte Index { get; set; }
    public ELightingTime LightingTime { get; set; }
    public bool TryReplaceFrom(IReplacableAction action)
    {
        SetTimeSingleAction a = (SetTimeSingleAction)action;
        if (a.Index != Index || a.LightingTime != LightingTime)
            return false;

        Value = a.Value;
        return true;
    }
    public void Apply()
    {
        if ((int)LightingTime >= LevelLighting.times.Length || Index >= LevelLighting.times[(int)LightingTime].singles.Length)
        {
            Logger.DevkitServer.LogWarning(nameof(SetTimeSingleAction), "Lighting info out of range.");
            return;
        }

        LightingUtil.SetTimeSingle(LightingTime, Index, Value);
    }
#if SERVER
    public bool CheckCanApply()
    {
        if ((int)LightingTime < LevelLighting.times.Length && Index < LevelLighting.times[(int)LightingTime].singles.Length)
            return VanillaPermissions.EditLighting.Has(Instigator.m_SteamID);

        Logger.DevkitServer.LogWarning(nameof(SetTimeSingleAction), "Lighting info out of range.");
        return false;
    }
#endif
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        LightingTime = (ELightingTime)reader.ReadUInt8();
        Index = reader.ReadUInt8();
        Value = reader.ReadFloat();
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        writer.Write((byte)LightingTime);
        writer.Write(Index);
        writer.Write(Value);
    }
    public int CalculateSize() => 10;
}