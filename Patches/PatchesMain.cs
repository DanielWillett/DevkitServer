#define REFLECTION_TOOLS_ENABLE_HARMONY_LOG

using Cysharp.Threading.Tasks;
using DanielWillett.ReflectionTools;
using DevkitServer.API;
using DevkitServer.Configuration;
using DevkitServer.Levels;
using DevkitServer.Multiplayer;
using DevkitServer.Players;
using HarmonyLib;
using SDG.Framework.Landscapes;
using SDG.NetPak;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using Version = System.Version;
#if CLIENT
using DevkitServer.API.UI;
using DevkitServer.API.Multiplayer;
using DevkitServer.Multiplayer.Levels;
using DevkitServer.Util.Encoding;
#endif
#if SERVER
using DevkitServer.Multiplayer.Networking;
using System.Globalization;
using Unturned.SystemEx;
#endif


namespace DevkitServer.Patches;
[HarmonyPatch]
[EarlyTypeInit]
internal static class PatchesMain
{
    public const string HarmonyId = "dw.devkitserver";
    private const string Source = "PATCHING";
    private static Harmony? _patcher;
    internal static bool FailedRichPresencePatch = true;
    internal static Local RichPresenceLocalizationOverride = null!;

    public static Harmony Patcher
    {
        get => _patcher ??= new Harmony(HarmonyId);
        internal set => _patcher = value;
    }
    internal static void EarlyInitPatcher()
    {
        string path = Path.Combine(UnturnedPaths.RootDirectory.FullName, "Logs");
        Directory.CreateDirectory(path);

        path = Path.Combine(path, "harmony.log");
        HarmonyLog.ResetConditional(path, enableDebug: true);

        Patcher = new Harmony(HarmonyId);
    }
    internal static void Init()
    {
        Logger.DevkitServer.LogInfo(Source, "Patching game code...");

        try
        {
            Stopwatch sw = Stopwatch.StartNew();
            Patcher.PatchAll();
#if SERVER
            ServerGizmoPatches.Patch();
            ClientAssetIntegrityPatches.Patch();
#elif CLIENT
            LevelObjectPatches.OptionalPatches();
            SpawnsEditorPatches.ManualPatches();
            RoadsPatches.OptionalPatches();
            NavigationPatches.OptionalPatches();
            LightingPatches.DoPatching();
#endif
            TransportPatcher.ManualPatch();
            DoManualPatches();
            sw.Stop();
            Logger.DevkitServer.LogInfo(Source, $"Finished patching {"Unturned".Colorize(DevkitServerModule.UnturnedColor)} ({(sw.GetElapsedMilliseconds() / 1000d).ToString("0.0000").Colorize(FormattingUtil.NumberColor)} seconds).");
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(Source, ex, "Patch error");
            DevkitServerModule.Fault();
            Unpatch();
        }
    }
    internal static void Unpatch()
    {
        try
        {
            TransportPatcher.ManualUnpatch();
#if CLIENT
            LightingPatches.DoUnpatching();
#elif SERVER
            ClientAssetIntegrityPatches.Unpatch();
#endif
            DoManualUnpatches();
            Patcher.UnpatchAll(HarmonyId);
            Logger.DevkitServer.LogInfo(Source, $"Finished unpatching {"Unturned".Colorize(DevkitServerModule.UnturnedColor)}.");
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(Source, ex, "Unpatch error.");
        }
    }
    private static void DoManualPatches()
    {
#if SERVER
        Type? handlerType = null;
        try
        {
            handlerType = AccessorExtensions.AssemblyCSharp.GetType("SDG.Unturned.ServerMessageHandler_GetWorkshopFiles", false, false);
            if (handlerType == null)
                Logger.DevkitServer.LogWarning(Source, $"Type not found: {"ServerMessageHandler_GetWorkshopFiles".Colorize(FormattingColorType.Class)}. Can't hide map name from joining players.");
            else if (handlerType.GetMethod("ReadMessage", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) is { } method)
            {
                Patcher.Patch(method, transpiler: Accessor.Active.GetHarmonyMethod(TranspileWorkshopRequest));

                if (Accessor.Active.TryGetLambdaMethod(method, out MethodInfo writeMethod, [ typeof(NetPakWriter) ], null))
                {
                    Patcher.Patch(writeMethod, transpiler: Accessor.Active.GetHarmonyMethod(TranspileWorkshopRequestResponse));
                }
                else
                {
                    Logger.DevkitServer.LogWarning(Source, $"Lambda method not found in {method.Format()}. Player's won't be able to join via server code.");
                    DevkitServerModule.Fault();
                }
            }
            else
                Logger.DevkitServer.LogWarning(Source, $"Method not found: {FormattingUtil.FormatMethod(typeof(void), handlerType, "ReadMessage",
                    [(typeof(ITransportConnection), "transportConnection"), (typeof(NetPakReader), "reader")],
                    isStatic: true)}. Can't hide map from joining players (by checking password before sending mods).");

        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogWarning(Source, ex, $"Failed to patch method: {FormattingUtil.FormatMethod(typeof(void), handlerType, "ReadMessage",
                [(typeof(ITransportConnection), "transportConnection"), (typeof(NetPakReader), "reader")],
                isStatic: true)}. Can't hide map from joining players (by checking password before sending mods).");
        }
#elif CLIENT
        Type? handlerType = null;
        try
        {
            handlerType = AccessorExtensions.AssemblyCSharp.GetType("SDG.Unturned.ClientMessageHandler_DownloadWorkshopFiles", false, false);
            if (handlerType == null)
                Logger.DevkitServer.LogWarning(Source, $"Type not found: {"ClientMessageHandler_DownloadWorkshopFiles".Colorize(FormattingColorType.Class)}. Can't hide map name from joining players.");
            else if (handlerType.GetMethod("ReadMessage", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) is { } method)
            {
                Patcher.Patch(method, transpiler: Accessor.Active.GetHarmonyMethod(TranspileWorkshopResponse));
            }
            else
                Logger.DevkitServer.LogWarning(Source, $"Method not found: {FormattingUtil.FormatMethod(typeof(void), handlerType, "ReadMessage",
                    [(typeof(ITransportConnection), "transportConnection"), (typeof(NetPakReader), "reader")],
                    isStatic: true)}. May have issues joining the server, especially with server codes.");
        
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogWarning(Source, ex, $"Failed to patch method: {FormattingUtil.FormatMethod(typeof(void), handlerType, "ReadMessage",
                [(typeof(ITransportConnection), "transportConnection"), (typeof(NetPakReader), "reader")],
                isStatic: true)}. Can't hide map from joining players (by checking password before sending mods).");
        }
        try
        {
            if (typeof(Provider).GetMethod("onClientTransportReady", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) is { } definingMethod)
            {
                if (Accessor.Active.TryGetLambdaMethod(definingMethod, out MethodInfo lambdaMethod, [ typeof(NetPakWriter) ]))
                    Patcher.Patch(lambdaMethod, transpiler: Accessor.Active.GetHarmonyMethod(TranspileWriteWorkshopRequest));
                else
                {
                    Logger.DevkitServer.LogWarning(Source, $"Lambda method not found in {definingMethod.Format()}. Can't join DevkitServer servers.");
                    DevkitServerModule.Fault();
                }
            }
            else
            {
                Logger.DevkitServer.LogWarning(Source, $"Method not found: {FormattingUtil.FormatMethod(typeof(void), typeof(Provider), "onClientTransportReady",
                    arguments: Type.EmptyTypes, isStatic: true)}. Can't join DevkitServer servers.");
                DevkitServerModule.Fault();
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogWarning(Source, ex, $"Failed to patch method: {FormattingUtil.FormatMethod(typeof(void), typeof(Provider), "onClientTransportReady",
                arguments: Type.EmptyTypes, isStatic: true)}. Can't join DevkitServer servers.");
            DevkitServerModule.Fault();
        }
