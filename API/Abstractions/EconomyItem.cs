using DevkitServer.API.Logging;
using SDG.Framework.Devkit;

namespace DevkitServer.API.Abstractions;

/// <summary>
/// Represents and abstracts a costmetic clothing item from the Steam marketplace.
/// </summary>
/// <typeparam name="TAsset">Asset type.</typeparam>
public sealed class EconomyItem<TAsset> : IDirtyable where TAsset : ItemAsset
{
    private const string Source = "ECON ITEM";

    private TAsset? _asset;
    private int _steamId;
    private MythicAsset? _mythicAsset;
    private bool _isMythicDirty;

    /// <summary>
    /// Ensure that the item has <c>Pro</c> in its dat file.
    /// </summary>
    public bool RequirePro { get; set; }

    /// <summary>
    /// Has the items changed since <see cref="IDirtyable.save"/> was last ran.
    /// </summary>
    public bool isDirty { get; set; }

    /// <summary>
    /// Steam marketplace item ID.
    /// </summary>
    public int SteamItem
    {
        get => _steamId;
        set
        {
            if (_steamId == value)
                return;
            _steamId = value;
            _asset = null;
            _isMythicDirty = true;
            if (_steamId != 0)
            {
                try
                {
                    _asset = Assets.find<TAsset>(Provider.provider.economyService.getInventoryItemGuid(value));
                }
                catch (Exception ex)
                {
                    Logger.DevkitServer.LogDebug(Source, ex, $"Error getting a GUID for steam inventory item ID: {value.Format()}. This can be ignored.");
                }
                if (RequirePro && _asset is not { isPro: true } || !RequirePro && _asset == null)
                {
                    _asset = null;
                    _steamId = 0;
                    Logger.DevkitServer.LogDebug(Source, $"Error getting a GUID for steam inventory item ID: {value.Format()}, item was not pro. This can be ignored.");
                }
            }
            if (value != 0 && _asset == null)
            {
                Logger.DevkitServer.LogDebug(Source, $"Error getting a GUID for steam inventory item ID: {value.Format()}. This can be ignored.");
            }
            isDirty = true;
        }
    }

    /// <summary>
    /// Unturned asset.
    /// </summary>
    public TAsset? Asset
    {
        get => _asset;
        set
        {
            if (_asset == value) return;
            if (RequirePro && value is { isPro: false })
            {
                Logger.DevkitServer.LogDebug(Source, $"Error getting steam inventory item ID for asset: {value.Format()}, not Pro. This can be ignored.");
            }
            _asset = value;
            _isMythicDirty = true;
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
                catch (Exception ex)
                {
                    Logger.DevkitServer.LogDebug(Source, ex, $"Error getting steam inventory item ID for asset: {value.Format()}. This can be ignored.");
                }
            }
            if (_steamId == 0 && _asset is { isPro: true })
            {
                _asset = null;
                Logger.DevkitServer.LogDebug(Source, $"Error getting steam inventory item ID for asset: {value.Format()}. This can be ignored.");
            }

            isDirty = true;
        }
    }

    /// <summary>
    /// Asset of the mythic skin.
    /// </summary>
    public MythicAsset? MythicAsset
    {
        get
        {
            if (_isMythicDirty)
            {
                if (_steamId == 0)
                    return null;

                ushort id = Provider.provider.economyService.getInventoryMythicID(_steamId);
                if (id != 0)
                {
                    _mythicAsset = Assets.find(EAssetType.MYTHIC, id) as MythicAsset;
                }
                _isMythicDirty = false;
            }

            return _mythicAsset;
        }
        set
        {
            if (_mythicAsset == value) return;
            _mythicAsset = value;
            _isMythicDirty = false;
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
