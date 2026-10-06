using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Orbyss.Foundation.Execution;
using Orbyss.Foundation.PostgreSql;

internal static class ConnectionStageProbe
{
    internal static async Task RunAsync(IPostgreSqlUnitLeaseFactory<ProbeContext> leases, CaptureLoggerProvider logs)
    {
        var failures = new List<string>();
        foreach (var stage in new[] { "open", "lock-setup" })
        foreach (var edge in new[] { "pre", "post", "caller" })
        {
            var clock = new ManualTimeProvider();
            using var outer = new TimeProviderDeadlineFactory(clock).Create(TimeSpan.FromSeconds(10));
            using var caller = new CancellationTokenSource();
            var observed = false;
            var denied = false;
            var closed = false;
            var sourceId = Guid.Empty;
            await using (var unit = await leases.BeginUnitAsync(caller.Token, outer))
            await using (var context = await unit.Factory.CreateDbContextAsync())
            {
                sourceId = context.DataSourceId;
                var native = context.Database.GetDbConnection();
                if (native is not NpgsqlConnection) throw new Exception("Connection admission replaced the concrete native connection.");
                var advance = stage == "open" ? TimeSpan.FromSeconds(2) : TimeSpan.FromMilliseconds(400);
                void CompleteNativeStage()
                {
                    if (observed) return;
                    observed = true;
                    if (edge == "caller") caller.Cancel();
                    else clock.Advance(advance, deliverTimers: false);
                }
                if (edge == "pre")
                {
                    clock.AdvanceAfterTimerCreation(stage == "open" ? TimeSpan.FromSeconds(1) : TimeSpan.FromMilliseconds(300), advance);
                    observed = true;
                }
                else if (stage == "open")
                    native.StateChange += (_, value) => { if (value.CurrentState == ConnectionState.Open) CompleteNativeStage(); };
                else logs.NativeCommandCompleted = CompleteNativeStage;
                try { await context.Database.OpenConnectionAsync(unit.Deadline.Token); }
                catch (OperationCanceledException) { denied = true; }
                finally { logs.NativeCommandCompleted = null; }
                closed = native.State == ConnectionState.Closed;
                Console.WriteLine($"Native connection admission {stage}/{edge}: rejected={denied}, observed={observed}, state={native.State}, outerExpired={outer.IsExpired}.");
                if (!denied || !closed || !observed || outer.IsExpired || !ReferenceEquals(native, context.Database.GetDbConnection()))
                    failures.Add(stage + "/" + edge);
            }
            if (clock.ActiveTimers != 1) failures.Add(stage + "/" + edge + "/timers");
            // Native opening or setup may have completed before rejection. A new
            // owned context must reuse the stable datasource and execute normally.
            await using var recovery = await leases.BeginUnitAsync();
            await using var healthy = await recovery.Factory.CreateDbContextAsync();
            var lockTimeout = await healthy.Database.SqlQueryRaw<string>("SELECT current_setting('lock_timeout') AS \"Value\"")
                .SingleAsync(recovery.Deadline.Token);
            if (healthy.DataSourceId != sourceId || lockTimeout != "100ms") failures.Add(stage + "/" + edge + "/reset");
        }
        if (failures.Count != 0) throw new Exception("Expired native connection admission reported success or retained open state: " + string.Join(", ", failures));
        Console.WriteLine("Native open/lock-setup pre-dispatch and post-completion monotonic expiry, caller cancellation, native close/reset and stable source identity passed.");
    }
}
