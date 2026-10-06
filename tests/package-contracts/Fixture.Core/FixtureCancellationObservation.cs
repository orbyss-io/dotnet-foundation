namespace Foundation.ContractFixture.Core;
public sealed record FixtureCancellationObservation(bool StageCanceled, bool OuterExpired,
    double ElapsedMilliseconds, bool FollowupSucceeded, bool FastWriteCanceled, bool FastWriteCommitted,
    bool ScheduledAdvanceOccurred,
    bool OpenStageCanceled, bool OpenStageClosed, bool OpenStageRecovered,
    bool OpenStageOuterExpired, bool OpenStageAdvanceObserved,
    bool SetupStageCanceled, bool SetupStageClosed, bool SetupStageRecovered,
    bool SetupStageOuterExpired, bool SetupStageAdvanceObserved);
