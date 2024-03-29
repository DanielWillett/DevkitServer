using Cysharp.Threading.Tasks;
using DevkitServer.API.Multiplayer;
using DevkitServer.API.Permissions;
using DevkitServer.Core.Permissions;
using DevkitServer.Util.Encoding;
#if SERVER
using DevkitServer.API.UI;
using DevkitServer.Multiplayer;
#endif

namespace DevkitServer.Core.Cartography;
public static class CartographyReplication
{
#if SERVER
    /// <summary>
    /// Send a chart or satellite image at the given path to clients. The path does not determine where it will go on the remote side, only where it reads from.
    /// </summary>
    /// <param name="path">Path of the image to read from. Must be in PNG format.</param>
    /// <param name="isChart">Is this path a chart or satellite?</param>
    /// <param name="transportConnections">Clients to send the map to. Defaults to all clients.</param>
    /// <exception cref="FormatException">Invalid PNG data in file.</exception>
    public static async UniTask<LargeMessageTransmissionStatus> SendCartography(string path, bool isChart, IReadOnlyList<ITransportConnection>? transportConnections = null, CancellationToken token = default)
#else
    /// <summary>
    /// Send a chart or satellite image at the given path to the server. The path does not determine where it will go on the remote side, only where it reads from.
    /// </summary>
    /// <param name="path">Path of the image to read from. Must be in PNG format.</param>
    /// <param name="isChart">Is this path a chart or satellite?</param>
    /// <exception cref="FormatException">Invalid PNG data in file.</exception>
    /// <exception cref="NoPermissionsException">No permissions to upload baked maps.</exception>
    public static async UniTask<LargeMessageTransmissionStatus> SendCartography(string path, bool isChart, CancellationToken token = default)
#endif
    {
        DevkitServerModule.AssertIsDevkitServerClient();

#if SERVER
        transportConnections ??= Provider.GatherClientConnections();

        // since this is an async function, we can't use pooled lists
        if (transportConnections is PooledTransportConnectionList)
            transportConnections = transportConnections.ToList();
#endif

#if CLIENT
        await UniTask.SwitchToMainThread(token);
        PermissionLeaf leaf = isChart ? VanillaPermissions.BakeCartographyChart : VanillaPermissions.BakeCartographyGPS;
        if (!leaf.Has())
            throw new NoPermissionsException(leaf);
#endif
        await UniTask.SwitchToThreadPool();
        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(path, token).ConfigureAwait(false);
            await UniTask.SwitchToMainThread(token);
        }
        // sharing violation is a possibility
        catch (Exception ex)
        {
            await UniTask.SwitchToMainThread(token);
            Logger.DevkitServer.LogWarning(nameof(CartographyReplication), ex, $"Error reading {(isChart ? "chart" : "satellite")}, trying again on the main thread.");
            try
            {
                // ReSharper disable once MethodHasAsyncOverloadWithCancellation
                bytes = File.ReadAllBytes(path);
            }
            catch (Exception ex2)
            {
                throw new Exception($"Failed to read {(isChart ? "chart" : "satellite")}.", ex2);
            }
        }

        Texture2D texture = new Texture2D(1, 1);
        try
        {
            if (!texture.LoadImage(bytes, false))
            {
                Logger.DevkitServer.LogError(nameof(CartographyReplication), $"Invalid PNG data read from {path.Format()}.");
                throw new FormatException("Invalid PNG data in file.");
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(nameof(CartographyReplication), ex, $"Invalid PNG data read from {path.Format()}.");
            throw new FormatException("Invalid PNG data in file.");
        }
        // send to server in PNG, but to clients in JPG
#if CLIENT
        byte[] imgData = texture.EncodeToPNG();
#else
        byte[] imgData = texture.EncodeToJPG(100);
#endif
        Object.Destroy(texture);
#if SERVER
        LargeMessageTransmission transmission = new LargeMessageTransmission(transportConnections, imgData, 24576)
#else
        LargeMessageTransmission transmission = new LargeMessageTransmission(imgData, 24576)
#endif
        {
            LogSource = "SEND " + (isChart ? "CHART" : "SATELLITE"),
            HandlerType = typeof(CartographyReplicationHandler),
            Handler = new CartographyReplicationHandler(),
            AllowCompression = false
        };

        try
        {
#if SERVER
            bool[] successes = await transmission.Send(token);
            for (int i = 0; i < successes.Length; ++i)
            {
                if (!successes[i])
                    Logger.DevkitServer.LogWarning(nameof(CartographyReplication), $"Failed to send {(isChart ? "chart" : "satellite")} to {transportConnections[i].Format()}.");
            }
#else
            bool success = await transmission.Send(token);
            if (!success)
                Logger.DevkitServer.LogWarning(nameof(CartographyReplication), $"Failed to send {(isChart ? "chart" : "satellite")} to server.");
#endif
        }
        catch (OperationCanceledException)
        {
            Logger.DevkitServer.LogInfo(nameof(CartographyReplication), $"Cartogrpahy {(isChart ? "chart" : "satellite")} at {path.Format()} cancelled.");

            return LargeMessageTransmissionStatus.Cancelled;
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(nameof(CartographyReplication), ex, $"Error transmitting cartogrpahy {(isChart ? "chart" : "satellite")} at {path.Format()}.");

            return LargeMessageTransmissionStatus.Failure;
        }

        return LargeMessageTransmissionStatus.Success;
    }

