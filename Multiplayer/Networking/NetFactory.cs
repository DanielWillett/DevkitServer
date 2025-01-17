#if DEBUG
//#define METHOD_LOGGING
//#define REFLECTION_LOGGING
//#define PRINT_DATA
//#define MESSAGE_ENUM_LOGGING
#endif

using Cysharp.Threading.Tasks;
using DanielWillett.ReflectionTools;
using DanielWillett.SpeedBytes;
using DevkitServer.API;
using DevkitServer.API.Multiplayer;
using DevkitServer.API.Permissions;
using DevkitServer.Multiplayer.Actions;
using DevkitServer.Multiplayer.Movement;
using DevkitServer.Patches;
using DevkitServer.Plugins;
using HarmonyLib;
using SDG.NetPak;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
#if PRINT_DATA
using DanielWillett.SpeedBytes.Formatting;
#endif
#if CLIENT
using DevkitServer.Multiplayer.Sync;
#endif

namespace DevkitServer.Multiplayer.Networking;

/// <summary>
/// Handles injecting DevkitServer messages into the game's networking system. Also handles dispatching and invoking <see cref="NetCall"/>s.
/// </summary>
[EarlyTypeInit]
public static class NetFactory
{
    /// <summary>The maximum amount of time in seconds a listener is guaranteed to stay active.</summary>
    public const double MaxListenTimeout = 600d;

    internal const string Source = "NET FACTORY";

    /// <summary>The maximum recommended low-speed packet size (due to steamworks throughput limitations).</summary>
#if CLIENT
    public const int MaxPacketSize = 61440;
#else
    public const int MaxPacketSize = 40960;
#endif

#nullable disable
    private static long[] _inByteCt;
    private static long[] _outByteCt;
#nullable restore
    private static float _inByteCtStTime;
    private static float _outByteCtStTime;
    private static uint _disallowedServerBits;
    private static uint _disallowedClientBits;
    private static uint _disallowedServerBitMask;
    private static uint _disallowedClientBitMask;

    private static readonly List<ListenerTimestamp> LocalListenerTimestamps = new List<ListenerTimestamp>(32);
    private static readonly ConcurrentDictionary<long, Listener> LocalListeners = new ConcurrentDictionary<long, Listener>();
    private static readonly ConcurrentDictionary<long, Listener> LocalAckRequests = new ConcurrentDictionary<long, Listener>();

    private static readonly ConcurrentDictionary<ushort, NetMethodInfo[]> Methods = new ConcurrentDictionary<ushort, NetMethodInfo[]>();
    private static readonly ConcurrentDictionary<Guid, NetMethodInfo[]> GuidMethods = new ConcurrentDictionary<Guid, NetMethodInfo[]>();

    private static readonly ConcurrentDictionary<ushort, BaseNetCall> Invokers = new ConcurrentDictionary<ushort, BaseNetCall>();
    private static readonly ConcurrentDictionary<ushort, BaseNetCall> HighSpeedInvokers = new ConcurrentDictionary<ushort, BaseNetCall>();

    private static readonly ConcurrentDictionary<Guid, BaseNetCall> GuidInvokers = new ConcurrentDictionary<Guid, BaseNetCall>();
    private static readonly ConcurrentDictionary<Guid, BaseNetCall> GuidHighSpeedInvokers = new ConcurrentDictionary<Guid, BaseNetCall>();

    private static Func<PooledTransportConnectionList>? PullFromTransportConnectionListPool;
    internal static readonly NetPakWriter Writer = new NetPakWriter();

    /// <summary>
    /// Offset of the net messages for received data. On the server this would be of type <see cref="EServerMessage"/>, and on the client it would be of type <see cref="EClientMessage"/>.
    /// </summary>
    public static int ReceiveBlockOffset { get; private set; }

    /// <summary>
    /// Offset of the net messages for sent data. On the server this would be of type <see cref="EClientMessage"/>, and on the client it would be of type <see cref="EServerMessage"/>.
    /// </summary>
    public static int WriteBlockOffset { get; private set; }

    /// <summary>
    /// Size of the net message block taken up by DevkitServer and <see cref="ICustomNetMessageListener"/>s.
    /// </summary>
    public static int BlockSize { get; private set; }

    /// <summary>
    /// Size of the net message block taken up by vanilla DevkitServer.
    /// </summary>
    public static int DevkitServerBlockSize { get; private set; }

    /// <summary>
    /// New max bits count of the message enum for receiving data. On the server this would be <see cref="EServerMessage"/>, and on the client it would be <see cref="EClientMessage"/>.
    /// </summary>
    public static int NewReceiveBitCount { get; private set; }

    /// <summary>
    /// New max bits count of the message enum for sending data. On the server this would be <see cref="EClientMessage"/>, and on the client it would be <see cref="EServerMessage"/>.
    /// </summary>
    public static int NewWriteBitCount { get; private set; }

    /// <summary>
    /// Old max bits count of the message enum for receiving data. On the server this would be <see cref="EServerMessage"/>, and on the client it would be <see cref="EClientMessage"/>.
    /// </summary>
    public static int OldReceiveBitCount { get; private set; }

    /// <summary>
    /// Old max bits count of the message enum for sending data. On the server this would be <see cref="EClientMessage"/>, and on the client it would be <see cref="EServerMessage"/>.
    /// </summary>
    public static int OldWriteBitCount { get; private set; }

