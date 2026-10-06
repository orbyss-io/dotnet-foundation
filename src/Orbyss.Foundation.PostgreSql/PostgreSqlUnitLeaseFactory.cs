using CShells.Lifecycle;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Orbyss.Foundation.Execution.Core;

namespace Orbyss.Foundation.PostgreSql;

/// <summary>Begins tracked scopes on its exact shell generation, including during initialization.</summary>
internal sealed class PostgreSqlUnitLeaseFactory<TContext>(
    IShell shell,
    IExecutionDeadlineFactory deadlines,
    PostgreSqlContextRegistration<TContext> registration) : IPostgreSqlUnitLeaseFactory<TContext>
    where TContext : FoundationPostgreSqlDbContext
{
    /// <inheritdoc />
    public async ValueTask<PostgreSqlUnitLease<TContext>> BeginUnitAsync(CancellationToken callerCancellation = default, IExecutionDeadline? operationDeadline = null)
    {
        callerCancellation.ThrowIfCancellationRequested();
        var scope = shell.BeginScope();
        IExecutionDeadline? deadline = null;
        try
        {
            deadline = operationDeadline is null
                ? deadlines.Create(registration.Policy.OperationTimeout, callerCancellation)
                : operationDeadline.BeginStage(registration.Policy.OperationTimeout, callerCancellation);
            var state = scope.ServiceProvider.GetRequiredService<ContextLeaseState<TContext>>();
            state.Initialize(scope, deadline, registration.DataSource, registration.Policy);
            return new PostgreSqlUnitLease<TContext>(state, scope.ServiceProvider.GetRequiredService<IDbContextFactory<TContext>>());
        }
        catch
        {
            try { deadline?.Dispose(); }
            finally { await scope.DisposeAsync().ConfigureAwait(false); }
            throw;
        }
    }
}
