using DevkitServer.Configuration;
using DevkitServer.Util.Encoding;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevkitServer.Core.Cartography;
public class LevelCartographyConfigData : SchemaConfiguration
{
    private Dictionary<Guid, EObjectChart>? _dictionaryCache;
    private EObjectChart[]? _roadMaterials;
    protected override string GetSchemaURI() => DevkitServerModule.GetRelativeRepositoryUrl("Module/Schemas/cartography_config.json", true);

    [JsonPropertyName("override_chart_color_provider")]
    public string? PreferredChartColorProvider { get; set; }

    [JsonPropertyName("override_active_compositors")]
    public string[]? ActiveCompositors { get; set; }

    [JsonPropertyName("chart_type_overrides")]
    public Dictionary<string, string>? ChartOverrides { get; set; }

    /// <summary>
    /// Attempts to resolve a guid from <see cref="ChartOverrides"/>.
    /// </summary>
    public bool TryGetObjectChartOverride(Guid guid, out EObjectChart chart)
    {
        if (ChartOverrides == null || ChartOverrides.Count == 0)
        {
            chart = EObjectChart.NONE;
            return false;
        }

        if (_dictionaryCache == null)
        {
            CreateCaches();
        }

        return _dictionaryCache!.TryGetValue(guid, out chart);
    }

    /// <summary>
    /// Attempts to resolve a guid from <see cref="ChartOverrides"/>.
    /// </summary>
    public bool TryGetRoadMaterialChartOverride(byte material, out EObjectChart chart)
    {
        if (ChartOverrides == null || ChartOverrides.Count == 0)
        {
            chart = EObjectChart.NONE;
            return false;
        }

        if (_roadMaterials == null)
        {
            CreateCaches();
        }

        if (material >= _roadMaterials!.Length)
        {
            chart = EObjectChart.NONE;
            return false;
        }

        chart = _roadMaterials[material];
        return true;
    }

    private void CreateCaches()
    {
        _dictionaryCache = new Dictionary<Guid, EObjectChart>(ChartOverrides.Count);
        _roadMaterials = new EObjectChart[LevelRoads.materials.Length];
        for (int i = 0; i < _roadMaterials.Length; ++i)
        {
            RoadMaterial material = LevelRoads.materials[i];
            _roadMaterials[i] = material.isConcrete ? material.width <= 8d ? EObjectChart.ROAD : EObjectChart.HIGHWAY : EObjectChart.PATH;
        }

        foreach (KeyValuePair<string, string> ovr in ChartOverrides)
        {
            if (!Enum.TryParse(ovr.Value, true, out EObjectChart value))
            {
                Logger.DevkitServer.LogWarning(nameof(LevelCartographyConfigData), $"Invalid chart type in cartography config: \"{ovr.Key.Format()}\". This row will be ignored.");
                continue;
            }

            if (Guid.TryParse(ovr.Key, out Guid parsedGuid))
            {
                _dictionaryCache.Add(parsedGuid, value);
            }
            else
            {
                int index = Array.FindIndex(LevelRoads.materials, x => x.material != null && x.material.mainTexture != null && x.material.mainTexture.name.Equals(ovr.Key, StringComparison.Ordinal));
                if (index != -1)
                {
                    _roadMaterials[index] = value;
                }
                else
                {
                    Logger.DevkitServer.LogWarning(nameof(LevelCartographyConfigData), $"Invalid GUID in cartography config: \"{ovr.Key.Format()}\". This row will be ignored.");
                }
            }
        }
    }

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

        Logger.DevkitServer.LogInfo(nameof(LevelCartographyConfigData), $"No 'cartography_config.json' is present in {info.getLocalizedName().Format(false)}.");
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
            Logger.DevkitServer.LogDebug(nameof(LevelCartographyConfigData), $"Using level cartography config at {file.Format()}.");
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(nameof(LevelCartographyConfigData), ex, $"Unable to read {file.Format()} for cartography config. Json parser exception. Using default options.");
            Logger.DevkitServer.LogInfo(nameof(LevelCartographyConfigData), $"See {DevkitServerModule.GetRelativeRepositoryUrl("Documentation/Cartography/Introduction.md", false).Format(false)} for how to format your cartography config.");
            return null;
        }

        if (data != null)
            return data;

        Logger.DevkitServer.LogWarning(nameof(LevelCartographyConfigData), $"Unable to read {file.Format()} for cartography config. No value available. Using default options.");
        Logger.DevkitServer.LogInfo(nameof(LevelCartographyConfigData), $"[{nameof(LevelCartographyConfigData)}] See {DevkitServerModule.GetRelativeRepositoryUrl("Documentation/Cartography/Introduction.md", false).Format(false)} for how to format your cartography config.");
        return null;
    }
}
