#if CLIENT
using DevkitServer.API.UI;
using DevkitServer.Patches;
using DevkitServer.Players.UI;
using HarmonyLib;
using System.Reflection;

namespace DevkitServer.Players.UI.Handlers;
internal class CustomSleekWrapperDestroyHandler : ICustomOnDestroy
{
    private const string Source = UIAccessTools.Source + ".SLEEK WRAPPER HANDLER";

    private static CustomSleekWrapperDestroyHandler? _instance;

    public event Action<Type?, object?>? OnDestroy;
    public bool HasBeenInitialized { get; set; }
    public bool HasOnDestroyBeenInitialized { get; set; }

    public CustomSleekWrapperDestroyHandler()
    {
        _instance = this;
    }
    public void Patch()
    {
        MethodInfo? method = typeof(SleekWrapper).GetMethod(nameof(SleekWrapper.destroy), BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (method == null)
        {
            Logger.LogWarning("Unable to find method: SleekWrapper.destroy.", method: Source);
            return;
        }

        PatchesMain.Patcher.Patch(method, prefix: new HarmonyMethod(new Action<object>(OnDestroyInvoker).Method));
    }

    public void Unpatch()
    {
        MethodInfo? method = typeof(SleekWrapper).GetMethod(nameof(SleekWrapper.destroy), BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (method != null)
            PatchesMain.Patcher.Unpatch(method, new Action<object>(OnDestroyInvoker).Method);
    }

    private static void OnDestroyInvoker(object __instance)
    {
        Type type = __instance.GetType();
        if (UIAccessTools.TryGetUITypeInfo(type, out UITypeInfo typeInfo) && typeInfo.CustomOnDestroy is not CustomSleekWrapperDestroyHandler)
            return;
        _instance?.OnDestroy?.Invoke(null, __instance);
    }
}
#endif