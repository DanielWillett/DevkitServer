#if CLIENT
using DevkitServer.API.Cartography.Compositors;
using DevkitServer.Core.Cartography;
using DevkitServer.Core.Cartography.Compositors;
using DevkitServer.Plugins;
using System.Diagnostics;

namespace DevkitServer.API.Cartography;
internal static class CartographyCompositing
{
    private static readonly CartographyCompositorInfo[] DefaultCompositors =
    [
        new CartographyCompositorInfo(typeof(OverlayCartographyCompositor), null!, 0, true, true)
    ];

    internal static bool CompositeForeground(Texture2D texture, LevelCartographyConfigData? config, in CartographyCaptureData data)
    {
        List<CartographyCompositorInfo> types = new List<CartographyCompositorInfo>(4);

        bool isExplicit;

        if (config?.ActiveCompositors == null)
        {
            isExplicit = false;
            // plugin compositors
            types.AddRange(PluginLoader.Assemblies.SelectMany(x => x.CartographyCompositors));

            // 'vanilla' compositors
            types.AddRange(DefaultCompositors);

            types.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }
        else
        {
            isExplicit = true;
            for (int i = 0; i < config.ActiveCompositors.Length; i++)
            {
                string compositorName = config.ActiveCompositors[i];

                // vanilla
                CartographyCompositorInfo info = DefaultCompositors
                    .FirstOrDefault(x => x.Type.Name.Equals(compositorName, StringComparison.InvariantCultureIgnoreCase));

                // all plugins
                if (info.Type == null)
                {
                    info = PluginLoader.Assemblies
                        .SelectMany(x => x.CartographyCompositors)
                        .OrderByDescending(x => x.Priority)
                        .FirstOrDefault(x => x.Type.Name.Equals(compositorName, StringComparison.InvariantCultureIgnoreCase));
                }

                if (info.Type == null || !typeof(ICartographyCompositor).IsAssignableFrom(info.Type) || info.Type.IsAbstract || info.Type.IsInterface)
                {
                    Logger.DevkitServer.LogWarning(nameof(CartographyCompositing), $"Unknown chart provider: {compositorName.Format()}, skipping this compositor. Make sure your compositor implements {typeof(ICartographyCompositor).Format()}.");
                    continue;
                }

                types.Add(info);
            }
        }

        RenderTexture? active = null;

        Lazy<RenderTexture> renderTexture = new Lazy<RenderTexture>(() =>
        {
            texture.Apply(false);

            RenderTexture rt = RenderTexture.GetTemporary(texture.width, texture.height);

            active = RenderTexture.active;
            RenderTexture.active = rt;
            Graphics.Blit(texture, rt);
            Logger.DevkitServer.LogDebug(nameof(CartographyCompositing), "Created compositing render texture.");

            return rt;
        }, LazyThreadSafetyMode.None);

        bool didAnything = false;

        Stopwatch sw = new Stopwatch();
        
        foreach (CartographyCompositorInfo info in types)
        {
            if (!info.SupportsChart && data.IsChart)
            {
                if (info.Plugin != null)
                    info.Plugin.LogDebug(nameof(CartographyCompositing), $"Skipping compositor {info.Type.Format()} as it doesn't support chart renders.");
                else
                    Logger.DevkitServer.LogDebug(nameof(CartographyCompositing), $"Skipping compositor {info.Type.Format()} as it doesn't support chart renders.");

                continue;
            }
            if (!info.SupportsSatellite && !data.IsChart)
            {
                if (info.Plugin != null)
                    info.Plugin.LogDebug(nameof(CartographyCompositing), $"Skipping compositor {info.Type.Format()} as it doesn't support satellite renders.");
                else
                    Logger.DevkitServer.LogDebug(nameof(CartographyCompositing), $"Skipping compositor {info.Type.Format()} as it doesn't support satellite renders.");

                continue;
            }

            try
            {
                if (info.Plugin != null)
                    info.Plugin.LogDebug(nameof(CartographyCompositing), $"Applying compositor of type {info.Type.Format()}.");
                else
                    Logger.DevkitServer.LogDebug(nameof(CartographyCompositing), $"Applying compositor of type {info.Type.Format()}.");
                
                ICartographyCompositor compositor = (ICartographyCompositor)Activator.CreateInstance(info.Type, true);

                sw.Restart();

                bool compositorDidAnything = compositor.Composite(in data, renderTexture, isExplicit);

                sw.Stop();

                if (compositorDidAnything)
                {
                    didAnything = true;
                    if (info.Plugin != null)
                        info.Plugin.LogInfo(nameof(CartographyCompositing), $"Applied compositor of type {info.Type.Format()} in {sw.GetElapsedMilliseconds().Format("F2")} ms.");
                    else
                        Logger.DevkitServer.LogInfo(nameof(CartographyCompositing), $"Applied compositor of type {info.Type.Format()} in {sw.GetElapsedMilliseconds().Format("F2")} ms.");
                }
                else
                {
                    if (info.Plugin != null)
                        info.Plugin.LogDebug(nameof(CartographyCompositing), $"Did not use compositor of type {info.Type.Format()} after {sw.GetElapsedMilliseconds().Format("F2")} ms.");
                    else
                        Logger.DevkitServer.LogDebug(nameof(CartographyCompositing), $"Did not use compositor of type {info.Type.Format()} after {sw.GetElapsedMilliseconds().Format("F2")} ms.");
                }
            }
            catch (Exception ex)
            {
                if (info.Plugin != null)
                {
                    info.Plugin.LogError(nameof(CartographyCompositing), ex, $"Exception thrown while applying compositor of type {info.Type.Format()}.");
                }
                else
                {
                    Logger.DevkitServer.LogError(nameof(CartographyCompositing), ex, $"Exception thrown while applying compositor of type {info.Type.Format()}.");
                }
            }
        }
        
        if (renderTexture.IsValueCreated)
        {
            texture.ReadPixels(new Rect(0, 0, data.ImageSize.x, data.ImageSize.y), 0, 0, false);
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(renderTexture.Value);
            Logger.DevkitServer.LogDebug(nameof(CartographyCompositing), "Released compositing render texture.");
        }
        else didAnything = false;

        return didAnything;
    }
}
#endif