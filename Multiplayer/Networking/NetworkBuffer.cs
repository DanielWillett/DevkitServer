﻿namespace DevkitServer.Multiplayer.Networking;
internal class NetworkBuffer : IDisposable
{
    public byte[] Buffer;
    public readonly int BufferSize;
    private readonly Action<ArraySegment<byte>> _onMsgReady;
    private byte[]? _pendingData;
    private int _pendingLength;
    public MessageOverhead PendingOverhead;
    private bool _disposed;
    public readonly ITransportConnection Owner;
    public event NetworkBufferProgressUpdate? BufferProgressUpdated;
    public NetworkBuffer(Action<ArraySegment<byte>> onMsgReady, int capacity, ITransportConnection owner) : this (onMsgReady, owner, new byte[capacity]) { }
    public NetworkBuffer(Action<ArraySegment<byte>> onMsgReady, ITransportConnection owner, byte[] buffer)
    {
        _onMsgReady = onMsgReady;
        BufferSize = buffer.Length;
        Buffer = buffer;
        Owner = owner;
    }
    public unsafe void ProcessBuffer(int amtReceived, int offset = 0)
    {
        if (_disposed) return;
        try
        {
            lock (this)
            {
                fixed (byte* bytes = &Buffer[offset])
                {
                    bool isNewMsg = _pendingData is null;
                    if (isNewMsg)
                    {
                        if (amtReceived < MessageOverhead.MinimumSize)
                        {
                            Logger.DevkitServer.LogWarning(nameof(NetworkBuffer), "Received message less than " + FormattingUtil.FormatCapacity(MessageOverhead.MinimumSize, colorize: true) + " long!");
                            BufferProgressUpdated?.Invoke(0, 0);
                            return;
                        }
                        PendingOverhead = new MessageOverhead(bytes);
                    }
                    int size = PendingOverhead.Size;
                    int expSize = size + PendingOverhead.Length;
                    if (isNewMsg)
                    {
                        if (expSize == amtReceived) // new single packet, process all
                        {
                            _pendingData = new byte[amtReceived];
                            fixed (byte* ptr = _pendingData)
                                System.Buffer.MemoryCopy(bytes, ptr, amtReceived, amtReceived);
                            BufferProgressUpdated?.Invoke(amtReceived, amtReceived);
                            _onMsgReady(_pendingData);
                            goto reset;
                        }

                        if (amtReceived < expSize) // starting a new packet that continues past the current data, copy to buffer and return
                        {
                            _pendingData = new byte[expSize];
                            fixed (byte* ptr = _pendingData)
                                System.Buffer.MemoryCopy(bytes, ptr, expSize, amtReceived);
                            _pendingLength = amtReceived;
                            BufferProgressUpdated?.Invoke(amtReceived, expSize);
                            return;
                        }
                        // multiple messages in one.
                        _pendingData = new byte[expSize];
                        fixed (byte* ptr = _pendingData)
                            System.Buffer.MemoryCopy(bytes, ptr, expSize, expSize);
                        BufferProgressUpdated?.Invoke(expSize, expSize);
                        _onMsgReady(_pendingData);
                        _pendingData = null;
                        PendingOverhead = default;
                        _pendingLength = 0;
                        amtReceived -= expSize;
                        offset = expSize;
                        goto next;
                    }

                    // this data will complete the pending packet
                    int ttlSize = _pendingLength + amtReceived;
                    if (ttlSize == expSize)
                    {
                        fixed (byte* ptr = &_pendingData![_pendingLength])
                            System.Buffer.MemoryCopy(bytes, ptr, amtReceived, amtReceived);
                        BufferProgressUpdated?.Invoke(ttlSize, expSize);
                        _onMsgReady(_pendingData);
                        goto reset;
                    }
                    // continue the data for another packet
                    if (ttlSize < expSize)
                    {
                        fixed (byte* ptr = &_pendingData![_pendingLength])
                            System.Buffer.MemoryCopy(bytes, ptr, amtReceived, amtReceived);
                        _pendingLength += amtReceived;
                        BufferProgressUpdated?.Invoke(ttlSize, expSize);
                        return;
                    }

                    // end off the current message, start the next one
                    int remaining = expSize - _pendingLength;
                    fixed (byte* ptr = &_pendingData![_pendingLength])
                        System.Buffer.MemoryCopy(bytes, ptr, remaining, remaining);
                    BufferProgressUpdated?.Invoke(expSize, expSize);
                    _onMsgReady(_pendingData);
                    _pendingData = null;
                    PendingOverhead = default;
                    _pendingLength = 0;
                    amtReceived -= remaining;
                    offset = remaining;
                    goto next;

                    reset:
                    _pendingData = null;
                    PendingOverhead = default;
                    _pendingLength = 0;
                }
            }
            return;
        next:
            ProcessBuffer(amtReceived, offset);
        }
        catch (OverflowException)
        {
            Logger.DevkitServer.LogError(nameof(NetworkBuffer), "Overflow exception hit trying to allocate " + FormattingUtil.FormatCapacity(PendingOverhead.Size, colorize: true) + " for message " + PendingOverhead.Format() + ".");
            _pendingData = null;
            PendingOverhead = default;
            Buffer = null!;
            GC.Collect();
        }
        catch (OutOfMemoryException)
        {
            Logger.DevkitServer.LogError(nameof(NetworkBuffer), "Out of Memory exception hit trying to allocate " + FormattingUtil.FormatCapacity(PendingOverhead.Size, colorize: true) + " for message " + PendingOverhead.Format() + ".");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Buffer = null!;
        if (_pendingData != null)
        {
            _pendingData = null;
            GC.Collect();
        }
        PendingOverhead = default;
        _disposed = true;
    }
}

public delegate void NetworkBufferProgressUpdate(long bytesDownloaded, long totalBytes);