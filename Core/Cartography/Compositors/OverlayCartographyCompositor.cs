#if CLIENT
using DevkitServer.API.Cartography;
using DevkitServer.API.Cartography.Compositors;

namespace DevkitServer.Core.Cartography.Compositors;
internal class OverlayCartographyCompositor : ICartographyCompositor
{
    public bool SupportsSatellite => true;
    public bool SupportsChart => true;
    public bool Composite(in CartographyCaptureData data, Lazy<RenderTexture> texture)
    {
        List<Texture2D> foundImages = new List<Texture2D>();

        string levelPath = data.Level.path;

        bool didAnything = false;

        didAnything |= ApplyOverlays(in data, foundImages, texture, Path.Combine(levelPath, "Editor", "Overlays"), true);
        if (!didAnything)
            didAnything |= ApplyOverlays(in data, foundImages, texture, Path.Combine(levelPath, "Editor", "Overlay"), true);
        if (!didAnything)
            didAnything |= ApplyOverlays(in data, foundImages, texture, Path.Combine(levelPath, "Overlays"), true);
        if (!didAnything)
            didAnything |= ApplyOverlays(in data, foundImages, texture, Path.Combine(levelPath, "Overlay"), true);
        if (!didAnything)
            didAnything |= ApplyOverlays(in data, foundImages, texture, Path.Combine(levelPath, "Editor"), false);
        if (!didAnything)
            didAnything |= ApplyOverlays(in data, foundImages, texture, Path.Combine(levelPath, "Chart"), false);
        if (!didAnything)
            didAnything |= ApplyOverlays(in data, foundImages, texture, Path.Combine(levelPath, "Terrain"), false);
        if (!didAnything)
            didAnything |= ApplyOverlays(in data, foundImages, texture, Path.Combine(levelPath), false);

        return didAnything;

        static bool ApplyOverlays(in CartographyCaptureData data, List<Texture2D> foundImages, Lazy<RenderTexture> texture, string dir, bool anyFn)
        {
            if (!Directory.Exists(dir))
                return false;

            bool didAnything = false;
            Logger.LogDebug($"Looking for overlays in {dir.Format()}.");
            foreach (string file in Directory.EnumerateFiles(dir, "*", SearchOption.TopDirectoryOnly).OrderByDescending(Path.GetFileName))
            {
                string fn = Path.GetFileNameWithoutExtension(file);

                if (anyFn || fn.IndexOf("overlay", StringComparison.InvariantCultureIgnoreCase) == -1)
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

                Texture2D compositedTexture = new Texture2D(2, 2)
                {
                    name = fn,
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Point
                };

                foundImages.Add(compositedTexture);
                try
                {
                    compositedTexture.LoadImage(File.ReadAllBytes(file), false);

                    Graphics.Blit(compositedTexture, texture.Value);
                    didAnything = true;
                }
                catch (Exception ex)
                {
                    foundImages.RemoveAt(foundImages.Count - 1);
                    Object.Destroy(compositedTexture);
                    Logger.LogError($"Failed to read composited overlay at {file.Format()}.");
                    Logger.LogError(ex);
                }
            }

            return didAnything;
        }
    }
}
#endif