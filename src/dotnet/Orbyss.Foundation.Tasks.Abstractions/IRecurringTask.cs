namespace Orbyss.Foundation.Tasks;

/// <summary>Runs periodically for one shell generation and once per host replica.</summary>
public interface IRecurringTask : IFoundationTask
{
    /// <summary>Gets the delay between completed recurring executions.</summary>
    TimeSpan Interval { get; }

    /// <summary>Gets the delay before the first recurring execution.</summary>
    TimeSpan InitialDelay => TimeSpan.Zero;

    /// <summary>Runs one occurrence of the recurring operation.</summary>
    /// <param name="cancellationToken">Signals that the owning shell generation is stopping.</param>
    /// <returns>A task that represents the occurrence.</returns>
    Task ExecuteAsync(CancellationToken cancellationToken);
}