    /// <summary>
    /// Message listeners for the DevkitServer message block. On the server these are of type NetMessages.ServerReadHandler, and on the client they're of type NetMessages.ClientReadHandler.
    /// </summary>
    public static IReadOnlyList<Delegate> Listeners { get; private set; } = Array.Empty<Delegate>();


#if CLIENT
    internal static StaticGetter<IClientTransport> GetPlayerTransportConnection =
        Accessor.GenerateStaticGetter<Provider, IClientTransport>("clientTransport", throwOnError: true)!;
#endif
    internal static bool ReclaimMessageBlock()
    {
        Type devkitType = typeof(DevkitServerMessage);
        List<(ICustomNetMessageListener, PluginAssembly)> pluginListeners = [];
        foreach (PluginAssembly assembly in PluginLoader.Assemblies
                     .OrderBy(x => x.Assembly.GetName().Name)
                     .ThenByDescending(x => x.Assembly.GetName().Version))
        {
            if (assembly.CustomNetMessageListeners.Count > 0)
                pluginListeners.AddRange(assembly.CustomNetMessageListeners.Select(x => (x, assembly)));
        }

        // calculates the minimum amount of bits needed to represent the number.
        static int GetMinBitCount(int n) => (int)Math.Floor(Math.Log(n, 2)) + 1;

        FieldInfo[] clientFields = typeof(EClientMessage).GetFields(BindingFlags.Static | BindingFlags.Public);
        FieldInfo[] serverFields = typeof(EServerMessage).GetFields(BindingFlags.Static | BindingFlags.Public);
        FieldInfo[] devkitServerFields = devkitType.GetFields(BindingFlags.Static | BindingFlags.Public);

        int clientBlockSize = clientFields.Max(x => Convert.ToInt32(x.GetValue(null))) + 1;
        int serverBlockSize = serverFields.Max(x => Convert.ToInt32(x.GetValue(null))) + 1;
        int devkitServerBlockSize = devkitServerFields.Max(x => Convert.ToInt32(x.GetValue(null))) + 1;

#if SERVER
        ReceiveBlockOffset = serverBlockSize;
        WriteBlockOffset = clientBlockSize;
#else
        ReceiveBlockOffset = clientBlockSize;
        WriteBlockOffset = serverBlockSize;
#endif
        // since GetWorkshopFiles and DownloadWorkshopFiles are sent before the client could possibly know if the server is a DevkitServer server,
        // we need to make sure none of the lower bits that overlap with the vanilla block are the same as these.
        //
        // This way we can read/write GetWorkshopFiles and DownloadWorkshopFiles in the vanilla block size
        // and use these messages to check if it's a DevkitServer server or not
        FieldInfo? disallowedClientField = clientFields.FirstOrDefault(x => x.Name.Equals(nameof(EClientMessage.DownloadWorkshopFiles), StringComparison.Ordinal));
        FieldInfo? disallowedServerField = serverFields.FirstOrDefault(x => x.Name.Equals(nameof(EServerMessage.GetWorkshopFiles), StringComparison.Ordinal));

        if (disallowedClientField != null)
            _disallowedClientBits = unchecked( (uint)Convert.ToInt32(disallowedClientField.GetValue(null)) );
        else
            _disallowedClientBits = uint.MaxValue;

        if (disallowedServerField != null)
            _disallowedServerBits = unchecked( (uint)Convert.ToInt32(disallowedServerField.GetValue(null)) );
        else
            _disallowedServerBits = uint.MaxValue;

        _disallowedClientBitMask = 0;
        _disallowedServerBitMask = 0;

        int clientBitCount = GetMinBitCount(clientFields.Length);
        for (int i = 0; i < clientBitCount; ++i)
            _disallowedClientBitMask |= 1u << i;

        int serverBitCount = GetMinBitCount(clientFields.Length);
        for (int i = 0; i < serverBitCount; ++i)
            _disallowedServerBitMask |= 1u << i;

        int nextOffset = DevkitServerBlockSize;
        for (int i = 0; i < pluginListeners.Count; ++i)
        {
            do
            {
                ++nextOffset;
            } while (((unchecked( (uint)nextOffset ) + clientBlockSize) & _disallowedClientBitMask) == _disallowedClientBits || ((unchecked( (uint)nextOffset ) + serverBlockSize) & _disallowedServerBitMask) == _disallowedServerBits);
        }

        int pluginListenerBlockSize = nextOffset + 1;

        DevkitServerBlockSize = devkitServerBlockSize;
        BlockSize = DevkitServerBlockSize + pluginListenerBlockSize;
        OldReceiveBitCount = GetMinBitCount(ReceiveBlockOffset);
        OldWriteBitCount = GetMinBitCount(WriteBlockOffset);

        bool anyDisallowed = false;
        foreach (FieldInfo dsEnumField in devkitServerFields.Where(x => ((unchecked( (uint)Convert.ToInt32(x.GetValue(null)) ) + serverBlockSize) & _disallowedClientBitMask) == _disallowedClientBits))
        {
            Logger.DevkitServer.LogError(Source, $"DevkitServer enum field {dsEnumField.Name.Colorize(FormattingColorType.Enum)} overlaps the disallowed client bits.");
            anyDisallowed = true;
        }
        foreach (FieldInfo dsEnumField in devkitServerFields.Where(x => ((unchecked( (uint)Convert.ToInt32(x.GetValue(null))) + clientBlockSize) & _disallowedServerBitMask) == _disallowedServerBits))
        {
            Logger.DevkitServer.LogError(Source, $"DevkitServer enum field {dsEnumField.Name.Colorize(FormattingColorType.Enum)} overlaps the disallowed server bits.");
            anyDisallowed = true;
        }

        if (anyDisallowed)
        {
            NewReceiveBitCount = OldReceiveBitCount;
            NewWriteBitCount = OldWriteBitCount;
            BlockSize = 0;
            return false;
        }

        NewReceiveBitCount = GetMinBitCount(ReceiveBlockOffset + BlockSize);
        NewWriteBitCount = GetMinBitCount(WriteBlockOffset + BlockSize);

        Logger.DevkitServer.LogDebug(Source, "Collected message block data: " +
                        $"Offsets: (Receive = {ReceiveBlockOffset.Format()}, Write = {WriteBlockOffset.Format()}) | " +
                        $"Size = {BlockSize.Format()} | " +
                        $"Original Bit Count = (Receive: {OldReceiveBitCount.Format()}b, Write: {OldWriteBitCount.Format()}b) | " +
                        $"Final Bit Count = (Receive: {NewReceiveBitCount.Format()}b, Write: {NewWriteBitCount.Format()}b) |" +
                        $"Disallowed client bits = 0b{Convert.ToString(_disallowedClientBits, 2)} (mask: 0b{Convert.ToString(_disallowedClientBitMask)}) |" +
                        $"Disallowed server bits = 0b{Convert.ToString(_disallowedServerBits, 2)} (mask: 0b{Convert.ToString(_disallowedServerBitMask)})" +
                        ".");

        // initialize DevkitMessage callbacks
        MethodInfo[] methods = new MethodInfo[BlockSize];

        MethodInfo identity = Accessor.GetMethod(ReceiveIdentity)!;
        for (int i = 0; i < methods.Length; ++i)
            methods[i] = identity;

        // vanilla callbacks
        methods[(int)DevkitServerMessage.InvokeNetCall] = typeof(NetFactory).GetMethod(nameof(ReceiveMessage), BindingFlags.NonPublic | BindingFlags.Static)!;
#if CLIENT
        methods[(int)DevkitServerMessage.MovementRelay] = typeof(ClientUserMovement).GetMethod(nameof(ClientUserMovement.ReceiveRemoteMovementPackets), BindingFlags.NonPublic | BindingFlags.Static)!;
        methods[(int)DevkitServerMessage.SendTileData] = typeof(TileSync).GetMethod(nameof(TileSync.ReceiveTileData), BindingFlags.NonPublic | BindingFlags.Static)!;
        methods[(int)DevkitServerMessage.SendNavigationData] = typeof(NavigationSync).GetMethod(nameof(NavigationSync.ReceiveNavigationData), BindingFlags.NonPublic | BindingFlags.Static)!;
#elif SERVER
        methods[(int)DevkitServerMessage.MovementRelay] = typeof(ServerUserMovement).GetMethod(nameof(ServerUserMovement.ReceiveUserMovementPacket), BindingFlags.NonPublic | BindingFlags.Static)!;
#endif
        methods[(int)DevkitServerMessage.ActionRelay] = typeof(EditorActions).GetMethod(nameof(EditorActions.ReceiveActionRelay), BindingFlags.NonPublic | BindingFlags.Static)!;

        // plugin callbacks
        MethodInfo? interfaceMethod = typeof(ICustomNetMessageListener).GetMethod(nameof(ICustomNetMessageListener.OnRawMessageReceived), BindingFlags.Public | BindingFlags.Instance);
        if (interfaceMethod != null)
        {
            nextOffset = DevkitServerBlockSize;
            for (int i = 0; i < pluginListeners.Count; ++i)
            {
                while (((unchecked( (uint)nextOffset ) + clientBlockSize) & _disallowedClientBitMask) == _disallowedClientBits
                       || ((unchecked( (uint)nextOffset ) + serverBlockSize) & _disallowedServerBitMask) == _disallowedServerBits)
                {
                    ++nextOffset;
                }

                (ICustomNetMessageListener listener, PluginAssembly assembly) = pluginListeners[i];
                Type listenerType = listener.GetType();
                MethodInfo? implMethod = Accessor.GetImplementedMethod(listenerType, interfaceMethod);
                if (implMethod == null)
                {
                    assembly.LogWarning(Source, $"Unable to find {typeof(ICustomNetMessageListener).Format()}.{nameof(ICustomNetMessageListener.OnRawMessageReceived).Colorize(FormattingColorType.Method)} " +
                                                $"implementation in {listenerType.Format()}.");
                    continue;
                }

                methods[nextOffset] = implMethod;
                ++nextOffset;

                assembly.LogInfo(Source, $"Registered {typeof(ICustomNetMessageListener).Format()} of type {listenerType.Format()} (receivable on: {listener.ReceivingSide.Format()}).");
            }
        }
        else
        {
            Logger.DevkitServer.LogWarning(Source, $"Unable to find {typeof(ICustomNetMessageListener).Format()}.{nameof(ICustomNetMessageListener.OnRawMessageReceived).Colorize(FormattingColorType.Method)}.");
        }

        Delegate[] listeners = new Delegate[methods.Length];
        Listeners = new ReadOnlyCollection<Delegate>(listeners);
        _inByteCt = new long[BlockSize];
        _outByteCt = new long[BlockSize];
        _inByteCtStTime = _outByteCtStTime = CachedTime.RealtimeSinceStartup;

        // patch reading enum method.

#if SERVER
        Type readEnum = typeof(EServerMessage_NetEnum);
        Type writeEnum = typeof(EClientMessage_NetEnum);
#else
        Type readEnum = typeof(EClientMessage_NetEnum);
        Type writeEnum = typeof(EServerMessage_NetEnum);
#endif
        bool p1 = false;
        bool p2 = false;
        MethodInfo? readMethod = readEnum.GetMethod("ReadEnum", BindingFlags.Static | BindingFlags.Public);
        MethodInfo? writeMethod = writeEnum.GetMethod("WriteEnum", BindingFlags.Static | BindingFlags.Public);
        if (readMethod == null)
        {
            if (writeMethod == null)
                Logger.DevkitServer.LogError(Source, "Unable to find " + readEnum.Format() + ".ReadEnum(...) or " + writeEnum.Format() + ".WriteEnum(...) !");
            else
                Logger.DevkitServer.LogError(Source, "Unable to find " + readEnum.Format() + ".ReadEnum(...)!");

            goto reset;
        }
        if (writeMethod == null)
        {
            Logger.DevkitServer.LogError(Source, "Unable to find " + writeEnum.Format() + ".WriteEnum(...) !");
            goto reset;
        }

        try
        {
            PatchesMain.Patcher.Patch(readMethod, prefix: new HarmonyMethod(typeof(NetFactory).GetMethod("ReadEnumPatch", BindingFlags.Static | BindingFlags.NonPublic)));
            p1 = true;
            PatchesMain.Patcher.Patch(writeMethod, prefix: new HarmonyMethod(typeof(NetFactory).GetMethod("WriteEnumPatch", BindingFlags.Static | BindingFlags.NonPublic)));
            p2 = true;
        }
        catch (Exception ex)
        {
            if (p1)
            {
                try
                {
                    PatchesMain.Patcher.Unpatch(readMethod, HarmonyPatchType.Prefix, PatchesMain.HarmonyId);
                }
                catch (Exception ex2)
                {
                    Logger.DevkitServer.LogWarning(Source, ex2, $"Unable to unpatch {readEnum.Format()}.ReadEnum(...) after an error patching {writeEnum.Format()}.WriteEnum(...).");
                }
            }
            Logger.DevkitServer.LogError(Source, ex, "Failed to patch networking enum " + (p1 ? "write method." : "read and/or write methods."));
            goto reset;
        }

        const string netMessagesName = "SDG.Unturned.NetMessages";
        Type? netMessagesType;
        try
        {
            netMessagesType = AccessorExtensions.AssemblyCSharp.GetType(netMessagesName, true, false);
            if (netMessagesType == null)
            {
                Logger.DevkitServer.LogError(Source, $"Unable to find type {netMessagesName.Colorize(FormattingColorType.Class)}!");
                goto reset;
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(Source, ex, $"Unable to find type {netMessagesName.Colorize(FormattingColorType.Class)}!");
            goto reset;
        }
        RuntimeHelpers.RunClassConstructor(netMessagesType.TypeHandle);

        // add network listeners to array
        const string fieldName =
#if SERVER
            "serverReadCallbacks";
#else
            "clientReadCallbacks";
#endif
        FieldInfo? field = netMessagesType.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
        if (field == null)
        {
            Logger.DevkitServer.LogError(Source, $"Unable to find {netMessagesType.Format()}.{fieldName.Colorize(FormattingColorType.Property)}!");
            goto reset;
        }
        Array readCallbacks = (Array)field.GetValue(null);

        Type callbackType = readCallbacks.GetType().GetElementType()!;

        Array nArr = Array.CreateInstance(callbackType, readCallbacks.Length + Listeners.Count);
        Array.Copy(readCallbacks, nArr, readCallbacks.Length);

#if SERVER
        const ConnectionSide receivingSide = ConnectionSide.Server;
#else
        const ConnectionSide receivingSide = ConnectionSide.Client;
#endif

        CustomNetMessageListeners.LocalMappings.Clear();
        CustomNetMessageListeners.InverseLocalMappings.Clear();

        int pluginListenerIndex = -1;
        for (int i = 0; i < methods.Length; ++i)
        {
            MethodInfo? m = methods[i];
            if (m == null || m == identity)
                continue;

            int index = readCallbacks.Length + i;
            
            DevkitServerMessage messageIndex = (DevkitServerMessage)i;
            if (i >= DevkitServerBlockSize)
            {
                (ICustomNetMessageListener listener, PluginAssembly assembly) = pluginListeners[++pluginListenerIndex];
                Type listenerType = listener.GetType();
                if ((listener.ReceivingSide & receivingSide) == 0)
                {
                    assembly.LogInfo(Source, $"Skipping implementing {listenerType.Format()} as it's not received on this connection side ({receivingSide.Format()}).");
                    continue;
                }

                try
                {
                    listener.LocalMessageIndex = messageIndex;
                    CustomNetMessageListeners.LocalMappings[listenerType] = messageIndex;
                    CustomNetMessageListeners.InverseLocalMappings[messageIndex] = listenerType;
                    nArr.SetValue(listeners[i] = m.CreateDelegate(callbackType, listener), index);
                }
                catch (Exception ex)
                {
                    assembly.LogError(Source, ex, $"Implemented {nameof(ICustomNetMessageListener.OnRawMessageReceived).Colorize(FormattingColorType.Method)} method for " +
                                                  $"{listenerType.Format()} can not be converted to {callbackType.Format()}. Ensure your side settings are correct.");
                }
            }
            else
            {
                try
                {
                    nArr.SetValue(listeners[i] = m.CreateDelegate(callbackType), index);
                }
                catch (Exception ex)
                {
                    Logger.DevkitServer.LogError(Source, ex, $"Implemented method for {messageIndex.Format()} can not be converted to {callbackType.Format()}.");
                    goto reset;
                }
            }
        }

        field.SetValue(null, nArr);

        CustomNetMessageListeners.AreLocalMappingsDirty = true;

        if (DevkitServerModule.IsEditing)
            CustomNetMessageListeners.SendLocalMappings();
        
        Logger.DevkitServer.LogDebug(Source, "Reclaimed networking block.");
        return true;

    reset:
        NewReceiveBitCount = OldReceiveBitCount;
        NewWriteBitCount = OldWriteBitCount;
        BlockSize = 0;
        Listeners = Array.Empty<Delegate>();

        // undo patches

        if (!p1)
            return false;

        try
        {
            PatchesMain.Patcher.Unpatch(readMethod, HarmonyPatchType.Prefix, PatchesMain.HarmonyId);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogWarning(Source, ex, "Unable to unpatch " + readEnum.Format() + ".ReadEnum(...) while cancelling initialization.");
        }

        if (!p2)
            return false;

        try
        {
            PatchesMain.Patcher.Unpatch(writeMethod, HarmonyPatchType.Prefix, PatchesMain.HarmonyId);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogWarning(Source, ex, "Unable to unpatch " + writeEnum.Format() + ".WriteEnum(...) while cancelling initialization.");
        }

        return false;
    }

    /// <summary>
    /// Reset the tracked bandwidth statistics.
    /// </summary>
    /// <param name="send"></param>
    public static void ResetByteTracking(bool? send = null)
    {
        if (send.HasValue)
        {
            Array.Clear(send.Value ? _outByteCt : _inByteCt, 0, BlockSize);
            if (send.Value)
                _outByteCtStTime = CachedTime.RealtimeSinceStartup;
            else
                _inByteCtStTime = CachedTime.RealtimeSinceStartup;
        }
        else
        {
            Array.Clear(_outByteCt, 0, BlockSize);
            Array.Clear(_inByteCt, 0, BlockSize);
            _inByteCtStTime = _outByteCtStTime = CachedTime.RealtimeSinceStartup;
        }
    }

    /// <summary>
    /// Gets the average data speed (either in or out, depending on the value of <paramref name="send"/>) in B/s per message type.
    /// </summary>
    public static float GetBytesPerSecondAvg(DevkitServerMessage message, bool send) => GetBytesTotal(message, send, out float timespan) / timespan;

    /// <summary>
    /// Gets the average data speed (either in or out, depending on the value of <paramref name="send"/>) in B/s per message type.
    /// </summary>
    public static float GetBytesPerSecondAvg(ICustomNetMessageListener listener, bool send) => GetBytesTotal(listener.LocalMessageIndex, send, out float timespan) / timespan;

    /// <summary>
    /// Gets the average data speed (either in or out, depending on the value of <paramref name="send"/>) in B/s total.
    /// </summary>
    public static float GetBytesPerSecondAvg(bool send) => GetBytesTotal(send, out float timespan) / timespan;

    /// <summary>
    /// Gets the total amount of data (either in or out, depending on the value of <paramref name="send"/>) in B per message type.
    /// </summary>
    /// <param name="timespan">The amount of time in seconds this data was tracked over.</param>
    public static long GetBytesTotal(DevkitServerMessage message, bool send, out float timespan)
    {
        timespan = CachedTime.RealtimeSinceStartup - (send ? _outByteCtStTime : _inByteCtStTime);
        return (int)message < BlockSize ? (send ? _outByteCt : _inByteCt)[(int)message] : 0;
    }

    /// <summary>
    /// Gets the total amount of data (either in or out, depending on the value of <paramref name="send"/>) in B per message type.
    /// </summary>
    /// <param name="timespan">The amount of time in seconds this data was tracked over.</param>
    public static long GetBytesTotal(ICustomNetMessageListener listener, bool send, out float timespan) => GetBytesTotal(listener.LocalMessageIndex, send, out timespan);

    /// <summary>
    /// Gets the total amount of data (either in or out, depending on the value of <paramref name="send"/>) in B.
    /// </summary>
    /// <param name="timespan">The amount of time in seconds this data was tracked over.</param>
    public static long GetBytesTotal(bool send, out float timespan)
    {
        long ttl = 0;
        timespan = CachedTime.RealtimeSinceStartup - (send ? _outByteCtStTime : _inByteCtStTime);
        for (int i = 0; i < BlockSize; ++i)
            ttl += (send ? _outByteCt : _inByteCt)[i];

        return ttl;
    }

    /// <summary>
    /// Increment the amount of bytes (either in or out, depending on the value of <paramref name="send"/>) by a listener.
    /// </summary>
    public static void IncrementByteCount(ICustomNetMessageListener listener, bool send, long length) => IncrementByteCount(listener.LocalMessageIndex, send, length);
    internal static void IncrementByteCount(DevkitServerMessage msg, bool send, long length)
    {
        if ((int)msg >= BlockSize)
            return;

        if (send)
            length += Mathf.CeilToInt(NewWriteBitCount / 8f) + sizeof(ushort);
        else
            length += Mathf.CeilToInt(NewReceiveBitCount / 8f) + sizeof(ushort);

        (send ? _outByteCt : _inByteCt)[(int)msg] += length;
    }
    internal static bool Init()
    {
        Writer.buffer = Block.buffer;
        Reflect(Assembly.GetExecutingAssembly(),
#if SERVER
            NetCallSource.FromClient
#else
            NetCallSource.FromServer
#endif
        );

        PullFromTransportConnectionListPool = null;
        try
        {
            MethodInfo? method = AccessorExtensions.AssemblyCSharp
                .GetType("SDG.Unturned.TransportConnectionListPool", true, false)?.GetMethod("Get",
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (method != null)
            {
                PullFromTransportConnectionListPool = (Func<PooledTransportConnectionList>)method.CreateDelegate(typeof(Func<PooledTransportConnectionList>));
            }
            else
            {
                Logger.DevkitServer.LogWarning(Source, "Couldn't find Get in TransportConnectionListPool, list pooling will not be used.");
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogWarning(Source, "Couldn't get Get from TransportConnectionListPool, list pooling will not be used (" + ex.Message + ").");
        }

        return true;
    }

    /// <summary>
    /// Get a <see cref="PooledTransportConnectionList"/> from the internal pool.
    /// </summary>
    /// <exception cref="AggregateException">Multiple reflection failures.</exception>
    /// <exception cref="Exception">Reflection failure.</exception>
    public static PooledTransportConnectionList GetPooledTransportConnectionList(int capacity = -1)
    {
        PooledTransportConnectionList? rtn = null;
        Exception? ex2 = null;
        if (PullFromTransportConnectionListPool != null)
        {
            try
            {
                rtn = PullFromTransportConnectionListPool();
            }
            catch (Exception ex)
            {
                ex2 = ex;
                Logger.DevkitServer.LogError(nameof(GetPooledTransportConnectionList), ex);
                PullFromTransportConnectionListPool = null;
            }
        }
        if (rtn == null)
        {
            if (capacity == -1)
                capacity = Provider.clients.Count;
            try
            {
                rtn = (PooledTransportConnectionList)Activator.CreateInstance(typeof(PooledTransportConnectionList),
                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null, new object[] { capacity }, CultureInfo.InvariantCulture, null);
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogError(nameof(GetPooledTransportConnectionList), ex);
                if (ex2 != null)
                    throw new AggregateException("Unable to create pooled transport connection!", ex2, ex);

                throw new Exception("Unable to create pooled transport connection!", ex);
            }
        }

        return rtn;
    }

    /// <summary>
    /// Get a <see cref="PooledTransportConnectionList"/> from the internal pool and add the provided transport connections to it.
    /// </summary>
    /// <exception cref="AggregateException">Multiple reflection failures.</exception>
    /// <exception cref="Exception">Reflection failure.</exception>
    public static PooledTransportConnectionList GetPooledTransportConnectionList(IEnumerable<ITransportConnection> connections, int capacity = -1)
    {
        PooledTransportConnectionList rtn = GetPooledTransportConnectionList(capacity);
        rtn.AddRange(connections);
        return rtn;
    }
    private static void ReceiveIdentity(
#if SERVER
        ITransportConnection transportConnection,
#endif
        NetPakReader reader)
    {
        Logger.DevkitServer.LogWarning(Source, "Received message with no listener"
#if SERVER
                                               + $" from {transportConnection.Format()}"
#endif
                                               + "."
        );
    }


    [UsedImplicitly]
    private static void ReceiveMessage(
#if SERVER
        ITransportConnection transportConnection,
#endif
        NetPakReader reader)
    {
        if (!reader.ReadUInt16(out ushort len))
        {
            Logger.DevkitServer.LogWarning(Source, "Received invalid message, can't read length"
#if SERVER
                                                   + $" from {transportConnection.Format()}"
#endif
                                                   + "."
                );
            return;
        }

        IncrementByteCount(DevkitServerMessage.InvokeNetCall, false, len + sizeof(ushort));

        if (!reader.ReadBytesPtr(len, out byte[] bytes, out int offset))
        {
            Logger.DevkitServer.LogWarning(Source, $"Received invalid message, can't read bytes of length {len.Format()} B"
#if SERVER
                                                   + $" from {transportConnection.Format()}"
#endif
                                                   + $". Expected length <= {reader.RemainingSegmentLength.Format()}."
                );
            return;
        }

        ArraySegment<byte> data = new ArraySegment<byte>(bytes, offset, len);
        if (len < MessageOverhead.MinimumSize)
        {
            Logger.DevkitServer.LogError(Source, $"Message too small to create overhead ({len.Format()} B).");
            return;
        }
        MessageOverhead ovh;
        try
        {
            ovh = new MessageOverhead(data);
        }
        catch (IndexOutOfRangeException)
        {
            Logger.DevkitServer.LogError(Source, $"Message too small to create overhead ({len.Format()} B).");
            return;
        }
#if SERVER
        OnReceived(data, transportConnection, in ovh, false);
#else
        OnReceived(data, GetPlayerTransportConnection(), in ovh, false);
#endif
    }

    [UsedImplicitly]
    private static bool ReadEnumPatch(NetPakReader reader,
#if SERVER
        ref EServerMessage value,
#else
        ref EClientMessage value,
#endif
        ref bool __result)
    {
#if CLIENT
        // singleplayer
        if (Provider.isServer)
        {
            Logger.DevkitServer.LogDebug(Source, "Incoming message for singleplayer. 1");
            return true;
        }
#endif

        if (!reader.ReadBits(OldReceiveBitCount, out uint num))
        {
            Logger.DevkitServer.LogDebug(Source, $"Failed to read old bit count, invalid message index: {reader.errors}. IsEditing: {DevkitServerModule.IsEditing}. 2");
            value = default;
            __result = false;
            return false;
        }

#if SERVER
        EServerMessage msg = (EServerMessage)num;
        if (msg == EServerMessage.GetWorkshopFiles)
        {
            value = msg;
            __result = true;
#if MESSAGE_ENUM_LOGGING
            Logger.DevkitServer.LogDebug(Source, $"Incoming message with type: {msg.Format()} (read with vanilla bit count: {OldReceiveBitCount}). 3");
#endif
            return false;
        }
#else
        EClientMessage msg = (EClientMessage)num;
        if (msg == EClientMessage.DownloadWorkshopFiles)
        {
            value = msg;
            __result = true;
#if MESSAGE_ENUM_LOGGING
            Logger.DevkitServer.LogDebug(Source, $"Incoming message with type: {msg.Format()} (read with vanilla bit count: {OldReceiveBitCount}). 3");
#endif
            return false;
        }
#endif
        int bitDiff = NewReceiveBitCount - OldReceiveBitCount;
        if (DevkitServerModule.IsEditing)
        {
            if (bitDiff != 0)
            {
                if (!reader.ReadBits(bitDiff, out uint restOfBits))
                {
                    Logger.DevkitServer.LogError(Source, $"Unable to read other {bitDiff} bits of a {DevkitServerModule.ColorizedModuleName} message. 4");
                    value = default;
                    __result = false;
                    return false;
                }
                num |= restOfBits << bitDiff;
#if SERVER
                msg = (EServerMessage)num;
#else
                msg = (EClientMessage)num;
#endif
            }

            if (num < ReceiveBlockOffset + BlockSize)
            {
                value = msg;
                __result = true;
#if MESSAGE_ENUM_LOGGING
                Logger.DevkitServer.LogDebug(Source, $"Incoming message with type: {
                    ((uint)value >= ReceiveBlockOffset
                        ? ((DevkitServerMessage)((uint)value - ReceiveBlockOffset)).ToString().Colorize(DevkitServerModule.ModuleColor)
                        : value.Format())
                } (read with {DevkitServerModule.ColorizedModuleName} bit count: {NewReceiveBitCount}). 5");
#endif
            }
            else
            {
                value = default;
                __result = false;
            }
            return false;
        }

        value = msg;
        __result = num < ReceiveBlockOffset;
#if MESSAGE_ENUM_LOGGING
        Logger.DevkitServer.LogDebug(Source, $"Incoming message with type: {value.Format()} (read with vanilla bit count: {OldReceiveBitCount}). 6");
#endif

        return false;
    }

    [UsedImplicitly]
    private static bool WriteEnumPatch(NetPakWriter writer,
#if SERVER
        EClientMessage value
#else
        EServerMessage value
#endif
        , ref bool __result)
    {
#if CLIENT
        // singleplayer
        if (Provider.isServer)
        {
            Logger.DevkitServer.LogDebug(Source, "Outgoing message for singleplayer. 7");
            return true;
        }
#endif
        
        uint v = (uint)value;
#if CLIENT
        if (value == EServerMessage.GetWorkshopFiles || !DevkitServerModule.IsEditing)
#elif SERVER
        if (value == EClientMessage.DownloadWorkshopFiles || !DevkitServerModule.IsEditing)
#endif
        {
            __result = writer.WriteBits(v, OldWriteBitCount);
#if MESSAGE_ENUM_LOGGING
            Logger.DevkitServer.LogDebug(Source, $"Outgoing message with type: {value.Format()} (written with vanilla bit count: {OldWriteBitCount}). 8");
#endif
            return false;
        }

        __result = writer.WriteBits(v, NewWriteBitCount);
#if MESSAGE_ENUM_LOGGING
        Logger.DevkitServer.LogDebug(Source, $"Outgoing message with type: {
            ((uint)value >= WriteBlockOffset
                ? ((DevkitServerMessage)((uint)value - WriteBlockOffset)).ToString().Colorize(DevkitServerModule.ModuleColor)
                : value.Format())
        } (written with {DevkitServerModule.ColorizedModuleName} bit count: {NewWriteBitCount}). 9");
#endif
        return false;
    }

    /// <summary>
    /// Gets the name of the invoker with the specified <see cref="DevkitServerNetCall"/> value.
    /// </summary>
    /// <returns>The field name of the invoker, or <paramref name="id"/> as a string as a fallback.</returns>
    /// <param name="hs">Look for high-speed invokers instead.</param>
    public static string GetInvokerName(ushort id, bool hs = false) =>
        (!hs ? Invokers : HighSpeedInvokers).TryGetValue(id, out BaseNetCall bnc) ? bnc.Name : id.ToString();

    /// <summary>
    /// Gets the name of the invoker with the specified <see cref="Guid"/> value.
    /// </summary>
    /// <returns>The field name of the invoker, or <paramref name="guid"/> as a string (formatted with "N") as a fallback.</returns>
    /// <param name="hs">Look for high-speed invokers instead.</param>
    public static string GetInvokerName(Guid guid, bool hs = false) =>
        (!hs ? GuidInvokers : GuidHighSpeedInvokers).TryGetValue(guid, out BaseNetCall bnc) ? bnc.Name : guid.ToString("N");

    /// <summary>
    /// Gets the invoker with the specified <see cref="DevkitServerNetCall"/> value.
    /// </summary>
    /// <param name="hs">Look for high-speed invokers instead.</param>
    public static T? GetInvoker<T>(ushort id, bool hs = false) where T : BaseNetCall =>
        (!hs ? Invokers : HighSpeedInvokers).TryGetValue(id, out BaseNetCall bnc) ? bnc as T : null;

    /// <summary>
    /// Gets the invoker with the specified <see cref="Guid"/> value.
    /// </summary>
    /// <param name="hs">Look for high-speed invokers instead.</param>
    public static T? GetInvoker<T>(Guid guid, bool hs = false) where T : BaseNetCall =>
        (!hs ? GuidInvokers : GuidHighSpeedInvokers).TryGetValue(guid, out BaseNetCall bnc) ? bnc as T : null;

    private static bool FillParameters(in MessageOverhead overhead, ref MessageContext ctx,
#if SERVER
        ITransportConnection
#else
        IClientTransport
#endif
        connection, BaseNetCall call, ArraySegment<byte> message, bool hs, ref object[]? parameters)
    {
        if (parameters != null)
            return true;
        if (!call.Read(message, out parameters))
        {
            Logger.DevkitServer.LogWarning(Source, $"Unable to read incoming message for message {overhead.Format()}.");
            return false;
        }

        ctx = new MessageContext(connection, overhead, hs);

        if (parameters is not { Length: > 0 })
            parameters = [ctx];
        else
            parameters[0] = ctx;

#if METHOD_LOGGING
        Logger.DevkitServer.LogDebug(Source, $"  Read net method: {overhead.Format()}: {string.Join(", ", parameters.Select(x => x.Format()))}.", ConsoleColor.DarkYellow);
#endif
        return true;
    }
    private static bool InvokeMethod(in MessageOverhead overhead, ref MessageContext ctx,
#if SERVER
        ITransportConnection
#else
        IClientTransport
#endif
            connection, NetMethodInfo methodInfo, BaseNetCall call, ArraySegment<byte> message, bool hs, object[]? parameters)
    {
        if (!FillParameters(in overhead, ref ctx, connection, call, message, hs, ref parameters))
            return false;

        if (DevkitServerModule.IsMainThread)
        {
            InvokeMethodIntl(in overhead, ref ctx, methodInfo, parameters!);
        }
        else
        {
            MessageContext context2 = ctx;
            DevkitServerUtility.QueueOnMainThread(() =>
            {
                MessageContext context = context2;
                InvokeMethodIntl(in context.Overhead, ref context, methodInfo, parameters!);
            });
        }

        return true;
    }
    private static void InvokeMethodIntl(in MessageOverhead overhead, ref MessageContext ctx, NetMethodInfo methodInfo, object[] parameters)
    {
        object? res;
        try
        {
            res = methodInfo.Method.Invoke(null, parameters);
        }
        catch (Exception ex)
        {
            PrintInvokeError(in overhead, methodInfo.Method, ex);
            return;
        }
        if (res is int resp)
        {
            ctx.Acknowledge(resp);
        }
        else if (res is StandardErrorCode resp1)
        {
            ctx.Acknowledge(resp1);
        }
        else if (res is Task task)
        {
            if (!task.IsCompleted)
            {
                MessageContext context = ctx;
                Task.Run(async () =>
                {
                    try
                    {
                        await task.ConfigureAwait(false);
                        if (task is Task<int> ti)
                        {
                            context.Acknowledge(ti.Result);
                        }
                        else if (task is Task<StandardErrorCode> ti2)
                        {
                            context.Acknowledge(ti2.Result);
                        }
                    }
                    catch (Exception ex)
                    {
                        PrintInvokeError(in context.Overhead, methodInfo.Method, ex);
                    }
                });
            }
            else if (task is Task<int> ti)
            {
                ctx.Acknowledge(ti.Result);
            }
            else if (task is Task<StandardErrorCode> ti2)
            {
                ctx.Acknowledge(ti2.Result);
            }
        }
        else if (res is UniTask<int> uniTask)
        {
            if (uniTask.Status == UniTaskStatus.Pending)
            {
                MessageContext context = ctx;
                UniTask.Create(async () =>
                {
                    try
                    {
                        int errCode = await uniTask;
                        context.Acknowledge(errCode);
                    }
                    catch (Exception ex)
                    {
                        PrintInvokeError(in context.Overhead, methodInfo.Method, ex);
                    }
                });
            }
            else
            {
                ctx.Acknowledge(uniTask.GetAwaiter().GetResult());
            }
        }
        else if (res is UniTask<StandardErrorCode> uniTask2)
        {
            if (uniTask2.Status == UniTaskStatus.Pending)
            {
                MessageContext context = ctx;
                UniTask.Create(async () =>
                {
                    try
                    {
                        StandardErrorCode errCode = await uniTask2;
                        context.Acknowledge(errCode);
                    }
                    catch (Exception ex)
                    {
                        PrintInvokeError(in context.Overhead, methodInfo.Method, ex);
                    }
                });
            }
            else
            {
                ctx.Acknowledge(uniTask2.GetAwaiter().GetResult());
            }
        }
        else if (res is UniTask { Status: UniTaskStatus.Pending } uniTask3)
        {
            MessageContext context = ctx;
            UniTask.Create(async () =>
            {
                try
                {
                    await uniTask3;
                }
                catch (Exception ex)
                {
                    PrintInvokeError(in context.Overhead, methodInfo.Method, ex);
                }
            });
        }
#if METHOD_LOGGING
        Logger.DevkitServer.LogDebug(Source, $"  Invoked net method: {methodInfo.Method.Format()}.", ConsoleColor.DarkYellow);
#endif
    }
    private static void PrintInvokeError(in MessageOverhead overhead, MethodBase method, Exception ex)
    {
        if (ex is TargetInvocationException { InnerException: { } t })
            ex = t;
        Logger.DevkitServer.LogError(Source, $"Error running method {method.Format()}.");
        Logger.DevkitServer.LogError(Source, ex, $"Message: {overhead.Format()}.");
    }

    private static readonly List<(NetTask, MessageContext, int?, object[]?)> _mainThreadPendingList = new List<(NetTask, MessageContext, int?, object[]?)>(8);
    private static void ParseMessage(in MessageOverhead overhead,
#if SERVER
        ITransportConnection
#else
        IClientTransport
#endif
            connection, BaseNetCall call, ArraySegment<byte> message, int size, bool hs)
    {
        bool verified = !hs || connection is not HighSpeedConnection hsc || hsc.Verified;
        object[]? parameters = null;
        MessageContext ctx = default;
        List<(NetTask, MessageContext, int?, object[]?)> toInvoke = DevkitServerModule.IsMainThread ? _mainThreadPendingList : new List<(NetTask, MessageContext, int?, object[]?)>(2);
        bool invokedAny;
        try
        {
            if (overhead.RequestKey != default && verified)
            {
                if ((overhead.Flags & MessageFlags.RequestResponse) == MessageFlags.RequestResponse)
                {
                    long rk = overhead.RequestKey;
                    if (LocalListeners.TryGetValue(rk, out Listener listener) && listener.Caller.Equals(call))
                    {
                        if (listener.Task is { IsCompleted: false })
                        {
                            if (!FillParameters(in overhead, ref ctx, connection, call, message, hs, ref parameters))
                            {
#if METHOD_LOGGING
                                Logger.DevkitServer.LogDebug(Source, $"  Failed to read method: {overhead.Format()}.", ConsoleColor.DarkYellow);
#endif
                                return;
                            }

#if METHOD_LOGGING
                            Logger.DevkitServer.LogDebug(Source, "  Satisfied local listener.", ConsoleColor.DarkYellow);
#endif
                            toInvoke.Add((listener.Task, default, null, parameters));
                        }

                        LocalListeners.Remove(rk, out _);
                    }
                }
                else if ((overhead.Flags & MessageFlags.AcknowledgeResponse) == MessageFlags.AcknowledgeResponse)
                {
                    long rk = overhead.RequestKey;
                    if (LocalAckRequests.TryGetValue(rk, out Listener listener) && listener.Caller.Equals(call))
                    {
                        if (listener.Task is { IsCompleted: false })
                        {
                            int? errorCode = null;
                            if (overhead.Size == sizeof(int))
                            {
                                if (size >= sizeof(int))
                                    errorCode = BitConverter.ToInt32(message.Array!, message.Offset);
                            }

#if METHOD_LOGGING
                            Logger.DevkitServer.LogDebug(Source, $"  Satisfied local acknowledgement listener {(errorCode.HasValue ? errorCode.Value.Format() : ((object?)null).Format())}.", ConsoleColor.DarkYellow);
#endif
                            
                            toInvoke.Add((listener.Task, new MessageContext(connection, overhead, hs), errorCode, null));
                        }

                        LocalAckRequests.Remove(rk, out _);
                    }
                }
            }

            invokedAny = toInvoke.Count > 0;
            if (!invokedAny || (overhead.Flags & MessageFlags.RunOriginalMethodOnRequest) != 0)
            {
                NetMethodInfo[]? methods;
                if ((overhead.Flags & MessageFlags.Guid) != 0)
                    GuidMethods.TryGetValue(overhead.MessageGuid, out methods);
                else
                    Methods.TryGetValue(overhead.MessageId, out methods);

                if (methods == null)
                    return;

                for (int i = 0; i < methods.Length; ++i)
                {
                    NetMethodInfo netMethodInfo = methods[i];
                    if (netMethodInfo.Method is null || netMethodInfo.Attribute.HighSpeed != hs)
                        continue;

                    if (InvokeMethod(in overhead, ref ctx, connection, netMethodInfo, call, message, hs, parameters))
                    {
                        invokedAny = true;
                        continue;
                    }
#if METHOD_LOGGING
                    Logger.DevkitServer.LogDebug(Source, $"  Failed to read method: {overhead.Format()}.", ConsoleColor.DarkYellow);
#endif
                    return;
                }
            }
            else invokedAny = true;

            for (int i = 0; i < toInvoke.Count; ++i)
            {
                (NetTask netTask, MessageContext ctx2, int? errCode, object[]? parameters2) = toInvoke[i];
                if (parameters2 == null)
                    netTask.TellCompleted(in ctx2, true, errCode);
                else
                    netTask.TellCompleted(parameters2, true);
            }
        }
        finally
        {
            toInvoke.Clear();
        }

        if (!invokedAny)
        {
            Logger.DevkitServer.LogWarning(Source, $"Could not find invoker for message from {connection.Format()}: {overhead.Format()}.");
        }
    }
    internal static void OnReceived(ArraySegment<byte> message,
#if SERVER
        ITransportConnection
#else
        IClientTransport
#endif
            connection, in MessageOverhead overhead, bool hs)
    {
        int size = overhead.Size;
        if (size > message.Count - overhead.Length)
        {
            Logger.DevkitServer.LogError(Source, $"Message overhead read a size larger than the message payload: {size.Format()} B.");
            goto rtn;
        }
#if METHOD_LOGGING || PRINT_DATA
#if SERVER
        string log = $"Received message from {connection.Format()}: {overhead.Format()}.";
#elif CLIENT
        string log = $"Received message: {overhead.Format()}.";
#endif

#if METHOD_LOGGING
        if (message.Array!.Length - message.Offset > overhead.Length)
        {
            log += Environment.NewLine;
            log += FormattingUtil.FormatBinary(message.Array, 16, message.Offset, overhead.Length, "000", true);
        }

        if (size > 0)
        {
            log += ByteFormatter.FormatBinary(message.AsSpan().Slice(overhead.Length, size),
                ByteStringFormat.NewLineAtBeginning | ByteStringFormat.RowLabels | ByteStringFormat.ColumnLabels | ByteStringFormat.Columns32 | ByteStringFormat.Base16
                | ByteStringFormat.First128 | ByteStringFormat.Last128);
        }
#endif
        Logger.DevkitServer.LogDebug(Source, log);
#endif
        int index = overhead.Length;

        if ((overhead.Flags & MessageFlags.Guid) != 0
                ? (hs ? GuidHighSpeedInvokers : GuidInvokers).TryGetValue(overhead.MessageGuid, out BaseNetCall call)
                : (hs ? HighSpeedInvokers : Invokers).TryGetValue(overhead.MessageId, out call))
        {
            ParseMessage(in overhead, connection, call, new ArraySegment<byte>(message.Array!, message.Offset + index, message.Count - index), size, hs);
        }
        else
        {
            Logger.DevkitServer.LogWarning(Source, $"Could not find invoker for message from {connection.Format()}: {overhead.Format()}.");
        }

        rtn:
        RemoveExpiredListeners();
    }
    private static void RemoveExpiredListeners()
    {
        DateTime now = DateTime.UtcNow;
        lock (LocalListenerTimestamps)
        {
            for (int i = LocalListenerTimestamps.Count - 1; i >= 0; --i)
            {
                if ((now - LocalListenerTimestamps[i].Timestamp).TotalSeconds <= MaxListenTimeout)
                    continue;

                ListenerTimestamp ts = LocalListenerTimestamps[i];
                LocalListenerTimestamps.RemoveAt(i);
                (ts.IsAcknowledgeRequest ? LocalAckRequests : LocalListeners).Remove(ts.RequestId, out _);
                Logger.DevkitServer.LogDebug(Source, $"{(ts.IsAcknowledgeRequest ? "Acknowledge request" : "Listener")} expired: {ts.RequestId.Format()}.");
            }
        }
    }

    /// <summary>
    /// Add an assembly's <see cref="NetCall"/>s and net methods to the registry.
    /// </summary>
    /// <remarks>This is done automatically for plugins.</remarks>
    /// <param name="search">Serverside/Clientside search priority.</param>
    /// <param name="outMethods">Optional output list for found net methods.</param>
    /// <param name="outCalls">Optional output list for found <see cref="NetCall"/>s</param>
    public static void Reflect(Assembly assembly, NetCallSource search, IList<NetMethodInfo>? outMethods = null, IList<NetInvokerInfo>? outCalls = null)
    {
        List<Type> types = Accessor.GetTypesSafe(assembly, removeIgnored: false);
        int before1 = Methods.Count;
        int before2 = Invokers.Count;
        ReflectMethods(types, search, outMethods);
        ReflectInvokers(types, outCalls, outMethods);
        Logger.DevkitServer.LogInfo("NET REFLECT", assembly.GetName().Name.Format(false) + " registered " + (Methods.Count - before1).Format() + " net-methods and " + (Invokers.Count - before2).Format() + " invokers.");
    }
    private static void ReflectMethods(List<Type> types, NetCallSource search, IList<NetMethodInfo>? outMethods = null)
    {
        for (int i = 0; i < types.Count; ++i)
        {
            MethodInfo[] methods;
            try
            {
               methods = types[i].GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            }
            catch (SystemException ex)
            {
                methods = Array.Empty<MethodInfo>();
                Logger.DevkitServer.LogWarning(Source, ex, $"Unable to load methods of type {types[i].Format()}.");
            }

            for (int m = 0; m < methods.Length; ++m)
            {
                MethodInfo method = methods[m];

                if (method.IsIgnored() ||
                    !method.TryGetAttributeSafe(out NetCallAttribute attribute) ||
                    attribute.Type == NetCallSource.None ||
                    !(attribute.Type == NetCallSource.FromEither || search == NetCallSource.FromEither || search == attribute.Type))
                    continue;

                NetMethodInfo kvp = new NetMethodInfo(method, attribute);
                if (!string.IsNullOrEmpty(attribute.GuidString) && Guid.TryParse(attribute.GuidString, out Guid guid))
                {
                    if (GuidMethods.TryGetValue(guid, out NetMethodInfo[] existingMethods))
                    {
                        NetMethodInfo[] newArr = new NetMethodInfo[existingMethods.Length + 1];
                        for (int j = 0; j < existingMethods.Length; ++j)
                            newArr[j] = existingMethods[j];
                        newArr[existingMethods.Length] = kvp;
                        GuidMethods[guid] = newArr;
                    }
                    else
                    {
                        GuidMethods[guid] = [ kvp ];
                    }
#if REFLECTION_LOGGING
                    Logger.DevkitServer.LogDebug("NET REFLECT", $"Registered net method: {method.Format()} to {guid.Format("N")}.", ConsoleColor.DarkYellow);
#endif
                }
                else if (attribute.MethodID != 0)
                {
                    if (Methods.TryGetValue(attribute.MethodID, out NetMethodInfo[] existingMethods))
                    {
                        NetMethodInfo[] newArr = new NetMethodInfo[existingMethods.Length + 1];
                        for (int j = 0; j < existingMethods.Length; ++j)
                            newArr[j] = existingMethods[j];
                        newArr[existingMethods.Length] = kvp;
                        Methods[attribute.MethodID] = newArr;
                    }
                    else
                    {
                        Methods[attribute.MethodID] = [ kvp ];
                    }
#if REFLECTION_LOGGING
                    Logger.DevkitServer.LogDebug("NET REFLECT", $"Registered net method: {method.Format()} to {attribute.MethodID.Format()}.", ConsoleColor.DarkYellow);
#endif
                }
                else continue;
                outMethods?.Add(kvp);
            }
        }
    }
    private static void ReflectInvokers(IReadOnlyList<Type> types, IList<NetInvokerInfo>? outCalls = null, IList<NetMethodInfo>? outMethods = null)
    {
        Type ctx = typeof(MessageContext);
        Assembly dka = Assembly.GetExecutingAssembly();
        for (int i = 0; i < types.Count; ++i)
        {
            FieldInfo[] fields;
            try
            {
                fields = types[i].GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            }
            catch (SystemException ex)
            {
                fields = Array.Empty<FieldInfo>();
                Logger.DevkitServer.LogWarning(Source, ex, $"Unable to load fields of type {types[i].Format()}.");
            }

            for (int f = 0; f < fields.Length; ++f)
            {
                if (!fields[f].IsStatic || fields[f].IsIgnored() || !fields[f].FieldType.IsSubclassOf(typeof(BaseNetCall)))
                    continue;

                FieldInfo field = fields[f];
                if (field.GetValue(null) is not BaseNetCall call)
                    continue;
                Type callType = call.GetType();
                bool core = false;
                IDevkitServerPlugin? plugin = null;
                if (field.DeclaringType?.Assembly == dka)
                    core = true;
                else
                    plugin = PluginLoader.FindPluginForMember(field);
                if (!core && plugin == null)
                {
                    Logger.DevkitServer.LogWarning("NET REFLECT", $"Unable to link {field.Format()} invoker to a plugin. Use the {typeof(PluginIdentifierAttribute).Format()} on the field to link an invoker to a " +
                                                                  "plugin when multiple plugins are loaded from an assembly.");
                    continue;
                }

                call.Plugin = plugin;
                if (call.Id != default)
                {
                    if (!call.HighSpeed && Invokers.TryGetValue(call.Id, out BaseNetCall c2) || call.HighSpeed && HighSpeedInvokers.TryGetValue(call.Id, out c2))
                    {
                        if (c2.GetType() != call.GetType())
                            Logger.DevkitServer.LogWarning("NET REFLECT", $"Inconsistant duplicate invoker {call.Id.Format()}{(call.HighSpeed ? " (HS)" : string.Empty)} at field {field.Format()}.");
                        continue;
                    }
                }
                else if (call.Guid != default)
                {
                    if (!call.HighSpeed && GuidInvokers.TryGetValue(call.Guid, out BaseNetCall c2) || call.HighSpeed && GuidHighSpeedInvokers.TryGetValue(call.Guid, out c2))
                    {
                        if (c2.GetType() != call.GetType())
                            Logger.DevkitServer.LogWarning("NET REFLECT", $"Inconsistant duplicate invoker {call.Guid.Format()}{(call.HighSpeed ? " (HS)" : string.Empty)} at field {field.Format()}.");
                        continue;
                    }
                }
                else
                {
                    Logger.DevkitServer.LogWarning("NET REFLECT", $"Invoker {field.Format()} does not have a unique ID or GUID.");
                    continue;
                }
                Type[] generics = callType.GetGenericArguments();
                List<NetMethodInfo>? outList = outCalls == null ? null : new List<NetMethodInfo>(1);

                NetMethodInfo[]? infos;
                bool guid = false;
                if (call.Guid != Guid.Empty)
                {
                    guid = true;
                    GuidMethods.TryGetValue(call.Guid, out infos);
                }
                else if (call.Id != 0)
                {
                    Methods.TryGetValue(call.Id, out infos);
                }
                else infos = null;

                if (infos != null)
                {
                    for (int index = infos.Length - 1; index >= 0; --index)
                    {
                        NetMethodInfo info = infos[index];
                        if (info.Attribute.HighSpeed != call.HighSpeed)
                            continue;

                        ParameterInfo[] parameters = info.Method.GetParameters();
                        if (callType == typeof(NetCall))
                        {
                            if (parameters.Length != 1 ||
                                !parameters[0].ParameterType.IsAssignableFrom(ctx) ||
                                !info.Method.IsStatic)
                            {
                                Logger.DevkitServer.LogWarning("NET REFLECT", $"Method {info.Method.Format()} has the wrong signature for invoker {field.Format()}. {Environment.NewLine}" +
                                                                              $"Expected signature: {FormattingUtil.FormatMethod(typeof(void), info.Method.DeclaringType, info.Method.Name,
                                                                                  [ (ctx, "ctx") ], isStatic: true)}.");

                                DevkitServerUtility.RemoveFromArray(ref infos, index);
                                if (guid)
                                    GuidMethods[call.Guid] = infos;
                                else
                                    Methods[call.Id] = infos;

                                outMethods?.RemoveAll(x => x.Method == info.Method);
                            }
                        }
                        else if (typeof(NetCallCustom).IsAssignableFrom(callType))
                        {
                            if (parameters.Length != 2 ||
                                !parameters[0].ParameterType.IsAssignableFrom(ctx) ||
                                !parameters[1].ParameterType.IsAssignableFrom(typeof(ByteReader)) ||
                                !info.Method.IsStatic)
                            {
                                Logger.DevkitServer.LogWarning("NET REFLECT", $"Method {info.Method.Format()} has the wrong signature for invoker {field.Format()}. {Environment.NewLine}" +
                                                                              $"Expected signature: {FormattingUtil.FormatMethod(typeof(void), info.Method.DeclaringType,
                                                                                  info.Method.Name, [ (ctx, "ctx"), (typeof(ByteReader), "reader") ],
                                                                                  isStatic: true)}.");

                                DevkitServerUtility.RemoveFromArray(ref infos, index);
                                if (guid)
                                    GuidMethods[call.Guid] = infos;
                                else
                                    Methods[call.Id] = infos;

                                outMethods?.RemoveAll(x => x.Method == info.Method);
                            }
                        }
                        else if (callType.IsSubclassOf(typeof(NetCallRaw)) || callType.IsSubclassOf(typeof(DynamicNetCall)))
                        {
                            bool pass = true;
                            if (parameters.Length - 1 == generics.Length)
                            {
                                for (int t = 0; t < generics.Length; t++)
                                {
                                    if (!generics[t].IsAssignableFrom(parameters[t + 1].ParameterType))
                                    {
                                        pass = false;
                                        break;
                                    }
                                }
                            }
                            else pass = false;
                            if (!pass)
                            {
                                (Type type, string? name)[] parameters2 = new (Type type, string? name)[generics.Length + 1];
                                parameters2[0] = (ctx, "ctx");
                                for (int k = 0; k < generics.Length; ++k)
                                {
                                    parameters2[k + 1] = (generics[k], "arg" + k);
                                }
                                Logger.DevkitServer.LogWarning("NET REFLECT", $"Method {info.Method.Format()} has the wrong signature for invoker {field.Format()}. {Environment.NewLine}" +
                                                                              $"Expected signature: {FormattingUtil.FormatMethod(typeof(void),
                                                                                  info.Method.DeclaringType, info.Method.Name, parameters2, isStatic: true)}.");

                                DevkitServerUtility.RemoveFromArray(ref infos, index);
                                if (guid)
                                    GuidMethods[call.Guid] = infos;
                                else
                                    Methods[call.Id] = infos;

                                outMethods?.RemoveAll(x => x.Method == info.Method);
                            }
                        }

                        outList?.Add(info);
#if REFLECTION_LOGGING
                        Logger.DevkitServer.LogDebug("NET REFLECT", $"Registered net call: {field.Format()} to {(call.Guid != Guid.Empty ? call.Guid.Format("N") : call.Id.Format())}.", ConsoleColor.DarkYellow);
#endif
                    }
                }

                call.Name = (core ? PermissionLeaf.DevkitServerModulePrefix : plugin!.PermissionPrefix) + "." + field.Name;
                call.SetThrowOnError(true);
                if (call.Id != default)
                    (call.HighSpeed ? HighSpeedInvokers : Invokers)[call.Id] = call;
                else
                    (call.HighSpeed ? GuidHighSpeedInvokers : GuidInvokers)[call.Guid] = call;

                outCalls?.Add(new NetInvokerInfo(field, call, outList?.ToArray() ?? Array.Empty<NetMethodInfo>()));
            }
        }
    }

