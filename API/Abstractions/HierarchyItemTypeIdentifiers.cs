using DevkitServer.Plugins;
using DevkitServer.Util.Encoding;
using SDG.Framework.Devkit;
using SDG.Framework.Foliage;
using SDG.Framework.Landscapes;
using SDG.Framework.Water;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DevkitServer.API.Abstractions;
public static class HierarchyItemTypeIdentifierEx
{
    private const string Source = "HIERARCHY ITEM IDENTIFIERS";
    private const ushort DataVersion = 0;
    private const int MaxType = 5;
    private static readonly List<HierarchyItemTypeIdentifierFactoryInfo> Factories = new List<HierarchyItemTypeIdentifierFactoryInfo>();
    private static bool _anyInFactory;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool TryAddFactory(IHierarchyItemTypeIdentifierFactory factory)
    {
        Type type = factory.GetType();
        IDevkitServerPlugin? pluginSource = PluginLoader.FindPluginForMember(type);
        if (pluginSource == null)
        {
            Assembly caller = Assembly.GetCallingAssembly();
            pluginSource = PluginLoader.FindPluginForAssembly(caller);
            if (pluginSource == null)
            {
                Logger.DevkitServer.LogError(Source, "Unable to link " + type.Format() + " to a plugin. Use the " + typeof(PluginIdentifierAttribute).Format() +
                                                     " to link a hierarchy item identifier factory to a plugin when multiple plugins are loaded from an assembly.");
                return false;
            }
        }

        HierarchyItemTypeIdentifierFactoryInfo info = new HierarchyItemTypeIdentifierFactoryInfo(factory, type, pluginSource);
        AddFactory(info);
        return true;
    }
    internal static void AddFactory(HierarchyItemTypeIdentifierFactoryInfo factoryInfo)
    {
        lock (Factories)
        {
            Type type = factoryInfo.Type;
            HierarchyItemTypeIdentifierFactoryInfo? removed = null;
            for (int i = Factories.Count - 1; i >= 0; --i)
            {
                HierarchyItemTypeIdentifierFactoryInfo factory2 = Factories[i];
                if (!type.IsAssignableFrom(factory2.Type))
                    continue;

                if (factory2.Factory is IDisposable disp)
                    disp.Dispose();
                Factories[i] = factoryInfo;
                removed = factory2;
                break;
            }
            if (removed == null)
            {
                Factories.Add(factoryInfo);
                _anyInFactory = true;
            }

            IDevkitServerPlugin? pluginSource = PluginLoader.FindPluginForMember(type);

            if (pluginSource != null)
                pluginSource.LogInfo(Source, $"Registered hierarchy item identifier factory: {type.Format()}.");
            else if (type.Assembly == Accessor.DevkitServer)
                Logger.DevkitServer.LogInfo(Source, $"Registered hierarchy item identifier factory: {type.Format()}.");
            else
                Logger.DevkitServer.LogInfo(Source, $"Registered hierarchy item identifier factory: {type.Format()} from {type.Assembly}.");

            if (removed.HasValue)
            {
                Type removedType = removed.Value.Type;
                if (removed.Value.Plugin != null)
                    removed.Value.Plugin.LogDebug(Source, $" + Deregistered duplicate hierarchy item identifier factory: {removedType.Format()}.");
                else if (removedType.Assembly == Accessor.DevkitServer)
                    Logger.DevkitServer.LogDebug(Source, $" + Deregistered duplicate hierarchy item identifier factory: {removedType.Format()}.");
                else
                    Logger.DevkitServer.LogDebug(Source, $" + Deregistered duplicate hierarchy item identifier factory: {removedType.Format()} from {removedType.Assembly.Format()}.");
            }
        }
    }
    public static int TryRemoveFactory(Type type)
    {
        int c = 0;
        lock (Factories)
        {
            for (int i = Factories.Count - 1; i >= 0; --i)
            {
                HierarchyItemTypeIdentifierFactoryInfo info = Factories[i];
                if (!type.IsAssignableFrom(info.Type))
                    continue;
                if (info.Factory is IDisposable disp)
                    disp.Dispose();
                Factories.RemoveAt(i);
                _anyInFactory = Factories.Count > 0;
                ++c;

                Type removedType = info.Type;
                if (info.Plugin != null)
                    info.Plugin.LogInfo(Source, $"Deregistered hierarchy item identifier factory: {removedType.Format()}.");
                else if (removedType.Assembly == Accessor.DevkitServer)
                    Logger.DevkitServer.LogDebug(Source, $"Deregistered hierarchy item identifier factory: {removedType.Format()}.");
                else
                    Logger.DevkitServer.LogDebug(Source, $"Deregistered hierarchy item identifier factory: {removedType.Format()} from {removedType.Assembly.Format()}.");
            }
        }

        return c;
    }

