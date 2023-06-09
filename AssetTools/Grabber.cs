namespace DevkitServer.AssetTools;
public static class Grabber
{
    public static void DownloadResource<T>(StaticResourceRef<T> path, string outPath) where T : Object
    {
        T resource = path.GetOrLoad();
        
        if (resource is Texture2D texture)
        {
            byte[] bytes = texture.EncodeOrBlitTexturePNG(false);
            if (!outPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                outPath += ".png";
            if (Path.GetDirectoryName(outPath) is { } dir)
                Directory.CreateDirectory(dir);
            File.WriteAllBytes(outPath, bytes);
        }
        else throw new NotSupportedException("Saving " + resource.GetType().Name);
    }
    public static void DownloadResource<T>(string path, string outPath) where T : Object
    {
        T resource = Resources.Load<T>(path);
        
        if (resource is Texture2D texture)
        {
            byte[] bytes = texture.EncodeOrBlitTexturePNG(true);
            if (!outPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                outPath += ".png";
            if (Path.GetDirectoryName(outPath) is { } dir)
                Directory.CreateDirectory(dir);
            File.WriteAllBytes(outPath, bytes);
        }
        else throw new NotSupportedException("Saving " + resource.GetType().Name);
    }
    public static void DownloadFromBundle<T>(Bundle bundle, string path, string outPath) where T : Object
    {
        T resource = bundle.load<T>(path);
        
        if (resource is Texture2D texture)
        {
            byte[] bytes = texture.EncodeOrBlitTexturePNG(false);
            if (!outPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                outPath += ".png";
            if (Path.GetDirectoryName(outPath) is { } dir)
                Directory.CreateDirectory(dir);
            File.WriteAllBytes(outPath, bytes);
        }
        else throw new NotSupportedException("Saving " + resource.GetType().Name);
    }

    public static byte[] EncodeOrBlitTexturePNG(this Texture2D texture, bool destroy)
    {
        Texture2D useableTexture = ConvertToReadable(texture, destroy);

        byte[] bytes = useableTexture.EncodeToPNG();

        if (texture != useableTexture)
            Object.Destroy(useableTexture);

        if (destroy)
            Object.Destroy(texture);

        return bytes ?? Array.Empty<byte>();
    }
    public static Texture2D ConvertToReadable(Texture2D texture, bool destroy = false)
    {
        if (texture.isReadable)
            return texture;

        int width = texture.width;
        int height = texture.height;
        RenderTexture rt = RenderTexture.GetTemporary(width, height);
        Texture2D outTexture = new Texture2D(width, height, TextureFormat.ARGB32, false) { name = texture.name };
        
        RenderTexture active = RenderTexture.active;
        RenderTexture.active = rt;
        Graphics.Blit(texture, rt);
        outTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
        outTexture.Apply(false);
        RenderTexture.active = active;
        RenderTexture.ReleaseTemporary(rt);
        if (destroy)
            Object.Destroy(texture);
        return outTexture;
    }
}