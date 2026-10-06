namespace Foundation.ContractFixture.Core;
public interface IHeldFixtureStorageUnit : IAsyncDisposable
{
    Guid DataSourceId { get; }
    Task<bool> CheckAliveAsync(CancellationToken cancellationToken);
}