    /// <summary>
    /// Starts waiting for a <see cref="NetTask"/> response or acknowledge response.
    /// </summary>
    public static void RegisterListener(NetTask netTask, BaseNetCall caller)
    {
        Listener listener = new Listener(netTask, caller);
        (netTask.IsAcknowledgementRequest ? LocalAckRequests : LocalListeners)[netTask.RequestId] = listener;
        lock (LocalListenerTimestamps)
            LocalListenerTimestamps.Add(new ListenerTimestamp(netTask));
    }

    /// <summary>
    /// Stops waiting for a <see cref="NetTask"/> response or acknowledge response.
    /// </summary>
    public static void RemoveListener(NetTask task)
    {
        if (task.IsAcknowledgementRequest)
        {
            LocalAckRequests.Remove(task.RequestId, out _);
        }
        else
        {
            LocalListeners.Remove(task.RequestId, out _);
        }

        lock (LocalListenerTimestamps)
        {
            for (int i = LocalListenerTimestamps.Count - 1; i >= 0; --i)
            {
                if (LocalListenerTimestamps[i].RequestId != task.RequestId)
                    continue;

                LocalListenerTimestamps.RemoveAt(i);
                break;
            }
        }
    }

    /// <summary>
    /// Send data of message type <see cref="DevkitServerMessage"/>. Writes the message type, length (unsigned int16), and the byte data using a <see cref="NetPakWriter"/>.
    /// </summary>
    /// <exception cref="ArgumentException">Length is too long (must be at most <see cref="ushort.MaxValue"/>) or a <see cref="HighSpeedConnection"/> is used with a <see cref="ICustomNetMessageListener"/>.</exception>
    public static void SendGeneric(DevkitServerMessage message, ArraySegment<byte> bytes,
#if SERVER
        ITransportConnection connection,
#endif
        bool reliable = true)
    {
        DevkitServerModule.AssertIsDevkitServerClient();

#if SERVER
        if ((int)message >= DevkitServerBlockSize && connection is HighSpeedConnection)
            throw new ArgumentException("Not allowed to use HighSpeedConnections with ICustomNetMessageListeners.", nameof(connection));
#endif
        
        if (bytes.Count > ushort.MaxValue)
            throw new ArgumentException($"Length must be less than {ushort.MaxValue} B.", nameof(bytes));

        DevkitServerMessage mapping = message;
        if ((int)mapping >= DevkitServerBlockSize && CustomNetMessageListeners.InverseLocalMappings.TryGetValue(mapping, out Type localMappingType))
        {
#if CLIENT
            if (CustomNetMessageListeners.RemoteMappings.TryGetValue(localMappingType, out DevkitServerMessage remoteMapping))
                mapping = remoteMapping;
#elif SERVER
            if (CustomNetMessageListeners.RemoteMappings.TryGetValue(connection, out ConcurrentDictionary<Type, DevkitServerMessage> mappings)
                && mappings.TryGetValue(localMappingType, out DevkitServerMessage remoteMapping))
            {
                mapping = remoteMapping;
            }
#endif
        }

        NetPakWriter writer = DevkitServerModule.IsMainThread ? Writer : new NetPakWriter { buffer = new byte[bytes.Count + 8] };
        writer.Reset();

#if CLIENT
        writer.WriteEnum((EServerMessage)(WriteBlockOffset + (int)mapping));
#else
        writer.WriteEnum((EClientMessage)(WriteBlockOffset + (int)mapping));
#endif
        writer.WriteUInt16((ushort)bytes.Count);
        writer.WriteBytes(bytes.Array, bytes.Offset, bytes.Count);
        writer.Flush();
        IncrementByteCount(message, true, bytes.Count);
#if CLIENT
        GetPlayerTransportConnection().Send(writer.buffer, writer.writeByteIndex, reliable ? ENetReliability.Reliable : ENetReliability.Unreliable);
#else
        connection.Send(writer.buffer, writer.writeByteIndex, reliable ? ENetReliability.Reliable : ENetReliability.Unreliable);
#endif
    }

