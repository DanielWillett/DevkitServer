using DevkitServer.API;
using DevkitServer.API.Multiplayer;
using HarmonyLib;
using SDG.NetTransport.SteamNetworkingSockets;
using System.Globalization;
using System.Reflection;

namespace DevkitServer.Patches;
[HarmonyPatch]
public static class TransportPatcher
{
    private static readonly CachedMulticastEvent<ConnectionArgs> EventOnTransportConnectionCreated = new CachedMulticastEvent<ConnectionArgs>(typeof(TransportPatcher), nameof(OnTransportConnectionCreated));
    private static readonly CachedMulticastEvent<ConnectionArgs> EventOnTransportConnectionDestroyed = new CachedMulticastEvent<ConnectionArgs>(typeof(TransportPatcher), nameof(OnTransportConnectionDestroyed));

    internal const string Source = "TRANSPORT PATCHES";

    /// <summary>
    /// Transport tag used to tell the client to use <see cref="TransportType"/> instead of the default.
    /// </summary>
    public const string DevkitServerTransportTag = "ds";

    /// <summary>
    /// Steamworks Networking Sockets <see cref="ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendBufferSize"/> in bytes.
    /// </summary>
    public const int SendBufferSize =
#if SERVER
        2097152;
#else
        1048576;
#endif

    /// <summary>
    /// Called after a transport connection first gets connected.
    /// </summary>
    public static event ConnectionArgs OnTransportConnectionCreated
    {
        add => EventOnTransportConnectionCreated.Add(value);
        remove => EventOnTransportConnectionCreated.Remove(value);
    }

    /// <summary>
    /// Called after a transport connection has been completely disconnected.
    /// </summary>
    /// <remarks>No data can be sent to the connection during this event.</remarks>
    public static event ConnectionArgs OnTransportConnectionDestroyed
    {
        add => EventOnTransportConnectionDestroyed.Add(value);
        remove => EventOnTransportConnectionDestroyed.Remove(value);
    }

