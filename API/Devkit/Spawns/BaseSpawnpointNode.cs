using DevkitServer.Models;
using DevkitServer.Multiplayer.Actions;
using SDG.Framework.Devkit.Interactable;
#if CLIENT
using DevkitServer.Core.Tools;
using HighlightingSystem;
using SDG.Framework.Devkit.Transactions;
#endif

namespace DevkitServer.API.Devkit.Spawns;

public abstract class BaseSpawnpointNode : MonoBehaviour, ISpawnpointNode, IDevkitSelectionTransformableHandler
#if CLIENT
    , IDevkitSelectionDeletableHandler, IDevkitHighlightHandler, IDevkitInteractableBeginSelectionHandler, IDevkitInteractableEndSelectionHandler
#endif
{
    private NetId64 _netId;

    public bool IsSelected { get; private set; }
    public bool IsAdded { get; internal set; } = true;
    public Collider Collider { get; protected set; } = null!;
    public Renderer? Renderer { get; protected set; }
    public abstract bool ShouldBeVisible { get; }
    public abstract SpawnType SpawnType { get; }
    public virtual Color Color
    {
        set
        {
            if (Renderer != null)
                Renderer.material.color = value;
        }
    }

    public NetId64 NetId
    {
        get
        {
            if (!DevkitServerModule.IsEditing)
                return NetId64.Invalid;

            return _netId.IsNull() ? _netId = GetNetId() : _netId;
        }
    }

    [UsedImplicitly]
    private void Start()
    {
        Renderer = GetComponent<Renderer>();
        Init();
        SetupCollider();

        if (Collider != null)
        {
            Collider.isTrigger = true;
            Collider.tag = "Logic";
        }
    }
    public void AddSpawnToList()
    {
        Logger.DevkitServer.LogDebug(GetType().Name, $"Adding {Format(FormattingUtil.FormatProvider)}.");
        if (IsAdded)
            return;

        IsAdded = Add();
    }

    public void RemoveSpawnFromList()
    {
        Logger.DevkitServer.LogDebug(GetType().Name, $"Removing {Format(FormattingUtil.FormatProvider)}.");
        if (!IsAdded)
            return;

        Remove();
        IsAdded = false;
    }
#if CLIENT
    void IDevkitInteractableBeginSelectionHandler.beginSelection(InteractionData data)
    {
        IsSelected = true;
        HighlighterUtil.Highlight(transform, Color.yellow);
    }
    void IDevkitInteractableEndSelectionHandler.endSelection(InteractionData data)
    {
        IsSelected = false;
        HighlighterUtil.Unhighlight(transform);
    }
    void IDevkitSelectionDeletableHandler.Delete(ref bool destroy)
    {
        if (DevkitServerModule.IsEditing)
            RemoveSpawnFromList();
        else
        {
            DevkitTransactionManager.recordTransaction(new RemoveSpawnTransaction(this));
            destroy = false;
        }
    }
    void IDevkitHighlightHandler.OnHighlight(Highlighter highlighter)
    {
        highlighter.overlay = true;
    }
#endif
    protected virtual void SetupCollider()
    {
        BoxCollider collider = transform.GetOrAddComponent<BoxCollider>();
        Collider = collider;
        collider.size = new Vector3(1f, 1f, 1f);
        collider.center = new Vector3(0f, 0f, 0f);
    }
    protected virtual void Init() { }
    protected abstract bool Add();
    protected abstract bool Remove();
    protected abstract void Transform();
    protected abstract NetId64 GetNetId();

    /// <summary>
    /// Double checks the stored index or region, just in case an external source modified it without calling the right events.
    /// </summary>
    /// <returns><see langword="false"/> if the spawnpoint couldn't be found in the game's records at all.</returns>
    public abstract bool SanityCheck();
    public abstract string Format(ITerminalFormatProvider provider);

    void IDevkitSelectionTransformableHandler.transformSelection() => Transform();
}
public abstract class RegionalSpawnpointNode : BaseSpawnpointNode
{
    protected RegionIdentifier RegionIntl;
    public RegionIdentifier Region
    {
        get => RegionIntl;
        internal set => RegionIntl = value;
    }
}

public abstract class IndexedSpawnpointNode : BaseSpawnpointNode
{
    protected int IndexIntl;
    public int Index
    {
        get => IndexIntl;
        internal set => IndexIntl = value;
    }
}

public interface ISpawnpointNode : ITerminalFormattable
{
    // ReSharper disable InconsistentNaming
    GameObject gameObject { get; }
    Transform transform { get; }

    // ReSharper restore InconsistentNaming
    bool IsSelected { get; }
    bool IsAdded { get; }
    bool ShouldBeVisible { get; }
    Collider Collider { get; }
    Renderer? Renderer { get; }
    Color Color { set; }
}
public interface IRotatableNode : ISpawnpointNode
{
    Renderer? ArrowRenderer { get; }
}