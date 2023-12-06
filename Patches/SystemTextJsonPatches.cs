using DevkitServer.API;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;

namespace DevkitServer.Patches;

[EarlyTypeInit]
internal static class SystemTextJsonPatches
{
    private static MethodInfo? _getAttribute;
    internal static void Patch()
    {
        try
        {
            _getAttribute = Type.GetType("System.Text.Json.JsonPropertyInfo, System.Text.Json", true, false)?
                .GetMethod("GetAttribute", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?
                .MakeGenericMethod(typeof(Attribute));
            
            if (_getAttribute != null)
                PatchesMain.Patcher.Patch(_getAttribute, transpiler: new HarmonyMethod(Accessor.GetMethod(GetAttributeTranspiler)));
            else
                CommandWindow.LogError("Unable to patch System.Text.Json.JsonPropertyInfo.GetAttribute<TAttribute>(). Method not found.");
        }
        catch (Exception ex)
        {
            CommandWindow.LogError("Unable to patch System.Text.Json.JsonPropertyInfo.GetAttribute<TAttribute>().");
            CommandWindow.LogError(ex);
        }
    }

    internal static void Unpatch()
    {
        if (_getAttribute != null)
        {
            try
            {
                PatchesMain.Patcher.Unpatch(_getAttribute, Accessor.GetMethod(GetAttributeTranspiler));
            }
            catch (Exception ex)
            {
                CommandWindow.LogError("Unable to unpatch System.Text.Json.JsonPropertyInfo.GetAttribute<TAttribute>().");
                CommandWindow.LogError(ex);
            }
        }
    }

    // enables inheritance in the JsonPropertyInfo.GetAttribute method, for some reason this isn't enabled usually (in the version I'm using).
    private static IEnumerable<CodeInstruction> GetAttributeTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo? method = typeof(CustomAttributeExtensions).GetMethod("GetCustomAttribute",
            BindingFlags.Static | BindingFlags.Public, null, CallingConventions.Any,
            new Type[] { typeof(MemberInfo), typeof(Type), typeof(bool) }, null);

        if (method == null)
        {
            CommandWindow.LogWarning("Failed to transpile System.Text.Json.JsonPropertyInfo.GetAttribute<TAttribute>(), can't find GetCustomAttribute.");
            return instructions;
        }

        List<CodeInstruction> ins = [..instructions];

        for (int i = 1; i < ins.Count; ++i)
        {
            if (!ins[i].Calls(method))
                continue;

            ins[i - 1].opcode = OpCodes.Ldc_I4_1;
            break;
        }

        return ins;
    }
}
