using SDG.Unturned;
using System;
using UnityEngine;

namespace DanielWillett.LevelObjectIcons.Models;

public class AssetIconPreset
{
    public string? Name => Assets.find(Object)?.name;
    public Guid Object { get; set; }
    public Vector3 IconPosition { get; set; }
    public Quaternion IconRotation { get; set; }
    public int Priority { get; set; }
    public string? File { get; set; }
}