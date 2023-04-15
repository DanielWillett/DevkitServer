using System.IO.Compression;
using System.Runtime.Remoting.Messaging;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Patches;
using DevkitServer.Util.Encoding;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine.PlayerLoop;

namespace DevkitServer.Multiplayer.LevelData;

[EarlyTypeInit]
public static class EditorLevel
{
    public const int DataBufferPacketSize = NetFactory.MaxPacketSize; // 60 KiB (must be slightly under ushort.MaxValue, 60 KB is a good middle ground to allow for future overhead expansion, etc).
    private static readonly NetCall SendRequestLevel = new NetCall((ushort)NetCalls.RequestLevel);
    private static readonly NetCall<int, string, long, int> StartSendLevel = new NetCall<int, string, long, int>((ushort)NetCalls.StartSendLevel);
    private static readonly NetCallCustom SendLevelPacket = new NetCallCustom((ushort)NetCalls.SendLevel);
    private static readonly NetCall<bool, bool> EndSendLevel = new NetCall<bool, bool>((ushort)NetCalls.EndSendLevel);
    private static readonly NetCall<int[]> RequestPackets = new NetCall<int[]>((ushort)NetCalls.RequestLevelPackets);
    private static readonly NetCall<int, int> SendCheckup = new NetCall<int, int>((ushort)NetCalls.RequestLevelCheckup);
    public static string TempLevelPath => Path.Combine(UnturnedPaths.RootDirectory.FullName, "DevkitServer",
#if SERVER
        Provider.serverID,
#else
        Parser.getIPFromUInt32(Provider.currentServerInfo.ip) + "_" + Provider.currentServerInfo.connectionPort,
#endif
        "{0}", "Level");

#if SERVER

    private static readonly ByteWriter LevelWriter = new ByteWriter(false, 134217728); // 128 MB

