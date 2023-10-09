using DevkitServer.Levels;
using DevkitServer.Util.Encoding;
using System.Reflection;
using DevkitServer.Plugins;

namespace DevkitServer.Multiplayer.Levels;

/// <summary>
/// Manages replicated level data which is packaged and sent along with the level on join.
/// </summary>
public static class ReplicatedLevelDataRegistry
{
    private const ushort Version = 0;
    internal const string Source = "REPLICATED LVL DATA";
    private static readonly Dictionary<Type, ReplicatedLevelDataSourceInfo> RegisteredTypes = new Dictionary<Type, ReplicatedLevelDataSourceInfo>(32);
    internal static void Shutdown()
    {
        lock (RegisteredTypes)
        {
            foreach (ReplicatedLevelDataSourceInfo srcInfo in RegisteredTypes.Values)
            {
                if (srcInfo.Instance is IDisposable disp)
                    disp.Dispose();
            }

            RegisteredTypes.Clear();
        }
    }

    internal static void RegisterFromAssembly(Assembly assembly, IList<ReplicatedLevelDataSourceInfo>? info, PluginAssembly? plugin)
    {
        lock (RegisteredTypes)
        {
            int ct = 0;
            foreach (Type type in Accessor.GetTypesSafe(assembly))
            {
                if (type.IsInterface || type.IsValueType || !typeof(IReplicatedLevelDataSource).IsAssignableFrom(type))
                    continue;

                Type? interfaceType = type.GetInterfaces().FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IReplicatedLevelDataSource<>));
                if (interfaceType == null)
                    continue;

                Type dataType = interfaceType.GetGenericArguments()[0];

                ConstructorInfo? ctor = type.GetConstructor(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null, Array.Empty<Type>(), null);
                if (ctor == null)
                {
                    if (plugin == null)
                        Logger.LogWarning($"Missing default constructor for type {type.Format()} (with data {dataType.Format()}).", method: Source);
                    else
                        plugin.LogWarning($"Missing default constructor for type {type.Format()} (with data {dataType.Format()}).", method: Source);
                    continue;
                }

                if (RegisteredTypes.ContainsKey(dataType))
                {
                    if (plugin == null)
                        Logger.LogWarning($"Duplicate data type in data source {type.Format()} (with data {dataType.Format()}) skipped.", method: Source);
                    else
                        plugin.LogWarning($"Duplicate data type in data source {type.Format()} (with data {dataType.Format()}) skipped.", method: Source);
                    continue;
                }

                IReplicatedLevelDataSource? instance = null;
                try
                {
                    instance = (IReplicatedLevelDataSource)ctor.Invoke(Array.Empty<object>());

                    ReplicatedLevelDataSourceInfo replicatedLevelDataSourceInfo = new ReplicatedLevelDataSourceInfo(type, dataType, instance, plugin);
                    RegisteredTypes.Add(dataType, replicatedLevelDataSourceInfo);
                    info?.Add(replicatedLevelDataSourceInfo);
                    ++ct;

                    if (plugin == null)
                        Logger.LogDebug($"[{Source}] Regestered {type.Format()} (with data {dataType.Format()}) as replicated level data.");
                    else
                        plugin.LogDebug($"[{Source}] Regestered {type.Format()} (with data {dataType.Format()}) as replicated level data.");
                }
                catch (Exception ex)
                {

                    if (plugin == null)
                    {
                        Logger.LogWarning($"Error creating instance of {type.Format()} (with data {dataType.Format()}). Type skipped.", method: Source);
                        Logger.LogError(ex, method: Source);
                    }
                    else
                    {
                        plugin.LogWarning($"Error creating instance of {type.Format()} (with data {dataType.Format()}). Type skipped.", method: Source);
                        plugin.LogError(ex, method: Source);
                    }

                    if (instance is IDisposable disp)
                        disp.Dispose();
                }
            }

            if (plugin == null)
                Logger.LogInfo($"[{Source}] Regestered {ct.Format()} replicated level data sources.");
            else
                plugin.LogInfo($"[{Source}] Regestered {ct.Format()} replicated level data sources.");
        }
    }
