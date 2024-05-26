using DanielWillett.ReflectionTools;
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
    private static readonly NetCall<byte[], CSteamID> SendRequestLevel = new NetCall<byte[], CSteamID>(DevkitServerNetCall.RequestLevel);
    [UsedImplicitly]
    private static readonly NetCall<byte[]> SendPending = new NetCall<byte[]>(DevkitServerNetCall.SendPending);
    internal static List<ITransportConnection> PendingToReceiveActions = new List<ITransportConnection>(4);
#if CLIENT
    public static string TempLevelPath => Path.Combine(DevkitServerConfig.ServerFolder, "Levels", Provider.CurrentServerAdvertisement?.map ?? Provider.map ?? Guid.NewGuid().ToString("N"));
#endif
#if SERVER
    [NetCall(NetCallSource.FromClient, (ushort)DevkitServerNetCall.RequestLevel)]
    private static StandardErrorCode ReceiveLevelRequest(MessageContext ctx, byte[] passwordSHA1, CSteamID fallbackUser)
    {
        if (!string.IsNullOrEmpty(Provider.serverPassword) && !Hash.verifyHash(passwordSHA1, Provider.serverPasswordHash))
        {
            Logger.DevkitServer.LogInfo("SEND LEVEL", $"{ctx.Connection.Format()} tried to request level data with an invalid password.");
            DevkitServerUtility.CustomDisconnect(ctx.Connection, ESteamRejection.WRONG_PASSWORD);
            return StandardErrorCode.AccessViolation;
        }

        CSteamID user = UserManager.TryGetSteamId(ctx.Connection);
        if (user.GetEAccountType() == EAccountType.k_EAccountTypeIndividual)
        {
            if (user != fallbackUser)
                return StandardErrorCode.InvalidData;
        }
        else
        {
            user = fallbackUser;
            Logger.DevkitServer.LogWarning("SEND LEVEL", $"Unable to determine SteamID of {ctx.Connection.Format()}, " +
                                                         $"falling back to user reported SteamID, {fallbackUser.Format()}.");
        }
        Logger.DevkitServer.LogInfo("SEND LEVEL", $"Received level request from ({user.Format()}|{ctx.Connection.Format()}).", ConsoleColor.DarkCyan);
        SendLevel(ctx.Connection, user);
        return StandardErrorCode.Success;
    }
    public static void SendLevel(ITransportConnection connection, CSteamID user)
    {
        PendingToReceiveActions.Add(connection);
        UniTask.Create(async () =>
        {
            LevelData data = await LevelData.GatherLevelData(false, user);

            await UniTask.SwitchToMainThread();

            if (!connection.IsConnected())
                return;

            data.WriteToData(true);

            LargeMessageTransmission transmission = new LargeMessageTransmission([ connection ], data.Data)
            {
                LogSource = "SEND LEVEL",
                HandlerType = typeof(LevelTransmissionHandler)
            };

            try
            {
                if ((await transmission.Send(DevkitServerModule.UnloadToken))[0])
                {
                    Logger.DevkitServer.LogInfo(transmission.LogSource, $"Sent level {Provider.map.Format(false)} (size: {FormattingUtil.FormatCapacity(transmission.OriginalSize, colorize: true)}) to {connection.Format()}.");
                    return;
                }

                Logger.DevkitServer.LogWarning(transmission.LogSource, $"Failed to send level {Provider.map.Format(false)} (size: {FormattingUtil.FormatCapacity(transmission.OriginalSize, colorize: true)}) to {connection.Format()}.");
            }
            catch (OperationCanceledException)
            {
                if (connection.IsConnected())
                    DevkitServerUtility.CustomDisconnect(connection, DevkitServerModule.LevelLoadingLocalization.Translate("DownloadCancelled"));

                return;
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogError(transmission.LogSource, ex, $"Failed to send level to connection: {connection.Format()}.");
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
                Logger.DevkitServer.LogInfo("SEND LEVEL", $"Password incorrect for server: {Provider.serverName.Format()}.");
                DevkitServerUtility.CustomDisconnect(ESteamConnectionFailureInfo.PASSWORD);
                yield break;
            }
            if (task.Parameters.ErrorCode is not (int)StandardErrorCode.Success)
            {
                Logger.DevkitServer.LogInfo("SEND LEVEL", $"Failed to begin pending level download: {(task.Parameters.ErrorCode.HasValue ? ((StandardErrorCode)task.Parameters.ErrorCode.Value).Format() : "Unknown Error".Colorize(ConsoleColor.Red))}.");
                DevkitServerUtility.CustomDisconnect($"Error connecting: {(task.Parameters.ErrorCode.HasValue ? ((StandardErrorCode)task.Parameters.ErrorCode.Value).ToString() : "Unknown Error")}.");
                yield break;
            }

            if (TemporaryEditorActions.Instance == null)
                TemporaryEditorActions.BeginListening();
            yield return new WaitForSeconds(0.1f);
            
            task = SendRequestLevel.RequestAck(passwordHash, Provider.client, 3000);
            Logger.DevkitServer.LogDebug("SEND LEVEL", "Sent level request.", ConsoleColor.DarkCyan);
            yield return task;
        }
        if (!task.Parameters.Responded)
        {
            Logger.DevkitServer.LogWarning("SEND LEVEL", "Did not receive acknowledgement to level request; request timed out.");
            DevkitServerUtility.CustomDisconnect("Did not receive acknowledgement to level request; request timed out.");
        }
        else
        {
            Logger.DevkitServer.LogDebug("SEND LEVEL", "Received acknowledgement to level request.", ConsoleColor.DarkCyan);
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
            Logger.DevkitServer.LogWarning("SEND LEVEL", $"Failed to read received level at: {dir.Format(true)}.", ConsoleColor.DarkCyan);
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
            Logger.DevkitServer.LogInfo("SEND LEVEL", $"Loaded {allAssets.Count(x => x.GetOrigin() == origin).Format()} asset(s) from {origin.name.Format()}");
#endif

            GC.Collect();
            Resources.UnloadUnusedAssets();
        }
        
        PatchesMain.Launch(info);
    }
#endif
}
