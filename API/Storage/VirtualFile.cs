namespace DevkitServer.API.Storage;

/// <summary>
/// Represents a file stored in memory.
/// </summary>
public sealed class VirtualFile(string path, ArraySegment<byte> content)
{
    /// <summary>
    /// Path of the file relative to a <see cref="VirtualDirectoryRoot"/>.
    /// </summary>
    public string Path { get; } = path;

    /// <summary>
    /// Raw contents of the file.
    /// </summary>
    public ArraySegment<byte> Content { get; } = content;
}