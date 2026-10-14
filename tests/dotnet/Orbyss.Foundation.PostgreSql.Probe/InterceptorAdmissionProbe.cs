using System.Data.Common;
using CShells.Lifecycle;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Orbyss.Foundation.PostgreSql;

internal static class InterceptorAdmissionProbe
{
    public static async Task RunAsync(IShellRegistry registry, IShell shell, IShell other, string connection, string shellName)
    {
        var evidence = shell.ServiceProvider.GetRequiredService<InterceptorEvidence>();
        var otherEvidence = other.ServiceProvider.GetRequiredService<InterceptorEvidence>();
        var leases = shell.ServiceProvider.GetRequiredService<IPostgreSqlUnitLeaseFactory<InterceptedContext>>();
        await using (var plain = shell.ServiceProvider.CreateAsyncScope())
        {
            try { plain.ServiceProvider.GetRequiredService<IDbContextFactory<InterceptedContext>>().CreateDbContext(); throw new Exception("Untracked interception admitted"); }
            catch (InvalidOperationException) { }
            Require(evidence.Instances.Count == 0, "contribution ran before lease admission");
        }
        var unit = await leases.BeginUnitAsync();
        var first = await unit.Factory.CreateDbContextAsync();
        var fault = evidence.Instances.Last();
        var second = await unit.Factory.CreateDbContextAsync();
        var normal = evidence.Instances.Last();
        Require(!ReferenceEquals(fault, normal) && fault.Scope == normal.Scope, "transient context/scoped unit isolation lost");
        Require(first.DataSourceId == second.DataSourceId, "interception changed provider identity");
        fault.Arm();
        await using (var transaction = await first.Database.BeginTransactionAsync(first.Deadline.Token))
        {
            first.Notes.Add(new ProbeNote { Name = "native-interceptor-durable" });
            await first.SaveChangesAsync(first.Deadline.Token);
            try { await transaction.CommitAsync(first.Deadline.Token); throw new Exception("Postcommit native fault did not propagate"); }
            catch (InvalidOperationException failure) when (ReferenceEquals(failure, fault.Failure)) { }
        }
        await using (var independent = new NpgsqlConnection(connection))
        {
            await independent.OpenAsync();
            await using var read = new NpgsqlCommand("SELECT count(*) FROM fixture_notes WHERE \"Name\" = @name", independent);
            read.Parameters.AddWithValue("name", "native-interceptor-durable");
            Require((long)(await read.ExecuteScalarAsync())! == 1, "native postcommit fault did not leave independently durable state");
        }
        foreach (var context in new[] { first, second })
        {
            await using var transaction = await context.Database.BeginTransactionAsync(context.Deadline.Token);
            await transaction.CommitAsync(context.Deadline.Token);
        }
        Require(fault.Faults == 1 && normal.Faults == 0, "one-shot contribution leaked to subsequent/other transactions");
        await using (var otherUnit = await other.ServiceProvider.GetRequiredService<IPostgreSqlUnitLeaseFactory<InterceptedContext>>().BeginUnitAsync())
        {
            await using var context = await otherUnit.Factory.CreateDbContextAsync();
            var contribution = otherEvidence.Instances.Last();
            Require(contribution.Scope != fault.Scope && context.DataSourceId != first.DataSourceId, "interception crossed shell scope/source ownership");
            await using var transaction = await context.Database.BeginTransactionAsync(context.Deadline.Token);
            await transaction.CommitAsync(context.Deadline.Token);
        }
        foreach (var mode in new[] { 1, 2, 3 })
        {
            evidence.RejectMode = mode;
            await using var rejected = await leases.BeginUnitAsync();
            try { await rejected.Factory.CreateDbContextAsync(); throw new Exception("Invalid contribution accepted"); }
            catch (InvalidOperationException) { }
        }
        evidence.RejectMode = 0;
        await unit.DisposeAsync();
        Require(fault.Disposals == 0 && normal.Disposals == 0, "unit closure disposed contributions still referenced by contexts");
        await first.DisposeAsync();
        Require(fault.Disposals == 0, "context disposed a borrowed DI contribution before unit scope release");
        var reload = await registry.ReloadAsync(shellName);
        Require(reload.Error is null && reload.Drain is not null, "interceptor generation reload failed");
        var drain = reload.Drain!.WaitAsync();
        await Task.Delay(50);
        Require(!drain.IsCompleted, "active intercepted context did not retain drain");
        await Task.WhenAll(second.DisposeAsync().AsTask(), second.DisposeAsync().AsTask());
        await drain.WaitAsync(TimeSpan.FromSeconds(5));
        Require(fault.Disposals == 1 && normal.Disposals == 1, "DI contributions were not disposed exactly once after final context release");
        Console.WriteLine("Native postcommit interceptor fault propagated after independently observed durable commit; one-shot/context/shell isolation, contribution rejection, DI disposal and generation drain passed.");
    }

    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}

public sealed class InterceptedContext(DbContextOptions<InterceptedContext> options) : FoundationPostgreSqlDbContext(options)
{
    public DbSet<ProbeNote> Notes => Set<ProbeNote>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<ProbeNote>().ToTable("fixture_notes");
        model.Entity<ProbeNote>().HasKey(note => note.Id);
        model.Entity<ProbeNote>().HasIndex(note => note.Name).IsUnique().HasDatabaseName("fixture_notes_name_unique");
    }
}

public sealed class InterceptorEvidence
{
    public List<PostCommitFaultInterceptor> Instances { get; } = [];
    public int RejectMode { get; set; }
}

public sealed class InterceptorUnitIdentity { public Guid Id { get; } = Guid.NewGuid(); }

public sealed class PostCommitFaultInterceptor(InterceptorUnitIdentity scope, InterceptorEvidence evidence) : DbTransactionInterceptor, IDisposable
{
    private int armed;
    public Guid Scope => scope.Id;
    public int Faults { get; private set; }
    public int Disposals { get; private set; }
    public InvalidOperationException Failure { get; } = new("test-only postcommit interceptor fault");
    public void Arm() => armed = 1;
    public void Register() => evidence.Instances.Add(this);
    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref armed, 0) != 0) { Faults++; throw Failure; }
        return Task.CompletedTask;
    }
    public void Dispose() => Disposals++;
}
