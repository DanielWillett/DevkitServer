namespace DevkitServer.Util.Encoding;

/// <summary>
/// Read-only stream for processing JSON input.
///<br/><br/>
/// Features:<br/>
///  * Remove comments (leaves white space, not a big deal for auto-parsers)<br/>
///  * Skip UTF-8 byte order mark.
/// </summary>
public class Utf8JsonPreProcessingStream : Stream
{
    private static byte[]? _readBuffer;

    /// <summary>
    /// Default size of the UTF-8 buffer.
    /// </summary>
    /// <remarks>Value: 128 bytes.</remarks>
    public const int DefaultBufferSize = 128;

    /// <summary>
    /// Size of the UTF-8 byte order mark in bytes.
    /// </summary>
    public const int Utf8BomSize = 3;

    /// <summary>
    /// Composite of the UTF-8 byte order mark starting at bit 24.
    /// </summary>
    public const int Utf8Bom = (0xEF << 16) | (0xBB << 8) | 0xBF;

    private const byte CommentSlash = (byte)'/';
    private const byte CommentStar = (byte)'*';
    private const byte DoubleQuote = (byte)'"';
    private const byte EscapeSlash = (byte)'\\';

    private readonly Stream _stream;
    private readonly bool _tryConsumeBom;
    private readonly bool _removeComments;
    private readonly byte[] _buffer;

    private long _position;
    private int _underlyingPosition;
    private int _bufferPos = -1;
    private int _bufferSize;
    private bool _inString;
    private bool _lastWasEscape;
    private bool _hasConsumedUtf8Bom;
    private int _readOneBomProgress;
    private bool _disposed;

    public Utf8JsonPreProcessingStream(string file, int bufferSize = DefaultBufferSize, bool removeComments = true, bool tryConsumeBom = true)
    {
        if (bufferSize < 0)
            bufferSize = DefaultBufferSize;
        else if (tryConsumeBom && bufferSize < Utf8BomSize)
            bufferSize = Utf8BomSize;

        _stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
        LeaveOpen = false;
        _buffer = new byte[bufferSize];
        _tryConsumeBom = tryConsumeBom;
        _hasConsumedUtf8Bom = !tryConsumeBom;
        _removeComments = removeComments;
    }
    public Utf8JsonPreProcessingStream(Stream stream, int bufferSize = DefaultBufferSize, bool removeComments = true, bool tryConsumeBom = true, bool leaveOpen = true)
    {
        if (!stream.CanRead)
            throw new ArgumentException("Stream must be readable.", nameof(stream));

        if (bufferSize < 0)
            bufferSize = DefaultBufferSize;
        else if (tryConsumeBom && bufferSize < Utf8BomSize)
            bufferSize = Utf8BomSize;

        _stream = stream;
        _buffer = new byte[bufferSize];
        LeaveOpen = leaveOpen;
        _tryConsumeBom = tryConsumeBom;
        _hasConsumedUtf8Bom = !tryConsumeBom;
        _removeComments = removeComments;
    }

    /// <summary>
    /// Underlying stream to read from.
    /// </summary>
    public Stream Stream => _stream;

    /// <summary>
    /// Should the underlying stream be left open after <see cref="Dispose"/> is called?
    /// </summary>
    public bool LeaveOpen { get; }

    /// <summary>
    /// Always <see langword="true"/>.
    /// </summary>
    public override bool CanRead => true;

    /// <summary>
    /// Always <see langword="false"/>.
    /// </summary>
    public override bool CanSeek => false;

    /// <summary>
    /// Always <see langword="false"/>.
    /// </summary>
    public override bool CanWrite => false;

    /// <summary>
    /// Length of the underlying stream.
    /// </summary>
    /// <remarks>This is also the max length of the processed data.</remarks>
    public override long Length => _stream.Length;

    /// <summary>
    /// Number of bytes read after processing.
    /// </summary>
    public override long Position { get => _position; set => throw new NotSupportedException(); }

    /// <summary>
    /// Number of bytes read before processing.
    /// </summary>
    /// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed.</exception>
    public long UnderlyingPosition => _underlyingPosition;

