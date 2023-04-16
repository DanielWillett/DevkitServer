using System.Runtime.CompilerServices;
using Action = System.Action;

namespace DevkitServer.Util;

public class MainThreadTask
{
    internal static MainThreadTask CompletedNoSkip => new MainThreadTask(false);
    internal static MainThreadTask CompletedSkip => new MainThreadTask(true);
    internal const int DefaultTimeout = 5000;
    protected readonly bool SkipFrame;
    protected volatile bool IsCompleted = false;
    protected readonly MainThreadResult Awaiter;
    public readonly CancellationToken Token;

    private MainThreadTask(bool skipFrame)
    {
        this.SkipFrame = skipFrame;
        this.Token = CancellationToken.None;
        IsCompleted = true;
        Awaiter = new MainThreadResult(this);
    }
    public MainThreadTask(bool skipFrame, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        this.SkipFrame = skipFrame;
        this.Token = token;
        Awaiter = new MainThreadResult(this);
    }
    public MainThreadResult GetAwaiter()
    {
        return Awaiter;
    }
    public sealed class MainThreadResult : INotifyCompletion
    {
        internal Action Continuation;
        public readonly MainThreadTask Task;
        public MainThreadResult(MainThreadTask task)
        {
            this.Task = task ?? throw new ArgumentNullException(nameof(task), "Task was null in MainThreadResult constructor.");
        }
        public bool IsCompleted => DevkitServerModule.IsMainThread || Task.IsCompleted;
        public void OnCompleted(Action continuation)
        {
            Task.Token.ThrowIfCancellationRequested();
            if (DevkitServerModule.IsMainThread && !Task.SkipFrame)
            {
                continuation();
                Task.IsCompleted = true;
            }
            else
            {
                this.Continuation = continuation;
                lock (DevkitServerModuleComponent.ThreadActionRequests)
                    DevkitServerModuleComponent.ThreadActionRequests.Enqueue(this);
            }
        }
        internal void Complete()
        {
            Task.IsCompleted = true;
        }

        private bool WaitCheck() => Task.IsCompleted;
        public void GetResult()
        {
            if (DevkitServerModule.IsMainThread)
                return;
            SpinWait.SpinUntil(WaitCheck, DefaultTimeout);
        }
    }
}