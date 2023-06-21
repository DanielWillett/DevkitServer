using DevkitServer.API;
using DevkitServer.API.Permissions;
using DevkitServer.Multiplayer.Actions;
using DevkitServer.Patches;
using DevkitServer.Players;
using DevkitServer.Plugins;
using DevkitServer.Util.Encoding;
using HarmonyLib;
using JetBrains.Annotations;
using SDG.NetPak;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
#if CLIENT
using DevkitServer.Multiplayer.Sync;
#endif

namespace DevkitServer.Multiplayer.Networking;

[EarlyTypeInit]
public static class NetFactory
{
    /// <summary>The maximum amount of time in seconds a listener is guaranteed to stay active.</summary>
    public const double MaxListenTimeout = 600d;

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
    private static readonly List<Listener> localListeners = new List<Listener>(16);
    private static readonly List<Listener> localAckRequests = new List<Listener>(16);
    private static readonly List<NetMethodInfo> registeredMethods = new List<NetMethodInfo>(16);
    private static readonly Dictionary<ushort, BaseNetCall> invokers = new Dictionary<ushort, BaseNetCall>(16);
    private static readonly Dictionary<ushort, BaseNetCall> hsInvokers = new Dictionary<ushort, BaseNetCall>(16);
    private static readonly Dictionary<Guid, BaseNetCall> guidInvokers = new Dictionary<Guid, BaseNetCall>(16);
    private static readonly Dictionary<Guid, BaseNetCall> guidHsInvokers = new Dictionary<Guid, BaseNetCall>(16);
    private static Func<PooledTransportConnectionList>? PullFromTransportConnectionListPool;
    private static readonly NetPakWriter Writer = new NetPakWriter();
    public static int ReceiveBlockOffset { get; private set; }
    public static int WriteBlockOffset { get; private set; }
    public static int BlockSize { get; private set; }
    public static int NewReceiveBitCount { get; private set; }
    public static int NewWriteBitCount { get; private set; }
    public static Delegate[] Listeners { get; private set; } = Array.Empty<Delegate>();


#if CLIENT
    internal static StaticGetter<IClientTransport> GetPlayerTransportConnection =
        Accessor.GenerateStaticGetter<Provider, IClientTransport>("clientTransport", BindingFlags.NonPublic, throwOnError: true)!;
#endif
    private static bool ClaimMessageBlock()
    {
        Type devkitType = typeof(DevkitMessage);
#if SERVER
        Type vanilla = typeof(EServerMessage);
        Type vanillaSend = typeof(EClientMessage);
#else
        Type vanilla = typeof(EClientMessage);
        Type vanillaSend = typeof(EServerMessage);
#endif

        // calculates the minimum amount of bits needed to represent the number.
        int GetMinBitCount(int n) => (int)Math.Floor(Math.Log(n, 2)) + 1;

        ReceiveBlockOffset = vanilla.GetFields(BindingFlags.Static | BindingFlags.Public).Length;
        WriteBlockOffset = vanillaSend.GetFields(BindingFlags.Static | BindingFlags.Public).Length;
        BlockSize = devkitType.GetFields(BindingFlags.Static | BindingFlags.Public).Length;
        int origReceiveBitCount = GetMinBitCount(ReceiveBlockOffset);
        int origWriteBitCount = GetMinBitCount(WriteBlockOffset);

        NewReceiveBitCount = GetMinBitCount(ReceiveBlockOffset + BlockSize);
        NewWriteBitCount = GetMinBitCount(WriteBlockOffset + BlockSize);

        Logger.LogDebug($"[NET FACTORY] Collected enum values: Offsets: (rec {ReceiveBlockOffset}, wrt {WriteBlockOffset}) | Size: {BlockSize} | Orig bit ct: (rec {origReceiveBitCount}, wrt {origWriteBitCount}). Bit cts: (rec {NewReceiveBitCount}, wrt {NewWriteBitCount}).");

        // initialize DevkitMessage callbacks
        MethodInfo[] methods = new MethodInfo[BlockSize];

        methods[(int)DevkitMessage.InvokeMethod] = typeof(NetFactory).GetMethod(nameof(ReceiveMessage), BindingFlags.NonPublic | BindingFlags.Static)!;
        methods[(int)DevkitMessage.MovementRelay] = typeof(UserInput).GetMethod(nameof(UserInput.ReceiveMovementRelay), BindingFlags.NonPublic | BindingFlags.Static)!;
#if CLIENT
        methods[(int)DevkitMessage.SendTileData] = typeof(TileSync).GetMethod(nameof(TileSync.ReceiveTileData), BindingFlags.NonPublic | BindingFlags.Static)!;
#endif
        methods[(int)DevkitMessage.ActionRelay] = typeof(EditorActions).GetMethod(nameof(EditorActions.ReceiveActionRelay), BindingFlags.NonPublic | BindingFlags.Static)!;

        Listeners = new Delegate[methods.Length];
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
                Logger.LogError("Unable to find " + readEnum.Name + ".ReadEnum(...) or " + writeEnum.Name + ".WriteEnum(...) !", method: "NET FACTORY");
            else
                Logger.LogError("Unable to find " + readEnum.Name + ".ReadEnum(...)!", method: "NET FACTORY");

            goto reset;
        }
        if (writeMethod == null)
        {
            Logger.LogError("Unable to find " + writeEnum.Name + ".WriteEnum(...) !", method: "NET FACTORY");
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
                    Logger.LogWarning($"Unable to unpatch {readEnum.Format()}.ReadEnum(...) after an error patching {writeEnum.Format()}.WriteEnum(...).", method: "NET FACTORY");
                    Logger.LogError(ex2, method: "NET FACTORY");
                }
            }
            Logger.LogError("Failed to patch networking enum " + (p1 ? "write method." : "read and/or write methods.", method: "NET FACTORY"));
            Logger.LogError(ex, method: "NET FACTORY");
            goto reset;
        }

        const string netMessagesName = "SDG.Unturned.NetMessages";
        Type? netMessagesType;
        try
        {
            netMessagesType = Accessor.AssemblyCSharp.GetType(netMessagesName, true, false);
            if (netMessagesType == null)
            {
                Logger.LogError("Unable to find type " + netMessagesName + "!", method: "NET FACTORY");
                goto reset;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Unable to find type " + netMessagesName + "!", method: "NET FACTORY");
            Logger.LogError(ex, method: "NET FACTORY");
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
            Logger.LogError("Unable to find " + netMessagesType.Format() + "." + fieldName + "!", method: "NET FACTORY");
            goto reset;
        }
        Array readCallbacks = (Array)field.GetValue(null);

        Type callbackType = readCallbacks.GetType().GetElementType()!;

        Array nArr = Array.CreateInstance(callbackType, readCallbacks.Length + Listeners.Length);
        Array.Copy(readCallbacks, nArr, readCallbacks.Length);

        for (int i = 0; i < methods.Length; ++i)
        {
            MethodInfo m = methods[i];
            if (m == null)
                continue;
            try
            {
                nArr.SetValue(Listeners[i] = m.CreateDelegate(callbackType), readCallbacks.Length + i);
            }
            catch (Exception ex)
            {
                Logger.LogError("Implemented method for " + (DevkitMessage)i + " can not be converted to " + callbackType.Name + ".", method: "NET FACTORY");
                Logger.LogError(ex, method: "NET FACTORY");
                goto reset;
            }
        }

        field.SetValue(null, nArr);
        return true;

    reset:
        NewReceiveBitCount = origReceiveBitCount;
        NewWriteBitCount = origWriteBitCount;
        BlockSize = 0;
        Listeners = Array.Empty<Delegate>();
        if (p1)
        {
            try
            {
                PatchesMain.Patcher.Unpatch(readMethod, HarmonyPatchType.Prefix, PatchesMain.HarmonyId);
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Unable to unpatch " + readEnum.Format() + ".ReadEnum(...) while cancelling initialization.", method: "NET FACTORY");
                Logger.LogError(ex, method: "NET FACTORY");
            }
            if (p2)
            {
                try
                {
                    PatchesMain.Patcher.Unpatch(writeMethod, HarmonyPatchType.Prefix, PatchesMain.HarmonyId);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning("Unable to unpatch " + writeEnum.Format() + ".WriteEnum(...) while cancelling initialization.", method: "NET FACTORY");
                    Logger.LogError(ex, method: "NET FACTORY");
                }
            }
        }
        return false;
    }
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
    public static float GetBytesPerSecondAvg(DevkitMessage message, bool send) => GetBytesTotal(message, send, out float timespan) / timespan;
    public static float GetBytesPerSecondAvg(bool send) => GetBytesTotal(send, out float timespan) / timespan;
    public static long GetBytesTotal(DevkitMessage message, bool send, out float timespan)
    {
        timespan = CachedTime.RealtimeSinceStartup - (send ? _outByteCtStTime : _inByteCtStTime);
        if ((int)message < BlockSize)
            return (send ? _outByteCt : _inByteCt)[(int)message];

        return 0;
    }
    public static long GetBytesTotal(bool send, out float timespan)
    {
        long ttl = 0;
        timespan = CachedTime.RealtimeSinceStartup - (send ? _outByteCtStTime : _inByteCtStTime);
        for (int i = 0; i < BlockSize; ++i)
            ttl += (send ? _outByteCt : _inByteCt)[i];

        return ttl;
    }
    internal static void IncrementByteCount(bool send, DevkitMessage msg, long length)
    {
        if ((int)msg < BlockSize)
        {
            if (send)
                length += Mathf.CeilToInt(NewWriteBitCount / 8f) + sizeof(ushort);
            else
                length += Mathf.CeilToInt(NewReceiveBitCount / 8f) + sizeof(ushort);
            (send ? _outByteCt : _inByteCt)[(int)msg] += length;
        }
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

        if (!ClaimMessageBlock())
            return false;

        PullFromTransportConnectionListPool = null;
        try
        {
            MethodInfo? method = Accessor.AssemblyCSharp
                .GetType("SDG.Unturned.TransportConnectionListPool", true, false)?.GetMethod("Get",
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (method != null)
            {
                PullFromTransportConnectionListPool = (Func<PooledTransportConnectionList>)method.CreateDelegate(typeof(Func<PooledTransportConnectionList>));
            }
            else
            {
                Logger.LogWarning("Couldn't find Get in TransportConnectionListPool, list pooling will not be used.", method: "NET FACTORY");
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Couldn't get Get from TransportConnectionListPool, list pooling will not be used (" + ex.Message + ").", method: "NET FACTORY");
        }

        return true;
    }
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
                Logger.LogError(ex);
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
                Logger.LogError(ex);
                if (ex2 != null)
                    throw new AggregateException("Unable to create pooled transport connection!", ex2, ex);

                throw new Exception("Unable to create pooled transport connection!", ex);
            }
        }

        return rtn;
    }
    public static PooledTransportConnectionList GetPooledTransportConnectionList(IEnumerable<ITransportConnection> selector, int capacity = -1)
    {
        PooledTransportConnectionList rtn = GetPooledTransportConnectionList(capacity);
        rtn.AddRange(selector);
        return rtn;
    }
    [UsedImplicitly]
    private static void ReceiveMessage(
#if SERVER
        ITransportConnection transportConnection,
#endif
        NetPakReader reader)
    {
        if (!reader.ReadUInt16(out ushort len))
            return;
        byte[] bytes = new byte[len];
        if (!reader.ReadBytes(bytes, len))
            return;
        IncrementByteCount(false, DevkitMessage.InvokeMethod, len + sizeof(ushort));

        MessageOverhead ovh = new MessageOverhead(bytes);
#if SERVER
        OnReceived(bytes, transportConnection, ovh, false);
#else
        OnReceived(bytes, GetPlayerTransportConnection(), ovh, false);
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
        if (DevkitServerModule.IsEditing)
        {
            bool flag = reader.ReadBits(NewReceiveBitCount, out uint num);
            if (num <= ReceiveBlockOffset + BlockSize)
            {
#if SERVER
                value = (EServerMessage)num;
#else
                value = (EClientMessage)num;
#endif
                // Logger.LogDebug($"Reading net message enum {num,2}: {(num <= ReceiveBlockOffset ? ("Unturned." + value) : ("DevkitServer." + (DevkitMessage)(num - ReceiveBlockOffset)))}");
                __result = flag;
            }
            else
            {
                value = default;
                __result = false;
            }
            return false;
        }

        return true;
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
        if (DevkitServerModule.IsEditing)
        {
            uint v = (uint)value;
#if SERVER
            // if (value != EClientMessage.InvokeMethod)
#else
            // if (value != EServerMessage.InvokeMethod)
#endif
                // Logger.LogDebug($"Writing net message enum {v, 2}: {(v < WriteBlockOffset ? ("Unturned." + value) : ("DevkitServer." + (DevkitMessage)(v - WriteBlockOffset)))}");
            __result = writer.WriteBits(v, NewWriteBitCount);
            return false;
        }

        return true;
    }

    internal static string GetInvokerName(ushort id, bool hs = false) =>
        (!hs ? invokers : hsInvokers).TryGetValue(id, out BaseNetCall bnc) ? bnc.Name : id.ToString();
    internal static T? GetInvoker<T>(ushort id) where T : BaseNetCall =>
        invokers.TryGetValue(id, out BaseNetCall bnc) ? bnc as T : null;
    internal static void OnReceived(byte[] message,
#if SERVER
        ITransportConnection
#else
        IClientTransport
#endif
            connection, in MessageOverhead overhead, bool hs)
    {
        if (message.Length < MessageOverhead.MinimumSize)
        {
            Logger.LogError("Received too short of a message.", method: "NET FACTORY");
            goto rtn;
        }

        int size = overhead.Size;
        if (size > message.Length)
        {
            Logger.LogError("Message overhead read a size larger than the message payload: " + size + " bytes.",
                method: "NET FACTORY");
            goto rtn;
        }
        
        byte[] data = new byte[size];
        Buffer.BlockCopy(message, overhead.Length, data, 0, size);

        if ((hs ? hsInvokers : invokers).TryGetValue(overhead.MessageId, out BaseNetCall call))
        {
            bool verified = !hs || connection is not HighSpeedConnection hsc || hsc.Verified;
            object[]? parameters = null;
            bool one = false;
            if (overhead.RequestKey != default && verified)
            {
                if ((overhead.Flags & MessageFlags.RequestResponse) == MessageFlags.RequestResponse)
                {
                    one = true;
                    long rk = overhead.RequestKey;
                    for (int l = 0; l < localListeners.Count; ++l)
                    {
                        if (localListeners[l].RequestId == rk)
                        {
                            Listener lt = localListeners[l];
                            if (lt.Caller.ID == call.ID && lt.Caller.HighSpeed == hs)
                            {
                                if (lt.Task != null && !lt.Task.isCompleted)
                                {
                                    lock (call)
                                    {
                                        if (!call.Read(data, out parameters))
                                        {
                                            Logger.LogWarning(
                                                $"Unable to read incoming message for message {overhead.Format()}.",
                                                method: "NET FACTORY");
                                            goto rtn;
                                        }
                                    }

                                    object[] old = parameters;
                                    parameters = new object[old.Length + 1];
                                    parameters[0] = new MessageContext(connection, overhead, hs);
                                    Array.Copy(old, 0, parameters, 1, old.Length);
                                    lt.Task.TellCompleted(parameters, true);
                                }

                                localListeners.RemoveAt(l);
                                break;
                            }
                        }
                    }
                }
                else if ((overhead.Flags & MessageFlags.AcknowledgeResponse) == MessageFlags.AcknowledgeResponse)
                {
                    one = true;
                    long rk = overhead.RequestKey;
                    for (int l = 0; l < localAckRequests.Count; ++l)
                    {
                        if (localAckRequests[l].RequestId == rk)
                        {
                            Listener lt = localAckRequests[l];
                            if (lt.Caller.ID == call.ID && lt.Caller.HighSpeed == hs)
                            {
                                int? errorCode = null;
                                if (overhead.Size == sizeof(int))
                                {
                                    if (data.Length >= sizeof(int))
                                        errorCode = BitConverter.ToInt32(data, 0);
                                }

                                if (lt.Task != null && !lt.Task.isCompleted)
                                {
                                    lt.Task.TellCompleted(new MessageContext(connection, overhead, hs), true,
                                        errorCode);
                                }

                                localAckRequests.RemoveAt(l);
                                break;
                            }
                        }
                    }
                }
            }

            if (one && (overhead.Flags & MessageFlags.RunOriginalMethodOnRequest) !=
                MessageFlags.RunOriginalMethodOnRequest)
                goto rtn;
            for (int i = 0; i < registeredMethods.Count; ++i)
            {
                if (registeredMethods[i].Attribute.MethodID == call.ID &&
                    registeredMethods[i].Attribute.HighSpeed == hs &&
                    (verified || registeredMethods[i].Attribute.HighSpeedAllowUnverified))
                {
                    NetMethodInfo info = registeredMethods[i];
                    if (parameters == null)
                    {
                        lock (call)
                        {
                            if (!call.Read(data, out parameters))
                            {
                                Logger.LogWarning(
                                    $"Unable to read incoming message for message {overhead.Format()}\n.",
                                    method: "NET FACTORY");
                                goto rtn;
                            }
                        }

                        object[] old = parameters;
                        parameters = new object[old.Length + 1];
                        parameters[0] = new MessageContext(connection, overhead, hs);
                        Array.Copy(old, 0, parameters, 1, old.Length);
                    }

                    try
                    {
                        object res = info.Method.Invoke(null, parameters);
                        if (res is int resp)
                        {
                            ((MessageContext)parameters[0]).Acknowledge(resp);
                        }
                        else if (res is StandardErrorCode resp1)
                        {
                            ((MessageContext)parameters[0]).Acknowledge(resp1);
                        }
                        else if (res is Task task)
                        {
                            if (!task.IsCompleted)
                            {
                                MessageOverhead ovh = overhead;
                                Task.Run(async () =>
                                {
                                    try
                                    {
                                        await task.ConfigureAwait(false);
                                        if (task is Task<int> ti)
                                        {
                                            ((MessageContext)parameters[0]).Acknowledge(ti.Result);
                                        }
                                        else if (task is Task<StandardErrorCode> ti2)
                                        {
                                            ((MessageContext)parameters[0]).Acknowledge(ti2.Result);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.LogError("Error running method " + info.Method.Format(),
                                            method: "NET FACTORY");
                                        Logger.LogError("Message: " + ovh.Format() + ".", method: "NET FACTORY");
                                        Logger.LogError(ex);
                                    }
                                });
                            }
                            else if (task is Task<int> ti)
                            {
                                ((MessageContext)parameters[0]).Acknowledge(ti.Result);
                            }
                            else if (task is Task<StandardErrorCode> ti2)
                            {
                                ((MessageContext)parameters[0]).Acknowledge(ti2.Result);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ex is TargetInvocationException { InnerException: { } t })
                            ex = t;
                        Logger.LogError("Error running method " + info.Method.Format(), method: "NET FACTORY");
                        Logger.LogError("Message: " + overhead.Format() + ".", method: "NET FACTORY");
                        Logger.LogError(ex);
                    }
                }
            }
        }

        rtn:
        RemoveExpiredListeners();
    }
    private static void RemoveExpiredListeners()
    {
        DateTime now = DateTime.UtcNow;
        for (int i = localAckRequests.Count - 1; i >= 0; --i)
        {
            if ((now - localAckRequests[i].Timestamp).TotalSeconds > MaxListenTimeout)
            {
                localAckRequests.RemoveAt(i);
                Logger.LogDebug($"[NET FACTORY] Acknowledge request expired: {localAckRequests[i].Caller.Name.Format(false)}");
            }
        }

        for (int i = localListeners.Count - 1; i >= 0; --i)
        {
            if ((now - localListeners[i].Timestamp).TotalSeconds > MaxListenTimeout)
            {
                localListeners.RemoveAt(i);
                Logger.LogDebug($"[NET FACTORY] Request expired: {localAckRequests[i].Caller.Name.Format(false)}");
            }
        }
    }
    public static void Reflect(Assembly assembly, NetCallSource search, IList<NetMethodInfo>? outMethods = null, IList<NetInvokerInfo>? outCalls = null)
    {
        List<Type> types = Accessor.GetTypesSafe(removeIgnored: false);
        int before1 = registeredMethods.Count;
        int before2 = invokers.Count;
        ReflectMethods(types, search, outMethods);
        ReflectInvokers(types, outCalls, outMethods);
        Logger.LogInfo("[NET FACTORY] " + assembly.GetName().Name.Format(false) + " registered " + (registeredMethods.Count - before1).Format() + " net-methods and " + (invokers.Count - before2).Format() + " invokers.");
    }
    private static void ReflectMethods(List<Type> types, NetCallSource search, IList<NetMethodInfo>? outMethods = null)
    {
        for (int i = 0; i < types.Count; ++i)
        {
            MethodInfo[] methods = types[i].GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            for (int m = 0; m < methods.Length; ++m)
            {
                MethodInfo method = methods[m];

                if (Attribute.IsDefined(method, typeof(IgnoreAttribute)) ||
                    Attribute.GetCustomAttribute(method, typeof(NetCallAttribute)) is not NetCallAttribute attribute ||
                    attribute.Type == NetCallSource.None ||
                    !(attribute.Type == NetCallSource.FromEither || search == NetCallSource.FromEither || search == attribute.Type))
                    continue;

                bool found = false;
                for (int j = 0; j < registeredMethods.Count; ++j)
                {
                    NetMethodInfo info = registeredMethods[j];
                    if (info.Method == method)
                    {
                        found = true;
                        break;
                    }
                }
                if (found) continue;
                NetMethodInfo kvp = new NetMethodInfo(method, attribute);
                registeredMethods.Add(kvp);
                outMethods?.Add(kvp);
            }
        }
    }
    private static void ReflectInvokers(List<Type> types, IList<NetInvokerInfo>? outCalls = null, IList<NetMethodInfo>? outMethods = null)
    {
        Type ctx = typeof(MessageContext);
        Assembly dka = Assembly.GetExecutingAssembly();
        for (int i = 0; i < types.Count; ++i)
        {
            FieldInfo[] fields = types[i].GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            for (int f = 0; f < fields.Length; ++f)
            {
                if (!fields[f].IsStatic || Attribute.IsDefined(fields[f], typeof(IgnoreAttribute)) || !fields[f].FieldType.IsSubclassOf(typeof(BaseNetCall)))
                    continue;
                
                FieldInfo field = fields[f];
                if (Attribute.GetCustomAttribute(field, typeof(IgnoreAttribute)) != null || field.GetValue(null) is not BaseNetCall call)
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
                    Logger.LogWarning($"Unable to link {field.Format()} invoker to a plugin. Use the {typeof(PluginIdentifierAttribute).Format()} on the field to link an invoker to a " +
                                      "plugin when multiple plugins are loaded from an assembly.", method: "NET REFLECT");
                    continue;
                }

                call.Plugin = plugin;
                if (call.ID != default)
                {
                    if (!call.HighSpeed && invokers.TryGetValue(call.ID, out BaseNetCall c2) || call.HighSpeed && hsInvokers.TryGetValue(call.ID, out c2))
                    {
                        if (c2.GetType() != call.GetType())
                            Logger.LogWarning($"Inconsistant duplicate invoker {call.ID.Format()} {(call.HighSpeed ? " (HS)" : string.Empty)} at field {field.Format()}.",
                                method: "NET REFLECT");
                        continue;
                    }
                }
                else if (call.Guid != default)
                {
                    if (!call.HighSpeed && guidInvokers.TryGetValue(call.Guid, out BaseNetCall c2) || call.HighSpeed && guidHsInvokers.TryGetValue(call.Guid, out c2))
                    {
                        if (c2.GetType() != call.GetType())
                            Logger.LogWarning($"Inconsistant duplicate invoker {call.Guid.Format()} {(call.HighSpeed ? " (HS)" : string.Empty)} at field {field.Format()}.",
                                method: "NET REFLECT");
                        continue;
                    }
                }
                else
                {
                    Logger.LogWarning($"Invoker {field.Format()} does not have a unique ID or GUID.",
                        method: "NET REFLECT");
                    continue;
                }
                Type[] generics = callType.GetGenericArguments();
                List<NetMethodInfo>? outList = outCalls == null ? null : new List<NetMethodInfo>(1);
                for (int index = registeredMethods.Count - 1; index >= 0; --index)
                {
                    NetMethodInfo info = registeredMethods[index];
                    if (!(
                            (call.ID != default && info.Attribute.MethodID == call.ID ||
                             call.Guid != Guid.Empty &&
                             !string.IsNullOrEmpty(info.Attribute.GuidString) &&
                             Guid.TryParse(info.Attribute.GuidString, out Guid g) &&
                             g == call.Guid
                            ) &&
                            info.Attribute.HighSpeed == call.HighSpeed))
                        continue;

                    ParameterInfo[] parameters = info.Method.GetParameters();
                    if (callType == typeof(NetCall))
                    {
                        if (parameters.Length != 1 ||
                            !parameters[0].ParameterType.IsAssignableFrom(ctx) ||
                            !info.Method.IsStatic)
                        {
                            Logger.LogWarning($"Method {info.Method.Format()} has the wrong signature for invoker {field.Format()}. {Environment.NewLine}" +
                                              $"Expected signature: {FormattingUtil.FormatMethod(typeof(void), info.Method.DeclaringType, info.Method.Name, new (Type type, string? name)[]
                                              {
                                                  (ctx, "ctx")
                                              }, isStatic: true)}.", method: "NET REFLECT");
                            registeredMethods.RemoveAt(index);
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
                            Logger.LogWarning($"Method {info.Method.Format()} has the wrong signature for invoker {field.Format()}. {Environment.NewLine}" +
                                              $"Expected signature: {FormattingUtil.FormatMethod(typeof(void), info.Method.DeclaringType, info.Method.Name, new (Type type, string? name)[]
                                              {
                                                  (ctx, "ctx"),
                                                  (typeof(ByteReader), "reader")
                                              }, isStatic: true)}.", method: "NET REFLECT");
                            registeredMethods.RemoveAt(index);
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
                                parameters2[k + 1] = (generics[k], null);
                            }
                            Logger.LogWarning($"Method {info.Method.Format()} has the wrong signature for invoker {field.Format()}. {Environment.NewLine}" +
                                              $"Expected signature: {FormattingUtil.FormatMethod(typeof(void), info.Method.DeclaringType, info.Method.Name, parameters2, isStatic: true)}.",
                                method: "NET REFLECT");
                            registeredMethods.RemoveAt(index);
                            outMethods?.RemoveAll(x => x.Method == info.Method);
                        }
                    }

                    outList?.Add(info);
                }

                call.Name = (core ? Permission.DevkitServerModuleCode : plugin!.PermissionPrefix) + "." + field.Name;
                call.SetThrowOnError(true);
                if (call.ID != default)
                    (call.HighSpeed ? hsInvokers : invokers).Add(call.ID, call);
                else
                    (call.HighSpeed ? guidHsInvokers : guidInvokers).Add(call.Guid, call);

                outCalls?.Add(new NetInvokerInfo(field, call, outList?.ToArray() ?? Array.Empty<NetMethodInfo>()));
            }
        }
    }
    internal static void RegisterListener(NetTask netTask, BaseNetCall caller)
    {
        Listener listener = new Listener(netTask, caller);
        localListeners.Add(listener);
    }
    internal static void RegisterAckListener(NetTask netTask, BaseNetCall caller)
    {
        Listener listener = new Listener(netTask, caller);
        localAckRequests.Add(listener);
    }
    internal static void RemoveListener(NetTask task)
    {
        if (task.isAck)
        {
            for (int i = 0; i < localAckRequests.Count; i++)
            {
                if (localAckRequests[i].RequestId == task.requestId)
                {
                    localAckRequests.RemoveAt(i);
                    break;
                }
            }
        }
        else
        {
            for (int i = 0; i < localListeners.Count; i++)
            {
                if (localListeners[i].RequestId == task.requestId)
                {
                    localListeners.RemoveAt(i);
                    break;
                }
            }
        }
    }
    public static void SendGeneric(DevkitMessage message,
#if SERVER
        ITransportConnection connection,
#endif
        byte[] bytes, int offset = 0, int length = -1, bool reliable = true)
    {
        if (length == -1)
            length = bytes.Length - offset;

        Writer.Reset();
#if CLIENT
        Writer.WriteEnum((EServerMessage)(WriteBlockOffset + (int)message));
#else
        Writer.WriteEnum((EClientMessage)(WriteBlockOffset + (int)message));
#endif
        Writer.WriteUInt16((ushort)length);
        Writer.WriteBytes(bytes, offset, length);
        Writer.Flush();
        IncrementByteCount(true, message, length);
#if CLIENT
        GetPlayerTransportConnection().Send(Writer.buffer, Writer.writeByteIndex, reliable ? ENetReliability.Reliable : ENetReliability.Unreliable);
#else
        connection.Send(Writer.buffer, Writer.writeByteIndex, reliable ? ENetReliability.Reliable : ENetReliability.Unreliable);
#endif
    }
