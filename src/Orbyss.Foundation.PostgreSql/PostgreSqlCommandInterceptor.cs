using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Npgsql;

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
    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        var stage = Begin(command, eventData, cancellationToken);
        if (result.HasResult) return result;
        try
        {
            // EF passes the caller token to its own dispatch. Suppress only that dispatch,
            // retaining EF's Executed callbacks and its concrete native reader ownership.
            stage.ThrowIfExpired();
            return InterceptionResult<DbDataReader>.SuppressWithResult(await command.ExecuteReaderAsync(stage.Token).ConfigureAwait(false));
        }
        catch
        {
            End(command);
            throw;
        }
    }
    /// <inheritdoc />
    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        try
        {
            Check(command);
            RetainThroughNativeClose(command, result);
            return result;
        }
        catch
        {
            try { result.Dispose(); }
            finally { End(command); }
            throw;
        }
    }
    /// <inheritdoc />
    public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        DbDataReader result, CancellationToken cancellationToken = default)
    {
        try
        {
            Check(command);
            RetainThroughNativeClose(command, result);
            return result;
        }
        catch
        {
            try { await result.DisposeAsync().ConfigureAwait(false); }
            finally { End(command); }
            throw;
        }
    }
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
    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var stage = Begin(command, eventData, cancellationToken);
        if (result.HasResult) return result;
        try
        {
            stage.ThrowIfExpired();
            return InterceptionResult<int>.SuppressWithResult(await command.ExecuteNonQueryAsync(stage.Token).ConfigureAwait(false));
        }
        catch
        {
            End(command);
            throw;
        }
    }
    /// <inheritdoc />
    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    {
        try { Check(command); return result; }
        finally { End(command); }
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
    public override async ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<object> result, CancellationToken cancellationToken = default)
    {
        var stage = Begin(command, eventData, cancellationToken);
        if (result.HasResult) return result;
        try
        {
            stage.ThrowIfExpired();
            return InterceptionResult<object>.SuppressWithResult((await command.ExecuteScalarAsync(stage.Token).ConfigureAwait(false))!);
        }
        catch
        {
            End(command);
            throw;
        }
    }
    /// <inheritdoc />
    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
    {
        try { Check(command); return result; }
        finally { End(command); }
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
    private PostgreSqlCommandStage Begin(DbCommand command, CommandEventData eventData, CancellationToken caller)
    {
        var stage = new PostgreSqlCommandStage(command, ((FoundationPostgreSqlDbContext)eventData.Context!).Lease, caller, logger);
        if (!stages.TryAdd(command, stage))
        {
            stage.Dispose();
            throw new InvalidOperationException("A context command cannot execute concurrently with itself.");
        }
        return stage;
    }

    /// <summary>Rejects completion after expiry; cancellation never establishes rollback.</summary>
    private void Check(DbCommand command)
    {
        if (stages.TryGetValue(command, out var stage)) stage.ThrowIfExpired();
    }

    /// <summary>Retains cancellation through native drain, including faults before EF's disposing notification.</summary>
    private void RetainThroughNativeClose(DbCommand command, DbDataReader result)
    {
        if (result is not NpgsqlDataReader reader) return;
        EventHandler? closed = null;
        closed = (_, _) =>
        {
            // Remove our own closure before cleanup can fail; native readers are reused by Npgsql.
            reader.ReaderClosed -= closed;
            End(command);
        };
        reader.ReaderClosed += closed;
    }

    /// <summary>Releases a completed/failed command stage.</summary>
    private void End(DbCommand command)
    {
        if (stages.TryRemove(command, out var stage)) stage.Dispose();
    }
}
