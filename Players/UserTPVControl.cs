#if CLIENT
using JetBrains.Annotations;
using System.Globalization;
using System.Reflection;
using DevkitServer.Multiplayer;
using HarmonyLib;

namespace DevkitServer.Players;

public class UserTPVControl : MonoBehaviour
{
    private static GameObject? _objectPrefab;
    private static bool _init;
    private EditorClothes _editorClothes;
    public EditorUser User { get; internal set; } = null!;
    public GameObject Model { get; private set; } = null!;

    [UsedImplicitly]
    private void Start()
    {
        if (User == null || User.Player == null)
        {
            Destroy(this);
            Logger.LogError("Invalid UserTPVControl setup; EditorUser not found!");
            return;
        }
        if (!_init)
        {
            _init = true;
            if (DevkitServerModule.Bundle == null)
            {
                Logger.LogError("Unable to set up UserTPVControl object, " + "devkitserver.masterbundle".Format() + " not loaded.");
                return;
            }

            _objectPrefab = DevkitServerModule.Bundle.load<GameObject>("resources/tpv_char_server");
            DontDestroyOnLoad(_objectPrefab);
        }

        if (_objectPrefab == null)
        {
            Logger.LogError("Unable to set up UserTPVControl object, " + "Resources/TPV_Char_Server".Format() + " not found in " + "devkitserver.masterbundle".Format() + " (or it was not loaded).");
            return;
        }

        Model = Instantiate(_objectPrefab, transform, false);
        Model.transform.rotation = Quaternion.Euler(90, 0, 0);
        Model.name = "TPV_Editor_" + User.SteamId.m_SteamID.ToString(CultureInfo.InvariantCulture);
        _editorClothes = Model.AddComponent<EditorClothes>();
        _editorClothes.Face = User.Player.face;
        _editorClothes.Beard = User.Player.beard;
        _editorClothes.SkinColor = User.Player.skin;
        _editorClothes.Hair = User.Player.hair;
        _editorClothes.VisualHat = User.Player.hatItem;
        _editorClothes.IsVisual = User.Player.player.clothing.thirdClothes.isVisual;
        _editorClothes.IsMythic = User.Player.player.clothing.thirdClothes.isMythic;
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
        _init = false;
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
            ctrl._editorClothes.Face = ctrl.User.Player!.face;
            ctrl._editorClothes.Apply();
        }
    }
}
[EarlyTypeInit]
internal sealed class EditorClothes : MonoBehaviour
{
    private static readonly StaticGetter<Shader>? GetClothesShader = Accessor.GenerateStaticGetter<HumanClothes, Shader>("clothingShader");
    private static readonly StaticGetter<Shader>? GetHairShader = Accessor.GenerateStaticGetter<HumanClothes, Shader>("shader");
    private static readonly StaticGetter<int>? GetSkinColorPropertyId = Accessor.GenerateStaticGetter<HumanClothes, int>("skinColorPropertyID");
    private static readonly StaticGetter<int>? GetFaceAlbedoTexturePropertyID = Accessor.GenerateStaticGetter<HumanClothes, int>("faceAlbedoTexturePropertyID");
    private static readonly StaticGetter<int>? GetFaceEmissionTexturePropertyID = Accessor.GenerateStaticGetter<HumanClothes, int>("faceEmissionTexturePropertyID");
    private static readonly InstanceGetter<ItemClothingAsset, bool>? GetShouldMirrorLeftHandedModel = Accessor.GenerateInstanceGetter<ItemClothingAsset, bool>("shouldMirrorLeftHandedModel");
    private static readonly Action<GameObject, bool>? CallDestroyCollidersInChildren =
        Accessor.GenerateStaticCaller<Action<GameObject, bool>>(typeof(Provider).Assembly
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
    private int _visualHat;
    private ItemHatAsset? _visualHatAsset;
    private ItemHatAsset? _hatAsset;
    private bool _hatDirty;
    private bool _isVisual;
    private bool _isMythic;
    private bool _isLeftHanded;
    private Color _skinColor;
    private bool _skinColorDirty;
    private Material? _materialClothing;
    private Material? _materialHair;
    private Renderer[]? _meshes;
    private Transform? _hairModel;
    private Transform? _beardModel;
    private Transform? _hatModel;
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
    public int VisualHat
    {
        get => _visualHat;
        set
        {
            if (_visualHat == value)
                return;
            _visualHat = value;
            if (_visualHat != 0)
            {
                try
                {
                    _visualHatAsset = Assets.find(EAssetType.ITEM, Provider.provider.economyService.getInventoryItemID(_visualHat)) as ItemHatAsset;
                }
                catch
                {
                    _visualHatAsset = null;
                }
                if (_visualHatAsset is { isPro: false })
                {
                    _visualHat = 0;
                    _visualHatAsset = null;
                }
            }
            else
                _visualHatAsset = null;
            _hatDirty = true;
        }
    }
    public ItemHatAsset? HatAsset
    {
        get => _hatAsset;
        internal set
        {
            _hatAsset = value;
            _hatDirty = true;
        }
    }
    public bool IsVisual
    {
        get => _isVisual;
        set
        {
            if (_isVisual == value)
                return;
            _isVisual = value;
            _hatDirty = true;
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
            _hatDirty = true;
        }
    }
    public bool Hand
    {
        get => this._isLeftHanded;
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
        _hairDirty = dirty;
        _hatDirty = dirty;
        _skinColorDirty = dirty;
        _faceDirty = dirty;
    }
    public void Apply()
    {
        Logger.LogInfo("Applying...");
        if (_hatAsset != null && _hatAsset.isPro)
        {
            _hatAsset = null;
            _hatDirty = true;
        }
        if (_skinColorDirty && _materialClothing != null)
        {
            _materialClothing.SetColor(GetSkinColorPropertyId?.Invoke() ?? Shader.PropertyToID("_SkinColor"), _skinColor);
            Logger.LogInfo("Skin color updated: " + _skinColor.Format() + ".");
        }
        if (_faceDirty && _materialClothing != null)
        {
            Texture albedo = Resources.Load<Texture2D>("Faces/" + _face.ToString(CultureInfo.InvariantCulture) + "/Texture");
            Texture emission = Resources.Load<Texture2D>("Faces/" + _face.ToString(CultureInfo.InvariantCulture) + "/Emission");
            _materialClothing.SetTexture(GetFaceAlbedoTexturePropertyID?.Invoke() ?? Shader.PropertyToID("_FaceAlbedoTexture"), albedo);
            _materialClothing.SetTexture(GetFaceEmissionTexturePropertyID?.Invoke() ?? Shader.PropertyToID("_FaceEmissionTexture"), emission);
            Logger.LogInfo("Face updated: " + _face.Format() + ".");
        }
        ItemHatAsset? hat = this._visualHatAsset == null || !_isVisual ? _hatAsset : _visualHatAsset;
        bool hairVis = (hat == null || hat.hairVisible);
        if (hairVis != _hasHair)
        {
            _hasHair = hairVis;
            _hairDirty = true;
        }
        bool beardVis = (hat == null || hat.beardVisible);
        if (beardVis != _hasBeard)
        {
            _hasBeard = hairVis;
            _beardDirty = true;
        }
        if (_hatDirty)
        {
            if (_hatModel != null)
                Destroy(_hatModel.gameObject);
            if (hat != null && hat.hat != null && hat.shouldBeVisible(false))
            {
                _hatModel = Instantiate(hat.hat, Vector3.zero, Quaternion.identity, transform).transform;
                _hatModel.name = "Hat" + hat.GUID.ToString("N");
                _hatModel.transform.localScale = new Vector3(1f, !this._isLeftHanded || !(GetShouldMirrorLeftHandedModel != null && GetShouldMirrorLeftHandedModel(hat)) ? 1f : -1f, 1f);
                if (hat.shouldDestroyClothingColliders)
                    CallDestroyCollidersInChildren?.Invoke(_hatModel.gameObject, true);
                _hatModel.DestroyRigidbody();
                if (_isVisual && _isMythic && _visualHat != 0)
                {
                    ushort inventoryMythicId = Provider.provider.economyService.getInventoryMythicID(_visualHat);
                    if (inventoryMythicId != 0)
                        ItemTool.applyEffect(_hatModel, inventoryMythicId, EEffectType.HOOK);
                }
                ApplyHairOverride(hat, _hatModel);
            }
            Logger.LogInfo("Hat updated: " + (hat?.GUID.Format() ?? "null") + ".");
        }
        if (_hairDirty)
        {
            if (_hairModel != null)
                Destroy(_hairModel);
            if (_hasHair)
            {
                GameObject hairModel = Resources.Load<GameObject>("Hairs/" + _hair.ToString(CultureInfo.InvariantCulture) + "/Hair");
                if (hairModel != null)
                {
                    _hairModel = Instantiate(hairModel, Vector3.zero, Quaternion.identity, transform).transform;
                    _hairModel.name = "Hair" + _hair.ToString(CultureInfo.InvariantCulture);
                    Transform m0 = _hairModel.Find("Model_0");
                    if (m0 != null)
                        m0.GetComponent<Renderer>().sharedMaterial = _materialHair;
                    _hairModel.DestroyRigidbody();
                }
            }
            Logger.LogInfo("Hair updated: " + _hair.Format() + " (Enabled: " + _hasHair.Format() + ").");
        }
        if (_beardDirty)
        {
            if (_beardModel != null)
                Destroy(_beardModel);
            if (_hasBeard)
            {
                GameObject beardModel = Resources.Load<GameObject>("Beards/" + _beard.ToString(CultureInfo.InvariantCulture) + "/Beard");
                if (beardModel != null)
                {
                    _beardModel = Instantiate(beardModel, Vector3.zero, Quaternion.identity, transform).transform;
                    _beardModel.name = "Beard" + _beard.ToString(CultureInfo.InvariantCulture);
                    Transform m0 = _beardModel.Find("Model_0");
                    if (m0 != null)
                        m0.GetComponent<Renderer>().sharedMaterial = _materialHair;
                    _beardModel.DestroyRigidbody();
                }
            }
            Logger.LogInfo("Beard updated: " + _beard.Format() + " (Enabled: " + _hasBeard.Format() + ").");
        }

        MarkAllDirty(false);
    }

    private void ApplyHairOverride(ItemGearAsset itemAsset, Transform rootModel)
    {
        if (string.IsNullOrEmpty(itemAsset.hairOverride))
            return;
        Transform childRecursive = rootModel.FindChildRecursive(itemAsset.hairOverride);
        if (childRecursive == null)
            Assets.reportError(itemAsset, "cannot find hair override '{0}'", itemAsset.hairOverride);
        else
        {
            Renderer component = childRecursive.GetComponent<Renderer>();
            if (component != null)
            {
                component.sharedMaterial = _materialHair;
            }
        }
    }

    [UsedImplicitly]
    private void Awake()
    {
        _meshes = transform.gameObject.GetComponentsInChildren<Renderer>();
        Logger.LogInfo("Found meshes: " + _meshes.Length);
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
            _materialClothing = null;
        }
        if (_materialHair != null)
        {
            Destroy(_materialHair);
            _materialHair = null;
        }
    }
}

#endif