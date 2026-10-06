using System.Globalization;

internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly List<ManualTimer> timers = [];
    private long timestamp;
    private DateTimeOffset utc = DateTimeOffset.Parse("2026-10-06T00:00:00Z", CultureInfo.InvariantCulture);
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => timestamp;
    public override DateTimeOffset GetUtcNow() => utc;
    public int ActiveTimers => timers.Count;
    public void ShiftUtc(TimeSpan change) => utc += change;
    public void Advance(TimeSpan change)
    {
        timestamp += change.Ticks;
        utc += change;
        foreach (var timer in timers.ToArray()) timer.Fire(timestamp);
    }
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        timers.Add(timer);
        timer.Change(dueTime, period);
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
