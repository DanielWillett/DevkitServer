using NuGet.Versioning;
using System;
using System.Text.Json.Serialization;

namespace DevkitServer.Launcher.Models;
internal class NuGetVersionsResponse
{
    [JsonPropertyName("versions")]
    public NuGetVersion[] Versions { get; set; } = Array.Empty<NuGetVersion>();
}