    /// <summary>
    /// Send data of message type <see cref="DevkitServerMessage"/>. Writes the message type and the byte data using a <see cref="NetPakWriter"/>.
    /// </summary>
    /// <exception cref="ArgumentException">A <see cref="HighSpeedConnection"/> is used with a <see cref="ICustomNetMessageListener"/>.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="writeAction"/> is <see langword="null"/>.</exception>
    public static void SendGeneric(DevkitServerMessage message, Action<NetPakWriter> writeAction,
#if SERVER
        ITransportConnection connection,
#endif
        bool reliable = true)
    {
        DevkitServerModule.AssertIsDevkitServerClient();

#if SERVER
        if ((int)message >= DevkitServerBlockSize && connection is HighSpeedConnection)
            throw new ArgumentException("Not allowed to use HighSpeedConnections with ICustomNetMessageListeners.", nameof(connection));
#endif
        
        if (writeAction == null)
            throw new ArgumentNullException(nameof(writeAction));

        DevkitServerMessage mapping = message;
        if ((int)mapping >= DevkitServerBlockSize && CustomNetMessageListeners.InverseLocalMappings.TryGetValue(mapping, out Type localMappingType))
        {
#if CLIENT
            if (CustomNetMessageListeners.RemoteMappings.TryGetValue(localMappingType, out DevkitServerMessage remoteMapping))
                mapping = remoteMapping;
#elif SERVER
            if (CustomNetMessageListeners.RemoteMappings.TryGetValue(connection, out ConcurrentDictionary<Type, DevkitServerMessage> mappings)
                && mappings.TryGetValue(localMappingType, out DevkitServerMessage remoteMapping))
            {
                mapping = remoteMapping;
            }
#endif
        }

        NetPakWriter writer = DevkitServerModule.IsMainThread ? Writer : new NetPakWriter { buffer = new byte[128] };
        writer.Reset();

#if CLIENT
        writer.WriteEnum((EServerMessage)(WriteBlockOffset + (int)mapping));
#else
        writer.WriteEnum((EClientMessage)(WriteBlockOffset + (int)mapping));
#endif
        int pos = writer.writeByteIndex;

        writeAction(writer);
        writer.AlignToByte();

        pos = writer.writeByteIndex - pos;
        writer.Flush();
        IncrementByteCount(message, true, pos);
#if CLIENT
        GetPlayerTransportConnection().Send(writer.buffer, writer.writeByteIndex, reliable ? ENetReliability.Reliable : ENetReliability.Unreliable);
#else
        connection.Send(writer.buffer, writer.writeByteIndex, reliable ? ENetReliability.Reliable : ENetReliability.Unreliable);
#endif
    }
#if SERVER

