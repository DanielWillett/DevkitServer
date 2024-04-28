#if CLIENT
using DanielWillett.ReflectionTools;
using DevkitServer.API.Abstractions;
using DevkitServer.API.Lighting;
using DevkitServer.API.Permissions;
using DevkitServer.API.UI;
using DevkitServer.Core.Permissions;
using DevkitServer.Multiplayer;
using DevkitServer.Multiplayer.Actions;
using HarmonyLib;
using System.Reflection;

namespace DevkitServer.Patches;

[HarmonyPatch]
internal class LightingPatches
{
    private const string Source = nameof(LightingPatches);
    private static IOptionalPatch? _onDraggedAzimuthSlider;
    private static IOptionalPatch? _onDraggedBiasSlider;
    private static IOptionalPatch? _onDraggedFadeSlider;
    private static IOptionalPatch? _onValuedSeaLevelSlider;
    private static IOptionalPatch? _onValuedSnowLevelSlider;
    private static IOptionalPatch? _onToggledRainToggle;
    private static IOptionalPatch? _onToggledSnowToggle;
    private static IOptionalPatch? _onTypedRainFreqField;
    private static IOptionalPatch? _onTypedRainDurField;
    private static IOptionalPatch? _onTypedSnowFreqField;
    private static IOptionalPatch? _onTypedSnowDurField;
    private static IOptionalPatch? _onDraggedMoonSlider;
    private static IOptionalPatch? _onDraggedTimeSlider;
    private static IOptionalPatch? _onClickedTimeButton;
    private static IOptionalPatch? _onClickedPreviewWeather;
    private static IOptionalPatch? _onPickedColorPicker;
    private static IOptionalPatch? _onDraggedSingleSlider;

    private static readonly StaticGetter<ISleekButton[]>? GetTimeButtons = Accessor.GenerateStaticGetter<EditorEnvironmentLightingUI, ISleekButton[]>("timeButtons");
    private static readonly StaticGetter<SleekColorPicker[]>? GetColorPickers = Accessor.GenerateStaticGetter<EditorEnvironmentLightingUI, SleekColorPicker[]>("colorPickers");
    private static readonly StaticGetter<ISleekSlider[]>? GetSingleSliders = Accessor.GenerateStaticGetter<EditorEnvironmentLightingUI, ISleekSlider[]>("singleSliders");
    private static readonly StaticGetter<ISleekSlider>? GetTimeSlider = Accessor.GenerateStaticGetter<EditorEnvironmentLightingUI, ISleekSlider>("timeSlider");
    private static readonly StaticGetter<ISleekField>? GetWeatherGuidField = Accessor.GenerateStaticGetter<EditorEnvironmentLightingUI, ISleekField>("weatherGuidField");
    private static readonly StaticGetter<ISleekSlider>? GetAzimuthSlider = Accessor.GenerateStaticGetter<EditorEnvironmentLightingUI, ISleekSlider>("azimuthSlider");
    private static readonly StaticGetter<ISleekSlider>? GetBiasSlider = Accessor.GenerateStaticGetter<EditorEnvironmentLightingUI, ISleekSlider>("biasSlider");
    private static readonly StaticGetter<ISleekSlider>? GetFadeSlider = Accessor.GenerateStaticGetter<EditorEnvironmentLightingUI, ISleekSlider>("fadeSlider");
    private static readonly StaticGetter<SleekValue>? GetSeaLevelSlider = Accessor.GenerateStaticGetter<EditorEnvironmentLightingUI, SleekValue>("seaLevelSlider");
    private static readonly StaticGetter<SleekValue>? GetSnowLevelSlider = Accessor.GenerateStaticGetter<EditorEnvironmentLightingUI, SleekValue>("snowLevelSlider");
    private static readonly StaticGetter<ISleekFloat32Field>? GetRainFreqField = Accessor.GenerateStaticGetter<EditorEnvironmentLightingUI, ISleekFloat32Field>("rainFreqField");
    private static readonly StaticGetter<ISleekFloat32Field>? GetRainDurField = Accessor.GenerateStaticGetter<EditorEnvironmentLightingUI, ISleekFloat32Field>("rainDurField");
    private static readonly StaticGetter<ISleekFloat32Field>? GetSnowFreqField = Accessor.GenerateStaticGetter<EditorEnvironmentLightingUI, ISleekFloat32Field>("snowFreqField");
    private static readonly StaticGetter<ISleekFloat32Field>? GetSnowDurField = Accessor.GenerateStaticGetter<EditorEnvironmentLightingUI, ISleekFloat32Field>("snowDurField");
    private static readonly StaticGetter<ISleekToggle>? GetRainToggle = Accessor.GenerateStaticGetter<EditorEnvironmentLightingUI, ISleekToggle>("rainToggle");
    private static readonly StaticGetter<ISleekToggle>? GetSnowToggle = Accessor.GenerateStaticGetter<EditorEnvironmentLightingUI, ISleekToggle>("snowToggle");
    private static readonly StaticGetter<ISleekSlider>? GetMoonSlider = Accessor.GenerateStaticGetter<EditorEnvironmentLightingUI, ISleekSlider>("moonSlider");

