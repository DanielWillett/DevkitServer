#if CLIENT
namespace DevkitServer.API.Devkit;
public interface IDevkitSelectionDeletableHandler
{
    void Delete(ref bool destroy);
}
#endif