    private class CartographyReplicationHandler : BaseLargeMessageTransmissionClientHandler
    {
        public CartographyReplicationHandler()
        {
            Logger.DevkitServer.LogDebug(nameof(CartographyReplication), "Created handler.");
        }

        protected internal override void OnStart()
        {
            bool isChart;

            if (Transmission.LogSource.Equals("SEND CHART", StringComparison.Ordinal))
                isChart = true;
            else if (Transmission.LogSource.Equals("SEND SATELLITE", StringComparison.Ordinal))
                isChart = false;
            else
            {
                Logger.DevkitServer.LogError(Transmission.LogSource, "Unknown cartography type from log source.");
                CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(5d));
                UniTask.Create(async () =>
                {
                    try
                    {
                        await Transmission.Cancel(cts.Token);
                    }
                    finally
                    {
                        cts.Dispose();
                        Transmission.Dispose();
                    }
                });
                return;
            }

            Logger.DevkitServer.LogInfo(Transmission.LogSource, $"Receiving {(isChart ? "chart" : "satellite")} image...");

#if SERVER
            PermissionLeaf leaf = isChart ? VanillaPermissions.BakeCartographyChart : VanillaPermissions.BakeCartographyGPS;

            CSteamID steamId = UserManager.TryGetSteamId(Transmission.Connections[0]);

            if (steamId.UserSteam64() && leaf.Has(steamId.m_SteamID))
                return;

            Logger.DevkitServer.LogError(Transmission.LogSource, "No permission to bake map.");
            CancellationTokenSource cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(5d));
            UniTask.Create(async () =>
            {
                try
                {
                    await Transmission.Cancel(cts2.Token);
                }
                finally
                {
                    cts2.Dispose();
                    Transmission.Dispose();
                }
            });

