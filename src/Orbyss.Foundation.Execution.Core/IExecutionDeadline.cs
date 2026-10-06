namespace Orbyss.Foundation.Execution.Core;

/// <summary>An owned monotonic operation deadline with caller and expiry cancellation.</summary>
public interface IExecutionDeadline : IDisposable
{
    /// <summary>Gets the cancellation token linked to caller cancellation and deadline expiry.</summary>
    CancellationToken Token { get; }

    /// <summary>Gets the nonnegative time remaining, independently of wall-clock changes.</summary>
    TimeSpan Remaining { get; }

    /// <summary>Gets whether this deadline or its parent expired; caller cancellation alone is not expiry.</summary>
    bool IsExpired { get; }

    /// <summary>Creates an independently owned stage capped by this deadline; retries cannot reset the parent.</summary>
    /// <param name="maximumDuration">The positive maximum stage duration.</param>
    /// <param name="callerCancellation">Optional additional cancellation for this stage.</param>
    /// <returns>A stage that the caller must dispose.</returns>
    IExecutionDeadline BeginStage(TimeSpan maximumDuration, CancellationToken callerCancellation = default);
}
