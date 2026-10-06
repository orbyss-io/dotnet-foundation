using Foundation.ContractFixture.Core;
using Microsoft.EntityFrameworkCore;
namespace Foundation.ContractFixture.PostgreSql;
internal sealed class HeldFixtureStorageUnit(FixtureDbContext context) : IHeldFixtureStorageUnit
{
    public Guid DataSourceId => context.DataSourceId;
    public async Task<bool> CheckAliveAsync(CancellationToken cancellationToken) =>
        await context.Database.SqlQueryRaw<int>("SELECT 1 AS \"Value\"").SingleAsync(context.Deadline.Token) == 1;
    public ValueTask DisposeAsync() => context.DisposeAsync();
}