    internal static void ManualPatch()
    {
#if SERVER
        // ServerTransport_SteamNetworkingSockets.HandleState_Connected
        try
        {
            MethodInfo? method = typeof(ServerTransport_SteamNetworkingSockets).GetMethod("HandleState_Connected", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (method != null)
            {
                PatchesMain.Patcher.Patch(method, postfix: Accessor.GetHarmonyMethod(PostFixOnTransportConnectionEstablished));
                Logger.LogDebug($"[{Source}] Postfixed {method.Format()} to add an event for when transport connections are established.");
            }
            else Logger.LogWarning("Method not found: ServerTransport_SteamNetworkingSockets.HandleState_Connected.", method: Source);
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Patcher unpatching error: ServerTransport_SteamNetworkingSockets.HandleState_Connected.", method: Source);
            Logger.LogError(ex, method: Source);
        }

        // ServerTransport_SteamNetworkingSockets.CloseConnection
        try
        {
            MethodInfo? method = typeof(ServerTransport_SteamNetworkingSockets).GetMethod("CloseConnection", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (method != null)
            {
                PatchesMain.Patcher.Patch(method, postfix: Accessor.GetHarmonyMethod(PostFixOnTransportConnectionClosed));
                Logger.LogDebug($"[{Source}] Postfixed {method.Format()} to add an event for when transport connections are disposed.");
            }
            else Logger.LogWarning("Method not found: ServerTransport_SteamNetworkingSockets.CloseConnection.", method: Source);
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Patcher unpatching error: ServerTransport_SteamNetworkingSockets.CloseConnection.", method: Source);
            Logger.LogError(ex, method: Source);
        }
#elif CLIENT
        // ClientTransport_SteamNetworkingSockets.HandleState_Connected
        try
        {
            MethodInfo? method = typeof(ClientTransport_SteamNetworkingSockets).GetMethod("HandleState_Connected", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (method != null)
            {
                PatchesMain.Patcher.Patch(method, postfix: Accessor.GetHarmonyMethod(PostFixOnTransportConnectionEstablished));
                Logger.LogDebug($"[{Source}] Postfixed {method.Format()} to add an event for when the client transport connection is established.");
            }
            else Logger.LogWarning("Method not found: ClientTransport_SteamNetworkingSockets.HandleState_Connected.", method: Source);
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Patching error: ClientTransport_SteamNetworkingSockets.HandleState_Connected.", method: Source);
            Logger.LogError(ex, method: Source);
        }

        // ClientTransport_SteamNetworkingSockets.TearDown
        try
        {
            MethodInfo? method = typeof(ClientTransport_SteamNetworkingSockets).GetMethod(nameof(ClientTransport_SteamNetworkingSockets.TearDown), BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (method != null)
            {
                PatchesMain.Patcher.Patch(method, postfix: Accessor.GetHarmonyMethod(PostFixOnTransportConnectionClosed));
                Logger.LogDebug($"[{Source}] Postfixed {method.Format()} to add an event for when the client transport connection is disposed.");
            }
            else Logger.LogWarning("Method not found: ClientTransport_SteamNetworkingSockets.TearDown.", method: Source);
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Patching error: ClientTransport_SteamNetworkingSockets.TearDown.", method: Source);
            Logger.LogError(ex, method: Source);
        }
#endif
    }

    internal static void ManualUnpatch()
    {
#if SERVER
        // ServerTransport_SteamNetworkingSockets.HandleState_Connected
        try
        {
            MethodInfo? method = typeof(ServerTransport_SteamNetworkingSockets).GetMethod("HandleState_Connected", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (method != null)
            {
                PatchesMain.Patcher.Patch(method, postfix: Accessor.GetHarmonyMethod(PostFixOnTransportConnectionEstablished));
                Logger.LogDebug($"[{Source}] Postfixed {method.Format()} to add an event for when transport connections are established.");
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Patcher unpatching error: ServerTransport_SteamNetworkingSockets.HandleState_Connected.", method: Source);
            Logger.LogError(ex, method: Source);
        }

        // ServerTransport_SteamNetworkingSockets.CloseConnection
        try
        {
            MethodInfo? method = typeof(ServerTransport_SteamNetworkingSockets).GetMethod("CloseConnection", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (method != null)
            {
                PatchesMain.Patcher.Patch(method, postfix: Accessor.GetHarmonyMethod(PostFixOnTransportConnectionClosed));
                Logger.LogDebug($"[{Source}] Postfixed {method.Format()} to add an event for when transport connections are disposed.");
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Patcher unpatching error: ServerTransport_SteamNetworkingSockets.CloseConnection.", method: Source);
            Logger.LogError(ex, method: Source);
        }
#elif CLIENT
        // ClientTransport_SteamNetworkingSockets.HandleState_Connected
        try
        {
            MethodInfo? method = typeof(ClientTransport_SteamNetworkingSockets).GetMethod("HandleState_Connected", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (method != null)
            {
                PatchesMain.Patcher.Patch(method, postfix: Accessor.GetHarmonyMethod(PostFixOnTransportConnectionEstablished));
                Logger.LogDebug($"[{Source}] Postfixed {method.Format()} to add an event for when the client transport connection is established.");
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Patcher unpatching error: ClientTransport_SteamNetworkingSockets.HandleState_Connected.", method: Source);
            Logger.LogError(ex, method: Source);
        }

        // ClientTransport_SteamNetworkingSockets.TearDown
        try
        {
            MethodInfo? method = typeof(ClientTransport_SteamNetworkingSockets).GetMethod(nameof(ClientTransport_SteamNetworkingSockets.TearDown), BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (method != null)
            {
                PatchesMain.Patcher.Patch(method, postfix: Accessor.GetHarmonyMethod(PostFixOnTransportConnectionClosed));
                Logger.LogDebug($"[{Source}] Postfixed {method.Format()} to add an event for when the client transport connection is disposed.");
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Patcher unpatching error: ClientTransport_SteamNetworkingSockets.TearDown.", method: Source);
            Logger.LogError(ex, method: Source);
        }
#endif
    }

    /// <summary>
    /// Type to use for transport when <see cref="DevkitServerModule.IsEditing"/> is <see langword="true"/>.
    /// </summary>
    /// <remarks>Defaults to <see cref="DevkitServerSteamNetworkingSocketsTransport"/>.</remarks>
    public static Type? TransportType { get; set; } = typeof(DevkitServerSteamNetworkingSocketsTransport);

#if SERVER
    private static readonly StaticGetter<CommandLineString>? GetNetTransportFlag =
        Accessor.GenerateStaticGetter<CommandLineString>(Accessor.AssemblyCSharp.GetType("SDG.Unturned.NetTransportFactory", false)!, "clImpl");
    internal static void WarnUnsupportedTransport()
    {
        if (GetNetTransportFlag?.Invoke() is { hasValue: true, value: { } flag } && !flag.Equals("SteamNetworkingSockets", StringComparison.OrdinalIgnoreCase) && !flag.Equals(DevkitServerModule.ModuleName, StringComparison.OrdinalIgnoreCase))
        {
            Logger.LogWarning($"Unable to use specified transport type: \"{"-NetTransport".Colorize(ConsoleColor.White)} {flag.Colorize(ConsoleColor.Red)}\", only \"{"-NetTransport".Colorize(ConsoleColor.White)} {"SteamNetworkingSockets".Colorize(DevkitServerModule.UnturnedColor)}\" or \"{"-NetTransport".Colorize(ConsoleColor.White)} {DevkitServerModule.ModuleName.Colorize(DevkitServerModule.ModuleColor)}\" is supported.", method: Source);
            TransportType = null;
        }
        else
        {
            Logger.LogInfo($"[{Source}] Verified valid transport command line argument: \"{"-NetTransport".Colorize(ConsoleColor.White)} {(GetNetTransportFlag?.Invoke() is { hasValue: true, value: { } flag2 } ? flag2 : "default".Colorize(ConsoleColor.DarkGray))}\".");
        }
    }
#endif

#if SERVER
    [HarmonyPatch("NetTransportFactory", "CreateServerTransport")]
#else
    [HarmonyPatch("NetTransportFactory", "CreateClientTransport")]
#endif
    [HarmonyPrefix]
    [UsedImplicitly]
    private static bool OnCreatingTransport(
#if CLIENT
        string tag, ref IClientTransport __result
#else
        ref IServerTransport __result
#endif
        )
    {
#if SERVER
        if (GetNetTransportFlag?.Invoke() is { hasValue: true, value: { } flag } && flag.Equals(DevkitServerModule.ModuleName, StringComparison.OrdinalIgnoreCase))
        {
            __result = new DevkitServerSteamNetworkingSocketsTransport();
            return false;
        }
        WarnUnsupportedTransport();
#endif

#if SERVER
        if (TransportType != null)
#else
        if (TransportType != null && DevkitServerTransportTag.Equals(tag, StringComparison.Ordinal))
#endif
        {
            __result =
#if SERVER
                (IServerTransport)
#else
                (IClientTransport)
#endif
                Activator.CreateInstance(TransportType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.CreateInstance,
                null, Array.Empty<object>(), CultureInfo.CurrentCulture, null);
            return false;
        }

        return true;
    }

#if SERVER
    [HarmonyPatch("NetTransportFactory", "GetTag")]
    [HarmonyPrefix]
    [UsedImplicitly]
    private static bool OnGettingTag(IServerTransport serverTransport, ref string __result)
    {
        if (serverTransport is DevkitServerSteamNetworkingSocketsTransport)
        {
            __result = DevkitServerTransportTag;
            return false;
        }

        return true;
    }
#endif
#if SERVER
    private static readonly InstanceGetter<ServerTransport_SteamNetworkingSockets, IList>? GetTransportConnections = Accessor.GenerateInstanceGetter<ServerTransport_SteamNetworkingSockets, IList>("transportConnections", throwOnError: false); 
    private static void PostFixOnTransportConnectionEstablished(ServerTransport_SteamNetworkingSockets __instance, ref SteamNetConnectionStatusChangedCallback_t callback)
    {
        ref HSteamNetConnection handle = ref callback.m_hConn;
        ref SteamNetworkingIPAddr addr = ref callback.m_info.m_addrRemote;

        Logger.LogDebug($"[{Source}] Transport connection created: {Parser.getIPFromUInt32(addr.GetIPv4()).ColorizeNoReset(new Color32(204, 255, 102, 255))}{":".ColorizeNoReset(FormattingColorType.Punctuation)}{addr.m_port.ToString(CultureInfo.InvariantCulture).Colorize(new Color32(170, 255, 0, 255))}.");
        if (__instance == null || GetTransportConnections == null)
            return;

        // find latest added connection that matches the steamworks handle.
        IList list = GetTransportConnections(__instance);
        ITransportConnection? connection = null;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            connection = (ITransportConnection)list[i];

            if (connection.GetHashCode() == handle.GetHashCode())
                break;
        }

        if (connection == null)
        {
            Logger.LogWarning($"Unknown transport connection with handle {handle.m_HSteamNetConnection.Format("X8")}.", method: Source);
            return;
        }

        EventOnTransportConnectionCreated.TryInvoke(connection);
    }
    private static void PostFixOnTransportConnectionClosed(ITransportConnection transportConnection)
    {
        CustomNetMessageListeners.RemoteMappings.Remove(transportConnection, out _);

        EventOnTransportConnectionDestroyed?.TryInvoke(transportConnection);
        Logger.LogDebug($"[{Source}] Transport connection torn down: {transportConnection.Format()}.");
    }
#elif CLIENT
    private static void PostFixOnTransportConnectionEstablished(ClientTransport_SteamNetworkingSockets __instance, ref SteamNetConnectionStatusChangedCallback_t callback)
    {
        if (DevkitServerModule.IsEditing)
        {
            CustomNetMessageListeners.AreLocalMappingsDirty = true;
            CustomNetMessageListeners.SendLocalMappings();
        }
        else
            Logger.LogDebug($"[{Source}] Client transport connection either connected to not a DevkitServer server or is unable to tell as of yet.");

        EventOnTransportConnectionCreated.TryInvoke(__instance);
    }
    private static void PostFixOnTransportConnectionClosed(ClientTransport_SteamNetworkingSockets __instance)
    {
        Logger.LogDebug($"[{Source}] Client transport connection torn down.");

        EventOnTransportConnectionDestroyed?.TryInvoke(__instance);
    }
#endif
}

public class DevkitServerSteamNetworkingSocketsTransport
#if SERVER
 : ServerTransport_SteamNetworkingSockets
#else
 : ClientTransport_SteamNetworkingSockets
#endif
{
    internal DevkitServerSteamNetworkingSocketsTransport() { }
    protected override List<SteamNetworkingConfigValue_t> BuildDefaultConfig()
    {
        List<SteamNetworkingConfigValue_t> @base = base.BuildDefaultConfig();
        @base.Add(new SteamNetworkingConfigValue_t
        {
            m_eDataType = ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32,
            m_eValue = ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendBufferSize,
            m_val = new SteamNetworkingConfigValue_t.OptionValue
            {
                m_int32 = TransportPatcher.SendBufferSize
            }
        });
        Logger.LogInfo($"[{TransportPatcher.Source}] Set transport buffer size to {DevkitServerUtility.FormatBytes(TransportPatcher.SendBufferSize).Colorize(DevkitServerModule.UnturnedColor)}.");
        return @base;
    }
}