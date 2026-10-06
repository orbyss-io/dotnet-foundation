using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Orbyss.Foundation.Execution.Core;

namespace Orbyss.Foundation.PostgreSql;

/// <summary>Runs native connection/pool admission inside a capped deadline and sets owned lock waits.</summary>
internal sealed class PostgreSqlConnectionInterceptor : DbConnectionInterceptor
{
    /// <inheritdoc />
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result) =>
        ConnectionOpeningAsync(connection, eventData, result).AsTask().GetAwaiter().GetResult();

    /// <inheritdoc />
    public override async ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData,
        InterceptionResult result, CancellationToken cancellationToken = default)
    {
        if (result.IsSuppressed) return result;
        var lease = ((FoundationPostgreSqlDbContext)eventData.Context!).Lease;
        try
        {
            using var stage = lease.Deadline.BeginStage(lease.Policy.ConnectionTimeout, cancellationToken);
            Admit(stage);
            await connection.OpenAsync(stage.Token).ConfigureAwait(false);
            Admit(stage);
            return InterceptionResult.Suppress();
        }
        catch (Exception failure)
        {
            await CloseRejectedConnectionAsync(connection, failure).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) =>
        ConnectionOpenedAsync(connection, eventData).GetAwaiter().GetResult();

    /// <inheritdoc />
    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        var lease = ((FoundationPostgreSqlDbContext)eventData.Context!).Lease;
        try
        {
            using var stage = lease.Deadline.BeginStage(lease.Policy.CommandTimeout, cancellationToken);
            Admit(stage);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT set_config('lock_timeout', @budget, false)";
            command.CommandTimeout = PostgreSqlPolicy.Seconds(stage.Remaining);
            var parameter = command.CreateParameter();
            parameter.ParameterName = "budget";
            var remaining = lease.Deadline.Remaining;
            var cap = remaining < lease.Policy.LockTimeout ? remaining : lease.Policy.LockTimeout;
            parameter.Value = Math.Max(1, Math.Ceiling(cap.TotalMilliseconds)).ToString(System.Globalization.CultureInfo.InvariantCulture) + "ms";
            command.Parameters.Add(parameter);
            Admit(stage);
            await command.ExecuteScalarAsync(stage.Token).ConfigureAwait(false);
            Admit(stage);
        }
        catch (Exception failure)
        {
            await CloseRejectedConnectionAsync(connection, failure).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Rejects elapsed stages even when timer callbacks or native cancellation dispatch are delayed.</summary>
    private static void Admit(IExecutionDeadline stage)
    {
        stage.Token.ThrowIfCancellationRequested();
        if (stage.IsExpired) throw new OperationCanceledException("The PostgreSQL connection admission deadline elapsed.", stage.Token);
    }

    /// <summary>Returns no rejected open/setup connection as healthy; preserves both failures if native cleanup fails.</summary>
    private static async Task CloseRejectedConnectionAsync(DbConnection connection, Exception admissionFailure)
    {
        try
        {
            if (connection.State != ConnectionState.Closed) await connection.CloseAsync().ConfigureAwait(false);
        }
        catch (Exception cleanupFailure)
        {
            throw new AggregateException("PostgreSQL connection admission and native cleanup failed.", admissionFailure, cleanupFailure);
        }
    }
}
