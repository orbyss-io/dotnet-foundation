using Microsoft.EntityFrameworkCore;
using Orbyss.Foundation.PostgreSql;
namespace Foundation.ContractFixture.PostgreSql;
internal sealed class FixtureDbContext(DbContextOptions<FixtureDbContext> options) : FoundationPostgreSqlDbContext(options);
