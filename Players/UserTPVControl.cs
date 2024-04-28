#if CLIENT
using DanielWillett.ReflectionTools;
using DevkitServer.API;
using DevkitServer.API.Abstractions;
using DevkitServer.Multiplayer;
using HarmonyLib;
using System.Globalization;
using System.Reflection;

namespace DevkitServer.Players;

public class UserTPVControl : MonoBehaviour
{
    private static readonly Vector3 TPVScale = new Vector3(2.5f, 2.5f, 2.5f);
    private static readonly Quaternion TPVRotation = Quaternion.Euler(90f, 0f, 0f);
    private static GameObject? _objectPrefab;
    private EditorClothes _editorClothes = null!;
    public EditorUser User { get; internal set; } = null!;
    public GameObject Model { get; private set; } = null!;

    [UsedImplicitly]
    private void Start()
    {
        if (User == null || User.Player == null)
        {
            Destroy(this);
            Logger.DevkitServer.LogError(nameof(UserTPVControl), "Invalid UserTPVControl setup; EditorUser not found!");
            return;
        }
        if (_objectPrefab == null)
        {
            if (DevkitServerModule.Bundle == null)
            {
                Logger.DevkitServer.LogError(nameof(UserTPVControl), "Unable to set up UserTPVControl object, " + "devkitserver.masterbundle".Format() + " not loaded.");
                return;
            }

            _objectPrefab = Instantiate(DevkitServerModule.Bundle.load<GameObject>("resources/tpv_char_server"));
            if (_objectPrefab != null)
                DontDestroyOnLoad(_objectPrefab);

            if (_objectPrefab == null)
            {
                Logger.DevkitServer.LogError(nameof(UserTPVControl), "Unable to set up UserTPVControl object, " + "Resources/TPV_Char_Server".Format() + " not found in " + "devkitserver.masterbundle".Format() + " (or it was not loaded).");
                return;
            }
        }

        Model = Instantiate(_objectPrefab, transform, false);
        Model.transform.localScale = TPVScale;
        Model.transform.localRotation = TPVRotation;
        Model.name = "TPV_Editor_" + User.SteamId.m_SteamID.ToString(CultureInfo.InvariantCulture);
        _editorClothes = Model.AddComponent<EditorClothes>();
        _editorClothes.Face = User.Player.face;
        _editorClothes.Beard = User.Player.beard;
        _editorClothes.SkinColor = User.Player.skin;
        _editorClothes.Hair = User.Player.hair;
        _editorClothes.Hat.SteamItem = User.Player.hatItem;
        _editorClothes.Glasses.SteamItem = User.Player.glassesItem;
        _editorClothes.Mask.SteamItem = User.Player.maskItem;
        _editorClothes.IsLeftHanded = User.Player.IsLeftHanded;
        _editorClothes.IsVisual = true;
        _editorClothes.IsMythic = true;
        _editorClothes.Apply();
    }
    [UsedImplicitly]
    void OnDestroy()
    {
        Destroy(Model);
        Model = null!;
    }