    /// <summary>
    /// Send data of message type <see cref="DevkitServerMessage"/> to multiple users. Writes the message type, length (unsigned int16), and the byte data using a <see cref="NetPakWriter"/>.
    /// </summary>
    /// <param name="connections">Leave <see langword="null"/> to select all online users.</param>
    /// <exception cref="ArgumentException">Length is too long (must be at most <see cref="ushort.MaxValue"/>) or a <see cref="HighSpeedConnection"/> is used with a <see cref="ICustomNetMessageListener"/>.</exception>
    public static void SendGeneric(DevkitServerMessage message, ArraySegment<byte> bytes, IReadOnlyList<ITransportConnection>? connections = null, bool reliable = true)
    {
        ThreadUtil.assertIsGameThread();

        DevkitServerModule.AssertIsDevkitServerClient();

        if (bytes.Count > ushort.MaxValue)
            throw new ArgumentException($"Length must be less than {ushort.MaxValue} B.", nameof(bytes));

        if (connections != null && (int)message >= DevkitServerBlockSize)
        {
            for (int i = 0; i < connections.Count; ++i)
            {
                if (connections[i] is HighSpeedConnection)
                    throw new ArgumentException("Not allowed to use HighSpeedConnections with ICustomNetMessageListeners.", nameof(connections));
            }
        }

        connections ??= Provider.GatherRemoteClientConnections();
        if (connections.Count == 0)
            return;
        bool anyConnectionsHaveAlteredIndexes = false;
        Type? localMappingType = null;
        if ((int)message >= DevkitServerBlockSize && CustomNetMessageListeners.InverseLocalMappings.TryGetValue(message, out localMappingType))
        {
            for (int i = 0; i < connections.Count; ++i)
            {
                if (!CustomNetMessageListeners.RemoteMappings.TryGetValue(connections[i], out ConcurrentDictionary<Type, DevkitServerMessage> mappings)
                    || !mappings.TryGetValue(localMappingType, out DevkitServerMessage remoteMessage)
                    || remoteMessage == message)
                {
                    continue;
                }

                anyConnectionsHaveAlteredIndexes = true;
                break;
            }
        }
        NetPakWriter writer = DevkitServerModule.IsMainThread ? Writer : new NetPakWriter { buffer = new byte[bytes.Count + 8] };
        writer.Reset();
        int enumIndex = writer.writeByteIndex;
#if CLIENT
        writer.WriteEnum((EServerMessage)(WriteBlockOffset + (int)message));
#else
        writer.WriteEnum((EClientMessage)(WriteBlockOffset + (int)message));
#endif
        writer.WriteUInt16((ushort)bytes.Count);
        writer.WriteBytes(bytes.Array, bytes.Offset, bytes.Count);
        writer.Flush();
        ENetReliability reliability = reliable ? ENetReliability.Reliable : ENetReliability.Unreliable;
        IncrementByteCount(message, true, bytes.Count * connections.Count);

        if (!anyConnectionsHaveAlteredIndexes)
        {
            for (int i = 0; i < connections.Count; ++i)
                connections[i].Send(writer.buffer, writer.writeByteIndex, reliability);
        }
        else
        {
            // apply mappings
            int oldIndex = writer.writeByteIndex;
            DevkitServerMessage last = message;
            for (int i = 0; i < connections.Count; ++i)
            {
                ITransportConnection connection = connections[i];

                if (!CustomNetMessageListeners.RemoteMappings.TryGetValue(connection, out ConcurrentDictionary<Type, DevkitServerMessage> mappings)
                    || !mappings.TryGetValue(localMappingType!, out DevkitServerMessage remoteMessage)
                    || remoteMessage == message)
                {
                    remoteMessage = message;
                }

                if (remoteMessage != last)
                {
                    last = remoteMessage;

                    writer.writeByteIndex = enumIndex;
#if CLIENT
                    writer.WriteEnum((EServerMessage)(WriteBlockOffset + (int)remoteMessage));
#else
                    writer.WriteEnum((EClientMessage)(WriteBlockOffset + (int)remoteMessage));
#endif
                    writer.WriteUInt16((ushort)bytes.Count); // this has to be rewritten because the 32 bit scratch could intersect with the enum written above.
                    writer.AlignToByte();
                    writer.Flush();
                    writer.writeByteIndex = oldIndex;
                }

                connection.Send(writer.buffer, oldIndex, reliability);
            }
        }
    }