    public static IHierarchyItemTypeIdentifier? GetIdentifier(IDevkitHierarchyItem item)
    {
        if (_anyInFactory)
        {
            IHierarchyItemTypeIdentifier? res = GetIdentifierFromFactories(item);
            if (res != null)
                return res;
        }

        if (item is TempNodeBase)
            return new NodeItemTypeIdentifier(item.GetType());

        if (item is VolumeBase)
            return new VolumeItemTypeIdentifier(item.GetType());

        if (item is Landscape)
            return LandscapeItemTypeIdentifier.Instance;

        if (item is FoliageSystem)
            return FoliageSystemItemTypeIdentifier.Instance;

#pragma warning disable CS0612
        if (item is DevkitHierarchyWorldObject obj)
        {
            return new LegacyDevkitHierarchyWorldObjectIdentifier(obj.GUID, obj.placementOrigin, obj.customMaterialOverride, obj.materialIndexOverride);
        }
#pragma warning restore CS0612

        return null;
    }
    private static IHierarchyItemTypeIdentifier? GetIdentifierFromFactories(IDevkitHierarchyItem item)
    {
        lock (Factories)
        {
            for (int i = Factories.Count - 1; i >= 0; --i)
            {
                HierarchyItemTypeIdentifierFactoryInfo info = Factories[i];
                try
                {
                    if ((info.Plugin == null || PluginLoader.IsLoaded(info.Plugin)) && info.Factory.GetIdentifier(item) is { } result)
                        return result;
                }
                catch (Exception ex)
                {
                    Type type = info.GetType();
                    IDevkitServerPlugin? pluginSource = PluginLoader.FindPluginForMember(type);
                    if (pluginSource != null)
                    {
                        pluginSource.LogError($"Error in hierarchy item identifier factory: {type.Format()}.");
                        pluginSource.LogError(ex);
                    }
                    else
                    {
                        if (type.Assembly == Accessor.DevkitServer)
                            Logger.DevkitServer.LogError(Source, ex, $"Error in hierarchy item identifier factory: {type.Format()}.");
                        else
                            Logger.DevkitServer.LogError(Source, ex, $"Error in hierarchy item identifier factory: {type.Format()} from {type.Assembly.Format()}.");
                    }
                }
            }
        }

        return null;
    }
    public static void Instantiate(this IHierarchyItemTypeIdentifier identifier, Vector3 position) =>
        identifier.Instantiate(position, Quaternion.identity, Vector3.one);
    public static void WriteIdentifier(ByteWriter writer, IHierarchyItemTypeIdentifier? identifier)
    {
        writer.Write(DataVersion);

        if (identifier == null)
        {
            writer.Write((byte)255);
            return;
        }
        byte type = identifier.TypeIndex;
        if (type == 255)
            throw new InvalidOperationException("Tried to write an identifier with type code 255.");
        writer.Write(type);
        if (type is 0 or > MaxType)
            writer.Write(identifier.GetType());

        identifier.Write(writer);
    }
    public static IHierarchyItemTypeIdentifier? ReadIdentifier(ByteReader reader)
    {
        ushort version = reader.ReadUInt16();

        byte type = reader.ReadUInt8();
        Type? type2 = type is (0 or > MaxType) and not 255 ? reader.ReadType() : null;

        IHierarchyItemTypeIdentifier? val = type switch
        {
            255 => null,
            1 => NodeItemTypeIdentifier.ReadFromPool(reader),
            2 => VolumeItemTypeIdentifier.ReadFromPool(reader),
#pragma warning disable CS0612
            3 => LegacyDevkitHierarchyWorldObjectIdentifier.ReadSingle(reader, version),
#pragma warning restore CS0612
            4 => LandscapeItemTypeIdentifier.Instance,
            5 => FoliageSystemItemTypeIdentifier.Instance,
            _ => type2 != null && !type2.IsAbstract && typeof(IHierarchyItemTypeIdentifier).IsAssignableFrom(type2)
                ? Activator.CreateInstance(type2, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.CreateInstance | BindingFlags.Instance,
                    null, Array.Empty<object>(), CultureInfo.InvariantCulture) as IHierarchyItemTypeIdentifier : null
        };
        if (val == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"Failed to read identifier type: {type2?.Format() ?? type.Format()}.");
            return null;
        }

