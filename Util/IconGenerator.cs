#if CLIENT
using UnityEngine.Rendering;
using GraphicsSettings = SDG.Unturned.GraphicsSettings;

namespace DevkitServer.Util;
public sealed class IconGenerator : MonoBehaviour
{
    internal const string Source = "OBJECT ICONS";

    public const float FOV = 60f;
    public const float DistanceScale = 1;
    public const float FarClipPlaneScale = 2.25f;
    private static readonly Vector3 EulerDefaultCameraRotation = new Vector3(5.298f, -23.733f, 0f);
    private static readonly Quaternion DefaultCameraRotation = Quaternion.Euler(EulerDefaultCameraRotation);
    private static readonly Dictionary<Guid, ObjectIconMetrics> Metrics = new Dictionary<Guid, ObjectIconMetrics>(4);
    private static readonly List<Renderer> WorkingRenderersList = new List<Renderer>(4);
    private Camera _camera = null!;
    private Light _light = null!;
    public static IconGenerator? Instance { get; private set; }
    [UsedImplicitly]
    private void Start()
    {
        _camera = this.gameObject.GetComponent<Camera>();
        _light = this.gameObject.GetComponent<Light>();

        _camera.cullingMask = RayMasks.LARGE | RayMasks.MEDIUM | RayMasks.SMALL;
        _camera.clearFlags = CameraClearFlags.Nothing;
        _camera.forceIntoRenderTexture = true;
        _camera.enabled = false;
        _camera.orthographic = false;
        _camera.fieldOfView = FOV;

        _light.enabled = false;
        _light.bounceIntensity = 0f;

        Instance = this;
    }
    public static void ClearCache() => Metrics.Clear();
    public static void ClearCache(Guid guid) => Metrics.Remove(guid);
    public static void GetIcon(Asset asset, int width, int height, Action<Asset, Texture2D?, bool> onIconReady)
    {
        ThreadUtil.assertIsGameThread();

        if (Instance == null)
            throw new InvalidOperationException("Not initialized.");

        if (asset is VehicleAsset vehicle)
        {
            VehicleTool.getIcon(vehicle.id, 0, vehicle, null, width, height, false, txt => onIconReady(asset, txt, true));
            return;
        }
        if (asset is ItemAsset item)
        {
            ItemTool.getIcon(item.id, 0, 100, item.getState(true), item, null, string.Empty, string.Empty, width, height, false, false, txt => onIconReady(asset, txt, true));
            return;
        }

        if (asset is not ObjectAsset obj)
            throw new ArgumentException("Asset must be item asset, vehicle asset, or object asset.");

        GameObject? model = obj.GetOrLoadModel();
        if (obj.type == EObjectType.DECAL)
        {
            if (model != null)
            {
                Decal? decal = model.GetComponentInChildren<Decal>();
                if (decal != null && decal.material.GetTexture("_MainTex") is Texture2D texture)
                {
                    onIconReady(asset, texture, false);
                    return;
                }
            }
            onIconReady(asset, null, true);
            return;
        }

        if (model == null)
        {
            onIconReady(asset, null, true);
            return;
        }

        ObjectIconMetrics metrics = GetObjectIconMetrics(asset);
        GameObject levelObject = Instantiate(model, new Vector3(-256f, -256f, 0f), LevelObjectUtil.DefaultObjectRotation);

        Texture2D icon = Instance.CaptureIcon(asset.GUID.ToString("N"), levelObject, in metrics, width, height);

        Destroy(levelObject);

        onIconReady(asset, icon, true);
    }
    public static void GetCameraPositionAndRotation(in ObjectIconMetrics metrics, Transform target, out Vector3 position, out Quaternion rotation)
    {
        position = metrics.CameraPosition;
        if (metrics.IsCustom)
        {
            Matrix4x4 matrix = Matrix4x4.TRS(target.transform.position - metrics.ObjectPositionOffset, target.transform.rotation, target.localScale);
            position = matrix.MultiplyPoint3x4(position);
            rotation = target.transform.rotation * metrics.CameraRotation;
        }
        else
        {
            position = target.transform.position - metrics.ObjectPositionOffset + position;
            rotation = metrics.CameraRotation;
        }
    }
    private Texture2D CaptureIcon(string name, GameObject target, in ObjectIconMetrics metrics, int width, int height)
    {
        RenderSettingsCopy settings = new RenderSettingsCopy();
        RenderSettings.fog = false;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Color.white;
        RenderSettings.ambientEquatorColor = Color.white;
        RenderSettings.ambientGroundColor = Color.white;
        if (Provider.isConnected)
            LevelLighting.setEnabled(false);
        _camera.enabled = true;
        _light.enabled = true;
        Vector3 position = metrics.CameraPosition;
        if (width != height && !metrics.IsCustom)
        {
            float diagFov = 2 * Mathf.Atan(Mathf.Tan(FOV / 2f) * ((float)height / width));
            float distance = metrics.ObjectSize / 2 / Mathf.Tan(diagFov) * DistanceScale;
            position = DefaultCameraRotation * Vector3.forward * distance;
            _camera.farClipPlane = distance * FarClipPlaneScale;
        }
        else
            _camera.farClipPlane = metrics.FarClipPlane;

        _light.range = _camera.farClipPlane;

        Quaternion rotation;

        if (metrics.IsCustom)
        {
            Matrix4x4 matrix = Matrix4x4.TRS(target.transform.position - metrics.ObjectPositionOffset, target.transform.rotation, Vector3.one);
            position = matrix.MultiplyPoint3x4(position);
            rotation = target.transform.rotation * metrics.CameraRotation;
        }
        else
        {
            position = target.transform.position - metrics.ObjectPositionOffset + position;
            rotation = metrics.CameraRotation;
        }

        _camera.transform.SetPositionAndRotation(position, rotation);
        RenderTexture targetTexture = RenderTexture.GetTemporary(width, height, 16, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, GraphicsSettings.IsItemIconAntiAliasingEnabled ? 4 : 1);
        targetTexture.name = "Render_" + name;
        RenderTexture.active = targetTexture;
        GL.Clear(true, true, ColorEx.BlackZeroAlpha);
        _camera.targetTexture = targetTexture;
        _camera.Render();

        if (Provider.isConnected)
            LevelLighting.setEnabled(true);
        settings.Apply();

        Texture2D outTexture = new Texture2D(width, height, TextureFormat.ARGB32, false)
        {
            name = "Icon_Obj_" + name,
            filterMode = FilterMode.Point
        };
        
        outTexture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
        outTexture.Apply();

        RenderTexture.ReleaseTemporary(targetTexture);
        _camera.targetTexture = null;
        _camera.enabled = false;
        _light.enabled = false;
        return outTexture;
    }
    public static ObjectIconMetrics GetObjectIconMetrics(Asset asset)
    {
        ThreadUtil.assertIsGameThread();

        if (Metrics.TryGetValue(asset.GUID, out ObjectIconMetrics metrics))
            return metrics;

        GameObject srcObject = asset switch
        {
            ObjectAsset obj => obj.GetOrLoadModel(),
            ItemBarricadeAsset barricade => barricade.barricade,
            ItemStructureAsset structure => structure.structure,
            _ => throw new ArgumentException("Asset must be an object asset, barricade asset, or structure asset.")
        };

        AssetIconPreset? preset = ObjectIconPresets.ActivelyEditing;

        if (preset == null || preset.Asset.GUID != asset.GUID)
            ObjectIconPresets.Presets.TryGetValue(asset.GUID, out preset);
        
        if (preset != null)
        {
            Vector3 pos = preset.IconPosition;
            Quaternion rot = preset.IconRotation;
            metrics = new ObjectIconMetrics(pos, Vector3.zero, rot, 0f, pos.magnitude * FarClipPlaneScale, true);
            Metrics.Add(asset.GUID, metrics);
            return metrics;
        }

        Transform? icon = srcObject.transform.Find("Icon");
        if (icon == null)
            icon = srcObject.transform.Find("Icon2");
        if (icon != null)
        {
            Matrix4x4 matrix = srcObject.transform.worldToLocalMatrix;
            Vector3 pos = matrix.MultiplyPoint3x4(icon.position);
            Quaternion rot = icon.localRotation;
            metrics = new ObjectIconMetrics(pos, Vector3.zero, rot, 0f, pos.magnitude * FarClipPlaneScale, true);
            Metrics.Add(asset.GUID, metrics);
            return metrics;
        }

        if (!TryGetExtents(srcObject, out Bounds bounds))
        {
            metrics = new ObjectIconMetrics(Vector3.zero, Vector3.zero, Quaternion.identity, 0f, 16f, false);
            Metrics.Add(asset.GUID, metrics);
            return metrics;
        }

        float size = Mathf.Max(bounds.extents.x, bounds.extents.y);

        float distance = size / Mathf.Tan(FOV) * DistanceScale;
        Vector3 position = DefaultCameraRotation * Vector3.back * -distance;
        Vector3 center = bounds.center;
        center.z = bounds.min.z + bounds.size.z / 4;
        metrics = new ObjectIconMetrics(-position, srcObject.transform.position - center, Quaternion.LookRotation(position), size, distance * FarClipPlaneScale, false);
        Metrics.Add(asset.GUID, metrics);
        return metrics;
    }
    public static bool TryGetExtents(GameObject obj, out Bounds bounds)
    {
        ThreadUtil.assertIsGameThread();

        bounds = new Bounds();
        bool foundOne = false;
        obj.GetComponentsInChildren(WorkingRenderersList);
        try
        {
            foreach (Renderer renderer in WorkingRenderersList)
            {
                if (renderer is not MeshRenderer and not SkinnedMeshRenderer)
                    continue;
                if (foundOne)
                    bounds.Encapsulate(renderer.bounds);
                else
                {
                    foundOne = true;
                    bounds = renderer.bounds;
                }

            }
        }
        finally
        {
            WorkingRenderersList.Clear();
        }

        return foundOne;
    }
    [UsedImplicitly]
    private void OnDestroy()
    {
        ClearCache();
        Instance = null;
    }
    private readonly struct RenderSettingsCopy
    {
        private readonly bool _fog;
        private readonly AmbientMode _ambientMode;
        private readonly Color _skyColor;
        private readonly Color _equatorColor;
        private readonly Color _groundColor;
        public RenderSettingsCopy()
        {
            _fog = RenderSettings.fog;
            _ambientMode = RenderSettings.ambientMode;
            _skyColor = RenderSettings.ambientSkyColor;
            _equatorColor = RenderSettings.ambientEquatorColor;
            _groundColor = RenderSettings.ambientGroundColor;
        }
        public void Apply()
        {
            RenderSettings.fog = _fog;
            RenderSettings.ambientMode = _ambientMode;
            RenderSettings.ambientSkyColor = _skyColor;
            RenderSettings.ambientEquatorColor = _equatorColor;
            RenderSettings.ambientGroundColor = _groundColor;
        }
    }
    public struct ObjectIconMetrics
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
    }

    public struct ObjectIconRequestInfo
    {
        public Asset Asset { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public AssetReference<MaterialPaletteAsset> Material { get; set; } = AssetReference<MaterialPaletteAsset>.invalid;
        public int MaterialIndexOverride { get; set; } = -1;
        public ObjectIconRequestInfo(Asset asset)
        {
            Asset = asset;
            Width = 128;
            Height = 128;
        }
    }
}
#endif