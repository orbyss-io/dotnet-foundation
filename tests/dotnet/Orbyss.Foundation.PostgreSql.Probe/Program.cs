using System.Diagnostics;
using System.Text.Json;
using CShells.DependencyInjection;
using CShells.Lifecycle;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Orbyss.Foundation.Execution;
using Orbyss.Foundation.PostgreSql;

DeadlineProbe.Run();
if (args is ["--deadline-only"])
{
    Console.WriteLine("Deadline-only qualification passed; native PostgreSQL was not exercised.");
    return;
}
if (args.Length != 0) throw new ArgumentException("Only --deadline-only is accepted; full native qualification uses its explicit fixture environment.");
var capturedLogs = new CaptureLoggerProvider();
using var nativeGlobalFactory = LoggerFactory.Create(builder => builder.AddProvider(capturedLogs).SetMinimumLevel(LogLevel.Trace));
NpgsqlLoggingConfiguration.InitializeLogging(nativeGlobalFactory, parameterLoggingEnabled: true);
var connection = Environment.GetEnvironmentVariable("FOUNDATION_POSTGRES_TEST_CONNECTION")
    ?? throw new InvalidOperationException("The disposable PostgreSQL fixture connection is required.");
try
{
    new ServiceCollection().AddFoundationPostgreSql<ProbeContext>("invalid", new PostgreSqlOptions { ConnectionString = connection, CommandTimeout = TimeSpan.Zero }, options => new ProbeContext(options));
    throw new Exception("invalid native budget was admitted");
}
catch (ArgumentOutOfRangeException) { }
var duplicatePolicy = new ServiceCollection();
duplicatePolicy.AddFoundationPostgreSql<ProbeContext>("same", new PostgreSqlOptions { ConnectionString = connection }, options => new ProbeContext(options));
try
{
    duplicatePolicy.AddFoundationPostgreSql<SinglePoolContext>("same", new PostgreSqlOptions { ConnectionString = connection, OperationTimeout = TimeSpan.FromSeconds(20) }, options => new SinglePoolContext(options));
    throw new Exception("inconsistent named datasource policy was admitted");
}
catch (ArgumentException) { }
var values = new Dictionary<string, string?>();
foreach (var name in new[] { "a", "b" })
{
    values[$"CShells:Shells:{name}:Features:PostgreSqlProbe"] = "true";
    var policy = $"CShells:Shells:{name}:Configuration:Foundation:PostgreSql:Policies:primary";
    values[$"{policy}:ConnectionString"] = connection;
    values[$"{policy}:OperationTimeout"] = "00:00:03";
    values[$"{policy}:ConnectionTimeout"] = "00:00:01";
    values[$"{policy}:CommandTimeout"] = "00:00:00.300";
    values[$"{policy}:LockTimeout"] = "00:00:00.100";
}
var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
var services = new ServiceCollection();
services.AddLogging(builder => builder.AddProvider(capturedLogs).SetMinimumLevel(LogLevel.Trace));
services.AddSingleton<IConfiguration>(configuration);
services.AddCShells(shells => shells.WithConfigurationProvider(configuration));
await using var provider = services.BuildServiceProvider();
var registry = provider.GetRequiredService<IShellRegistry>();
var shell = await registry.GetOrActivateAsync("a");
var otherShell = await registry.GetOrActivateAsync("b");
var leases = shell.ServiceProvider.GetRequiredService<IPostgreSqlUnitLeaseFactory<ProbeContext>>();
Require(shell.ServiceProvider.GetRequiredService<InitializationEvidence>().DataSourceId != otherShell.ServiceProvider.GetRequiredService<InitializationEvidence>().DataSourceId,
    "two shells shared a datasource");
await using (var rawScope = shell.ServiceProvider.CreateAsyncScope())
{
    try { rawScope.ServiceProvider.GetRequiredService<IDbContextFactory<ProbeContext>>().CreateDbContext(); throw new Exception("plain DI scope admitted a context"); }
    catch (InvalidOperationException) { }
}

