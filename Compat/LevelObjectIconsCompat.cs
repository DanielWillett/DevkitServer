#if CLIENT
using DevkitServer.API;
using DevkitServer.API.UI.Icons;
using DevkitServer.Configuration;
using SDG.Framework.Modules;

namespace DevkitServer.Compat;
internal static class LevelObjectIconsCompat
{
    internal static void AddIconProviders(IList<IDefaultIconProvider> providers)
    {
        Logger.LogInfo($"Loading compat layer for {"LevelObjectIcons".Colorize(new Color32(255, 255, 102, 255))}...");
        IEnumerable<Type> types = !DevkitServerConfig.Config.DisableDefaultLevelObjectIconProviderSearch
            ? Accessor.GetTypesSafe(ModuleHook.modules.Where(x => x.assemblies != null).SelectMany(x => x.assemblies), true)
            : Accessor.GetTypesSafe(true);

        foreach (Type type in types.Where(x => x is { IsInterface: false, IsAbstract: false }))
        {
            try
            {
                if (!typeof(DanielWillett.LevelObjectIcons.API.IDefaultIconProvider).IsAssignableFrom(type))
                    continue;
            }
            catch (Exception ex)
            {
                Logger.LogInfo($"Unable to check type {type.Format()} for {typeof(DanielWillett.LevelObjectIcons.API.IDefaultIconProvider).Format()} - {ex.GetType().Format()} {ex.Message.Format(true)}");
                continue;
            }

            try
            {
                DanielWillett.LevelObjectIcons.API.IDefaultIconProvider provider = (DanielWillett.LevelObjectIcons.API.IDefaultIconProvider)Activator.CreateInstance(type);
                providers.Add(new DefaultIconProviderProxyToInternal(provider));
            }
            catch (Exception ex)
            {
                Logger.LogError($"Unable to apply icon provider: {type.Format()}.", method: ObjectIconGenerator.Source);
                Logger.LogError(ex, method: ObjectIconGenerator.Source);
            }
        }
    }
}
#endif