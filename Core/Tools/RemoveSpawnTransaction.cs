#if CLIENT
using DevkitServer.API.Devkit.Spawns;
using SDG.Framework.Devkit.Transactions;

namespace DevkitServer.Core.Tools;

public class RemoveSpawnTransaction(BaseSpawnpointNode node) : IDevkitTransaction
{
    protected bool _isActive;
    bool IDevkitTransaction.delta => true;
    public BaseSpawnpointNode Node { get; } = node;

    public void begin() => redo();

    public void end() { }

    public void forget()
    {
        if (node == null || _isActive)
            return;
        node.RemoveSpawnFromList(true);
    }

    public void undo()
    {
        if (node != null)
        {
            node.AddSpawnToList();
            node.gameObject.SetActive(node.ShouldBeVisible);
        }
        _isActive = true;
    }

    public void redo()
    {
        if (node != null)
        {
            node.RemoveSpawnFromList(false);
            node.gameObject.SetActive(false);
        }
        _isActive = false;
    }
}
#endif