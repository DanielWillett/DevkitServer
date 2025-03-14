using System.Globalization;
using DevkitServer.API.Cartography.Compositors;
using DevkitServer.Configuration;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevkitServer.Core.Cartography;

public class LevelCartographyConfigData : SchemaConfiguration
{
    private Dictionary<Guid, EObjectChart>? _dictionaryCache;
    private EObjectChart[]? _roadMaterials;
    private JsonDocument? _doc;

    internal string? FilePath;
    protected override string GetSchemaURI() => DevkitServerModule.GetRelativeRepositoryUrl("Module/Schemas/cartography_config_schema.json", true);

    [JsonPropertyName("override_chart_color_provider")]
    public string? PreferredChartColorProvider { get; set; }

    [JsonPropertyName("chart_type_overrides")]
    public Dictionary<string, string>? ChartOverrides { get; set; }

    [JsonPropertyName("time")]
    public string? Time { get; set; }

    internal void SyncTime(out float oldTime)
    {
        oldTime = float.NaN;
        if (string.IsNullOrWhiteSpace(Time))
            return;

        if (!LightingUtil.TryParseLevelTime(Time, CultureInfo.InvariantCulture, out float time))
        {
            Logger.DevkitServer.LogWarning(nameof(LevelCartographyConfigData), $"Invalid time format, expected decimal number or {"HH[:MM[:SS]] [AM|PM]".Colorize(FormattingColorType.String)} format.");
            return;
        }

        oldTime = LevelLighting.time;
        LevelLighting.time = time;
    }

    public CartographyCompositorConfigurationInfo[]? GetActiveCompositors()
    {
#if CLIENT
        if (this is CompositorPipeline pipeline)
            return pipeline.Compositors;
#endif

        if (_doc == null)
        {
            return null;
        }

        JsonElement doc = _doc.RootElement;

        if (doc.ValueKind == JsonValueKind.Undefined)
            return null;

        if (!doc.TryGetProperty("override_active_compositors", out JsonElement element) && !doc.TryGetProperty("compositors", out element) || element.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        int len = element.GetArrayLength();
        CartographyCompositorConfigurationInfo[] comps = new CartographyCompositorConfigurationInfo[len];
        JsonElement.ArrayEnumerator enumerator = element.EnumerateArray();
        for (int i = 0; i < len && enumerator.MoveNext(); ++i)
        {
            ref CartographyCompositorConfigurationInfo c = ref comps[i];
            JsonElement subElem = enumerator.Current;
            if (subElem.ValueKind == JsonValueKind.String)
            {
                c.TypeName = subElem.GetString();
            }
            else if (subElem.ValueKind == JsonValueKind.Object && subElem.TryGetProperty("type", out JsonElement typeName) && typeName.ValueKind == JsonValueKind.String)
            {
                c.TypeName = typeName.GetString();
                c.ExtraConfig = subElem;
            }
        }

        enumerator.Dispose();

        return comps;
    }

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
        _dictionaryCache = new Dictionary<Guid, EObjectChart>(ChartOverrides!.Count);
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

    public static LevelCartographyConfigData? ReadFromLevel(LevelInfo? info, out JsonDocument configDocument, bool createDoc = true)
    {
        info ??= Level.info;

        string levelDir = info.path;

        string file = Path.Combine(levelDir, "Editor", "cartography_config.json");
        if (File.Exists(file))
            return Read(file, out configDocument, createDoc);
        
        file = Path.Combine(levelDir, "Editor", "cartography.json");
        if (File.Exists(file))
            return Read(file, out configDocument, createDoc);

        file = Path.Combine(levelDir, "Chart", "cartography_config.json");
        if (File.Exists(file))
            return Read(file, out configDocument, createDoc);
        
        file = Path.Combine(levelDir, "Chart", "cartography.json");
        if (File.Exists(file))
            return Read(file, out configDocument, createDoc);

        file = Path.Combine(levelDir, "Terrain", "cartography_config.json");
        if (File.Exists(file))
            return Read(file, out configDocument, createDoc);
        
        file = Path.Combine(levelDir, "Terrain", "cartography.json");
        if (File.Exists(file))
            return Read(file, out configDocument, createDoc);

        file = Path.Combine(levelDir, "cartography_config.json");
        if (File.Exists(file))
            return Read(file, out configDocument, createDoc);
        
        file = Path.Combine(levelDir, "cartography.json");
        if (File.Exists(file))
            return Read(file, out configDocument, createDoc);

        Logger.DevkitServer.LogInfo(nameof(LevelCartographyConfigData), $"No 'cartography_config.json' is present in {info.getLocalizedName().Format(false)}.");
        configDocument = createDoc ? JsonDocument.Parse("{}") : null!;
        return null;
    }

    private static LevelCartographyConfigData? Read(string file, out JsonDocument configDocument, bool createDoc)
    {
        LevelCartographyConfigData? data;
        try
        {
            Span<byte> bytes = FileUtil.ReadAllBytesUtf8(file);
            Utf8JsonReader reader = new Utf8JsonReader(bytes, DevkitServerConfig.ReaderOptions);
            JsonDocument? document;
            if (createDoc)
            {
                Utf8JsonReader docReader = reader;
                document = JsonDocument.ParseValue(ref docReader);
            }
            else
            {
                document = null;
            }
            data = JsonSerializer.Deserialize<LevelCartographyConfigData>(ref reader, DevkitServerConfig.SerializerSettings);
            Logger.DevkitServer.LogDebug(nameof(LevelCartographyConfigData), $"Using level cartography config at {file.Format()}.");
            if (data != null)
                data._doc = document;
            configDocument = document!;
        }
        catch (Exception ex)
        {
            configDocument = createDoc ? JsonDocument.Parse("{}") : null!;
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