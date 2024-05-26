using DevkitServer.Util.Encoding;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DevkitServer.API;

namespace DevkitServer.Multiplayer.Networking;

/// <summary>
/// Represents the header for <see cref="DevkitServerMessage.InvokeNetCall"/> messges.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = MaximumSize)]
public readonly struct MessageOverhead : ITerminalFormattable
{
    [ThreadStatic]
    private static byte[]? t_GuidBuffer;

    public const int MinimumSize = 7;
    public const int MinimumGuidSize = 21;
    public const int MaximumSize = 43;

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
    [FieldOffset(27)]
    public readonly Guid MessageGuid;

    // All flags that use a request key
    internal const MessageFlags RequestKeyMask  = MessageFlags.Request |
                                                  MessageFlags.RequestResponse |
                                                  MessageFlags.AcknowledgeRequest |
                                                  MessageFlags.AcknowledgeResponse |
                                                  MessageFlags.RequestResponseWithAcknowledgeRequest;
    // All flags that use a response key
    internal const MessageFlags ResponseKeyMask = MessageFlags.RequestResponseWithAcknowledgeRequest;
    internal static unsafe void SetSize(ref MessageOverhead overhead, int size) => *(int*)((byte*)Unsafe.AsPointer(ref overhead) + 3) = size;

    /// <exception cref="IndexOutOfRangeException">If the pointer doesn't point to enough valid memory for the read.</exception>
    public unsafe MessageOverhead(byte[] bytes)
    {
        if (bytes == null)
            throw new ArgumentNullException(nameof(bytes));

        fixed (byte* ptr = bytes)
        {
            Read(ptr, bytes.Length, out Flags, out MessageId, out Size, out RequestKey, out ResponseKey, out Length, out MessageGuid);
        }
    }

    /// <exception cref="IndexOutOfRangeException">If the pointer doesn't point to enough valid memory for the read.</exception>
    public unsafe MessageOverhead(ArraySegment<byte> bytes)
    {
        if (bytes.Array == null)
            throw new ArgumentNullException(nameof(bytes));

        fixed (byte* ptr = &bytes.Array[bytes.Offset])
        {
            Read(ptr, bytes.Count, out Flags, out MessageId, out Size, out RequestKey, out ResponseKey, out Length, out MessageGuid);
        }
    }
    /// <exception cref="AccessViolationException">If the pointer doesn't point to enough valid memory for the read.</exception>
    public unsafe MessageOverhead(byte* ptr)
    {
        Read(ptr, -1, out Flags, out MessageId, out Size, out RequestKey, out ResponseKey, out Length, out MessageGuid);
    }
    /// <exception cref="IndexOutOfRangeException">If the pointer doesn't point to enough valid memory for the read.</exception>
    public unsafe MessageOverhead(byte* ptr, int len)
    {
        Read(ptr, len, out Flags, out MessageId, out Size, out RequestKey, out ResponseKey, out Length, out MessageGuid);
    }
    private static unsafe void Read(byte* ptr, int len, out MessageFlags flags, out ushort messageId, out int size, out long requestKey, out long responseKey, out int length, out Guid guid)
    {
        if (len != -1 && len < MinimumSize)
            throw new IndexOutOfRangeException();

        requestKey = default;
        responseKey = default;
        length = 7;
        flags = (MessageFlags)(*ptr);
        int offset = 1;
        if ((flags & MessageFlags.Guid) == 0)
        {
            messageId = UnsafeBitConverter.GetUInt16(ptr, offset);
            offset += sizeof(ushort);
            guid = Guid.Empty;
        }
        else
        {
            messageId = 0;
            if (len != -1 && len < offset + 16)
                throw new IndexOutOfRangeException();
            byte[] bytes = t_GuidBuffer ??= new byte[16];

            fixed (byte* bufferPtr = bytes)
                *(Guid*)bufferPtr = *(Guid*)(ptr + offset);

            guid = new Guid(bytes);
            offset += 16;
        }

        if (len != -1 && len < offset + sizeof(int))
            throw new IndexOutOfRangeException();

        size = UnsafeBitConverter.GetInt32(ptr, offset);
        offset += sizeof(int);
        if ((flags & RequestKeyMask) != 0 && (len == -1 || len >= offset + sizeof(long)))
        {
            if (len != -1 && len < offset + sizeof(long))
                throw new IndexOutOfRangeException();

            requestKey = UnsafeBitConverter.GetInt64(ptr, offset);
            offset += sizeof(long);
        }
        if ((flags & ResponseKeyMask) != 0 && (len == -1 || len >= offset + sizeof(long)))
        {
            if (len != -1 && len < offset + sizeof(long))
                throw new IndexOutOfRangeException();

            responseKey = UnsafeBitConverter.GetInt64(ptr, offset);
            offset += sizeof(long);
        }

        length = offset;
    }

    /// <summary>
    /// Create a core message overhead using a <see cref="DevkitServerNetCall"/> value.
    /// </summary>
    public MessageOverhead(MessageFlags flags, ushort messageId, int size, long requestKey = default, long responseKey = default)
    {
        flags &= ~MessageFlags.Guid;
        Flags = flags;
        MessageId = messageId;
        Size = size;
        Length = MinimumSize;
        MessageGuid = Guid.Empty;
        if (flags == MessageFlags.None)
            return;
        RequestKey = (flags & RequestKeyMask) != 0 ? requestKey : default;
        ResponseKey = (flags & ResponseKeyMask) != 0 ? responseKey : default;
        if ((flags & RequestKeyMask) != 0)
            Length += sizeof(long);
        if ((flags & ResponseKeyMask) != 0)
            Length += sizeof(long);
    }

    /// <summary>
    /// Create a plugin message overhead using a <see cref="Guid"/> value.
    /// </summary>
    public MessageOverhead(MessageFlags flags, Guid guid, int size, long requestKey = default, long responseKey = default)
    {
        flags |= MessageFlags.Guid;
        Flags = flags;
        MessageId = 0;
        MessageGuid = guid;
        Size = size;
        Length = MinimumGuidSize;
        if (flags is MessageFlags.None or MessageFlags.Guid)
            return;
        RequestKey = (flags & RequestKeyMask) != 0 ? requestKey : default;
        ResponseKey = (flags & ResponseKeyMask) != 0 ? responseKey : default;
        if ((flags & RequestKeyMask) != 0)
            Length += sizeof(long);
        if ((flags & ResponseKeyMask) != 0)
            Length += sizeof(long);
    }
    public MessageOverhead(MessageFlags flags, Guid guid, ushort messageId, int size, long requestKey = default, long responseKey = default)
    {
        if (guid != Guid.Empty)
            flags |= MessageFlags.Guid;
        Flags = flags;
        MessageId = messageId;
        MessageGuid = guid;
        Size = size;
        Length = (flags & MessageFlags.Guid) != 0 ? MinimumGuidSize : MinimumSize;
        if (flags is MessageFlags.None or MessageFlags.Guid)
            return;
        RequestKey = (flags & RequestKeyMask) != 0 ? requestKey : default;
        ResponseKey = (flags & ResponseKeyMask) != 0 ? responseKey : default;
        if ((flags & RequestKeyMask) != 0)
            Length += sizeof(long);
        if ((flags & ResponseKeyMask) != 0)
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
        if ((Flags & MessageFlags.Guid) == 0)
        {
            UnsafeBitConverter.GetBytes(output, (ushort)MessageId, length);
            length += sizeof(ushort);
        }
        else
        {
            byte[] bytes = MessageGuid.ToByteArray();
            fixed (byte* ptr = bytes)
                Buffer.MemoryCopy(ptr, output + length, 16, 16);
            length += 16;
        }
        UnsafeBitConverter.GetBytes(output, Size, length);
        length += sizeof(int);
        if ((Flags & RequestKeyMask) != 0)
        {
            UnsafeBitConverter.GetBytes(output, RequestKey, length);
            length += sizeof(long);
        }
        if ((Flags & ResponseKeyMask) != 0)
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
        if ((Flags & MessageFlags.Guid) == 0)
        {
            if (MessageId == 0)
                msg = "Unknown Message Type (" + MessageId.ToString(CultureInfo.InvariantCulture) + ");";
            else
                msg = "Msg: " + NetFactory.GetInvokerName(MessageId, (Flags & MessageFlags.HighSpeed) != 0) + ";";
        }
        else
        {
            if (MessageGuid == Guid.Empty)
                msg = "Unknown Message Type (" + MessageGuid.ToString("N") + ");";
            else
                msg = "Msg: " + NetFactory.GetInvokerName(MessageGuid, (Flags & MessageFlags.HighSpeed) != 0) + ";";
        }
        msg += " " + FormattingUtil.FormatCapacity(Size) + ";";
        if (ResponseKey != 0)
            msg += " Snowflake: " + RequestKey.ToString(CultureInfo.InvariantCulture) + ";";
        if (RequestKey != 0)
            msg += (ResponseKey == 0 ? " Snowflake: " : " Request Key: ") + RequestKey.ToString(CultureInfo.InvariantCulture) + ";";
        if (Flags is not MessageFlags.None)
            msg += " Flags: " + Flags.ToString("F");
        return msg;
    }
    public string Format(ITerminalFormatProvider provider)
    {
        string msg;
        if ((Flags & MessageFlags.Guid) == 0)
        {
            if (MessageId == 0)
                msg = "Unknown Message Type".ColorizeNoReset(FormattingColorType.Keyword) + " (".ColorizeNoReset(FormattingColorType.Punctuation) + MessageId.Format() + ");".ColorizeNoReset(FormattingColorType.Punctuation);
            else
                msg = "Msg".ColorizeNoReset(FormattingColorType.Keyword) + ": ".ColorizeNoReset(FormattingColorType.Punctuation) + NetFactory.GetInvokerName(MessageId, (Flags & MessageFlags.HighSpeed) != 0).ColorizeNoReset(FormattingColorType.Struct) + ";".ColorizeNoReset(FormattingColorType.Punctuation);
        }
        else
        {
            if (MessageGuid == Guid.Empty)
                msg = "Unknown Message Type".ColorizeNoReset(FormattingColorType.Keyword) + " (".ColorizeNoReset(FormattingColorType.Punctuation) + MessageGuid.Format("N") + ");".ColorizeNoReset(FormattingColorType.Punctuation);
            else
                msg = "Msg".ColorizeNoReset(FormattingColorType.Keyword) + ": ".ColorizeNoReset(FormattingColorType.Punctuation) + NetFactory.GetInvokerName(MessageGuid, (Flags & MessageFlags.HighSpeed) != 0).ColorizeNoReset(FormattingColorType.Struct) + ";".ColorizeNoReset(FormattingColorType.Punctuation);
        }
        msg += " " + FormattingUtil.FormatCapacity(Size, colorize: true) + ";".ColorizeNoReset(FormattingColorType.Punctuation);
        if (ResponseKey != 0)
            msg += " Snowflake".ColorizeNoReset(FormattingColorType.Keyword) + ": ".ColorizeNoReset(FormattingColorType.Punctuation) + RequestKey.Format() + ";".ColorizeNoReset(FormattingColorType.Punctuation);
        if (RequestKey != 0)
            msg += (ResponseKey == 0 ? " Snowflake".ColorizeNoReset(FormattingColorType.Keyword) : " Request Key".ColorizeNoReset(FormattingColorType.Keyword)) + ": ".ColorizeNoReset(FormattingColorType.Punctuation) + RequestKey.Format() + ";".ColorizeNoReset(FormattingColorType.Punctuation);
        if (Flags is not MessageFlags.None)
            msg += " Flags".ColorizeNoReset(FormattingColorType.Keyword) + ": ".ColorizeNoReset(FormattingColorType.Punctuation) + Flags.ToString("F").ColorizeNoReset(FormattingColorType.Struct);
        return msg + FormattingUtil.GetResetSuffix();
    }
}