    internal static void DoPatching()
    {
        Type uiType = typeof(EditorEnvironmentLightingUI);

        _onDraggedAzimuthSlider = OptionalPatches.Prefix(Source,
            uiType.GetMethod("onDraggedAzimuthSlider", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance),
            Accessor.GetMethod(OnDraggedAzimuthSlider)!,
            () => FormattingUtil.FormatMethod<Dragged>("onDraggedAzimuthSlider", isStatic: true, declTypeOverride: uiType),
            "Azimuth lighting adjustments will not be synced.");

        _onDraggedBiasSlider = OptionalPatches.Prefix(Source,
            uiType.GetMethod("onDraggedBiasSlider", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance),
            Accessor.GetMethod(OnDraggedBiasSlider)!,
            () => FormattingUtil.FormatMethod<Dragged>("onDraggedBiasSlider", isStatic: true, declTypeOverride: uiType),
            "Bias lighting adjustments will not be synced.");

        _onDraggedFadeSlider = OptionalPatches.Prefix(Source,
            uiType.GetMethod("onDraggedFadeSlider", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance),
            Accessor.GetMethod(OnDraggedFadeSlider)!,
            () => FormattingUtil.FormatMethod<Dragged>("onDraggedFadeSlider", isStatic: true, declTypeOverride: uiType),
            "Fade lighting adjustments will not be synced.");

        _onValuedSeaLevelSlider = OptionalPatches.Prefix(Source,
            uiType.GetMethod("onValuedSeaLevelSlider", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance),
            Accessor.GetMethod(OnValuedSeaLevelSlider)!,
            () => FormattingUtil.FormatMethod<Valued>("onValuedSeaLevelSlider", isStatic: true, declTypeOverride: uiType),
            "SeaLevel lighting adjustments will not be synced.");

        _onValuedSnowLevelSlider = OptionalPatches.Prefix(Source,
            uiType.GetMethod("onValuedSnowLevelSlider", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance),
            Accessor.GetMethod(OnValuedSnowLevelSlider)!,
            () => FormattingUtil.FormatMethod<Valued>("onValuedSnowLevelSlider", isStatic: true, declTypeOverride: uiType),
            "SnowLevel lighting adjustments will not be synced.");

        _onToggledRainToggle = OptionalPatches.Prefix(Source,
            uiType.GetMethod("onToggledRainToggle", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance),
            Accessor.GetMethod(OnToggledRainToggle)!,
            () => FormattingUtil.FormatMethod<Toggled>("onToggledRainToggle", isStatic: true, declTypeOverride: uiType),
            "Rain enabling and disabling will not be synced.");

        _onToggledSnowToggle = OptionalPatches.Prefix(Source,
            uiType.GetMethod("onToggledSnowToggle", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance),
            Accessor.GetMethod(OnToggledSnowToggle)!,
            () => FormattingUtil.FormatMethod<Toggled>("onToggledSnowToggle", isStatic: true, declTypeOverride: uiType),
            "Snow enabling and disabling will not be synced.");

        _onTypedRainFreqField = OptionalPatches.Prefix(Source,
            uiType.GetMethod("onTypedRainFreqField", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance),
            Accessor.GetMethod(OnTypedRainFreqField)!,
            () => FormattingUtil.FormatMethod<TypedSingle>("onTypedRainFreqField", isStatic: true, declTypeOverride: uiType),
            "Rain frequency adjustments will not be synced.");

        _onTypedRainDurField = OptionalPatches.Prefix(Source,
            uiType.GetMethod("onTypedRainDurField", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance),
            Accessor.GetMethod(OnTypedRainDurField)!,
            () => FormattingUtil.FormatMethod<TypedSingle>("onTypedRainDurField", isStatic: true, declTypeOverride: uiType),
            "Rain duration adjustments will not be synced.");

        _onTypedSnowFreqField = OptionalPatches.Prefix(Source,
            uiType.GetMethod("onTypedSnowFreqField", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance),
            Accessor.GetMethod(OnTypedSnowFreqField)!,
            () => FormattingUtil.FormatMethod<TypedSingle>("onTypedSnowFreqField", isStatic: true, declTypeOverride: uiType),
            "Snow frequency adjustments will not be synced.");

        _onTypedSnowDurField = OptionalPatches.Prefix(Source,
            uiType.GetMethod("onTypedSnowDurField", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance),
            Accessor.GetMethod(OnTypedSnowDurField)!,
            () => FormattingUtil.FormatMethod<TypedSingle>("onTypedSnowDurField", isStatic: true, declTypeOverride: uiType),
            "Snow duration adjustments will not be synced.");

        _onDraggedMoonSlider = OptionalPatches.Prefix(Source,
            uiType.GetMethod("onDraggedMoonSlider", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance),
            Accessor.GetMethod(OnDraggedMoonSlider)!,
            () => FormattingUtil.FormatMethod<Dragged>("onDraggedMoonSlider", isStatic: true, declTypeOverride: uiType),
            "Moon stage adjustments will not be synced.");

        _onDraggedTimeSlider = OptionalPatches.Prefix(Source,
            uiType.GetMethod("onDraggedTimeSlider", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance),
            Accessor.GetMethod(OnDraggedTimeSlider)!,
            () => FormattingUtil.FormatMethod<Dragged>("onDraggedTimeSlider", isStatic: true, declTypeOverride: uiType),
            "Time adjustments will not be synced.");

        _onClickedTimeButton = OptionalPatches.Prefix(Source,
            uiType.GetMethod("onClickedTimeButton", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance),
            Accessor.GetMethod(OnClickedTimeButton)!,
            () => FormattingUtil.FormatMethod<ClickedButton>("onClickedTimeButton", isStatic: true, declTypeOverride: uiType),
            "Time stage adjustments will not be synced.");

        _onClickedPreviewWeather = OptionalPatches.Prefix(Source,
            uiType.GetMethod("OnClickedPreviewWeather", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance),
            Accessor.GetMethod(OnClickedPreviewWeather)!,
            () => FormattingUtil.FormatMethod<ClickedButton>("OnClickedPreviewWeather", isStatic: true, declTypeOverride: uiType),
            "Weather previews will not be synced.");

        _onPickedColorPicker = OptionalPatches.Prefix(Source,
            uiType.GetMethod("onPickedColorPicker", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance),
            Accessor.GetMethod(OnPickedColorPicker)!,
            () => FormattingUtil.FormatMethod<ColorPicked>("onPickedColorPicker", isStatic: true, declTypeOverride: uiType),
            "Time light color adjustments will not be synced.");

        _onDraggedSingleSlider = OptionalPatches.Prefix(Source,
            uiType.GetMethod("onDraggedSingleSlider", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance),
            Accessor.GetMethod(OnDraggedSingleSlider)!,
            () => FormattingUtil.FormatMethod<Dragged>("onDraggedSingleSlider", isStatic: true, declTypeOverride: uiType),
            "Time light value adjustments will not be synced.");
    }
    internal static void DoUnpatching()
    {
        OptionalPatches.Unpatch(ref _onDraggedAzimuthSlider);
        OptionalPatches.Unpatch(ref _onDraggedAzimuthSlider);
        OptionalPatches.Unpatch(ref _onDraggedBiasSlider);
        OptionalPatches.Unpatch(ref _onDraggedFadeSlider);
        OptionalPatches.Unpatch(ref _onValuedSeaLevelSlider);
        OptionalPatches.Unpatch(ref _onValuedSnowLevelSlider);
        OptionalPatches.Unpatch(ref _onToggledRainToggle);
        OptionalPatches.Unpatch(ref _onToggledSnowToggle);
        OptionalPatches.Unpatch(ref _onTypedRainFreqField);
        OptionalPatches.Unpatch(ref _onTypedRainDurField);
        OptionalPatches.Unpatch(ref _onTypedSnowFreqField);
        OptionalPatches.Unpatch(ref _onTypedSnowDurField);
        OptionalPatches.Unpatch(ref _onDraggedMoonSlider);
        OptionalPatches.Unpatch(ref _onDraggedTimeSlider);
        OptionalPatches.Unpatch(ref _onClickedTimeButton);
        OptionalPatches.Unpatch(ref _onClickedPreviewWeather);
        OptionalPatches.Unpatch(ref _onPickedColorPicker);
        OptionalPatches.Unpatch(ref _onDraggedSingleSlider);
    }
    private static bool OnDraggedAzimuthSlider(ISleekSlider slider, float state)
    {
        if (!VanillaPermissions.EditLighting.Has())
        {
            slider.Value = LevelLighting.azimuth / 360f;
            EditorMessage.SendNoPermissionMessage(VanillaPermissions.EditLighting);
        }

        float old = LevelLighting.azimuth;
        float azimuth = state * 360f;
        if (old == state)
            return false;

        SetLightingFloatProperties properties = new SetLightingFloatProperties(LightingValue.Azimuth, azimuth, CachedTime.DeltaTime);

        if (DevkitServerModule.IsEditing)
        {
            bool shouldAllow = true;
            ClientEvents.InvokeOnSetLightingFloatRequested(in properties, ref shouldAllow);
            if (!shouldAllow)
            {
                slider.Value = LevelLighting.azimuth / 360f;
                return false;
            }
        }

        LevelLighting.azimuth = azimuth;

        Logger.DevkitServer.LogDebug(nameof(LightingPatches), $"Azimuth updated: {old.Format()} -> {LevelLighting.azimuth.Format()}.");

        if (DevkitServerModule.IsEditing)
            ClientEvents.InvokeOnSetLightingFloat(in properties);

        return false;
    }
    private static bool OnDraggedBiasSlider(ISleekSlider slider, float state)
    {
        if (!VanillaPermissions.EditLighting.Has())
        {
            slider.Value = LevelLighting.bias;
            EditorMessage.SendNoPermissionMessage(VanillaPermissions.EditLighting);
        }

        float old = LevelLighting.bias;
        if (old == state)
            return false;

        SetLightingFloatProperties properties = new SetLightingFloatProperties(LightingValue.Bias, state, CachedTime.DeltaTime);

        if (DevkitServerModule.IsEditing)
        {
            bool shouldAllow = true;
            ClientEvents.InvokeOnSetLightingFloatRequested(in properties, ref shouldAllow);
            if (!shouldAllow)
            {
                slider.Value = LevelLighting.bias;
                return false;
            }
        }

        LevelLighting.bias = state;

        Logger.DevkitServer.LogDebug(nameof(LightingPatches), $"Bias updated: {old.Format()} -> {LevelLighting.bias.Format()}.");

        if (DevkitServerModule.IsEditing)
            ClientEvents.InvokeOnSetLightingFloat(in properties);

        return false;
    }
    private static bool OnDraggedFadeSlider(ISleekSlider slider, float state)
    {
        if (!VanillaPermissions.EditLighting.Has())
        {
            slider.Value = LevelLighting.fade;
            EditorMessage.SendNoPermissionMessage(VanillaPermissions.EditLighting);
        }

        float old = LevelLighting.fade;
        if (old == state)
            return false;

        SetLightingFloatProperties properties = new SetLightingFloatProperties(LightingValue.Fade, state, CachedTime.DeltaTime);

        if (DevkitServerModule.IsEditing)
        {
            bool shouldAllow = true;
            ClientEvents.InvokeOnSetLightingFloatRequested(in properties, ref shouldAllow);
            if (!shouldAllow)
            {
                slider.Value = LevelLighting.fade;
                return false;
            }
        }

        LevelLighting.fade = state;

        Logger.DevkitServer.LogDebug(nameof(LightingPatches), $"Fade updated: {old.Format()} -> {LevelLighting.fade.Format()}.");

        if (DevkitServerModule.IsEditing)
            ClientEvents.InvokeOnSetLightingFloat(in properties);

        return false;
    }
    private static bool OnValuedSeaLevelSlider(SleekValue slider, float state)
    {
        if (!VanillaPermissions.EditLighting.Has())
        {
            slider.state = LevelLighting.seaLevel;
            EditorMessage.SendNoPermissionMessage(VanillaPermissions.EditLighting);
        }

        float old = LevelLighting.seaLevel;
        if (old == state)
            return false;

        SetLightingFloatProperties properties = new SetLightingFloatProperties(LightingValue.SeaLevel, state, CachedTime.DeltaTime);

        if (DevkitServerModule.IsEditing)
        {
            bool shouldAllow = true;
            ClientEvents.InvokeOnSetLightingFloatRequested(in properties, ref shouldAllow);
            if (!shouldAllow)
            {
                slider.state = LevelLighting.seaLevel;
                return false;
            }
        }

        LevelLighting.seaLevel = state;

        Logger.DevkitServer.LogDebug(nameof(LightingPatches), $"Sea level updated: {old.Format()} -> {LevelLighting.seaLevel.Format()}.");

        if (DevkitServerModule.IsEditing)
            ClientEvents.InvokeOnSetLightingFloat(in properties);

        return false;
    }
    private static bool OnValuedSnowLevelSlider(SleekValue slider, float state)
    {
        if (!VanillaPermissions.EditLighting.Has())
        {
            slider.state = LevelLighting.snowLevel;
            EditorMessage.SendNoPermissionMessage(VanillaPermissions.EditLighting);
        }

        float old = LevelLighting.snowLevel;
        if (old == state)
            return false;

        SetLightingFloatProperties properties = new SetLightingFloatProperties(LightingValue.SnowLevel, state, CachedTime.DeltaTime);

        if (DevkitServerModule.IsEditing)
        {
            bool shouldAllow = true;
            ClientEvents.InvokeOnSetLightingFloatRequested(in properties, ref shouldAllow);
            if (!shouldAllow)
            {
                slider.state = LevelLighting.snowLevel;
                return false;
            }
        }

        LevelLighting.snowLevel = state;

        Logger.DevkitServer.LogDebug(nameof(LightingPatches), $"Snow level updated: {old.Format()} -> {LevelLighting.snowLevel.Format()}.");

        if (DevkitServerModule.IsEditing)
            ClientEvents.InvokeOnSetLightingFloat(in properties);

        return false;
    }
    private static bool OnToggledRainToggle(ISleekToggle toggle, bool state)
    {
        if (!VanillaPermissions.EditLighting.Has())
        {
            toggle.Value = LevelLighting.canRain;
            EditorMessage.SendNoPermissionMessage(VanillaPermissions.EditLighting);
        }

        bool old = LevelLighting.canRain;
        if (old == state)
            return false;

        SetLightingByteProperties properties = new SetLightingByteProperties(LightingValue.HasRain, state ? (byte)1 : (byte)0, CachedTime.DeltaTime);

        if (DevkitServerModule.IsEditing)
        {
            bool shouldAllow = true;
            ClientEvents.InvokeOnSetLightingByteRequested(in properties, ref shouldAllow);
            if (!shouldAllow)
            {
                toggle.Value = LevelLighting.canRain;
                return false;
            }
        }

        LevelLighting.canRain = state;

        Logger.DevkitServer.LogDebug(nameof(LightingPatches), $"Rain enable updated: {old.Format()} -> {LevelLighting.canRain.Format()}.");

        if (DevkitServerModule.IsEditing)
            ClientEvents.InvokeOnSetLightingByte(in properties);

        return false;
    }
    private static bool OnToggledSnowToggle(ISleekToggle toggle, bool state)
    {
        if (!VanillaPermissions.EditLighting.Has())
        {
            toggle.Value = LevelLighting.canSnow;
            EditorMessage.SendNoPermissionMessage(VanillaPermissions.EditLighting);
        }

        bool old = LevelLighting.canSnow;
        if (old == state)
            return false;

        SetLightingByteProperties properties = new SetLightingByteProperties(LightingValue.HasSnow, state ? (byte)1 : (byte)0, CachedTime.DeltaTime);

        if (DevkitServerModule.IsEditing)
        {
            bool shouldAllow = true;
            ClientEvents.InvokeOnSetLightingByteRequested(in properties, ref shouldAllow);
            if (!shouldAllow)
            {
                toggle.Value = LevelLighting.canSnow;
                return false;
            }
        }

        LevelLighting.canSnow = state;

        Logger.DevkitServer.LogDebug(nameof(LightingPatches), $"Snow enable updated: {old.Format()} -> {LevelLighting.canSnow.Format()}.");

        if (DevkitServerModule.IsEditing)
            ClientEvents.InvokeOnSetLightingByte(in properties);

        return false;
    }
    private static bool OnTypedRainFreqField(ISleekFloat32Field field, float state)
    {
        if (!VanillaPermissions.EditLighting.Has())
        {
            field.Value = LevelLighting.rainFreq;
            EditorMessage.SendNoPermissionMessage(VanillaPermissions.EditLighting);
        }

        float old = LevelLighting.rainFreq;
        if (old == state)
            return false;

        SetLightingFloatProperties properties = new SetLightingFloatProperties(LightingValue.RainFrequency, state, CachedTime.DeltaTime);

        if (DevkitServerModule.IsEditing)
        {
            bool shouldAllow = true;
            ClientEvents.InvokeOnSetLightingFloatRequested(in properties, ref shouldAllow);
            if (!shouldAllow)
            {
                field.Value = LevelLighting.rainFreq;
                return false;
            }
        }

        LevelLighting.rainFreq = state;

        Logger.DevkitServer.LogDebug(nameof(LightingPatches), $"Rain frequency updated: {old.Format()} -> {LevelLighting.rainFreq.Format()}.");

        if (DevkitServerModule.IsEditing)
            ClientEvents.InvokeOnSetLightingFloat(in properties);

        return false;
    }
    private static bool OnTypedRainDurField(ISleekFloat32Field field, float state)
    {
        if (!VanillaPermissions.EditLighting.Has())
        {
            field.Value = LevelLighting.rainDur;
            EditorMessage.SendNoPermissionMessage(VanillaPermissions.EditLighting);
        }

        float old = LevelLighting.rainDur;
        if (old == state)
            return false;

        SetLightingFloatProperties properties = new SetLightingFloatProperties(LightingValue.RainDuration, state, CachedTime.DeltaTime);

        if (DevkitServerModule.IsEditing)
        {
            bool shouldAllow = true;
            ClientEvents.InvokeOnSetLightingFloatRequested(in properties, ref shouldAllow);
            if (!shouldAllow)
            {
                field.Value = LevelLighting.rainDur;
                return false;
            }
        }

        LevelLighting.rainDur = state;

        Logger.DevkitServer.LogDebug(nameof(LightingPatches), $"Rain duration updated: {old.Format()} -> {LevelLighting.rainDur.Format()}.");

        if (DevkitServerModule.IsEditing)
            ClientEvents.InvokeOnSetLightingFloat(in properties);

        return false;
    }
    private static bool OnTypedSnowFreqField(ISleekFloat32Field field, float state)
    {
        if (!VanillaPermissions.EditLighting.Has())
        {
            field.Value = LevelLighting.snowFreq;
            EditorMessage.SendNoPermissionMessage(VanillaPermissions.EditLighting);
        }

        float old = LevelLighting.snowFreq;
        if (old == state)
            return false;

        SetLightingFloatProperties properties = new SetLightingFloatProperties(LightingValue.SnowFrequency, state, CachedTime.DeltaTime);

        if (DevkitServerModule.IsEditing)
        {
            bool shouldAllow = true;
            ClientEvents.InvokeOnSetLightingFloatRequested(in properties, ref shouldAllow);
            if (!shouldAllow)
            {
                field.Value = LevelLighting.snowFreq;
                return false;
            }
        }

        LevelLighting.snowFreq = state;

        Logger.DevkitServer.LogDebug(nameof(LightingPatches), $"Snow frequency updated: {old.Format()} -> {LevelLighting.snowFreq.Format()}.");

        if (DevkitServerModule.IsEditing)
            ClientEvents.InvokeOnSetLightingFloat(in properties);

        return false;
    }
    private static bool OnTypedSnowDurField(ISleekFloat32Field field, float state)
    {
        if (!VanillaPermissions.EditLighting.Has())
        {
            field.Value = LevelLighting.snowDur;
            EditorMessage.SendNoPermissionMessage(VanillaPermissions.EditLighting);
        }

        float old = LevelLighting.snowDur;
        if (old == state)
            return false;

        SetLightingFloatProperties properties = new SetLightingFloatProperties(LightingValue.SnowDuration, state, CachedTime.DeltaTime);

        if (DevkitServerModule.IsEditing)
        {
            bool shouldAllow = true;
            ClientEvents.InvokeOnSetLightingFloatRequested(in properties, ref shouldAllow);
            if (!shouldAllow)
            {
                field.Value = LevelLighting.snowDur;
                return false;
            }
        }

        LevelLighting.snowDur = state;

        Logger.DevkitServer.LogDebug(nameof(LightingPatches), $"Snow duration updated: {old.Format()} -> {LevelLighting.snowDur.Format()}.");

        if (DevkitServerModule.IsEditing)
            ClientEvents.InvokeOnSetLightingFloat(in properties);

        return false;
    }
    private static bool OnDraggedMoonSlider(ISleekSlider slider, float state)
    {
        bool hasReplicationPerms = ClientInfo.Info is not { ServerSyncsEditorTime: false } && VanillaPermissions.EditLighting.Has();

        byte moonCycle = (byte)(state * LevelLighting.MOON_CYCLES);
        if (moonCycle >= LevelLighting.MOON_CYCLES)
            moonCycle = (byte)(LevelLighting.MOON_CYCLES - 1);

        byte old = LevelLighting.moon;
        if (old == moonCycle)
        {
            slider.Value = (float)moonCycle / LevelLighting.MOON_CYCLES;
            return false;
        }

        SetLightingByteProperties properties = new SetLightingByteProperties(LightingValue.Moon, moonCycle, CachedTime.DeltaTime);

        if (DevkitServerModule.IsEditing)
        {
            bool shouldAllow = true;
            ClientEvents.InvokeOnSetLightingByteRequested(in properties, ref shouldAllow);
            if (!shouldAllow)
            {
                slider.Value = (float)LevelLighting.moon / LevelLighting.MOON_CYCLES;
                return false;
            }
        }

        LevelLighting.moon = moonCycle;
        slider.Value = (float)moonCycle / LevelLighting.MOON_CYCLES;

        Logger.DevkitServer.LogDebug(nameof(LightingPatches), $"Moon cycle updated: {old.Format()} -> {moonCycle.Format()}.");

        if (hasReplicationPerms && DevkitServerModule.IsEditing)
            ClientEvents.InvokeOnSetLightingByte(in properties);

        return false;
    }
    private static bool OnDraggedTimeSlider(ISleekSlider slider, float state)
    {
        bool hasReplicationPerms = ClientInfo.Info is not { ServerSyncsEditorTime: false } && VanillaPermissions.EditLighting.Has();

        float old = LevelLighting.time;
        if (old == state)
            return false;

        SetLightingFloatProperties properties = new SetLightingFloatProperties(LightingValue.Time, state, CachedTime.DeltaTime);

        if (DevkitServerModule.IsEditing)
        {
            bool shouldAllow = true;
            ClientEvents.InvokeOnSetLightingFloatRequested(in properties, ref shouldAllow);
            if (!shouldAllow)
            {
                slider.Value = LevelLighting.time;
                return false;
            }
        }

        LevelLighting.time = state;

        Logger.DevkitServer.LogDebug(nameof(LightingPatches), $"Time updated: {old.Format()} -> {state.Format()}.");

        if (hasReplicationPerms && DevkitServerModule.IsEditing)
            ClientEvents.InvokeOnSetLightingFloat(in properties);

        return false;
    }
    private static bool OnClickedTimeButton(ISleekElement button)
    {
        ISleekButton[]? timeButtons = GetTimeButtons?.Invoke();
        if (timeButtons == null)
        {
            Logger.DevkitServer.LogWarning(nameof(OnClickedTimeButton), "Unable to replicate time button press - failed to get timeButtons.");
            return true;
        }

        int index = Array.IndexOf(timeButtons, (ISleekButton)button);
        if (index == -1)
        {
            Logger.DevkitServer.LogWarning(nameof(OnClickedTimeButton), "Unable to replicate time button press - failed to find index of button.");
            return false;
        }

        bool hasReplicationPerms = ClientInfo.Info is not { ServerSyncsEditorTime: false } && VanillaPermissions.EditLighting.Has();

        LightingUtil.TryGetSelectedLightingTime(out ELightingTime oldSelection);

        ELightingTime lighting = (ELightingTime)index;

        if (!LightingUtil.TrySetSelectedLightingTime(lighting))
        {
            Logger.DevkitServer.LogWarning(nameof(OnClickedTimeButton), "Unable to replicate time button press - failed to set lighting time.");
            return true;
        }

        float time = (ELightingTime)index switch
        {
            ELightingTime.DAWN => 0.0f,
            ELightingTime.MIDDAY => LevelLighting.bias / 2f,
            ELightingTime.DUSK => LevelLighting.bias,
            ELightingTime.MIDNIGHT => 1f - (1f - LevelLighting.bias) / 2f,
            _ => LevelLighting.time
        };

        float old = LevelLighting.time;
        if (old == time)
            return false;

        SetLightingFloatProperties properties = new SetLightingFloatProperties(LightingValue.Time, time, CachedTime.DeltaTime);

        if (DevkitServerModule.IsEditing)
        {
            bool shouldAllow = true;
            ClientEvents.InvokeOnSetLightingFloatRequested(in properties, ref shouldAllow);
            if (!shouldAllow)
                return false;
        }

        LevelLighting.time = time;

        Logger.DevkitServer.LogDebug(nameof(LightingPatches), $"Selected time updated: {old.Format()} -> {LevelLighting.time.Format()}, {oldSelection.Format()} -> {lighting.Format()}.");

        ISleekSlider? timeSlider = GetTimeSlider?.Invoke();
        if (timeSlider != null)
            timeSlider.Value = LevelLighting.time;

        if (hasReplicationPerms && DevkitServerModule.IsEditing)
            ClientEvents.InvokeOnSetLightingFloat(in properties);

        return false;
    }
    private static bool OnClickedPreviewWeather(ISleekElement button)
    {
        bool hasReplicationPerms = ClientInfo.Info is not { ServerSyncsEditorWeather: false } && VanillaPermissions.EditLighting.Has();

        ISleekField? guidField = GetWeatherGuidField?.Invoke();
        if (guidField == null)
        {
            Logger.DevkitServer.LogWarning(nameof(OnClickedTimeButton), "Unable to replicate weather preview - failed to get weatherGuidField.");
            return true;
        }

        WeatherAssetBase? asset = !Guid.TryParse(guidField.Text, out Guid result)
            ? null
            : Assets.find<WeatherAssetBase>(result);

        WeatherAssetBase? old = LevelLighting.GetActiveWeatherAsset();
        if (old == asset)
            return false;

        SetPreviewWeatherAssetProperties properties = new SetPreviewWeatherAssetProperties(asset?.getReferenceTo<WeatherAssetBase>() ?? AssetReference<WeatherAssetBase>.invalid, CachedTime.DeltaTime);

        if (DevkitServerModule.IsEditing)
        {
            bool shouldAllow = true;
            ClientEvents.InvokeOnSetPreviewWeatherAssetRequested(in properties, ref shouldAllow);
            if (!shouldAllow)
                return false;
        }

        LightingManager.ReceiveLightingActiveWeather(asset?.GUID ?? Guid.Empty, 0f, default);

        Logger.DevkitServer.LogDebug(nameof(LightingPatches), $"Previewed weather asset updated: {old.Format()} -> {asset.Format()}.");

        if (hasReplicationPerms && DevkitServerModule.IsEditing)
            ClientEvents.InvokeOnSetPreviewWeatherAsset(in properties);

        return false;
    }
    private static bool OnPickedColorPicker(SleekColorPicker picker, Color state)
    {
        SleekColorPicker[]? colorPickers = GetColorPickers?.Invoke();
        if (colorPickers == null)
        {
            Logger.DevkitServer.LogWarning(nameof(OnPickedColorPicker), "Unable to replicate time color change - failed to get colorPickers.");
            return true;
        }

        if (!LightingUtil.TryGetSelectedLightingTime(out ELightingTime time))
        {
            Logger.DevkitServer.LogWarning(nameof(OnPickedColorPicker), "Unable to replicate time color change - failed to get lighting time.");
            return true;
        }

        int index = Array.IndexOf(colorPickers, picker);
        if (index == -1)
        {
            Logger.DevkitServer.LogWarning(nameof(OnPickedColorPicker), "Unable to replicate time color change - failed to find index of color picker.");
            return false;
        }

        ref Color value = ref LevelLighting.times[(int)time].colors[index];

        if (!VanillaPermissions.EditLighting.Has())
        {
            picker.state = value;
            EditorMessage.SendNoPermissionMessage(VanillaPermissions.EditLighting);
        }

        Color old = value;
        if (old == state)
            return false;

        SetTimeColorProperties properties = new SetTimeColorProperties(time, (byte)index, state, CachedTime.DeltaTime);

        if (DevkitServerModule.IsEditing)
        {
            bool shouldAllow = true;
            ClientEvents.InvokeOnSetTimeColorRequested(in properties, ref shouldAllow);
            if (!shouldAllow)
            {
                picker.state = old;
                return false;
            }
        }

        value = state;

        Logger.DevkitServer.LogDebug(nameof(LightingPatches), $"{LightingUtil.GetTimeColorDisplayString(index)} color of {time.Format()} updated: {old.Format()} -> {state.Format()}.");

        LevelLighting.updateLighting();

        if (DevkitServerModule.IsEditing)
            ClientEvents.InvokeOnSetTimeColor(in properties);

        return false;
    }
    private static bool OnDraggedSingleSlider(ISleekSlider slider, float state)
    {
        ISleekSlider[]? singleSliders = GetSingleSliders?.Invoke();
        if (singleSliders == null)
        {
            Logger.DevkitServer.LogWarning(nameof(OnDraggedSingleSlider), "Unable to replicate time value change - failed to get singleSliders.");
            return true;
        }

        if (!LightingUtil.TryGetSelectedLightingTime(out ELightingTime time))
        {
            Logger.DevkitServer.LogWarning(nameof(OnDraggedSingleSlider), "Unable to replicate time value change - failed to get lighting time.");
            return true;
        }

        int index = Array.IndexOf(singleSliders, slider);
        if (index == -1)
        {
            Logger.DevkitServer.LogWarning(nameof(OnDraggedSingleSlider), "Unable to replicate time value change - failed to find index of slider.");
            return false;
        }

        ref float value = ref LevelLighting.times[(int)time].singles[index];

        if (!VanillaPermissions.EditLighting.Has())
        {
            slider.Value = value;
            EditorMessage.SendNoPermissionMessage(VanillaPermissions.EditLighting);
        }

        float old = value;
        if (old == state)
            return false;

        SetTimeSingleProperties properties = new SetTimeSingleProperties(time, (byte)index, state, CachedTime.DeltaTime);

        if (DevkitServerModule.IsEditing)
        {
            bool shouldAllow = true;
            ClientEvents.InvokeOnSetTimeSingleRequested(in properties, ref shouldAllow);
            if (!shouldAllow)
            {
                slider.Value = old;
                return false;
            }
        }

        value = state;

        Logger.DevkitServer.LogDebug(nameof(LightingPatches), $"{LightingUtil.GetTimeSingleDisplayString(index)} of {time.Format()} updated: {old.Format()} -> {state.Format()}.");

        LevelLighting.updateLighting();

        if (DevkitServerModule.IsEditing)
            ClientEvents.InvokeOnSetTimeSingle(in properties);

        return false;
    }

