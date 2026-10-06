using Microsoft.EntityFrameworkCore;
using Orbyss.Foundation.Execution.Core;

namespace Orbyss.Foundation.PostgreSql;

/// <summary>Native EF context base that ties disposal to its factory-owned shell/data-source hold.</summary>
/// <remarks>Use only in the persistence implementation. It owns no application entities or transaction policy.</remarks>
public abstract class FoundationPostgreSqlDbContext(DbContextOptions options) : DbContext(options)
{
    /// <summary>The exact native options admitted by the selected registration adapter.</summary>
    private readonly DbContextOptions creationOptions = options;
    /// <summary>The context's admitted tracked reservation.</summary>
    private PostgreSqlContextLease? lease;
    /// <summary>Publishes one disposal task for sync/async/concurrent callers.</summary>
    private Task? disposal;

    /// <summary>Gets the operation deadline; use its token for application-owned database calls.</summary>
    public IExecutionDeadline Deadline => Lease.Deadline;
    /// <summary>Gets the non-secret stable source identity for this provider generation/policy.</summary>
    public Guid DataSourceId => Lease.DataSource.Id;
    /// <summary>Gets the admitted native policy and tracked hold for interceptors.</summary>
    internal PostgreSqlContextLease Lease => lease ?? throw new InvalidOperationException("This context was not created inside an owned PostgreSQL lease.");

    /// <summary>Attaches the factory hold before the context can perform I/O.</summary>
    internal void AttachLease(PostgreSqlContextLease ownedLease, DbContextOptions admittedOptions)
    {
        if (!ReferenceEquals(creationOptions, admittedOptions)) throw new InvalidOperationException("The context constructor must preserve its factory-admitted native options.");
        if (Volatile.Read(ref disposal) is not null || Interlocked.CompareExchange(ref lease, ownedLease, null) is not null)
            throw new InvalidOperationException("The factory cannot attach an already disposed or leased context.");
    }

    /// <summary>Native provider selection belongs to registration; contexts retain their own model mapping.</summary>
    protected sealed override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) => base.OnConfiguring(optionsBuilder);

    /// <inheritdoc />
    public sealed override void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    /// <inheritdoc />
    public sealed override ValueTask DisposeAsync()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var existing = Interlocked.CompareExchange(ref disposal, completion.Task, null);
        if (existing is not null) return new ValueTask(existing);
        _ = DisposeCoreAsync(completion);
        return new ValueTask(completion.Task);
    }

    /// <summary>Completes native disposal before releasing datasource and tracked scope ownership.</summary>
    private async Task DisposeCoreAsync(TaskCompletionSource completion)
    {
        try
        {
            try { await base.DisposeAsync().ConfigureAwait(false); }
            finally
            {
                if (lease is not null) await lease.DisposeAsync().ConfigureAwait(false);
            }
            completion.TrySetResult();
        }
        catch (Exception exception) { completion.TrySetException(exception); }
    }
}
