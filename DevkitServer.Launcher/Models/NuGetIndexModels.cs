extern alias NSJ;

using NuGet.Versioning;
using System;
using NSJ::Newtonsoft.Json;

namespace DevkitServer.Launcher.Models;
internal class NuGetIndex
{
    [JsonProperty("version")]
    public string? Version { get; set; }

    [JsonProperty("resources")]
    public NuGetResource[] Resources { get; set; } = Array.Empty<NuGetResource>();
}

#nullable disable
internal class NuGetResource
{
    [JsonProperty("@id")]
    public string Id { get; set; }

    [JsonProperty("@type")]
    public string Type { get; set; }

    [JsonProperty("comment")]
    public string Comment { get; set; }
}
#nullable restore