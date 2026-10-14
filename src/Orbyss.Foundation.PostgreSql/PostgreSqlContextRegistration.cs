using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Orbyss.Foundation.PostgreSql;

/// <summary>Retains one immutable native context registration per shell policy.</summary>
internal sealed record PostgreSqlContextRegistration<TContext>(
    ShellPostgreSqlDataSource DataSource,
    PostgreSqlPolicy Policy,
    Func<DbContextOptions<TContext>, TContext> Construct,
    Func<IServiceProvider, IReadOnlyList<IInterceptor>> Interceptors) where TContext : FoundationPostgreSqlDbContext;
