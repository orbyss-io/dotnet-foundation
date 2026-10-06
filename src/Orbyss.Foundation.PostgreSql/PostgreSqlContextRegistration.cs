using Microsoft.EntityFrameworkCore;

namespace Orbyss.Foundation.PostgreSql;

/// <summary>Retains one immutable native context registration per shell policy.</summary>
internal sealed record PostgreSqlContextRegistration<TContext>(
    ShellPostgreSqlDataSource DataSource,
    PostgreSqlPolicy Policy,
    Func<DbContextOptions<TContext>, TContext> Construct) where TContext : FoundationPostgreSqlDbContext;
