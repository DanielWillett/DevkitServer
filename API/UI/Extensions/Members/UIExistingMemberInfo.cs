#if CLIENT
using DevkitServer.API.Abstractions;
using System.Reflection;
using System.Reflection.Emit;

namespace DevkitServer.API.UI.Extensions.Members;

/// <summary>
/// Stores information about existing members (defined with <see cref="ExistingMemberAttribute"/>) on an extension.
/// </summary>
public class UIExistingMemberInfo
{
    /// <summary>
    /// Member to get or set in the extension.
    /// </summary>
    public MemberInfo Member { get; }

    /// <summary>
    /// Member to reference in the parent type.
    /// </summary>
    public MemberInfo Existing { get; }

    /// <summary>
    /// If the member in the parent type is static.
    /// </summary>
    public bool ExistingIsStatic { get; }

    /// <summary>
    /// If the member is set when the extension is created, instead of patching the extension member to get the value in realtime (customized by setting <see cref="ExistingMemberAttribute.InitializeMode"/>).
    /// </summary>
    public bool IsInitialized { get; }
    internal UIExistingMemberInfo(MemberInfo member, MemberInfo existing, bool existingIsStatic, bool isInitialized)
    {
        Member = member;
        Existing = existing;
        ExistingIsStatic = existingIsStatic;
        IsInitialized = isInitialized;
    }

    /// <summary>
    /// Emits instructions to get the value (expects the vanilla UI instance is on the stack if it's not a static member) and set the value of the member in the extension.
    /// </summary>
    /// <param name="il">Instruction emitter for a dynamic method.</param>
    /// <param name="onlyRead">Only get the value instead of also setting it to the extension's member.</param>
    public void EmitApply(IOpCodeEmitter il, bool onlyRead = false)
    {
        switch (Existing)
        {
            case FieldInfo field:
                il.Emit(ExistingIsStatic ? OpCodes.Ldsfld : OpCodes.Ldfld, field);
                break;
            case MethodInfo method:
                il.Emit(method.GetCall(), method);
                break;
            case PropertyInfo property:
                MethodInfo getter = property.GetGetMethod(true);
                if (getter == null)
                    goto default;
                il.Emit(getter.GetCall(), getter);
                break;
            default:
                Logger.DevkitServer.LogWarning(UIExtensionManager.Source, $"Invalid accessor for existing member: {Existing.Format()}.");
                il.Emit(OpCodes.Ldnull);
                break;
        }
        if (!onlyRead)
        {
            switch (Member)
            {
                case FieldInfo field:
                    il.Emit(field.IsStatic ? OpCodes.Stsfld : OpCodes.Stfld, field);
                    break;
                case PropertyInfo property:
                    MethodInfo setter = property.GetSetMethod(true);
                    if (setter == null)
                        goto default;
                    il.Emit(setter.GetCall(), setter);
                    break;
                case MethodInfo method:
                    il.Emit(method.GetCall(), method);
                    break;
                default:
                    Logger.DevkitServer.LogWarning(UIExtensionManager.Source, $"Invalid accessor for implementing member: {Member.Format()}.");
                    break;
            }
        }
    }
}
#endif