        if (type is > MaxType and not 255 or 0)
            val.Read(reader, version);

        return val;
    }
    public static void RegisterFromAssembly(Assembly assembly, List<HierarchyItemTypeIdentifierFactoryInfo>? infoOut)
    {
        List<Type> types = Accessor.GetTypesSafe(assembly, true);
        foreach (Type type in types)
        {
            if (!typeof(IHierarchyItemTypeIdentifierFactory).IsAssignableFrom(type))
                continue;

            ConstructorInfo? ctor = type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (ctor == null)
            {
                Logger.DevkitServer.LogError(Source, $"Unable to instantiate hierarchy item identifier factory {type.Format()} because it does not have a default constructor.");
                continue;
            }

            IDevkitServerPlugin? pluginSource = PluginLoader.FindPluginForMember(type);
            if (pluginSource == null)
            {
                Logger.DevkitServer.LogError(Source, "Unable to link " + type.Format() + " to a plugin. Use the " + typeof(PluginIdentifierAttribute).Format() +
                                                     " to link a hierarchy item identifier factory to a plugin when multiple plugins are loaded from an assembly.");
            }

            IHierarchyItemTypeIdentifierFactory factory = (IHierarchyItemTypeIdentifierFactory)Activator.CreateInstance(type, true);
            HierarchyItemTypeIdentifierFactoryInfo info = new HierarchyItemTypeIdentifierFactoryInfo(factory, type, pluginSource);
            AddFactory(info);
            infoOut?.Add(info);
        }
    }
}
/// <summary>
/// Allows a plugin to create custom implementations of <see cref="IHierarchyItemTypeIdentifier"/>.<br/>
/// Register with <see cref="HierarchyItemTypeIdentifierEx.TryAddFactory"/>, deregister with <see cref="HierarchyItemTypeIdentifierEx.TryRemoveFactory"/>.
/// </summary>
/// <remarks>Implementing types will be auto-registered unless they have the <see cref="IgnoreAttribute"/>.</remarks>
public interface IHierarchyItemTypeIdentifierFactory
{
    /// <returns><see langword="null"/> to move on to the next registered factory, otherwise returns the correct identifier.</returns>
    IHierarchyItemTypeIdentifier? GetIdentifier(IDevkitHierarchyItem item);
}

public readonly struct HierarchyItemTypeIdentifierFactoryInfo
{
    public IHierarchyItemTypeIdentifierFactory Factory { get; }
    public Type Type { get; }
    public IDevkitServerPlugin? Plugin { get; }
    internal HierarchyItemTypeIdentifierFactoryInfo(IHierarchyItemTypeIdentifierFactory factory, Type type, IDevkitServerPlugin? plugin)
    {
        Factory = factory;
        Type = type;
        Plugin = plugin;
    }
    internal HierarchyItemTypeIdentifierFactoryInfo(IHierarchyItemTypeIdentifierFactory factory, IDevkitServerPlugin plugin)
        : this(factory, factory.GetType(), plugin ?? throw new ArgumentNullException(nameof(plugin))) { }
}

/// <summary>
/// Represents a generic unique identifier for a hierarchy item.
/// </summary>
public interface IHierarchyItemTypeIdentifier : ITerminalFormattable
{
    /// <summary>
    /// Set to zero for custom implementations.
    /// </summary>
    byte TypeIndex { get; }

    /// <summary>
    /// Type of the Hierarchy Item.
    /// </summary>
    Type Type { get; }

    /// <summary>
    /// Function to instantiate the Hierarchy Object. Should take care of adding it to <see cref="LevelHierarchy"/>.
    /// </summary>
    /// <remarks>Most Unturned hierarchy items are added to the <see cref="LevelHierarchy"/> in OnEnable, check for this before adding it yourself.</remarks>
    void Instantiate(Vector3 position, Quaternion rotation, Vector3 scale);

    void Write(ByteWriter writer);
    void Read(ByteReader reader, ushort version);
}

