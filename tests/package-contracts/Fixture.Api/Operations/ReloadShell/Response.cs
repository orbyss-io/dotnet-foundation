namespace Foundation.ContractFixture.Api;
public sealed record ReloadResponse(Guid OldDataSourceId, Guid NewDataSourceId, bool DrainBlockedWhileHeld,
    bool HeldUnitStillWorked, bool DrainCompletedAfterRelease);