#if CLIENT
    internal static void LoadFromLevelData()
    {
        lock (RegisteredTypes)
        {
            LevelData data = EditorLevel.ServerPendingLevelData ?? throw new InvalidOperationException("Level data not loaded.");

            if (data.ReplicatedLevelData is not { Count: > 0 })
                return;

            foreach (object obj in data.ReplicatedLevelData)
            {
                Type type = obj.GetType();

                if (!RegisteredTypes.TryGetValue(type, out ReplicatedLevelDataSourceInfo srcInfo))
                    continue;

                try
                {
                    srcInfo.Load(srcInfo.Instance, obj);
                }
                catch (Exception ex)
                {
                    if (srcInfo.Assembly != null)
                    {
                        srcInfo.Assembly.LogError($"Error running LoadData on {srcInfo.Type.Format()} (with data {srcInfo.DataType.Format()}).", method: Source);
                        srcInfo.Assembly.LogError(ex, method: Source);
                    }
                    else
                    {
                        Logger.LogError($"Error running LoadData on {srcInfo.Type.Format()} (with data {srcInfo.DataType.Format()}).", method: Source);
                        Logger.LogError(ex, method: Source);
                    }
                }
            }
        }
    }
#elif SERVER
    internal static void SaveToLevelData(LevelData data)
    {
        lock (RegisteredTypes)
        {
            data.ReplicatedLevelData ??= new List<object>(RegisteredTypes.Count);
            foreach (ReplicatedLevelDataSourceInfo srcInfo in RegisteredTypes.Values)
            {
                try
                {
                    object saveData = srcInfo.Save(srcInfo.Instance);
                    data.ReplicatedLevelData.Add(saveData);
                }
                catch (Exception ex)
                {
                    if (srcInfo.Assembly != null)
                    {
                        srcInfo.Assembly.LogError($"Error running SaveData on {srcInfo.Type.Format()} (with data {srcInfo.DataType.Format()}).", method: Source);
                        srcInfo.Assembly.LogError(ex, method: Source);
                    }
                    else
                    {
                        Logger.LogError($"Error running SaveData on {srcInfo.Type.Format()} (with data {srcInfo.DataType.Format()}).", method: Source);
                        Logger.LogError(ex, method: Source);
                    }
                }
            }
        }
    }
