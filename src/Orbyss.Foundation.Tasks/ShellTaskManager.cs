using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Orbyss.Foundation.Tasks;

/// <summary>Owns startup, background, and recurring tasks for one shell generation.</summary>
/// <param name="serviceProvider">The shell-generation service provider.</param>
/// <param name="backgroundTasks">The shell-singleton background tasks.</param>
/// <param name="recurringTasks">The shell-singleton recurring tasks.</param>
/// <param name="logger">The lifecycle logger.</param>
internal sealed class ShellTaskManager(
    IServiceProvider serviceProvider,
    IEnumerable<IBackgroundTask> backgroundTasks,
    IEnumerable<IRecurringTask> recurringTasks,
    ILogger<ShellTaskManager> logger) : IShellTaskManager, IAsyncDisposable
{
    /// <summary>Serializes task-system startup and shutdown state transitions.</summary>
    private readonly object sync = new();
    /// <summary>The background tasks owned by this shell generation.</summary>
    private readonly IReadOnlyList<IBackgroundTask> backgroundTasks = backgroundTasks.ToArray();
    /// <summary>The recurring tasks owned by this shell generation.</summary>
    private readonly IReadOnlyList<IRecurringTask> recurringTasks = recurringTasks.ToArray();
    /// <summary>Cancels work when the owning shell generation drains.</summary>
    private CancellationTokenSource? lifetimeCancellation;
    /// <summary>The observed background and recurring task executions.</summary>
    private IReadOnlyList<Task> runningTasks = [];
    /// <summary>The shared idempotent shutdown operation.</summary>
    private Task? stopTask;
    /// <summary>Indicates whether startup has already begun.</summary>
    private bool started;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        lock (sync)
        {
            if (started)
                throw new InvalidOperationException("Shell tasks cannot be started twice in the same shell generation.");

            started = true;
            lifetimeCancellation = new CancellationTokenSource();
        }

        try
        {
            await RunStartupTasksAsync(cancellationToken).ConfigureAwait(false);
            var token = lifetimeCancellation.Token;
            runningTasks =
            [
                .. backgroundTasks.Select(task => RunBackgroundTaskAsync(task, token)),
                .. recurringTasks.Select(task => RunRecurringTaskAsync(task, token))
            ];
        }
        catch
        {
            await StopAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        lock (sync)
        {
            stopTask ??= StopCoreAsync(cancellationToken);
            return stopTask;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        lifetimeCancellation?.Dispose();
    }

    /// <summary>Runs scoped startup tasks in registration order.</summary>
    /// <param name="cancellationToken">Signals that shell activation was cancelled.</param>
    /// <returns>A task that represents all startup tasks.</returns>
    private async Task RunStartupTasksAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        foreach (var task in scope.ServiceProvider.GetServices<IStartupTask>())
        {
            logger.LogInformation("Starting shell startup task {TaskId}.", task.Id);
            await task.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Runs and observes one background task.</summary>
    /// <param name="task">The background task.</param>
    /// <param name="cancellationToken">Signals that the shell generation is stopping.</param>
    /// <returns>A task that represents the complete background operation.</returns>
    private async Task RunBackgroundTaskAsync(IBackgroundTask task, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Starting shell background task {TaskId}.", task.Id);
            await task.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Shell background task {TaskId} faulted.", task.Id);
            throw;
        }
    }

    /// <summary>Runs and observes one recurring task until cancellation.</summary>
    /// <param name="task">The recurring task.</param>
    /// <param name="cancellationToken">Signals that the shell generation is stopping.</param>
    /// <returns>A task that represents the recurring loop.</returns>
    private async Task RunRecurringTaskAsync(IRecurringTask task, CancellationToken cancellationToken)
    {
        if (task.Interval <= TimeSpan.Zero)
            throw new InvalidOperationException($"Recurring task '{task.Id}' must declare a positive interval.");

        try
        {
            if (task.InitialDelay > TimeSpan.Zero)
                await Task.Delay(task.InitialDelay, cancellationToken).ConfigureAwait(false);

            using var timer = new PeriodicTimer(task.Interval);
            do
            {
                await task.ExecuteAsync(cancellationToken).ConfigureAwait(false);
            }
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Shell recurring task {TaskId} faulted.", task.Id);
            throw;
        }
    }

    /// <summary>Cancels and awaits all observed task executions once.</summary>
    /// <param name="cancellationToken">Bounds the time allowed for shutdown.</param>
    /// <returns>A task that represents shutdown.</returns>
    private async Task StopCoreAsync(CancellationToken cancellationToken)
    {
        lifetimeCancellation?.Cancel();
        if (runningTasks.Count == 0)
            return;

        try
        {
            await Task.WhenAll(runningTasks).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "One or more shell tasks failed while the shell was draining.");
        }
    }
}
