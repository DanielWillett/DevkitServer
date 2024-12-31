#if CLIENT
using DanielWillett.ReflectionTools;
using DevkitServer.API.Cartography;
using DevkitServer.API.Cartography.Compositors;
using System.Text.Json;

namespace DevkitServer.Core.Cartography.Compositors;

[Priority(0)]
internal class OverlayCartographyCompositor : ICartographyCompositor
{
    public bool SupportsSatellite => true;
    public bool SupportsChart => true;
    public bool Composite(in CartographyCaptureData data, Lazy<RenderTexture> texture, bool isExplicitlyDefined, JsonElement configuration)
    {
        bool didAnything = false;
        if (configuration.ValueKind != JsonValueKind.Undefined)
        {
            if (configuration.TryGetProperty("images", out JsonElement element) && element.ValueKind == JsonValueKind.Array)
            {
                Logger.DevkitServer.LogDebug(nameof(OverlayCartographyCompositor), "Compositing using config ('images') instead of automatic file search.");
                foreach (JsonElement subElem in element.EnumerateArray())
                {
                    string? path = subElem.GetString();

                    if (data.ConfigurationFilePath != null && !Path.IsPathRooted(path))
                    {
                        path = Path.GetFullPath(path, Path.GetDirectoryName(data.ConfigurationFilePath));
                    }

                    if (!File.Exists(path))
                    {
                        Logger.DevkitServer.LogWarning(nameof(OverlayCartographyCompositor), $"File missing: {path.Format()}.");
                        continue;
                    }

                    didAnything |= ApplyOverlay(in data, path!, Path.GetFileNameWithoutExtension(path)!, texture);
                }

                return didAnything;
            }

            if (configuration.TryGetProperty("image", out element) && element.ValueKind == JsonValueKind.String)
            {
                Logger.DevkitServer.LogDebug(nameof(OverlayCartographyCompositor), "Compositing using config ('image') instead of automatic file search.");
                string? path = element.GetString();

                if (data.ConfigurationFilePath != null && !Path.IsPathRooted(path))
                {
                    path = Path.GetFullPath(path, Path.GetDirectoryName(data.ConfigurationFilePath));
                }

                if (File.Exists(path))
                {
                    return ApplyOverlay(in data, path!, Path.GetFileNameWithoutExtension(path)!, texture);
                }

                Logger.DevkitServer.LogWarning(nameof(OverlayCartographyCompositor), $"File missing: {path.Format()}.");
                return false;
            }
        }

        string levelPath = data.Level.path;

        didAnything |= ApplyOverlaysFromFolder(in data, isExplicitlyDefined, texture, Path.Combine(levelPath, "Editor", "Overlays"), true);
        didAnything |= ApplyOverlaysFromFolder(in data, isExplicitlyDefined, texture, Path.Combine(levelPath, "Editor", "Overlay"), true);
        didAnything |= ApplyOverlaysFromFolder(in data, isExplicitlyDefined, texture, Path.Combine(levelPath, "Overlays"), true);
        didAnything |= ApplyOverlaysFromFolder(in data, isExplicitlyDefined, texture, Path.Combine(levelPath, "Overlay"), true);
        didAnything |= ApplyOverlaysFromFolder(in data, isExplicitlyDefined, texture, Path.Combine(levelPath, "Editor"), false);
        didAnything |= ApplyOverlaysFromFolder(in data, isExplicitlyDefined, texture, Path.Combine(levelPath, "Chart"), false);
        didAnything |= ApplyOverlaysFromFolder(in data, isExplicitlyDefined, texture, Path.Combine(levelPath, "Terrain"), false);

        return didAnything;
    }

    private static bool ApplyOverlaysFromFolder(in CartographyCaptureData data, bool isExplicitlyDefined, Lazy<RenderTexture> texture, string dir, bool anyFn)
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

            if (ApplyOverlay(in data, file, fn, texture))
                didAnything = true;
        }

        return didAnything;
    }

    private static bool ApplyOverlay(in CartographyCaptureData data, string filePath, string fileName, Lazy<RenderTexture> texture)
    {
        bool chart = fileName.IndexOf("chart", StringComparison.InvariantCultureIgnoreCase) != -1;
        bool satellite = fileName.IndexOf("satellite", StringComparison.InvariantCultureIgnoreCase) != -1;

        if (data.Type is CartographyType.Satellite or CartographyType.Chart)
        {
            if (chart != satellite && chart != (data.Type == CartographyType.Chart))
                return false;
        }

        Texture2D compositedTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
        {
            name = fileName,
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Point
        };

        try
        {
            Logger.DevkitServer.LogInfo(nameof(OverlayCartographyCompositor), $"Applying overlay: {filePath.Format()}.");

            compositedTexture.LoadImage(File.ReadAllBytes(filePath), false);
            _ = texture.Value; // ensure the render texture has been initialized.
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, data.ImageSize.x, data.ImageSize.y, 0);
            Graphics.DrawTexture(new Rect(0f, 0f, data.ImageSize.x, data.ImageSize.y), compositedTexture);
            GL.PopMatrix();
            return true;
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(nameof(OverlayCartographyCompositor), ex, $"Failed to read composited overlay at {filePath.Format()}.");
        }
        finally
        {
            Object.Destroy(compositedTexture);
        }

        return false;
    }
}
#endif