[Obsolete]
public sealed class LegacyDevkitHierarchyWorldObjectIdentifier : IHierarchyItemTypeIdentifier
{
    private static readonly Type LegacyDevkitHierarchyWorldObjectType = typeof(DevkitHierarchyWorldObject);
    public byte TypeIndex => 3;
    public Type Type => LegacyDevkitHierarchyWorldObjectType;
    public Guid Guid { get; set; }
    public ELevelObjectPlacementOrigin PlacementOrigin { get; set; }
    public AssetReference<MaterialPaletteAsset> MaterialPaletteAsset { get; set; }
    public int MaterialIndexOverride { get; set; }
    public LegacyDevkitHierarchyWorldObjectIdentifier()
    {
        MaterialIndexOverride = -1;
    }
    public LegacyDevkitHierarchyWorldObjectIdentifier(Guid guid, ELevelObjectPlacementOrigin placementOrigin, AssetReference<MaterialPaletteAsset> palette, int paletteIndex)
    {
        Guid = guid;
        PlacementOrigin = placementOrigin;
        MaterialPaletteAsset = palette;
        MaterialIndexOverride = paletteIndex;
    }
    public void Instantiate(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        throw new NotSupportedException("Adding new legacy devkit hierarchy objects is not supported by DevkitServer or Unturned.");
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(Guid);
        writer.Write((byte)PlacementOrigin);
        writer.Write(MaterialIndexOverride);
        writer.Write(MaterialPaletteAsset.GUID);
    }
    public void Read(ByteReader reader, ushort version)
    {
        Guid = reader.ReadGuid();
        PlacementOrigin = (ELevelObjectPlacementOrigin)reader.ReadUInt8();
        MaterialIndexOverride = reader.ReadInt32();
        MaterialPaletteAsset = new AssetReference<MaterialPaletteAsset>(reader.ReadGuid());
    }

    internal static LegacyDevkitHierarchyWorldObjectIdentifier ReadSingle(ByteReader reader, ushort version)
    {
        LegacyDevkitHierarchyWorldObjectIdentifier id = new LegacyDevkitHierarchyWorldObjectIdentifier();
        id.Read(reader, version);
        return id;
    }

    public string Format(ITerminalFormatProvider provider)
    {
        ObjectAsset? asset = Assets.find<ObjectAsset>(Guid);
        return "[HIID] Legacy Object: " + (asset != null ? asset.Format() : Guid.Format());
    }
    public override string ToString()
    {
        ObjectAsset? asset = Assets.find<ObjectAsset>(Guid);
        return "[HIID] Legacy Object: " + (asset != null ? ("\"" + asset.objectName + "/" + asset.GUID.ToString("N") + "/" + asset.id) : Guid.ToString("N"));
    }
}

[EarlyTypeInit]
public sealed class NodeItemTypeIdentifier : IHierarchyItemTypeIdentifier
{
    private TempNodeSystemBase? _system;
    private bool _systemCached;

    private static readonly Action<TempNodeSystemBase, Vector3>? CallInstantiateNodeSystem =
        Accessor.GenerateInstanceCaller<TempNodeSystemBase, Action<TempNodeSystemBase, Vector3>>("Instantiate", allowUnsafeTypeBinding: true);

    private static readonly Func<TempNodeSystemBase, IEnumerable<GameObject>>? CallEnumerateGameObjects =
        Accessor.GenerateInstanceCaller<TempNodeSystemBase, Func<TempNodeSystemBase, IEnumerable<GameObject>>>("EnumerateGameObjects", allowUnsafeTypeBinding: true);

    private static readonly Dictionary<Type, NodeItemTypeIdentifier> Pool = new Dictionary<Type, NodeItemTypeIdentifier>(3);
    public static NodeItemTypeIdentifier Get(Type type)
    {
        if (!DevkitServerModule.IsMainThread)
            return new NodeItemTypeIdentifier(type);

        if (!Pool.TryGetValue(type, out NodeItemTypeIdentifier id))
        {
            id = new NodeItemTypeIdentifier(type);
            Pool.Add(type, id);
        }
        return id;
    }
    public static IEnumerable<GameObject> EnumerateSystem(TempNodeSystemBase nodeSystemBase)
    {
        if (CallEnumerateGameObjects != null)
            return CallEnumerateGameObjects.Invoke(nodeSystemBase) ?? Array.Empty<GameObject>();

        MethodInfo? method = nodeSystemBase.GetType().GetMethod("GetAllNodes", BindingFlags.Public | BindingFlags.Instance);

        if (method != null && typeof(IEnumerable<TempNodeBase>).IsAssignableFrom(method.ReturnType))
            return (method.Invoke(nodeSystemBase, Array.Empty<object>()) as IEnumerable<TempNodeBase>)?.Select(x => x.gameObject) ?? Array.Empty<GameObject>();

        return Array.Empty<GameObject>();
    }

    public byte TypeIndex => 1;
    public Type Type { get; private set; }
    public TempNodeSystemBase? System
    {
        get
        {
            if (_systemCached)
                return _system;

            if (Interlocked.CompareExchange(ref _system, TryGetSystem(Type), null) == null)
                _systemCached = true;

            return _system;
        }
    }

