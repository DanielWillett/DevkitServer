using DevkitServer.Patches;
using SDG.Framework.Devkit.Tools;
using SDG.Framework.Foliage;

namespace DevkitServer.Multiplayer.Actions;

public sealed class HierarchyActions
{
    public EditorActions EditorActions { get; }
    internal HierarchyActions(EditorActions actions)
    {
        EditorActions = actions;
    }
    public void Subscribe()
    {
#if CLIENT
        if (EditorActions.IsOwner)
        {

        }
#endif
    }

    public void Unsubscribe()
    {
#if CLIENT
        if (EditorActions.IsOwner)
        {

        }
#endif
    }
#if CLIENT

#endif

}