try { shell.ServiceProvider.GetRequiredService<IDbContextFactory<ProbeContext>>().CreateDbContext(); throw new Exception("root admitted a context"); }
catch (InvalidOperationException) { }

// Cancellation callbacks may close the owner reentrantly while its final scope is disposing.
{
    var unit = await leases.BeginUnitAsync();
    Task? reentrantClose = null;
    using var callback = unit.Deadline.Token.Register(() => reentrantClose = unit.DisposeAsync().AsTask());
    await unit.DisposeAsync();
    Require(reentrantClose is not null, "deadline disposal did not invoke its actual cancellation callback");
    await reentrantClose!;
}

// Actual tracked-scope disposal must share pending/faulted completion between owner callers.
foreach (var failure in new Exception?[] { null, new InvalidOperationException("seeded owned scope disposal failure") })
{
    var control = ScopeDisposalControl.Arm(failure);
    var unit = await leases.BeginUnitAsync();
    var firstClose = unit.DisposeAsync().AsTask();
    await control.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
    var secondClose = unit.DisposeAsync().AsTask();
    var sharedCompletion = !secondClose.IsCompleted;
    control.Release.TrySetResult();
    var failures = 0;
    foreach (var closing in new[] { firstClose, secondClose, unit.DisposeAsync().AsTask() })
    {
        try { await closing; }
        catch (InvalidOperationException exception) when (ReferenceEquals(exception, failure)) { failures++; }
    }
    Require(sharedCompletion && failures == (failure is null ? 0 : 3), "owner disposal lost pending or faulted tracked-scope completion");
}

await using (var unit = await leases.BeginUnitAsync())
{
    await using var first = await unit.Factory.CreateDbContextAsync();
    await using var second = await unit.Factory.CreateDbContextAsync();
    Require(!ReferenceEquals(first, second) && first.ContextId != second.ContextId && first.DataSourceId == second.DataSourceId
        && first.DataSourceId == shell.ServiceProvider.GetRequiredService<InitializationEvidence>().DataSourceId, "contexts were pooled/shared or changed source identity");
    first.Notes.Add(new ProbeNote { Name = "private-fixture-value" });
    await first.SaveChangesAsync(first.Deadline.Token);
    Require(await second.Notes.CountAsync(second.Deadline.Token) == 1, "independent context did not observe committed state");
    second.Notes.Add(new ProbeNote { Name = "private-fixture-value" });
    try { await second.SaveChangesAsync(second.Deadline.Token); throw new Exception("native unique constraint did not fire"); }
    catch (DbUpdateException exception)
    {
        Require(PostgreSqlFailureInfo.TryRead(exception, out var failure) && failure!.SqlState == "23505"
            && failure.ConstraintName == "fixture_notes_name_unique", "native constraint identity was not extracted");
        var json = JsonSerializer.Serialize(failure);
        Require(!json.Contains("private-fixture-value", StringComparison.Ordinal) && !json.Contains("INSERT", StringComparison.Ordinal), "failure projection exposed SQL/input");
    }
}

await using (var unit = await leases.BeginUnitAsync())
await using (var context = await unit.Factory.CreateDbContextAsync())
{
    try
    {
        await context.Database.ExecuteSqlRawAsync("DO $$ BEGIN RAISE EXCEPTION 'private-native-message-sentinel'; END $$", context.Deadline.Token);
        throw new Exception("native exception fixture did not fail");
    }
    catch (PostgresException) { }
}
Require(!capturedLogs.Entries.Any(entry => entry.Contains("private-fixture-value", StringComparison.Ordinal)
    || entry.Contains("INSERT INTO", StringComparison.Ordinal)
    || entry.Contains("private-native-message-sentinel", StringComparison.Ordinal)
    || entry.Contains("foundation-contracts-fixture-only", StringComparison.Ordinal)), "native PostgreSQL logging exposed private input, SQL, credentials or exception text");
