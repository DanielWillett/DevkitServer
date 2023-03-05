using DevkitServer.Patches;
using DevkitServer.Util.Encoding;
using HarmonyLib;
using JetBrains.Annotations;
using SDG.NetPak;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DevkitServer.Multiplayer;
public static class NetFactory
{
    public const EClientMessage ClientMessageType = (EClientMessage)18;
    public const EServerMessage ServerMessageType = (EServerMessage)9;
    private static readonly List<Listener> localListeners = new List<Listener>(16);
    private static readonly List<Listener> localAckRequests = new List<Listener>(16);
    private static readonly List<NetMethodInfo> registeredMethods = new List<NetMethodInfo>(16);
    private static readonly Dictionary<ushort, BaseNetCall> invokers = new Dictionary<ushort, BaseNetCall>(16);
    /// <summary>The maximum amount of time in seconds a listener is guaranteed to stay active.</summary>
    public const double MaxListenTimeout = 60d;
    private static readonly object sync = new object();
    private static readonly NetPakWriter Writer = new NetPakWriter();
#if CLIENT
    internal static StaticGetter<IClientTransport> GetPlayerTransportConnection =
        Accessor.GenerateStaticGetter<Provider, IClientTransport>("clientTransport", BindingFlags.NonPublic);
#endif
    internal static void Init()
    {
        Writer.buffer = Block.buffer;
        Reflect(Assembly.GetExecutingAssembly(),
#if SERVER
            NetCallSource.FromClient
#else
            NetCallSource.FromServer
#endif
        );
        PatchesMain.Patcher.Patch(
#if SERVER
            typeof(EServerMessage_NetEnum).GetMethod("ReadEnum", BindingFlags.Static | BindingFlags.Public)
#else
            typeof(EClientMessage_NetEnum).GetMethod("ReadEnum", BindingFlags.Static | BindingFlags.Public)
#endif
            , prefix: new HarmonyMethod(typeof(NetFactory).GetMethod("EnumPatch", BindingFlags.Static | BindingFlags.NonPublic)));

        Type nm = typeof(Provider).Assembly.GetType("SDG.Unturned.NetMessages");
        RuntimeHelpers.RunClassConstructor(nm.TypeHandle);

        // add network listener to array
        
#if SERVER
        FieldInfo field = nm.GetField("serverReadCallbacks", BindingFlags.NonPublic | BindingFlags.Static);
#else
        FieldInfo field = nm.GetField("clientReadCallbacks", BindingFlags.NonPublic | BindingFlags.Static);
#endif
        Array readCallbacks = (Array)field.GetValue(null);
        Array nArr = Array.CreateInstance(readCallbacks.GetType().GetElementType(), readCallbacks.Length + 1);
        Array.Copy(readCallbacks, nArr, readCallbacks.Length);

        nArr.SetValue(typeof(NetFactory)
            .GetMethod("ReceiveMessage", BindingFlags.NonPublic | BindingFlags.Static)
            .CreateDelegate(readCallbacks.GetType().GetElementType()), nArr.Length - 1);
        field.SetValue(null, nArr);
    }

#if SERVER
    [UsedImplicitly]
    private static void ReceiveMessage(ITransportConnection transportConnection, NetPakReader reader)
    {   
        reader.ReadUInt16(out ushort len);
        byte[] bytes = new byte[len];
        reader.ReadBytes(bytes, len);
        MessageOverhead ovh = new MessageOverhead(bytes);
        OnReceived(bytes, transportConnection, ovh);
    }
    [UsedImplicitly]
    private static bool EnumPatch(NetPakReader reader, ref EServerMessage value, ref bool __result)
    {
        bool flag = reader.ReadBits(4, out uint num);
        if (num is <= 8u or (uint)ServerMessageType)
        {
            value = (EServerMessage)num;
            __result = flag;
        }
        else
        {
            value = default;
            __result = false;
        }
        return false;
    }
#else
    [UsedImplicitly]
    private static void ReceiveMessage(NetPakReader reader)
    {
        reader.ReadUInt16(out ushort len);
        byte[] bytes = new byte[len];
        reader.ReadBytes(bytes, len);
        MessageOverhead ovh = new MessageOverhead(bytes);
        OnReceived(bytes, GetPlayerTransportConnection(), ovh);
    }
    [UsedImplicitly]
    private static bool EnumPatch(NetPakReader reader, ref EClientMessage value, ref bool __result)
    {
        bool flag = reader.ReadBits(5, out uint num);
        if (num is <= 17u or (uint)ClientMessageType)
        {
            value = (EClientMessage)num;
            __result = flag;
        }
        else
        {
            value = default;
            __result = false;
        }
        return false;
    }
#endif

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
                                            Logger.LogError("Error running async method " + info.Method.DeclaringType!.Namespace + "." + info.Method.DeclaringType.Name + "." + info.Method.Name + ":");
                                            Logger.LogError("Message: " + ovh.ToString() + ".");
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
                            Logger.LogError("Method " + info.Method.DeclaringType!.Namespace + "." + info.Method.DeclaringType.Name + "." + info.Method.Name + " doesn't have the correct signature for " + call.GetType().Name);
                            Logger.LogError("Message: " + overhead.ToString() + ".");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError("Error running method " + info.Method.DeclaringType!.Namespace + "." + info.Method.DeclaringType.Name + "." + info.Method.Name + ":");
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
            for (int i = 0; i < ex.LoaderExceptions.Count(); ++i)
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
                    Logger.LogWarning($"Method {method.Name} at {method.DeclaringType.Namespace}.{method.DeclaringType.Name} has no parameters. Expected first argument to be of type {nameof(MessageContext)}.");
                    continue;
                }
                else if (parameters[0].ParameterType != typeof(MessageContext))
                {
                    Logger.LogWarning($"Method {method.Name} at {method.DeclaringType.Namespace}.{method.DeclaringType.Name} has an invalid first parameter: " + parameters[0].ParameterType.FullName + $". Expected first argument to be of type {nameof(MessageContext)}.");
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
                                Logger.LogWarning("Inconsistant duplicate invoker " + call.ID + " at field " + field.Name + " at " + field.DeclaringType.Namespace + "." + field.DeclaringType.Name);
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
                                    Logger.LogWarning($"Method \"{info.Method.DeclaringType.Namespace}.{info.Method.DeclaringType.Name}.{info.Method.Name}\" has the wrong first parameter " +
                                                       $"for invoker \"{field.DeclaringType.Namespace}.{field.DeclaringType.Name}.{field.Name}\". (Expected {nameof(MessageContext)}).");
                                    registeredMethods.RemoveAt(index);
                                }
                                else if (callType == typeof(NetCall))
                                {
                                    if (parameters.Length != 1)
                                    {
                                        Logger.LogWarning($"Method \"{info.Method.DeclaringType.Namespace}.{info.Method.DeclaringType.Name}.{info.Method.Name}\" has the wrong " +
                                            $"parameters for invoker \"{field.DeclaringType.Namespace}.{field.DeclaringType.Name}.{field.Name}\" ({nameof(MessageContext)}).");
                                        registeredMethods.RemoveAt(index);
                                    }
                                }
                                else if (typeof(NetCallCustom).IsAssignableFrom(callType))
                                {
                                    if (parameters.Length != 2 || parameters[1].ParameterType != typeof(ByteReader))
                                    {
                                        Logger.LogWarning($"Method \"{info.Method.DeclaringType.Namespace}.{info.Method.DeclaringType.Name}.{info.Method.Name}\" has the wrong " +
                                                           $"parameters for invoker \"{field.DeclaringType.Namespace}.{field.DeclaringType.Name}.{field.Name}\" ({nameof(MessageContext)}, {nameof(ByteReader)}).");
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
                                            Logger.LogWarning($"Method  \"{info.Method.DeclaringType.Namespace}.{info.Method.DeclaringType.Name}.{info.Method.Name}\" has the wrong " +
                                                $"parameters for invoker \"{field.DeclaringType.Namespace}.{field.DeclaringType.Name}.{field.Name}\" " +
                                                $"({nameof(MessageContext)}, {(generics.Length > 0 ? generics[0].Name : "?")}).");
                                            registeredMethods.RemoveAt(index);
                                        }
                                    }
                                    else if (parameters.Length - 1 == generics.Length)
                                    {
                                        for (int t = 0; t < generics.Length; t++)
                                        {
                                            if (!generics[t].IsAssignableFrom(parameters[t + 1].ParameterType))
                                            {
                                                Logger.LogWarning($"Method  \"{info.Method.DeclaringType.Namespace}.{info.Method.DeclaringType.Name}.{info.Method.Name}\" has the wrong " +
                                                    $"parameters for invoker \"{field.DeclaringType.Namespace}.{field.DeclaringType.Name}.{field.Name}\" " +
                                                    $"({nameof(MessageContext)}, {string.Join(", ", generics.Select(x => x.Name))}).");
                                                registeredMethods.RemoveAt(index);
                                                break;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        Logger.LogWarning($"Method  \"{info.Method.DeclaringType.Namespace}.{info.Method.DeclaringType.Name}.{info.Method.Name}\" has the wrong " +
                                            $"parameters for invoker \"{field.DeclaringType.Namespace}.{field.DeclaringType.Name}.{field.Name}\" " +
                                            $"({nameof(MessageContext)}, {string.Join(", ", generics.Select(x => x.Name))}).");
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
                    continue;
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
    public static void Send(this
#if SERVER
        ITransportConnection
#else
        IClientTransport
#endif
        connection, byte[] bytes)
    {
        Writer.Reset();
#if SERVER
        Writer.WriteEnum(ClientMessageType);
#else
        Writer.WriteEnum(ServerMessageType);
#endif
        Writer.WriteUInt16((ushort)bytes.Length);
        Writer.WriteBytes(bytes);
        Writer.Flush();
        Logger.LogInfo("Sending " + bytes.Length + " bytes to " +
#if SERVER
        connection.GetAddressString(true)
#else
        "server"
#endif
        );
        connection.Send(Writer.buffer, Writer.writeByteIndex, ENetReliability.Reliable);
    }
#if SERVER
    public static void Send(this IEnumerable<ITransportConnection> connections, byte[] bytes)
    {
        Writer.Reset();
#if SERVER
        Writer.WriteEnum(ClientMessageType);
#else
        Writer.WriteEnum(ServerMessageType);
#endif
        Writer.WriteUInt16((ushort)bytes.Length);
        Writer.WriteBytes(bytes);
        Writer.Flush();
        Logger.LogInfo("Sending " + bytes.Length + " bytes.");
        foreach (ITransportConnection connection in connections)
            connection.Send(Writer.buffer, Writer.writeByteIndex, ENetReliability.Reliable);
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