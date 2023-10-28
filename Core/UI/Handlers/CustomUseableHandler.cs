#if CLIENT
using DevkitServer.API;
using DevkitServer.API.UI;
using HarmonyLib;
using System.Reflection;

namespace DevkitServer.Core.UI.Handlers;
internal class CustomUseableHandler : ICustomOnDestroyUIHandler, ICustomOnInitializeUIHandler
{
    private const string Source = UIAccessTools.Source + ".USEABLES";
    public event Action<Type?, object?>? OnDestroyed;
    public event Action<Type?, object?>? OnInitialized;
    public bool HasBeenInitialized { get; set; }
    public bool HasOnDestroyBeenInitialized { get; set; }
    public bool HasOnInitializeBeenInitialized { get; set; }
    public Dictionary<MethodInfo, List<Type>> AllowedEquipTypes { get; } = new Dictionary<MethodInfo, List<Type>>(8);
    public Dictionary<MethodInfo, List<Type>> AllowedDequipTypes { get; } = new Dictionary<MethodInfo, List<Type>>(8);
    public void Patch(Harmony patcher)
    {
        foreach (UITypeInfo useableInfo in UIAccessTools.TypeInfo.Values.Where(x => typeof(Useable).IsAssignableFrom(x.Type)))
        {
            Type? type = useableInfo.Type;
            bool equip = false, dequip = false;
            for (; (!equip || !dequip) && typeof(Useable).IsAssignableFrom(type); type = type.BaseType)
            {
                if (!equip)
                {
                    MethodInfo? method = useableInfo.Type.GetMethod(nameof(Useable.equip), BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance, null, Array.Empty<Type>(), null);
                    if (method != null)
                    {
                        if (!AllowedEquipTypes.TryGetValue(method, out List<Type> allowedTypes))
                        {
                            allowedTypes = new List<Type>(1) { useableInfo.Type };
                            AllowedEquipTypes.Add(method, allowedTypes);
                        }
                        else allowedTypes.Add(useableInfo.Type);

                        equip = true;
                    }
                }
                if (!dequip)
                {
                    MethodInfo? method = useableInfo.Type.GetMethod(nameof(Useable.dequip), BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance, null, Array.Empty<Type>(), null);
                    if (method != null)
                    {
                        if (!AllowedDequipTypes.TryGetValue(method, out List<Type> allowedTypes))
                        {
                            allowedTypes = new List<Type>(1) { useableInfo.Type };
                            AllowedDequipTypes.Add(method, allowedTypes);
                        }
                        else allowedTypes.Add(useableInfo.Type);

                        dequip = true;
                    }
                }
            }
        }
        HarmonyMethod equipPatch = new HarmonyMethod(Accessor.GetMethod(EquipPostfix)!);
        HarmonyMethod dequipPatch = new HarmonyMethod(Accessor.GetMethod(DequipPostfix)!);

        foreach (MethodInfo method in AllowedEquipTypes.Keys)
        {
            try
            {
                patcher.Patch(method, postfix: equipPatch);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to patch {method.Format()}.", method: Source);
                Logger.LogError(ex, method: Source);
            }
        }
        foreach (MethodInfo method in AllowedDequipTypes.Keys)
        {
            try
            {
                patcher.Patch(method, postfix: dequipPatch);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to patch {method.Format()}.", method: Source);
                Logger.LogError(ex, method: Source);
            }
        }
    }

    public void Unpatch(Harmony patcher)
    {
        MethodInfo equipPatch = new Action<Useable, MethodInfo, bool>(EquipPostfix).Method;
        MethodInfo dequipPatch = new Action<Useable, MethodInfo, bool>(DequipPostfix).Method;
        foreach (MethodInfo method in AllowedEquipTypes.Keys)
        {
            patcher.Unpatch(method, equipPatch);
        }
        foreach (MethodInfo method in AllowedDequipTypes.Keys)
        {
            patcher.Unpatch(method, dequipPatch);
        }
    }
    private static void EquipPostfix(Useable __instance, MethodInfo __originalMethod, bool __runOriginal)
    {
        Type type = __instance.GetType();
        if (!__runOriginal || !__instance.channel.IsLocalPlayer)
            return;

        if (!UIAccessTools.TryGetUITypeInfo(type, out UITypeInfo typeInfo) || typeInfo.CustomOnInitialize is not CustomUseableHandler customUseableHandler)
            return;

        customUseableHandler.OnInitialized?.Invoke(null, __instance);
    }
    private static void DequipPostfix(Useable __instance, MethodInfo __originalMethod, bool __runOriginal)
    {
        Type type = __instance.GetType();
        if (!__runOriginal || !__instance.channel.IsLocalPlayer)
            return;

        if (!UIAccessTools.TryGetUITypeInfo(type, out UITypeInfo typeInfo) || typeInfo.CustomOnDestroy is not CustomUseableHandler customUseableHandler)
            return;

        customUseableHandler.OnDestroyed?.Invoke(null, __instance);
    }
}
#endif