    internal static void Init()
    {
        Provider.onClientDisconnected += OnClientDisconnected;
    }
    internal static void Deinit()
    {
        Provider.onClientDisconnected -= OnClientDisconnected;
    }
    private static void OnClientDisconnected()
    {
        Destroy(_objectPrefab);
        _objectPrefab = null;
    }
    [HarmonyPatch(typeof(PlayerClothing), nameof(PlayerClothing.ReceiveFaceState))]
    [HarmonyPostfix]
    [UsedImplicitly]
    private static void PrefixReceiveFaceState(PlayerClothing __instance, byte index)
    {
        if (UserManager.FromPlayer(__instance.player) is { IsOnline: true } editor && editor.EditorObject != null && editor.EditorObject.TryGetComponent(out UserTPVControl ctrl))
        {
            ctrl._editorClothes.Face = index;
            ctrl._editorClothes.Apply();
        }
    }
}
[EarlyTypeInit]
[HarmonyPatch]
internal sealed class EditorClothes : MonoBehaviour
{
    private static readonly Quaternion CosmeticRotationOffset = Quaternion.Euler(-90f, 0f, -90f);
    private static readonly Vector3 CosmeticPositionOffset = new Vector3(0f, -1.1f, 0f);
    private static readonly StaticGetter<Shader>? GetClothesShader = Accessor.GenerateStaticGetter<HumanClothes, Shader>("clothingShader");
    private static readonly StaticGetter<Shader>? GetHairShader = Accessor.GenerateStaticGetter<HumanClothes, Shader>("shader");
    private static readonly StaticGetter<int>? GetSkinColorPropertyId = Accessor.GenerateStaticGetter<HumanClothes, int>("skinColorPropertyID");
    private static readonly StaticGetter<int>? GetFaceAlbedoTexturePropertyID = Accessor.GenerateStaticGetter<HumanClothes, int>("faceAlbedoTexturePropertyID");
    private static readonly StaticGetter<int>? GetFaceEmissionTexturePropertyID = Accessor.GenerateStaticGetter<HumanClothes, int>("faceEmissionTexturePropertyID");
    private static readonly InstanceGetter<ItemClothingAsset, bool>? GetShouldMirrorLeftHandedModel = Accessor.GenerateInstanceGetter<ItemClothingAsset, bool>("shouldMirrorLeftHandedModel");
    private static readonly Action<GameObject, bool>? CallDestroyCollidersInChildren =
        Accessor.GenerateStaticCaller<Action<GameObject, bool>>(AccessorExtensions.AssemblyCSharp
            .GetType("SDG.Unturned.PrefabUtil")
            ?.GetMethod("DestroyCollidersInChildren", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!);

    private byte _hair;
    private byte _beard;
    private bool _hairDirty;
    private bool _beardDirty;
    private byte _face;
    private bool _faceDirty;
    private bool _hasHair;
    private bool _hasBeard;
    private bool _isVisual;
    private bool _isMythic;
    private bool _isLeftHanded;
    private Color _skinColor;
    private bool _skinColorDirty;
    private Color _hairColor;
    private bool _hairColorDirty;
    private Material _materialClothing = null!;
    private Material _materialHair = null!;
    private Renderer[]? _meshes;
    private Transform? _hairModel;
    private Transform? _beardModel;
    private Transform? _hatModel;
    private Transform? _glassesModel;
    private Transform? _maskModel;
    public byte Hair
    {
        get => _hair;
        set
        {
            if (_hair == value)
                return;
            _hair = value;
            _hairDirty = true;
        }
    }
    public byte Beard
    {
        get => _beard;
        set
        {
            if (_hair == value)
                return;
            _beard = value;
            _beardDirty = true;
        }
    }
    public byte Face
    {
        get => _face;
        set
        {
            if (_face == value)
                return;
            _face = value;
            _faceDirty = true;
        }
    }
    public Color SkinColor
    {
        get => _skinColor;
        set
        {
            if (_skinColor == value)
                return;
            _skinColor = value;
            _skinColorDirty = true;
        }
    }
    
    public Color HairColor
    {
        get => _hairColor;
        set
        {
            if (_hairColor == value)
                return;
            _hairColor = value;
            _hairColorDirty = true;
        }
    }

    public EconomyItem<ItemHatAsset> Hat { get; } = new EconomyItem<ItemHatAsset>();
    public EconomyItem<ItemGlassesAsset> Glasses { get; } = new EconomyItem<ItemGlassesAsset>();
    public EconomyItem<ItemMaskAsset> Mask { get; } = new EconomyItem<ItemMaskAsset>();
    public bool IsVisual
    {
        get => _isVisual;
        set
        {
            if (_isVisual == value)
                return;
            _isVisual = value;
            Hat.isDirty = true;
            Glasses.isDirty = true;
            Mask.isDirty = true;
        }
    }
    public bool IsMythic
    {
        get => _isMythic;
        set
        {
            if (_isMythic == value)
                return;
            _isMythic = value;
            Hat.isDirty = true;
            Glasses.isDirty = true;
            Mask.isDirty = true;
        }
    }
    public bool IsLeftHanded
    {
        get => _isLeftHanded;
        set
        {
            if (_isLeftHanded == value)
                return;
            _isLeftHanded = value;
            MarkAllDirty(true);
        }
    }
    public void MarkAllDirty(bool dirty)
    {
        Hat.isDirty = dirty;
        Glasses.isDirty = dirty;
        Mask.isDirty = dirty;
        _hairDirty = dirty;
        _beardDirty = dirty;
        _skinColorDirty = dirty;
        _faceDirty = dirty;
        _hairColorDirty = dirty;
    }
    public void Apply()
    {
        if (_skinColorDirty && _materialClothing != null)
        {
            _materialClothing.SetColor(GetSkinColorPropertyId?.Invoke() ?? Shader.PropertyToID("_SkinColor"), _skinColor);
            _skinColorDirty = false;
        }
        if (_faceDirty && _materialClothing != null)
        {
            Texture albedo = Resources.Load<Texture2D>("Faces/" + _face.ToString(CultureInfo.InvariantCulture) + "/Texture");
            Texture emission = Resources.Load<Texture2D>("Faces/" + _face.ToString(CultureInfo.InvariantCulture) + "/Emission");
            _materialClothing.SetTexture(GetFaceAlbedoTexturePropertyID?.Invoke() ?? Shader.PropertyToID("_FaceAlbedoTexture"), albedo);
            _materialClothing.SetTexture(GetFaceEmissionTexturePropertyID?.Invoke() ?? Shader.PropertyToID("_FaceEmissionTexture"), emission);
            _faceDirty = false;
        }
        bool hairVis = !(Hat.Asset is { hairVisible: false } || Glasses.Asset is { hairVisible: false } || Mask.Asset is { hairVisible: false });
        bool beardVis = !(Hat.Asset is { beardVisible: false } || Glasses.Asset is { beardVisible: false } || Mask.Asset is { beardVisible: false });
        if (hairVis != _hasHair)
        {
            _hasHair = hairVis;
            _hairDirty = true;
        }
        if (beardVis != _hasBeard)
        {
            _hasBeard = beardVis;
            _beardDirty = true;
        }
        ApplyIfDirty(Hat, ref _hatModel, "Hat");
        ApplyIfDirty(Glasses, ref _glassesModel, "Glasses");
        ApplyIfDirty(Mask, ref _maskModel, "Mask");

        if (_hairDirty)
        {
            ApplyHair(_hair, ref _hairModel, "Hair", _hasHair);
            _hairDirty = false;
        }

        if (_beardDirty)
        {
            ApplyHair(_beard, ref _beardModel, "Beard", _hasBeard);
            _beardDirty = false;
        }

        MarkAllDirty(false);
    }
    private void ApplyHair(byte index, ref Transform? model, string prefix, bool enabled)
    {
        if (_hairColorDirty)
        {
            _materialHair.color = _hairColor;
            _hairColorDirty = false;
        }
        if (model != null)
        {
            Destroy(model.gameObject);
            model = null;
        }

        if (!enabled)
            return;

        GameObject hairModel = Resources.Load<GameObject>(prefix + "s/" + index.ToString(CultureInfo.InvariantCulture) + "/" + prefix);
        if (hairModel == null)
            return;

        model = Instantiate(hairModel, transform.position + CosmeticPositionOffset, transform.rotation * CosmeticRotationOffset, transform).transform;
        model.name = prefix + index.ToString(CultureInfo.InvariantCulture);
        Transform m0 = model.Find("Model_0");

        if (m0 != null)
            m0.GetComponent<Renderer>().sharedMaterial = _materialHair;

        model.DestroyRigidbody();
    }
    private void ApplyIfDirty<TAsset>(EconomyItem<TAsset> item, ref Transform? model, string namePrefix) where TAsset : ItemGearAsset
    {
        if (!item.isDirty)
            return;

        if (model != null)
        {
            Destroy(model.gameObject);
            model = null;
        }

        TAsset? asset = item.Asset;
        GameObject? instance = asset == null ? null : AssetUtil.GetItemInstance(asset);
        if (instance != null && asset!.shouldBeVisible(false))
        {
            model = Instantiate(instance, transform.position + CosmeticPositionOffset, transform.rotation * CosmeticRotationOffset, transform).transform;
            model.name = namePrefix + "_" + asset.GUID.ToString("N");
            model.transform.localScale = new Vector3(1f, !_isLeftHanded || !(GetShouldMirrorLeftHandedModel != null && GetShouldMirrorLeftHandedModel(asset)) ? 1f : -1f, 1f);
            if (asset.shouldDestroyClothingColliders)
                CallDestroyCollidersInChildren?.Invoke(model.gameObject, true);

            model.DestroyRigidbody();

            if (_isVisual && _isMythic)
            {
                MythicAsset? mythic = item.MythicAsset;
                CenterHeadEffect(null, transform, model);
                if (mythic != null)
                    ItemTool.applyEffect(model, mythic.id, EEffectType.HOOK);
            }
            ApplyHairOverride(asset, model);
        }

        item.isDirty = false;
    }
    [HarmonyPatch(typeof(HumanClothes), "centerHeadEffect")]
    [HarmonyReversePatch]
    [UsedImplicitly]
    private static void CenterHeadEffect(object? instance, Transform? skull, Transform model) { }
    private void ApplyHairOverride(ItemGearAsset itemAsset, Transform rootModel)
    {
        if (string.IsNullOrEmpty(itemAsset.hairOverride))
            return;
        Transform childRecursive = rootModel.FindChildRecursive(itemAsset.hairOverride);
        if (childRecursive == null)
            Assets.reportError(itemAsset, "Cannot find hair override \"" + itemAsset.hairOverride + "\".");
        else
        {
            Renderer component = childRecursive.GetComponent<Renderer>();
            if (component != null && _materialHair != null)
            {
                component.sharedMaterial = _materialHair;
            }
        }
    }

    [UsedImplicitly]
    private void Awake()
    {
        _meshes = transform.gameObject.GetComponentsInChildren<Renderer>();
        _materialClothing = new Material(GetClothesShader?.Invoke() ?? Shader.Find("Standard/Clothes"))
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        _materialHair = new Material(GetHairShader?.Invoke() ?? Shader.Find("Standard"))
        {
            name = "Hair",
            hideFlags = HideFlags.HideAndDontSave
        };
        _materialHair.SetFloat("_Glossiness", 0.0f);
        for (int i = 0; i < _meshes.Length; ++i)
        {
            Renderer r = _meshes[i];
            if (r != null) r.sharedMaterial = _materialClothing;
        }

        MarkAllDirty(true);
    }

    [UsedImplicitly]
    private void OnDestroy()
    {
        if (_materialClothing != null)
        {
            Destroy(_materialClothing);
            _materialClothing = null!;
        }
        if (_materialHair != null)
        {
            Destroy(_materialHair);
            _materialHair = null!;
        }
    }

    public override string ToString() => "Editor Clothes State: " +
                                        $"Face: {Face.Format()}, " +
                                        $"Beard: {Beard.Format()}, " +
                                        $"Hair: {Hair.Format()}, " +
                                        $"Skin Color: {ColorUtility.ToHtmlStringRGB(SkinColor).Colorize(SkinColor)}, " +
                                        $"Hair Color: {ColorUtility.ToHtmlStringRGB(HairColor).Colorize(HairColor)}, " +
                                        $"Hat: {Hat.Format()}, " +
                                        $"Glasses: {Glasses.Format()}, " +
                                        $"Mask: {Mask.Format()}, " +
                                        $"Visual: {IsVisual.Format()}, " +
                                        $"Mythic: {IsMythic.Format()}.";
}

#endif