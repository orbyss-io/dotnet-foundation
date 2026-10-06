using Microsoft.EntityFrameworkCore;
using Orbyss.Foundation.Execution.Core;

namespace Orbyss.Foundation.PostgreSql;

/// <summary>An owned tracked shell scope whose active contexts retain its datasource until disposal.</summary>
public sealed class PostgreSqlUnitLease<TContext> : IAsyncDisposable where TContext : FoundationPostgreSqlDbContext
{
    /// <summary>Owns admission and final tracked scope release.</summary>
    private readonly ContextLeaseState<TContext> state;
    /// <summary>Creates the public owned unit around its scoped native factory.</summary>
    internal PostgreSqlUnitLease(ContextLeaseState<TContext> state, IDbContextFactory<TContext> factory)
    {
        this.state = state;
        Factory = factory;
    }
    /// <summary>Gets the nonpooled native factory; each invocation creates an independent unit.</summary>
    public IDbContextFactory<TContext> Factory { get; }
    /// <summary>Gets the deadline retained by this unit and its contexts.</summary>
    public IExecutionDeadline Deadline => state.Deadline;
    /// <summary>Gets a stable non-secret source identity.</summary>
    public Guid DataSourceId => state.DataSource.Id;
    /// <inheritdoc />
    public ValueTask DisposeAsync() => state.CloseAsync();
}
