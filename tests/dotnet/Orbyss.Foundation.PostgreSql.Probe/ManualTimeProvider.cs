using System.Globalization;

internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly List<ManualTimer> timers = [];
    private long timestamp;
    private TimeSpan? advanceAfterTimer;
    private TimeSpan scheduledAdvance;
    private bool advanceOnTimestamp;
    private bool deliverScheduledTimers;
    private TimeSpan? advanceAtCreation;
    private TimeSpan creationAdvance;
    private DateTimeOffset utc = DateTimeOffset.Parse("2026-10-06T00:00:00Z", CultureInfo.InvariantCulture);
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp()
    {
        if (advanceOnTimestamp)
        {
            advanceOnTimestamp = false;
            ScheduledAdvanceOccurred = true;
            Advance(scheduledAdvance, deliverScheduledTimers);
        }
        return timestamp;
    }
    public override DateTimeOffset GetUtcNow() => utc;
    public int ActiveTimers => timers.Count;
    public bool ScheduledAdvanceOccurred { get; private set; }
    public void AdvanceOnTimestampAfterTimer(TimeSpan dueTime, TimeSpan advance, bool deliverTimers = true)
    {
        advanceAfterTimer = dueTime;
        scheduledAdvance = advance;
        deliverScheduledTimers = deliverTimers;
        ScheduledAdvanceOccurred = false;
    }
    public void ShiftUtc(TimeSpan change) => utc += change;
    public void AdvanceAfterTimerCreation(TimeSpan dueTime, TimeSpan advance)
    {
        advanceAtCreation = dueTime;
        creationAdvance = advance;
    }
    public void Advance(TimeSpan change, bool deliverTimers = true)
    {
        timestamp += change.Ticks;
        utc += change;
        if (deliverTimers) foreach (var timer in timers.ToArray()) timer.Fire(timestamp);
    }
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        timers.Add(timer);
        timer.Change(dueTime, period);
        if (advanceAfterTimer == dueTime)
        {
            advanceAfterTimer = null;
            advanceOnTimestamp = true;
        }
        if (advanceAtCreation == dueTime)
        {
            advanceAtCreation = null;
            Advance(creationAdvance, deliverTimers: false);
        }
        return timer;
    }
    private sealed class ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        private long due;
        private long period;
        private bool disposed;
        public bool Change(TimeSpan dueTime, TimeSpan newPeriod)
        {
            if (disposed) return false;
            due = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : owner.timestamp + dueTime.Ticks;
            period = newPeriod == Timeout.InfiniteTimeSpan ? 0 : newPeriod.Ticks;
            return true;
        }
        public void Fire(long now)
        {
            if (disposed || due > now) return;
            due = period <= 0 ? long.MaxValue : now + period;
            callback(state);
        }
        public void Dispose()
        {
            disposed = true;
            owner.timers.Remove(this);
        }
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
