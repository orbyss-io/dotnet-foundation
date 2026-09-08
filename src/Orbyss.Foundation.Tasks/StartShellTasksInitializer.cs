using CShells.Lifecycle;

namespace Orbyss.Foundation.Tasks;

/// <summary>Starts Orbyss Foundation tasks during shell initialization.</summary>
/// <param name="taskManager">The shell-generation task manager.</param>
public sealed class StartShellTasksInitializer(IShellTaskManager taskManager) : IShellInitializer
{
    /// <inheritdoc />
    public Task InitializeAsync(CancellationToken cancellationToken) => taskManager.StartAsync(cancellationToken);
}
