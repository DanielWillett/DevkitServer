using System;
using DanielWillett.LevelObjectIcons.Models;
using DevkitServer.API.UI.Icons;
using SDG.Unturned;
using UnityEngine;

namespace DanielWillett.LevelObjectIcons;
public sealed class IconGenerator : MonoBehaviour
{
    public const float FOV = ObjectIconGenerator.FOV;
    public const float DistanceScale = ObjectIconGenerator.DistanceScale;
    public const float FarClipPlaneScale = ObjectIconGenerator.FarClipPlaneScale;
    public static IconGenerator? Instance { get; private set; } = null;
    public static void ClearCache() => ObjectIconGenerator.ClearCache();
    public static void ClearCache(Guid guid) => ObjectIconGenerator.ClearCache(guid);
    public static void GetIcon(Asset asset, int width, int height, ObjectRenderOptions? options, Action<Asset, Texture2D?, bool, ObjectRenderOptions?> onIconReady)
    {
        ObjectIconGenerator.GetIcon(asset, width, height, FromPublic(options),
            (asset1, texture2D, arg3, arg4) =>
            {
                onIconReady(asset1, texture2D, arg3, FromInternal(arg4));
            }
        );
    }
    public static void GetCameraPositionAndRotation(in ObjectIconMetrics metrics, Transform target, out Vector3 position, out Quaternion rotation)
    {
        ObjectIconGenerator.GetCameraPositionAndRotation(metrics.ToInternal(), target, out position, out rotation);
    }
    public static ObjectIconMetrics GetObjectIconMetrics(Asset asset)
    {
        return new ObjectIconMetrics(ObjectIconGenerator.GetObjectIconMetrics(asset));
    }
    public static bool TryGetExtents(GameObject obj, out Bounds bounds)
    {
        return ObjectIconGenerator.TryGetExtents(obj, out bounds);
    }
    public static ObjectIconRenderOptions? FromPublic(ObjectRenderOptions? @public)
    {
        if (@public == null) return null;
        return new ObjectIconRenderOptions
        {
            MaterialIndexOverride = @public.MaterialIndexOverride,
            MaterialPaletteOverride = @public.MaterialPaletteOverride
        };
    }
    public static ObjectRenderOptions? FromInternal(ObjectIconRenderOptions? @internal)
    {
        if (@internal == null) return null;
        return new ObjectRenderOptions
        {
            MaterialIndexOverride = @internal.MaterialIndexOverride,
            MaterialPaletteOverride = @internal.MaterialPaletteOverride
        };
    }
    public readonly struct ObjectIconMetrics
    {
        public Vector3 CameraPosition { get; }
        public Vector3 ObjectPositionOffset { get; }
        public Quaternion CameraRotation { get; }
        public bool IsCustom { get; }
        public float ObjectSize { get; }
        public float FarClipPlane { get; }
        public ObjectIconMetrics(Vector3 cameraPosition, Vector3 objectPositionOffset, Quaternion cameraRotation, float objectSize, float farClipPlane, bool isCustom)
        {
            CameraPosition = cameraPosition;
            ObjectPositionOffset = objectPositionOffset;
            CameraRotation = cameraRotation;
            IsCustom = isCustom;
            ObjectSize = objectSize;
            FarClipPlane = farClipPlane;
        }
        internal ObjectIconMetrics(ObjectIconGenerator.ObjectIconMetrics metrics) : this(metrics.CameraPosition, metrics.ObjectPositionOffset, metrics.CameraRotation, metrics.ObjectSize, metrics.FarClipPlane, metrics.IsCustom) { }
        internal ObjectIconGenerator.ObjectIconMetrics ToInternal() => new ObjectIconGenerator.ObjectIconMetrics(CameraPosition, ObjectPositionOffset, CameraRotation, ObjectSize, FarClipPlane, IsCustom);
    }
}