#endif
    internal static void Read(LevelData data, ByteReader reader)
    {
        lock (RegisteredTypes)
        {
            reader.ReadUInt16(); // version

            int ct = reader.ReadInt32();
            if (data.ReplicatedLevelData == null)
                data.ReplicatedLevelData = new List<object>(ct);
            else
            {
                data.ReplicatedLevelData.Clear();
                data.ReplicatedLevelData.IncreaseCapacity(ct);
            }

            ByteReader? tempReader = reader.Stream != null ? new ByteReader { ThrowOnError = reader.ThrowOnError, LogOnError = reader.LogOnError } : null;
            for (int i = 0; i < ct; ++i)
            {
                int startPos = reader.Position;
                Type? type = reader.ReadType(out bool wasPassedNull);
                if (wasPassedNull)
                {
                    Logger.LogDebug($"[{Source}] Skipped replicated level data at index {i.Format()}.");
                    continue;
                }

                int byteCt = reader.ReadInt32();
                if (type == null)
                {
                    if (tempReader != null)
                    {
                        Logger.LogDebug($"[{Source}] Unrecognized replicated level data type at index {i.Format()}.");
                        continue;
                    }

                    int oldPos = reader.Position;
                    reader.Goto(startPos);
                    string? typeInfo = reader.ReadTypeInfo();
                    reader.Goto(oldPos);

                    Logger.LogWarning($"Unrecognized replicated level data type: {typeInfo.Format(true)} at index {i.Format()}.", method: Source);
                    continue;
                }

                ushort v = reader.ReadUInt16();

                object? replicatedData;
                if (tempReader == null)
                {
                    int expectedPosition = reader.Position + byteCt;
                    replicatedData = ReadValue(type, reader, v);
                    if (reader.Position != expectedPosition)
                    {
                        IReplicatedLevelDataSource? instance = RegisteredTypes.TryGetValue(type, out ReplicatedLevelDataSourceInfo info) ? info.Instance : null;
                        string versionString = $"(written data version {v.Format("X4")}, current: {instance?.CurrentDataVersion.Format("X4") ?? "unknown".Colorize(ConsoleColor.Red)})";
                        if (reader.Position < expectedPosition)
                        {
                            int bytesSkipped = reader.Position - expectedPosition;
                            reader.Skip(bytesSkipped);
                            Logger.LogWarning($"Replicated level data of type {type.Format()} read {bytesSkipped.Format()} B less than were written {versionString}.", method: Source);
                        }
                        else if (reader.Position > expectedPosition)
                        {
                            int bytesSkipped = expectedPosition - reader.Position;
                            reader.Goto(expectedPosition);
                            Logger.LogWarning($"Replicated level data of type {type.Format()} read {bytesSkipped.Format()} B more than were written {versionString}.", method: Source);
                        }
                    }
                }
                else
                {
                    tempReader.LoadNew(reader.ReadBlock(byteCt));
                    replicatedData = ReadValue(type, tempReader, v);
                    if (tempReader.Position != byteCt)
                    {
                        IReplicatedLevelDataSource? instance = RegisteredTypes.TryGetValue(type, out ReplicatedLevelDataSourceInfo info) ? info.Instance : null;
                        string versionString = $"(written data version {v.Format("X4")}, current: {instance?.CurrentDataVersion.Format("X4") ?? "unknown".Colorize(ConsoleColor.Red)})";
                        if (tempReader.Position < byteCt)
                        {
                            int bytesSkipped = tempReader.Position - byteCt;
                            Logger.LogWarning($"Replicated level data of type {type.Format()} read {bytesSkipped.Format()} B less from the stream than were written {versionString}.", method: Source);
                        }
                        else if (tempReader.Position > byteCt)
                        {
                            int bytesSkipped = byteCt - tempReader.Position;
                            Logger.LogWarning($"Replicated level data of type {type.Format()} read {bytesSkipped.Format()} B more from the stream than were written {versionString}.", method: Source);
                        }
                    }
                }

                if (replicatedData == null)
                {
                    Logger.LogWarning($"Failed to read replicated level data: {type.Format()}. This may not be an issue, but be on the lookout for things not being synced properly.", method: Source);
                    continue;
                }

                data.ReplicatedLevelData.Add(replicatedData);
                Logger.LogDebug($"[{Source}] Read {byteCt.Format()} B of data for data {type.Format()}.");
            }
        }
    }
    internal static void Write(LevelData data, ByteWriter writer)
    {
        lock (RegisteredTypes)
        {
            writer.Write(Version);
            if (data.ReplicatedLevelData == null)
            {
                writer.Write(0);
                return;
            }

            writer.Write(data.ReplicatedLevelData.Count);

            // doing it this way allows us to go back and write the length of the block for extra safety
            ByteWriter? tempWriter = writer.Stream != null ? new ByteWriter(false, 512) : null;

            for (int i = 0; i < data.ReplicatedLevelData.Count; ++i)
            {
                object replicatedData = data.ReplicatedLevelData[i];
                Type type = replicatedData.GetType();

                if (!RegisteredTypes.TryGetValue(type, out ReplicatedLevelDataSourceInfo srcInfo))
                {
                    writer.Write((Type?)null);
                    continue;
                }

                if (tempWriter != null)
                {
                    WriteValue(replicatedData, tempWriter, in srcInfo);
                    writer.WriteBlock(tempWriter.Buffer);
                    tempWriter.Flush();
                }
                else
                    WriteValue(replicatedData, writer, in srcInfo);
            }
        }
    }
    private static object? ReadValue(Type type, ByteReader reader, ushort version)
    {
        if (!RegisteredTypes.TryGetValue(type, out ReplicatedLevelDataSourceInfo srcInfo))
        {
            Logger.LogWarning($"Unregistered replicated level data type: {type.Format()}.", method: Source);
            return null;
        }

        try
        {
            return srcInfo.Read(srcInfo.Instance, reader, version);
        }
        catch (Exception ex)
        {
            if (srcInfo.Assembly != null)
            {
                srcInfo.Assembly.LogError($"Error running ReadData on {srcInfo.Type.Format()} (with data {srcInfo.DataType.Format()}).", method: Source);
                srcInfo.Assembly.LogError(ex, method: Source);
            }
            else
            {
                Logger.LogError($"Error running ReadData on {srcInfo.Type.Format()} (with data {srcInfo.DataType.Format()}).", method: Source);
                Logger.LogError(ex, method: Source);
            }

            return null;
        }
    }
    private static unsafe void WriteValue(object replicatedData, ByteWriter writer, in ReplicatedLevelDataSourceInfo srcInfo)
    {
        writer.Write(replicatedData.GetType());
        int pos = writer.Count;
        writer.Write(0);
        IReplicatedLevelDataSource instance = srcInfo.Instance;
        writer.Write(instance.CurrentDataVersion);
        try
        {
            srcInfo.Write(instance, writer, replicatedData);
        }
        catch (Exception ex)
        {
            if (srcInfo.Assembly != null)
            {
                srcInfo.Assembly.LogError($"Error running WriteData on {srcInfo.Type.Format()} (with data {srcInfo.DataType.Format()}).", method: Source);
                srcInfo.Assembly.LogError(ex, method: Source);
            }
            else
            {
                Logger.LogError($"Error running WriteData on {srcInfo.Type.Format()} (with data {srcInfo.DataType.Format()}).", method: Source);
                Logger.LogError(ex, method: Source);
            }
        }

        // update the byte count in the beginning
        fixed (byte* ptr = &writer.Buffer[pos])
        {
            *(int*)ptr = writer.Count - (pos + sizeof(int) + sizeof(ushort));
            ByteWriter.EndianCheck(ptr, sizeof(int));
        }
    }

    /// <summary>
    /// Find replicating level data in a <see cref="LevelData"/> object.
    /// </summary>
    public static TData? GetData<TData>(LevelData data) where TData : class, new()
    {
        lock (RegisteredTypes)
        {
            if (data.ReplicatedLevelData is not { Count: > 0 })
                return null;

            return data.ReplicatedLevelData.OfType<TData>().FirstOrDefault();
        }
    }

    /// <summary>
    /// Find replicating level data in a <see cref="LevelData"/> object.
    /// </summary>
    public static object? GetData(Type dataType, LevelData data)
    {
        lock (RegisteredTypes)
        {
            if (data.ReplicatedLevelData is not { Count: > 0 })
                return null;

            return data.ReplicatedLevelData.FirstOrDefault(dataType.IsInstanceOfType);
        }
    }

    /// <summary>
    /// Find registered information for a data source by it's data's type.
    /// </summary>
    public static bool TryGetInfoFromDataType<TData>(out ReplicatedLevelDataSourceInfo info) => TryGetInfoFromDataType(typeof(TData), out info);

    /// <summary>
    /// Find registered information for a data source by it's type.
    /// </summary>
    public static bool TryGetInfoFromSourceType<TDataSource>(out ReplicatedLevelDataSourceInfo info) => TryGetInfoFromSourceType(typeof(TDataSource), out info);

    /// <summary>
    /// Find registered information for a data source by it's data's type.
    /// </summary>
    public static bool TryGetInfoFromDataType(Type dataType, out ReplicatedLevelDataSourceInfo info)
    {
        lock (RegisteredTypes)
        {
            return RegisteredTypes.TryGetValue(dataType, out info);
        }
    }

    /// <summary>
    /// Find registered information for a data source by it's type.
    /// </summary>
    public static bool TryGetInfoFromSourceType(Type sourceType, out ReplicatedLevelDataSourceInfo info)
    {
        lock (RegisteredTypes)
        {
            foreach (ReplicatedLevelDataSourceInfo info2 in RegisteredTypes.Values)
            {
                if (info2.Type == sourceType)
                {
                    info = info2;
                    return true;
                }
            }
            foreach (ReplicatedLevelDataSourceInfo info2 in RegisteredTypes.Values)
            {
                if (sourceType.IsAssignableFrom(info2.Type))
                {
                    info = info2;
                    return true;
                }
            }
        }

        info = default;
        return false;
    }
}

