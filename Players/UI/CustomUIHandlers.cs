using DevkitServer.API.Extensions.UI;

#if CLIENT
namespace DevkitServer.Players.UI;

public interface ICustomUIHandler
{
    bool HasBeenInitialized { get; internal set; }
    void Patch(UITypeInfo typeInfo, UIExtensionParentTypeInfo parentTypeInfo);
    void Unpatch(UITypeInfo typeInfo, UIExtensionParentTypeInfo parentTypeInfo);
}

public interface ICustomOnOpen : ICustomUIHandler
{
    event Action<object?> OnOpened;
}
public interface ICustomOnClose : ICustomUIHandler
{
    event Action<object?> OnClose;
}

public interface ICustomOnDestroy : ICustomUIHandler
{
    event Action<object?> OnDestroy;
}

public interface ICustomOnInitialize : ICustomUIHandler
{
    event Action<object?> OnInitialize;
}
#endif