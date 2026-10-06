using Foundation.ContractFixture.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Orbyss.Foundation.PostgreSql;
using Orbyss.Foundation.Execution;
using System.Diagnostics;
using System.Data.Common;
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
    public async Task<FixtureCancellationObservation> ObserveCancellationAsync(CancellationToken cancellationToken)
    {
        var watch = Stopwatch.StartNew();
        var canceled = false;
        bool outerExpired;
        bool followup;
        await using (var unit = await leases.BeginUnitAsync(cancellationToken))
        await using (var context = await unit.Factory.CreateDbContextAsync(unit.Deadline.Token))
        {
            try { await context.Database.ExecuteSqlRawAsync("SELECT pg_sleep(3)", unit.Deadline.Token); }
            catch (Exception error) when (error is OperationCanceledException or DbException) { canceled = true; }
            outerExpired = unit.Deadline.IsExpired;
            followup = await context.Database.SqlQueryRaw<int>("SELECT 1 AS \"Value\"").SingleAsync(unit.Deadline.Token) == 1;
            await context.Database.ExecuteSqlRawAsync("CREATE TABLE IF NOT EXISTS fixture_cancellation_receipts (id uuid PRIMARY KEY)", unit.Deadline.Token);
        }
        watch.Stop();
        var clock = new FixtureTimeProvider();
        using var outer = new TimeProviderDeadlineFactory(clock).Create(TimeSpan.FromSeconds(10), cancellationToken);
        var receipt = Guid.NewGuid();
        var fastCanceled = false;
        await using (var unit = await leases.BeginUnitAsync(cancellationToken, outer))
        await using (var context = await unit.Factory.CreateDbContextAsync(unit.Deadline.Token))
        {
            await context.Database.OpenConnectionAsync(unit.Deadline.Token);
            clock.AdvanceAfterTimer(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1.5));
            try { await context.Database.ExecuteSqlRawAsync("INSERT INTO fixture_cancellation_receipts (id) VALUES ({0})", [receipt], unit.Deadline.Token); }
            catch (OperationCanceledException) { fastCanceled = true; }
        }
        // A separately owned native read checks the actual database; cancellation alone is no rollback proof.
        await using var reconciliation = await leases.BeginUnitAsync(cancellationToken);
        await using var check = await reconciliation.Factory.CreateDbContextAsync(reconciliation.Deadline.Token);
        var committed = await check.Database.SqlQueryRaw<bool>("SELECT EXISTS (SELECT 1 FROM fixture_cancellation_receipts WHERE id = {0}) AS \"Value\"", receipt)
            .SingleAsync(reconciliation.Deadline.Token);
        return new(canceled, outerExpired, watch.Elapsed.TotalMilliseconds, followup,
            fastCanceled, committed, clock.ScheduledAdvanceOccurred);
    }
}
