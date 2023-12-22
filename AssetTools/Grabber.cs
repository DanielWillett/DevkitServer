using DevkitServer.API;
using DevkitServer.Configuration;
using StackCleaner;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using UnityEngine.Rendering;

namespace DevkitServer.AssetTools;
public static class Grabber
{
    public static bool DownloadResource<T>(StaticResourceRef<T> path, string outPath) where T : Object
    {
        ThreadUtil.assertIsGameThread();

        T resource = path.GetOrLoad();
        if (resource == null)
        {
            Logger.DevkitServer.LogDebug(nameof(DownloadResource), "Resource not found: " + path.Format() + ".");
            return false;
        }

        bool result = Save(resource, outPath);

        Resources.UnloadAsset(resource);
        return result;
    }
    public static bool DownloadResource<T>(string path, string outPath) where T : Object
    {
        ThreadUtil.assertIsGameThread();

        T resource = Resources.Load<T>(path);
        bool result;
        if (resource == null)
        {
            Shader? shader = Shader.Find(path);
            if (shader == null)
            {
                Logger.DevkitServer.LogDebug(nameof(DownloadResource), "Resource not found: " + path.Format() + ".");
                return false;
            }

            result = Save(shader, outPath);
            Resources.UnloadAsset(shader);
        }
        else
            result = Save(resource, outPath);

        if (resource != null)
            Resources.UnloadAsset(resource);
        return result;
    }
    public static bool DownloadFromBundle<T>(Bundle bundle, string path, string outPath) where T : Object
    {
        ThreadUtil.assertIsGameThread();

        T resource = bundle.load<T>(path);
        if (resource == null)
        {
            Logger.DevkitServer.LogDebug(nameof(DownloadFromBundle), "Asset not found: " + bundle.name.Format(false) + "::" + path.Format(false) + ".");
            return false;
        }

        return Save(resource, outPath);
    }
    
