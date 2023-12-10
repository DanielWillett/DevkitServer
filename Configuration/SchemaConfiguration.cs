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
    public string SchemaURI => GetSchemaURI();

    // not using an abstract property here because older versions of System.Text.Json ignore base member attributes.
    protected abstract string GetSchemaURI();
}