Require(capturedLogs.Entries.Any(entry => entry.Contains("23505", StringComparison.Ordinal)), "native PostgreSQL logging lost safe SQLSTATE diagnostics");
Require(capturedLogs.Entries.Any(entry => entry.StartsWith("Microsoft.EntityFrameworkCore.Update|Error|", StringComparison.Ordinal)),
    "native PostgreSQL logging lost category or severity");

// Expiry after the first stage check but before native dispatch must reject a fast mutation.
// Cancel() alone cannot cancel a command that has not started executing yet.
foreach (var mode in new[] { "nonquery", "reader", "scalar" })
foreach (var deliverTimers in new[] { true, false })
{
    var clock = new ManualTimeProvider();
    using (var outer = new TimeProviderDeadlineFactory(clock).Create(TimeSpan.FromSeconds(10)))
    {
        await using (var unit = await leases.BeginUnitAsync(operationDeadline: outer))
        await using (var context = await unit.Factory.CreateDbContextAsync())
        {
            await context.Database.OpenConnectionAsync(unit.Deadline.Token);
            clock.AdvanceOnTimestampAfterTimer(TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(400), deliverTimers);
            var canceled = false;
            try
            {
                if (mode == "reader")
                {
                    await using var rows = context.Database.SqlQueryRaw<int>("INSERT INTO fixture_notes (\"Name\") VALUES ('dispatch-race-reader') RETURNING \"Id\" AS \"Value\"")
                        .AsAsyncEnumerable().GetAsyncEnumerator(unit.Deadline.Token);
                    await rows.MoveNextAsync();
                }
                else if (mode == "scalar")
                    await ExecuteScalarAsync(context, "INSERT INTO fixture_notes (\"Name\") VALUES ('dispatch-race-scalar') RETURNING \"Id\"", unit.Deadline.Token);
                else
                    await context.Database.ExecuteSqlRawAsync("INSERT INTO fixture_notes (\"Name\") VALUES ('dispatch-race-nonquery')", unit.Deadline.Token);
            }
            catch (OperationCanceledException) { canceled = true; }
            Require(clock.ScheduledAdvanceOccurred && canceled, "expired pre-dispatch command reported a successful native mutation");
            Require(!unit.Deadline.IsExpired && !outer.IsExpired && clock.ActiveTimers == 2,
                "pre-dispatch failure reset its outer deadline or leaked its command timer");
        }
        await using (var reconciliation = await leases.BeginUnitAsync())
        await using (var context = await reconciliation.Factory.CreateDbContextAsync())
            Require(!await context.Notes.AnyAsync(note => note.Name.StartsWith("dispatch-race-"), reconciliation.Deadline.Token),
                "an already expired command reached PostgreSQL before native dispatch");
    }
    Require(clock.ActiveTimers == 0, "pre-dispatch command failure leaked deadline ownership");
}

