using System.Text.Json;
using DevkitServer.Configuration;
using UnityEngine.Rendering;

namespace DevkitServer.AssetTools;
public static class Grabber
{
    public static bool DownloadResource<T>(StaticResourceRef<T> path, string outPath) where T : Object
    {
        T resource = path.GetOrLoad();
        if (resource == null)
        {
            Logger.LogDebug("Resource not found: " + path.Format() + ".");
            return false;
        }

        bool result = Save(resource, outPath);

        Resources.UnloadAsset(resource);
        return result;
    }
    public static bool DownloadResource<T>(string path, string outPath) where T : Object
    {
        T resource = Resources.Load<T>(path);
        if (resource == null)
        {
            Logger.LogDebug("Resource not found: " + path.Format() + ".");
            return false;
        }

        bool result = Save(resource, outPath);

        Resources.UnloadAsset(resource);
        return result;
    }
    public static bool DownloadFromBundle<T>(Bundle bundle, string path, string outPath) where T : Object
    {
        T resource = bundle.load<T>(path);
        if (resource == null)
        {
            Logger.LogDebug("Asset not found: " + bundle.name.Format(false) + "::" + path.Format(false) + ".");
            return false;
        }

        return Save(resource, outPath);
    }
    
    public static bool Save<T>(T @object, string outPath) where T : Object
    {
        switch (@object)
        {
            case null:
                throw new ArgumentNullException(nameof(@object));
            case Texture2D texture:
            {
                byte[] bytes = texture.EncodeOrBlitTexturePNG(false);
                if (!outPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    outPath += ".png";
                if (Path.GetDirectoryName(outPath) is { } dir)
                    Directory.CreateDirectory(dir);
                File.WriteAllBytes(outPath, bytes);
                return true;
            }
            case Material material:
                outPath = Path.Combine(Path.GetDirectoryName(outPath) ?? string.Empty, Path.GetFileNameWithoutExtension(outPath));
                DevkitServerUtility.CheckDirectory(false, outPath);
                using (FileStream stream = new FileStream(Path.Combine(outPath, "material_data.json"), FileMode.Create, FileAccess.Write, FileShare.Read))
                using (Utf8JsonWriter writer = new Utf8JsonWriter(stream, DevkitServerConfig.WriterOptions))
                {
                    writer.WriteStartObject();

                    Texture? mainTexture = material.mainTexture;

                    writer.WritePropertyName("name");
                    writer.WriteStringValue(material.name);

                    writer.WritePropertyName("color");
                    JsonSerializer.Serialize(writer, material.color, DevkitServerConfig.SerializerSettings);

                    writer.WritePropertyName("double_sided_gl");
                    writer.WriteBooleanValue(material.doubleSidedGI);
                    
                    writer.WritePropertyName("enable_instancing");
                    writer.WriteBooleanValue(material.enableInstancing);

                    writer.WritePropertyName("global_illumination_flags");
                    writer.WriteStringValue(material.globalIlluminationFlags.ToString());

                    writer.WritePropertyName("pass_count");
                    writer.WriteNumberValue(material.passCount);

                    writer.WritePropertyName("render_queue");
                    writer.WriteNumberValue(material.renderQueue);

                    writer.WritePropertyName("shader_keywords");
                    writer.WriteStartArray();
                    string[] keywords = material.shaderKeywords;
                    for (int i = 0; i < keywords.Length; ++i)
                        writer.WriteStringValue(keywords[i]);
                    writer.WriteEndArray();

                    writer.WritePropertyName("shader");
                    Shader? shader = material.shader;
                    if (shader != null)
                    {
                        writer.WriteStartObject();

                        writer.WritePropertyName("name");
                        writer.WriteStringValue(shader.name);

                        writer.WritePropertyName("pass_count");
                        writer.WriteNumberValue(shader.passCount);

                        writer.WritePropertyName("render_queue");
                        writer.WriteNumberValue(shader.renderQueue);

                        writer.WritePropertyName("is_supported");
                        writer.WriteBooleanValue(shader.isSupported);

                        writer.WritePropertyName("maximum_lod");
                        writer.WriteNumberValue(shader.maximumLOD);

                        writer.WritePropertyName("properties");
                        writer.WriteStartArray();

                        int ct = shader.GetPropertyCount();
                        for (int i = 0; i < ct; ++i)
                        {
                            string name = shader.GetPropertyName(i);
                            writer.WriteStartObject();

                            writer.WritePropertyName("name");
                            writer.WriteStringValue(name);

                            int id = shader.GetPropertyNameId(i);
                            writer.WritePropertyName("id");
                            writer.WriteNumberValue(id);

                            writer.WritePropertyName("description");
                            if (shader.GetPropertyDescription(i) is { } desc)
                                writer.WriteStringValue(desc);
                            else
                                writer.WriteNullValue();

                            writer.WritePropertyName("flags");
                            writer.WriteStringValue(shader.GetPropertyFlags(i).ToString());
                            
                            ShaderPropertyType type = shader.GetPropertyType(i);
                            writer.WritePropertyName("type");
                            writer.WriteStringValue(type.ToString());

                            writer.WritePropertyName("default_value");
                            switch (type)
                            {
                                case ShaderPropertyType.Color:
                                    Color defaultColor = shader.GetPropertyDefaultVectorValue(i);
                                    JsonSerializer.Serialize(writer, defaultColor, DevkitServerConfig.SerializerSettings);

                                    writer.WritePropertyName("value");
                                    Color valueColor = material.GetColor(id);
                                    JsonSerializer.Serialize(writer, valueColor, DevkitServerConfig.SerializerSettings);
                                    if (valueColor != defaultColor)
                                    {
                                        writer.WritePropertyName("non_default");
                                        writer.WriteBooleanValue(true);
                                    }
                                    break;
                                case ShaderPropertyType.Vector:
                                    Vector4 defaultVector = shader.GetPropertyDefaultVectorValue(i);
                                    JsonSerializer.Serialize(writer, defaultVector, DevkitServerConfig.SerializerSettings);

                                    writer.WritePropertyName("value");
                                    Vector4 valueVector = material.GetVector(id);
                                    JsonSerializer.Serialize(writer, valueVector, DevkitServerConfig.SerializerSettings);
                                    if (valueVector != defaultVector)
                                    {
                                        writer.WritePropertyName("non_default");
                                        writer.WriteBooleanValue(true);
                                    }
                                    break;
                                case ShaderPropertyType.Float or ShaderPropertyType.Range:
                                    float defaultFloat = shader.GetPropertyDefaultFloatValue(i);
                                    writer.WriteNumberValue(defaultFloat);
                                    if (type == ShaderPropertyType.Range)
                                    {
                                        writer.WritePropertyName("value_range");
                                        JsonSerializer.Serialize(writer, shader.GetPropertyRangeLimits(i), DevkitServerConfig.SerializerSettings);
                                    }

                                    writer.WritePropertyName("value");
                                    float valueFloat = material.GetFloat(id);
                                    writer.WriteNumberValue(valueFloat);
                                    if (!MathfEx.IsNearlyEqual(valueFloat, defaultFloat))
                                    {
                                        writer.WritePropertyName("non_default");
                                        writer.WriteBooleanValue(true);
                                    }
                                    break;
                                case ShaderPropertyType.Texture:
                                    string? texture = shader.GetPropertyTextureDefaultName(i);
                                    writer.WriteStartObject();

                                    writer.WritePropertyName("texture");
                                    if (texture != null)
                                        writer.WriteStringValue(texture);
                                    else writer.WriteNullValue();

                                    writer.WritePropertyName("dimension");
                                    TextureDimension dim = shader.GetPropertyTextureDimension(i);
                                    writer.WriteStringValue(dim.ToString());

                                    writer.WriteEndObject();

                                    writer.WritePropertyName("value");
                                    Texture texture2 = material.GetTexture(id);
                                    if (texture2 == null)
                                    {
                                        writer.WriteNullValue();
                                        if (texture != null)
                                        {
                                            writer.WritePropertyName("non_default");
                                            writer.WriteBooleanValue(true);
                                        }
                                    }
                                    else
                                    {
                                        writer.WriteStartObject();

                                        writer.WritePropertyName("texture");
                                        writer.WriteStringValue(texture2.name);

                                        writer.WritePropertyName("dimension");
                                        writer.WriteStringValue(dim.ToString());

                                        writer.WritePropertyName("offset");
                                        JsonSerializer.Serialize(writer, material.GetTextureOffset(id), DevkitServerConfig.SerializerSettings);

                                        writer.WritePropertyName("scale");
                                        JsonSerializer.Serialize(writer, material.GetTextureScale(id), DevkitServerConfig.SerializerSettings);

                                        writer.WriteEndObject();

                                        if (!string.Equals(texture2.name, texture, StringComparison.Ordinal))
                                        {
                                            writer.WritePropertyName("non_default");
                                            writer.WriteBooleanValue(true);
                                        }
                                    }
                                    break;
                                default:
                                    writer.WriteNullValue();

                                    writer.WritePropertyName("value");
                                    writer.WriteNullValue();
                                    break;
                            }

                            string[] attributes = shader.GetPropertyAttributes(i) ?? Array.Empty<string>();
                            writer.WritePropertyName("attributes");
                            writer.WriteStartArray();
                            for (int j = 0; j < attributes.Length; ++j)
                                writer.WriteStringValue(attributes[j]);
                            writer.WriteEndArray();

                            writer.WriteEndObject();
                        }

                        writer.WriteEndArray();

                        writer.WriteEndObject();
                    }
                    else
                        writer.WriteNullValue();


                    writer.WritePropertyName("main_texture");
                    writer.WriteStartObject();

                    writer.WritePropertyName("offset");
                    JsonSerializer.Serialize(writer, material.mainTextureOffset, DevkitServerConfig.SerializerSettings);
                    
                    writer.WritePropertyName("scale");
                    JsonSerializer.Serialize(writer, material.mainTextureScale, DevkitServerConfig.SerializerSettings);

                    writer.WritePropertyName("path");
                    if (mainTexture is Texture2D mainTexture2d)
                    {
                        string path = Path.Combine(outPath, "Main Texture (" + mainTexture2d.name + ").png");
                        Save(mainTexture2d, path);
                        writer.WriteStringValue(path);
                    }
                    else writer.WriteNullValue();

                    writer.WriteEndObject();

                    writer.WritePropertyName("texture_properties");
                    writer.WriteStartObject();
                    string[] properties = material.GetTexturePropertyNames();
                    for (int i = 0; i < properties.Length; ++i)
                    {
                        string propertyName = properties[i];
                        Texture? texture = material.GetTexture(propertyName);
                        Vector2 offset = material.GetTextureOffset(propertyName);
                        Vector2 scale = material.GetTextureScale(propertyName);
                        writer.WritePropertyName(propertyName);
                        writer.WriteStartObject();

                        writer.WritePropertyName("offset");
                        JsonSerializer.Serialize(writer, offset, DevkitServerConfig.SerializerSettings);

                        writer.WritePropertyName("scale");
                        JsonSerializer.Serialize(writer, scale, DevkitServerConfig.SerializerSettings);

                        writer.WritePropertyName("path");
                        if (texture is Texture2D t2d)
                        {
                            string path = Path.Combine(outPath, "Textures", propertyName + " (" + t2d.name + ").png");
                            Save(t2d, path);
                            writer.WriteStringValue(path);
                        }
                        else writer.WriteNullValue();
                        writer.WriteEndObject();
                    }
                    writer.WriteEndObject();

                    writer.WriteEndObject();

                    writer.Flush();
                }
                return true;
            default:
                throw new NotSupportedException("Saving " + @object.GetType().Name);
        }
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