extern alias NSJ;

using NSJ::Newtonsoft.Json;
using System;

namespace DevkitServer.Launcher.Models;
internal class NuGetVersionsResponse
{
    [JsonProperty("versions")]
    public string[] Versions { get; set; } = Array.Empty<string>();
}