using System;
using System.Collections.Generic;
using System.Reflection;
using Module = SDG.Framework.Modules.Module;

namespace DanielWillett.UITools.API.Extensions;

public interface IUIExtensionManager
{
    IReadOnlyList<UIExtensionInfo> Extensions { get; }
    bool DebugLogging { get; set; }
    T? GetInstance<T>() where T : class;
    T? GetInstance<T>(object vanillaUIInstance) where T : class;

    /// <summary>Manually register a UI extension.</summary>
    void RegisterExtension(Type extensionType, Type parentType, Module module);

    /// <summary>Register all UI extensions in an assembly and module.</summary>
    void RegisterFromModuleAssembly(Assembly assembly, Module module);

    /// <summary>
    /// Run any start-up requirements. This should not include any extension searching, as those will be registered with <see cref="M:DanielWillett.UITools.API.Extensions.IUIExtensionManager.RegisterFromModuleAssembly(System.Reflection.Assembly,SDG.Framework.Modules.Module)" /> and <see cref="M:DanielWillett.UITools.API.Extensions.IUIExtensionManager.RegisterExtension(System.Type,System.Type,SDG.Framework.Modules.Module)" />.
    /// </summary>
    void Initialize();
}