// Sync native commands do not accept cancellation tokens. Expiry must prevent false
// success, but this cancellation outcome cannot establish rollback: reconcile separately.
foreach (var mode in new[] { "nonquery", "reader", "scalar" })
foreach (var deliverTimers in new[] { true, false })
{
    var name = $"sync-dispatch-{mode}";
    var clock = new ManualTimeProvider();
    using (var outer = new TimeProviderDeadlineFactory(clock).Create(TimeSpan.FromSeconds(10)))
    {
        await using (var unit = await leases.BeginUnitAsync(operationDeadline: outer))
        await using (var context = await unit.Factory.CreateDbContextAsync())
        {
            await context.Database.OpenConnectionAsync(unit.Deadline.Token);
            clock.AdvanceOnTimestampAfterTimer(TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(400), deliverTimers);
            var canceled = false;
            try
            {
                if (mode == "reader")
                    context.Database.SqlQueryRaw<int>("INSERT INTO fixture_notes (\"Name\") VALUES ('sync-dispatch-reader') RETURNING \"Id\" AS \"Value\"")
                        .AsEnumerable().Single();
                else if (mode == "scalar")
                    ExecuteScalar(context, "INSERT INTO fixture_notes (\"Name\") VALUES ('sync-dispatch-scalar') RETURNING \"Id\"");
                else
                    context.Database.ExecuteSqlRaw("INSERT INTO fixture_notes (\"Name\") VALUES ('sync-dispatch-nonquery')");
            }
            catch (OperationCanceledException) { canceled = true; }
            Require(clock.ScheduledAdvanceOccurred && canceled && !unit.Deadline.IsExpired && clock.ActiveTimers == 2,
                "synchronous command reported success after expiry or leaked its stage");
        }
        await using (var reconciliation = await leases.BeginUnitAsync())
        await using (var context = await reconciliation.Factory.CreateDbContextAsync())
        {
            Require(await context.Notes.AnyAsync(note => note.Name == name, reconciliation.Deadline.Token),
                "synchronous cancellation fixture did not establish its uncertain committed outcome");
            await context.Notes.Where(note => note.Name == name).ExecuteDeleteAsync(reconciliation.Deadline.Token);
        }
    }
    Require(clock.ActiveTimers == 0, "synchronous command failure leaked deadline ownership");
}
await using (var unit = await leases.BeginUnitAsync())
await using (var context = await unit.Factory.CreateDbContextAsync())
{
    Require((int?)await ExecuteScalarAsync(context, "SELECT 1", unit.Deadline.Token) == 1
        && (int?)ExecuteScalar(context, "SELECT 2") == 2, "native scalar results changed after owned stage dispatch");
}
Console.WriteLine("Native reader/nonquery/scalar dispatch expiry, synchronous commit uncertainty and separate reconciliation passed.");

// Native close drains unread batches and can fail before EF's DataReaderDisposing callback.
// The native reader must retain its stage through close, then release it even on this fault.
foreach (var asynchronous in new[] { true, false })
{
    var clock = new ManualTimeProvider();
    using (var outer = new TimeProviderDeadlineFactory(clock).Create(TimeSpan.FromSeconds(10)))
    await using (var unit = await leases.BeginUnitAsync(operationDeadline: outer))
    await using (var context = await unit.Factory.CreateDbContextAsync())
    {
        var query = context.Database.SqlQueryRaw<int>("SELECT 1 AS \"Value\"; SELECT 1 / 0 AS \"Value\"");
        var closeFailed = false;
        if (asynchronous)
        {
            var rows = query.AsAsyncEnumerable().GetAsyncEnumerator(unit.Deadline.Token);
            try { Require(await rows.MoveNextAsync() && rows.Current == 1, "early-close fixture did not expose its first native result"); }
            finally
            {
                try { await rows.DisposeAsync(); }
                catch (PostgresException exception) when (exception.SqlState == "22012") { closeFailed = true; }
            }
            await rows.DisposeAsync();
        }
        else
        {
            var rows = query.AsEnumerable().GetEnumerator();
            try { Require(rows.MoveNext() && rows.Current == 1, "early-close fixture did not expose its first native result"); }
            finally
            {
                try { rows.Dispose(); }
                catch (PostgresException exception) when (exception.SqlState == "22012") { closeFailed = true; }
            }
            rows.Dispose();
        }
        Require(closeFailed && clock.ActiveTimers == 2, "native early-close fault leaked its owned command stage");
        Require(await context.Database.SqlQueryRaw<int>("SELECT 2 AS \"Value\"").SingleAsync(unit.Deadline.Token) == 2
            && clock.ActiveTimers == 2, "native reader reuse retained its previous close handler or stage");
    }
    Require(clock.ActiveTimers == 0, "native early-close fault retained a stage after context/lease disposal");
}
Console.WriteLine("Native sync/async early reader-close faults released retained command stages.");