    /// <summary>
    /// Send data of message type <see cref="DevkitServerMessage"/> to multiple users. Writes the message type and the byte data using a <see cref="NetPakWriter"/>.
    /// </summary>
    /// <param name="connections">Leave <see langword="null"/> to select all online users.</param>
    /// <exception cref="ArgumentException">Length is too long (must be at most <see cref="ushort.MaxValue"/>) or a <see cref="HighSpeedConnection"/> is used with a <see cref="ICustomNetMessageListener"/>.</exception>
    public static void SendGeneric(DevkitServerMessage message, Action<NetPakWriter> writeAction, IReadOnlyList<ITransportConnection>? connections = null, bool reliable = true)
    {
        ThreadUtil.assertIsGameThread();

        DevkitServerModule.AssertIsDevkitServerClient();

        if (writeAction == null)
            throw new ArgumentNullException(nameof(writeAction));

        if (connections != null && (int)message >= DevkitServerBlockSize)
        {
            for (int i = 0; i < connections.Count; ++i)
            {
                if (connections[i] is HighSpeedConnection)
                    throw new ArgumentException("Not allowed to use HighSpeedConnections with ICustomNetMessageListeners.", nameof(connections));
            }
        }

        connections ??= Provider.GatherRemoteClientConnections();
        if (connections.Count == 0)
            return;
        bool anyConnectionsHaveAlteredIndexes = false;
        Type? localMappingType = null;
        if ((int)message >= DevkitServerBlockSize && CustomNetMessageListeners.InverseLocalMappings.TryGetValue(message, out localMappingType))
        {
            for (int i = 0; i < connections.Count; ++i)
            {
                if (!CustomNetMessageListeners.RemoteMappings.TryGetValue(connections[i], out ConcurrentDictionary<Type, DevkitServerMessage> mappings)
                    || !mappings.TryGetValue(localMappingType, out DevkitServerMessage remoteMessage)
                    || remoteMessage == message)
                {
                    continue;
                }

                anyConnectionsHaveAlteredIndexes = true;
                break;
            }
        }
        NetPakWriter writer = DevkitServerModule.IsMainThread ? Writer : new NetPakWriter { buffer = new byte[128] };
        writer.Reset();
        int enumIndex = writer.writeByteIndex;
#if CLIENT
        writer.WriteEnum((EServerMessage)(WriteBlockOffset + (int)message));
#else
        writer.WriteEnum((EClientMessage)(WriteBlockOffset + (int)message));
#endif
        int pos = writer.writeByteIndex;

        writeAction(writer);
        writer.AlignToByte();

        pos = writer.writeByteIndex - pos;

        writer.Flush();
        ENetReliability reliability = reliable ? ENetReliability.Reliable : ENetReliability.Unreliable;
        IncrementByteCount(message, true, pos * connections.Count);

        if (!anyConnectionsHaveAlteredIndexes)
        {
            for (int i = 0; i < connections.Count; ++i)
                connections[i].Send(writer.buffer, writer.writeByteIndex, reliability);
        }
        else
        {
            // apply mappings
            int oldIndex = writer.writeByteIndex;
            DevkitServerMessage last = message;
            for (int i = 0; i < connections.Count; ++i)
            {
                ITransportConnection connection = connections[i];

                if (!CustomNetMessageListeners.RemoteMappings.TryGetValue(connection, out ConcurrentDictionary<Type, DevkitServerMessage> mappings)
                    || !mappings.TryGetValue(localMappingType!, out DevkitServerMessage remoteMessage)
                    || remoteMessage == message)
                {
                    remoteMessage = message;
                }

                if (remoteMessage != last)
                {
                    last = remoteMessage;

                    writer.writeByteIndex = enumIndex;
#if CLIENT
                    writer.WriteEnum((EServerMessage)(WriteBlockOffset + (int)remoteMessage));
#else
                    writer.WriteEnum((EClientMessage)(WriteBlockOffset + (int)remoteMessage));
#endif
                    writeAction(writer);
                    writer.AlignToByte();
                    writer.Flush();
                    writer.writeByteIndex = oldIndex;
                }

                connection.Send(writer.buffer, oldIndex, reliability);
            }
        }
    }
#endif

