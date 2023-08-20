using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace DevkitServer.Configuration;

/// <summary>
/// Allows easily adding a schema to your configuration file by deriving your class from this class.
/// </summary>
public abstract class SchemaConfiguration
{
    /// <summary>
    /// Relative or absolute URI to a JSON schema file.
    /// </summary>
    [JsonProperty("$schema", DefaultValueHandling = DefaultValueHandling.Ignore)]
    [JsonPropertyName("$schema")]
    [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public abstract string SchemaURI { get; }
}
