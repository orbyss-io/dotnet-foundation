namespace Orbyss.Foundation.Execution.Core;

/// <summary>Creates one owned outer deadline for a complete operation.</summary>
public interface IExecutionDeadlineFactory
{
    /// <summary>Creates an operation deadline without resetting it during subsequent stages or retries.</summary>
    /// <param name="duration">The positive operation budget.</param>
    /// <param name="callerCancellation">The caller's cancellation token.</param>
    /// <returns>The owned deadline, which the caller must dispose.</returns>
    IExecutionDeadline Create(TimeSpan duration, CancellationToken callerCancellation = default);
}
