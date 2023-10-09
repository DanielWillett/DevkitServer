using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace DevkitServer.Multiplayer.Networking;

/// <summary>
/// Represents an asynchronous task waiting for a <see cref="NetCall"/> request or acknowledge request. Can also be yield return'd in a coroutine.
/// </summary>
/// <remarks>Returns a <see cref="RequestResponse"/> when awaited.</remarks>
public sealed class NetTask : CustomYieldInstruction
{
    /// <summary>
    /// A completed <see cref="NetTask"/>.
    /// </summary>
    public static readonly NetTask Completed = new NetTask();

    /// <summary>
    /// Parameters returned, or <see cref="RequestResponse.Failed"/> if the operation has not been completed.
    /// </summary>
    public RequestResponse Parameters = RequestResponse.Failed;

    internal const int DefaultTimeoutMilliseconds = 5000;
    internal const int PollSpeedMilliseconds = 25;

    private readonly bool _isAck;
    private readonly NetTaskAwaiter _awaiter;
    private readonly int _timeoutMs;
    private readonly long _requestId;
    private static long _lastReqId;
    private Timer? _timeoutTimer;
    private bool _isCompleted;
    public override bool keepWaiting => !_isCompleted;
    public bool IsAcknowledgementRequest => _isAck;
    public bool IsCompleted => _isCompleted;
    public long RequestId => _requestId;
    private NetTask()
    {
        _awaiter = new NetTaskAwaiter(this);
        _isCompleted = true;
    }
    public NetTask(bool ack, int timeoutMs = DefaultTimeoutMilliseconds) : this(ack, GetNextRequestID(), timeoutMs) { }
    internal NetTask(bool ack, long reqId, int timeoutMs = DefaultTimeoutMilliseconds)
    {
        _isAck = ack;
        _requestId = reqId;
        if (timeoutMs / 1000d > NetFactory.MaxListenTimeout)
        {
            Logger.LogWarning("Started a listener or ack listener with a timeout longer than the max timeout (" +
                              NetFactory.MaxListenTimeout.ToString("0.##", CultureInfo.InvariantCulture) +
                              " seconds). Using max as timeout.");
            Logger.LogWarning(new StackTrace().ToString());
            timeoutMs = (int)Math.Floor(NetFactory.MaxListenTimeout * 1000d);
        }
        _awaiter = new NetTaskAwaiter(this);
        _timeoutTimer = new Timer(TimerMethod, this, timeoutMs, Timeout.Infinite);
        this._timeoutMs = timeoutMs;
    }

    /// <summary>
    /// Keep this <see cref="NetTask"/> from timing out.
    /// </summary>
    /// <remarks>Good for keeping a long message sent on a High-Speed connection alive by using a Steam connection.</remarks>
    public void KeepAlive()
    {
        _awaiter.EndTime = DateTime.UtcNow.AddMilliseconds(_timeoutMs);
        _timeoutTimer?.Change((int)Math.Round((_awaiter.EndTime - _awaiter.StartTime).TotalMilliseconds) + _timeoutMs, Timeout.Infinite);
        NetFactory.KeepAlive(this);
    }
    internal static long GetNextRequestID()
    {
        long ticks = DateTime.UtcNow.Ticks;
        if (ticks <= _lastReqId)
            ticks = _lastReqId + 1;
        _lastReqId = ticks;
        return ticks;
    }
    private static void TimerMethod(object state)
    {
        if (state is NetTask req)
        {
            if (!req._isCompleted)
            {
                req.TellCompleted(Array.Empty<object>(), false);
                NetFactory.RemoveListener(req);
            }
            else
                req._timeoutTimer?.Dispose();
            req._timeoutTimer = null;
        }
    }
    internal void TellCompleted(object[] parameters, bool responded)
    {
        _isCompleted = true;
        if (!responded || parameters is null || parameters.Length == 0 || parameters[0] is not MessageContext ctx)
        {
            Parameters = RequestResponse.Failed;
        }
        else
        {
            object[] p = parameters.Length == 1 ? Array.Empty<object>() : new object[parameters.Length - 1];
            if (p.Length > 0)
                Array.Copy(parameters, 1, p, 0, p.Length);
            Parameters = new RequestResponse(true, ctx, p);
        }
        if (_timeoutTimer != null)
        {
            _timeoutTimer.Dispose();
            _timeoutTimer = null;
        }
        _awaiter.TellComplete();
    }
    internal void TellCompleted(in MessageContext ctx, bool responded, int? errorCode)
    {
        _isCompleted = true;
        Parameters = !responded
            ? RequestResponse.Failed
            : errorCode.HasValue
                ? new RequestResponse(true, ctx, errorCode.Value)
                : new RequestResponse(true, ctx, Array.Empty<object>());
        if (_timeoutTimer != null)
        {
            _timeoutTimer.Dispose();
            _timeoutTimer = null;
        }
        _awaiter.TellComplete();
    }
    public NetTaskAwaiter GetAwaiter() => _awaiter;
    public sealed class NetTaskAwaiter : INotifyCompletion
    {
        private readonly NetTask _task;
        internal readonly DateTime StartTime;
        internal DateTime EndTime;
        private Action? _continuation;
        public NetTaskAwaiter(NetTask task)
        {
            _task = task ?? throw new ArgumentNullException(nameof(task), "Task was null in NetTaskResult constructor.");
            StartTime = DateTime.UtcNow;
            EndTime = StartTime.AddMilliseconds(task._timeoutMs);
        }
        public bool IsCompleted => _task._isCompleted;
        public void OnCompleted(Action continuation)
        {
            _continuation = continuation;
        }
        internal void TellComplete()
        {
            _task._isCompleted = true;
            _continuation?.Invoke();
        }

