using DevkitServer.Patches;
using DevkitServer.Players;
using DevkitServer.Util.Encoding;
using HarmonyLib;
using JetBrains.Annotations;
using SDG.NetPak;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DevkitServer.Multiplayer.Networking;

[EarlyTypeInit]
public static class NetFactory
{
    public enum DevkitMessage
    {
        InvokeMethod,
        RelayPacket
    }
    public const EClientMessage ClientMessageType = (EClientMessage)18;
    public const EServerMessage ServerMessageType = (EServerMessage)9;
    private static readonly List<Listener> localListeners = new List<Listener>(16);
    private static readonly List<Listener> localAckRequests = new List<Listener>(16);
    private static readonly List<NetMethodInfo> registeredMethods = new List<NetMethodInfo>(16);
    private static readonly Dictionary<ushort, BaseNetCall> invokers = new Dictionary<ushort, BaseNetCall>(16);
    private static Func<PooledTransportConnectionList>? PullFromTransportConnectionListPool;
    /// <summary>The maximum amount of time in seconds a listener is guaranteed to stay active.</summary>
    public const double MaxListenTimeout = 600d;
    private static readonly object sync = new object();
    private static readonly NetPakWriter Writer = new NetPakWriter();
    public static int ReceiveBlockOffset { get; private set; }
    public static int WriteBlockOffset { get; private set; }
    public static int BlockSize { get; private set; }
    public static int NewReceiveBitCount { get; private set; }
    public static int NewWriteBitCount { get; private set; }
    public static Delegate[] Listeners { get; private set; } = Array.Empty<Delegate>();
#if DEBUG
    private static readonly InstanceGetter<ClientMethodHandle, ClientMethodInfo>? ServerGetMethodInfo = Accessor.GenerateInstanceGetter<ClientMethodHandle, ClientMethodInfo>("clientMethodInfo");
    private static readonly InstanceGetter<ServerMethodHandle, ServerMethodInfo>? ClientGetMethodInfo = Accessor.GenerateInstanceGetter<ServerMethodHandle, ServerMethodInfo>("serverMethodInfo");
    private static readonly InstanceGetter<ClientMethodInfo, string>? ServerGetMethodName = Accessor.GenerateInstanceGetter<ClientMethodInfo, string>("name");
    private static readonly InstanceGetter<ServerMethodInfo, string>? ClientGetMethodName = Accessor.GenerateInstanceGetter<ServerMethodInfo, string>("name");
#endif


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

        Logger.LogDebug($"Collected enum values: Offsets: (rec {ReceiveBlockOffset}, wrt {WriteBlockOffset}) | Size: {BlockSize} | Orig bit ct: (rec {origReceiveBitCount}, wrt {origWriteBitCount}). Bit cts: (rec {NewReceiveBitCount}, wrt {NewWriteBitCount}).");

        // initialize DevkitMessage callbacks
        MethodInfo[] methods = new MethodInfo[BlockSize];

        methods[(int)DevkitMessage.InvokeMethod] = typeof(NetFactory).GetMethod(nameof(ReceiveMessage), BindingFlags.NonPublic | BindingFlags.Static)!;
        methods[(int)DevkitMessage.RelayPacket] = typeof(NetFactory).GetMethod(nameof(ReceiveRelayPacket), BindingFlags.NonPublic | BindingFlags.Static)!;

        Listeners = new Delegate[methods.Length];

        // patch reading enum method.

#if SERVER
        Type readEnum  = typeof(EServerMessage_NetEnum);
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
                Logger.LogError("Unable to find " + readEnum.Name + ".ReadEnum(...) or " + writeEnum.Name + ".WriteEnum(...) !");
            else
                Logger.LogError("Unable to find " + readEnum.Name + ".ReadEnum(...)!");

