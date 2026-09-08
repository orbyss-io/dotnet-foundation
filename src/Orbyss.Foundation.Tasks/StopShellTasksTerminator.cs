using CShells.Lifecycle;

namespace Orbyss.Foundation.Tasks;

/// <summary>Drains Orbyss Foundation tasks during shell termination.</summary>
/// <param name="taskManager">The shell-generation task manager.</param>
public sealed class StopShellTasksTerminator(IShellTaskManager taskManager) : IShellTerminator
{
    /// <inheritdoc />
    public Task TerminateAsync(CancellationToken cancellationToken = default) => taskManager.StopAsync(cancellationToken);
}
