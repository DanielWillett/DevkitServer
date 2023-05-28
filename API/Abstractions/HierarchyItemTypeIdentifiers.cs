using System.Globalization;
using DevkitServer.Util.Encoding;
using SDG.Framework.Devkit;
using SDG.Framework.Foliage;
using SDG.Framework.Landscapes;
using SDG.Framework.Water;
using System.Reflection;

namespace DevkitServer.API.Abstractions;
public static class HierarchyItemTypeIdentifierEx
{
    private const ushort DataVersion = 0;
    private const int MaxType = 5;

    public static void Instantiate(this IHierarchyItemTypeIdentifier identifier, Vector3 position) =>
        identifier.Instantiate(position, Quaternion.identity, Vector3.one);
    public static void WriteIdentifier(ByteWriter writer, IHierarchyItemTypeIdentifier? identifier)
    {
        writer.Write(DataVersion);

        if (identifier == null)
        {
            writer.Write(DataVersion);
            writer.Write((byte)255);
            return;
        }
        byte type = identifier.TypeIndex;
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
            // 3 => new ObjectItemTypeIdentifier(),
            4 => LandscapeItemTypeIdentifier.Instance,
            5 => FoliageSystemItemTypeIdentifier.Instance,
            _ => type2 != null && !type2.IsAbstract && typeof(IHierarchyItemTypeIdentifier).IsAssignableFrom(type2)
                ? Activator.CreateInstance(type2, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.CreateInstance | BindingFlags.Instance,
                    null, Array.Empty<object>(), CultureInfo.InvariantCulture) as IHierarchyItemTypeIdentifier
                : null
        };
        if (val == null)
        {
            Logger.LogWarning($"[READ IDENTIFIER] Failed to read identifier type: {type2?.Format() ?? type.Format()}.");
            return null;
        }
        if (type == 3)
            val.Read(reader, version);
        return val;
    }
}

public interface IHierarchyItemTypeIdentifier
{
    byte TypeIndex { get; }
    Type Type { get; }
    void Instantiate(Vector3 position, Quaternion rotation, Vector3 scale);
    void Write(ByteWriter writer);
    void Read(ByteReader reader, ushort version);
    string FormatToString();
}

[EarlyTypeInit]
public sealed class NodeItemTypeIdentifier : IHierarchyItemTypeIdentifier
{
    private static readonly Action<TempNodeSystemBase, Vector3>? CallInstantiateNodeSystem =
        Accessor.GenerateInstanceCaller<TempNodeSystemBase, Action<TempNodeSystemBase, Vector3>>("Instantiate",
            new Type[] { typeof(Vector3) }, false);

    private static readonly Dictionary<Type, NodeItemTypeIdentifier> Pool = new Dictionary<Type, NodeItemTypeIdentifier>(3);
    public static NodeItemTypeIdentifier Get(Type type)
    {
        if (!Pool.TryGetValue(type, out NodeItemTypeIdentifier id))
        {
            id = new NodeItemTypeIdentifier(type);
            Pool.Add(type, id);
        }
        return id;
    }

    public byte TypeIndex => 1;
    public Type Type { get; private set; }

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

        TempNodeSystemBase? system = TryGetSystem(Type);
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
                Logger.LogWarning($"Node type not found: {reader.ReadTypeInfo().Format()}.");
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
                Logger.LogWarning($"Node type not found: {reader.ReadTypeInfo().Format()}.");
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
            Logger.LogWarning($"Unknown node type: {((object?)null).Format()}.");
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
                Logger.LogWarning($"Unknown node type: {nodeType.Format()}.");
            else
            {
                Logger.LogInfo($"Dynamically found system ({t.Format()}) from node: {nodeType.Format()}. This should be added to the cached list for performance.");
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
                            Logger.LogError($"Unable to get instance of node system: {t}.");
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
            Logger.LogError($"Error getting node system for {nodeType.Format()}.");
            Logger.LogError(ex);
        }

        return null;
    }
    string IHierarchyItemTypeIdentifier.FormatToString() => "[HIID] Node: " + Type.Format();
    public override string ToString() => "[HIID] Node: " + Type.Name;
}

[EarlyTypeInit]
public sealed class VolumeItemTypeIdentifier : IHierarchyItemTypeIdentifier
{
    private static readonly Action<VolumeManagerBase, Vector3, Quaternion, Vector3>? CallInstantiateVolumeSystem =
        Accessor.GenerateInstanceCaller<VolumeManagerBase, Action<VolumeManagerBase, Vector3, Quaternion, Vector3>>("InstantiateVolume",
            new Type[] { typeof(Vector3), typeof(Quaternion), typeof(Vector3) }, false);

    private static readonly Dictionary<Type, VolumeItemTypeIdentifier> Pool = new Dictionary<Type, VolumeItemTypeIdentifier>(18);
    public static VolumeItemTypeIdentifier Get(Type type)
    {
        if (!Pool.TryGetValue(type, out VolumeItemTypeIdentifier id))
        {
            id = new VolumeItemTypeIdentifier(type);
            Pool.Add(type, id);
        }
        return id;
    }
    public byte TypeIndex => 2;
    public Type Type { get; private set; }

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

        VolumeManagerBase? system = TryGetManager(Type);
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
                Logger.LogWarning($"Node type not found: {reader.ReadTypeInfo().Format()}.");
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
                Logger.LogWarning($"Node type not found: {reader.ReadTypeInfo().Format()}.");
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
            Logger.LogWarning($"Unknown volume manager type: {((object?)null).Format()}.");
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
                    Logger.LogInfo($"Dynamically found volume ({gen.Format()}) from manager: {manager.GetType().Format()}. This should be added to the cached list for performance.");
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
            Logger.LogInfo($"Dynamically found volume ({type.Format()}) from manager: {manager.GetType().Format()}. This should be added to the cached list for performance.");
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
            Logger.LogWarning($"Unknown volume type: {((object?)null).Format()}.");
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
                Logger.LogWarning($"Unknown volume type: {nodeType.Format()}.");
            else
            {
                Logger.LogInfo($"Dynamically found manager ({t.Format()}) from volume: {nodeType.Format()}. This should be added to the cached list for performance.");
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
                            Logger.LogError($"Unable to get instance of volume manager: {t}.");
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
            Logger.LogError($"Error getting volume manager for {nodeType.Format()}.");
            Logger.LogError(ex);
        }

        return null;
    }
    string IHierarchyItemTypeIdentifier.FormatToString() => "[HIID] Volume: " + Type.Format();
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
    string IHierarchyItemTypeIdentifier.FormatToString() => ToString();
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
    string IHierarchyItemTypeIdentifier.FormatToString() => ToString();
    public override string ToString() => "[HIID] Foliage System";
}