            goto reset;
        }
        if (writeMethod == null)
        {
            Logger.LogError("Unable to find " + writeEnum.Name + ".WriteEnum(...) !");
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
                    Logger.LogWarning($"Unable to unpatch {readEnum.Format()}.ReadEnum(...) after an error patching {writeEnum.Format()}.WriteEnum(...).");
                    Logger.LogError(ex2);
                }
            }
            Logger.LogError("Failed to patch networking enum " + (p1 ? "write method." : "read and/or write methods."));
            Logger.LogError(ex);
            goto reset;
        }
#if DEBUG
        try
        {
            PatchesMain.Patcher.Patch(typeof(ClientMethodHandle).GetMethod("GetWriterWithStaticHeader", BindingFlags.Instance | BindingFlags.NonPublic),
                prefix: new HarmonyMethod(typeof(NetFactory).GetMethod(nameof(PatchServerGetWriterWithStaticHeader), BindingFlags.Static | BindingFlags.NonPublic)));
            PatchesMain.Patcher.Patch(typeof(ServerMethodHandle).GetMethod("GetWriterWithStaticHeader", BindingFlags.Instance | BindingFlags.NonPublic),
                prefix: new HarmonyMethod(typeof(NetFactory).GetMethod(nameof(PatchClientGetWriterWithStaticHeader), BindingFlags.Static | BindingFlags.NonPublic)));
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to patch in debug prints for GetWriterWithStaticHeader in client and server method handle.");
            Logger.LogError(ex);
        }
#endif

        const string netMessagesName = "SDG.Unturned.NetMessages";
        Type? netMessagesType;
        try
        {
            netMessagesType = typeof(Provider).Assembly.GetType(netMessagesName, true, false);
            if (netMessagesType == null)
            {
                Logger.LogError("Unable to find type " + netMessagesName + "!");
                goto reset;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Unable to find type " + netMessagesName + "!");
            Logger.LogError(ex);
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
            Logger.LogError("Unable to find " + netMessagesType.Format() + "." + fieldName + "!");
            goto reset;
        }
        Array readCallbacks = (Array)field.GetValue(null);

        Type callbackType = readCallbacks.GetType().GetElementType()!;

        Array nArr = Array.CreateInstance(callbackType, readCallbacks.Length + Listeners.Length);
        Array.Copy(readCallbacks, nArr, readCallbacks.Length);

        for (int i = 0; i < methods.Length; ++i)
        {
            try
            {
                nArr.SetValue(Listeners[i] = methods[i].CreateDelegate(callbackType), readCallbacks.Length + i);
            }
            catch (Exception ex)
            {
                Logger.LogError("Implemented method for " + (DevkitMessage)i + " can not be converted to " + callbackType.Name + ".");
                Logger.LogError(ex);
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
                Logger.LogWarning("Unable to unpatch " + readEnum.Format() + ".ReadEnum(...) while cancelling initialization.");
                Logger.LogError(ex);
            }
            if (p2)
            {
                try
                {
                    PatchesMain.Patcher.Unpatch(writeMethod, HarmonyPatchType.Prefix, PatchesMain.HarmonyId);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning("Unable to unpatch " + writeEnum.Format() + ".WriteEnum(...) while cancelling initialization.");
                    Logger.LogError(ex);
                }
            }
        }
        return false;
    }
#if DEBUG
    private static void PatchServerGetWriterWithStaticHeader(ClientMethodHandle __instance)
    {
        ClientMethodInfo? info = ServerGetMethodInfo?.Invoke(__instance);
        string name = (info == null || ServerGetMethodName == null ? null : ServerGetMethodName(info)) ?? "unknown";
        Logger.LogDebug($"[CLIENT MTD HNDL] Unturned.InvokeMessage sending: {name,-36} (Type: {__instance.GetType().Format(),-24})");
    }
    private static void PatchClientGetWriterWithStaticHeader(ServerMethodHandle __instance)
    {
        ServerMethodInfo? info = ClientGetMethodInfo?.Invoke(__instance);
        string name = (info == null || ClientGetMethodName == null ? null : ClientGetMethodName(info)) ?? "unknown";
        Logger.LogDebug($"[SERVER MTD HNDL] Unturned.InvokeMessage sending: {name,-36} (Type: {__instance.GetType().Format(),-24})");
    }
