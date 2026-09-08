namespace Orbyss.Foundation.Tasks;

/// <summary>Starts and drains all tasks owned by one shell generation.</summary>
public interface IShellTaskManager
{
    /// <summary>Runs startup tasks and starts background and recurring tasks.</summary>
    /// <param name="cancellationToken">Signals that shell activation was cancelled.</param>
    /// <returns>A task that represents task-system startup.</returns>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>Cancels and awaits all running shell tasks.</summary>
    /// <param name="cancellationToken">Bounds the time allowed for task-system shutdown.</param>
    /// <returns>A task that represents task-system shutdown.</returns>
    Task StopAsync(CancellationToken cancellationToken);
}
