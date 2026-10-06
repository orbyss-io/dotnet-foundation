namespace Foundation.ContractFixture.Core;
public sealed record FixtureCancellationObservation(bool StageCanceled, bool OuterExpired,
    double ElapsedMilliseconds, bool FollowupSucceeded, bool FastWriteCanceled, bool FastWriteCommitted,
    bool ScheduledAdvanceOccurred);