#endif
        // Level.includeHash
        try
        {
            MethodInfo? method = typeof(Level).GetMethod(nameof(Level.includeHash), BindingFlags.Public | BindingFlags.Static);
            if (method != null)
            {
                Patcher.Patch(method, prefix: Accessor.Active.GetHarmonyMethod(PatchLevelIncludeHatch));
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogWarning(Source, ex, "Patcher error: Level.includeHash.");
        }
#if CLIENT
        if (DevkitServerConfig.Config.EnableBetterLevelCreation)
        {
            // MenuWorkshopEditorUI.onClickedAddButton
            try
            {
                MethodInfo? method = typeof(MenuWorkshopEditorUI).GetMethod("onClickedAddButton", BindingFlags.NonPublic | BindingFlags.Static);
                if (method != null)
                    Patcher.Patch(method, transpiler: Accessor.Active.GetHarmonyMethod(MapCreation.TranspileOnClickedAddLevelButton));
                else
                    Logger.DevkitServer.LogWarning(Source, $"Method not found to patch map creation: {FormattingUtil.FormatMethod(typeof(void),
                        typeof(MenuWorkshopEditorUI), "onClickedAddButton", [ (typeof(ISleekElement), "button") ], isStatic: true)}.");
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogWarning(Source, ex, $"Failed to patch method: {FormattingUtil.FormatMethod(typeof(void), typeof(MenuWorkshopEditorUI), "onClickedAddButton",
                        [ (typeof(ISleekElement), "button") ], isStatic: true)}.");
            }
        }

        try
        {
            MethodInfo? method = typeof(Provider).GetMethod("updateSteamRichPresence", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (method != null)
            {
                Patcher.Patch(method, transpiler: Accessor.Active.GetHarmonyMethod(TranspileUpdateSteamRichPresence));
                FailedRichPresencePatch = false;
            }
            else
                Logger.DevkitServer.LogWarning(Source, $"Method not found to patch rich presence: {FormattingUtil.FormatMethod(typeof(void),
                    typeof(Provider), "updateSteamRichPresence", arguments: Type.EmptyTypes, isStatic: true)}.");
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogWarning(Source, ex, $"Failed to patch method: {FormattingUtil.FormatMethod(typeof(void),
                typeof(Provider), "updateSteamRichPresence", arguments: Type.EmptyTypes, isStatic: true)}.");
        }
#endif

        // Level.save
        try
        {
            MethodInfo method = Accessor.GetMethod(Level.save)!;
            Patcher.Patch(method, prefix: Accessor.Active.GetHarmonyMethod(OnLevelSaving), finalizer: Accessor.Active.GetHarmonyMethod(OnLevelSavedFinalizer));
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogWarning(Source, ex, $"Failed to patch method: {Accessor.GetMethod(Level.save).Format()}.");
        }


        // Level.init
        try
        {
            MethodInfo? method = typeof(Level).GetMethod(nameof(Level.init), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(int) }, null);
            if (method == null)
            {
                Logger.DevkitServer.LogWarning(Source, $"Method not found: {FormattingUtil.FormatMethod(typeof(IEnumerator), typeof(Level),
                                                        nameof(Level.init), [ (typeof(int), "id") ])}.");
            }
            else
            {
                Patcher.Patch(method, postfix: Accessor.Active.GetHarmonyMethod(PostfixLevelInit));
                Logger.DevkitServer.LogDebug(Source, $"Postfixed {method.Format()} to add a on begin level load call.");
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogWarning(Source, ex, $"Failed to patch coroutine: {FormattingUtil.FormatMethod(typeof(IEnumerator), typeof(Level),
                nameof(Level.init), [ (typeof(int), "id") ])}.");
        }
