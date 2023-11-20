using DanielWillett.LevelObjectIcons.Models;
using DevkitServer.Util;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace DanielWillett.LevelObjectIcons;
public static class ObjectIconPresets
{
    public static readonly Quaternion DefaultObjectRotation = LevelObjectUtil.DefaultObjectRotation;
    public static AssetIconPreset? ActivelyEditing => FromInternal(DevkitServer.API.UI.Icons.ObjectIconPresets.ActivelyEditing);
    public static bool DebugLogging { get; set; }
    public static IReadOnlyDictionary<Guid, AssetIconPreset> ActivePresets => new ReadOnlyDictionary<Guid, AssetIconPreset>(GetInternalDictionary());
    private static AssetIconPreset? FromInternal(DevkitServer.API.UI.Icons.AssetIconPreset? internalIconAssetPreset)
    {
        if (internalIconAssetPreset == null)
            return null;

        return new AssetIconPreset
        {
            File = internalIconAssetPreset.File,
            IconPosition = internalIconAssetPreset.IconPosition,
            IconRotation = internalIconAssetPreset.IconRotation,
            Object = internalIconAssetPreset.Asset.GUID,
            Priority = internalIconAssetPreset.Priority
        };
    }
    private static Dictionary<Guid, AssetIconPreset> GetInternalDictionary()
    {
        IReadOnlyDictionary<Guid, DevkitServer.API.UI.Icons.AssetIconPreset> otherDict = DevkitServer.API.UI.Icons.ObjectIconPresets.ActivePresets;

        Dictionary<Guid, AssetIconPreset> newDict = new Dictionary<Guid, AssetIconPreset>(otherDict.Count);

        foreach (KeyValuePair<Guid, DevkitServer.API.UI.Icons.AssetIconPreset> kvp in otherDict)
            newDict.Add(kvp.Key, FromInternal(kvp.Value)!);

        return newDict;
    }
}
