namespace DevkitServer.Util;

public sealed class TaskYieldInstruction : CustomYieldInstruction
{
    public CancellationToken Token { get; }
    public Task Task { get; }

    public override bool keepWaiting
    {
        get
        {
            Token.ThrowIfCancellationRequested();
            if (Task.IsCanceled)
                throw new OperationCanceledException(Token);
            if (Task.IsFaulted)
            {
                if (Task.Exception is { } ex)
                    throw ex.InnerExceptions.Count == 1 ? ex.InnerExceptions[0] : ex;
                return false;
            }
            return !Task.IsCompleted;
        }
    }
    public TaskYieldInstruction(Task task, CancellationToken token = default)
    {
        Token = token;
        Task = task;
        token.ThrowIfCancellationRequested();
    }
    public bool TryGetResult<TResult>(out TResult result)
    {
        if (Task is Task<TResult> { IsCompleted: true, IsFaulted: false, IsCanceled: false } res)
        {
            result = res.Result;
            return true;
        }

        result = default!;
        return false;
    }
}
public sealed class ValueTaskYieldInstruction : CustomYieldInstruction
{
    public CancellationToken Token { get; }
    public ValueTask Task { get; }

    public override bool keepWaiting
    {
        get
        {
            Token.ThrowIfCancellationRequested();
            if (Task.IsCanceled)
                throw new OperationCanceledException();
            if (Task.IsFaulted)
            {
                if (Task.AsTask() is { Exception: { } ex })
                    throw ex.InnerExceptions.Count == 1 ? ex.InnerExceptions[0] : ex;
                return false;
            }
            return !Task.IsCompleted;
        }
    }
    public ValueTaskYieldInstruction(ValueTask task, CancellationToken token = default)
    {
        Token = token;
        Task = task;
        token.ThrowIfCancellationRequested();
    }
    public bool TryGetResult<TResult>(out TResult result)
    {
        if (Task is ValueTask<TResult> { IsCompleted: true, IsFaulted: false, IsCanceled: false } res)
        {
            result = res.Result;
            return true;
        }

        result = default!;
        return false;
    }
}
