using DevkitServer.Configuration;
using DevkitServer.Util.Encoding;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevkitServer.Core.Cartography;
public class LevelCartographyConfigData : SchemaConfiguration
{
    protected override string GetSchemaURI() => DevkitServerModule.GetRelativeRepositoryUrl("Module/Schemas/cartography_config.json", true);

    [JsonPropertyName("override_chart_color_provider")]
    public string? PreferredChartColorProvider { get; set; }

    [JsonPropertyName("override_active_compositors")]
    public string[]? ActiveCompositors { get; set; }

    public static LevelCartographyConfigData? ReadFromLevel(LevelInfo? info)
    {
        info ??= Level.info;

        string levelDir = info.path;

        string file = Path.Combine(levelDir, "Editor", "cartography_config.json");
        if (File.Exists(file))
            return Read(file);
        
        file = Path.Combine(levelDir, "Editor", "cartography.json");
        if (File.Exists(file))
            return Read(file);

        file = Path.Combine(levelDir, "Chart", "cartography_config.json");
        if (File.Exists(file))
            return Read(file);
        
        file = Path.Combine(levelDir, "Chart", "cartography.json");
        if (File.Exists(file))
            return Read(file);

        file = Path.Combine(levelDir, "Terrain", "cartography_config.json");
        if (File.Exists(file))
            return Read(file);
        
        file = Path.Combine(levelDir, "Terrain", "cartography.json");
        if (File.Exists(file))
            return Read(file);

        file = Path.Combine(levelDir, "cartography_config.json");
        if (File.Exists(file))
            return Read(file);
        
        file = Path.Combine(levelDir, "cartography.json");
        if (File.Exists(file))
            return Read(file);

        Logger.LogInfo($"No 'cartography_config.json' is present in {info.getLocalizedName().Format(false)}.");
        return null;
    }

    private static LevelCartographyConfigData? Read(string file)
    {
        LevelCartographyConfigData? data;
        try
        {
            ArraySegment<byte> bytes;

            using (Utf8JsonPreProcessingStream stream = new Utf8JsonPreProcessingStream(file))
                bytes = stream.ReadAllBytes();

            Utf8JsonReader reader = new Utf8JsonReader(bytes, DevkitServerConfig.ReaderOptions);
            data = JsonSerializer.Deserialize<LevelCartographyConfigData>(ref reader, DevkitServerConfig.SerializerSettings);
            Logger.LogDebug($"[{nameof(LevelCartographyConfigData)}] Using level cartography config at {file.Format()}.");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Unable to read {file.Format()} for cartography config. Json parser exception. Using default options.", method: nameof(LevelCartographyConfigData));
            Logger.LogInfo($"[{nameof(LevelCartographyConfigData)}] See {DevkitServerModule.GetRelativeRepositoryUrl("Documentation/Cartography Rendering.md", false).Format(false)} for how to format your cartography config.");
            Logger.LogError(ex, method: nameof(LevelCartographyConfigData));
            return null;
        }

        if (data != null)
            return data;

        Logger.LogWarning($"Unable to read {file.Format()} for cartography config. No value available. Using default options.", method: nameof(LevelCartographyConfigData));
        Logger.LogInfo($"[{nameof(LevelCartographyConfigData)}] See {DevkitServerModule.GetRelativeRepositoryUrl("Documentation/Cartography Rendering.md", false).Format(false)} for how to format your cartography config.");
        return null;
    }
}
