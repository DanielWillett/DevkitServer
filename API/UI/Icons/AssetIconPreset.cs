using DevkitServer.Configuration;
using System.Text.Json.Serialization;

namespace DevkitServer.API.UI.Icons;
public class AssetIconPreset
{
    [JsonPropertyName("object")]
    [JsonConverter(typeof(AssetReferenceJsonConverterFactory.AssetReferenceJsonConverterGuidPreferred<ObjectAsset>))]
    public AssetReference<ObjectAsset> Asset { get; set; }

    [JsonPropertyName("position")]
    public Vector3 IconPosition { get; set; }

    [JsonPropertyName("rotation")]
    [JsonConverter(typeof(QuaternionEulerPreferredJsonConverter))]
    public Quaternion IconRotation { get; set; }

    [JsonPropertyName("priority")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Priority { get; set; }

    [JsonIgnore]
    public string? File { get; set; }
}