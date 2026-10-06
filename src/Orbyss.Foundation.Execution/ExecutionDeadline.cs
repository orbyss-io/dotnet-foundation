using Orbyss.Foundation.Execution.Core;

namespace Orbyss.Foundation.Execution;

/// <summary>Owns its expiry timer and linked cancellation sources; remaining time is monotonic.</summary>
internal sealed class ExecutionDeadline : IExecutionDeadline
{
    /// <summary>The injected timestamp and timer authority.</summary>
    private readonly TimeProvider timeProvider;
    /// <summary>The monotonic timestamp captured when this stage begins.</summary>
    private readonly long started;
    /// <summary>The stage's immutable local cap.</summary>
    private readonly TimeSpan duration;
    /// <summary>The parent retains the complete operation budget.</summary>
    private readonly IExecutionDeadline? parent;
    /// <summary>Owns timer-driven local expiry.</summary>
    private readonly CancellationTokenSource expiry;
    /// <summary>Combines caller/parent cancellation and local expiry.</summary>
    private readonly CancellationTokenSource cancellation;
    /// <summary>Guards single disposal and rejects new stages after closure.</summary>
    private int disposed;

    /// <summary>Creates a stage linked to its caller without altering any parent deadline.</summary>
    internal ExecutionDeadline(TimeProvider timeProvider, TimeSpan duration, CancellationToken caller, IExecutionDeadline? parent)
    {
        this.timeProvider = timeProvider;
        this.duration = duration;
        this.parent = parent;
        started = timeProvider.GetTimestamp();
        expiry = new CancellationTokenSource(duration, timeProvider);
        if (duration == TimeSpan.Zero) expiry.Cancel();
        cancellation = parent is null ? CancellationTokenSource.CreateLinkedTokenSource(caller, expiry.Token)
            : CancellationTokenSource.CreateLinkedTokenSource(caller, expiry.Token, parent.Token);
        Token = cancellation.Token;
    }

    /// <inheritdoc />
    public CancellationToken Token { get; }

    /// <inheritdoc />
    public TimeSpan Remaining
    {
        get
        {
            var remaining = duration - timeProvider.GetElapsedTime(started);
            if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
            return parent is not null && parent.Remaining < remaining ? parent.Remaining : remaining;
        }
    }

    /// <inheritdoc />
    public bool IsExpired => Remaining == TimeSpan.Zero || parent?.IsExpired == true;

    /// <inheritdoc />
    public IExecutionDeadline BeginStage(TimeSpan maximumDuration, CancellationToken callerCancellation = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maximumDuration, TimeSpan.Zero);
        var remaining = Remaining;
        return new ExecutionDeadline(timeProvider, remaining < maximumDuration ? remaining : maximumDuration, callerCancellation, this);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        try { cancellation.Cancel(); }
        finally
        {
            cancellation.Dispose();
            expiry.Dispose();
        }
    }
}