    /// <summary>
    /// Seek to the beginning of the stream and reset all buffers.
    /// </summary>
    /// <exception cref="T:System.IO.IOException">An I/O error occurs.</exception>
    /// <exception cref="T:System.NotSupportedException">The stream does not support seeking, such as if the stream is constructed from a pipe or console output.</exception>
    /// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed.</exception>
    public void Reset()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(Utf8JsonPreProcessingStream));

        _stream.Seek(0, SeekOrigin.Begin);
        _bufferPos = -1;
        _inString = false;
        _lastWasEscape = false;
        _position = 0;
        _underlyingPosition = 0;
        _hasConsumedUtf8Bom = !_tryConsumeBom;
        _readOneBomProgress = 0;
    }

    /// <summary>
    /// Does nothing.
    /// </summary>
    /// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed.</exception>
    public override void Flush()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(Utf8JsonPreProcessingStream));
    }

    /// <summary>
    /// Returns a span of bytes representing the processed JSON data.
    /// </summary>
    /// <param name="allowStaticBuffer">When this is <see langword="true"/>, uses a static buffer if on the main thread.</param>
    public ArraySegment<byte> ReadAllBytes(bool allowStaticBuffer = true)
    {
        int len = (int)Math.Min(int.MaxValue, Length);
        byte[] buffer;
        if (allowStaticBuffer && DevkitServerModule.IsMainThread)
        {
            buffer = _readBuffer ??= new byte[len];
            if (buffer.Length < len)
                _readBuffer = buffer = new byte[len];
        }
        else
            buffer = new byte[len];

        int ct = Read(buffer, 0, len);
        return new ArraySegment<byte>(buffer, 0, ct);
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(Utf8JsonPreProcessingStream));

        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer));

        if (offset >= buffer.Length && (count != 0 || offset != 0))
            throw new ArgumentOutOfRangeException(nameof(offset));

        if (count == 0)
            return 0;

        if (count + offset > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(count));

        int index = 0;
        while (true)
        {
            if (_bufferPos < 0)
            {
                int readCt = _stream.Read(_buffer, 0, _buffer.Length);
                _underlyingPosition += readCt;
                _bufferSize = readCt;
                _bufferPos = 0;
                if (readCt == 0)
                {
                    _bufferPos = -1;
                    return index;
                }
                if (!_hasConsumedUtf8Bom && _underlyingPosition - readCt == _readOneBomProgress && readCt >= Utf8BomSize)
                    SkipBom(0);
            }

            int ct = Math.Min(count - index, _bufferSize - _bufferPos);
            for (int i = 0; i < ct; ++i)
            {
                byte val = _buffer[_bufferPos++];
                ++_position;
                if (val == CommentSlash && !_inString && _removeComments)
                {
                    SkipComment();
                    if (_bufferPos < 0)
                        break;
                    i = 0;
                    ct = Math.Min(count - index, _bufferSize - _bufferPos + 1);
                }
                else
                {
                    ConsumeByte(val);
                    buffer[index] = val;
                    ++index;
                }
            }

            if (index >= count)
            {
                if (_bufferPos >= _bufferSize)
                    _bufferPos = -1;
                return index;
            }

            _bufferPos = -1;
        }
    }

    /// <inheritdoc />
    public override int ReadByte()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(Utf8JsonPreProcessingStream));

        while (true)
        {
            int val;
            if (_bufferPos >= 0)
            {
                val = _buffer[_bufferPos++];
                if (_bufferPos >= _bufferSize)
                    _bufferPos = -1;

            }
            else
            {
                val = _stream.ReadByte();
                if (val == -1)
                    return -1;
                ++_underlyingPosition;
                if (!_hasConsumedUtf8Bom && _underlyingPosition - 1 == _readOneBomProgress)
                {
                    if (SkipBom((byte)val))
                    {
                        ++_readOneBomProgress;
                        continue;
                    }
                }
            }
            ++_position;

            if (val == CommentSlash && !_inString && _removeComments)
                SkipComment();
            else
            {
                ConsumeByte((byte)val);
                return val;
            }
        }
    }
    private void ConsumeByte(byte val)
    {
        if (val == EscapeSlash)
        {
            _lastWasEscape = !_lastWasEscape;
        }
        else if (val == DoubleQuote)
        {
            if (_lastWasEscape)
                return;

            _inString = !_inString;
        }
    }
    private bool SkipBom(byte val)
    {
        for (; _readOneBomProgress < Utf8BomSize; ++_readOneBomProgress)
        {
            byte b = (byte)(Utf8Bom >> ((Utf8BomSize - _readOneBomProgress - 1) * 8));
            if (_bufferPos >= 0)
            {
                if (b == _buffer[_bufferPos])
                {
                    ++_bufferPos;
                    if (_readOneBomProgress == Utf8BomSize - 1)
                        return true;
                }
                else return false;
            }
            else
                return val == b;
        }

        return false;
    }
    private void SkipComment()
    {
        bool multiLine = false;
        bool isWaitingForLastSlash = false;
        bool first = true;
        do
        {
            int ct;
            if (_bufferPos < 0)
            {
                ct = _stream.Read(_buffer, 0, _buffer.Length);
                _underlyingPosition += ct;
                _bufferSize = ct;
                _bufferPos = 0;
                if (ct == 0)
                {
                    _bufferPos = -1;
                    break;
                }
                if (!_hasConsumedUtf8Bom && _underlyingPosition - ct == _readOneBomProgress && ct >= Utf8BomSize)
                    SkipBom(0);
            }
            else
            {
                ct = _bufferSize - _bufferPos;
                if (ct <= 0)
                {
                    _bufferPos = -1;
                    continue;
                }
            }

            if (first)
            {
                multiLine = _buffer[_bufferPos] == CommentStar;
                if (first && ct > 1 && (!multiLine && _buffer[_bufferPos] != CommentSlash || multiLine && _buffer[_bufferPos] != CommentStar))
                    break;
            }
            else if (isWaitingForLastSlash && _buffer[_bufferPos] == CommentSlash)
            {
                ++_bufferPos;
                if (_bufferPos >= _bufferSize)
                    _bufferPos = -1;
                break;
            }

            isWaitingForLastSlash = false;

            if (first)
            {
                ++_bufferPos;
                --ct;
                if (_bufferPos >= _bufferSize)
                    _bufferPos = -1;
            }
            for (int i = 0; i < ct; ++i)
            {
                byte character = _buffer[i + _bufferPos];
                if (character is (byte)'\r' or (byte)'\n')
                {
                    if (multiLine)
                        continue;

                    _bufferPos = ct - 1 != i ? i + _bufferPos : -1;
                    if (_bufferPos >= _bufferSize)
                        _bufferPos = -1;
                    _position += ct;
                    return;
                }
                if (character == CommentStar)
                {
                    if (!multiLine)
                        continue;

                    if (ct - 1 == i)
                    {
                        isWaitingForLastSlash = true;
                        _bufferPos = -1;
                        break;
                    }

                    if (_buffer[i + _bufferPos + 1] != CommentSlash)
                        continue;

                    _bufferPos = i + _bufferPos + 2;
                    if (_bufferPos >= _bufferSize)
                        _bufferPos = -1;
                    _position += ct;
                    return;
                }
            }

            first = false;

            _position += ct;
            _bufferPos = -1;
        }
        while (true);
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed)
            return;
        if (!LeaveOpen)
            _stream.Dispose();

        _bufferPos = -1;
        _inString = false;
        _lastWasEscape = false;
        _position = 0;
        _underlyingPosition = 0;
        _hasConsumedUtf8Bom = !_tryConsumeBom;
        _readOneBomProgress = 0;
        _disposed = true;
    }

    /// <summary>
    /// Not supported.
    /// </summary>
    /// <exception cref="NotSupportedException"/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <summary>
    /// Not supported.
    /// </summary>
    /// <exception cref="NotSupportedException"/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <summary>
    /// Not supported.
    /// </summary>
    /// <exception cref="NotSupportedException"/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}