    internal NodeItemTypeIdentifier()
    {
        Type = null!;
    }
    /// <summary>
    /// Use <see cref="Get"/> instead.
    /// </summary>
    internal NodeItemTypeIdentifier(Type type)
    {
        Type = type;
    }

    public void Instantiate(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        ThreadUtil.assertIsGameThread();

        if (CallInstantiateNodeSystem == null)
            return;

        TempNodeSystemBase? system = System;
        if (system == null)
            return;

        CallInstantiateNodeSystem(system, position);
    }

    public void Write(ByteWriter writer)
    {
        writer.Write(Type);
    }
    public void Read(ByteReader reader, ushort version)
    {
        int pos = reader.Position;
        Type? t = reader.ReadType();
        if (t == null)
        {
            if (reader.Stream == null && !reader.HasFailed)
            {
                reader.Goto(pos);
                Logger.DevkitServer.LogWarning(nameof(NodeItemTypeIdentifier), $"Node type not found: {reader.ReadTypeInfo().Format()}.");
            }
            if (reader.ThrowOnError)
                throw new Exception("Node type not found.");
        }
        else Type = t;
    }


    public static NodeItemTypeIdentifier? ReadFromPool(ByteReader reader)
    {
        int pos = reader.Position;
        Type? t = reader.ReadType();
        if (t == null)
        {
            if (reader.Stream == null && !reader.HasFailed)
            {
                reader.Goto(pos);
                Logger.DevkitServer.LogWarning(nameof(NodeItemTypeIdentifier), $"Node type not found: {reader.ReadTypeInfo().Format()}.");
            }

            if (reader.ThrowOnError)
                throw new Exception("Node type not found.");
        }
        else return Get(t);

        return null;
    }

    public static TempNodeSystemBase? TryGetSystem(Type nodeType)
    {
        if (nodeType == null)
        {
            Logger.DevkitServer.LogWarning(nameof(NodeItemTypeIdentifier), $"Unknown node type: {((object?)null).Format()}.");
            return null;
        }
        if (typeof(AirdropDevkitNode).IsAssignableFrom(nodeType))
            return AirdropDevkitNodeSystem.Get();

        if (typeof(LocationDevkitNode).IsAssignableFrom(nodeType))
            return LocationDevkitNodeSystem.Get();

        if (typeof(Spawnpoint).IsAssignableFrom(nodeType))
            return SpawnpointSystemV2.Get();

        try
        {
            Type t = Accessor.AssemblyCSharp.GetType("SDG.Unturned." + nodeType.Name + "SystemV2", false, true) ??
                     Accessor.AssemblyCSharp.GetType("SDG.Unturned." + nodeType.Name + "System", false, true) ??
                     Accessor.AssemblyCSharp.GetType("SDG.Framework.Devkit." + nodeType.Name + "SystemV2", false, true) ??
                     Accessor.AssemblyCSharp.GetType("SDG.Framework.Devkit." + nodeType.Name + "System", false, true);
            if (t == null || !typeof(TempNodeSystemBase).IsAssignableFrom(t))
                Logger.DevkitServer.LogWarning(nameof(NodeItemTypeIdentifier), $"Unknown node type: {nodeType.Format()}.");
            else
            {
                Logger.DevkitServer.LogWarning(nameof(NodeItemTypeIdentifier), $"Dynamically found system ({t.Format()}) from node: {nodeType.Format()}. This should be added to the cached list for performance.");
                MethodInfo? getter = t.GetMethod("Get", BindingFlags.Static | BindingFlags.Public | BindingFlags.IgnoreCase);
                if (getter == null || !getter.IsStatic || !t.IsAssignableFrom(getter.ReturnType) || getter.GetParameters().Length != 0)
                {
                    FieldInfo? instanceField = t.GetField("instance", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase);
                    if (instanceField == null || !instanceField.IsStatic || !t.IsAssignableFrom(instanceField.FieldType))
                    {
                        PropertyInfo? instanceProperty = t.GetProperty("instance", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase);
                        getter = instanceProperty?.GetGetMethod();
                        if (instanceProperty == null || getter == null || !getter.IsStatic || !t.IsAssignableFrom(getter.ReturnType))
                        {
                            Logger.DevkitServer.LogError(nameof(NodeItemTypeIdentifier), $"Unable to get instance of node system: {t}.");
                            return null;
                        }

                        return (TempNodeSystemBase)getter.Invoke(null, Array.Empty<object>());
                    }
                    return (TempNodeSystemBase)instanceField.GetValue(null);
                }
                return (TempNodeSystemBase)getter.Invoke(null, Array.Empty<object>());
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(nameof(NodeItemTypeIdentifier), ex, $"Error getting node system for {nodeType.Format()}.");
        }

        return null;
    }
    string ITerminalFormattable.Format(ITerminalFormatProvider provider) => "[HIID] Node: " + Type.Format();
    public override string ToString() => "[HIID] Node: " + Type.Name;
}

[EarlyTypeInit]
public sealed class VolumeItemTypeIdentifier : IHierarchyItemTypeIdentifier
{
    private VolumeManagerBase? _manager;
    private bool _managerCached;

