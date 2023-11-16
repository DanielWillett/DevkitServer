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
    void RegisterExtension(Type extensionType, Type parentType, Module module);
    void RegisterFromModuleAssembly(Assembly assembly, Module module);
    void Initialize();
}