// Native command expiry and caller cancellation remain separate owned outcomes.
await using (var unit = await leases.BeginUnitAsync())
await using (var context = await unit.Factory.CreateDbContextAsync())
{
    var watch = Stopwatch.StartNew();
    try { await context.Database.ExecuteSqlRawAsync("SELECT pg_sleep(3)", context.Deadline.Token); throw new Exception("command cap did not expire"); }
    catch (Exception exception) when (exception is OperationCanceledException or System.Data.Common.DbException)
    {
        Require(watch.Elapsed < TimeSpan.FromSeconds(2) && !unit.Deadline.IsExpired, "command stage reset/consumed the outer deadline");
    }
}
using (var caller = new CancellationTokenSource(TimeSpan.FromMilliseconds(150)))
await using (var unit = await leases.BeginUnitAsync(caller.Token))
await using (var context = await unit.Factory.CreateDbContextAsync())
{
    try { await context.Database.ExecuteSqlRawAsync("SELECT pg_sleep(3)", unit.Deadline.Token); throw new Exception("caller cancellation was ignored"); }
    catch (OperationCanceledException) { Require(!unit.Deadline.IsExpired, "caller cancellation became operation expiry"); }
}

// Row lock wait uses native PostgreSQL settings and does not classify a domain outcome.
await using (var unit = await leases.BeginUnitAsync())
await using (var holder = await unit.Factory.CreateDbContextAsync())
await using (var waiter = await unit.Factory.CreateDbContextAsync())
await using (var transaction = await holder.Database.BeginTransactionAsync(unit.Deadline.Token))
{
    await holder.Database.ExecuteSqlRawAsync("UPDATE fixture_notes SET \"Name\" = \"Name\" WHERE \"Id\" = 1", unit.Deadline.Token);
    try { await waiter.Database.ExecuteSqlRawAsync("UPDATE fixture_notes SET \"Name\" = \"Name\" WHERE \"Id\" = 1", unit.Deadline.Token); throw new Exception("native lock cap did not expire"); }
    catch (Exception exception) when (exception is System.Data.Common.DbException)
    {
        Require(PostgreSqlFailureInfo.TryRead(exception, out var failure) && failure!.SqlState == "55P03", "lock failure identity changed");
    }
    await transaction.RollbackAsync(unit.Deadline.Token);
}

// A fixed one-connection pool proves stable source identity and reset-on-return.
var single = shell.ServiceProvider.GetRequiredService<IPostgreSqlUnitLeaseFactory<SinglePoolContext>>();
var singleUnit = await single.BeginUnitAsync();
var occupied = await singleUnit.Factory.CreateDbContextAsync();
await occupied.Database.OpenConnectionAsync(occupied.Deadline.Token);
var pid = await occupied.Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync(occupied.Deadline.Token);
await occupied.Database.ExecuteSqlRawAsync("SET application_name = 'fixture-state'", occupied.Deadline.Token);
await using (var waiting = await singleUnit.Factory.CreateDbContextAsync())
{
    var watch = Stopwatch.StartNew();
    try { await waiting.Database.OpenConnectionAsync(waiting.Deadline.Token); throw new Exception("pool/open stage did not expire"); }
    catch (OperationCanceledException) { Require(watch.Elapsed < TimeSpan.FromSeconds(1), "connection wait exceeded cap"); }
}
await occupied.DisposeAsync();
await singleUnit.DisposeAsync();
await using (var nextUnit = await single.BeginUnitAsync())
await using (var next = await nextUnit.Factory.CreateDbContextAsync())
{
    await next.Database.OpenConnectionAsync(next.Deadline.Token);
    var nextPid = await next.Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync(next.Deadline.Token);
    Require(pid == nextPid, "operation changed pool/source identity");
    Require(await next.Database.SqlQueryRaw<string>("SELECT current_setting('application_name') AS \"Value\"").SingleAsync(next.Deadline.Token) == "foundation-qualifier", "pool session state was not reset");
}