/// <summary>
/// Stores information about <see cref="IReplicatedLevelDataSource{TData}"/>s.
/// </summary>
public readonly struct ReplicatedLevelDataSourceInfo
{
    /// <summary>
    /// The type of <see cref="IReplicatedLevelDataSource{TData}"/>.
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// The data type.
    /// </summary>
    public Type DataType { get; }

    /// <summary>
    /// The singleton instance of <see cref="Type"/>.
    /// </summary>
    public IReplicatedLevelDataSource Instance { get; }

    /// <summary>
    /// The plugin this level data comes from, or <see langword="null"/> if it's from DevkitServer.
    /// </summary>
    public PluginAssembly? Assembly { get; }

#if CLIENT
    internal readonly Action<IReplicatedLevelDataSource, object> Load;
#elif SERVER
    internal readonly Func<IReplicatedLevelDataSource, object> Save;
#endif
    internal readonly Action<IReplicatedLevelDataSource, ByteWriter, object> Write;
    internal readonly Func<IReplicatedLevelDataSource, ByteReader, ushort, object> Read;

    internal ReplicatedLevelDataSourceInfo(Type type, Type dataType, IReplicatedLevelDataSource instance, PluginAssembly? assembly)
    {
        Type = type;
        DataType = dataType;
        Instance = instance;
        Assembly = assembly;

        Type interfaceType = typeof(IReplicatedLevelDataSource<>).MakeGenericType(dataType);

#if CLIENT
        MethodInfo? ioMethod = interfaceType.GetMethod(nameof(IReplicatedLevelDataSource<object>.LoadData), BindingFlags.Public | BindingFlags.Instance);
        ioMethod = ioMethod == null ? null : Accessor.GetImplementedMethod(type, ioMethod);
        if (ioMethod == null)
        {
            Logger.LogError($"Load method not implemented in {type.Format()} (with data {dataType.Format()}).", method: ReplicatedLevelDataRegistry.Source);
        }
#elif SERVER
        MethodInfo? ioMethod = interfaceType.GetMethod(nameof(IReplicatedLevelDataSource<object>.SaveData), BindingFlags.Public | BindingFlags.Instance);
        ioMethod = ioMethod == null ? null : Accessor.GetImplementedMethod(type, ioMethod);
        if (ioMethod == null)
        {
            Logger.LogError($"Save method not implemented in {type.Format()} (with data {dataType.Format()}).", method: ReplicatedLevelDataRegistry.Source);
        }
#endif

        MethodInfo? writeMethod = interfaceType.GetMethod(nameof(IReplicatedLevelDataSource<object>.WriteData), BindingFlags.Public | BindingFlags.Instance);
        writeMethod = writeMethod == null ? null : Accessor.GetImplementedMethod(type, writeMethod);
        if (writeMethod == null)
        {
            Logger.LogError($"WriteData method not implemented in {type.Format()} (with data {dataType.Format()}).", method: ReplicatedLevelDataRegistry.Source);
        }

        MethodInfo? readMethod = interfaceType.GetMethod(nameof(IReplicatedLevelDataSource<object>.ReadData), BindingFlags.Public | BindingFlags.Instance);
        readMethod = readMethod == null ? null : Accessor.GetImplementedMethod(type, readMethod);
        if (readMethod == null)
        {
            Logger.LogError($"WriteData method not implemented in {type.Format()} (with data {dataType.Format()}).", method: ReplicatedLevelDataRegistry.Source);
        }

        if (readMethod == null || writeMethod == null || ioMethod == null)
        {
            throw new MissingMethodException("Expected replicated level data source methods not implemented.");
        }

        Read = (Func<IReplicatedLevelDataSource, ByteReader, ushort, object>)Accessor.GenerateInstanceCaller(typeof(Func<IReplicatedLevelDataSource, ByteReader, ushort, object>), readMethod, true, true)!;
        Write = (Action<IReplicatedLevelDataSource, ByteWriter, object>)Accessor.GenerateInstanceCaller(typeof(Action<IReplicatedLevelDataSource, ByteWriter, object>), writeMethod, true, true)!;
#if CLIENT
        Load = (Action<IReplicatedLevelDataSource, object>)Accessor.GenerateInstanceCaller(typeof(Action<IReplicatedLevelDataSource, object>), ioMethod, true, true)!;
#elif SERVER
        Save = (Func<IReplicatedLevelDataSource, object>)Accessor.GenerateInstanceCaller(typeof(Func<IReplicatedLevelDataSource, object>), ioMethod, true, true)!;
#endif
    }

#if CLIENT
    /// <summary>
    /// Apply <paramref name="data"/> to the active state of the game.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="data"/> is not of type <see cref="DataType"/>.</exception>
    /// <remarks>Clientside</remarks>
    public void LoadData(object data)
    {
        if (!DataType.IsInstanceOfType(data))
            throw new ArgumentException($"Must be of type {DataType}.", nameof(data));
        Load(Instance, data);
    }
#elif SERVER

    /// <summary>
    /// Copy data from the active state of the game.
    /// </summary>
    /// <remarks>Serverside</remarks>
    public object SaveData() => Save(Instance);
#endif

    /// <summary>
    /// Write the data to a <see cref="ByteWriter"/>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="data"/> is not of type <see cref="DataType"/>.</exception>
    public void WriteData(object data, ByteWriter writer)
    {
        if (!DataType.IsInstanceOfType(data))
            throw new ArgumentException($"Must be of type {DataType}.", nameof(data));

        Write(Instance, writer, data);
    }

    /// <summary>
    /// Read the data from a <see cref="ByteReader"/>.
    /// </summary>
    public object ReadData(ByteReader reader, ushort dataVersion) => Read(Instance, reader, dataVersion);
}

