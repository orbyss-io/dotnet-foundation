namespace Foundation.ContractFixture.PostgreSql;
internal sealed class FixtureTimer(FixtureTimeProvider owner) : ITimer
{
    private bool disposed;
    // Timers are owned/disposed normally; this adversarial clock intentionally postpones delivery.
    public bool Change(TimeSpan dueTime, TimeSpan period) => !disposed;
    public void Dispose() { disposed = true; owner.Timers.Remove(this); }
    public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
}
