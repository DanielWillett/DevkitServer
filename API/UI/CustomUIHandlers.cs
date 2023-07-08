#if CLIENT
namespace DevkitServer.API.UI;

public interface ICustomUIHandler
{
    bool HasBeenInitialized { get; internal set; }
    void Patch();
    void Unpatch();
}

public interface ICustomOnOpen : ICustomUIHandler
{
    event Action<Type?, object?> OnOpened;
    bool HasOnOpenBeenInitialized { get; internal set; }
}
public interface ICustomOnClose : ICustomUIHandler
{
    event Action<Type?, object?> OnClose;
    bool HasOnCloseBeenInitialized { get; internal set; }
}

public interface ICustomOnDestroy : ICustomUIHandler
{
    event Action<Type?, object?> OnDestroy;
    bool HasOnDestroyBeenInitialized { get; internal set; }
}

public interface ICustomOnInitialize : ICustomUIHandler
{
    event Action<Type?, object?> OnInitialize;
    bool HasOnInitializeBeenInitialized { get; internal set; }
}
#endif