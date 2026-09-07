namespace Orbyss.Foundation.Tasks;

/// <summary>Runs once while a shell generation is activating.</summary>
public interface IStartupTask : IFoundationTask
{
    /// <summary>Runs the shell startup operation.</summary>
    /// <param name="cancellationToken">Signals that shell activation was cancelled.</param>
    /// <returns>A task that represents the startup operation.</returns>
    Task ExecuteAsync(CancellationToken cancellationToken);
}