    [NetCall(NetCallSource.FromClient, (ushort)NetCalls.RequestLevel)]
    private static StandardErrorCode ReceiveLevelRequest(MessageContext ctx)
    {
        Logger.LogInfo($"[SEND LEVEL] Received level request from ({ctx.Connection.GetAddressString(true) ?? "[unknown address]"}).", ConsoleColor.DarkCyan);
        DevkitServerModule.ComponentHost.StartCoroutine(SendLevelCoroutine(ctx.Connection));
        return StandardErrorCode.Success;
    }
    public static void CancelLevelDownload(ITransportConnection connection)
    {
        EndSendLevel.Invoke(connection, true, false);
    }
    private static IEnumerator SendLevelCoroutine(ITransportConnection connection)
    {
        const float defaultDelay = 0.25f;
        const int firstCheckupPosition = 25 - 1;
        float startTime = Time.realtimeSinceStartup;
        string lvlName = Level.info.name;
        LevelData levelDataAtTimeOfRequest = LevelData.GatherLevelData();
        
        Folder folder = levelDataAtTimeOfRequest.LevelFolderContent;
        Folder.Write(LevelWriter, in folder);
        byte[] data = LevelWriter.ToArray();
        LevelWriter.FinishWrite();
        Logger.LogInfo("[SEND LEVEL] Original Size: " + DevkitServerUtility.FormatBytes(data.Length) + ".");
        bool compressed = false;
        
        using (MemoryStream mem = new MemoryStream(data.Length))
        using (DeflateStream stream = new DeflateStream(mem, CompressionLevel.Optimal))
        {
            IAsyncResult writeTask = stream.BeginWrite(data, 0, data.Length, null, null);
            while (!writeTask.IsCompleted)
                yield return null;

            stream.EndWrite(writeTask);
            stream.Close(); // flush just doesn't work here for some reason
            byte[] data2 = mem.ToArray();
            if (data2.Length < data.Length)
            {
                compressed = true;
                Logger.LogInfo("[SEND LEVEL] Compresssed Size: " + DevkitServerUtility.FormatBytes(data2.Length) + ".");
                data = data2;
            }
        }
        Logger.LogInfo($"[SEND LEVEL] Gathered level data ({DevkitServerUtility.FormatBytes(data.Length)}) for ({connection.GetAddressString(true) ?? "[unknown address]"}) after {Time.realtimeSinceStartup - startTime:F2} seconds.", ConsoleColor.DarkCyan);
        NetTask task2 = EndSendLevel.Listen();
        MessageOverhead cpy = new MessageOverhead(MessageFlags.None, (ushort)NetCalls.SendLevel, 0, 0);
        byte[] dataBuffer = new byte[cpy.Length + sizeof(int) + DataBufferPacketSize];
        int index = 0;
        int c = -1;
        int ttl = (int)Math.Ceiling(data.Length / (double)DataBufferPacketSize);
        StartSendLevel.Invoke(connection, data.Length, lvlName, task2.requestId, ttl);

        if (!connection.TryGetPort(out _))
            yield break;
        yield return new WaitForSecondsRealtime(0.25f);
        if (!connection.TryGetPort(out _))
            yield break;
        float packetDelay = defaultDelay;
        string ip = connection.GetAddressString(false);
        if (ip.Equals("127.0.0.1", StringComparison.Ordinal))
            packetDelay = 0.05f;
        int checkup = firstCheckupPosition;
        int lastCheckup = 0;
        startTime = Time.realtimeSinceStartup;
        Logger.LogInfo($"[SEND LEVEL] Sending {ttl} packets for level data ({lvlName}).", ConsoleColor.DarkCyan);
        while (true)
        {
            int len = Math.Min(DataBufferPacketSize, data.Length - index);
            if (len <= 0) break;
            cpy = new MessageOverhead(MessageFlags.None, (ushort)NetCalls.SendLevel, sizeof(int) + len, 0);
            Buffer.BlockCopy(data, index, dataBuffer, cpy.Length + sizeof(int), len);
            ++c;
            index += len;
            cpy.GetBytes(dataBuffer, 0);
            UnsafeBitConverter.GetBytes(dataBuffer, c, cpy.Length);
            connection.Send(dataBuffer, false);
            //yield return null;
            // DevkitServerUtility.PrintBytesHex(dataBuffer, 32, 64);
            // Logger.LogDebug($"[SEND LEVEL] Sent {DevkitServerUtility.FormatBytes(len)} from index #{index} as packet {c + 1} / {ttl} at time {Time.realtimeSinceStartup - startTime:F3} for level ({lvlName}).");

            if (c == checkup)
            {
                yield return new WaitForSeconds(0.5f);
                bool retried = false;
                retry2:
                NetTask task3 = SendCheckup.RequestAck(connection, lastCheckup, c, 10000);
                while (!task3.isCompleted)
                    yield return null;
                if (task3._parameters.Responded && task3._parameters.ErrorCode.HasValue)
                {
                    int misses = task3._parameters.ErrorCode.Value;
                    if (misses == 0)
                    {
                        float old = packetDelay;
                        if (packetDelay > 0.06f)
                        {
                            packetDelay *= 0.75f;
                            checkup += firstCheckupPosition;
                            Logger.LogDebug($"[SEND LEVEL] Packet delay decreased from {old} -> {packetDelay}.");
                        }
                    }
                    else if (misses < 0)
                    {
                        Logger.LogInfo($"[SEND LEVEL] User errored at checkup {c + 1} / {ttl}.", ConsoleColor.DarkCyan);
                        yield break;
                    }
                    else
                    {
                        float old = packetDelay;
                        packetDelay /= (float)misses / (c - lastCheckup);
                        Logger.LogDebug($"[SEND LEVEL] Packet delay increasesd from {old} -> {packetDelay} after missing {misses} / {c - lastCheckup} packet(s).");
                    }
                }
                else if (!retried)
                {
                    retried = true;
                    yield return new WaitForSeconds(0.25f);
                    goto retry2;
                }
                else
                {
                    Logger.LogInfo($"[SEND LEVEL] User failed to respond to checkup at {c + 1} / {ttl}.", ConsoleColor.DarkCyan);
                    yield break;
                }

                lastCheckup = c;
            }
            yield return new WaitForSeconds(packetDelay);
            if (!connection.TryGetPort(out _))
            {
                Logger.LogInfo($"[SEND LEVEL] User disconnected at packet {c + 1} / {ttl}.", ConsoleColor.DarkCyan);
                yield break;
            }
        }

        yield return new WaitForSecondsRealtime(0.25f);
        if (!connection.TryGetPort(out _))
            yield break;
        rerequest:
        NetTask task = EndSendLevel.Request(RequestPackets, connection, false, compressed);
        yield return task;
        if (task._parameters.TryGetParameter(0, out int[] packets) && packets.Length > 0)
        {
            Array.Sort(packets);
            cpy = new MessageOverhead(MessageFlags.AcknowledgeRequest, (ushort)NetCalls.SendLevel, 0, 0);
            dataBuffer = new byte[cpy.Length + sizeof(int) + DataBufferPacketSize];
            for (int i = 0; i < packets.Length; ++i)
            {
                bool retry = false;
                int packet = packets[i];
                index = DataBufferPacketSize * packet;
                int len = Math.Min(DataBufferPacketSize, data.Length - index);
                if (len <= 0) break;
                task = SendLevelPacket.ListenAck(1500);
                cpy = new MessageOverhead(MessageFlags.AcknowledgeRequest, (ushort)NetCalls.SendLevel, sizeof(int) + len, task.requestId);
                Buffer.BlockCopy(data, index, dataBuffer, cpy.Length + sizeof(int), len);
                cpy.GetBytes(dataBuffer, 0);
                UnsafeBitConverter.GetBytes(dataBuffer, packet, cpy.Length);
            retry:
                connection.Send(dataBuffer, true);
                yield return task;
                if (!task._parameters.Responded)
                {
                    if (!retry)
                    {
                        retry = true;
                        Logger.LogWarning($"[SEND LEVEL] Retrying recovery packet {packet + 1} for level ({lvlName}).");
                        goto retry;
                    }

                    Logger.LogError($"[SEND LEVEL] Failed retry of sending recovery packet {packet + 1} for level ({lvlName}).");
                    Provider.reject(connection, ESteamRejection.PLUGIN, "Failed to download " + lvlName + ".");
                    yield break;
                }

                // Logger.LogDebug($"[SEND LEVEL] {(retry ? "Resent" : "Sent")} {DevkitServerUtility.FormatBytes(len)} from index #{index} as recovery packet {packet + 1} / {ttl} at time {Time.realtimeSinceStartup - startTime:F3} for level ({lvlName}).");
            }

            yield return new WaitForSecondsRealtime(0.125f);
            goto rerequest;
        }
        Logger.LogInfo($"[SEND LEVEL] Sent level data ({data.Length:N} B) for level {lvlName}.", ConsoleColor.DarkCyan);
    }
#endif
#if CLIENT
    private static readonly ByteReader LevelReader = new ByteReader { ThrowOnError = true };
    private static readonly Func<string, bool, ulong, LevelInfo?> LoadLevelInfo =
        Accessor.GenerateStaticCaller<Level, Func<string, bool, ulong, LevelInfo?>>("loadLevelInfo", new Type[] { typeof(string), typeof(bool), typeof(ulong) }, true)!;

