namespace Foundation.ContractFixture.PostgreSql;
// Qualification-only monotonic clock supplied through the public optional operation-deadline seam.
internal sealed class FixtureTimeProvider : TimeProvider
{
    internal readonly List<FixtureTimer> Timers = [];
    internal long Timestamp;
    private TimeSpan? armAfterTimer;
    private TimeSpan advance;
    private bool armed;
    public bool ScheduledAdvanceOccurred { get; private set; }
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp()
    {
        if (armed)
        {
            armed = false;
            ScheduledAdvanceOccurred = true;
            // Deliberately delay timer delivery: elapsed admission must still reject the write.
            Timestamp += advance.Ticks;
        }
        return Timestamp;
    }
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch + TimeSpan.FromTicks(Timestamp);
    public void AdvanceAfterTimer(TimeSpan due, TimeSpan elapsed) { armAfterTimer = due; advance = elapsed; }
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new FixtureTimer(this);
        Timers.Add(timer);
        timer.Change(dueTime, period);
        if (armAfterTimer == dueTime) { armAfterTimer = null; armed = true; }
        return timer;
    }
}