        /// <summary>
        /// Running this method directly will freeze the application.
        /// </summary>
        public RequestResponse GetResult()
        {
            if (_task._isCompleted || _task._timeoutTimer == null) return _task.Parameters;
            NetTask task1 = _task;
            SpinWait.SpinUntil(() => task1._isCompleted || DateTime.UtcNow > task1._awaiter.EndTime);
            if (!_task._isCompleted)
                NetFactory.RemoveListener(_task);
            return _task.Parameters.Parameters is null ? RequestResponse.Failed : _task.Parameters;
        }
    }
}

/// <summary>
/// Represents the response from a <see cref="NetTask"/> request or acknowledge request.
/// </summary>
public readonly struct RequestResponse
{
    public static readonly RequestResponse Failed = new RequestResponse(false, MessageContext.Nil, Array.Empty<object>());

    /// <summary>
    /// Did the request get a response at all?
    /// </summary>
    public readonly bool Responded;

    /// <summary>
    /// Context of the response.
    /// </summary>
    public readonly MessageContext Context;

    /// <summary>
    /// Parameters responded with. Use <see cref="TryGetParameter{T}"/> to access these easier.
    /// </summary>
    public readonly object[] Parameters;

    /// <summary>
    /// Optional error code. Usually can be casted to <see cref="StandardErrorCode"/>.
    /// </summary>
    public readonly int? ErrorCode;

    /// <summary>
    /// The request responded with either no error code or <see cref="StandardErrorCode.Success"/>.
    /// </summary>
    public bool Success => Responded && ErrorCode is null or (int)StandardErrorCode.Success;

    public RequestResponse(bool responded, MessageContext context, int errorCode) : this(responded, context, Array.Empty<object>())
    {
        ErrorCode = errorCode;
    }
    public RequestResponse(bool responded, MessageContext context, object[] parameters)
    {
        Responded = responded;
        Context = context;
        Parameters = parameters;
        ErrorCode = null;
    }
    public RequestResponse(bool responded, MessageContext context, int errorCode, object[] parameters)
    {
        Responded = responded;
        Context = context;
        Parameters = parameters;
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Try to cast a parameter to <typeparamref name="T"/> if there was a response.
    /// </summary>
    public bool TryGetParameter<T>(int index, out T parameter)
    {
        if (Responded && Parameters.Length > index && Parameters[index] is T t)
        {
            parameter = t;
            return true;
        }

        parameter = default!;
        return false;
    }

    public override string ToString()
    {
        if (ErrorCode.HasValue)
        {
            if (ErrorCode.Value <= (int)StandardErrorCode.NoPermissions)
                return "{ " + (StandardErrorCode)ErrorCode.Value + " (" + ErrorCode.Value.ToString("X8", CultureInfo.InvariantCulture) + ") }";
            return "{ " + ErrorCode.Value.ToString("X8", CultureInfo.InvariantCulture) + " }";
        }

        if (Responded)
            return "{ " + nameof(StandardErrorCode.Success) + " (0x????????) }";

        return "{ No Response (0x????????) }";
    }
}
