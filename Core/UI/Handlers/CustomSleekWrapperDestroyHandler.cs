#if CLIENT
using DanielWillett.ReflectionTools;
using DevkitServer.API.UI;
using HarmonyLib;
using System.Reflection;

namespace DevkitServer.Core.UI.Handlers;
internal class CustomSleekWrapperDestroyHandler : ICustomOnDestroyUIHandler
{
    private const string Source = UIAccessTools.Source + ".SLEEK WRAPPER HANDLER";
    private readonly List<MethodInfo> _implementations = new List<MethodInfo>(3);
    private static readonly Dictionary<Type, InstanceGetter<object, SleekWrapper>?> WrapperGetters = new Dictionary<Type, InstanceGetter<object, SleekWrapper>?>(1);

    public event Action<Type?, object?>? OnDestroyed;
    public bool HasBeenInitialized { get; set; }
    public bool HasOnDestroyBeenInitialized { get; set; }
    public void Patch(Harmony patcher)
    {
        MethodInfo? intlDestroy = typeof(ISleekElement).GetMethod(nameof(ISleekElement.InternalDestroy), BindingFlags.Public | BindingFlags.Instance);
        if (intlDestroy == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"Unable to find method: {FormattingUtil.FormatMethod(typeof(void), typeof(ISleekElement), nameof(ISleekElement.InternalDestroy), arguments: Type.EmptyTypes)}.");
            return;
        }

        _implementations.Clear();
        foreach (Type type in Accessor.GetTypesSafe(new Assembly[] { typeof(Provider).Assembly, typeof(ISleekElement).Assembly })
                     .Where(x => x is { IsAbstract: false, IsClass: true } &&
                                 x.Name.IndexOf("Proxy", StringComparison.Ordinal) != -1 &&
                                 typeof(ISleekElement).IsAssignableFrom(x)))
        {
            MethodInfo? impl = Accessor.GetImplementedMethod(type, intlDestroy);

            if (impl == null)
                Logger.DevkitServer.LogWarning(Source, $"Unable to find implemented method: {FormattingUtil.FormatMethod(typeof(void), type, nameof(ISleekElement.InternalDestroy), arguments: Type.EmptyTypes)}.");
            else _implementations.Add(impl);
        }

        for (int i = 0; i < _implementations.Count; ++i)
        {
            try
            {
                patcher.Patch(_implementations[i], prefix: new HarmonyMethod(Accessor.GetMethod(OnDestroyInvoker)!));
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogWarning(Source, ex, $"[{Source}] Unable to patch: {_implementations[i].Format()}.");
            }
        }
    }
    public void Unpatch(Harmony patcher)
    {
        foreach (MethodInfo method in _implementations)
        {
            try
            {
                patcher.Unpatch(method, Accessor.GetMethod(OnDestroyInvoker)!);
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogWarning(Source, ex, $"Unable to unpatch: {method.Format()}.");
            }
        }
    }
    private static void OnDestroyInvoker(object __instance)
    {
        Type type = __instance.GetType();
        if (!WrapperGetters.TryGetValue(type, out InstanceGetter<object, SleekWrapper>? getter))
        {
            getter = CreateGetter(type);
            WrapperGetters.Add(type, getter);
        }

        if (getter == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"Unable to get sleek wrapper from proxy implementation: {type.Format()}.");
            return;
        }

        __instance = getter(__instance);
        if (__instance == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"Sleek wrapper not available from proxy implementation: {type.Format()}.");
            return;
        }

        type = __instance.GetType();

        if (!UIAccessTools.TryGetUITypeInfo(type, out UITypeInfo typeInfo) || typeInfo.CustomOnDestroy is not CustomSleekWrapperDestroyHandler customSleekHandler)
            return;

        customSleekHandler.OnDestroyed?.Invoke(null, __instance);
    }
    private static InstanceGetter<object, SleekWrapper>? CreateGetter(Type type)
    {
        FieldInfo? field = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(x => x.FieldType == typeof(SleekWrapper));

        if (field != null)
            return Accessor.GenerateInstanceGetter<SleekWrapper>(type, field.Name);

        PropertyInfo? property = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(x => x.PropertyType == typeof(SleekWrapper));

        if (property != null)
            return Accessor.GenerateInstancePropertyGetter<SleekWrapper>(type, property.Name);

        Logger.DevkitServer.LogWarning(Source, $"Failed to find property or field for SleekWrapper in proxy type: {type.Format()}.");
        return null;
    }
}
#endif