#if SERVER
    public static void SendGeneric(DevkitMessage message, byte[] bytes, IList<ITransportConnection>? connections = null, int offset = 0, int length = -1, bool reliable = true)
    {
        if (length == -1)
            length = bytes.Length - offset;

        connections ??= Provider.GatherRemoteClientConnections();
        if (connections.Count == 0)
            return;
        Writer.Reset();
#if CLIENT
        Writer.WriteEnum((EServerMessage)(WriteBlockOffset + (int)message));
#else
        Writer.WriteEnum((EClientMessage)(WriteBlockOffset + (int)message));
#endif
        Writer.WriteUInt16((ushort)length);
        Writer.WriteBytes(bytes, offset, length);
        Writer.Flush();
        ENetReliability reliability = reliable ? ENetReliability.Reliable : ENetReliability.Unreliable;
        IncrementByteCount(true, message, length * connections.Count);
        for (int i = 0; i < connections.Count; ++i)
            connections[i].Send(Writer.buffer, Writer.writeByteIndex, reliability);
    }
#endif
    public static void Send(this
#if SERVER
        ITransportConnection
#else
        IClientTransport
#endif
        connection, byte[] bytes, bool reliable = true, int count = -1, int offset = -1)
    {
        ThreadUtil.assertIsGameThread();

        if (connection is HighSpeedConnection conn2)
        {
            if (offset > 0)
            {
                byte[] newBytes = new byte[count];
                Buffer.BlockCopy(bytes, offset, newBytes, 0, count);
                bytes = newBytes;
            }
            conn2.Send(bytes, count < 0 ? bytes.Length : count, reliable ? ENetReliability.Reliable : ENetReliability.Unreliable);
            return;
        }

        Writer.Reset();
        int msg = WriteBlockOffset + (int)DevkitMessage.InvokeMethod;
#if SERVER
        Writer.WriteEnum((EClientMessage)msg);
#else
        Writer.WriteEnum((EServerMessage)msg);
#endif
        int len = Math.Min(ushort.MaxValue, bytes.Length);
        Writer.WriteUInt16((ushort)len);
        Writer.WriteBytes(bytes, offset < 0 ? 0 : offset, count < 0 ? bytes.Length : count);
        IncrementByteCount(true, DevkitMessage.InvokeMethod, len);
        Writer.Flush();
#if DEBUG
        Logger.LogDebug("[NET FACTORY] Sending " + bytes.Length + " bytes to " +
#if SERVER
            connection.Format()
#else
            "server"
#endif
            + ".");
#endif
        connection.Send(Writer.buffer, Writer.writeByteIndex, reliable ? ENetReliability.Reliable : ENetReliability.Unreliable);
    }
