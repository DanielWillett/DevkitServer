using SDG.Framework.Utilities;

namespace DevkitServer.Multiplayer.Actions;

[EarlyTypeInit]
public class ActionSettings : IDisposable
{
    private static readonly int SettingsFlagLength;
    public EditorActions EditorActions { get; }

    internal static readonly Pool<ActionSettingsCollection> CollectionPool = new Pool<ActionSettingsCollection>();
    private readonly ActionSettingsCollection?[] _activeSettings = new ActionSettingsCollection?[SettingsFlagLength];
    static ActionSettings()
    {
        int max = -1;
        ActionSetting[] actions = (ActionSetting[])Enum.GetValues(typeof(ActionSetting));
        const int size = sizeof(ActionSetting) * 8;
        for (int i = 0; i < actions.Length; ++i)
        {
            ActionSetting c = actions[i];
            if (c is ActionSetting.Extended or ActionSetting.None) continue;
            for (int b = max + 1; b < size; ++b)
            {
                if ((c & (ActionSetting)(1 << b)) != 0)
                    max = b;
            }
        }

        SettingsFlagLength = max + 1;
        Logger.LogDebug($"[EDITOR ACTIONS] ActionSetting flag length: {SettingsFlagLength.Format()}/{size.Format()} bits.");
        ListPool<ActionSettingsCollection>.warmup((uint)(Provider.isServer ? 16 : 2));
#if CLIENT
        CollectionPool.warmup((uint)(SettingsFlagLength * 4));
#endif
    }
    internal ActionSettings(EditorActions actions)
    {
        EditorActions = actions;
    }
    public ActionSettingsCollection? GetSettings(ActionSetting value)
    {
        for (int i = 0; i < SettingsFlagLength; ++i)
        {
            if ((value & (ActionSetting)(1 << i)) != 0)
                return i < 0 || i >= SettingsFlagLength ? null : _activeSettings[i];
        }

        return null;
    }
    internal void SetSettings(ActionSettingsCollection collection)
    {
        for (int i = 0; i < SettingsFlagLength; ++i)
        {
            if ((collection.Flags & (ActionSetting)(1 << i)) != 0)
            {
                ref ActionSettingsCollection? col = ref _activeSettings[i];
                if (col != null)
                {
                    bool stillUsed = false;
                    for (int j = 0; j < SettingsFlagLength; ++j)
                    {
                        if (i != j && _activeSettings[j] == col)
                        {
                            stillUsed = true;
                            break;
                        }
                    }
                    if (!stillUsed)
                        CollectionPool.release(col);
                }
                col = collection;
            }
        }
    }

    void IDisposable.Dispose()
    {
        for (int i = 0; i < SettingsFlagLength; ++i)
        {
            if (_activeSettings[i] is { } collection)
            {
                _activeSettings[i] = null;
                for (int j = i + 1; j < SettingsFlagLength; ++j)
                {
                    if (_activeSettings[j] == collection)
                        _activeSettings[j] = null;
                }
                CollectionPool.release(collection);
            }
        }
    }
}