    private static byte[][]? _pendingLevel;
    private static long _pendingCancelKey;
    private static int _pendingLevelIndex;
    private static int _pendingLevelLength;
    private static string? _pendingLevelName;
    private static float _pendingLevelStartTime;
    private static int _startingMissingPackets;
    private static int _missingPackets;

    private static IEnumerator TryReceiveLevelCoroutine()
    {
        NetTask task = SendRequestLevel.RequestAck(3000);
        Logger.LogInfo("[RECEIVE LEVEL] Sent level request.", ConsoleColor.DarkCyan);
        yield return task;
        if (!task._parameters.Responded)
        {
            LoadingUI.updateKey("Downloading Level / Request Failed");
            Logger.LogWarning("[RECEIVE LEVEL] Did not receive acknowledgement to level request; request timed out.");
            Provider.disconnect();
        }
        else
        {
            Logger.LogInfo("[RECEIVE LEVEL] Received acknowledgement to level request.", ConsoleColor.DarkCyan);
            LoadingUI.updateKey("Downloading Level / Server Compressing Level");
            LoadingUI.updateProgress(0f);
        }
    }
    
    internal static void RequestLevel()
    {
        LoadingUI.updateKey("Downloading Level / Requesting Level Info");
        _pendingLevel = null;
        _pendingLevelName = null;
        _pendingLevelIndex = 0;
        _pendingLevelLength = 0;
        _pendingCancelKey = 0;
        _missingPackets = 0;
        _startingMissingPackets = 0;
        DevkitServerModule.ComponentHost.StartCoroutine(TryReceiveLevelCoroutine());
    }
    private static void UpdateLoadingUI()
    {
        if (_missingPackets > 0)
        {
            if (_missingPackets == _startingMissingPackets)
                LoadingUI.updateScene();
            LoadingUI.updateKey("Downloading Level / Recovering Missing Packets [ " + (_startingMissingPackets - _missingPackets) + " / " + _startingMissingPackets + " ]");
            LoadingUI.updateProgress((float)(_startingMissingPackets - _missingPackets) / _startingMissingPackets);
        }
        else if (_pendingLevelIndex >= _pendingLevelLength || _startingMissingPackets > 0)
        {
            LoadingUI.updateKey("Downloading Level / Installing Level " + _pendingLevelName + ".");
            LoadingUI.updateProgress(1f);
            LoadingUI.updateScene();
        }
        else
        {
            float time = Time.realtimeSinceStartup;
            string t = "Downloading Level / " + _pendingLevelName + " [ " + DevkitServerUtility.FormatBytes(_pendingLevelIndex) +
                       " / " + DevkitServerUtility.FormatBytes(_pendingLevelLength) + " ]";
            if ((time - _pendingLevelStartTime) > 2f)
            {
                float remaining = (time - _pendingLevelStartTime) / _pendingLevelIndex * (_pendingLevelLength - _pendingLevelIndex);
                t += " @ " + DevkitServerUtility.FormatBytes((long)(_pendingLevelIndex / (time - _pendingLevelStartTime))) + " / sec |" +
                    " Remaining: " + Mathf.FloorToInt(remaining / 60).ToString("00") + ":" + Mathf.FloorToInt(remaining % 60).ToString("00");
            }
            else t += " | Calculating Speed...";
            LoadingUI.updateKey(t);
            LoadingUI.updateProgress((float)_pendingLevelIndex / _pendingLevelLength);
        }
    }
    [NetCall(NetCallSource.FromServer, (ushort)NetCalls.RequestLevelCheckup)]
    private static int ReceiveLevelCheckup(MessageContext ctx, int start, int end)
    {
        if (_pendingLevel == null)
        {
            Logger.LogError("[RECEIVE LEVEL] Received level data before a start level message.");
            return -1;
        }

        Logger.LogDebug($"[RECEIVE LEVEL] Received level checkup: {start} -> {end}.");
        int missing = 0;
        for (int i = start - 1; i < end; ++i)
        {
            if (_pendingLevel[start] == null)
                ++missing;
        }

        return missing;
    }
    [NetCall(NetCallSource.FromServer, (ushort)NetCalls.StartSendLevel)]
    private static void ReceiveStartLevel(MessageContext ctx, int length, string lvlName, long reqId, int packetCount)
    {
        _pendingLevel = new byte[packetCount][];
        _pendingLevelLength = length;
        _pendingLevelName = lvlName;
        _pendingLevelIndex = 0;
        _pendingCancelKey = reqId;
        _missingPackets = 0;
        _startingMissingPackets = 0;
        _pendingLevelStartTime = Time.realtimeSinceStartup;
        Logger.LogInfo($"[RECEIVE LEVEL] Started receiving level data ({DevkitServerUtility.FormatBytes(length)}) for level {lvlName}.", ConsoleColor.DarkCyan);
        UpdateLoadingUI();
    }

