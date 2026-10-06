using Orbyss.Foundation.Execution.Core;

namespace Orbyss.Foundation.PostgreSql;

/// <summary>Owns tracked shell scopes for initialization, requests, background work and cleanup alike.</summary>
public interface IPostgreSqlUnitLeaseFactory<TContext> where TContext : FoundationPostgreSqlDbContext
{
    /// <summary>Creates an owned scope with a deadline capped by the supplied complete-operation deadline.</summary>
    /// <param name="callerCancellation">Cancellation for this operation when no outer deadline is supplied.</param>
    /// <param name="operationDeadline">An optional already owned complete-operation deadline; it is never reset.</param>
    /// <returns>The lease. Contexts created through it must each be independently disposed.</returns>
    ValueTask<PostgreSqlUnitLease<TContext>> BeginUnitAsync(CancellationToken callerCancellation = default, IExecutionDeadline? operationDeadline = null);
}
