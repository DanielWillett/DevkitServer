#if CLIENT
using DevkitServer.API;
using DevkitServer.API.UI;
using HarmonyLib;
using System.Reflection;

namespace DevkitServer.Core.UI.Handlers;
internal class CustomSleekWrapperDestroyHandler : ICustomOnDestroyUIHandler
{
    private const string Source = UIAccessTools.Source + ".SLEEK WRAPPER HANDLER";
    private readonly List<MethodInfo> Implementations = new List<MethodInfo>(3);
    private static readonly Dictionary<Type, InstanceGetter<object, SleekWrapper>?> WrapperGetters = new Dictionary<Type, InstanceGetter<object, SleekWrapper>?>(1);

    public event Action<Type?, object?>? OnDestroyed;
    public bool HasBeenInitialized { get; set; }
    public bool HasOnDestroyBeenInitialized { get; set; }
    public void Patch(Harmony patcher)
    {
        MethodInfo? intlDestroy = typeof(ISleekElement).GetMethod(nameof(ISleekElement.InternalDestroy), BindingFlags.Public | BindingFlags.Instance);
        if (intlDestroy == null)
        {
            Logger.LogWarning("Unable to find method: ISleekElement.InternalDestroy.", method: Source);
            return;
        }

        Implementations.Clear();
        foreach (Type type in Accessor.GetTypesSafe(new Assembly[] { typeof(Provider).Assembly, typeof(ISleekElement).Assembly })
                     .Where(x => x is { IsAbstract: false, IsClass: true } &&
                                 x.Name.IndexOf("Proxy", StringComparison.Ordinal) != -1 &&
                                 typeof(ISleekElement).IsAssignableFrom(x)))
        {
            MethodInfo? impl = Accessor.GetImplementedMethod(type, intlDestroy);

            if (impl == null)
                Logger.LogWarning($"Unable to find implemented method: {type.Format()}.InternalDestroy.", method: Source);
            else Implementations.Add(impl);
        }

        for (int i = 0; i < Implementations.Count; ++i)
        {
            try
            {
                patcher.Patch(Implementations[i], prefix: new HarmonyMethod(Accessor.GetMethod(OnDestroyInvoker)!));
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[{Source}] Unable to patch: {Implementations[i].Format()}.", method: Source);
                Logger.LogError(ex, method: Source);
            }
        }
    }
    public void Unpatch(Harmony patcher)
    {
        foreach (MethodInfo method in Implementations)
        {
            try
            {
                patcher.Unpatch(method, Accessor.GetMethod(OnDestroyInvoker)!);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Unable to unpatch: {method.Format()}.", method: Source);
                Logger.LogError(ex, method: Source);
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
            Logger.LogWarning($"Unable to get sleek wrapper from proxy implementation: {type.Format()}.", method: Source);
            return;
        }

        __instance = getter(__instance);
        if (__instance == null)
        {
            Logger.LogWarning($"Sleek wrapper not available from proxy implementation: {type.Format()}.", method: Source);
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

        Logger.LogWarning($"Failed to find property or field for SleekWrapper in proxy type: {type.Format()}.", method: Source);
        return null;
    }
}
#endif