    private static readonly Action<VolumeManagerBase, Vector3, Quaternion, Vector3>? CallInstantiateVolumeSystem =
        Accessor.GenerateInstanceCaller<VolumeManagerBase, Action<VolumeManagerBase, Vector3, Quaternion, Vector3>>("InstantiateVolume", throwOnError: false, allowUnsafeTypeBinding: true);

    private static readonly Dictionary<Type, VolumeItemTypeIdentifier> Pool = new Dictionary<Type, VolumeItemTypeIdentifier>(18);
    public static VolumeItemTypeIdentifier Get(Type type)
    {
        if (!DevkitServerModule.IsMainThread)
            return new VolumeItemTypeIdentifier(type);
        
        if (!Pool.TryGetValue(type, out VolumeItemTypeIdentifier id))
        {
            id = new VolumeItemTypeIdentifier(type);
            Pool.Add(type, id);
        }
        return id;
    }
    public byte TypeIndex => 2;
    public Type Type { get; private set; }
    public VolumeManagerBase? Manager
    {
        get
        {
            if (_managerCached)
                return _manager;

            if (Interlocked.CompareExchange(ref _manager, TryGetManager(Type), null) == null)
                _managerCached = true;

            return _manager;
        }
    }

    internal VolumeItemTypeIdentifier()
    {
        Type = null!;
    }
    /// <summary>
    /// Use <see cref="Get"/> instead.
    /// </summary>
    internal VolumeItemTypeIdentifier(Type type)
    {
        Type = type;
    }

    public void Instantiate(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        ThreadUtil.assertIsGameThread();

        if (CallInstantiateVolumeSystem == null)
            return;

        VolumeManagerBase? system = Manager;
        if (system == null)
            return;

        CallInstantiateVolumeSystem(system, position, rotation, scale);
    }

