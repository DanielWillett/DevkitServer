using DanielWillett.ReflectionTools;
using System.Reflection;

namespace DevkitServer.API;

/// <summary>
/// Add this to a static field or property to have the directory of the value stored in it created on load.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class CreateDirectoryAttribute : Attribute
{
    private static List<Type>? _checkedTypes = new List<Type>(64);

    /// <summary>
    /// Is the path relative to <see cref="UnturnedPaths.RootDirectory"/>?
    /// </summary>
    public bool RelativeToGameDir { get; set; }
    internal bool FaultOnFailure { get; set; } = true;
    internal static void DisposeLoadList() => _checkedTypes = null;

    /// <summary>
    /// Find all <see cref="CreateDirectoryAttribute"/>s in the given assembly and create their respective directories.
    /// </summary>
    public static void CreateInAssembly(Assembly assembly) => CreateInAssembly(assembly, false);

    /// <summary>
    /// Find all <see cref="CreateDirectoryAttribute"/>s in the given type and create their respective directories.
    /// </summary>
    public static void CreateInType(Type type) => CreateInType(type, false);
    internal static void CreateInAssembly(Assembly assembly, bool allowFault)
    {
        List<Type> types = Accessor.GetTypesSafe(assembly, false);
        for (int index = 0; index < types.Count; index++)
        {
            Type type = types[index];
            CreateInType(type, allowFault);
        }
    }
    internal static void CreateInType(Type type, bool allowFault)
    {
        if (_checkedTypes != null && _checkedTypes.Contains(type))
            return;
        FieldInfo[] fields = type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        for (int f = 0; f < fields.Length; ++f)
        {
            FieldInfo field = fields[f];
            if (!field.IsStatic || IsDefined(field, typeof(IgnoreAttribute)) || GetCustomAttribute(field, typeof(CreateDirectoryAttribute)) is not CreateDirectoryAttribute cdir)
                continue;
            if (typeof(string).IsAssignableFrom(field.FieldType))
            {
                try
                {
                    string? path = (string?)field.GetValue(null);
                    if (path == null)
                        Logger.DevkitServer.LogWarning("CHECK DIR", $"Unable to check directory for {field.Format()}, field returned {((object?)null).Format()}.");
                    else FileUtil.CheckDirectory(cdir.RelativeToGameDir, allowFault && cdir.FaultOnFailure, path, field);
                }
                catch (Exception ex)
                {
                    Logger.DevkitServer.LogWarning("CHECK DIR", ex, $"Unable to check directory for {field.Format()}, type initializer threw exception.");
                }
            }
            else if (typeof(FileInfo).IsAssignableFrom(field.FieldType))
            {
                try
                {
                    FileInfo? fileInfo = (FileInfo?)field.GetValue(null);
                    cdir.RelativeToGameDir = false;
                    string? file = fileInfo?.DirectoryName;
                    if (file == null)
                    {
                        if (fileInfo == null)
                            Logger.DevkitServer.LogWarning("CHECK DIR", $"Unable to check directory for {field.Format()}, field returned {((object?)null).Format()}.");
                    }
                    else FileUtil.CheckDirectory(false, allowFault && cdir.FaultOnFailure, file, field);
                }
                catch (Exception ex)
                {
                    Logger.DevkitServer.LogWarning("CHECK DIR", ex, $"Unable to check directory for {field.Format()}, type initializer threw exception.");
                }
            }
            else if (typeof(DirectoryInfo).IsAssignableFrom(field.FieldType))
            {
                try
                {
                    string? dir = ((DirectoryInfo?)field.GetValue(null))?.FullName;
                    cdir.RelativeToGameDir = false;
                    if (dir == null)
                        Logger.DevkitServer.LogWarning("CHECK DIR", $"Unable to check directory for {field.Format()}, field returned {((object?)null).Format()}.");
                    else FileUtil.CheckDirectory(false, allowFault && cdir.FaultOnFailure, dir, field);
                }
                catch (Exception ex)
                {
                    Logger.DevkitServer.LogWarning("CHECK DIR", ex, $"Unable to check directory for {field.Format()}, type initializer threw exception.");
                }
            }
            else
            {
                Logger.DevkitServer.LogWarning("CHECK DIR", $"Unable to check directory for {field.Format()}, valid on types: " +
                                                            $"{typeof(string).Format()}, {typeof(FileInfo).Format()}, or {typeof(DirectoryInfo).Format()}.");
            }
        }

        PropertyInfo[] properties = type.GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        for (int p = 0; p < properties.Length; ++p)
        {
            PropertyInfo property = properties[p];
            if (property.GetMethod == null || property.GetMethod.IsStatic || property.GetIndexParameters() is { Length: > 0 } ||
                IsDefined(property, typeof(IgnoreAttribute)) || GetCustomAttribute(property, typeof(CreateDirectoryAttribute)) is not CreateDirectoryAttribute cdir)
                continue;
            if (typeof(string).IsAssignableFrom(property.PropertyType))
            {
                try
                {
                    string? path = (string?)property.GetMethod?.Invoke(null, Array.Empty<object>());
                    if (path == null)
                        Logger.DevkitServer.LogWarning("CHECK DIR", $"Unable to check directory for {property.Format()}, field returned {((object?)null).Format()}.");
                    else FileUtil.CheckDirectory(cdir.RelativeToGameDir, allowFault && cdir.FaultOnFailure, path, property);
                }
                catch (Exception ex)
                {
                    Logger.DevkitServer.LogWarning("CHECK DIR", ex, $"Unable to check directory for {property.Format()}, property getter or type initializer threw exception.");
                }
            }
            else if (typeof(FileInfo).IsAssignableFrom(property.PropertyType))
            {
                try
                {
                    FileInfo? fileInfo = (FileInfo?)property.GetMethod?.Invoke(null, Array.Empty<object>());
                    string? file = fileInfo?.DirectoryName;
                    if (file == null)
                    {
                        if (fileInfo == null)
                            Logger.DevkitServer.LogWarning("CHECK DIR", $"Unable to check directory for {property.Format()}, field returned {((object?)null).Format()}.");
                    }
                    else FileUtil.CheckDirectory(false, allowFault && cdir.FaultOnFailure, file, property);
                }
                catch (Exception ex)
                {
                    Logger.DevkitServer.LogWarning("CHECK DIR", ex, $"Unable to check directory for {property.Format()}, property getter or type initializer threw exception.");
                }
            }
            else if (typeof(DirectoryInfo).IsAssignableFrom(property.PropertyType))
            {
                try
                {
                    string? dir = ((DirectoryInfo?)property.GetMethod?.Invoke(null, Array.Empty<object>()))?.FullName;
                    if (dir == null)
                        Logger.DevkitServer.LogWarning("CHECK DIR", $"Unable to check directory for {property.Format()}, field returned {((object?)null).Format()}.");
                    else FileUtil.CheckDirectory(false, allowFault && cdir.FaultOnFailure, dir, property);
                }
                catch (Exception ex)
                {
                    Logger.DevkitServer.LogWarning("CHECK DIR", ex, $"Unable to check directory for {property.Format()}, property getter or type initializer threw exception.");
                }
            }
            else
            {
                Logger.DevkitServer.LogWarning("CHECK DIR", $"Unable to check directory for {property.Format()}, valid on types: " +
                                                            $"{typeof(string).Format()}, {typeof(FileInfo).Format()}, or {typeof(DirectoryInfo).Format()}.");
            }
        }

        _checkedTypes?.Add(type);
    }
}
