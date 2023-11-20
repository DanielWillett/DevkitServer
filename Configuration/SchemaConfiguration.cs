extern alias NSJ;

using System.Text.Json.Serialization;
using NSJ::Newtonsoft.Json;

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
    public abstract string SchemaURI { get; }
}
