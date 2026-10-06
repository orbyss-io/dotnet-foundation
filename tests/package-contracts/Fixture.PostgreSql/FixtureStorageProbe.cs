using Foundation.ContractFixture.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Orbyss.Foundation.PostgreSql;
namespace Foundation.ContractFixture.PostgreSql;
internal sealed class FixtureStorageProbe(IPostgreSqlUnitLeaseFactory<FixtureDbContext> leases, IServiceProvider provider) : IFixtureStorageProbe
{
    public async Task<FixtureStorageObservation> ObserveAsync(CancellationToken cancellationToken)
    {
        var plainRejected = false;
        await using (var plain = provider.CreateAsyncScope())
        {
            try { await using var invalid = await plain.ServiceProvider.GetRequiredService<IDbContextFactory<FixtureDbContext>>().CreateDbContextAsync(cancellationToken); }
            catch (InvalidOperationException) { plainRejected = true; }
        }
        await using var unit = await leases.BeginUnitAsync(cancellationToken);
        await using var first = await unit.Factory.CreateDbContextAsync(cancellationToken);
        await using var second = await unit.Factory.CreateDbContextAsync(cancellationToken);
        var queries = await Task.WhenAll(
            first.Database.SqlQueryRaw<int>("SELECT 1 AS \"Value\"").SingleAsync(first.Deadline.Token),
            second.Database.SqlQueryRaw<int>("SELECT 1 AS \"Value\"").SingleAsync(second.Deadline.Token));
        await unit.DisposeAsync();
        var closedRejected = false;
        try { await using var invalid = await unit.Factory.CreateDbContextAsync(cancellationToken); }
        catch (InvalidOperationException) { closedRejected = true; }
        return new(first.DataSourceId, first.ContextId.InstanceId, second.ContextId.InstanceId,
            plainRejected, queries.SequenceEqual([1, 1]), closedRejected);
    }
    public async ValueTask<IHeldFixtureStorageUnit> HoldAsync(CancellationToken cancellationToken)
    {
        var unit = await leases.BeginUnitAsync(cancellationToken);
        FixtureDbContext? context = null;
        try
        {
            context = await unit.Factory.CreateDbContextAsync(cancellationToken);
            await context.Database.OpenConnectionAsync(context.Deadline.Token);
            await unit.DisposeAsync();
            return new HeldFixtureStorageUnit(context);
        }
        catch
        {
            if (context is not null) await context.DisposeAsync();
            await unit.DisposeAsync();
            throw;
        }
    }
}
