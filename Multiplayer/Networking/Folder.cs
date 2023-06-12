using System.Text;
using DevkitServer.Util.Encoding;
using SDG.Framework.Utilities;

namespace DevkitServer.Multiplayer.Networking;

public struct Folder
{
    public string FolderName;
    public string[] Folders;
    public File[] Files;
    public struct File
    {
        public byte[] Content;
        public string Path;
    }
    public Folder(string directoryPath)
    {
        DirectoryInfo info = new DirectoryInfo(directoryPath);
        if (info.Exists)
        {
            string parentPath = info.FullName;
            FolderName = info.Name;
            DirectoryInfo[] dirs = info.EnumerateDirectories("*", SearchOption.AllDirectories).ToArray();
            FileInfo[] files = info.EnumerateFiles("*", SearchOption.AllDirectories).ToArray();
            Folders = new string[dirs.Length];
            for (int i = 0; i < dirs.Length; i++)
            {
                Folders[i] = GetRelativePath(parentPath, dirs[i].FullName);
            }
            Files = new File[files.Length];
            for (int i = 0; i < files.Length; i++)
            {
                Files[i] = new File
                {
                    Path = GetRelativePath(parentPath, files[i].FullName),
                    Content = System.IO.File.ReadAllBytes(files[i].FullName)
                };
            }
        }
        else throw new DirectoryNotFoundException();
    }
    public Folder(string directoryPath, Predicate<FileInfo>? shouldIncludeFile, Predicate<DirectoryInfo>? shouldIncludeFolder)
    {
        DirectoryInfo info = new DirectoryInfo(directoryPath);
        if (info.Exists)
        {
            string parentPath = info.FullName;
            FolderName = info.Name;
            DirectoryInfo[] dirs = info.EnumerateDirectories("*", SearchOption.AllDirectories).ToArray();
            FileInfo[] files = info.EnumerateFiles("*", SearchOption.AllDirectories).ToArray();
            if (shouldIncludeFolder != null)
            {
                List<string> foldersOut = ListPool<string>.claim();
                try
                {
                    for (int i = 0; i < dirs.Length; i++)
                    {
                        if (shouldIncludeFolder(dirs[i]))
                        {
                            foldersOut.Add(FormatPath(GetRelativePath(parentPath, dirs[i].FullName)));
                        }
                    }

                    Folders = foldersOut.ToArray();
                }
                finally
                {
                    ListPool<string>.release(foldersOut);
                }
            }
            else
            {
                Folders = new string[dirs.Length];
                for (int i = 0; i < dirs.Length; i++)
                {
                    Folders[i] = FormatPath(GetRelativePath(parentPath, dirs[i].FullName));
                }
            }
            if (shouldIncludeFile != null)
            {
                List<File> filesOut = ListPool<File>.claim();
                try
                {
                    for (int i = 0; i < files.Length; i++)
                    {
                        if (shouldIncludeFile(files[i]))
                        {
                            filesOut.Add(new File
                            {
                                Path = FormatPath(GetRelativePath(parentPath, files[i].FullName)),
                                Content = System.IO.File.ReadAllBytes(files[i].FullName)
                            });
                        }
                    }

                    Files = filesOut.ToArray();
                }
                finally
                {
                    ListPool<File>.release(filesOut);
                }
            }
            else
            {
                Files = new File[files.Length];
                for (int i = 0; i < files.Length; i++)
                {
                    Files[i] = new File
                    {
                        Path = FormatPath(GetRelativePath(parentPath, files[i].FullName)),
                        Content = System.IO.File.ReadAllBytes(files[i].FullName)
                    };
                }
            }
        }
        else throw new DirectoryNotFoundException();
    }
    private static string FormatPath(string path)
    {
        return Path.DirectorySeparatorChar == '\\' ? path : path.Replace('/', '\\');
    }
    private static string UnformatPath(string path)
    {
        return Path.DirectorySeparatorChar == '\\' ? path : path.Replace('\\', Path.DirectorySeparatorChar);
    }
    public readonly void WriteContentsToDisk(string directory)
    {
        for (int i = 0; i < Folders.Length; ++i)
        {
            Directory.CreateDirectory(Path.Combine(directory, Folders[i]));
        }
        for (int i = 0; i < Files.Length; i++)
        {
            ref File file = ref Files[i];
            string path = Path.Combine(directory, file.Path);
            string? dir = Path.GetDirectoryName(file.Path);
            if (dir != null) Directory.CreateDirectory(dir);
            using FileStream stream = new FileStream(path, System.IO.File.Exists(path) ? FileMode.Truncate : FileMode.Create, FileAccess.Write, FileShare.Read);
            stream.Write(file.Content, 0, file.Content.Length);
        }
    }
    public readonly void WriteToDisk(string directory)
    {
        string b = Directory.CreateDirectory(Path.Combine(directory, FolderName)).FullName;
        WriteContentsToDisk(b);
    }
    public static Folder Read(ByteReader reader)
    {
        Folder folder = new Folder
        {
            FolderName = reader.ReadString(),
            Folders = new string[reader.ReadInt32()]
        };
        
        for (int i = 0; i < folder.Folders.Length; ++i)
        {
            folder.Folders[i] = UnformatPath(reader.ReadString());
        }
        int len = reader.ReadInt32();
        folder.Files = new File[len];
        for (int i = 0; i < len; ++i)
        {
            folder.Files[i] = new File
            {
                Path = UnformatPath(reader.ReadString()),
                Content = reader.ReadLongUInt8Array() ?? Array.Empty<byte>()
            };
            // Logger.LogInfo("File: " + folder.Files[i].Path + " (" + DevkitServerUtility.FormatBytes(folder.Files[i].Content.Length) + ")");
        }
        return folder;
    }

    public static void Write(ByteWriter writer, Folder folder) => Write(writer, in folder);
    public static void Write(ByteWriter writer, in Folder folder)
    {
        writer.Write(folder.FolderName);
        writer.Write(folder.Folders.Length);
        for (int i = 0; i < folder.Folders.Length; ++i)
            writer.Write(FormatPath(folder.Folders[i]));
        
        File[] fls = folder.Files;
        writer.Write(fls.Length);
        for (int i = 0; i < fls.Length; ++i)
        {
            ref File file = ref fls[i];
            writer.Write(FormatPath(file.Path));
            writer.WriteLong(file.Content);
        }
    }
    // https://stackoverflow.com/questions/51179331/is-it-possible-to-use-path-getrelativepath-net-core2-in-winforms-proj-targeti
    private static string GetRelativePath(string relativeTo, string path)
    {
        Uri uri = new Uri(relativeTo);
        string rel = Uri.UnescapeDataString(uri.MakeRelativeUri(new Uri(path)).ToString()).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        if (!rel.Contains(Path.DirectorySeparatorChar.ToString()))
            rel = "." + Path.DirectorySeparatorChar + rel;
        
        return rel;
    }

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder(" • " + FolderName + ":" + Environment.NewLine);
        List<string> folders = Folders.OrderBy(x => x).ToList();
        List<File> files = Files.OrderBy(x => x.Path).ToList();
        sb.AppendLine(" + Folders:");
        foreach (string folder in folders)
        {
            sb.AppendLine("  • \"" + folder + "\"");
        }
        sb.AppendLine(" + Files:");
        foreach (File file in files)
        {
            sb.AppendLine($"  • \"{file.Path}\" ({file.Content.Length:N} B)");
        }

        return sb.ToString();
    }
}