            if (steamId.UserSteam64() && UserManager.FromId(steamId) is { } user)
                EditorMessage.SendNoPermissionMessage(user, leaf);
#endif
        }

        protected internal override void OnFinished(LargeMessageTransmissionStatus status)
        {
            if (status != LargeMessageTransmissionStatus.Success)
                return;

            if (Transmission.IsServer)
                return;

            string path = Level.info.path;
            bool isChart;

            if (Transmission.LogSource.Equals("SEND CHART", StringComparison.Ordinal))
            {
                path = Path.Combine(path, "Chart.png");
                isChart = true;
            }
            else if (Transmission.LogSource.Equals("SEND SATELLITE", StringComparison.Ordinal))
            {
                path = Path.Combine(path, "Map.png");
                isChart = false;
            }
            else
            {
                Logger.DevkitServer.LogError(Transmission.LogSource, "Unknown cartography type from log source.");
                return;
            }

#if SERVER
            Logger.DevkitServer.LogInfo(Transmission.LogSource, $"{(isChart ? "Chart" : "Satellite")} received from {Transmission.Connections[0].Format()}.");
            UniTask.Create(async () =>
            {
                await UniTask.SwitchToMainThread(DevkitServerModule.UnloadToken);

                Texture2D texture = new Texture2D(1, 1);

                ArraySegment<byte> transmissionContent = Transmission.Content;
                byte[] data = transmissionContent.Array!;

                if (data == null)
                {
                    Logger.DevkitServer.LogError(nameof(CartographyReplication), $"Invalid JPG or PNG data received for {(isChart ? "chart" : "satellite")} from {Transmission.Connections[0].Format()} (content is null).");
                    return;
                }

                if (transmissionContent.Offset != 0 || transmissionContent.Count != data.Length)
                {
                    byte[] newData = new byte[transmissionContent.Count];
                    Buffer.BlockCopy(data, transmissionContent.Offset, newData, 0, newData.Length);
                }

                try
                {
                    if (!texture.LoadImage(data, false))
                    {
                        Logger.DevkitServer.LogError(nameof(CartographyReplication), $"Invalid JPG or PNG data received for {(isChart ? "chart" : "satellite")} from {Transmission.Connections[0].Format()}.");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Logger.DevkitServer.LogError(nameof(CartographyReplication), ex, $"Invalid JPG or PNG data received for {(isChart ? "chart" : "satellite")} from {Transmission.Connections[0].Format()}.");
                    return;
                }

                byte[] png = texture.EncodeToPNG();

                await UniTask.SwitchToThreadPool();

                try
                {
                    await using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                    {
                        await fs.WriteAsync(png, DevkitServerModule.UnloadToken);
                        Logger.DevkitServer.LogInfo(Transmission.LogSource, $"{(isChart ? "Chart" : "Satellite")} written to file {path.Format(false)}.");
                    }

                    await UniTask.SwitchToMainThread(DevkitServerModule.UnloadToken);
                }
                catch (Exception ex)
                {
                    await UniTask.SwitchToMainThread(DevkitServerModule.UnloadToken);
                    Logger.DevkitServer.LogWarning(nameof(CartographyReplication), ex, $"Error writing {(isChart ? "chart" : "satellite")}, trying again on the main thread.");

                    try
                    {
                        using FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
                        fs.Write(png);
                        Logger.DevkitServer.LogInfo(Transmission.LogSource, $"{(isChart ? "Chart" : "Satellite")} written to file {path.Format(false)}.");
                    }
                    catch (Exception ex2)
                    {
                        Logger.DevkitServer.LogError(nameof(CartographyReplication), ex2, $"Error writing {(isChart ? "chart" : "satellite")}.");
                    }
                }

                PooledTransportConnectionList transportConnections = DevkitServerUtility.GetAllConnections(Transmission.Connections[0]);

                if (transportConnections.Count == 0)
                    return;

                LargeMessageTransmission transmission = new LargeMessageTransmission(transportConnections, transmissionContent, 24576)
                {
                    LogSource = "SEND " + (isChart ? "CHART" : "SATELLITE"),
                    HandlerType = typeof(CartographyReplicationHandler),
                    Handler = new CartographyReplicationHandler()
                };

                try
                {
                    bool[] successes = await transmission.Send(DevkitServerModule.UnloadToken);

                    await UniTask.SwitchToMainThread(DevkitServerModule.UnloadToken);

                    for (int i = 0; i < successes.Length; ++i)
                    {
                        if (!successes[i])
                            Logger.DevkitServer.LogWarning(nameof(CartographyReplication), $"Failed to send {(isChart ? "chart" : "satellite")} to {transportConnections[i].Format()}.");
                    }
                }
                catch (OperationCanceledException)
                {
                    Logger.DevkitServer.LogInfo(nameof(CartographyReplication), $"Cartogrpahy {(isChart ? "chart" : "satellite")} at {path.Format()} cancelled.");
                }
                catch (Exception ex)
                {
                    Logger.DevkitServer.LogError(nameof(CartographyReplication), ex, $"Error transmitting cartogrpahy {(isChart ? "chart" : "satellite")} at {path.Format()}.");
                }
            });
#else
            Logger.DevkitServer.LogInfo(Transmission.LogSource, $"{(isChart ? "Chart" : "Satellite")} received from {Transmission.Connection.Format()}.");
            Task.Run(async () =>
            {
                await using FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Write);
                await fs.WriteAsync(Transmission.Content, DevkitServerModule.UnloadToken);
                Logger.DevkitServer.LogInfo(nameof(CartographyReplication), $"Received {(isChart ? "chart" : "satellite")} from another client.");
            });
#endif
        }
    }
}