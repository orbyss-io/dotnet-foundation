using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Orbyss.Foundation.PostgreSql;

/// <summary>Creates independent contexts only in an admitted tracked shell scope.</summary>
internal sealed class OwnedPostgreSqlDbContextFactory<TContext>(
    ContextLeaseState<TContext> state,
    PostgreSqlContextRegistration<TContext> registration,
    ILogger<OwnedPostgreSqlDbContextFactory<TContext>>? logger = null,
    ILoggerFactory? loggerFactory = null) : IDbContextFactory<TContext>
    where TContext : FoundationPostgreSqlDbContext
{
    /// <inheritdoc />
    public TContext CreateDbContext()
    {
        var lease = state.Acquire();
        TContext? context = null;
        try
        {
            var options = new DbContextOptionsBuilder<TContext>().UseNpgsql(registration.DataSource.Source,
                native => native.CommandTimeout(PostgreSqlPolicy.Seconds(registration.Policy.CommandTimeout)))
                .UseLoggerFactory(new PostgreSqlRedactingLoggerFactory(loggerFactory ?? NullLoggerFactory.Instance))
                .AddInterceptors(new PostgreSqlConnectionInterceptor(), new PostgreSqlCommandInterceptor(
                    logger ?? NullLogger<OwnedPostgreSqlDbContextFactory<TContext>>.Instance)).Options;
            context = registration.Construct(options);
            context.AttachLease(lease, options);
            return context;
        }
        catch
        {
            try { context?.Dispose(); }
            finally { lease.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
            throw;
        }
    }

    /// <inheritdoc />
    public Task<TContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(CreateDbContext());
    }
}
