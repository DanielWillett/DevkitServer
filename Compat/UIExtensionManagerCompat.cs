using DanielWillett.UITools;
using DanielWillett.UITools.API.Extensions;
using DevkitServer.API;
using DevkitServer.API.UI.Extensions;
using DevkitServer.Plugins;
using System.Reflection;
using DanielWillett.UITools.Util;
using SDG.Framework.Modules;
using Module = SDG.Framework.Modules.Module;

namespace DevkitServer.Compat;
internal class UIExtensionManagerCompat : IUIExtensionManager
{
    private List<DanielWillett.UITools.API.Extensions.UIExtensionInfo>? _ext;
    private IReadOnlyList<DanielWillett.UITools.API.Extensions.UIExtensionInfo>? _extensionsRo;
    private static UIExtensionManagerCompat? _instance;

    // ReSharper disable once InconsistentlySynchronizedField
    public static IUIExtensionManager? Manager => _instance;
    public bool DebugLogging { get; set; }
    public void Initialize() { }
    public IReadOnlyList<DanielWillett.UITools.API.Extensions.UIExtensionInfo> Extensions
    {
        get
        {
            if (_extensionsRo == null)
            {
                lock (this)
                {
                    if (_extensionsRo == null)
                    {
                        _ext = UIExtensionManager.Extensions
                            .Select(x => new DanielWillett.UITools.API.Extensions.UIExtensionInfo(x.ImplementationType, x.ParentType, x.Priority, x.Plugin is ModulePlugin mp ? mp.Module : DevkitServerModule.Module))
                            .ToList();
                        _extensionsRo = _ext.AsReadOnly();
                    }
                }
            }

            return _extensionsRo;
        }
    }
    internal static void Init()
    {
        if (_instance != null)
            return;

        if (Interlocked.CompareExchange(ref _instance, new UIExtensionManagerCompat(), null) != null)
            return;
        
        UIAccessor.ManageHarmonyDebugLog = false;
        ModuleHook.onModulesInitialized += OnModulesLoaded;
    }

    private static void OnModulesLoaded()
    {
        ModuleHook.onModulesInitialized -= OnModulesLoaded;

        lock (_instance!)
        {
            UIAccessor.Init();
            UnturnedUIToolsNexus.UIExtensionManager = _instance;
            UnturnedUIToolsNexus.Initialize();
        }
    }

    internal static void RebuildLists()
    {
        Init();
        lock (_instance!)
        {
            _instance._extensionsRo = null!;
            _instance._ext = null!;
        }
    }
    public T? GetInstance<T>() where T : class => UIExtensionManager.GetInstance<T>();
    public T? GetInstance<T>(object vanillaUIInstance) where T : class => UIExtensionManager.GetInstance<T>(vanillaUIInstance);
    public void RegisterExtension(Type extensionType, Type parentType, Module module)
    {
        IDevkitServerPlugin plugin = PluginLoader.FindOrCreateModulePlugin(module);
        UIExtensionManager.RegisterExtension(new API.UI.Extensions.UIExtensionInfo(extensionType, parentType, extensionType.GetPriority(), plugin));
    }
    public void RegisterFromModuleAssembly(Assembly assembly, Module module)
    {
        if (module == DevkitServerModule.Module)
            return;
        IDevkitServerPlugin plugin = PluginLoader.FindOrCreateModulePlugin(module);
        List<Type> types = Accessor.GetTypesSafe(assembly, true);
        foreach (Type type in types)
        {
            if (Attribute.GetCustomAttribute(type, typeof(DanielWillett.UITools.API.Extensions.UIExtensionAttribute)) is not DanielWillett.UITools.API.Extensions.UIExtensionAttribute attribute)
                continue;

            LoadPriorityAttribute? priority = Attribute.GetCustomAttribute(type, typeof(LoadPriorityAttribute)) as LoadPriorityAttribute;

            API.UI.Extensions.UIExtensionInfo info = new API.UI.Extensions.UIExtensionInfo(type, attribute.ParentType, priority == null ? 0 : priority.Priority, plugin)
            {
                SuppressUIExtensionParentWarning = attribute.SuppressUIExtensionParentWarning || typeof(DanielWillett.UITools.API.Extensions.UIExtension).IsAssignableFrom(type)
            };
            
            if (UIExtensionManager.RegisterExtension(info))
            {
                UIExtensionManager.LogDebug($"Registered UI extension: {type.Format()}.", plugin, type.Assembly);
            }
        }
    }

    public static void InvokeOnOpened(object instantiation, API.UI.Extensions.UIExtensionInfo info)
    {
        if (instantiation is not DanielWillett.UITools.API.Extensions.UIExtension extension)
            return;

        try
        {
            extension.InvokeOnOpened();
        }
        catch (Exception ex)
        {
            UIExtensionManager.LogError($"Error invoking OnOpened from {instantiation.GetType().Format()}.", info.Plugin, info.Assembly);
            UIExtensionManager.LogError(ex, info.Plugin, info.Assembly);
        }
    }
    public static void InvokeOnClosed(object instantiation, API.UI.Extensions.UIExtensionInfo info)
    {
        if (instantiation is not DanielWillett.UITools.API.Extensions.UIExtension extension)
            return;

        try
        {
            extension.InvokeOnClosed();
        }
        catch (Exception ex)
        {
            UIExtensionManager.LogError($"Error invoking OnClosed from {instantiation.GetType().Format()}.", info.Plugin, info.Assembly);
            UIExtensionManager.LogError(ex, info.Plugin, info.Assembly);
        }
    }
    public static bool IsAssignableFromUIExtension(Type type) => typeof(DanielWillett.UITools.API.Extensions.UIExtension).IsAssignableFrom(type);
}
