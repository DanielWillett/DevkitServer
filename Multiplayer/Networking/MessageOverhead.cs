using DevkitServer.Util.Encoding;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DevkitServer.Multiplayer.Networking;

[StructLayout(LayoutKind.Explicit, Size = MaximumSize)]
public readonly struct MessageOverhead
{
    public const int MinimumSize = 7;
    public const int MaximumSize = 27;

    [FieldOffset(0)]
    public readonly MessageFlags Flags;
    [FieldOffset(1)]
    public readonly ushort MessageId;
    [FieldOffset(3)]
    public readonly int Size;
    [FieldOffset(7)]
    public readonly long RequestKey = default;
    /// <summary>Only used with <see cref="MessageFlags.RequestResponseWithAcknowledgeRequest"/> as the new message key to acknowledge.</summary>
    [FieldOffset(15)]
    public readonly long ResponseKey = default;
    [FieldOffset(23)]
    public readonly int Length;

    // All flags that use a request key
    private const MessageFlags RequestKeyMask = MessageFlags.Request |
                                                 MessageFlags.RequestResponse |
                                                 MessageFlags.AcknowledgeRequest |
                                                 MessageFlags.AcknowledgeResponse |
                                                 MessageFlags.RequestResponseWithAcknowledgeRequest;
    // All flags that use a response key
    private const MessageFlags ResponseKeyMask = MessageFlags.RequestResponseWithAcknowledgeRequest;
    public ulong Sender => (Flags & MessageFlags.Relay) == 0 ? 0 : (ulong)RequestKey;
    public static unsafe void SetSize(ref MessageOverhead overhead, int size) => *(int*)((byte*)Unsafe.AsPointer(ref overhead) + 3) = size;

    /// <exception cref="IndexOutOfRangeException">If the pointer doesn't point to enough valid memory for the read.</exception>
    public unsafe MessageOverhead(byte[] bytes, ulong sender = 0)
    {
        fixed (byte* ptr = bytes)
        {
            Read(ptr, sender, bytes.Length, out Flags, out MessageId, out Size, out RequestKey, out ResponseKey, out Length);
        }
    }
    /// <exception cref="AccessViolationException">If the pointer doesn't point to enough valid memory for the read.</exception>
    public unsafe MessageOverhead(byte* ptr, ulong sender = 0)
    {
        Read(ptr, sender, -1, out Flags, out MessageId, out Size, out RequestKey, out ResponseKey, out Length);
    }
    /// <exception cref="IndexOutOfRangeException">If the pointer doesn't point to enough valid memory for the read.</exception>
    public unsafe MessageOverhead(byte* ptr, int len, ulong sender = 0)
    {
        Read(ptr, sender, len, out Flags, out MessageId, out Size, out RequestKey, out ResponseKey, out Length);
    }
    private static unsafe void Read(byte* ptr, ulong sender, int len, out MessageFlags flags, out ushort messageId, out int size, out long requestKey, out long responseKey, out int length)
    {
        if (len != -1 && len < MinimumSize) throw new IndexOutOfRangeException();
        requestKey = default;
        responseKey = default;
        length = 7;
        flags = (MessageFlags)(*ptr);
        int offset = 1;
        messageId = UnsafeBitConverter.GetUInt16(ptr, offset);
        offset += sizeof(ushort);
        size = UnsafeBitConverter.GetInt32(ptr, offset);
        offset += sizeof(int);
        if ((flags & RequestKeyMask) > 0 && (len == -1 || len >= offset + sizeof(long)))
        {
            requestKey = UnsafeBitConverter.GetInt64(ptr, offset);
            offset += sizeof(long);
        }
        if ((flags & ResponseKeyMask) > 0 && (len == -1 || len >= offset + sizeof(long)))
        {
            responseKey = UnsafeBitConverter.GetInt64(ptr, offset);
            offset += sizeof(long);
        }
        if (sender != 0)
        {
            flags |= MessageFlags.Relay;
            requestKey = (long)sender;
        }

        length = offset;
    }
    public MessageOverhead(MessageFlags flags, ushort messageId, int size, long requestKey = default, long responseKey = default)
    {
        Flags = flags;
        MessageId = messageId;
        Size = size;
        RequestKey = (flags & RequestKeyMask) > 0 ? requestKey : default;
        ResponseKey = (flags & ResponseKeyMask) > 0 ? responseKey : default;
        Length = MinimumSize;
        if (flags == MessageFlags.None) return;
        if ((flags & RequestKeyMask) > 0)
            Length += sizeof(long);
        if ((flags & ResponseKeyMask) > 0)
            Length += sizeof(long);
    }
    public unsafe void GetBytes(byte[] output, int index)
    {
        fixed (byte* ptr = &output[index])
            GetBytes(ptr, out _);
    }
    public unsafe void GetBytes(byte* output, out int length)
    {
        *output = (byte)Flags;
        length = 1;
        UnsafeBitConverter.GetBytes(output, MessageId, length);
        length += sizeof(ushort);
        UnsafeBitConverter.GetBytes(output, Size, length);
        length += sizeof(int);
        if ((Flags & RequestKeyMask) > 0)
        {
            UnsafeBitConverter.GetBytes(output, RequestKey, length);
            length += sizeof(long);
        }
        if ((Flags & ResponseKeyMask) > 0)
        {
            UnsafeBitConverter.GetBytes(output, ResponseKey, length);
            length += sizeof(long);
        }
    }
    public unsafe byte[] GetBytes()
    {
        byte[] bytes = new byte[Length];
        fixed (byte* ptr2 = bytes)
        {
            GetBytes(ptr2, out _);
        }
        return bytes;
    }
    public override string ToString()
    {
        string msg;
        if (MessageId == 0)
            msg = "Unknown Message Type;";
        else
            msg = "Msg: " + NetFactory.GetInvokerName(MessageId, (Flags & MessageFlags.HighSpeed) != 0) + ";";
        msg += " " + DevkitServerUtility.FormatBytes(Size) + ";";
        if ((Flags & MessageFlags.Relay) == 0 && ResponseKey != 0)
            msg += " Snowflake: " + RequestKey.ToString(CultureInfo.InvariantCulture) + ";";
        if ((Flags & MessageFlags.Relay) != 0 && Sender != 0)
            msg += " Sender: " + Sender.ToString(CultureInfo.InvariantCulture) + ";";
        if (RequestKey != 0)
            msg += (ResponseKey == 0 ? " Snowflake: " : " Request Key: ") + RequestKey.ToString(CultureInfo.InvariantCulture) + ";";
        if (Flags is not MessageFlags.None)
            msg += " Flags: " + Flags.ToString("F");
        return msg;
    }
}