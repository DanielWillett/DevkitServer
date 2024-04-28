using DanielWillett.SpeedBytes;
using System.Collections.Concurrent;

namespace DevkitServer.API.Storage;

/// <summary>
/// Helper methods for <see cref="VirtualDirectoryRoot"/>.
/// </summary>
public static class VirtualDirectories
{
    private static HashSet<string>? _restrictedFileTypes;
    private static IReadOnlyList<string>? _restrictedFileTypesRo;

    /// <summary>
    /// All file types that are usually restricted by <see cref="Save"/> and <see cref="SaveAsync"/>.
    /// </summary>
    public static IReadOnlyList<string> RestrictedFileTypes => _restrictedFileTypesRo ??= new List<string>(RestrictedFileTypesIntl).AsReadOnly();
    
    /// <summary>
    /// Create an in-memory copy of a file-system.
    /// </summary>
    /// <exception cref="DirectoryNotFoundException"/>
    public static VirtualDirectoryRoot Create(string directoryRoot,
        Predicate<FileInfo>? shouldIncludeFile = null, Predicate<DirectoryInfo>? shouldIncludeDirectory = null)
    {
        DirectoryInfo info = new DirectoryInfo(directoryRoot);

        if (!info.Exists)
            throw new DirectoryNotFoundException();

        VirtualDirectoryBuilder builder = new VirtualDirectoryBuilder(info.FullName);

        foreach (FileSystemInfo sysInfo in info.EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
        {
            if (sysInfo is FileInfo file)
            {
                if (shouldIncludeFile != null && !shouldIncludeFile(file))
                    continue;

                long fileSize = file.Length;

                if (builder.TotalLength + fileSize > 1073741824L)
                    builder.FlushQueue();

                builder.FilePaths.Enqueue(file.FullName);
                builder.TotalLength += fileSize;
            }
            else if (sysInfo is DirectoryInfo directory)
            {
                if (shouldIncludeDirectory != null && !shouldIncludeDirectory(directory))
                    continue;

                builder.Directories.Add(Path.GetRelativePath(builder.Root, directory.FullName));
            }
        }
        
        builder.FlushQueue();
        return new VirtualDirectoryRoot(builder.Directories.ToArray(), builder.Files.ToArray());
    }

    /// <summary>
    /// Create an in-memory copy of a file-system with asynchronous reading.
    /// </summary>
    /// <exception cref="DirectoryNotFoundException"/>
    /// <exception cref="OperationCanceledException"/>
    public static async Task<VirtualDirectoryRoot> CreateAsync(string directoryRoot,
        Predicate<FileInfo>? shouldIncludeFile = null, Predicate<DirectoryInfo>? shouldIncludeDirectory = null,
        CancellationToken token = default)
    {
        DirectoryInfo info = new DirectoryInfo(directoryRoot);

        if (!info.Exists)
            throw new DirectoryNotFoundException();

        VirtualDirectoryBuilder builder = new VirtualDirectoryBuilder(info.FullName);

        foreach (FileSystemInfo sysInfo in info.EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
        {
            if (sysInfo is FileInfo file)
            {
                if (shouldIncludeFile != null && !shouldIncludeFile(file))
                    continue;

                builder.FilePaths.Enqueue(file.FullName);
            }
            else if (sysInfo is DirectoryInfo directory)
            {
                if (shouldIncludeDirectory != null && !shouldIncludeDirectory(directory))
                    continue;

                builder.Directories.Add(Path.GetRelativePath(builder.Root, directory.FullName));
            }
        }

        await builder.FlushQueueAsync(token).ConfigureAwait(false);
        return new VirtualDirectoryRoot(builder.Directories.ToArray(), builder.Files.ToArray());
    }

    /// <summary>
    /// Write an in-memory copy of a file-system with to storage.
    /// </summary>
    /// <exception cref="DirectoryNotFoundException"/>
    public static void Save(this VirtualDirectoryRoot root, string directory, bool restrictUnexpectedFileTypes = true)
    {
        directory = Path.GetFullPath(directory);
        Logger.DevkitServer.LogDebug(nameof(VirtualDirectories), $"Saving virtual directory to {directory.Format(false)}...");

        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        
        foreach (string lowerDirectory in root.Directories)
        {
            if (!VerifyDirectory(lowerDirectory))
                continue;

            string fullPath = Path.Combine(directory, lowerDirectory);
            Directory.CreateDirectory(fullPath);
        }
        
        foreach (VirtualFile file in root.Files)
        {
            if (!VerifyFile(file, restrictUnexpectedFileTypes))
                continue;

            string fullPath = Path.Combine(directory, file.Path);
            string? dirName = Path.GetDirectoryName(fullPath);
            if (dirName != null)
                Directory.CreateDirectory(dirName);

            using FileStream fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            fs.Write(file.Content);
        }
#if DEBUG
        Logger.DevkitServer.LogDebug(nameof(VirtualDirectories), "  Done");
#endif
    }

    /// <summary>
    /// Write an in-memory copy of a file-system with to storage with asynchronous writing.
    /// </summary>
    /// <exception cref="DirectoryNotFoundException"/>
    /// <exception cref="OperationCanceledException"/>
    public static Task SaveAsync(this VirtualDirectoryRoot root, string directory, bool restrictUnexpectedFileTypes = true, CancellationToken token = default)
    {
        directory = Path.GetFullPath(directory);
        Logger.DevkitServer.LogDebug(nameof(VirtualDirectories), $"Saving virtual directory to {directory.Format(false)}...");

        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        foreach (string lowerDirectory in root.Directories)
        {
            if (!VerifyDirectory(lowerDirectory))
                continue;

            string fullPath = Path.Combine(directory, lowerDirectory);
            Directory.CreateDirectory(fullPath);
        }

        Task[] tasks = new Task[root.Files.Count];

        for (int i = 0; i < root.Files.Count; i++)
        {
            VirtualFile file = root.Files[i];
            if (VerifyFile(file, restrictUnexpectedFileTypes))
                tasks[i] = WriteAsync(file, directory, token);
            else
                tasks[i] = Task.CompletedTask;
        }

        return Task.WhenAll(tasks);

        static async Task WriteAsync(VirtualFile file, string directory, CancellationToken token)
        {
            string fullPath = Path.Combine(directory, file.Path);
            string? dirName = Path.GetDirectoryName(fullPath);
            if (dirName != null)
                Directory.CreateDirectory(dirName);

            await using FileStream fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            await fs.WriteAsync(file.Content, token);
        }
#if DEBUG
        Logger.DevkitServer.LogDebug(nameof(VirtualDirectories), "  Done");
#endif
    }
    private static bool VerifyDirectory(string directory)
    {
        if (directory.IndexOf("..", StringComparison.Ordinal) == -1)
            return true;

        Logger.DevkitServer.LogWarning(nameof(VirtualDirectoryRoot), $"Skipping directory: {directory} because it contains a 'move up' path element (..).");
        return false;
    }
    private static bool VerifyFile(VirtualFile file, bool restrictUnexpectedFileTypes)
    {
        if (file.Path.IndexOf("..", StringComparison.Ordinal) != -1)
        {
            Logger.DevkitServer.LogWarning(nameof(VirtualDirectoryRoot), $"Skipping file: {file.Path} because it contains a 'move up' path element (..).");
            return false;
        }
        string ext = Path.GetExtension(file.Path);
        if (!restrictUnexpectedFileTypes || string.IsNullOrEmpty(ext) || !RestrictedFileTypesIntl.Contains(ext))
            return true;

        Logger.DevkitServer.LogWarning(nameof(VirtualDirectoryRoot), $"Skipping file: {file.Path} because of it's file type.");
        return false;

    }

    /// <summary>
    /// Write a <see cref="VirtualDirectoryRoot"/> to a <see cref="ByteWriter"/>.
    /// </summary>
    public static void Write(this VirtualDirectoryRoot root, ByteWriter writer)
    {
        int totalSize = 9;
        foreach (string dir in root.Directories)
            totalSize += dir.Length * sizeof(char) + sizeof(ushort);

        long totalFileSize = 0L;
        foreach (VirtualFile file in root.Files)
        {
            totalSize += file.Path.Length * sizeof(char) + sizeof(ushort) + sizeof(int) + file.Content.Count;
            totalFileSize += file.Content.Count;
        }

        writer.ExtendBufferFor(totalSize);

        const byte v = 0;
        writer.Write(v);

        writer.Write(root.Directories.Count);
        writer.Write(root.Files.Count);
        writer.Write(totalFileSize);

        foreach (string dir in root.Directories)
            writer.Write(FileUtil.FormatUniversalPath(dir));

        foreach (VirtualFile dir in root.Files)
        {
            writer.Write(FileUtil.FormatUniversalPath(dir.Path));
            writer.Write(dir.Content.Count);
            writer.WriteBlock(dir.Content);
        }
    }

    /// <summary>
    /// Read a <see cref="VirtualDirectoryRoot"/> from a <see cref="ByteReader"/>.
    /// </summary>
    public static VirtualDirectoryRoot Read(ByteReader reader)
    {
        _ = reader.ReadUInt8();

        int dirCount = reader.ReadInt32();
        int fileCount = reader.ReadInt32();
        long totalFileSize = reader.ReadInt64();

        string[] directories = new string[dirCount];
        VirtualFile[] files = new VirtualFile[fileCount];

        for (int i = 0; i < dirCount; ++i)
            directories[i] = FileUtil.UnformatUniversalPath(reader.ReadString());

        int index = 0;
        byte[] buffer = new byte[Math.Min(1073741824L, totalFileSize)];

        for (int i = 0; i < fileCount; ++i)
        {
            string path = FileUtil.UnformatUniversalPath(reader.ReadString());
            int fileSize = reader.ReadInt32();
            reader.ReadBlockTo(buffer.AsSpan(index, fileSize));
            files[i] = new VirtualFile(path, new ArraySegment<byte>(buffer, index, fileSize));
            index += fileSize;
        }

        return new VirtualDirectoryRoot(directories, files);
    }
    private static HashSet<string> RestrictedFileTypesIntl => _restrictedFileTypes ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".exe",
        ".msi",
        ".msp",
        ".pif",
        ".gadget",
        ".application",
        ".com",
        ".scr",
        ".hta",
        ".cpl",
        ".msc",
        ".jar",
        ".cmd",
        ".vb",
        ".vbs",
        ".js",
        ".jse",
        ".ws",
        ".wsf",
        ".wsc",
        ".wsh",
        ".sh",
        ".bat",
        ".dll",
        ".asm",
        ".cs",
        ".c",
        ".cpp",
        ".h",
        ".py",
        ".zip",
        ".rar",
        ".7z",
        ".nupkg",
        ".snupkg",
        ".docx",
        ".doc",
        ".xls",
        ".xlsx",
        ".ppt",
        ".pptx",
        ".pdf",
        ".iso",
        ".img",
        ".inf",
        ".lnk",
        ".scf",
        ".reg",
        ".ics",
        ".ico",
        ".htm",
        ".html"
    };
    private sealed class VirtualDirectoryBuilder(string root)
    {
        public readonly string Root = root;
        public readonly ConcurrentQueue<string> FilePaths = [];

        public readonly ConcurrentBag<string> Directories = [];
        public readonly ConcurrentBag<VirtualFile> Files = [];

        public long TotalLength;

        public void FlushQueue()
        {
            byte[] fileOutput = new byte[Interlocked.Exchange(ref TotalLength, 0)];
            int index = 0;
            while (FilePaths.TryDequeue(out string file))
            {
                using FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);

                long fileSize = fs.Length;
                if (fileSize > fileOutput.Length - index)
                {
                    byte[] newBuffer = new byte[fileSize];
                    int c = fs.Read(fileOutput, index, newBuffer.Length);
                    Files.Add(new VirtualFile(Path.GetRelativePath(Root, file), new ArraySegment<byte>(newBuffer, 0, c)));
                }
                else
                {
                    int c = fs.Read(fileOutput, index, (int)fileSize);
                    Files.Add(new VirtualFile(Path.GetRelativePath(Root, file), new ArraySegment<byte>(fileOutput, index, c)));
                    index += (int)fileSize;
                }
            }
        }
        public Task FlushQueueAsync(CancellationToken token)
        {
            List<Task> readTasks = new List<Task>(FilePaths.Count);

            while (FilePaths.TryDequeue(out string file1)
                   | FilePaths.TryDequeue(out string? file2)
                   | FilePaths.TryDequeue(out string? file3)
                   | FilePaths.TryDequeue(out string? file4)
                   | FilePaths.TryDequeue(out string? file5)
                   | FilePaths.TryDequeue(out string? file6)
                   | FilePaths.TryDequeue(out string? file7)
                   | FilePaths.TryDequeue(out string? file8)
                   | FilePaths.TryDequeue(out string? file9)
                   | FilePaths.TryDequeue(out string? file10))
            {
                readTasks.Add(ReadChunk(file1, file2, file3, file4, file5, file6, file7, file8, file9, file10, token));
            }

            return Task.WhenAll(readTasks);

            async Task ReadChunk(string file1, string? file2, string? file3, string? file4, string? file5, string? file6, string? file7, string? file8, string? file9, string? file10, CancellationToken token)
            {
                Task<byte[]> t1 = File.ReadAllBytesAsync(file1, token);
                Task<byte[]>? t2 = file2 != null ? File.ReadAllBytesAsync(file2, token) : null;
                Task<byte[]>? t3 = file3 != null ? File.ReadAllBytesAsync(file3, token) : null;
                Task<byte[]>? t4 = file4 != null ? File.ReadAllBytesAsync(file4, token) : null;
                Task<byte[]>? t5 = file5 != null ? File.ReadAllBytesAsync(file5, token) : null;
                Task<byte[]>? t6 = file6 != null ? File.ReadAllBytesAsync(file6, token) : null;
                Task<byte[]>? t7 = file7 != null ? File.ReadAllBytesAsync(file7, token) : null;
                Task<byte[]>? t8 = file8 != null ? File.ReadAllBytesAsync(file8, token) : null;
                Task<byte[]>? t9 = file9 != null ? File.ReadAllBytesAsync(file9, token) : null;
                Task<byte[]>? t10 = file10 != null ? File.ReadAllBytesAsync(file10, token) : null;

                Files.Add(new VirtualFile(Path.GetRelativePath(Root, file1), await t1.ConfigureAwait(false)));
                if (t2 != null)
                    Files.Add(new VirtualFile(Path.GetRelativePath(Root, file2), await t2.ConfigureAwait(false)));
                if (t3 != null)
                    Files.Add(new VirtualFile(Path.GetRelativePath(Root, file3), await t3.ConfigureAwait(false)));
                if (t4 != null)
                    Files.Add(new VirtualFile(Path.GetRelativePath(Root, file4), await t4.ConfigureAwait(false)));
                if (t5 != null)
                    Files.Add(new VirtualFile(Path.GetRelativePath(Root, file5), await t5.ConfigureAwait(false)));
                if (t6 != null)
                    Files.Add(new VirtualFile(Path.GetRelativePath(Root, file6), await t6.ConfigureAwait(false)));
                if (t7 != null)
                    Files.Add(new VirtualFile(Path.GetRelativePath(Root, file7), await t7.ConfigureAwait(false)));
                if (t8 != null)
                    Files.Add(new VirtualFile(Path.GetRelativePath(Root, file8), await t8.ConfigureAwait(false)));
                if (t9 != null)
                    Files.Add(new VirtualFile(Path.GetRelativePath(Root, file9), await t9.ConfigureAwait(false)));
                if (t10 != null)
                    Files.Add(new VirtualFile(Path.GetRelativePath(Root, file10), await t10.ConfigureAwait(false)));
            }
        }
    }
}
