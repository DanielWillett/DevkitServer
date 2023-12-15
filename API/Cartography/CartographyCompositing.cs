#if CLIENT
using DevkitServer.API.Cartography.Compositors;
using DevkitServer.Core.Cartography.Compositors;
using DevkitServer.Plugins;
using System.Diagnostics;

namespace DevkitServer.API.Cartography;
internal static class CartographyCompositing
{
    private static readonly CartographyCompositorInfo[] DefaultChartColorProviders =
    [
        new CartographyCompositorInfo(typeof(OverlayCartographyCompositor), null!, 0, true, true)
    ];

    internal static bool CompositeForeground(Texture2D texture, in CartographyCaptureData data)
    {
        List<CartographyCompositorInfo> types = new List<CartographyCompositorInfo>(4);

        // plugin compositors
        types.AddRange(PluginLoader.Assemblies.SelectMany(x => x.CartographyCompositors));

        // 'vanilla' compositors
        types.AddRange(DefaultChartColorProviders);

        types.Sort((a, b) => b.Priority.CompareTo(a.Priority));

        RenderTexture? active = null;

        Lazy<RenderTexture> renderTexture = new Lazy<RenderTexture>(() =>
        {
            texture.Apply(false);

            RenderTexture rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, texture.graphicsFormat, 1);

            active = RenderTexture.active;
            RenderTexture.active = rt;
            Graphics.Blit(texture, rt);
            Logger.LogDebug("Created compositing render texture.");

            return rt;
        }, LazyThreadSafetyMode.None);

        bool didAnything = false;

        Stopwatch sw = new Stopwatch();
        
        foreach (CartographyCompositorInfo info in types)
        {
            if (!info.SupportsChart && data.IsChart)
            {
                if (info.Plugin != null)
                    info.Plugin.LogDebug($"Skipping compositor {info.Type.Format()} as it doesn't support chart renders.");
                else
                    Logger.LogDebug($"Skipping compositor {info.Type.Format()} as it doesn't support chart renders.");

                continue;
            }
            if (!info.SupportsSatellite && !data.IsChart)
            {
                if (info.Plugin != null)
                    info.Plugin.LogDebug($"Skipping compositor {info.Type.Format()} as it doesn't support satellite renders.");
                else
                    Logger.LogDebug($"Skipping compositor {info.Type.Format()} as it doesn't support satellite renders.");

                continue;
            }

            try
            {
                if (info.Plugin != null)
                    info.Plugin.LogDebug($"Applying compositor of type {info.Type.Format()}.");
                else
                    Logger.LogDebug($"[{nameof(CompositeForeground)}] Applying compositor of type {info.Type.Format()}.");
                
                ICartographyCompositor compositor = (ICartographyCompositor)Activator.CreateInstance(info.Type, true);

                sw.Restart();

                bool compositorDidAnything = compositor.Composite(in data, renderTexture);

                sw.Stop();

                if (compositorDidAnything)
                {
                    didAnything = true;
                    if (info.Plugin != null)
                        info.Plugin.LogInfo($"Applied compositor of type {info.Type.Format()} in {sw.GetElapsedMilliseconds().Format("F2")} ms.");
                    else
                        Logger.LogInfo($"[{nameof(CompositeForeground)}] Applied compositor of type {info.Type.Format()} in {sw.GetElapsedMilliseconds().Format("F2")} ms.");
                }
                else
                {
                    if (info.Plugin != null)
                        info.Plugin.LogDebug($"Did not use compositor of type {info.Type.Format()} after {sw.GetElapsedMilliseconds().Format("F2")} ms.");
                    else
                        Logger.LogDebug($"[{nameof(CompositeForeground)}] Did not use compositor of type {info.Type.Format()} after {sw.GetElapsedMilliseconds().Format("F2")} ms.");
                }
            }
            catch (Exception ex)
            {
                if (info.Plugin != null)
                {
                    info.Plugin.LogError($"Exception thrown while applying compositor of type {info.Type.Format()}.");
                    info.Plugin.LogError(ex);
                }
                else
                {
                    Logger.LogError($"Exception thrown while applying compositor of type {info.Type.Format()}.", method: info.Type.Name);
                    Logger.LogError(ex, method: info.Type.Name);
                }
            }
        }

        if (renderTexture.IsValueCreated)
        {
            RenderTexture.active = active;
            texture.ReadPixels(new Rect(0, 0, data.ImageSize.x, data.ImageSize.y), 0, 0, false);
            RenderTexture.ReleaseTemporary(renderTexture.Value);
            Logger.LogDebug("Released compositing render texture.");
        }
        else didAnything = false;

        return didAnything;
    }
}
#endif