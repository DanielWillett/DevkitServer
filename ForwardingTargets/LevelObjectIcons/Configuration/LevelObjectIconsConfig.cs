using UnityEngine;

namespace DanielWillett.LevelObjectIcons.Configuration;
public class LevelObjectIconsConfig
{
    public string SchemaURI => "https://raw.githubusercontent.com/DanielWillett/LevelObjectIcons/master/Schemas/level_object_icons_config_schema.json";
    public KeyCode EditKeybind { get; set; } = KeyCode.F8;
    public KeyCode LogMissingKeybind { get; set; } = KeyCode.Keypad5;
    public bool ShouldCycleMaterialPalette { get; set; } = true;
    public bool EnableDebugLogging { get; set; }
    public bool DisableDefaultProviderSearch { get; set; }
}