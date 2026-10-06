using Microsoft.Extensions.Logging;
using Npgsql;

namespace Orbyss.Foundation.PostgreSql;

/// <summary>Retains one native datasource until all factory-created context holds drain.</summary>
internal sealed class ShellPostgreSqlDataSource : IAsyncDisposable
{
    /// <summary>Serializes admission, release and disposal.</summary>
    private readonly object gate = new();
    /// <summary>Signals completion of all active context holds during teardown.</summary>
    private readonly TaskCompletionSource drained = new(TaskCreationOptions.RunContinuationsAsynchronously);
    /// <summary>The outstanding native context-unit count.</summary>
    private int active;
    /// <summary>Prevents new context admission after teardown begins.</summary>
    private bool closing;
    /// <summary>Publishes one shared disposal task for concurrent callers.</summary>
    private Task? disposal;

    /// <summary>Gets the stable native source selected by the policy.</summary>
    internal NpgsqlDataSource Source { get; }
    /// <summary>Gets a non-secret provider-generation identity for qualification/diagnostics.</summary>
    internal Guid Id { get; } = Guid.NewGuid();

    /// <summary>Builds an admitted source; opening connections remains operation-owned.</summary>
    internal ShellPostgreSqlDataSource(PostgreSqlPolicy policy, ILoggerFactory loggerFactory) => Source = new NpgsqlDataSourceBuilder(policy.ConnectionString)
        .UseLoggerFactory(new PostgreSqlRedactingLoggerFactory(loggerFactory)).EnableParameterLogging(false).Build();

    /// <summary>Admits a context hold only while this generation's datasource is available.</summary>
    internal void Acquire()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(closing, this);
            active++;
        }
    }

    /// <summary>Releases a context hold after native context disposal has completed.</summary>
    internal void Release()
    {
        lock (gate)
        {
            if (--active < 0) throw new InvalidOperationException("A PostgreSQL context hold was released twice.");
            if (closing && active == 0) drained.TrySetResult();
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        lock (gate)
        {
            if (disposal is not null) return new ValueTask(disposal);
            closing = true;
            if (active == 0) drained.TrySetResult();
            disposal = DisposeCoreAsync();
            return new ValueTask(disposal);
        }
    }

    /// <summary>Waits for units even if host emergency teardown bypasses its ordinary scope wait.</summary>
    private async Task DisposeCoreAsync()
    {
        await drained.Task.ConfigureAwait(false);
        await Source.DisposeAsync().ConfigureAwait(false);
    }
}
