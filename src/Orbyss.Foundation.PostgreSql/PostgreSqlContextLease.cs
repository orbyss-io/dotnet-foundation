using Orbyss.Foundation.Execution.Core;

namespace Orbyss.Foundation.PostgreSql;

/// <summary>Releases one native datasource and tracked scope reservation exactly once.</summary>
internal sealed class PostgreSqlContextLease(
    IExecutionDeadline deadline,
    ShellPostgreSqlDataSource dataSource,
    PostgreSqlPolicy policy,
    Func<ValueTask> releaseScope) : IAsyncDisposable
{
    /// <summary>Guards concurrent/double context disposal.</summary>
    private int released;
    /// <summary>Gets the outer monotonic deadline.</summary>
    internal IExecutionDeadline Deadline { get; } = deadline;
    /// <summary>Gets the stable native source.</summary>
    internal ShellPostgreSqlDataSource DataSource { get; } = dataSource;
    /// <summary>Gets the immutable stage policy.</summary>
    internal PostgreSqlPolicy Policy { get; } = policy;

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref released, 1) != 0) return ValueTask.CompletedTask;
        DataSource.Release();
        return releaseScope();
    }
}
