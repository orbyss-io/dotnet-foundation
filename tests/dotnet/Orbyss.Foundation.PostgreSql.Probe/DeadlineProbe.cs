using Orbyss.Foundation.Execution;

internal static class DeadlineProbe
{
    public static void Run()
    {
        var clock = new ManualTimeProvider();
        var factory = new TimeProviderDeadlineFactory(clock);
        using (var outer = factory.Create(TimeSpan.FromSeconds(10)))
        {
            clock.Advance(TimeSpan.FromSeconds(3));
            Require(outer.Remaining == TimeSpan.FromSeconds(7), "remaining time is not monotonic");
            clock.ShiftUtc(TimeSpan.FromDays(-100));
            Require(outer.Remaining == TimeSpan.FromSeconds(7), "wall-clock change reset the deadline");
            using var first = outer.BeginStage(TimeSpan.FromSeconds(20));
            Require(first.Remaining == TimeSpan.FromSeconds(7), "stage exceeded outer deadline");
            clock.Advance(TimeSpan.FromSeconds(4));
            using var retry = outer.BeginStage(TimeSpan.FromSeconds(20));
            Require(retry.Remaining == TimeSpan.FromSeconds(3), "retry reset outer deadline");
            clock.Advance(TimeSpan.FromSeconds(3));
            Require(outer.IsExpired && first.IsExpired && retry.IsExpired && outer.Token.IsCancellationRequested
                && first.Token.IsCancellationRequested && retry.Token.IsCancellationRequested, "expiry did not propagate");
        }
        Require(clock.ActiveTimers == 0, "deadline disposal retained timers");
        using (var caller = new CancellationTokenSource())
        using (var outer = factory.Create(TimeSpan.FromSeconds(10), caller.Token))
        {
            caller.Cancel();
            Require(outer.Token.IsCancellationRequested && !outer.IsExpired, "caller cancellation was mislabeled as expiry");
        }
        using (var outer = factory.Create(TimeSpan.FromSeconds(10)))
        using (var stageCaller = new CancellationTokenSource())
        using (var stage = outer.BeginStage(TimeSpan.FromSeconds(5), stageCaller.Token))
        {
            stageCaller.Cancel();
            Require(stage.Token.IsCancellationRequested && !outer.Token.IsCancellationRequested && !stage.IsExpired, "stage caller cancellation changed its parent");
        }
        var parent = factory.Create(TimeSpan.FromSeconds(10));
        using (var stage = parent.BeginStage(TimeSpan.FromSeconds(5)))
        {
            parent.Dispose();
            Require(stage.Token.IsCancellationRequested, "parent disposal did not cancel its stage");
        }
        Require(clock.ActiveTimers == 0, "nested cancellation retained timers");
        Console.WriteLine("Monotonic deadlines, caller/expiry distinction, stage/retry caps and timer disposal passed.");
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
