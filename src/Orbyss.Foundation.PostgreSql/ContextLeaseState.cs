using CShells.Lifecycle;
using Orbyss.Foundation.Execution.Core;

namespace Orbyss.Foundation.PostgreSql;

/// <summary>Attaches a scoped factory to an explicitly owned tracked shell lease.</summary>
internal sealed class ContextLeaseState<TContext> where TContext : FoundationPostgreSqlDbContext
{
    /// <summary>Serializes owner closure and context reservations.</summary>
    private readonly object gate = new();
    /// <summary>The actual CShells tracked scope, retained until the last context leaves.</summary>
    private IShellScope? scope;
    /// <summary>The number of owner/context references retaining that scope.</summary>
    private int references;
    /// <summary>Rejects new units after the owner closes admission.</summary>
    private bool closed;
    /// <summary>Retains the owner's one pending/completed/faulted close outcome for all disposal callers.</summary>
    private Task? ownerClosure;
    /// <summary>The owned deadline spanning this lease's independent contexts.</summary>
    internal IExecutionDeadline Deadline { get; private set; } = null!;
    /// <summary>The selected generation's datasource.</summary>
    internal ShellPostgreSqlDataSource DataSource { get; private set; } = null!;
    /// <summary>The immutable native policy.</summary>
    internal PostgreSqlPolicy Policy { get; private set; } = null!;

    /// <summary>Enables a factory only from the tracked lease adapter.</summary>
    internal void Initialize(IShellScope ownedScope, IExecutionDeadline deadline, ShellPostgreSqlDataSource dataSource, PostgreSqlPolicy policy)
    {
        lock (gate)
        {
            if (scope is not null) throw new InvalidOperationException("A PostgreSQL scope can own only one context lease.");
            scope = ownedScope;
            references = 1;
            Deadline = deadline;
            DataSource = dataSource;
            Policy = policy;
        }
    }

    /// <summary>Rejects root/plain-DI/out-of-lease creation and reserves both source and tracked scope.</summary>
    internal PostgreSqlContextLease Acquire()
    {
        lock (gate)
        {
            if (scope is null || closed) throw new InvalidOperationException("Context creation requires an open Foundation PostgreSQL tracked shell lease.");
            Deadline.Token.ThrowIfCancellationRequested();
            DataSource.Acquire();
            references++;
            return new PostgreSqlContextLease(Deadline, DataSource, Policy, ReleaseAsync);
        }
    }

    /// <summary>Closes new admission while retaining the scope for contexts already in use.</summary>
    internal ValueTask CloseAsync()
    {
        TaskCompletionSource completion;
        lock (gate)
        {
            if (ownerClosure is not null) return new ValueTask(ownerClosure);
            closed = true;
            completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            ownerClosure = completion.Task;
        }
        _ = CompleteOwnerClosureAsync(completion);
        return new ValueTask(completion.Task);
    }

    /// <summary>Publishes closure before disposal callbacks can reenter the owner.</summary>
    private async Task CompleteOwnerClosureAsync(TaskCompletionSource completion)
    {
        try { await ReleaseAsync().ConfigureAwait(false); completion.TrySetResult(); }
        catch (Exception exception) { completion.TrySetException(exception); }
    }

    /// <summary>Disposes the tracked scope exactly after its final reference is released.</summary>
    private ValueTask ReleaseAsync()
    {
        IShellScope? release;
        lock (gate)
        {
            if (--references < 0) throw new InvalidOperationException("A tracked PostgreSQL lease was released twice.");
            release = references == 0 ? scope : null;
        }
        return release is null ? ValueTask.CompletedTask : DisposeOwnedScopeAsync(release);
    }

    /// <summary>Releases timers and tracked ownership after all contexts have finished.</summary>
    private async ValueTask DisposeOwnedScopeAsync(IShellScope release)
    {
        try { Deadline.Dispose(); }
        finally { await release.DisposeAsync().ConfigureAwait(false); }
    }
}
