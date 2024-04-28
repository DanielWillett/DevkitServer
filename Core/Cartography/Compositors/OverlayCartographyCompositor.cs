#if CLIENT
using DanielWillett.ReflectionTools;
using DevkitServer.API.Cartography;
using DevkitServer.API.Cartography.Compositors;

namespace DevkitServer.Core.Cartography.Compositors;

[Priority(0)]
internal class OverlayCartographyCompositor : ICartographyCompositor
{
    public bool SupportsSatellite => true;
    public bool SupportsChart => true;
    public bool Composite(in CartographyCaptureData data, Lazy<RenderTexture> texture, bool isExplicitlyDefined)
    {
        List<Texture2D> foundImages = new List<Texture2D>();

        string levelPath = data.Level.path;

        if (ApplyOverlays(in data, foundImages, isExplicitlyDefined, texture, Path.Combine(levelPath, "Editor", "Overlays"), true))
            return true;
        if (ApplyOverlays(in data, foundImages, isExplicitlyDefined, texture, Path.Combine(levelPath, "Editor", "Overlay"), true))
            return true;
        if (ApplyOverlays(in data, foundImages, isExplicitlyDefined, texture, Path.Combine(levelPath, "Overlays"), true))
            return true;
        if (ApplyOverlays(in data, foundImages, isExplicitlyDefined, texture, Path.Combine(levelPath, "Overlay"), true))
            return true;
        if (ApplyOverlays(in data, foundImages, isExplicitlyDefined, texture, Path.Combine(levelPath, "Editor"), false))
            return true;
        if (ApplyOverlays(in data, foundImages, isExplicitlyDefined, texture, Path.Combine(levelPath, "Chart"), false))
            return true;
        if (ApplyOverlays(in data, foundImages, isExplicitlyDefined, texture, Path.Combine(levelPath, "Terrain"), false))
            return true;
        if (ApplyOverlays(in data, foundImages, isExplicitlyDefined, texture, levelPath, false))
            return true;

        return false;

        static bool ApplyOverlays(in CartographyCaptureData data, List<Texture2D> foundImages, bool isExplicitlyDefined, Lazy<RenderTexture> texture, string dir, bool anyFn)
        {
            if (!Directory.Exists(dir))
                return false;

            bool didAnything = false;
            if (isExplicitlyDefined)
                Logger.DevkitServer.LogInfo(nameof(OverlayCartographyCompositor), $"Looking for overlays in {dir.Format()}.");
            else
                Logger.DevkitServer.LogDebug(nameof(OverlayCartographyCompositor), $"Looking for overlays in {dir.Format()}.");
            foreach (string file in Directory.EnumerateFiles(dir, "*", SearchOption.TopDirectoryOnly).OrderByDescending(Path.GetFileName))
            {
                string fn = Path.GetFileNameWithoutExtension(file);

                if (!anyFn && fn.IndexOf("overlay", StringComparison.InvariantCultureIgnoreCase) == -1)
                    continue;

                string ext = Path.GetExtension(file);
                if (string.IsNullOrEmpty(ext))
                    continue;

                if (!ext.Equals(".png", StringComparison.OrdinalIgnoreCase) && !ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) && !ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
                    continue;

                bool chart = fn.IndexOf("chart", StringComparison.InvariantCultureIgnoreCase) != -1;
                bool satellite = fn.IndexOf("satellite", StringComparison.InvariantCultureIgnoreCase) != -1;

                if (chart != satellite && chart != data.IsChart)
                    continue;

                Texture2D compositedTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    name = fn,
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Point
                };

                foundImages.Add(compositedTexture);
                try
                {
                    Logger.DevkitServer.LogInfo(nameof(OverlayCartographyCompositor), $"Applying overlay: {file.Format()}.");

                    compositedTexture.LoadImage(File.ReadAllBytes(file), false);
                    _ = texture.Value; // ensure the render texture has been initialized.
                    GL.PushMatrix();
                    GL.LoadPixelMatrix(0, data.ImageSize.x, data.ImageSize.y, 0);
                    Graphics.DrawTexture(new Rect(0f, 0f, data.ImageSize.x, data.ImageSize.y), compositedTexture);
                    GL.PopMatrix();
                    didAnything = true;
                }
                catch (Exception ex)
                {
                    foundImages.RemoveAt(foundImages.Count - 1);
                    Logger.DevkitServer.LogError(nameof(OverlayCartographyCompositor), ex, $"Failed to read composited overlay at {file.Format()}.");
                }
                finally
                {
                    Object.Destroy(compositedTexture);
                }
            }

            return didAnything;
        }
    }
}
#endif