    public void Write(ByteWriter writer)
    {
        writer.Write(Type);
    }
    public void Read(ByteReader reader, ushort version)
    {
        int pos = reader.Position;
        Type? t = reader.ReadType();
        if (t == null)
        {
            if (reader.Stream == null && !reader.HasFailed)
            {
                reader.Goto(pos);
                Logger.DevkitServer.LogWarning(nameof(VolumeItemTypeIdentifier), $"Node type not found: {reader.ReadTypeInfo().Format()}.");
            }
            if (reader.ThrowOnError)
                throw new Exception("Node type not found.");
        }
        else Type = t;
    }
    public static VolumeItemTypeIdentifier? ReadFromPool(ByteReader reader)
    {
        int pos = reader.Position;
        Type? t = reader.ReadType();
        if (t == null)
        {
            if (reader.Stream == null && !reader.HasFailed)
            {
                reader.Goto(pos);
                Logger.DevkitServer.LogWarning(nameof(VolumeItemTypeIdentifier), $"Node type not found: {reader.ReadTypeInfo().Format()}.");
            }

            if (reader.ThrowOnError)
                throw new Exception("Node type not found.");
        }
        else return Get(t);

        return null;
    }
    public static Type? TryGetComponentType(VolumeManagerBase manager)
    {
        if (manager == null)
        {
            Logger.DevkitServer.LogWarning(nameof(VolumeItemTypeIdentifier), $"Unknown volume manager type: {((object?)null).Format()}.");
            return null;
        }

        if (manager is AmbianceVolumeManager)
            return typeof(AmbianceVolume);

        if (manager is ArenaCompactorVolumeManager)
            return typeof(ArenaCompactorVolume);

        if (manager is CartographyVolumeManager)
            return typeof(CartographyVolume);

        if (manager is CullingVolumeManager)
            return typeof(CullingVolume);

        if (manager is DeadzoneVolumeManager)
            return typeof(DeadzoneVolume);

        if (manager is EffectVolumeManager)
            return typeof(EffectVolume);

        if (manager is FoliageVolumeManager)
            return typeof(FoliageVolume);

        if (manager is HordePurchaseVolumeManager)
            return typeof(HordePurchaseVolume);

        if (manager is KillVolumeManager)
            return typeof(KillVolume);

        if (manager is LandscapeHoleVolumeManager)
            return typeof(LandscapeHoleVolume);

        if (manager is NavClipVolumeManager)
            return typeof(NavClipVolume);

        if (manager is OxygenVolumeManager)
            return typeof(OxygenVolume);

        if (manager is PlayerClipVolumeManager)
            return typeof(PlayerClipVolume);

        if (manager is SafezoneVolumeManager)
            return typeof(SafezoneVolume);

        if (manager is TeleporterEntranceVolumeManager)
            return typeof(TeleporterEntranceVolume);

        if (manager is TeleporterExitVolumeManager)
            return typeof(TeleporterExitVolume);

        if (manager is UndergroundWhitelistVolumeManager)
            return typeof(UndergroundWhitelistVolume);

        if (manager is WaterVolumeManager)
            return typeof(WaterVolume);

        IEnumerable<VolumeBase> v = manager.EnumerateAllVolumes();
        Type type = v.GetType();
        if (type.IsGenericType)
        {
            Type[] gens = type.GetGenericArguments();
            for (int i = 0; i < gens.Length; ++i)
            {
                Type gen = gens[i];
                if (typeof(VolumeBase).IsAssignableFrom(gen) && gen != typeof(VolumeBase))
                {
                    Logger.DevkitServer.LogInfo(nameof(VolumeItemTypeIdentifier), $"Dynamically found volume ({gen.Format()}) from manager: {manager.GetType().Format()}. This should be added to the cached list for performance.");
                    return gen;
                }
            }
        }

        bool redo = false;
        type = manager.GetType();
        doRedo:
        if (type != null && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(VolumeManager<,>))
        {
            type = type.GetGenericArguments()[0];
            Logger.DevkitServer.LogInfo(nameof(VolumeItemTypeIdentifier), $"Dynamically found volume ({type.Format()}) from manager: {manager.GetType().Format()}. This should be added to the cached list for performance.");
            return type;
        }

        if (!redo && type != null)
        {
            type = type.BaseType!;
            redo = true;
            goto doRedo;
        }

        return null;
    }
    public static VolumeManagerBase? TryGetManager(Type nodeType)
    {
        if (nodeType == null)
        {
            Logger.DevkitServer.LogWarning(nameof(VolumeItemTypeIdentifier), $"Unknown volume type: {((object?)null).Format()}.");
            return null;
        }
        if (typeof(AmbianceVolume).IsAssignableFrom(nodeType))
            return AmbianceVolumeManager.Get();

        if (typeof(ArenaCompactorVolume).IsAssignableFrom(nodeType))
            return ArenaCompactorVolumeManager.Get();

        if (typeof(CartographyVolume).IsAssignableFrom(nodeType))
            return CartographyVolumeManager.Get();

        if (typeof(CullingVolume).IsAssignableFrom(nodeType))
            return CullingVolumeManager.Get();

        if (typeof(DeadzoneVolume).IsAssignableFrom(nodeType))
            return DeadzoneVolumeManager.Get();

        if (typeof(EffectVolume).IsAssignableFrom(nodeType))
            return EffectVolumeManager.Get();

        if (typeof(FoliageVolume).IsAssignableFrom(nodeType))
            return FoliageVolumeManager.Get();

        if (typeof(HordePurchaseVolume).IsAssignableFrom(nodeType))
            return HordePurchaseVolumeManager.Get();

        if (typeof(KillVolume).IsAssignableFrom(nodeType))
            return KillVolumeManager.Get();

        if (typeof(LandscapeHoleVolume).IsAssignableFrom(nodeType))
            return LandscapeHoleVolumeManager.Get();

        if (typeof(NavClipVolume).IsAssignableFrom(nodeType))
            return NavClipVolumeManager.Get();

        if (typeof(OxygenVolume).IsAssignableFrom(nodeType))
            return OxygenVolumeManager.Get();

        if (typeof(PlayerClipVolume).IsAssignableFrom(nodeType))
            return PlayerClipVolumeManager.Get();

        if (typeof(SafezoneVolume).IsAssignableFrom(nodeType))
            return SafezoneVolumeManager.Get();

        if (typeof(TeleporterEntranceVolume).IsAssignableFrom(nodeType))
            return TeleporterEntranceVolumeManager.Get();

        if (typeof(TeleporterExitVolume).IsAssignableFrom(nodeType))
            return TeleporterExitVolumeManager.Get();

        if (typeof(UndergroundWhitelistVolume).IsAssignableFrom(nodeType))
            return UndergroundWhitelistVolumeManager.Get();

        if (typeof(WaterVolume).IsAssignableFrom(nodeType))
            return WaterVolumeManager.Get();

        try
        {
            Type t = Accessor.AssemblyCSharp.GetType("SDG.Unturned." + nodeType.Name + "ManagerV2", false, true) ??
                     Accessor.AssemblyCSharp.GetType("SDG.Unturned." + nodeType.Name + "Manager", false, true) ??
                     Accessor.AssemblyCSharp.GetType("SDG.Framework.Devkit." + nodeType.Name + "ManagerV2", false, true) ??
                     Accessor.AssemblyCSharp.GetType("SDG.Framework.Devkit." + nodeType.Name + "Manager", false, true);
            if (t == null || !typeof(VolumeManagerBase).IsAssignableFrom(t))
                Logger.DevkitServer.LogWarning(nameof(VolumeItemTypeIdentifier), $"Unknown volume type: {nodeType.Format()}.");
            else
            {
                Logger.DevkitServer.LogInfo(nameof(VolumeItemTypeIdentifier), $"Dynamically found manager ({t.Format()}) from volume: {nodeType.Format()}. This should be added to the cached list for performance.");
                MethodInfo? getter = t.GetMethod("Get", BindingFlags.Static | BindingFlags.Public | BindingFlags.IgnoreCase);
                if (getter == null || !getter.IsStatic || !t.IsAssignableFrom(getter.ReturnType) || getter.GetParameters().Length != 0)
                {
                    FieldInfo? instanceField = t.GetField("instance", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase);
                    if (instanceField == null || !instanceField.IsStatic || !t.IsAssignableFrom(instanceField.FieldType))
                    {
                        PropertyInfo? instanceProperty = t.GetProperty("instance", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase);
                        getter = instanceProperty?.GetGetMethod();
                        if (instanceProperty == null || getter == null || !getter.IsStatic || !t.IsAssignableFrom(getter.ReturnType))
                        {
                            Logger.DevkitServer.LogError(nameof(VolumeItemTypeIdentifier), $"Unable to get instance of volume manager: {t}.");
                            return null;
                        }

                        return (VolumeManagerBase)getter.Invoke(null, Array.Empty<object>());
                    }
                    return (VolumeManagerBase)instanceField.GetValue(null);
                }
                return (VolumeManagerBase)getter.Invoke(null, Array.Empty<object>());
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(nameof(VolumeItemTypeIdentifier), ex, $"Error getting volume manager for {nodeType.Format()}.");
        }

        return null;
    }
    string ITerminalFormattable.Format(ITerminalFormatProvider provider) => "[HIID] Volume: " + Type.Format();
    public override string ToString() => "[HIID] Volume: " + Type.Name;
}

[EarlyTypeInit]
public sealed class LandscapeItemTypeIdentifier : IHierarchyItemTypeIdentifier
{
    private static readonly Type LandscapeType = typeof(Landscape);
    public Type Type => LandscapeType;
    public byte TypeIndex => 4;
    public static LandscapeItemTypeIdentifier Instance { get; } = new LandscapeItemTypeIdentifier();
    private LandscapeItemTypeIdentifier() { }
    public void Instantiate(Vector3 position, Quaternion rotation, Vector3 scale) => throw new NotSupportedException("Landscape is a singleton and can not be instantiated. Access with Landscape.instance.");
    public void Write(ByteWriter writer) { }
    public void Read(ByteReader reader, ushort version) { }
    string ITerminalFormattable.Format(ITerminalFormatProvider provider) => ToString();
    public override string ToString() => "[HIID] Landscape";
}

[EarlyTypeInit]
public sealed class FoliageSystemItemTypeIdentifier : IHierarchyItemTypeIdentifier
{
    private static readonly Type FoliageSystemType = typeof(FoliageSystem);
    public Type Type => FoliageSystemType;
    public byte TypeIndex => 5;
    public static FoliageSystemItemTypeIdentifier Instance { get; } = new FoliageSystemItemTypeIdentifier();
    private FoliageSystemItemTypeIdentifier() { }
    public void Instantiate(Vector3 position, Quaternion rotation, Vector3 scale) => throw new NotSupportedException("Foliage system is a singleton and can not be instantiated. Access with FoliageSystem.instance.");
    public void Write(ByteWriter writer) { }
    public void Read(ByteReader reader, ushort version) { }
    string ITerminalFormattable.Format(ITerminalFormatProvider provider) => ToString();
    public override string ToString() => "[HIID] Foliage System";
}