await using (var readerUnit = await single.BeginUnitAsync())
await using (var readerContext = await readerUnit.Factory.CreateDbContextAsync())
{
    var count = 0;
    var watch = Stopwatch.StartNew();
    try
    {
        await foreach (var row in readerContext.Database.SqlQueryRaw<int>(
            "SELECT g AS \"Value\", pg_sleep(0.002) AS delay FROM generate_series(1, 10000) AS g").AsAsyncEnumerable().WithCancellation(readerUnit.Deadline.Token)) count++;
        throw new Exception("native streaming reader bypassed its retained command deadline");
    }
    catch (Exception exception) when (exception is OperationCanceledException or System.Data.Common.DbException)
    {
        Require(count > 0 && watch.Elapsed < TimeSpan.FromSeconds(3) && !readerUnit.Deadline.IsExpired,
            "streaming command stage failed to retain its cap after native ReaderExecuted");
    }
}

await using (var failureUnit = await shell.ServiceProvider.GetRequiredService<IPostgreSqlUnitLeaseFactory<FailureContext>>().BeginUnitAsync())
{
    try { await failureUnit.Factory.CreateDbContextAsync(); throw new Exception("seeded constructor failure did not fire"); }
    catch (InvalidOperationException) { }
}
await using (var wrongOptionsUnit = await shell.ServiceProvider.GetRequiredService<IPostgreSqlUnitLeaseFactory<WrongOptionsContext>>().BeginUnitAsync())
{
    try { await wrongOptionsUnit.Factory.CreateDbContextAsync(); throw new Exception("context constructor replaced admitted provider options"); }
    catch (InvalidOperationException) { }
}

// Closing an owner early must retain its actual tracked scope and data source through reload drain.
var activeUnit = await leases.BeginUnitAsync();
var activeContext = await activeUnit.Factory.CreateDbContextAsync();
await activeContext.Database.OpenConnectionAsync(activeContext.Deadline.Token);
await activeUnit.DisposeAsync();
try { await activeUnit.Factory.CreateDbContextAsync(); throw new Exception("closed lease admitted another unit"); }
catch (InvalidOperationException) { }
var reload = await registry.ReloadAsync("a");
Require(reload.Error is null && reload.NewShell is not null && reload.Drain is not null, "actual shell reload failed");
var drain = reload.Drain!.WaitAsync();
await Task.Delay(100);
Require(!drain.IsCompleted, "drain disposed a datasource with an active context");
Require(await activeContext.Database.SqlQueryRaw<int>("SELECT 1 AS \"Value\"").SingleAsync(activeContext.Deadline.Token) == 1,
    "active unit lost its datasource during drain");
await Task.WhenAll(activeContext.DisposeAsync().AsTask(), activeContext.DisposeAsync().AsTask());
await activeContext.DisposeAsync();
await drain.WaitAsync(TimeSpan.FromSeconds(5));
Require(shell.State == ShellLifecycleState.Disposed, "context release did not complete actual shell drain");
await using (var replacement = await reload.NewShell!.ServiceProvider.GetRequiredService<IPostgreSqlUnitLeaseFactory<ProbeContext>>().BeginUnitAsync())
{
    Require(replacement.DataSourceId != activeUnit.DataSourceId, "new shell generation reused the disposed source");
}
foreach (var active in registry.GetActiveShells()) await (await registry.DrainAsync(active)).WaitAsync();
Console.WriteLine("Actual PostgreSQL shell initialization, independent factory units, native waits/cancellation, pool reset and active-unit drain passed.");

static void Require(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

// Exercise EF's actual scalar dispatch path, which ordinary LINQ selects implement as readers.
static Task<object?> ExecuteScalarAsync(ProbeContext context, string sql, CancellationToken token)
{
    var command = context.GetService<IRelationalCommandBuilderFactory>().Create().Append(sql).Build();
    return command.ExecuteScalarAsync(ScalarParameters(context), token);
}

static object? ExecuteScalar(ProbeContext context, string sql)
{
    var command = context.GetService<IRelationalCommandBuilderFactory>().Create().Append(sql).Build();
    return command.ExecuteScalar(ScalarParameters(context));
}

static RelationalCommandParameterObject ScalarParameters(ProbeContext context) => new(
    context.GetService<IRelationalConnection>(), null, null, context, context.GetService<IRelationalCommandDiagnosticsLogger>());
