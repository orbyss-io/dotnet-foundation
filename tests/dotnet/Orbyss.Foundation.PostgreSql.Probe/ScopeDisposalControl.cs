using System.Collections.Concurrent;

internal sealed class ScopeDisposalControl
{
    private static readonly ConcurrentQueue<ScopeDisposalControl> pending = new();
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Exception? Failure { get; init; }
    public static ScopeDisposalControl Arm(Exception? failure = null)
    {
        var control = new ScopeDisposalControl { Failure = failure };
        pending.Enqueue(control);
        return control;
    }
    public static ScopeDisposalControl? Claim() => pending.TryDequeue(out var control) ? control : null;
    public async ValueTask DisposeAsync()
    {
        Entered.TrySetResult();
        await Release.Task;
        if (Failure is not null) throw Failure;
    }
}