#if CLIENT
        // EditorInteract.Update
        try
        {
            MethodInfo? method = AccessorExtensions.AssemblyCSharp.GetType("SDG.Unturned.EditorInteract")?.GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (method != null)
                Patcher.Patch(method, prefix: Accessor.Active.GetHarmonyMethod(EditorInteractUpdatePrefix));
            else
                Logger.DevkitServer.LogWarning(Source, $"Method not found to patch editor looking while not in Editor controller: {FormattingUtil.FormatMethod(typeof(void),
                                               AccessorExtensions.AssemblyCSharp.GetType("SDG.Unturned.EditorInteract"), "Update", arguments: Type.EmptyTypes)}.");
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogWarning(Source, ex, $"Failed to patch method: {FormattingUtil.FormatMethod(typeof(void), AccessorExtensions.AssemblyCSharp.GetType("SDG.Unturned.EditorInteract"), "Update", arguments: Type.EmptyTypes)}.");
        }

        // LoadingUI.onClickedCancelButton
        try
        {
            MethodInfo? method = typeof(LoadingUI).GetMethod("onClickedCancelButton", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (method != null)
            {
                Patcher.Patch(method, prefix: Accessor.Active.GetHarmonyMethod(OnClickedCancelLoadingPrefix));
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogWarning(Source, ex, "Patcher unpatching error: LoadingUI.onClickedCancelButton.");
        }
#endif
    }
    private static void DoManualUnpatches()
    {
#if SERVER
        Type? handlerType = null;
        try
        {
            handlerType = AccessorExtensions.AssemblyCSharp.GetType("SDG.Unturned.ServerMessageHandler_GetWorkshopFiles", false, false);
            if (handlerType != null && handlerType.GetMethod("ReadMessage", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) is { } method)
                Patcher.Unpatch(method, Accessor.GetMethod(TranspileWorkshopRequest));
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogWarning(Source, ex, $"Failed to unpatch method: {FormattingUtil.FormatMethod(typeof(void), handlerType, "ReadMessage",
                [(typeof(ITransportConnection), "transportConnection"), (typeof(NetPakReader), "reader")],
                isStatic: true)}.");
        }
#elif CLIENT
        try
        {
            if (typeof(Provider).GetMethod("onClientTransportReady", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) is { } definingMethod
                && Accessor.Active.TryGetLambdaMethod(definingMethod, out MethodInfo lambdaMethod, [ typeof(NetPakWriter) ]))
            {
                Patcher.Unpatch(lambdaMethod, Accessor.GetMethod(TranspileWriteWorkshopRequest));
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogWarning(Source, ex, $"Failed to unpatch method: {FormattingUtil.FormatMethod(typeof(void), typeof(Provider), "onClientTransportReady",
                arguments: Type.EmptyTypes, isStatic: true)}.");
            DevkitServerModule.Fault();
        }
#endif
        // Level.includeHash
        try
        {
            MethodInfo? method = typeof(Level).GetMethod(nameof(Level.includeHash), BindingFlags.Public | BindingFlags.Static);
            if (method != null)
            {
                Patcher.Unpatch(method, Accessor.GetMethod(PatchLevelIncludeHatch));
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogWarning(Source, ex, "Patcher unpatching error: Level.includeHash.");
        }

        // Level.init
        try
        {
            MethodInfo? method = typeof(Level).GetMethod("init", BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(int) }, null);
            if (method != null)
            {
                Patcher.Unpatch(method, Accessor.GetMethod(PostfixLevelInit));
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogWarning(Source, ex, $"Failed to unpatch coroutine: {FormattingUtil.FormatMethod(typeof(IEnumerator), typeof(Level), nameof(Level.init), namedArguments: [ (typeof(int), "id") ])}.");
        }

#if CLIENT
        // EditorInteract.Update
        try
        {
            MethodInfo? method = AccessorExtensions.AssemblyCSharp.GetType("SDG.Unturned.EditorInteract")?.GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (method != null)
            {
                Patcher.Unpatch(method, Accessor.GetMethod(EditorInteractUpdatePrefix));
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogWarning(Source, ex, $"Failed to unpatch method: {FormattingUtil.FormatMethod(typeof(void), AccessorExtensions.AssemblyCSharp.GetType("SDG.Unturned.EditorInteract"), "Update", arguments: Type.EmptyTypes)}.");
        }

        try
        {
            MethodInfo? method = typeof(Provider).GetMethod("updateSteamRichPresence", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (method != null)
            {
                Patcher.Unpatch(method, Accessor.GetMethod(TranspileUpdateSteamRichPresence));
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogWarning(Source, ex, $"Failed to unpatch method: {FormattingUtil.FormatMethod(typeof(void),
                typeof(Provider), "updateSteamRichPresence", arguments: Type.EmptyTypes, isStatic: true)}.");
        }
        FailedRichPresencePatch = true;

        // LoadingUI.onClickedCancelButton
        try
        {
            MethodInfo? method = typeof(LoadingUI).GetMethod("onClickedCancelButton", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (method != null)
            {
                Patcher.Unpatch(method, Accessor.GetMethod(OnClickedCancelLoadingPrefix));
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogWarning(Source, ex, "Patcher unpatching error: LoadingUI.onClickedCancelButton.");
        }
#endif
    }
#if CLIENT
    private static void WritePasswordHash(NetPakWriter writer)
    {
        if (string.IsNullOrEmpty(Provider.serverPassword) || Provider.serverPasswordHash.Length != 20)
        {
            writer.WriteBit(false);
        }
        else
        {
            writer.WriteBit(true);
            writer.WriteBytes(Provider.serverPasswordHash);
        }
    }
    private static IEnumerable<CodeInstruction> TranspileWriteWorkshopRequest(IEnumerable<CodeInstruction> instructions, MethodBase method)
    {
        List<CodeInstruction> t = [
            ..instructions,
            new CodeInstruction(method.IsStatic ? OpCodes.Ldarg_0 : OpCodes.Ldarg_1),
            new CodeInstruction(OpCodes.Call, Accessor.GetMethod(WritePasswordHash))
        ];

        if (t.Count <= 2 || t[^3].opcode != OpCodes.Ret)
            return t;

        // move ret to end of list
        t.Add(t[^3]);
        t.RemoveAt(t.Count - 4);

        return t;
    }
    private static IEnumerable<CodeInstruction> TranspileWorkshopResponse(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase method)
    {
        TranspileContext ctx = new TranspileContext(method, generator, instructions);

        MethodInfo? receiveWorkshopResponse = typeof(Provider).GetMethod("receiveWorkshopResponse", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        while (ctx.MoveNext())
        {
            if (!ctx.Instruction.Calls(receiveWorkshopResponse))
                continue;

            int oldIndex = ctx.CaretIndex;
            ctx.CaretIndex = ctx.GetLastUnconsumedIndex(null);
            ctx.EmitAbove(method.IsStatic ? OpCodes.Ldarg_0 : OpCodes.Ldarg_1);
            ctx.EmitAbove(OpCodes.Call, Accessor.GetMethod(ReadDevkitServerMarker)!);
            ctx.CaretIndex = oldIndex + 2;
        }
        return ctx;
    }
    private static void ReadDevkitServerMarker(NetPakReader reader)
    {
        if (!reader.ReadUInt32(out uint dsVsn))
        {
            DevkitServerModule.IsEditing = false;
            Logger.DevkitServer.LogInfo(nameof(ReadDevkitServerMarker), $"Connecting to a server not running {DevkitServerModule.ColorizedModuleName}.");
            DevkitServerServers[Provider.server.m_SteamID] = uint.MaxValue;
            return;
        }

        DevkitServerServers[Provider.server.m_SteamID] = dsVsn;

        if (dsVsn == 0u)
        {
            DevkitServerModule.IsEditing = false;
            Logger.DevkitServer.LogInfo(nameof(ReadDevkitServerMarker), $"Connecting to a server running {DevkitServerModule.ColorizedModuleName} but not in edit mode.");
            return;
        }

        DevkitServerModule.IsEditing = true;
        Logger.DevkitServer.LogInfo(nameof(ReadDevkitServerMarker), $"Connecting to a server running {DevkitServerModule.ColorizedModuleName}.");
    }

    internal static uint? GetKnownServerDevkitServerVersion(CSteamID serverId) => DevkitServerServers.TryGetValue(serverId.m_SteamID, out uint v) ? v : null;
    private static readonly Dictionary<ulong, uint> DevkitServerServers = new Dictionary<ulong, uint>();
#endif
#if SERVER
    private static void WriteDevkitServerTag(NetPakWriter writer)
    {
        writer.WriteUInt32(1);
    }
    private static IEnumerable<CodeInstruction> TranspileWorkshopRequestResponse(IEnumerable<CodeInstruction> instructions, MethodBase method)
    {
        List<CodeInstruction> t = [
            ..instructions,
            new CodeInstruction(method.IsStatic ? OpCodes.Ldarg_0 : OpCodes.Ldarg_1),
            new CodeInstruction(OpCodes.Call, Accessor.GetMethod(WriteDevkitServerTag)!)
        ];

        if (t.Count <= 2 || t[^3].opcode != OpCodes.Ret)
            return t;

        // move ret to end of list
        t.Add(t[^3]);
        t.RemoveAt(t.Count - 4);

        return t;
    }
    private static readonly Dictionary<uint, KeyValuePair<int, float>> PasswordTriesIPv4 = new Dictionary<uint, KeyValuePair<int, float>>(16);
    private static readonly Dictionary<ulong, KeyValuePair<int, float>> PasswordTriesSteam64 = new Dictionary<ulong, KeyValuePair<int, float>>(16);
    private static bool ReadPasswordHash(ITransportConnection connection, NetPakReader reader)
    {
        Logger.DevkitServer.LogDebug(nameof(ReadPasswordHash), "Validating password hash...");

        bool serverHasPassword = !string.IsNullOrEmpty(Provider.serverPassword) && Provider.serverPasswordHash.Length == 20;

        IPv4Address ipv4 = IPv4Address.Zero;
        CSteamID steam64 = CSteamID.Nil;

        if (!connection.TryGetIPv4Address(out uint address))
        {
            steam64 = UserManager.TryGetSteamId(connection);
            if (!steam64.UserSteam64())
            {
                Logger.DevkitServer.LogInfo(nameof(ReadPasswordHash), " Failed to get IPv4 and Steam ID of connecting user.");
                if (serverHasPassword)
                {
                    Provider.reject(connection, ESteamRejection.PLUGIN, DevkitServerModule.MainLocalization.Translate("UnknownIPv4AndSteam64"));
                    return false;
                }
            }

            Logger.DevkitServer.LogDebug(nameof(ReadPasswordHash), $" Failed to get IPv4 but was able to get Steam ID of connecting user: {steam64.Format()}.");
        }
        else
        {
            ipv4 = new IPv4Address(address);

            steam64 = UserManager.TryGetSteamId(connection);
            if (!steam64.UserSteam64())
            {
                steam64 = CSteamID.Nil;
                Logger.DevkitServer.LogDebug(nameof(ReadPasswordHash), $" Failed to get Steam ID but was able to get IPv4 of connecting user: {ipv4.Format()}.");
            }
            else
                Logger.DevkitServer.LogDebug(nameof(ReadPasswordHash), $" Got SteamID and IPv4 of connecting user: {steam64.Format()}, {ipv4.Format()}.");
        }

        byte[] source = Array.Empty<byte>();
        int offset = 0;
        if (!reader.ReadBit(out bool hasPassword) || hasPassword && !reader.ReadBytesPtr(20, out source, out offset))
        {
            Logger.DevkitServer.LogDebug(nameof(ReadPasswordHash), " Failed to read hash (likely a vanilla client connecting).");
            
            if (!DevkitServerModule.IsEditing)
                return true;

            Provider.reject(connection, ESteamRejection.SERVER_MODULE_DESYNC);
            return false;
        }

        if (!serverHasPassword)
        {
            Logger.DevkitServer.LogDebug(nameof(ReadPasswordHash), " Looks good (no password).");
            return true;
        }

        if (!hasPassword)
        {
            Logger.DevkitServer.LogDebug(nameof(ReadPasswordHash), " Rejected (no password given).");
            Provider.reject(connection, ESteamRejection.WRONG_PASSWORD);
            return false;
        }

        if (DevkitServerConfig.Config.PasswordAttempts > 0 && DevkitServerConfig.Config.WrongPasswordBlockExpireSeconds > 0f)
        {
            if (!ipv4.IsZero && PasswordTriesIPv4.TryGetValue(ipv4.value, out KeyValuePair<int, float> countRecord) &&
                countRecord.Key >= DevkitServerConfig.Config.PasswordAttempts &&
                CachedTime.RealtimeSinceStartup - countRecord.Value < DevkitServerConfig.Config.WrongPasswordBlockExpireSeconds)
            {
                string expires = Mathf.CeilToInt(DevkitServerConfig.Config.WrongPasswordBlockExpireSeconds - CachedTime.RealtimeSinceStartup + countRecord.Value)
                    .ToString(CultureInfo.InvariantCulture);
                Provider.reject(connection, ESteamRejection.PLUGIN, DevkitServerModule.MainLocalization.Translate("TooManyPasswordAttempts", expires));
                Logger.DevkitServer.LogDebug(nameof(ReadPasswordHash), $" Too many invalid password attempts from {ipv4.Format()}, expires in {expires.Format()} sec.");
                return false;
            }
            if (steam64.UserSteam64() && PasswordTriesSteam64.TryGetValue(steam64.m_SteamID, out countRecord) &&
                countRecord.Key >= DevkitServerConfig.Config.PasswordAttempts &&
                CachedTime.RealtimeSinceStartup - countRecord.Value < DevkitServerConfig.Config.WrongPasswordBlockExpireSeconds)
            {
                string expires = Mathf.CeilToInt(DevkitServerConfig.Config.WrongPasswordBlockExpireSeconds - CachedTime.RealtimeSinceStartup + countRecord.Value)
                    .ToString(CultureInfo.InvariantCulture);
                Provider.reject(connection, ESteamRejection.PLUGIN, DevkitServerModule.MainLocalization.Translate("TooManyPasswordAttempts", expires));
                Logger.DevkitServer.LogDebug(nameof(ReadPasswordHash), $" Too many invalid password attempts from {steam64.Format()}, expires in {expires.Format()} sec.");
                return false;
            }
        }

        List<ulong>? toRemove = null;
        foreach (KeyValuePair<uint, KeyValuePair<int, float>> entry in PasswordTriesIPv4)
        {
            if (CachedTime.RealtimeSinceStartup - entry.Value.Value > DevkitServerConfig.Config.WrongPasswordBlockExpireSeconds * 2)
                (toRemove ??= new List<ulong>(PasswordTriesIPv4.Count)).Add(entry.Key);
        }

        if (toRemove != null)
        {
            foreach (ulong ipAddress in toRemove)
                PasswordTriesIPv4.Remove((uint)ipAddress);
            toRemove.Clear();
        }
        
        foreach (KeyValuePair<ulong, KeyValuePair<int, float>> entry in PasswordTriesSteam64)
        {
            if (CachedTime.RealtimeSinceStartup - entry.Value.Value > DevkitServerConfig.Config.WrongPasswordBlockExpireSeconds * 2)
                (toRemove ??= new List<ulong>(PasswordTriesSteam64.Count)).Add(entry.Key);
        }

        if (toRemove != null)
        {
            foreach (ulong steamId in toRemove)
                PasswordTriesSteam64.Remove(steamId);
        }

        ArraySegment<byte> hash = new ArraySegment<byte>(source, offset, source.Length == 0 ? 0 : 20);
        byte[] existing = Provider.serverPasswordHash;
        for (int i = 0; i < 20; ++i)
        {
            if (hash[i] == existing[i])
                continue;

            if (DevkitServerConfig.Config.PasswordAttempts > 0 && DevkitServerConfig.Config.WrongPasswordBlockExpireSeconds > 0f)
            {
                int c;

                if (!ipv4.IsZero && PasswordTriesIPv4.TryGetValue(ipv4.value, out KeyValuePair<int, float> countRecord))
                    c = countRecord.Key + 1;
                else
                    c = 1;

                if (steam64.UserSteam64() && PasswordTriesSteam64.TryGetValue(steam64.m_SteamID, out countRecord))
                    c = Math.Max(c, countRecord.Key + 1);
                else
                    c = Math.Max(c, 1);

                if (!ipv4.IsZero)
                    PasswordTriesIPv4[ipv4.value] = new KeyValuePair<int, float>(c, CachedTime.RealtimeSinceStartup);

                if (steam64.UserSteam64())
                    PasswordTriesSteam64[steam64.m_SteamID] = new KeyValuePair<int, float>(c, CachedTime.RealtimeSinceStartup);

                Logger.DevkitServer.LogDebug(nameof(ReadPasswordHash), $" Invalid password for address {steam64.Format()} ({ipv4.Format()}), try {c.Format()} / {DevkitServerConfig.Config.PasswordAttempts.Format()}.");
            }
            else
                Logger.DevkitServer.LogDebug(nameof(ReadPasswordHash), $" Invalid password for address {steam64.Format()} ({ipv4.Format()}).");

            Provider.reject(connection, ESteamRejection.WRONG_PASSWORD);
            return false;
        }

        if (!ipv4.IsZero)
            PasswordTriesIPv4.Remove(ipv4.value);
        if (steam64.UserSteam64())
            PasswordTriesSteam64.Remove(steam64.m_SteamID);

        Logger.DevkitServer.LogDebug(nameof(ReadPasswordHash), " Looks good.");
        return true;
    }
    private const string DefaultGetWorkshopHeaderString = "Hello!";
    private static IEnumerable<CodeInstruction> TranspileWorkshopRequest(IEnumerable<CodeInstruction> instructions, MethodBase method, ILGenerator generator)
    {
        List<CodeInstruction> ins = [..instructions];

        ins.Insert(0, new CodeInstruction(OpCodes.Ldarg_0));
        ins.Insert(1, new CodeInstruction(OpCodes.Ldarg_1));
        ins.Insert(2, new CodeInstruction(OpCodes.Call, Accessor.GetMethod(new Action<ITransportConnection, NetPakReader>(OnConnectionPending))));
        Logger.DevkitServer.LogDebug(Source, $"{method.Format()} - Added prefix to GetWorkshop request.");

        for (int i = 3; i < ins.Count; ++i)
        {
            if (!ins[i].LoadsConstant(DefaultGetWorkshopHeaderString))
                continue;

            Label? lbl = PatchUtility.GetNextBranchTarget(ins, i);
            if (!lbl.HasValue)
            {
                Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Failed to find label target.");
                return ins;
            }

            i = PatchUtility.FindLabelDestinationIndex(ins, lbl.Value, i);
            if (i == -1)
            {
                Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Failed to find label index.");
                return ins;
            }

            ins.Insert(i, new CodeInstruction(OpCodes.Ldarg_0).WithStartBlocksFrom(ins[i]));
            ins.Insert(i + 1, new CodeInstruction(OpCodes.Ldarg_1));
            ins.Insert(i + 2, new CodeInstruction(OpCodes.Call, Accessor.GetMethod(ReadPasswordHash)));
            Label label = generator.DefineLabel();
            ins.Insert(i + 3, new CodeInstruction(OpCodes.Brtrue, label));
            ins.Insert(i + 4, new CodeInstruction(OpCodes.Ret));
            ins[i + 5].labels.Add(label);
            Logger.DevkitServer.LogDebug(Source, $"{method.Format()} - Added password check to GetWorkshop request.");
            return ins;
        }

        Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to add password check to the workshop request.");
        return ins;
    }
#endif
    private static bool OnLevelSaving()
    {
#if CLIENT
        if (DevkitServerModule.IsEditing)
        {
            Logger.DevkitServer.LogInfo(nameof(OnLevelSaving), "Asking server to save.");
            DevkitServerModule.AskSave();
            return false;
        }
#endif

        ThreadUtil.assertIsGameThread();

        bool locked = LevelData.ShouldActivateSaveLockOnLevelSave;
        if (locked)
            LevelData.SaveLock.Wait();

        try
        {
            Logger.DevkitServer.LogInfo(nameof(OnLevelSaving), "Saving editor data.");
            LandscapeUtil.DeleteUnusedTileData();
        }
        catch
        {
            if (locked)
                LevelData.SaveLock.Release();
            
            throw;
        }
        return true;
    }
    private static void OnLevelSavedFinalizer(bool __runOriginal)
    {
        if (__runOriginal && LevelData.ShouldActivateSaveLockOnLevelSave)
            LevelData.SaveLock.Release();
    }
#if CLIENT
    private static bool OnClickedCancelLoadingPrefix(ISleekButton button)
    {
        LargeMessageTransmission? levelDownload = LargeMessageTransmission.GetReceivingMessages().FirstOrDefault(x => x.HandlerType == typeof(LevelTransmissionHandler) && !x.WasCancelled);
        if (levelDownload == null)
            return true;

        UIAccessTools.SetLoadingCancelVisibility(false);

        UniTask.Create(async () =>
        {
            bool cancelled;
            CancellationTokenSource src = new CancellationTokenSource(TimeSpan.FromSeconds(5d));
            try
            {
                cancelled = await levelDownload.Cancel(src.Token);
            }
            catch (OperationCanceledException)
            {
                Logger.DevkitServer.LogInfo(levelDownload.LogSource, "Failed to cancel level download, timed out.");
                Level.exit();
                return;
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogWarning(levelDownload.LogSource, ex, "Failed to cancel level download.");
                Level.exit();
                return;
            }
            finally
            {
                src.Dispose();
            }

            Logger.DevkitServer.LogInfo(levelDownload.LogSource, !cancelled
                ? $"[{levelDownload.LogSource}] Level download already cancelled."
                : $"[{levelDownload.LogSource}] Level download cancelled by user request.");
        });

        return false;
    }
#endif
    private static bool PatchLevelIncludeHatch(string id, byte[] pendingHash)
    {
        return !Level.isLoaded;
    }
#if CLIENT
    private static bool EditorInteractUpdatePrefix()
    {
        return UserControl.LocalController is CameraController.Editor or CameraController.None;
    }
#endif
    private static bool IsEditorMode(PlayerCaller caller)
    {
        if (!DevkitServerModule.IsEditing)
            return false;
#if CLIENT
        if (caller.channel.IsLocalPlayer)
            return UserControl.LocalController == CameraController.Editor;
#endif
        EditorUser? user = UserManager.FromId(caller.player.channel.owner.playerID.steamID.m_SteamID);
        return !(user != null && user.Control.Controller == CameraController.Player);
    }
    
    private static void PostfixLevelInit(ref IEnumerator __result)
    {
        Logger.DevkitServer.LogInfo(Source, $"Level initializing: {Level.info.getLocalizedName().Format(false)}.");

        IEnumerator val = __result;
        __result = UniTask.ToCoroutine(async () =>
        {
            await AssetUtil.InvokeOnBeginLevelLoading(DevkitServerModule.UnloadToken);
            await val;
        });
    }

#if CLIENT
    private static string GetLevelNameOrHidden()
    {
        if (!Level.isEditor && !Level.info.isEditable)
            return Level.info.getLocalizedName();

        if (DevkitServerConfig.Config.HideMapNameFromRichPresence || ClientInfo.Info is { ServerForcesHideMapNameFromRichPresence: true })
            return "<hidden>";

        return Level.info.getLocalizedName();
    }
    private static IEnumerable<CodeInstruction> TranspileUpdateSteamRichPresence(IEnumerable<CodeInstruction> instructions, MethodBase method)
    {
        MethodInfo? getLevelInfo = typeof(Level).GetProperty(nameof(Level.info), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetGetMethod(true);
        if (getLevelInfo == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to find property getter: Level.info.");
            return instructions;
        }
        
        MethodInfo? getLocalizedName = typeof(LevelInfo).GetMethod(nameof(LevelInfo.getLocalizedName), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (getLocalizedName == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to find method: LevelInfo.getLocalizedName.");
            return instructions;
        }
        
        FieldInfo? localizationField = typeof(Provider).GetField(nameof(Provider.localization), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (localizationField == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to find field: Provider.localization.");
        }

        FieldInfo? newLocalization = typeof(PatchesMain).GetField(nameof(RichPresenceLocalizationOverride), BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
        if (newLocalization == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to find field: PatchesMain." + nameof(RichPresenceLocalizationOverride) + ".");
        }

        List<CodeInstruction> ins = [..instructions];

        int hidden3Times = 0;
        for (int i = 0; i < ins.Count; ++i)
        {
            if (PatchUtility.MatchPattern(ins, i, 
                    x => x.Calls(getLevelInfo),
                    x => x.Calls(getLocalizedName)
                        ))
            {
                CodeInstruction newCodeIns = new CodeInstruction(OpCodes.Call, Accessor.GetMethod(GetLevelNameOrHidden));
                PatchUtility.TransferStartingInstructionNeeds(ins[i], newCodeIns);
                PatchUtility.TransferEndingInstructionNeeds(ins[i + 1], newCodeIns);
                ins[i] = newCodeIns;
                ins.RemoveAt(i + 1);
                --i;
                ++hidden3Times;
            }
            else if (localizationField != null && PatchUtility.MatchPattern(ins, i,
                                                    x => x.LoadsField(localizationField)
                    ))
            {
                ins[i].operand = newLocalization;
            }
        }

        if (hidden3Times < 3)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Failed to patch for hiding map name. The map name may not be hidden from rich presence.");
        }

        return ins;
    }

    [HarmonyPatch(typeof(EditorUI), "OnEnable")]
    [HarmonyPostfix]
    [UsedImplicitly]
    private static void OnEditorUIEnabled(EditorUI __instance)
    {
        FieldInfo? areaField = typeof(Editor).GetField("_area", BindingFlags.NonPublic | BindingFlags.Instance);
        if (areaField == null)
        {
            Logger.DevkitServer.LogWarning(Source, "Unable to find field: Editor._area.");
            return;
        }
        FieldInfo? editorField = typeof(Editor).GetField("_editor", BindingFlags.NonPublic | BindingFlags.Static);
        if (editorField == null)
        {
            Logger.DevkitServer.LogWarning(Source, "Unable to find field: Editor._editor.");
            return;
        }
        Transform parent = __instance.transform.parent;
        if (parent != null && parent.TryGetComponent(out EditorArea area) && parent.TryGetComponent(out Editor editor))
        {
            editorField.SetValue(null, editor);
            areaField.SetValue(editor, area);
            Logger.DevkitServer.LogDebug(Source, "Patched issue with EditorUI not loading (set Editor._area and Editor._editor in OnEnable).");
        }
        else
        {
            Logger.DevkitServer.LogWarning(Source, "Unable to fix order of Editor component instantiations.");
        }
    }

    [HarmonyPatch(typeof(LoadingUI), "Update")]
    [HarmonyTranspiler]
    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> TranspileLoadingUIUpdate(IEnumerable<CodeInstruction> instructions, MethodBase method, ILGenerator generator)
    {
        FieldInfo? playerUIInstance = typeof(PlayerUI).GetField("instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (playerUIInstance == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to find field: PlayerUI.instance.");
            DevkitServerModule.Fault();
        }
        FieldInfo? editorUIInstance = typeof(EditorUI).GetField("instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (playerUIInstance == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to find field: EditorUI.instance.");
            DevkitServerModule.Fault();
        }

        MethodInfo getController = UserControl.GetLocalControllerMethod;

        List<CodeInstruction> ins = [..instructions];
        bool patchedOutPlayerUI = false;
        bool patchedOutEditorUI = false;
        for (int i = 1; i < ins.Count; ++i)
        {
            if (PatchUtility.MatchPattern(ins, i,
                    x => playerUIInstance != null && x.LoadsField(playerUIInstance) ||
                         editorUIInstance != null && x.LoadsField(editorUIInstance),
                    x => x.opcode != OpCodes.Ldnull) && ins[i - 1].operand is Label label)
            {
                Label lbl = generator.DefineLabel();
                ins.Insert(i, new CodeInstruction(OpCodes.Call, AccessorExtensions.IsDevkitServerGetter));
                ins.Insert(i + 1, new CodeInstruction(OpCodes.Brfalse, lbl));
                ins.Insert(i + 2, new CodeInstruction(OpCodes.Call, getController));
                CameraController current = ins[i + 3].LoadsField(playerUIInstance) ? CameraController.Player : CameraController.Editor;
                ins.Insert(i + 3, PatchUtility.LoadConstantI4((int)current));
                ins.Insert(i + 4, new CodeInstruction(OpCodes.Bne_Un, label));
                i += 5;
                ins[i].labels.Add(lbl);
                patchedOutPlayerUI |= current == CameraController.Player;
                patchedOutEditorUI |= current == CameraController.Editor;
            }
        }

        if (!patchedOutPlayerUI)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to edit call to {FormattingUtil.FormatMethod(typeof(void), typeof(PlayerUI), "Player_OnGUI", arguments: Type.EmptyTypes)}.");
            DevkitServerModule.Fault();
        }
        if (!patchedOutEditorUI)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to edit call to {FormattingUtil.FormatMethod(typeof(void), typeof(EditorUI), "Editor_OnGUI", arguments: Type.EmptyTypes)}.");
            DevkitServerModule.Fault();
        }

        return ins;
    }
    
    [HarmonyPatch("SDG.Unturned.Level, Assembly-CSharp", nameof(Level.isLoading), MethodType.Getter)]
    [HarmonyPrefix]
    [UsedImplicitly]
    private static bool PrefixGetIsLevelLoading(ref bool __result)
    {
        if (!DevkitServerModule.IsEditing)
            return true;

        __result = Level.isEditor && Level.isLoadingContent;
        return false;
    }
    
    private static bool _shouldContinueToLaunch;
    private static bool _hasRulesSub;

    [HarmonyPatch(typeof(Provider), nameof(Provider.launch))]
    [HarmonyPrefix]
    [UsedImplicitly]
    private static bool LaunchPatch()
    {
        if (_shouldContinueToLaunch)
            return true;

        if (Provider.CurrentServerConnectParameters == null)
        {
            Logger.DevkitServer.LogWarning(Source, "Unable to find Provider.CurrentServerConnectParameters. This could be caused by using a P2P or fake IP connection, if so please create an issue on GitHub.");
            Level.exit();
            return false;
        }

        // connecting from menu
        if (Provider.CurrentServerAdvertisement != null)
        {
            if (!_hasRulesSub)
            {
                _hasRulesSub = true;
                Provider.provider.matchmakingService.onRulesQueryRefreshed += RulesReady;
            }

            Provider.provider.matchmakingService.refreshRules(Provider.CurrentServerAdvertisement.ip, Provider.CurrentServerAdvertisement.queryPort);
            return false;
        }

        // connecting via server code
        uint? knownServerVersion = GetKnownServerDevkitServerVersion(Provider.server);
        if (!knownServerVersion.HasValue)
        {
            Logger.DevkitServer.LogError(Source, "Unable to verify whether or not this server is running DevkitServer. The server may need updated.");
            try
            {
                Provider.RequestDisconnect("Unable to verify whether or not this server is running DevkitServer. The server may need updated.");
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogWarning(nameof(LaunchPatch), ex, "Error trying to disconnect from server.");
                Level.exit();
            }
            return false;
        }

        if (knownServerVersion.Value > 0 && knownServerVersion.Value != uint.MaxValue)
        {
            DevkitServerModule.IsEditing = true;
            CustomNetMessageListeners.SendLocalMappings();
            EditorLevel.RequestLevel();
        }
        else
        {
            DevkitServerModule.IsEditing = false;
            ProceedWithLaunch();
        }

        return false;
    }
    private static void ProceedWithLaunch()
    {
        _shouldContinueToLaunch = true;
        try
        {
            Provider.launch();
        }
        finally
        {
            _shouldContinueToLaunch = false;
        }
    }
    internal static void Launch(LevelInfo? overrideLevelInfo)
    {
        if (!DevkitServerModule.IsEditing)
        {
            ProceedWithLaunch();
            return;
        }

        LevelInfo level = overrideLevelInfo ?? Level.getLevel(Provider.map);
        if (level == null)
        {
            DevkitServerUtility.CustomDisconnect("Could not find level \"" + Provider.map + "\"", ESteamConnectionFailureInfo.MAP);
        }
        else
        {
            Logger.DevkitServer.LogInfo(nameof(Launch), $"Loading server level: {level.getLocalizedName().Format(false)}.");
            Level.edit(level);
            Provider.gameMode = new DevkitServerGamemode();
        }
    }
    private static void RulesReady(Dictionary<string, string> rulesmap)
    {
        if (_hasRulesSub)
        {
            _hasRulesSub = false;
            Provider.provider.matchmakingService.onRulesQueryRefreshed -= RulesReady;
        }
        if (rulesmap.TryGetValue(DevkitServerModule.ServerRule, out string val))
        {
            if (!Version.TryParse(val, out Version version) || !DevkitServerModule.IsCompatibleWith(version))
            {
                DevkitServerUtility.CustomDisconnect(DevkitServerModule.MainLocalization.Translate("VersionKickMessage",
                    version?.ToString(4) ?? "[Unspecified]", AccessorExtensions.DevkitServer.GetName().Version.ToString(4)), ESteamConnectionFailureInfo.CUSTOM);
                return;
            }

            DevkitServerModule.IsEditing = true;
            CustomNetMessageListeners.SendLocalMappings();

            Logger.DevkitServer.LogInfo(nameof(RulesReady), $"Connecting to a server running {DevkitServerModule.ColorizedModuleName} " +
                           $"v{version.Format()} (You are running {AccessorExtensions.DevkitServer.GetName().Version.Format()})...");

            EditorLevel.RequestLevel();
        }
        else
        {
            Logger.DevkitServer.LogDebug(nameof(RulesReady), $"Did not find tag {DevkitServerModule.ServerRule.Format()}.");
            DevkitServerModule.IsEditing = false;
            ProceedWithLaunch();
        }
    }

    private static bool IsPlayerControlledOrNotEditing()
    {
        return !DevkitServerModule.IsEditing || !Level.isEditor || UserControl.LocalController == CameraController.Player;
    }
    private static bool IsEditorControlledOrNotEditing()
    {
        return !DevkitServerModule.IsEditing || !Level.isEditor || UserControl.LocalController == CameraController.Editor;
    }

    [UsedImplicitly]
    [HarmonyPatch(typeof(PlayerLook), "Update")]
    [HarmonyPrefix]
    private static bool PlayerLookUpdate(PlayerLook __instance) => !IsEditorMode(__instance);
    [UsedImplicitly]
    [HarmonyPatch(typeof(PlayerAnimator), "Update")]
    [HarmonyPrefix]
    private static bool PlayerAnimatorUpdate(PlayerAnimator __instance) => !IsEditorMode(__instance);
    [UsedImplicitly]
    [HarmonyPatch(typeof(PlayerAnimator), "LateUpdate")]
    [HarmonyPrefix]
    private static bool PlayerAnimatorLateUpdate(PlayerAnimator __instance) => !IsEditorMode(__instance);
    [UsedImplicitly]
    [HarmonyPatch(typeof(PlayerEquipment), "Update")]
    [HarmonyPrefix]
    private static bool PlayerEquipmentUpdate(PlayerEquipment __instance) => !IsEditorMode(__instance);
    [UsedImplicitly]
    [HarmonyPatch(typeof(PlayerMovement), "Update")]
    [HarmonyPrefix]
    private static bool PlayerMovementUpdate(PlayerMovement __instance) => !IsEditorMode(__instance);
    [UsedImplicitly]
    [HarmonyPatch(typeof(PlayerStance), "Update")]
    [HarmonyPrefix]
    private static bool PlayerStanceUpdate(PlayerStance __instance) => !IsEditorMode(__instance);
    [UsedImplicitly]
    [HarmonyPatch(typeof(PlayerInput), "FixedUpdate")]
    [HarmonyPrefix]
    private static bool PlayerInputFixedUpdate(PlayerInput __instance) => !IsEditorMode(__instance);
    [UsedImplicitly]
    [HarmonyPatch(typeof(PlayerInteract), "Update")]
    [HarmonyPrefix]
    private static bool PlayerInteractUpdate() => IsPlayerControlledOrNotEditing();
    [UsedImplicitly]
    [HarmonyPatch(typeof(PlayerUI), "Update")]
    [HarmonyPrefix]
    private static bool PlayerUIUpdate() => IsPlayerControlledOrNotEditing();
    [UsedImplicitly]
    [HarmonyPatch(typeof(EditorUI), "Update")]
    [HarmonyPrefix]
    private static bool EditorUIUpdate() => IsEditorControlledOrNotEditing();
    [UsedImplicitly]
    [HarmonyPatch(typeof(EditorPauseUI), "onClickedExitButton")]
    [HarmonyPrefix]
    private static bool EditorPauseUIExit()
    {
        if (!DevkitServerModule.IsEditing)
            return true;

        Provider.RequestDisconnect("clicked exit button from editor pause menu.");
        return false;
    }
    [UsedImplicitly]
    [HarmonyPatch(typeof(MenuConfigurationOptionsUI), MethodType.Constructor)]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> MenuConfigurationOptionsUITranspiler(IEnumerable<CodeInstruction> instructions, MethodBase method)
        => MenuConfigurationUITranspiler(instructions, method);
    [UsedImplicitly]
    [HarmonyPatch(typeof(MenuConfigurationGraphicsUI), MethodType.Constructor)]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> MenuConfigurationGraphicsUITranspiler(IEnumerable<CodeInstruction> instructions, MethodBase method)
        => MenuConfigurationUITranspiler(instructions, method);
    [UsedImplicitly]
    [HarmonyPatch(typeof(MenuConfigurationDisplayUI), MethodType.Constructor)]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> MenuConfigurationDisplayUITranspiler(IEnumerable<CodeInstruction> instructions, MethodBase method)
        => MenuConfigurationUITranspiler(instructions, method);
    [UsedImplicitly]
    [HarmonyPatch(typeof(MenuConfigurationControlsUI), MethodType.Constructor)]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> MenuConfigurationControlsUITranspiler(IEnumerable<CodeInstruction> instructions, MethodBase method)
        => MenuConfigurationUITranspiler(instructions, method);

    private static IEnumerable<CodeInstruction> MenuConfigurationUITranspiler(IEnumerable<CodeInstruction> instructions, MethodBase method)
    {
        MethodInfo? isConnected = typeof(Provider).GetProperty(nameof(Provider.isConnected), BindingFlags.Static | BindingFlags.Public)?.GetMethod;
        if (isConnected == null)
        {
            Logger.DevkitServer.LogWarning(Source, "Unable to find getter: Provider.isConnected.");
            DevkitServerModule.Fault();
        }

        MethodInfo? isEditing = typeof(Level).GetProperty(nameof(Level.isEditor), BindingFlags.Static | BindingFlags.Public)?.GetMethod;
        if (isEditing == null)
        {
            Logger.DevkitServer.LogWarning(Source, "Unable to find getter: Level.isEditor.");
            DevkitServerModule.Fault();
        }

        FieldInfo? playerWindow = typeof(PlayerUI).GetField("container", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (playerWindow == null)
        {
            Logger.DevkitServer.LogWarning(Source, "Unable to find static field: PlayerUI.container.");
            DevkitServerModule.Fault();
        }

        FieldInfo? editorWindow = typeof(EditorUI).GetField("window", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (editorWindow == null)
        {
            Logger.DevkitServer.LogWarning(Source, "Unable to find static field: EditorUI.window.");
            DevkitServerModule.Fault();
        }

        List<CodeInstruction> inst = new List<CodeInstruction>(instructions);
        bool oneConn = isConnected == null;
        bool oneEdit = isEditing == null;
        for (int i = 0; i < inst.Count; ++i)
        {
            CodeInstruction c = inst[i];
            yield return c;
            if (!oneConn && c.Calls(isConnected))
            {
                yield return new CodeInstruction(OpCodes.Call, Accessor.GetMethod(IsPlayerControlledOrNotEditing)!);
                yield return new CodeInstruction(OpCodes.And);
                yield return new CodeInstruction(OpCodes.Ldsfld, playerWindow);
                yield return new CodeInstruction(OpCodes.Ldnull);
                yield return new CodeInstruction(OpCodes.Ceq);
                yield return new CodeInstruction(OpCodes.Not);
                yield return new CodeInstruction(OpCodes.And);
                oneConn = true;
                Logger.DevkitServer.LogDebug(Source, $"{method.Format()} - Patched connection state checker.");
            }
            else if (!oneEdit && c.Calls(isEditing))
            {
                yield return new CodeInstruction(OpCodes.Ldsfld, editorWindow);
                yield return new CodeInstruction(OpCodes.Ldnull);
                yield return new CodeInstruction(OpCodes.Ceq);
                yield return new CodeInstruction(OpCodes.Not);
                yield return new CodeInstruction(OpCodes.And);
            }
        }

        if (!oneConn)
        {
            Logger.DevkitServer.LogWarning(Source, $"Unable to patch connection state checker in {method.Format()}.");
            DevkitServerModule.Fault();
        }
    }
#endif

    [HarmonyPatch(typeof(Provider), "loadGameMode")]
    [HarmonyPrefix]
    [UsedImplicitly]
    private static bool PrefixLoadGameMode()
    {
        if (DevkitServerModule.IsEditing)
        {
            Provider.gameMode = new DevkitServerGamemode();
            return false;
        }

        return true;
    }


#if SERVER
    internal static List<ITransportConnection> PendingConnections = new List<ITransportConnection>(8);

    [HarmonyPatch(typeof(Provider), "host")]
    [HarmonyPrefix]
    [UsedImplicitly]
    private static bool PrefixHost()
    {
        if (DevkitServerModule.HasLoadedBundle)
            return true;
        DevkitServerModule.ComponentHost.StartCoroutine(DevkitServerModule.TryLoadBundle(DoHost));
        return false;
    }

    private static void DoHost()
    {
        if (DevkitServerModule.HasLoadedBundle)
            Provider.host();
        else
        {
            Logger.DevkitServer.LogError("TryLoadBundle", $"Unable to host without {Path.Combine(DevkitServerConfig.BundlesFolder, "devkitserver.masterbundle").Format(false)}. Try redownloading from {DevkitServerModule.RepositoryUrl}.");
            DevkitServerModule.Fault();
        }
    }
    internal static void RemoveExpiredConnections()
    {
        for (int i = PendingConnections.Count - 1; i >= 0; --i)
        {
            if (PendingConnections[i].GetAddress() == null)
                PendingConnections.RemoveAt(i);
        }
    }
    internal static void RemoveConnection(ITransportConnection connection)
    {
        for (int i = PendingConnections.Count - 1; i >= 0; --i)
        {
            if (PendingConnections[i].Equals(connection))
                PendingConnections.RemoveAt(i);
        }
    }
    [NetCall(NetCallSource.FromClient, (ushort)DevkitServerNetCall.SendPending)]
    [UsedImplicitly]
    private static void OnConnectionPending(MessageContext ctx, byte[] passwordSHA1)
    {
        if (!string.IsNullOrEmpty(Provider.serverPassword) && !Hash.verifyHash(passwordSHA1, Provider.serverPasswordHash))
        {
            Logger.DevkitServer.LogInfo(nameof(OnConnectionPending), $"{ctx.Connection.Format()} tried to connect with an invalid password.");
            DevkitServerUtility.CustomDisconnect(ctx.Connection, ESteamRejection.WRONG_PASSWORD);
            ctx.Acknowledge(StandardErrorCode.AccessViolation);
            return;
        }

        OnConnectionPending(ctx.Connection, null!);
        ctx.Acknowledge(StandardErrorCode.Success);
    }

    private static void OnConnectionPending(ITransportConnection transportConnection, NetPakReader reader)
    {
        RemoveExpiredConnections();
        if (!PendingConnections.Contains(transportConnection))
        {
            RemoveConnection(transportConnection);
            PendingConnections.Add(transportConnection);
            Logger.DevkitServer.LogDebug(nameof(OnConnectionPending), "Connection pending: " + transportConnection.Format() + ".");
        }
        else
            Logger.DevkitServer.LogDebug(nameof(OnConnectionPending), "Connection already pending: " + transportConnection.Format() + ".");
    }
    [HarmonyPatch("ServerMessageHandler_ReadyToConnect", "ReadMessage", MethodType.Normal)]
    [HarmonyPrefix]
    [UsedImplicitly]
    private static void OnReadyToConnect(ITransportConnection transportConnection, NetPakReader reader)
    {
        RemoveExpiredConnections();
        RemoveConnection(transportConnection);
        Logger.DevkitServer.LogDebug(nameof(OnReadyToConnect), "Connection ready: " + transportConnection.Format() + ".");
    }
    [UsedImplicitly]
    [HarmonyPatch(typeof(LevelLighting), nameof(LevelLighting.updateLocal), typeof(Vector3), typeof(float), typeof(IAmbianceNode))]
    [HarmonyPrefix]
    private static bool UpdateLighting(Vector3 point, float windOverride, IAmbianceNode effectNode) => !DevkitServerModule.IsEditing;

    [UsedImplicitly]
    [HarmonyPatch("EditorInteract", "Update", MethodType.Normal)]
    [HarmonyPrefix]
    private static bool EditorInteractUpdate() => !DevkitServerModule.IsEditing;

    [UsedImplicitly]
    [HarmonyPatch("EditorUI", "Start", MethodType.Normal)]
    [HarmonyPrefix]
    private static bool EditorUIStart() => !DevkitServerModule.IsEditing;

    [UsedImplicitly]
    [HarmonyPatch("MainCamera", "Awake", MethodType.Normal)]
    [HarmonyPrefix]
    private static bool MainCameraAwake() => !DevkitServerModule.IsEditing;

    [UsedImplicitly]
    [HarmonyPatch(typeof(ObjectManager), "Update")]
    [HarmonyPrefix]
    private static bool ObjectManagerUpdate() => !DevkitServerModule.IsEditing;

    [UsedImplicitly]
    [HarmonyPatch(typeof(PlayerStance), "Update")]
    [HarmonyPrefix]
    private static bool PlayerStanceUpdate(PlayerStance __instance) => !IsEditorMode(__instance);

    [UsedImplicitly]
    [HarmonyPatch(typeof(PlayerStance), "GetStealthDetectionRadius")]
    [HarmonyPrefix]
    private static bool GetStealthDetectionRadius(ref float __result)
    {
        if (!DevkitServerModule.IsEditing)
            return true;
        __result = 0f;
        return false;
    }

    [UsedImplicitly]
    [HarmonyPatch(typeof(Editor), "save")]
    [HarmonyPrefix]
    private static bool SaveEditorSettings() => !DevkitServerModule.IsEditing;

    [UsedImplicitly]
    [HarmonyPatch(typeof(Provider), "AdvertiseConfig", MethodType.Normal)]
    [HarmonyPrefix]
    private static void AdvertiseConfig()
    {
        Version version = AccessorExtensions.DevkitServer.GetName().Version;
        Logger.DevkitServer.LogDebug(nameof(AdvertiseConfig), $"Setting SteamGameServer KeyValue {DevkitServerModule.ServerRule.Format()} to {version.Format()}.");
        SteamGameServer.SetKeyValue(DevkitServerModule.ServerRule, version.ToString(4));
    }

    [UsedImplicitly]
    [HarmonyPatch(typeof(LandscapeTile), nameof(LandscapeTile.updatePrototypes))]
    [HarmonyPrefix]
    private static void UpdatePrototypesPrefix(LandscapeTile __instance, ref TerrainLayer?[] ___terrainLayers)
    {
        // this function is commented out on the server build

        ___terrainLayers ??= new TerrainLayer[Landscape.SPLATMAP_LAYERS];

        for (int index = 0; index < Landscape.SPLATMAP_LAYERS; ++index)
        {
            AssetReference<LandscapeMaterialAsset> material = __instance.materials[index];
            LandscapeMaterialAsset landscapeMaterialAsset = material.Find();
            ___terrainLayers[index] = landscapeMaterialAsset?.getOrCreateLayer();
        }

        __instance.data.terrainLayers = ___terrainLayers;
    }

    private static readonly MethodInfo lvlLoad = typeof(Level).GetMethod(nameof(Level.load))!;
    private static readonly MethodInfo lvlEdit = typeof(PatchesMain).GetMethod(nameof(OnLoadEdit), BindingFlags.NonPublic | BindingFlags.Static)!;

    [HarmonyPatch(typeof(Provider), "onDedicatedUGCInstalled")]
    [HarmonyTranspiler]
    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> DedicatedUgcLoadedTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase method)
    {
        bool one = false;
        MethodInfo prefix = Accessor.GetMethod(MapCreation.PrefixLoadingDedicatedUGC)!;
        yield return new CodeInstruction(prefix.GetCallRuntime(), prefix);

        foreach (CodeInstruction instr in instructions)
        {
            if (!one && instr.Calls(lvlLoad))
            {
                yield return new CodeInstruction(OpCodes.Pop);
                yield return new CodeInstruction(lvlEdit.GetCallRuntime(), lvlEdit);
                Logger.DevkitServer.LogDebug(Source, "Inserted patch to " + method.Format() + " to load editor instead of player.");
                one = true;
            }
            else
                yield return instr;
        }
        if (!one)
        {
            Logger.DevkitServer.LogWarning(Source, "Failed to insert " + method.Format() + " patch to load editor instead of player.");
            DevkitServerModule.Fault();
        }
    }

    private static readonly MethodInfo? getLvlHash = typeof(Level).GetProperty(nameof(Level.hash), BindingFlags.Public | BindingFlags.Static)?.GetGetMethod(true);

    [UsedImplicitly]
    private static bool OnCheckingLevelHash(ITransportConnection connection)
    {
        Logger.DevkitServer.LogInfo(nameof(OnCheckingLevelHash), connection.Format() + " checking hash...");
        return DevkitServerModule.IsEditing;
    }

    [HarmonyPatch("ServerMessageHandler_ReadyToConnect", "ReadMessage", MethodType.Normal)]
    [HarmonyTranspiler]
    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> OnVerifyingPlayerDataTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase method)
    {
        MethodInfo mtd = typeof(PatchesMain).GetMethod(nameof(OnCheckingLevelHash), BindingFlags.Static | BindingFlags.NonPublic)!;
        MethodInfo? reject = typeof(Provider).GetMethods(BindingFlags.Static | BindingFlags.Public).FirstOrDefault(x =>
        {
            if (!x.Name.Equals(nameof(Provider.reject), StringComparison.Ordinal))
                return false;
            ParameterInfo[] ps = x.GetParameters();
            return ps.Length == 2 && ps[0].ParameterType == typeof(ITransportConnection) && ps[1].ParameterType == typeof(ESteamRejection);
        });
        if (reject == null)
        {
            Logger.DevkitServer.LogWarning(Source, "Unable to find method: Provider.reject(ITransportConnection, ESteamRejection).");
            DevkitServerModule.Fault();
        }
        List<CodeInstruction> list = [..instructions];
        int c = 0;
        for (int i = 0; i < list.Count; ++i)
        {
            CodeInstruction ins = list[i];
            if (c < 2 && i < list.Count - 5 && getLvlHash != null && mtd != null
                && AccessorExtensions.IsDevkitServerGetter != null &&
                list[i].opcode == OpCodes.Ldarg_0 && list[i + 1].LoadsConstant(ESteamRejection.WRONG_HASH_LEVEL) &&
                list[i + 2].Calls(reject) && list[i + 3].opcode == OpCodes.Ret)
            {
                Label label = generator.DefineLabel();
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(mtd.GetCallRuntime(), mtd);
                yield return new CodeInstruction(OpCodes.Brtrue_S, label);
                Logger.DevkitServer.LogDebug(Source, "Inserted patch to " + method.Format() + " to skip level hash verification.");
                yield return ins;
                yield return list[i + 1];
                yield return list[i + 2];
                yield return list[i + 3];
                i += 3;
                list[i + 4].labels.Add(label);
                ++c;
                continue;
            }
            
            yield return ins;
        }
        if (c != 2)
        {
            Logger.DevkitServer.LogWarning(Source, "Failed to insert two " + method.Format() + " patches to skip level hash verification.");
            DevkitServerModule.Fault();
        }
    }
    private static void OnLoadEdit(LevelInfo info)
    {
        Logger.DevkitServer.LogInfo(Source, "Loading DevkitServerEditor for " + info.getLocalizedName() + ".");
        DevkitServerModule.IsEditing = true;
        Level.edit(info);
    }
    
    [HarmonyPatch(typeof(LevelZombies), nameof(LevelZombies.load))]
    [HarmonyPostfix]
    [UsedImplicitly]
    private static void PostfixLoadLevelZombies()
    {
        FieldInfo? field = typeof(LevelZombies).GetField("_zombies", BindingFlags.Static | BindingFlags.NonPublic);
        if (field == null || !field.FieldType.IsAssignableFrom(typeof(List<ZombieSpawnpoint>[])))
        {
            Logger.DevkitServer.LogError(Source, $"Unable to find field: {typeof(LevelZombies).Format()}._zombies.");
            DevkitServerModule.Fault();
            return;
        }

        List<ZombieSpawnpoint>[] regions = new List<ZombieSpawnpoint>[LevelNavigation.bounds.Count];
        field.SetValue(null, regions);

        for (int index = 0; index < regions.Length; ++index)
        {
            regions[index] = new List<ZombieSpawnpoint>();
        }

        int c = 0;
        for (int x = 0; x < Regions.WORLD_SIZE; ++x)
        {
            for (int y = 0; y < Regions.WORLD_SIZE; ++y)
            {
                List<ZombieSpawnpoint> spawnpoints = LevelZombies.spawns[x, y];
                foreach (ZombieSpawnpoint spawnpoint in spawnpoints)
                {
                    if (LevelNavigation.tryGetBounds(spawnpoint.point, out byte bound) && LevelNavigation.checkNavigation(spawnpoint.point))
                    {
                        regions[bound].Add(spawnpoint);
                        ++c;
                    }
                }
            }
        }
        Logger.DevkitServer.LogInfo(Source, $"Copied over {c.Format()} zombie spawn{c.S()} from {typeof(LevelZombies).Format()}.");
    }


    [HarmonyPatch(typeof(VehicleManager), "onLevelLoaded")]
    [HarmonyTranspiler]
    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> VehicleManagerLevelLoadedTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase __method) => Accessor.Active.AddIsEditorCall(instructions, __method);

    [HarmonyPatch(typeof(BarricadeManager), "onLevelLoaded")]
    [HarmonyTranspiler]
    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> BarricadeManagerLevelLoadedTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase __method) => Accessor.Active.AddIsEditorCall(instructions, __method);

    // [HarmonyPatch(typeof(AnimalManager), "onLevelLoaded")]
    // [HarmonyTranspiler]
    // [UsedImplicitly]
    // private static IEnumerable<CodeInstruction> AnimalManagerLevelLoadedTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase __method) => Accessor.Active.AddIsEditorCall(instructions, __method);

    [HarmonyPatch(typeof(GroupManager), "onLevelLoaded")]
    [HarmonyTranspiler]
    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> GroupManagerLevelLoadedTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase __method) => Accessor.Active.AddIsEditorCall(instructions, __method);

    // [HarmonyPatch(typeof(ObjectManager), "onLevelLoaded")]
    // [HarmonyTranspiler]
    // [UsedImplicitly]
    // private static IEnumerable<CodeInstruction> ObjectManagerLevelLoadedTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase __method) => Accessor.Active.AddIsEditorCall(instructions, __method);

    [HarmonyPatch(typeof(StructureManager), "onLevelLoaded")]
    [HarmonyTranspiler]
    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> StructureManagerLevelLoadedTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase __method) => Accessor.Active.AddIsEditorCall(instructions, __method);

    [HarmonyPatch(typeof(VehicleManager), "onPostLevelLoaded")]
    [HarmonyTranspiler]
    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> VehicleManagerPostLevelLoadedTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase __method) => Accessor.Active.AddIsEditorCall(instructions, __method);
