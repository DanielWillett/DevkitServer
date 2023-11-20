using SDG.Unturned;
using UnityEngine;

namespace DanielWillett.LevelObjectIcons.API;
public interface IDefaultIconProvider
{
    int Priority { get; }
    void GetMetrics(ObjectAsset @object, out Vector3 position, out Quaternion rotation);
    bool AppliesTo(ObjectAsset @object);
}