#if SERVER
    public static void Send(this IList<ITransportConnection> connections, byte[] bytes, bool reliable = true)
    {
        Writer.Reset();
        Writer.WriteEnum((EClientMessage)(WriteBlockOffset + (int)DevkitMessage.InvokeMethod));
        Writer.WriteUInt16((ushort)bytes.Length);
        Writer.WriteBytes(bytes);
        Writer.Flush();
        Logger.LogDebug("[NET FACTORY] Sending " + bytes.Length + " bytes to " + connections.Count + " user(s).");
        IncrementByteCount(true, DevkitMessage.InvokeMethod, bytes.Length * connections.Count);
        for (int i = 0; i < connections.Count; ++i)
            connections[i].Send(Writer.buffer, Writer.writeByteIndex, reliable ? ENetReliability.Reliable : ENetReliability.Unreliable);
    }
#endif
    internal static void KeepAlive(NetTask task)
    {
        for (int i = 0; i < localAckRequests.Count; ++i)
            if (localAckRequests[i].Task == task)
                localAckRequests[i] = new Listener(task, localAckRequests[i].Caller);

        for (int i = 0; i < localListeners.Count; ++i)
            if (localListeners[i].Task == task)
                localListeners[i] = new Listener(task, localListeners[i].Caller);
    }
    private readonly struct Listener
    {
        public readonly long RequestId;
        public readonly NetTask? Task;
        public readonly BaseNetCall Caller;
        public readonly DateTime Timestamp;
        public Listener(NetTask task, BaseNetCall caller)
        {
            Task = task;
            RequestId = task.requestId;
            Caller = caller;
            Timestamp = DateTime.UtcNow;
        }
    }
}
public readonly struct NetMethodInfo
{
    public readonly MethodInfo Method;
    public readonly NetCallAttribute Attribute;
    public NetMethodInfo(MethodInfo method, NetCallAttribute attribute)
    {
        Method = method;
        Attribute = attribute;
    }
}

public readonly struct NetInvokerInfo
{
    public readonly FieldInfo Field;
    public readonly BaseNetCall Invoker;
    public readonly NetMethodInfo[] Methods;
    public NetInvokerInfo(FieldInfo field, BaseNetCall invoker, NetMethodInfo[] methods)
    {
        Field = field;
        Invoker = invoker;
        Methods = methods;
    }
}

[MeansImplicitUse]
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class NetCallAttribute : Attribute
{
    readonly NetCallSource type;
    readonly ushort methodId;
    readonly string? methodGuid;
    internal NetCallAttribute(NetCallSource type, NetCalls methodId) : this (type, (ushort)methodId) { }
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
public enum DevkitMessage
{
    InvokeMethod,
    InvokeGuidMethod,
    MovementRelay,
    SendTileData,
    ActionRelay
}
public delegate void SentMessage(ITransportConnection connection, in MessageOverhead overhead, byte[] message);
public delegate void ReceivedMessage(ITransportConnection connection, in MessageOverhead overhead, byte[] message);
public delegate void ClientConnected(ITransportConnection connection);
public delegate void ClientDisconnected(ITransportConnection connection);