#endif
#region Landscape.writeMaps

    [HarmonyPatch(typeof(Landscape), nameof(Landscape.writeHeightmap))]
    [HarmonyTranspiler]
    [UsedImplicitly]
    [HarmonyPriority(1)]
    private static IEnumerable<CodeInstruction> LandscapeWriteHeightmapTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase method)
        => LandscapeWriteTranspilerGeneric(instructions, method, "heightmapTransactions");

    [HarmonyPatch(typeof(Landscape), nameof(Landscape.writeSplatmap))]
    [HarmonyTranspiler]
    [UsedImplicitly]
    [HarmonyPriority(1)]
    private static IEnumerable<CodeInstruction> LandscapeWriteSplatmapTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase method)
        => LandscapeWriteTranspilerGeneric(instructions, method, "splatmapTransactions");

    [HarmonyPatch(typeof(Landscape), nameof(Landscape.writeHoles))]
    [HarmonyTranspiler]
    [UsedImplicitly]
    [HarmonyPriority(1)]
    private static IEnumerable<CodeInstruction> LandscapeWriteHolesTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase method)
        => LandscapeWriteTranspilerGeneric(instructions, method, "holeTransactions");

    private static IEnumerable<CodeInstruction> LandscapeWriteTranspilerGeneric(IEnumerable<CodeInstruction> instructions, MethodBase method, string fieldName)
    {
        FieldInfo? transactions = typeof(Landscape).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
        if (transactions == null)
        {
            Logger.DevkitServer.LogWarning(Source, "Unable to find field: Landscape." + fieldName.Format(false) + " in " + method.Format() + ".");
            DevkitServerModule.Fault();
        }

        List<CodeInstruction> ins = [..instructions];
        bool ld = false;
        for (int i = 0; i < ins.Count; ++i)
        {
            CodeInstruction c = ins[i];
            if (!ld && c.LoadsField(transactions))
            {
                ld = true;
                for (int j = i + 1; j < ins.Count; ++j)
                {
                    if (ins[j].Branches(out Label? lbl) && lbl.HasValue)
                    {
                        yield return new CodeInstruction(OpCodes.Ldsfld, LandscapeUtil.SaveTransactionsField);
                        yield return new CodeInstruction(OpCodes.Brfalse_S, lbl);
                        Logger.DevkitServer.LogDebug(Source, "Inserted save transactions check in " + method.Format() + ".");
                        break;
                    }
                }
            }

            yield return c;
        }
        if (!ld)
        {
            Logger.DevkitServer.LogWarning(Source, "Patching error for " + method.Format() + ". Invalid transpiler operation.");
            DevkitServerModule.Fault();
        }
    }
    #endregion
}
