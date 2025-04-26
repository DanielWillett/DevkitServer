#if CLIENT
using DanielWillett.ReflectionTools;
using DanielWillett.UITools.API.Extensions;
using DanielWillett.UITools.Core.Extensions;
using DevkitServer.Plugins;
using System.Reflection;
using Module = SDG.Framework.Modules.Module;

namespace DevkitServer.API.UI.Extensions;

public class DevkitServerUIExtensionManager : UIExtensionManager
{
    /// <inheritdoc />
    public override void RegisterFromModuleAssembly(Assembly assembly, Module module)
    {
        if (module.TryGetDevkitServerPlugin(out _))
        {
            throw new ArgumentException("Module is PluginModule. Use RegisterFromPluginAssembly instead.", nameof(module));
        }

        base.RegisterFromModuleAssembly(assembly, module);
    }

    internal void RegisterFromPluginAssembly(PluginAssembly pluginAssembly, List<DevkitServerUIExtensionInfo> extensions)
    {
        ThreadUtil.assertIsGameThread();
        foreach (Type type in Accessor.GetTypesSafe(pluginAssembly.Assembly, true))
        {
            if (Attribute.GetCustomAttribute(type, typeof(UIExtensionAttribute)) is not UIExtensionAttribute uiExtensionAttribute)
                continue;

            if (uiExtensionAttribute.ParentType == null)
            {
                LogError("Error initializing UI extension: "
                         + Accessor.Formatter.Format(type) + ". Unknown parent type in "
                         + Accessor.Formatter.Format(typeof(UIExtensionAttribute)) + ": \""
                         + (uiExtensionAttribute.SearchedParentType ?? "<unknown>") + "\".", pluginAssembly);
                return;
            }

            IDevkitServerPlugin? plugin = PluginLoader.FindPluginForMember(type);

            if (plugin == null)
            {
                LogError("Unable to link " + type.Format() + " to a plugin. Use the " + typeof(PluginIdentifierAttribute).Format() +
                         " to link a hierarchy item identifier factory to a plugin when multiple plugins are loaded from an assembly.", pluginAssembly);
                continue;
            }

            DevkitServerUIExtensionInfo info = new DevkitServerUIExtensionInfo(type, uiExtensionAttribute.ParentType, type.GetPriority(), plugin)
            {
                SuppressUIExtensionParentWarning = uiExtensionAttribute.SuppressUIExtensionParentWarning
            };
            try
            {
                InitializeExtension(info);
            }
            catch (Exception ex)
            {
                plugin.LogError("Error initializing UI extension: " + Accessor.Formatter.Format(type) + ".");
                plugin.LogError(ex);
                continue;
            }

            extensions.Add(info);

            plugin.LogDebug("Registered UI extension: " + Accessor.Formatter.Format(type) + ".");
        }
    }

    protected void LogDebug(string message, PluginAssembly pluginAssembly)
    {
        if (Logger.Terminal == null)
        {
            base.LogDebug(message, null, pluginAssembly.Assembly);
            return;
        }

        if (pluginAssembly.Plugins.Count == 1)
        {
            IDevkitServerPlugin plugin = pluginAssembly.Plugins[0];
            plugin.LogDebug(plugin.GetSource() + " | " + Source, message);
        }
        else
            LogDebug(message, null, pluginAssembly.Assembly);
    }

    protected void LogInfo(string message, PluginAssembly pluginAssembly)
    {
        if (Logger.Terminal == null)
        {
            base.LogInfo(message, null, pluginAssembly.Assembly);
            return;
        }

        if (pluginAssembly.Plugins.Count == 1)
        {
            IDevkitServerPlugin plugin = pluginAssembly.Plugins[0];
            plugin.LogInfo(plugin.GetSource() + " | " + Source, message);
        }
        else
            LogInfo(message, null, pluginAssembly.Assembly);
    }

    protected void LogWarning(string message, PluginAssembly pluginAssembly)
    {
        if (Logger.Terminal == null)
        {
            base.LogWarning(message, null, pluginAssembly.Assembly);
            return;
        }

        if (pluginAssembly.Plugins.Count == 1)
        {
            IDevkitServerPlugin plugin = pluginAssembly.Plugins[0];
            plugin.LogWarning(plugin.GetSource() + " | " + Source, message);
        }
        else
            LogWarning(message, null, pluginAssembly.Assembly);
    }

    protected void LogError(string message, PluginAssembly pluginAssembly)
    {
        if (Logger.Terminal == null)
        {
            base.LogError(message, null, pluginAssembly.Assembly);
            return;
        }

        if (pluginAssembly.Plugins.Count == 1)
        {
            IDevkitServerPlugin plugin = pluginAssembly.Plugins[0];
            plugin.LogError(plugin.GetSource() + " | " + Source, message);
        }
        else
            LogError(message, null, pluginAssembly.Assembly);
    }

    /// <inheritdoc />
    protected override void LogDebug(string message, Module? module = null, Assembly? assembly = null)
    {
        if (Logger.Terminal == null)
        {
            base.LogDebug(message, module, assembly);
            return;
        }

        if (module is PluginModule pluginModule)
        {
            pluginModule.Plugin.LogDebug(pluginModule.Plugin.GetSource() + " | " + Source, message);
        }
        else if (module == DevkitServerModule.Module)
        {
            Logger.DevkitServer.LogDebug(Source, message);
        }
        else
        {
            base.LogDebug(message, module, assembly);
        }
    }

    /// <inheritdoc />
    protected override void LogInfo(string message, Module? module = null, Assembly? assembly = null)
    {
        if (Logger.Terminal == null)
        {
            base.LogInfo(message, module, assembly);
            return;
        }

        if (module is PluginModule pluginModule)
        {
            pluginModule.Plugin.LogInfo(pluginModule.Plugin.GetSource() + " | " + Source, message);
        }
        else if (module == DevkitServerModule.Module)
        {
            Logger.DevkitServer.LogInfo(Source, message);
        }
        else
        {
            base.LogInfo(message, module, assembly);
        }
    }

    /// <inheritdoc />
    protected override void LogWarning(string message, Module? module = null, Assembly? assembly = null)
    {
        if (Logger.Terminal == null)
        {
            base.LogWarning(message, module, assembly);
            return;
        }

        if (module is PluginModule pluginModule)
        {
            pluginModule.Plugin.LogWarning(pluginModule.Plugin.GetSource() + " | " + Source, message);
        }
        else if (module == DevkitServerModule.Module)
        {
            Logger.DevkitServer.LogWarning(Source, message);
        }
        else
        {
            base.LogWarning(message, module, assembly);
        }
    }

    /// <inheritdoc />
    protected override void LogError(string message, Module? module = null, Assembly? assembly = null)
    {
        if (Logger.Terminal == null)
        {
            base.LogError(message, module, assembly);
            return;
        }

        if (module is PluginModule pluginModule)
        {
            pluginModule.Plugin.LogError(pluginModule.Plugin.GetSource() + " | " + Source, message);
        }
        else if (module == DevkitServerModule.Module)
        {
            Logger.DevkitServer.LogError(Source, message);
        }
        else
        {
            base.LogError(message, module, assembly);
        }
    }
}
#endif