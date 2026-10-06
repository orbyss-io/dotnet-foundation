using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Orbyss.Foundation.PostgreSql;

/// <summary>Owns command stages and retains reader-stage cancellation until disposal.</summary>
internal sealed class PostgreSqlCommandInterceptor(ILogger logger) : DbCommandInterceptor
{
    /// <summary>Holds stages only for commands currently executing.</summary>
    private readonly ConcurrentDictionary<DbCommand, PostgreSqlCommandStage> stages = new(ReferenceEqualityComparer.Instance);

    /// <inheritdoc />
    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Begin(command, eventData, default);
        return result;
    }
    /// <inheritdoc />
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Begin(command, eventData, cancellationToken);
        return ValueTask.FromResult(result);
    }
    /// <inheritdoc />
    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result) => result;
    /// <inheritdoc />
    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        DbDataReader result, CancellationToken cancellationToken = default) => ValueTask.FromResult(ReaderExecuted(command, eventData, result));
    /// <inheritdoc />
    public override InterceptionResult DataReaderDisposing(DbCommand command, DataReaderDisposingEventData eventData, InterceptionResult result)
    {
        // Npgsql's EF adapter relies on its concrete reader. Keep the native reader and
        // retain command cancellation through EF's standard disposal notification.
        End(command);
        return result;
    }
    /// <inheritdoc />
    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        Begin(command, eventData, default);
        return result;
    }
    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Begin(command, eventData, cancellationToken);
        return ValueTask.FromResult(result);
    }
    /// <inheritdoc />
    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    {
        End(command);
        return result;
    }
    /// <inheritdoc />
    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result,
        CancellationToken cancellationToken = default) => ValueTask.FromResult(NonQueryExecuted(command, eventData, result));
    /// <inheritdoc />
    public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        Begin(command, eventData, default);
        return result;
    }
    /// <inheritdoc />
    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<object> result, CancellationToken cancellationToken = default)
    {
        Begin(command, eventData, cancellationToken);
        return ValueTask.FromResult(result);
    }
    /// <inheritdoc />
    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
    {
        End(command);
        return result;
    }
    /// <inheritdoc />
    public override ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData eventData, object? result,
        CancellationToken cancellationToken = default) => ValueTask.FromResult(ScalarExecuted(command, eventData, result));
    /// <inheritdoc />
    public override void CommandFailed(DbCommand command, CommandErrorEventData eventData) => End(command);
    /// <inheritdoc />
    public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        End(command);
        return Task.CompletedTask;
    }
    /// <inheritdoc />
    public override void CommandCanceled(DbCommand command, CommandEndEventData eventData) => End(command);
    /// <inheritdoc />
    public override Task CommandCanceledAsync(DbCommand command, CommandEndEventData eventData, CancellationToken cancellationToken = default)
    {
        End(command);
        return Task.CompletedTask;
    }

    /// <summary>Reserves a stage before any native command starts.</summary>
    private void Begin(DbCommand command, CommandEventData eventData, CancellationToken caller)
    {
        var stage = new PostgreSqlCommandStage(command, ((FoundationPostgreSqlDbContext)eventData.Context!).Lease, caller, logger);
        if (!stages.TryAdd(command, stage))
        {
            stage.Dispose();
            throw new InvalidOperationException("A context command cannot execute concurrently with itself.");
        }
    }

    /// <summary>Releases a completed/failed command stage.</summary>
    private void End(DbCommand command)
    {
        if (stages.TryRemove(command, out var stage)) stage.Dispose();
    }
}