    internal static void Send(this
#if SERVER
        ITransportConnection
#else
        IClientTransport
#endif
        connection, byte[] bytes, bool reliable = true, int count = -1, int offset = -1)
    {
        DevkitServerModule.AssertIsDevkitServerClient();

        if (offset < 0)
            offset = 0;
        if (count < 0)
            count = bytes.Length - offset;

        if (offset + count > bytes.Length)
            throw new ArgumentOutOfRangeException(nameof(count));

#if METHOD_LOGGING
        MessageOverhead overhead;
        unsafe
        {
            fixed (byte* ptr = &bytes[offset])
                overhead = new MessageOverhead(ptr);
        }
#endif

        if (connection is HighSpeedConnection conn2)
        {
            if (offset > 0)
            {
                byte[] newBytes = new byte[count];
                Buffer.BlockCopy(bytes, offset, newBytes, 0, count);
                bytes = newBytes;
            }
            conn2.Send(bytes, count, reliable ? ENetReliability.Reliable : ENetReliability.Unreliable);
#if METHOD_LOGGING

            Logger.DevkitServer.LogDebug(Source, "Sending " + count.Format() + " B (HS) to " +
#if SERVER
                                                 connection.Format()
#else
                                                 "server"
#endif
                                                 + $": {overhead.Format()}.", ConsoleColor.DarkYellow);

#endif
            return;
        }

        ThreadUtil.assertIsGameThread();

        Writer.Reset();
        int msg = WriteBlockOffset + (int)DevkitServerMessage.InvokeNetCall;
#if SERVER
        Writer.WriteEnum((EClientMessage)msg);
#else
        Writer.WriteEnum((EServerMessage)msg);
#endif
        int len = Math.Min(ushort.MaxValue, count);
        if (len < count)
        {
            Logger.DevkitServer.LogWarning(Source, $"Message to be sent to {connection.Format()} truncated.");
        }

        Writer.WriteUInt16((ushort)len);
        Writer.WriteBytes(bytes, offset, len);
        IncrementByteCount(DevkitServerMessage.InvokeNetCall, true, len);
        Writer.Flush();
#if METHOD_LOGGING
        Logger.DevkitServer.LogDebug(Source, "Sending " + len.Format() + " B to " +
#if SERVER
                                             connection.Format()
#else
                                             "server"
#endif
                                             + $": {overhead.Format()}.", ConsoleColor.DarkYellow);
#endif
        connection.Send(Writer.buffer, Writer.writeByteIndex, reliable ? ENetReliability.Reliable : ENetReliability.Unreliable);
    }
#if SERVER
    internal static void Send(IReadOnlyList<ITransportConnection>? connections, byte[] bytes, bool reliable = true)
    {
        if (connections == null)
        {
            ThreadUtil.assertIsGameThread();
            connections = Provider.GatherRemoteClientConnections();
        }
        else
        {
            for (int i = 0; i < connections.Count; ++i)
            {
                if (connections[i] is not HighSpeedConnection)
                    ThreadUtil.assertIsGameThread();
            }
        }

        DevkitServerModule.AssertIsDevkitServerClient();

        if (connections.Count == 0)
            return;
        NetPakWriter writer = DevkitServerModule.IsMainThread ? Writer : new NetPakWriter { buffer = new byte[bytes.Length + 8] };

        writer.Reset();
        writer.WriteEnum((EClientMessage)(WriteBlockOffset + (int)DevkitServerMessage.InvokeNetCall));
        writer.WriteUInt16((ushort)bytes.Length);
        writer.WriteBytes(bytes);
        writer.Flush();
#if METHOD_LOGGING
        MessageOverhead overhead;
        unsafe
        {
            fixed (byte* ptr = bytes)
                overhead = new MessageOverhead(ptr);
        }
        Logger.DevkitServer.LogDebug(Source, $"Sending {bytes.Length.Format()} B to {connections.Count.Format()} user(s): {overhead.Format()}.", ConsoleColor.DarkYellow);
#endif
        IncrementByteCount(DevkitServerMessage.InvokeNetCall, true, bytes.Length * connections.Count);
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] is HighSpeedConnection conn)
            {
                conn.Send(bytes, bytes.Length, reliable ? ENetReliability.Reliable : ENetReliability.Unreliable);
#if METHOD_LOGGING

                Logger.DevkitServer.LogDebug(Source, $"Sending {bytes.Length.Format()} B (HS) to {conn.Format()}: {overhead.Format()}.", ConsoleColor.DarkYellow);
#endif
            }
            
            connections[i].Send(writer.buffer, writer.writeByteIndex, reliable ? ENetReliability.Reliable : ENetReliability.Unreliable);
        }
    }