    public static void UpdateAzimuth()
    {
        ISleekSlider? slider = GetAzimuthSlider?.Invoke();
        if (slider != null)
            slider.Value = LevelLighting.azimuth / 360f;
    }
    public static void UpdateBias()
    {
        ISleekSlider? slider = GetBiasSlider?.Invoke();
        if (slider != null)
            slider.Value = LevelLighting.bias;
    }
    public static void UpdateFade()
    {
        ISleekSlider? slider = GetFadeSlider?.Invoke();
        if (slider != null)
            slider.Value = LevelLighting.fade;
    }
    public static void UpdateRainDuration()
    {
        ISleekFloat32Field? slider = GetRainDurField?.Invoke();
        if (slider != null)
            slider.Value = LevelLighting.rainDur;
    }
    public static void UpdateRainFrequency()
    {
        ISleekFloat32Field? slider = GetRainFreqField?.Invoke();
        if (slider != null)
            slider.Value = LevelLighting.rainFreq;
    }
    public static void UpdateSnowDuration()
    {
        ISleekFloat32Field? slider = GetSnowDurField?.Invoke();
        if (slider != null)
            slider.Value = LevelLighting.snowDur;
    }
    public static void UpdateSnowFrequency()
    {
        ISleekFloat32Field? slider = GetSnowFreqField?.Invoke();
        if (slider != null)
            slider.Value = LevelLighting.snowFreq;
    }
    public static void UpdateSeaLevel()
    {
        SleekValue? slider = GetSeaLevelSlider?.Invoke();
        if (slider != null)
            slider.state = LevelLighting.seaLevel;
    }
    public static void UpdateSnowLevel()
    {
        SleekValue? slider = GetSnowLevelSlider?.Invoke();
        if (slider != null)
            slider.state = LevelLighting.snowLevel;
    }
    public static void UpdateTime()
    {
        ISleekSlider? slider = GetTimeSlider?.Invoke();
        if (slider != null)
            slider.Value = LevelLighting.time;
    }
    public static void UpdateMoonCycle()
    {
        ISleekSlider? slider = GetMoonSlider?.Invoke();
        if (slider != null)
            slider.Value = (float)LevelLighting.moon / LevelLighting.MOON_CYCLES;
    }
    public static void UpdateCanRain()
    {
        ISleekToggle? toggle = GetRainToggle?.Invoke();
        if (toggle != null)
            toggle.Value = LevelLighting.canRain;
    }
    public static void UpdateCanSnow()
    {
        ISleekToggle? toggle = GetSnowToggle?.Invoke();
        if (toggle != null)
            toggle.Value = LevelLighting.canSnow;
    }
    public static void UpdateWeatherAsset()
    {
        ISleekField? field = GetWeatherGuidField?.Invoke();
        if (field != null)
            field.Text = LevelLighting.GetActiveWeatherAsset()?.GUID.ToString("N") ?? string.Empty;
    }
    public static void UpdateTimeColor(ELightingTime time, int index)
    {
        if (!LightingUtil.TryGetSelectedLightingTime(out ELightingTime currentTime) || time != currentTime)
            return;

        SleekColorPicker[]? colorPickers = GetColorPickers?.Invoke();

        if (colorPickers == null || index >= colorPickers.Length)
            return;

        colorPickers[index].state = LevelLighting.times[(int)time].colors[index];
    }
    public static void UpdateTimeSingle(ELightingTime time, int index)
    {
        if (!LightingUtil.TryGetSelectedLightingTime(out ELightingTime currentTime) || time != currentTime)
            return;

        ISleekSlider[]? sliders = GetSingleSliders?.Invoke();

        if (sliders == null || index >= sliders.Length)
            return;

        sliders[index].Value = LevelLighting.times[(int)time].singles[index];
    }
}
#endif