    public static bool Save<T>(T @object, string outPath) where T : Object
    {
        ThreadUtil.assertIsGameThread();

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
            case Mesh mesh:
            {
                outPath = Path.Combine(Path.GetDirectoryName(outPath) ?? string.Empty, Path.GetFileNameWithoutExtension(outPath)) + ".obj";
                string? dir = Path.GetDirectoryName(outPath);
                if (dir != null)
                    FileUtil.CheckDirectory(false, dir);

                using FileStream fs = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                ExportMesh(mesh, fs, null, null);
                return true;
            }
            case Material material:
            {
                outPath = Path.Combine(Path.GetDirectoryName(outPath) ?? string.Empty, Path.GetFileNameWithoutExtension(outPath));
                if (Directory.Exists(outPath))
                    Directory.Delete(outPath, true);

                Directory.CreateDirectory(outPath);
                using FileStream stream = new FileStream(Path.Combine(outPath, "material_data.json"), FileMode.Create, FileAccess.Write, FileShare.Read);
                ExportMaterialJson(material, stream, outPath);
                return true;
            }
            case Shader shader:
            {
                outPath = Path.Combine(Path.GetDirectoryName(outPath) ?? string.Empty, Path.GetFileNameWithoutExtension(outPath)) + ".json";
                string? dir = Path.GetDirectoryName(outPath);
                if (dir != null)
                    FileUtil.CheckDirectory(false, dir);

                using FileStream fs = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                using Utf8JsonWriter writer = new Utf8JsonWriter(fs, DevkitServerConfig.WriterOptions);
                ExportShaderJson(shader, null, writer);
                writer.Flush();
                return true;
            }
            case GameObject gameObject:
            {
                outPath = Path.Combine(Path.GetDirectoryName(outPath) ?? string.Empty, Path.GetFileNameWithoutExtension(outPath));
                if (Directory.Exists(outPath))
                    Directory.Delete(outPath, true);

                Directory.CreateDirectory(outPath);

                // meshes, materials, textures
                if (gameObject.TryGetComponent(out Renderer renderer))
                {
                    FileUtil.CheckDirectory(false, outPath);
                    Material[] materials = renderer.materials;
                    if (gameObject.TryGetComponent(out MeshFilter filter))
                    {
                        using FileStream stream = new FileStream(Path.Combine(outPath, "mesh.obj"), FileMode.Create, FileAccess.Write, FileShare.Read);
                        ExportMesh(filter.sharedMesh, stream, outPath, materials);
                    }
                    else
                    {
                        using FileStream stream = new FileStream(Path.Combine(outPath, "materials.mtl"), FileMode.Create, FileAccess.Write, FileShare.Read);
                        ExportMaterialsWavefront(materials, out _, stream, outPath);
                    }
                    for (int i = 0; i < materials.Length; ++i)
                    {
                        string dir = Path.Combine(outPath, "Materials", i.ToString(CultureInfo.InvariantCulture) + "_" + materials[i].name);
                        FileUtil.CheckDirectory(false, dir);
                        using FileStream stream2 = new FileStream(Path.Combine(dir, "material.json"), FileMode.Create, FileAccess.Write, FileShare.Read);
                        ExportMaterialJson(materials[i], stream2, dir);
                    }
                }
                
                // component and thier values values
                using FileStream compStream = new FileStream(Path.Combine(outPath, "components.txt"), FileMode.Create, FileAccess.Write, FileShare.Read);
                using StreamWriter writer = new StreamWriter(compStream, Encoding.UTF8, 512, leaveOpen: false);
                Component[] components = gameObject.GetComponents<Component>();
                ITerminalFormatProvider oldProvider = FormattingUtil.FormatProvider;

                FormattingUtil.FormatProvider = new CustomTerminalFormatProvider(new StackTraceCleaner(new StackCleanerConfiguration
                {
                    ColorFormatting = StackColorFormatType.None
                }));
                
                writer.WriteLine($"# Components ({components.Length})");
                for (int i = 0; i < components.Length; ++i)
                {
                    Type type = components[i].GetType();
                    writer.WriteLine();
                    writer.Write(type.FullName);
                    writer.WriteLine(":");

                    foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.FlattenHierarchy | BindingFlags.Public)
                                 .Where(x => x.GetMethod != null))
                    {
                        writer.Write("    ");
                        writer.Write(property.Format());
                        writer.Write(" = ");

                        try
                        {
                            object? val = property.GetValue(components[i]);
                            try
                            {
                                writer.Write(val == null ? "null" : ("\"" + val.Format("0.########") + "\""));
                            }
                            catch (Exception ex)
                            {
                                writer.Write("FMT ERROR: " + ex.GetType().Name + " " + ex.Message);
                                try
                                {
                                    writer.Write("\"" + val.Format() + "\"");
                                }
                                catch (Exception ex2)
                                {
                                    writer.Write("RETRY FMT ERROR: " + ex2.GetType().Name + " " + ex2.Message);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            writer.Write("GET ERROR: " + ex.GetType().Name + " " + ex.Message);
                        }
                        
                        writer.WriteLine(';');
                    }

                    foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.FlattenHierarchy | BindingFlags.Public))
                    {
                        writer.Write("    ");
                        writer.Write(field.Format());
                        writer.Write(" = ");
                        object? val = field.GetValue(components[i]);
                        try
                        {
                            writer.Write(val == null ? "null" : ("\"" + val.Format("0.########") + "\""));
                        }
                        catch (Exception ex)
                        {
                            writer.Write("FMT ERROR: " + ex.GetType().Name + " " + ex.Message);
                            try
                            {
                                writer.Write("\"" + val.Format() + "\"");
                            }
                            catch (Exception ex2)
                            {
                                writer.Write("RETRY FMT ERROR: " + ex2.GetType().Name + " " + ex2.Message);
                            }
                        }
                        writer.WriteLine(';');
                    }
                }

                FormattingUtil.FormatProvider = oldProvider;
                int children = gameObject.transform.childCount;
                writer.WriteLine();
                writer.WriteLine();
                writer.WriteLine(children == 0 ? "No Children" : $"Children ({children}): ");
                for (int i = 0; i < children; ++i)
                {
                    Transform child = gameObject.transform.GetChild(i);
                    writer.WriteLine(i.ToString(CultureInfo.InvariantCulture) + ". " + child.name);
                    Save(child.gameObject, Path.Combine(outPath, "Child_" + i.ToString(CultureInfo.InvariantCulture) + "_" + child.gameObject.name + "."));
                }
                writer.Flush();