    [NetCall(NetCallSource.FromServer, (ushort)NetCalls.SendLevel)]
    private static void ReceiveLevelDataPacket(MessageContext ctx, ByteReader reader)
    {
        if (_pendingLevel == null || _pendingLevelName == null)
        {
            Logger.LogError("[RECEIVE LEVEL] Received level data before a start level message.");
            return;
        }
        
        int packet = reader.ReadInt32();
        if (packet < 0 || packet >= _pendingLevel.Length)
        {
            Logger.LogError($"[RECEIVE LEVEL] Received level data packet out of range of expected ({packet + 1} / {_pendingLevel.Length}), ignoring.");
            return;
        }
        
        if (_pendingLevel[packet] != null)
        {
            Logger.LogWarning($"[RECEIVE LEVEL] Packet already downloaded ({packet + 1} / {_pendingLevel.Length}), replacing.");
        }

        int len = reader.InternalBuffer!.Length - reader.Position;
        _pendingLevel[packet] = new byte[len];
        Buffer.BlockCopy(reader.InternalBuffer!, reader.Position, _pendingLevel[packet], 0, len);
        
        reader.Skip(len);
        if (_startingMissingPackets > 0)
        {
            if (_missingPackets > 0)
                --_missingPackets;
            Logger.LogInfo($"[RECEIVE LEVEL] Recovered ({DevkitServerUtility.FormatBytes(len)}) (#{packet + 1}) for level {_pendingLevelName}.", ConsoleColor.DarkCyan);
        }
        else
        {
            _pendingLevelIndex += len;
            Logger.LogInfo($"[RECEIVE LEVEL] Received ({DevkitServerUtility.FormatBytes(len)}) (#{packet + 1}) for level {_pendingLevelName}.", ConsoleColor.DarkCyan);
        }
        UpdateLoadingUI();
        ctx.Acknowledge();
    }
    public static void CancelLevelDownload()
    {
        if (_pendingLevel != null)
        {
            MessageOverhead ovh = new MessageOverhead(MessageFlags.RequestResponse, EndSendLevel.ID, 0, 0, _pendingCancelKey);
            EndSendLevel.Invoke(ref ovh, true, false);
            Provider.disconnect();
        }
    }