#endif

    internal static void KeepAlive(NetTask task)
    {
        for (int i = 0; i < LocalAckRequests.Count; ++i)
            if (LocalAckRequests[i].Task == task)
                LocalAckRequests[i] = new Listener(task, LocalAckRequests[i].Caller);

        for (int i = 0; i < LocalListeners.Count; ++i)
            if (LocalListeners[i].Task == task)
                LocalListeners[i] = new Listener(task, LocalListeners[i].Caller);
    }
    private readonly struct ListenerTimestamp(NetTask task)
    {
        public readonly long RequestId = task.RequestId;
        public readonly DateTime Timestamp = DateTime.UtcNow;
        public readonly bool IsAcknowledgeRequest = task.IsAcknowledgementRequest;
    }

    private readonly struct Listener(NetTask task, BaseNetCall caller)
    {
        public readonly NetTask? Task = task;
        public readonly BaseNetCall Caller = caller;
    }
}
public readonly struct NetMethodInfo(MethodInfo method, NetCallAttribute attribute)
{
    public readonly MethodInfo Method = method;
    public readonly NetCallAttribute Attribute = attribute;
}

public readonly struct NetInvokerInfo(FieldInfo field, BaseNetCall invoker, NetMethodInfo[] methods)
{
    public readonly FieldInfo Field = field;
    public readonly BaseNetCall Invoker = invoker;
    public readonly NetMethodInfo[] Methods = methods;
}

/// <summary>
/// Defines a method listener for a <see cref="NetCall"/>.
/// </summary>
[MeansImplicitUse]
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class NetCallAttribute : Attribute
{
    readonly NetCallSource type;
    readonly ushort methodId;
    readonly string? methodGuid;
    internal NetCallAttribute(NetCallSource type, DevkitServerNetCall methodId) : this(type, (ushort)methodId) { }
    internal NetCallAttribute(NetCallSource type, ushort methodId)
    {
        this.type = type;
        this.methodId = methodId;
    }
    public NetCallAttribute(NetCallSource type, string methodGuid)
    {
        this.type = type;
        this.methodGuid = methodGuid;
    }
    public NetCallSource Type => type;
    public ushort MethodID => methodId;
    public string? GuidString => methodGuid;
    public bool HighSpeed { get; set; }
    public bool HighSpeedAllowUnverified { get; set; }
}

/// <summary>
/// Defines which side the message would be received from.
/// </summary>
public enum NetCallSource : byte
{
    FromServer = 0,
    FromClient = 1,
    FromEither = 2,
    /// <summary>
    /// Equivalent to using the <see cref="IgnoreAttribute"/>.
    /// </summary>
    None = 3
}

/// <summary>
/// Represents reserved messages by DevkitServer and also used to wrap <see cref="ICustomMessageListener"/>
/// </summary>
public enum DevkitServerMessage : uint
{
    InvokeNetCall = 0,
    MovementRelay = 1,
    SendTileData = 2,
    ActionRelay = 3,
    SendNavigationData = 4
}
public delegate void SentMessage(ITransportConnection connection, in MessageOverhead overhead, byte[] message);
public delegate void ReceivedMessage(ITransportConnection connection, in MessageOverhead overhead, byte[] message);
public delegate void ClientConnected(ITransportConnection connection);
public delegate void ClientDisconnected(ITransportConnection connection);