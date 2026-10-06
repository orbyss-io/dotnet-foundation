namespace Foundation.ContractFixture.Core;
public sealed record FixtureStorageObservation(Guid DataSourceId, Guid FirstContextId, Guid SecondContextId,
    bool PlainFactoryRejected, bool IndependentQueriesSucceeded, bool ClosedUnitRejected);
