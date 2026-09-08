namespace Orbyss.Foundation.Tasks;

/// <summary>
/// Runs for the lifetime of one shell generation. The returned task must represent the complete operation.
/// </summary>
public interface IBackgroundTask : IFoundationTask
{
    /// <summary>Runs the background operation until completion or shell cancellation.</summary>
    /// <param name="cancellationToken">Signals that the owning shell generation is stopping.</param>
    /// <returns>A task that represents the complete background operation.</returns>
    Task ExecuteAsync(CancellationToken cancellationToken);
}
