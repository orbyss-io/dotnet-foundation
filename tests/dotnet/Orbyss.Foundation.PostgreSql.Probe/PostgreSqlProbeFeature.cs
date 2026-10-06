using CShells;
using CShells.Features;
using CShells.Lifecycle;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Orbyss.Foundation.PostgreSql;

[ShellFeature("PostgreSqlProbe")]
public sealed class PostgreSqlProbeFeature(ShellSettings settings) : IShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped(typeof(ILogger<>), typeof(ScopedProbeLogger<>));
        services.AddFoundationPostgreSql<ProbeContext>(settings, "primary", options => new ProbeContext(options));
        var single = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("FOUNDATION_POSTGRES_TEST_CONNECTION")) { MaxPoolSize = 1 };
        services.AddFoundationPostgreSql<SinglePoolContext>("single", new PostgreSqlOptions
        {
            ConnectionString = single.ConnectionString, OperationTimeout = TimeSpan.FromSeconds(3),
            ConnectionTimeout = TimeSpan.FromMilliseconds(150), CommandTimeout = TimeSpan.FromSeconds(2), LockTimeout = TimeSpan.FromMilliseconds(100)
        }, options => new SinglePoolContext(options));
        services.AddFoundationPostgreSql<FailureContext>("failure", new PostgreSqlOptions { ConnectionString = single.ConnectionString },
            _ => throw new InvalidOperationException("seeded context construction failure"));
        services.AddFoundationPostgreSql<WrongOptionsContext>("wrong-options", new PostgreSqlOptions { ConnectionString = single.ConnectionString },
            _ => new WrongOptionsContext(new DbContextOptions<WrongOptionsContext>()));
        services.AddSingleton<InitializationEvidence>();
        services.AddShellInitializer<ProbeInitializer>();
    }
}

public sealed class ProbeContext(DbContextOptions<ProbeContext> options) : FoundationPostgreSqlDbContext(options)
{
    public DbSet<ProbeNote> Notes => Set<ProbeNote>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProbeNote>().ToTable("fixture_notes");
        modelBuilder.Entity<ProbeNote>().HasKey(note => note.Id);
        modelBuilder.Entity<ProbeNote>().HasIndex(note => note.Name).IsUnique().HasDatabaseName("fixture_notes_name_unique");
    }
}
public sealed class SinglePoolContext(DbContextOptions<SinglePoolContext> options) : FoundationPostgreSqlDbContext(options);
public sealed class FailureContext(DbContextOptions<FailureContext> options) : FoundationPostgreSqlDbContext(options);
public sealed class WrongOptionsContext(DbContextOptions<WrongOptionsContext> options) : FoundationPostgreSqlDbContext(options);
public sealed class ProbeNote
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}
public sealed class InitializationEvidence
{
    public Guid DataSourceId { get; set; }
}
public sealed class ProbeInitializer(IPostgreSqlUnitLeaseFactory<ProbeContext> leases, InitializationEvidence evidence) : IShellInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var unit = await leases.BeginUnitAsync(cancellationToken);
        await using var context = await unit.Factory.CreateDbContextAsync(unit.Deadline.Token);
        await context.Database.EnsureCreatedAsync(unit.Deadline.Token);
        evidence.DataSourceId = context.DataSourceId;
    }
}
