namespace Foundation.ContractFixture.Core;
public interface IFixtureStorageProbe
{
    Task<FixtureStorageObservation> ObserveAsync(CancellationToken cancellationToken);
    ValueTask<IHeldFixtureStorageUnit> HoldAsync(CancellationToken cancellationToken);
    Task<FixtureCancellationObservation> ObserveCancellationAsync(CancellationToken cancellationToken);
}