    [NetCall(NetCallSource.FromServer, (ushort)NetCalls.EndSendLevel)]
    private static void ReceiveEndLevel(MessageContext ctx, bool cancelled, bool wasCompressed)
    {
        if (_pendingLevelName == null || _pendingLevel == null)
        {
            Logger.LogError("[RECEIVE LEVEL] Received end level without any data sent previously.");
            return;
        }
        if (!cancelled)
        {
            List<int>? missing = null;
            for (int i = 0; i < _pendingLevel.Length; ++i)
            {
                if (_pendingLevel[i] is null)
                {
                    Logger.LogDebug($"[RECEIVE LEVEL] Packet missing: {i + 1} / {_pendingLevel.Length}.");
                    (missing ??= new List<int>(16)).Add(i);
                }
            }
            if (missing == null)
            {
                LoadingUI.updateScene();
                LoadingUI.updateKey("Downloading Level / Installing Level " + _pendingLevelName + ".");
                string dir = TempLevelPath;
                dir = DevkitServerUtility.QuickFormat(dir, _pendingLevelName);
                if (Directory.Exists(dir))
                    Directory.Delete(dir, true);
                Directory.CreateDirectory(dir);
                LoadingUI.updateProgress(0.5f);
                ctx.Reply(RequestPackets, Array.Empty<int>());
                Logger.LogDebug($"[RECEIVE LEVEL] Allocating {_pendingLevelLength} bytes to level data array.");
                byte[] lvl = new byte[_pendingLevelLength];
                int index = 0;
                for (int i = 0; i < _pendingLevel.Length; ++i)
                {
                    byte[] b = _pendingLevel[i];
                    Buffer.BlockCopy(b, 0, lvl, index, b.Length);
                    index += b.Length;
                }
                if (wasCompressed)
                {
                    Logger.LogDebug("[RECEIVE LEVEL] Extracting level.");
                    LoadingUI.updateProgress(0.6f);
                    using MemoryStream output = new MemoryStream((int)(lvl.Length * 1.5));
                    using (MemoryStream input = new MemoryStream(lvl))
                    using (DeflateStream stream = new DeflateStream(input, CompressionMode.Decompress))
                    {
                        stream.CopyTo(output);
                    }

                    lvl = output.ToArray();
                    Logger.LogDebug($"[RECEIVE LEVEL] Decompressed from {DevkitServerUtility.FormatBytes(_pendingLevelLength)} -> {DevkitServerUtility.FormatBytes(lvl.Length)}.");
                }
#if DEBUG
                File.WriteAllBytes(Path.Combine(dir, "Raw Data.dat"), lvl);
#endif
                Logger.LogDebug("[RECEIVE LEVEL] Reading level folder.");
                LevelReader.LoadNew(lvl);
                Folder folder = Folder.Read(LevelReader);
                LoadingUI.updateProgress(0.75f);
                Logger.LogDebug("[RECEIVE LEVEL] Writing level folder.");
                folder.WriteContentsToDisk(dir);
                Logger.LogInfo($"[RECEIVE LEVEL] Finished receiving level data ({DevkitServerUtility.FormatBytes(_pendingLevel.Length)}) for level {_pendingLevelName}.", ConsoleColor.DarkCyan);
                LoadingUI.updateKey("Downloading Level / Level " + _pendingLevelName + " Installed");
                LoadingUI.updateProgress(1f);
                OnLevelReady(Path.Combine(dir, folder.FolderName));
                GC.Collect();
            }
            else
            {
                _startingMissingPackets = _missingPackets = missing.Count;
                ctx.Reply(RequestPackets, missing.ToArray());
                Logger.LogWarning($"[RECEIVE LEVEL] Missing {missing.Count} / {_pendingLevel.Length} ({(float)missing.Count / _pendingLevel.Length:P1}).");
                UpdateLoadingUI();
                return;
            }

        }
        else
        {
            ctx.Reply(RequestPackets, Array.Empty<int>());
            Logger.LogInfo("[RECEIVE LEVEL] Cancelled level load.");
            LoadingUI.updateKey("Downloading Level / Level Load Cancelled");
            Provider.disconnect();
        }

        _pendingLevel = null;
        _pendingLevelIndex = 0;
        _pendingLevelLength = 0;
        _pendingCancelKey = 0;
        _pendingLevelStartTime = 0;
        _pendingLevelName = null;
        _startingMissingPackets = 0;
        _missingPackets = 0;
    }

    private static void OnLevelReady(string dir)
    {
        LevelInfo? info = LoadLevelInfo(dir, false, 0ul);
        if (info == null)
        {
            Logger.LogInfo("[RECEIVE LEVEL] Failed to read received level at: \"" + dir + "\".", ConsoleColor.DarkCyan);
            Provider.disconnect();
        }

        DevkitServerModule.PendingLevelInfo = info;
        PatchesMain.Launch();
    }
#endif
}
