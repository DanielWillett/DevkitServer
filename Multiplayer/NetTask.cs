using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;
using Action = System.Action;

namespace DevkitServer.Multiplayer;

public sealed class NetTask
{
    internal const int DEFAULT_TIMEOUT_MS = 5000;
    internal const int POLL_SPEED_MS = 25;
    private readonly NetTaskAwaiter _awaiter;
    private readonly int timeoutMs;
    internal readonly long requestId;
    private Timer? timer;
    private static long lastReqId = 0;
    internal bool isCompleted = false;
    internal RequestResponse _parameters = RequestResponse.FAIL;
    internal readonly bool isAck;
    public NetTask(bool ack, int timeoutMs = DEFAULT_TIMEOUT_MS)
    {
        isAck = ack;
        if (timeoutMs / 1000d > NetFactory.MaxListenTimeout)
        {
            Logger.LogWarning("Started a listener or ack listener with a timeout longer than the max timeout (" +
                               NetFactory.MaxListenTimeout.ToString("0.##", CultureInfo.InvariantCulture) +
                               " seconds). Using max as timeout.");
            Logger.LogWarning(new StackTrace().ToString());
            timeoutMs = (int)Math.Floor(NetFactory.MaxListenTimeout * 1000d);
        }
        requestId = GetNextRequestID();
        _awaiter = new NetTaskAwaiter(this);
        timer = new Timer(TimerMethod, this, timeoutMs, Timeout.Infinite);
        this.timeoutMs = timeoutMs;
    }
    internal static long GetNextRequestID()
    {
        long ticks = DateTime.UtcNow.Ticks;
        if (ticks <= lastReqId)
            ticks = lastReqId + 1;
        lastReqId = ticks;
        return ticks;
    }
    private static void TimerMethod(object state)
    {
        if (state is NetTask req)
        {
            if (!req.isCompleted)
            {
                req.TellCompleted(Array.Empty<object>(), false);
                NetFactory.RemoveListener(req);
            }
            else
                req.timer?.Dispose();
            req.timer = null;
        }
    }
    internal void TellCompleted(object[] parameters, bool responded)
    {
        isCompleted = true;
        if (!responded || parameters is null || parameters.Length == 0 || parameters[0] is not MessageContext ctx)
        {
            _parameters = RequestResponse.FAIL;
        }
        else
        {
            object[] p = parameters.Length == 1 ? new object[0] : new object[parameters.Length - 1];
            if (p.Length > 0)
                Array.Copy(parameters, 1, p, 0, p.Length);
            _parameters = new RequestResponse(true, ctx, p);
        }
        if (timer != null)
        {
            timer.Dispose();
            timer = null;
        }
        _awaiter.TellComplete();
    }
    internal void TellCompleted(in MessageContext ctx, bool responded, int? errorCode)
    {
        isCompleted = true;
        _parameters = !responded
            ? RequestResponse.FAIL
            : errorCode.HasValue
                ? new RequestResponse(true, ctx, errorCode.Value)
                : new RequestResponse(true, ctx, Array.Empty<object>());
        if (timer != null)
        {
            timer.Dispose();
            timer = null;
        }
        _awaiter.TellComplete();
    }
    public NetTaskAwaiter GetAwaiter() => _awaiter;
    public sealed class NetTaskAwaiter : INotifyCompletion
    {
        private readonly NetTask task;
        private readonly DateTime start;
        private readonly DateTime end;
        public NetTaskAwaiter(NetTask task)
        {
            this.task = task ?? throw new ArgumentNullException(nameof(task), "Task was null in NetTaskResult constructor.");
            start = DateTime.UtcNow;
            end = start.AddMilliseconds(task.timeoutMs);
        }
        public bool IsCompleted { get => task.isCompleted; }
        private Action? continuation;
        public void OnCompleted(Action continuation)
        {
            this.continuation = continuation;
        }
        internal void TellComplete()
        {
            task.isCompleted = true;
            continuation?.Invoke();
        }
        public RequestResponse GetResult()
        {
            if (task.isCompleted || task.timer == null) return task._parameters;
            NetTask task1 = this.task;
            SpinWait.SpinUntil(() => task1.isCompleted || DateTime.UtcNow > task1._awaiter.end);
            if (!task.isCompleted)
                NetFactory.RemoveListener(task);
            return task._parameters.Parameters is null ? RequestResponse.FAIL : task._parameters;
        }
    }
}
public readonly struct RequestResponse
{
    public readonly static RequestResponse FAIL = new RequestResponse(false, MessageContext.Nil, Array.Empty<object>());
    public readonly bool Responded;
    public readonly MessageContext Context;
    public readonly object[] Parameters;
    public readonly int? ErrorCode;

    public RequestResponse(bool responded, MessageContext context, int errorCode) : this(responded, context, Array.Empty<object>())
    {
        this.ErrorCode = errorCode;
    }
    public RequestResponse(bool responded, MessageContext context, object[] parameters)
    {
        Responded = responded;
        Context = context;
        Parameters = parameters;
        ErrorCode = null;
    }

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
}
