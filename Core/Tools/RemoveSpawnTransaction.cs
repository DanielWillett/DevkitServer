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
        if (Node == null || _isActive)
            return;
        Node.RemoveSpawnFromList(true);
    }

    public void undo()
    {
        if (Node != null)
        {
            Node.AddSpawnToList();
            Node.gameObject.SetActive(Node.ShouldBeVisible);
        }
        _isActive = true;
    }

    public void redo()
    {
        if (Node != null)
        {
            Node.RemoveSpawnFromList(false);
            Node.gameObject.SetActive(false);
        }
        _isActive = false;
    }
}
#endif