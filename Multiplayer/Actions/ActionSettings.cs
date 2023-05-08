using SDG.Framework.Utilities;

namespace DevkitServer.Multiplayer.Actions;
public class ActionSettings
{
    private const int SettingsFlagLength = 32;
    public EditorActions EditorActions { get; }

    private readonly ActionSettingsCollection?[] _activeSettings = new ActionSettingsCollection?[SettingsFlagLength];
    internal static readonly Pool<ActionSettingsCollection> CollectionPool = new Pool<ActionSettingsCollection>();
    static ActionSettings()
    {
        ListPool<ActionSettingsCollection>.warmup((uint)(Provider.isServer ? 16 : 2));
#if CLIENT
        CollectionPool.warmup(SettingsFlagLength * 4);
#endif
    }
    internal ActionSettings(EditorActions actions)
    {
        EditorActions = actions;
    }
    public ActionSettingsCollection? GetSettings(ActionSetting value)
    {
        for (int i = 0; i < SettingsFlagLength; ++i)
            if ((value & (ActionSetting)(1 << i)) != 0)
                return i is < 0 or >= SettingsFlagLength ? null : _activeSettings[i];

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
}