                return true;
            }
            default:
                throw new NotSupportedException("Saving " + @object.GetType().Name);
        }
    }
    private static void ExportMesh(Mesh mesh, Stream stream, string? exportDirectory, Material[]? materials)
    {
        using StreamWriter writer = new StreamWriter(stream, Encoding.UTF8, 4096, leaveOpen: true);

        writer.Write("# Mesh: \"");
        writer.Write(mesh.name);
        writer.WriteLine('"');
        writer.WriteLine();
        bool exportedMaterials = exportDirectory != null && materials is { Length: > 0 };
        string[]? matNames = null;
        if (exportedMaterials)
        {
            string name = "materials_" + mesh.name + ".mtl";

            using (FileStream fs = new FileStream(Path.Combine(exportDirectory!, name), FileMode.Create, FileAccess.Write, FileShare.Read))
                ExportMaterialsWavefront(materials ?? Array.Empty<Material>(), out matNames, fs, exportDirectory);

            writer.WriteLine("# Material Library");
            writer.Write("mtllib ");
            writer.WriteLine(name);
            writer.WriteLine();
        }

        if (materials is { Length: > 0 })
        {
            writer.WriteLine("# Materials");
            for (int i = 0; i < materials.Length; ++i)
            {
                string name = matNames == null || matNames.Length <= i ? materials[i].name : matNames[i];
                if (i != 0)
                    writer.WriteLine();
                writer.Write("usemtl ");
                writer.WriteLine(name);
            }
            writer.WriteLine();
        }

        writer.WriteLine("# Vertices");
        foreach (Vector3 vertex in mesh.vertices)
            writer.WriteLine($"v {vertex.x.ToString("F6", CultureInfo.InvariantCulture)} {vertex.y.ToString("F6", CultureInfo.InvariantCulture)} {vertex.z.ToString("F6", CultureInfo.InvariantCulture)}");

        writer.WriteLine();

        writer.WriteLine("# Normals");
        foreach (Vector3 normal in mesh.normals)
            writer.WriteLine($"vn {normal.x.ToString("F6", CultureInfo.InvariantCulture)} {normal.y.ToString("F6", CultureInfo.InvariantCulture)} {normal.z.ToString("F6", CultureInfo.InvariantCulture)}");

        writer.WriteLine();

        writer.WriteLine("# Texture Coords");
        foreach (Vector2 uv in mesh.uv)
            writer.WriteLine($"vt {uv.x.ToString("F6", CultureInfo.InvariantCulture)} {uv.y.ToString("F6", CultureInfo.InvariantCulture)}");

        int ct = mesh.subMeshCount;
        List<int> triangles = new List<int>(32);

        writer.WriteLine();
        writer.WriteLine("# Faces");
        for (int i = 0; i < ct; ++i)
        {
            if (i != 0)
                writer.WriteLine();
            if (ct > 1)
                writer.WriteLine($"# Submesh {i.ToString(CultureInfo.InvariantCulture)}");
            mesh.GetTriangles(triangles, i);
            for (int t = 0; t < triangles.Count; t += 3)
            {
                string t1 = (triangles[t] + 1).ToString(CultureInfo.InvariantCulture),
                       t2 = (triangles[t + 1] + 1).ToString(CultureInfo.InvariantCulture),
                       t3 = (triangles[t + 2] + 1).ToString(CultureInfo.InvariantCulture);
                writer.WriteLine($"f {t1}/{t1}/{t1} {t2}/{t2}/{t2} {t3}/{t3}/{t3}");
            }
        }

        writer.WriteLine();
        writer.WriteLine("# End Of File");

        writer.Flush();
    }
    private static void ExportMaterialsWavefront(Material[] materials, out string[] materialNames, Stream stream, string? exportDirectroy)
    {
        using StreamWriter writer = new StreamWriter(stream, Encoding.UTF8, 4096, leaveOpen: true);

        List<string> names = new List<string>(materials == null ? 0 : materials.Length);

        if (materials is not { Length: > 0 })
        {
            materialNames = Array.Empty<string>();
            return;
        }

        materialNames = new string[materials.Length];
        for (int i = 0; i < materials.Length; ++i)
        {
            Material material = materials[i];
            Color color = material.color;
            if (i != 0)
                writer.WriteLine();
            writer.WriteLine("# Material " + i.ToString(CultureInfo.InvariantCulture) + ": " + material.name);
            string name = GetUniqueName(material.name, names);
            materialNames[i] = name;
            names.Add(name);
            writer.Write("newmtl ");
            writer.WriteLine(name);

            writer.WriteLine("Ka 1.000000 1.000000 1.000000");
            writer.Write("Kd ");
            writer.Write(color.r.ToString("F6"));
            writer.Write(" ");
            writer.Write(color.g.ToString("F6"));
            writer.Write(" ");
            writer.WriteLine(color.b.ToString("F6"));
            writer.WriteLine("Ks 0.000000 0.000000 0.000000");
            writer.WriteLine("Ns 100.000000");
            writer.WriteLine("Ni 1.000000");
            writer.WriteLine("d 1.000000");
            writer.WriteLine("illum 0");
            writer.WriteLine();

            int[] textures = material.GetTexturePropertyNameIDs();
            for (int t = 0; t < textures.Length; ++t)
            {
                Texture texture = material.GetTexture(textures[t]);
                if (texture is Texture2D t2d)
                {
                    string textureName = name + "_T_" + t.ToString(CultureInfo.InvariantCulture) + "_" +
                                         texture.name;
                    writer.Write("map_Kd");
                    writer.WriteLine(textureName);
                    if (exportDirectroy != null)
                    {
                        string path = Path.Combine(exportDirectroy, textureName + ".png");
                        Save(t2d, path);
                        writer.WriteLine($"# Texture \"{texture.name}\" saved to \"{path}\".");
                    }
                }
                else if (texture is null)
                {
                    writer.WriteLine("# No texture.");
                }
                else
                {
                    writer.WriteLine($"# Texture \"{texture.name}\" was not a raw image.");
                }
            }
        }
    }
    private static string GetUniqueName(string preferred, IList<string> cachedNames)
    {
        if (!cachedNames.Contains(preferred, StringComparer.OrdinalIgnoreCase))
            return preferred;

        int num = 2;

        string newName;
        while (cachedNames.Contains(newName = preferred + "_" + num.ToString(CultureInfo.InvariantCulture), StringComparer.OrdinalIgnoreCase))
            ++num;

        return newName;
    }
    private static void ExportShaderJson(Shader shader, Material? valueProvider, Utf8JsonWriter writer)
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

                    if (valueProvider != null)
                    {
                        writer.WritePropertyName("value");
                        Color valueColor = valueProvider.GetColor(id);
                        JsonSerializer.Serialize(writer, valueColor, DevkitServerConfig.SerializerSettings);
                        if (valueColor != defaultColor)
                        {
                            writer.WritePropertyName("non_default");
                            writer.WriteBooleanValue(true);
                        }
                    }
                    break;
                case ShaderPropertyType.Vector:
                    Vector4 defaultVector = shader.GetPropertyDefaultVectorValue(i);
                    JsonSerializer.Serialize(writer, defaultVector, DevkitServerConfig.SerializerSettings);

                    if (valueProvider != null)
                    {
                        writer.WritePropertyName("value");
                        Vector4 valueVector = valueProvider.GetVector(id);
                        JsonSerializer.Serialize(writer, valueVector, DevkitServerConfig.SerializerSettings);
                        if (valueVector != defaultVector)
                        {
                            writer.WritePropertyName("non_default");
                            writer.WriteBooleanValue(true);
                        }
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
                    if (valueProvider != null)
                    {
                        writer.WritePropertyName("value");
                        float valueFloat = valueProvider.GetFloat(id);
                        writer.WriteNumberValue(valueFloat);
                        if (!MathfEx.IsNearlyEqual(valueFloat, defaultFloat))
                        {
                            writer.WritePropertyName("non_default");
                            writer.WriteBooleanValue(true);
                        }
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

                    if (valueProvider != null)
                    {
                        writer.WritePropertyName("value");
                        Texture texture2 = valueProvider.GetTexture(id);
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
                            JsonSerializer.Serialize(writer, valueProvider.GetTextureOffset(id), DevkitServerConfig.SerializerSettings);

                            writer.WritePropertyName("scale");
                            JsonSerializer.Serialize(writer, valueProvider.GetTextureScale(id), DevkitServerConfig.SerializerSettings);

                            writer.WriteEndObject();

                            if (!string.Equals(texture2.name, texture, StringComparison.Ordinal))
                            {
                                writer.WritePropertyName("non_default");
                                writer.WriteBooleanValue(true);
                            }
                        }
                    }
                    break;
                default:
                    writer.WriteNullValue();
                    if (valueProvider != null)
                    {
                        writer.WritePropertyName("value");

                        writer.WriteNullValue();
                    }
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
    private static void ExportMaterialJson(Material material, Stream stream, string? exportDirectory)
    {
        using Utf8JsonWriter writer = new Utf8JsonWriter(stream, DevkitServerConfig.WriterOptions);
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
            ExportShaderJson(shader, material, writer);
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
        if (exportDirectory != null && mainTexture is Texture2D mainTexture2d)
        {
            string path = Path.Combine(exportDirectory, "Main Texture (" + mainTexture2d.name + ").png");
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
            if (exportDirectory != null && texture is Texture2D t2d)
            {
                string path = Path.Combine(exportDirectory, "Textures", propertyName + " (" + t2d.name + ").png");
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
    public static byte[] EncodeOrBlitTexturePNG(this Texture2D texture, bool destroy)
    {
        Texture2D useableTexture = ConvertToReadable(texture);

        byte[] bytes = useableTexture.EncodeToPNG();

        if (texture != useableTexture)
            Object.Destroy(useableTexture);

        if (destroy)
            Object.Destroy(texture);

        return bytes ?? Array.Empty<byte>();
    }
    public static Texture2D ConvertToReadable(Texture2D texture)
    {
        ThreadUtil.assertIsGameThread();

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
        return outTexture;
    }
}