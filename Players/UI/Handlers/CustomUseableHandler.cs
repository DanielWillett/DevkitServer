#if CLIENT
using DevkitServer.API.UI;
using DevkitServer.Patches;
using DevkitServer.Players.UI;
using HarmonyLib;
using System.Reflection;

namespace DevkitServer.Players.UI.Handlers;
internal class CustomUseableHandler : ICustomOnDestroy, ICustomOnInitialize
{
    private const string Source = UIAccessTools.Source + ".USEABLES";
    private static CustomUseableHandler? _instance;
    public event Action<Type?, object?>? OnDestroy;
    public event Action<Type?, object?>? OnInitialize;
    public bool HasBeenInitialized { get; set; }
    public bool HasOnDestroyBeenInitialized { get; set; }
    public bool HasOnInitializeBeenInitialized { get; set; }
    public Dictionary<MethodInfo, List<Type>> AllowedEquipTypes { get; } = new Dictionary<MethodInfo, List<Type>>(8);
    public Dictionary<MethodInfo, List<Type>> AllowedDequipTypes { get; } = new Dictionary<MethodInfo, List<Type>>(8);
    public CustomUseableHandler()
    {
        _instance = this;
    }
    public void Patch()
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
        HarmonyMethod equipPatch = new HarmonyMethod(new Action<Useable, MethodInfo, bool>(EquipPostfix).Method);
        HarmonyMethod dequipPatch = new HarmonyMethod(new Action<Useable, MethodInfo, bool>(DequipPostfix).Method);
        foreach (MethodInfo method in AllowedEquipTypes.Keys)
        {
            PatchesMain.Patcher.Patch(method, postfix: equipPatch);
            Logger.LogDebug($"[{Source}] Patched {method.Format()} for types: {string.Join(", ", AllowedEquipTypes[method].Select(x => x.Format()))}.");
        }
        foreach (MethodInfo method in AllowedDequipTypes.Keys)
        {
            PatchesMain.Patcher.Patch(method, postfix: dequipPatch);
            Logger.LogDebug($"[{Source}] Patched {method.Format()} for types: {string.Join(", ", AllowedDequipTypes[method].Select(x => x.Format()))}.");
        }
    }

    public void Unpatch()
    {
        MethodInfo equipPatch = new Action<Useable, MethodInfo, bool>(EquipPostfix).Method;
        MethodInfo dequipPatch = new Action<Useable, MethodInfo, bool>(DequipPostfix).Method;
        foreach (MethodInfo method in AllowedEquipTypes.Keys)
        {
            PatchesMain.Patcher.Unpatch(method, equipPatch);
        }
        foreach (MethodInfo method in AllowedDequipTypes.Keys)
        {
            PatchesMain.Patcher.Unpatch(method, dequipPatch);
        }
    }
    private static void EquipPostfix(Useable __instance, MethodInfo __originalMethod, bool __runOriginal)
    {
        if (__runOriginal && _instance != null && __instance.channel.isOwner && _instance.AllowedEquipTypes.TryGetValue(__originalMethod, out List<Type> allowedTypes) && allowedTypes.Contains(__instance.GetType()))
        {
            _instance.OnInitialize?.Invoke(null, __instance);
        }
    }
    private static void DequipPostfix(Useable __instance, MethodInfo __originalMethod, bool __runOriginal)
    {
        if (__runOriginal && _instance != null && __instance.channel.isOwner && _instance.AllowedDequipTypes.TryGetValue(__originalMethod, out List<Type> allowedTypes) && allowedTypes.Contains(__instance.GetType()))
        {
            _instance.OnDestroy?.Invoke(null, __instance);
        }
    }

}
#endif