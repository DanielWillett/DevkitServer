using DevkitServer.Models;
using SDG.Framework.Devkit.Interactable;
#if CLIENT
using DevkitServer.Core.Tools;
using SDG.Framework.Devkit.Transactions;
#endif

namespace DevkitServer.API.Devkit.Spawns;

public abstract class BaseSpawnpointNode : MonoBehaviour, ISpawnpointNode, IDevkitInteractableBeginSelectionHandler, IDevkitInteractableEndSelectionHandler, IDevkitSelectionTransformableHandler
#if CLIENT
    , IDevkitSelectionDeletableHandler
#endif
{
    public bool IsSelected { get; private set; }
    public bool IsAdded { get; internal set; } = true;
    public Collider Collider { get; protected set; } = null!;
    public Renderer? Renderer { get; protected set; }
    internal bool IgnoreDestroy { get; set; }
    public abstract bool ShouldBeVisible { get; }
    public virtual Color Color
    {
        set
        {
            if (Renderer != null)
                Renderer.material.color = value;
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
    void IDevkitInteractableBeginSelectionHandler.beginSelection(InteractionData data)
    {
        IsSelected = true;
    }
    void IDevkitInteractableEndSelectionHandler.endSelection(InteractionData data)
    {
        IsSelected = false;
    }
#if CLIENT
    void IDevkitSelectionDeletableHandler.Delete(ref bool destroy)
    {
        destroy = false;
        if (DevkitServerModule.IsEditing)
            RemoveSpawnFromList();
        else
            DevkitTransactionManager.recordTransaction(new RemoveSpawnTransaction(this));
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

    public abstract string Format(ITerminalFormatProvider provider);

    void IDevkitSelectionTransformableHandler.transformSelection() => Transform();
}
public abstract class RegionalSpawnpointNode : BaseSpawnpointNode
{
    public RegionIdentifier Region { get; internal set; }
}

public abstract class IndexedSpawnpointNode : BaseSpawnpointNode
{
    public int Index { get; internal set; }
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