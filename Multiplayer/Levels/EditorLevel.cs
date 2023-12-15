using DevkitServer.API;
using DevkitServer.Levels;
using DevkitServer.Multiplayer.Networking;
#if CLIENT
using DevkitServer.Configuration;
using DevkitServer.Multiplayer.Actions;
using DevkitServer.Patches;
using SDG.Provider;
#endif
#if SERVER
using Cysharp.Threading.Tasks;
using DevkitServer.Util.Encoding;
#endif

namespace DevkitServer.Multiplayer.Levels;

[EarlyTypeInit]
public static class EditorLevel
{
    public const int DataBufferPacketSize = NetFactory.MaxPacketSize; // 60 KiB (must be slightly under ushort.MaxValue, 60 KB is a good middle ground to allow for future overhead expansion, etc).
    [UsedImplicitly]
    private static readonly NetCall<byte[]> SendRequestLevel = new NetCall<byte[]>(DevkitServerNetCall.RequestLevel);
    [UsedImplicitly]
    private static readonly NetCall<byte[]> SendPending = new NetCall<byte[]>(DevkitServerNetCall.SendPending);
    internal static List<ITransportConnection> PendingToReceiveActions = new List<ITransportConnection>(4);
#if CLIENT
    public static string TempLevelPath => Path.Combine(DevkitServerConfig.ServerFolder, "Levels", Provider.CurrentServerAdvertisement?.map ?? Guid.NewGuid().ToString("N"), "Level Install");
#endif
#if SERVER
    [NetCall(NetCallSource.FromClient, (ushort)DevkitServerNetCall.RequestLevel)]
    private static StandardErrorCode ReceiveLevelRequest(MessageContext ctx, byte[] passwordSHA1)
    {
        if (!string.IsNullOrEmpty(Provider.serverPassword) && !Hash.verifyHash(passwordSHA1, Provider.serverPasswordHash))
        {
            Logger.LogInfo($"[SEND LEVEL] {ctx.Connection.Format()} tried to request level data with an invalid password.");
            DevkitServerUtility.CustomDisconnect(ctx.Connection, ESteamRejection.WRONG_PASSWORD);
            return StandardErrorCode.AccessViolation;
        }

        Logger.LogInfo($"[SEND LEVEL] Received level request from ({ctx.Connection.Format()}).", ConsoleColor.DarkCyan);
        SendLevel(ctx.Connection);
        return StandardErrorCode.Success;
    }
    public static void SendLevel(ITransportConnection connection)
    {
        ThreadUtil.assertIsGameThread();

        LevelData data = LevelData.GatherLevelData(false);
        PendingToReceiveActions.Add(connection);
        data.WriteToData(true);

        LargeMessageTransmission transmission = new LargeMessageTransmission(connection, data.Data)
        {
            LogSource = "SEND LEVEL",
            HandlerType = typeof(LevelTransmissionHandler)
        };
        UniTask.Create(async () =>
        {
            try
            {
                if (await transmission.Send(DevkitServerModule.UnloadToken))
                {
                    Logger.LogInfo($"[{transmission.LogSource}] Sent level {Provider.map.Format(false)} (size: {DevkitServerUtility.FormatBytes(transmission.OriginalSize)}) to {connection.Format()}.");
                    return;
                }

                Logger.LogWarning($"Failed to send level {Provider.map.Format(false)} (size: {DevkitServerUtility.FormatBytes(transmission.OriginalSize)}) to {connection.Format()}.", method: transmission.LogSource);
            }
            catch (OperationCanceledException)
            {
                if (connection.IsConnected())
                    DevkitServerUtility.CustomDisconnect(connection, DevkitServerModule.LevelLoadingLocalization.Translate("DownloadCancelled"));
                
                return;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to send level to connection: {connection.Format()}.", method: transmission.LogSource);
                Logger.LogError(ex, method: transmission.LogSource);
            }
            finally
            {
                transmission.Dispose();
            }

            if (connection.IsConnected())
            {
                DevkitServerUtility.CustomDisconnect(connection, DevkitServerModule.LevelLoadingLocalization.Translate("DownloadFailed"));
            }
        });
    }
#endif
#if CLIENT
    private static readonly Func<string, ulong, LevelInfo?> ReadLevelInfo =
        Accessor.GenerateStaticCaller<Level, Func<string, ulong, LevelInfo?>>("ReadLevelInfo", throwOnError: true)!;

    internal static LevelData? ServerPendingLevelData;

