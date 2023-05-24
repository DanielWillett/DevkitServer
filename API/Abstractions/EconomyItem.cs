using SDG.Framework.Devkit;

namespace DevkitServer.API.Abstractions;
public sealed class EconomyItem<TAsset> : IDirtyable where TAsset : ItemClothingAsset
{
    private TAsset? _asset;
    private int _steamId;
    private MythicAsset? _mythic;
    private bool mythicDirty;

    public bool RequirePro { get; set; }
    public bool isDirty { get; set; }
    public int SteamItem
    {
        get => _steamId;
        set
        {
            if (_steamId == value)
                return;
            _steamId = value;
            _asset = null;
            mythicDirty = true;
            if (_steamId != 0)
            {
                try
                {
                    _asset = Assets.find<TAsset>(Provider.provider.economyService.getInventoryItemGuid(value));
                }
#if DEBUG
                catch (Exception ex)
#else
                catch
#endif
                {
#if DEBUG
                    Logger.LogDebug($"Error getting a GUID for steam inventory item ID: {value.Format()}. This can be ignored.");
                    Logger.LogError(ex);
#endif
                }
                if (RequirePro && _asset is not { isPro: true } || !RequirePro && _asset == null)
                {
                    _asset = null;
                    _steamId = 0;
                    Logger.LogDebug($"Error getting a GUID for steam inventory item ID: {value.Format()}, item was not pro. This can be ignored.");
                }
            }
            if (value != 0 && _asset == null)
            {
                Logger.LogDebug($"Error getting a GUID for steam inventory item ID: {value.Format()}. This can be ignored.");
            }
            isDirty = true;
        }
    }
    public TAsset? Asset
    {
        get => _asset;
        set
        {
            if (_asset == value) return;
            if (RequirePro && value is { isPro: false })
            {
                Logger.LogDebug($"Error getting steam inventory item ID for asset: {value.Format()}, not Pro. This can be ignored.");
            }
            _asset = value;
            mythicDirty = true;
            if (_asset == null)
            {
                _steamId = 0;
            }
            else
            {
                try
                {
                    _steamId = Provider.provider.economyService.getInventorySkinID(value!.id);
                }
#if DEBUG
                catch (Exception ex)
#else
                catch
#endif
                {
#if DEBUG
                    Logger.LogDebug($"Error getting steam inventory item ID for asset: {value.Format()}. This can be ignored.");
                    Logger.LogError(ex);
#endif
                }
            }
            if (_steamId == 0 && _asset is { isPro: true })
            {
                _asset = null;
                Logger.LogDebug($"Error getting steam inventory item ID for asset: {value.Format()}. This can be ignored.");
            }

            isDirty = true;
        }
    }

    public MythicAsset? Mythic
    {
        get
        {
            if (mythicDirty)
            {
                if (_steamId == 0)
                    return null;

                ushort id = Provider.provider.economyService.getInventoryMythicID(_steamId);
                if (id != 0)
                {
                    _mythic = Assets.find(EAssetType.MYTHIC, id) as MythicAsset;
                }
                mythicDirty = false;
            }

            return _mythic;
        }
        set
        {
            if (_mythic == value) return;
            _mythic = value;
            mythicDirty = false;
            isDirty = true;
        }
    }
    void IDirtyable.save() => isDirty = false;

    public EconomyItem() { }

    public EconomyItem(TAsset asset)
    {
        Asset = asset;
    }
    public EconomyItem(int steamItemId)
    {
        SteamItem = steamItemId;
    }

    public override string ToString()
    {
        if (Asset != null)
            return "SteamID: " + _steamId + " | " + Asset.getTypeNameAndIdDisplayString() + " {" + Asset.GUID.ToString("N") + "}";
        if (_steamId != 0)
            return "SteamID: " + _steamId;
        return "No Item Selected.";
    }
}
