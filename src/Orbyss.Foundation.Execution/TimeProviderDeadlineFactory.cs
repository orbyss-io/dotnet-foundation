using Orbyss.Foundation.Execution.Core;

namespace Orbyss.Foundation.Execution;

/// <summary>Creates monotonic deadlines using one injected clock and its timer implementation.</summary>
public sealed class TimeProviderDeadlineFactory(TimeProvider timeProvider) : IExecutionDeadlineFactory
{
    /// <inheritdoc />
    public IExecutionDeadline Create(TimeSpan duration, CancellationToken callerCancellation = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero);
        return new ExecutionDeadline(timeProvider, duration, callerCancellation, null);
    }
}
