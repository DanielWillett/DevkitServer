using SDG.Framework.Devkit;

namespace DevkitServer.Levels;
internal sealed class DirtyManagerState
{
    public List<IDirtyable> States = new List<IDirtyable>(DirtyManager.dirty.Count);
    private DirtyManagerState() { }
    public static DirtyManagerState Create()
    {
        DirtyManagerState state = new DirtyManagerState();
        state.States.AddRange(DirtyManager.dirty);
        Logger.DevkitServer.LogDebug(nameof(DirtyManagerState), $"Backed up states of {state.States.Count.Format()} {typeof(IDirtyable).Format()}(s).");
        return state;
    }

    public void Apply()
    {
        foreach (IDirtyable dirty in States)
        {
            dirty.isDirty = true;
            Logger.DevkitServer.LogDebug(nameof(DirtyManagerState), $"Recovered dirty state of {dirty.Format()}.");
            if (!DirtyManager.dirty.Contains(dirty))
                DirtyManager.markDirty(dirty);
        }

        States.Clear();
    }
}
