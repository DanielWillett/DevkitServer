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
    [JsonProperty("$schema", DefaultValueHandling = DefaultValueHandling.Ignore, Order = 1)]
    [JsonPropertyName("$schema")]
    public string SchemaURI => GetSchemaURI();

    /// <summary>
    /// Return a plain-text URL or relative path to the JSON schema for this config.
    /// </summary>
    protected abstract string GetSchemaURI();
}
