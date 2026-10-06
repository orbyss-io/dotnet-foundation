using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

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
        using var stage = lease.Deadline.BeginStage(lease.Policy.ConnectionTimeout, cancellationToken);
        stage.Token.ThrowIfCancellationRequested();
        await connection.OpenAsync(stage.Token).ConfigureAwait(false);
        return InterceptionResult.Suppress();
    }

    /// <inheritdoc />
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) =>
        ConnectionOpenedAsync(connection, eventData).GetAwaiter().GetResult();

    /// <inheritdoc />
    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        var lease = ((FoundationPostgreSqlDbContext)eventData.Context!).Lease;
        using var stage = lease.Deadline.BeginStage(lease.Policy.CommandTimeout, cancellationToken);
        stage.Token.ThrowIfCancellationRequested();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT set_config('lock_timeout', @budget, false)";
        command.CommandTimeout = PostgreSqlPolicy.Seconds(stage.Remaining);
        var parameter = command.CreateParameter();
        parameter.ParameterName = "budget";
        var remaining = lease.Deadline.Remaining;
        var cap = remaining < lease.Policy.LockTimeout ? remaining : lease.Policy.LockTimeout;
        parameter.Value = Math.Max(1, Math.Ceiling(cap.TotalMilliseconds)).ToString(System.Globalization.CultureInfo.InvariantCulture) + "ms";
        command.Parameters.Add(parameter);
        await command.ExecuteScalarAsync(stage.Token).ConfigureAwait(false);
    }
}
