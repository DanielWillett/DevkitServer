using HarmonyLib;
using JetBrains.Annotations;
using SDG.NetTransport.SteamNetworkingSockets;
using System.Globalization;
using System.Reflection;

namespace DevkitServer.Patches;
[HarmonyPatch]
public static class TransportPatcher
{
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
            Logger.LogWarning($"Unable to use specified transport type: \"{"-NetTransport".Colorize(ConsoleColor.White)} {flag.Colorize(ConsoleColor.Red)}\", only \"{"-NetTransport".Colorize(ConsoleColor.White)} {"SteamNetworkingSockets".Colorize(DevkitServerModule.UnturnedColor)}\" or \"{"-NetTransport".Colorize(ConsoleColor.White)} {DevkitServerModule.ModuleName.Colorize(DevkitServerModule.ModuleColor)}\" is supported.");
            TransportType = null;
        }
        else
        {
            Logger.LogInfo($"Verified valid transport command line argument: \"{"-NetTransport".Colorize(ConsoleColor.White)} {(GetNetTransportFlag?.Invoke() is { hasValue: true, value: { } flag2 } ? flag2 : "default".Colorize(ConsoleColor.DarkGray))}\".");
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
        Logger.LogInfo($"Set transport buffer size to {DevkitServerUtility.FormatBytes(TransportPatcher.SendBufferSize).Colorize(DevkitServerModule.UnturnedColor)}.");
        return @base;
    }
}