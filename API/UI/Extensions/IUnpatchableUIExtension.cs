namespace DevkitServer.API.UI.Extensions;

/// <summary>
/// Any patches using harmony should implement this. Allows the last extension left when the manager is being disposed to unpatch any static patches.
/// </summary>
public interface IUnpatchableUIExtension
{
    /// <summary>
    /// Called on the last extension left when the manager is being disposed.
    /// </summary>
    public void Unpatch();
}