    private static IEnumerator TryReceiveLevelCoroutine()
    {
        byte[] passwordHash = Provider.serverPasswordHash;
        NetTask task = SendPending.RequestAck(passwordHash, 1000);
        yield return task;
        if (task.Parameters.Responded)
        {
            if (task.Parameters.ErrorCode is (int)StandardErrorCode.AccessViolation)
            {
                Logger.LogInfo($"[SEND LEVEL] Password incorrect for server: {Provider.serverName.Format()}.");
                DevkitServerUtility.CustomDisconnect(ESteamConnectionFailureInfo.PASSWORD);
                yield break;
            }
            if (task.Parameters.ErrorCode is not (int)StandardErrorCode.Success)
            {
                Logger.LogInfo($"[SEND LEVEL] Failed to begin pending level download: {(task.Parameters.ErrorCode.HasValue ? ((StandardErrorCode)task.Parameters.ErrorCode.Value).Format() : "Unknown Error".Colorize(ConsoleColor.Red))}.");
                DevkitServerUtility.CustomDisconnect($"Error connecting: {(task.Parameters.ErrorCode.HasValue ? ((StandardErrorCode)task.Parameters.ErrorCode.Value).ToString() : "Unknown Error")}.");
                yield break;
            }

            if (TemporaryEditorActions.Instance == null)
                TemporaryEditorActions.BeginListening();
            yield return new WaitForSeconds(0.1f);
            
            task = SendRequestLevel.RequestAck(passwordHash, 3000);
            Logger.LogDebug("[SEND LEVEL] Sent level request.", ConsoleColor.DarkCyan);
            yield return task;
        }
        if (!task.Parameters.Responded)
        {
            Logger.LogWarning("[SEND LEVEL] Did not receive acknowledgement to level request; request timed out.");
            DevkitServerUtility.CustomDisconnect("Did not receive acknowledgement to level request; request timed out.");
        }
        else
        {
            Logger.LogDebug("[SEND LEVEL] Received acknowledgement to level request.", ConsoleColor.DarkCyan);
            LoadingUI.SetDownloadFileName("Level | Server Compressing Level");
            LoadingUI.NotifyDownloadProgress(1f);
        }
    }
    
    internal static void RequestLevel()
    {
        LoadingUI.SetLoadingText("Downloading Level from Server");
        LoadingUI.NotifyLevelLoadingProgress(0.01f);
        DevkitServerModule.ComponentHost.StartCoroutine(TryReceiveLevelCoroutine());
    }
    internal static void OnLevelReady(string dir)
    {
        DevkitServerModule.ComponentHost.StartCoroutine(DevkitServerModule.TryLoadBundle(() => DevkitServerModule.ComponentHost.StartCoroutine(LoadLevel(dir))));
    }

    private static readonly InstanceGetter<TempSteamworksWorkshop, List<PublishedFileId_t>> GetServerPendingIDs =
        Accessor.GenerateInstanceGetter<TempSteamworksWorkshop, List<PublishedFileId_t>>("serverPendingIDs", throwOnError: true)!;

    private static readonly Action<LevelInfo, List<PublishedFileId_t>> ApplyServerAssetMapping =
        Accessor.GenerateStaticCaller<Assets, Action<LevelInfo, List<PublishedFileId_t>>>("ApplyServerAssetMapping", throwOnError: true, allowUnsafeTypeBinding: true)!;

    private static IEnumerator LoadLevel(string dir)
    {
        GC.Collect();
        Resources.UnloadUnusedAssets();

        LevelInfo? info = ReadLevelInfo(dir, 0ul);
        if (info == null)
        {
            Logger.LogWarning("[SEND LEVEL] Failed to read received level at: \"" + dir + "\".", ConsoleColor.DarkCyan);
            DevkitServerUtility.CustomDisconnect("Failed to read received level.");
            yield break;
        }

        // apply the asset mapping before so the level assets dont get pooled in with the vanilla stuff.
        ApplyServerAssetMapping(info, GetServerPendingIDs(Provider.provider.workshopService));
        string bundlesFolder = Path.Combine(dir, "Bundles");
        if (Directory.Exists(bundlesFolder))
        {
            // Load assets from the map's Bundles folder.
            AssetOrigin origin = new AssetOrigin { name = "Map \"" + info.name + "\"", workshopFileId = 0ul };

            Assets.RequestAddSearchLocation(bundlesFolder, origin);

            yield return null;
            yield return new WaitForEndOfFrame();
            while (Assets.isLoading)
            {
                yield return null;
            }
#if DEBUG
            List<Asset> allAssets = new List<Asset>(8192);
            Assets.find(allAssets);
            Logger.LogInfo($"[SEND LEVEL] Loaded {allAssets.Count(x => x.GetOrigin() == origin).Format()} asset(s) from {origin.name.Format()}");
#endif

            GC.Collect();
            Resources.UnloadUnusedAssets();
        }
        
        PatchesMain.Launch(info);
    }
#endif
}
