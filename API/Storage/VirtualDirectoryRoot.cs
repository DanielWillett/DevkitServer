﻿using System.Text;

namespace DevkitServer.API.Storage;

/// <summary>
/// Stores a file structure and contents in-memory.
/// </summary>
public class VirtualDirectoryRoot(IReadOnlyList<string> directories, IReadOnlyList<VirtualFile> files)
{
    /// <summary>
    /// List of all directories.
    /// </summary>
    public IReadOnlyList<string> Directories { get; } = directories;

    /// <summary>
    /// List of all files and their contents.
    /// </summary>
    public IReadOnlyList<VirtualFile> Files { get; } = files;

    /// <summary>
    /// Dumps all folders and files (and their sizes) to printable text.
    /// </summary>
    public override string ToString()
    {
        StringBuilder sb = new StringBuilder(" • Virtual Directory:" + Environment.NewLine);

        sb.AppendLine(" + Folders:");
        foreach (string folder in Directories.OrderBy(x => x).ToList())
        {
            sb.AppendLine("  • \"" + folder + "\"");
        }

        sb.AppendLine(" + Files:");
        foreach (VirtualFile file in Files.OrderBy(x => x.Path).ToList())
        {
            sb.AppendLine($"  • \"{file.Path}\" ({DevkitServerUtility.FormatBytes(file.Content.Count)})");
        }

        return sb.ToString();
    }
}