/// <summary>
/// See <see cref="IReplicatedLevelDataSource{TData}"/>.
/// </summary>
public interface IReplicatedLevelDataSource
{
    /// <summary>
    /// Increment this anytime a change is made to the binary pattern of your data source, and keep your read method up to date for backwards compatability.
    /// </summary>
    ushort CurrentDataVersion { get; }
}

/// <summary>
/// Defines a singleton provider class for <typeparamref name="TData"/> to be written to and read from level data on join.
/// </summary>
/// <remarks>Implementing classes are automatically discovered during load.</remarks>
// ReSharper disable once TypeParameterCanBeVariant
public interface IReplicatedLevelDataSource<TData> : IReplicatedLevelDataSource where TData : class, new() 
{
#if CLIENT

    /// <summary>
    /// Apply <paramref name="data"/> to the active state of the game.
    /// </summary>
    /// <remarks>Clientside</remarks>
    void LoadData(TData data);

#elif SERVER

    /// <summary>
    /// Copy data from the active state of the game. There should be no references to anything that could change throughout the server's lifetime.
    /// </summary>
    /// <remarks>Serverside</remarks>
    TData SaveData();

#endif

    /// <summary>
    /// Write the data to a <see cref="ByteWriter"/>.
    /// </summary>
    /// <remarks>Use <see cref="IReplicatedLevelDataSource.CurrentDataVersion"/> for data versioning.</remarks>
    void WriteData(ByteWriter writer, TData data);

    /// <summary>
    /// Read the data from a <see cref="ByteReader"/>.
    /// </summary>
    /// <remarks>Use <see cref="IReplicatedLevelDataSource.CurrentDataVersion"/> for data versioning.</remarks>
    TData ReadData(ByteReader reader, ushort dataVersion);
}