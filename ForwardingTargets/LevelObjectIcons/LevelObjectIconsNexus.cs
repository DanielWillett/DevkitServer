using DanielWillett.LevelObjectIcons.Configuration;
using DevkitServer;
using DevkitServer.Configuration;
using DevkitServer.Util;
using HarmonyLib;
using SDG.Framework.Modules;
using SDG.Unturned;
using System;
using DevkitServer.API;
using UnityEngine;

namespace DanielWillett.LevelObjectIcons;

public sealed class LevelObjectIconsNexus : IModuleNexus
{
    public static LevelObjectIconsConfig Config => new LevelObjectIconsConfig
    {
        EditKeybind = DevkitServerConfig.Config.LevelObjectEditKeybind,
        DisableDefaultProviderSearch = DevkitServerConfig.Config.DisableDefaultLevelObjectIconProviderSearch,
        LogMissingKeybind = DevkitServerConfig.Config.LogMissingLevelObjectKeybind,
        EnableDebugLogging = DevkitServerModule.IsDebug,
        ShouldCycleMaterialPalette = DevkitServerConfig.Config.ShouldCycleLevelObjectMaterialPalette
    };
    public static Asset? SelectedAsset => LevelObjectUtil.SelectedAsset;
    public static Local Localization = DevkitServerModule.MainLocalization;
    public static Harmony Patcher => AccessorExtensions.DevkitServerModulePatcher;
    public static GameObject GameObjectHost => DevkitServerModule.GameObjectHost;
    public static void SaveConfig() => DevkitServerConfig.Save();
    public static void ReloadConfig() => DevkitServerConfig.Reload();
    public static void ReloadTranslations() => DevkitServerModule.ReloadMainLocalization();
    void IModuleNexus.initialize() => throw new NotImplementedException();
    void IModuleNexus.shutdown() => throw new NotImplementedException();
}