#endif
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
            MethodInfo? method = typeof(Provider).Assembly
                .GetType("SDG.Unturned.TransportConnectionListPool", true, false)?.GetMethod("Get",
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (method != null)
            {
                PullFromTransportConnectionListPool = (Func<PooledTransportConnectionList>)method.CreateDelegate(typeof(Func<PooledTransportConnectionList>));
            }
            else
            {
                Logger.LogWarning("Couldn't find Get in TransportConnectionListPool, list pooling will not be used.");
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Couldn't get Get from TransportConnectionListPool, list pooling will not be used (" + ex.Message + ").");
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
        MessageOverhead ovh = new MessageOverhead(bytes);
#if SERVER
        OnReceived(bytes, transportConnection, ovh);
#else
        OnReceived(bytes, GetPlayerTransportConnection(), ovh);
#endif
    }
    [UsedImplicitly]
    private static void ReceiveRelayPacket(
#if SERVER
        ITransportConnection transportConnection,
#endif
        NetPakReader reader)
    {
#if CLIENT
        if (!reader.ReadUInt16(out ushort len) || !reader.ReadUInt64(out ulong sender))
        {
            Logger.LogWarning("Unable to read incomming relay message from server.");
            return;
        }
        byte[] bytes = new byte[len];
        if (!reader.ReadBytes(bytes, len))
        {
            Logger.LogWarning("Unable to read incomming relay message from: " + sender + ".");
            return;
        }
        MessageOverhead ovh = new MessageOverhead(bytes, sender);
        OnReceived(bytes, GetPlayerTransportConnection(), ovh);
#endif
#if SERVER
        EditorUser? user = UserManager.FromConnection(transportConnection);
        if (user == null)
        {
            Logger.LogWarning("Unable to identify relay packet sender: " + transportConnection.GetAddressString(true) + ".");
            return;
        }

        if (!reader.ReadUInt16(out ushort len) || !reader.ReadUInt8(out byte amt))
            goto fail;
        unsafe
        {
            ulong* relays = stackalloc ulong[amt];
            for (int i = 0; i < amt; ++i)
            {
                if (!reader.ReadUInt64(out relays[i]))
                    goto fail;
            }


            byte[] msg = new byte[len];
            if (!reader.ReadBytes(msg))
                goto fail;
            byte[] output;
            fixed (byte* ptr = msg)
            {
                output = new byte[sizeof(ushort) + sizeof(ulong) + len];
                fixed (byte* ptr2 = output)
                {
                    UnsafeBitConverter.GetBytes(ptr2, len, 0);
                    UnsafeBitConverter.GetBytes(ptr2, user.SteamId.m_SteamID, sizeof(ushort));
                    Buffer.MemoryCopy(ptr, ptr2 + sizeof(ushort) + sizeof(ulong), len, len);
                }
            }

            PooledTransportConnectionList tempList;

            if (amt == 0)
            {
                tempList = GetPooledTransportConnectionList(UserManager.Users.Count - 1);
                for (int i = 0; i < UserManager.Users.Count; ++i)
                {
                    EditorUser user2 = UserManager.Users[i];
                    if (user.SteamId.m_SteamID != user2.SteamId.m_SteamID)
                        tempList.Add(user2.Connection);
                }
            }
            else
            {
                tempList = GetPooledTransportConnectionList(amt);
                for (int i = 0; i < UserManager.Users.Count; ++i)
                {
                    EditorUser user2 = UserManager.Users[i];
                    if (user.SteamId.m_SteamID != user2.SteamId.m_SteamID)
                    {
                        for (int j = 0; j < amt; ++j)
                        {
                            if (relays[j] == user2.SteamId.m_SteamID)
                            {
                                tempList.Add(user2.Connection);
                                break;
                            }
                        }
                    }
                }
            }

            Send(tempList, output);

            MessageOverhead ovh = new MessageOverhead(msg, user.SteamId.m_SteamID);
            if ((ovh.Flags & (MessageFlags.LayeredResponse | MessageFlags.LayeredRequest)) != 0)
                OnReceived(msg, transportConnection, in ovh);
        }
        return;
        fail:
        Logger.LogWarning("Unable to read incoming relay message from: " + transportConnection.GetAddressString(true) + ".");
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
                Logger.LogDebug($"Reading net message enum {num,2}: {(num <= ReceiveBlockOffset ? ("Unturned." + value) : ("DevkitServer." + (DevkitMessage)(num - ReceiveBlockOffset)))}");
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
            if (value != EClientMessage.InvokeMethod)
#else
            if (value != EServerMessage.InvokeMethod)
#endif
                Logger.LogDebug($"Writing net message enum {v, 2}: {(v < WriteBlockOffset ? ("Unturned." + value) : ("DevkitServer." + (DevkitMessage)(v - WriteBlockOffset)))}");
            __result = writer.WriteBits(v, NewWriteBitCount);
            return false;
        }

        return true;
    }

    internal static string GetInvokerName(ushort id) =>
        invokers.TryGetValue(id, out BaseNetCall bnc) ? bnc.Name : id.ToString();
    internal static T? GetInvoker<T>(ushort id) where T : BaseNetCall =>
        invokers.TryGetValue(id, out BaseNetCall bnc) ? bnc as T : null;
    internal static void OnReceived(byte[] message,
#if SERVER
        ITransportConnection
#else
        IClientTransport
#endif
            connection, in MessageOverhead overhead)
    {
        lock (sync)
        {
            if (message.Length < MessageOverhead.MinimumSize)
            {
                Logger.LogError("Received too short of a message.");
                goto rtn;
            }
            int size = overhead.Size;
            if (size > message.Length)
            {
                Logger.LogError("Message overhead read a size larger than the message payload: " + size + " bytes.");
                goto rtn;
            }
            Logger.LogDebug("Received method invocation: " + overhead + " ( in data: " + message.Length + " size: " + size + " )");
            byte[] data = new byte[size];
            Buffer.BlockCopy(message, overhead.Length, data, 0, size);
            if (invokers.TryGetValue(overhead.MessageId, out BaseNetCall call))
            {
                object[]? parameters = null;
                bool one = false;
                if (overhead.RequestKey != default)
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
                                if (lt.Caller.ID == call.ID)
                                {
                                    if (lt.Task != null && !lt.Task.isCompleted)
                                    {
                                        lock (call)
                                        {
                                            if (!call.Read(data, out parameters))
                                            {
                                                Logger.LogWarning(
                                                    $"Unable to read incoming message for message {overhead}.");
                                                goto rtn;
                                            }
                                        }

                                        object[] old = parameters;
                                        parameters = new object[old.Length + 1];
                                        parameters[0] = new MessageContext(connection, overhead);
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
                                if (lt.Caller.ID == call.ID)
                                {
                                    int? errorCode = null;
                                    if (overhead.Size == sizeof(int))
                                    {
                                        if (data.Length >= sizeof(int))
                                            errorCode = BitConverter.ToInt32(data, 0);
                                    }
                                    if (lt.Task != null && !lt.Task.isCompleted)
                                    {
                                        lt.Task.TellCompleted(new MessageContext(connection, overhead), true, errorCode);
                                    }
                                    localAckRequests.RemoveAt(l);
                                    break;
                                }
                            }
                        }
                    }
                }
                if (one && (overhead.Flags & MessageFlags.RunOriginalMethodOnRequest) != MessageFlags.RunOriginalMethodOnRequest)
                    goto rtn;
                for (int i = 0; i < registeredMethods.Count; ++i)
                {
                    if (registeredMethods[i].Attribute.MethodID == call.ID)
                    {
                        NetMethodInfo info = registeredMethods[i];
                        if (parameters == null)
                        {
                            lock (call)
                            {
                                if (!call.Read(data, out parameters))
                                {
                                    Logger.LogWarning(
                                        $"Unable to read incoming message for message {overhead}\n.");
                                    goto rtn;
                                }
                            }

                            object[] old = parameters;
                            parameters = new object[old.Length + 1];
                            parameters[0] = new MessageContext(connection, overhead);
                            Array.Copy(old, 0, parameters, 1, old.Length);
                        }
                        try
                        {
                            object res = info.Method.Invoke(null, parameters);
                            Logger.LogDebug("Invoked received method: " + info.Method.Format() + ".");
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
                                            Logger.LogError("Error running method " + info.Method.Format());
                                            Logger.LogError("Message: " + ovh + ".");
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
                        catch (ArgumentException)
                        {
                            Logger.LogError("Method " + info.Method.Format());
                            Logger.LogError("Message: " + overhead.ToString() + ".");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError("Error running method " + info.Method.Format());
                            Logger.LogError("Message: " + overhead.ToString() + ".");
                            Logger.LogError(ex);
                        }
                    }
                }
            }
            rtn:
            RemoveExpiredListeners();
        }
    }
    private static void RemoveExpiredListeners()
    {
        DateTime now = DateTime.UtcNow;
        for (int i = localAckRequests.Count - 1; i >= 0; --i)
            if ((now - localAckRequests[i].Timestamp).TotalSeconds > MaxListenTimeout)
                localAckRequests.RemoveAt(i);

        for (int i = localListeners.Count - 1; i >= 0; --i)
            if ((now - localListeners[i].Timestamp).TotalSeconds > MaxListenTimeout)
                localListeners.RemoveAt(i);
    }
    public static void Reflect(Assembly assembly, NetCallSource search)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t != null).ToArray();
            Logger.LogWarning("The following errors were encountered finding the types in assembly " + assembly.FullName + ": ");
            for (int i = 0; i < ex.LoaderExceptions.Length; ++i)
                Logger.LogWarning(ex.LoaderExceptions[i].Message);
            Logger.LogWarning("Net method registration will continue without the errored types, perhaps an assembly reference is missing that is used in that type.");
        }
        int before1 = registeredMethods.Count;
        int before2 = invokers.Count;
        ReflectMethods(types, search);
        ReflectInvokers(types);
        Logger.LogInfo(assembly.GetName().Name + " registered " + (registeredMethods.Count - before1).ToString() + " net-methods and " + (invokers.Count - before2).ToString() + " invokers.");
    }
    private static void ReflectMethods(Type[] types, NetCallSource search)
    {
        for (int i = 0; i < types.Length; ++i)
        {
            MethodInfo[] methods = types[i].GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            for (int m = 0; m < methods.Length; ++m)
            {
                MethodInfo method = methods[m];
                if (!method.IsStatic) continue;
                NetCallAttribute? attribute = method.GetCustomAttribute<NetCallAttribute>();
                if (attribute is null || attribute.Type == NetCallSource.None || !(attribute.Type == NetCallSource.FromEither || search == NetCallSource.FromEither || search == attribute.Type)) continue;
                bool found = false;
                for (int ifo = 0; ifo < registeredMethods.Count; ++ifo)
                {
                    NetMethodInfo info = registeredMethods[ifo];
                    if (info.Method.MethodHandle == method.MethodHandle)
                    {
                        found = true;
                        break;
                    }
                }
                if (found) continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length < 1)
                {
                    Logger.LogWarning($"Method {method.Format()} has no parameters. Expected first argument to be of type {nameof(MessageContext)}.");
                    continue;
                }
                else if (parameters[0].ParameterType != typeof(MessageContext))
                {
                    Logger.LogWarning($"Method {method.Format()} has an invalid first parameter: " + parameters[0].ParameterType.Format() + $". Expected first argument to be of type {nameof(MessageContext)}.");
                    continue;
                }

                registeredMethods.Add(new NetMethodInfo(method, attribute));
            }
        }
    }
    private static void ReflectInvokers(Type[] types)
    {
        for (int i = 0; i < types.Length; ++i)
        {
            FieldInfo[] fields = types[i].GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            for (int f = 0; f < fields.Length; ++f)
            {
                try
                {
                    if (fields[f].FieldType.IsSubclassOf(typeof(BaseNetCall)))
                    {
                        FieldInfo field = fields[f];
                        if (Attribute.GetCustomAttribute(field, typeof(IgnoreInvokerAttribute)) != null || field.GetValue(null) is not BaseNetCall call)
                            continue;
                        Type callType = call.GetType();
                        if (invokers.TryGetValue(call.ID, out BaseNetCall c2))
                        {
                            if (c2.GetType() != call.GetType())
                                Logger.LogWarning("Inconsistant duplicate invoker " + call.ID + " at field " + field.Format() + ".");
                            continue;
                        }
                        Type[] generics = callType.GetGenericArguments();
                        for (int index = registeredMethods.Count - 1; index >= 0; --index)
                        {
                            NetMethodInfo info = registeredMethods[index];
                            if (info.Attribute.MethodID == call.ID)
                            {
                                ParameterInfo[] parameters = info.Method.GetParameters();
                                if (parameters.Length == 0 || parameters[0].ParameterType != typeof(MessageContext))
                                {
                                    Logger.LogWarning($"Method \"{info.Method.Format()}\" has the wrong first parameter " +
                                                       $"for invoker \"{field.Format()}\". (Expected {nameof(MessageContext)}).");
                                    registeredMethods.RemoveAt(index);
                                }
                                else if (callType == typeof(NetCall))
                                {
                                    if (parameters.Length != 1)
                                    {
                                        Logger.LogWarning($"Method \"{info.Method.Format()}\" has the wrong " +
                                            $"parameters for invoker \"{field.Format()}\" ({nameof(MessageContext)}).");
                                        registeredMethods.RemoveAt(index);
                                    }
                                }
                                else if (typeof(NetCallCustom).IsAssignableFrom(callType))
                                {
                                    if (parameters.Length != 2 || parameters[1].ParameterType != typeof(ByteReader))
                                    {
                                        Logger.LogWarning($"Method \"{info.Method.Format()}\" has the wrong " +
                                                           $"parameters for invoker \"{field.Format()}\" ({nameof(MessageContext)}, {nameof(ByteReader)}).");
                                        registeredMethods.RemoveAt(index);
                                    }
                                }
                                else if (callType.IsSubclassOf(typeof(NetCallRaw)) || callType.IsSubclassOf(typeof(DynamicNetCall)))
                                {
                                    if (generics.Length == 0)
                                        continue;
                                    if (generics.Length == 1)
                                    {
                                        if (!(parameters.Length == 2 && generics[0].IsAssignableFrom(parameters[1].ParameterType)))
                                        {
                                            Logger.LogWarning($"Method  \"{info.Method.Format()}\" has the wrong " +
                                                $"parameters for invoker \"{field.Format()}\" " +
                                                $"({nameof(MessageContext)}, {(generics.Length > 0 ? generics[0].Format() : "?")}).");
                                            registeredMethods.RemoveAt(index);
                                        }
                                    }
                                    else if (parameters.Length - 1 == generics.Length)
                                    {
                                        for (int t = 0; t < generics.Length; t++)
                                        {
                                            if (!generics[t].IsAssignableFrom(parameters[t + 1].ParameterType))
                                            {
                                                Logger.LogWarning($"Method  \"{info.Method.Format()}\" has the wrong " +
                                                    $"parameters for invoker \"{field.Format()}\" " +
                                                    $"({nameof(MessageContext)}, {string.Join(", ", generics.Select(x => x.Format()))}).");
                                                registeredMethods.RemoveAt(index);
                                                break;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        Logger.LogWarning($"Method  \"{info.Method.Format()}\" has the wrong " +
                                            $"parameters for invoker \"{field.Format()}\" " +
                                            $"({nameof(MessageContext)}, {string.Join(", ", generics.Select(x => x.Format()))}).");
                                        registeredMethods.RemoveAt(index);
                                    }
                                }
                            }
                        }
                        call.Name = field.Name;
                        call.SetThrowOnError(true);
                        invokers.Add(call.ID, call);
                    }
                }
                catch (TypeLoadException)
                {
                    // ignored
                }
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
    private readonly struct NetMethodInfo
    {
        public readonly MethodInfo Method;
        public readonly NetCallAttribute Attribute;
        public NetMethodInfo(MethodInfo method, NetCallAttribute attribute)
        {
            Method = method;
            Attribute = attribute;
        }
    }
#if CLIENT
    public static void SendRelay(byte[] bytes, bool reliable = true, IList<ulong>? users = null)
    {
        ThreadUtil.assertIsGameThread();

        Writer.Reset();
        Writer.WriteEnum((EServerMessage)(WriteBlockOffset + (int)DevkitMessage.RelayPacket));
        int len = Math.Min(ushort.MaxValue, bytes.Length);
        Writer.WriteUInt16((ushort)len);

        int c = Math.Min(byte.MaxValue, users == null ? 0 : users.Count);
        Writer.WriteUInt8((byte)c);
        for (int i = 0; i < c; ++i)
            Writer.WriteUInt64(users![i]);

        Writer.WriteBytes(bytes, len);
        Writer.Flush();
#if DEBUG
        Logger.LogInfo("Sending " + bytes.Length + " bytes to server as a relay to " +
                       (users == null ? "all" : users.Count.ToString(CultureInfo.InvariantCulture)) + " user(s).");
#endif
        GetPlayerTransportConnection().Send(Writer.buffer, Writer.writeByteIndex, reliable ? ENetReliability.Reliable : ENetReliability.Unreliable);
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
        Writer.Flush();
#if DEBUG
        Logger.LogInfo("Sending " + bytes.Length + " bytes to " +
#if SERVER
        connection.GetAddressString(true)
#else
        "server"
#endif
        );
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
        Logger.LogInfo("Sending " + bytes.Length + " bytes to " + connections.Count + " user(s).");
        for (int i = 0; i < connections.Count; ++i)
            connections[i].Send(Writer.buffer, Writer.writeByteIndex, reliable ? ENetReliability.Reliable : ENetReliability.Unreliable);
    }
#endif
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

[MeansImplicitUse]
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class NetCallAttribute : Attribute
{
    readonly NetCallSource type;
    readonly ushort methodId;
    public NetCallAttribute(NetCallSource type, ushort methodId)
    {
        this.type = type;
        this.methodId = methodId;
    }
    public NetCallSource Type => type;
    public ushort MethodID => methodId;
}

[AttributeUsage(AttributeTargets.Field)]
public sealed class IgnoreInvokerAttribute : Attribute { }
public enum NetCallSource : byte
{
    FromServer = 0,
    FromClient = 1,
    FromEither = 2,
    None = 3
}
public delegate void SentMessage(ITransportConnection connection, in MessageOverhead overhead, byte[] message);
public delegate void ReceivedMessage(ITransportConnection connection, in MessageOverhead overhead, byte[] message);
public delegate void ClientConnected(ITransportConnection connection);
public delegate void ClientDisconnected(ITransportConnection connection);