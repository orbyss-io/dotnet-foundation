using System.Data.Common;
using Orbyss.Foundation.Execution.Core;
using Microsoft.Extensions.Logging;

namespace Orbyss.Foundation.PostgreSql;

/// <summary>Owns one command cap and best-effort native cancellation through complete reader consumption.</summary>
internal sealed class PostgreSqlCommandStage : IDisposable
{
    /// <summary>The independently owned capped command deadline.</summary>
    private readonly IExecutionDeadline deadline;
    /// <summary>Owns the driver's cancellation registration.</summary>
    private readonly CancellationTokenRegistration cancellation;
    /// <summary>Guards repeated reader/command cleanup.</summary>
    private int disposed;

    /// <summary>Configures a command without changing its datasource/pool identity.</summary>
    internal PostgreSqlCommandStage(DbCommand command, PostgreSqlContextLease lease, CancellationToken caller, ILogger logger)
    {
        deadline = lease.Deadline.BeginStage(lease.Policy.CommandTimeout, caller);
        Token = deadline.Token;
        try
        {
            Token.ThrowIfCancellationRequested();
            command.CommandTimeout = PostgreSqlPolicy.Seconds(deadline.Remaining);
            cancellation = Token.Register(() => TryCancel(command, logger));
        }
        catch
        {
            deadline.Dispose();
            throw;
        }
    }
    /// <summary>Gets stage cancellation for native reader calls.</summary>
    internal CancellationToken Token { get; }
    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        try { cancellation.Dispose(); }
        finally { deadline.Dispose(); }
    }

    /// <summary>Requests native cancellation; driver acknowledgement/failure remains visible to the operation.</summary>
    private static void TryCancel(DbCommand command, ILogger logger)
    {
        try { command.Cancel(); }
        catch (Exception exception) when (exception is InvalidOperationException or DbException)
        {
            // A completed/disposed command may race cancellation. This does not establish rollback.
            // Native timeout and the supplied operation token remain independent fallbacks.
            if (exception is DbException) logger.LogWarning(
                "PostgreSQL cancellation acknowledgement failed with code {ProviderErrorCode} and failure kind {ProviderFailureKind}.",
                "postgres_cancellation_acknowledgement_failed", exception.GetType().Name);
        }
    }
}
