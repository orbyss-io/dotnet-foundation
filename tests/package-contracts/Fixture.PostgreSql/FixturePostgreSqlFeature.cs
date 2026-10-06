using CShells;
using CShells.Features;
using Foundation.ContractFixture.Core;
using Microsoft.Extensions.DependencyInjection;
using Orbyss.Foundation.PostgreSql;
namespace Foundation.ContractFixture.PostgreSql;
[ShellFeature("ContractFixture.PostgreSql")]
public sealed class FixturePostgreSqlFeature(ShellSettings settings) : IShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddFoundationPostgreSql<FixtureDbContext>(settings, "fixture", options => new FixtureDbContext(options));
        services.AddSingleton<IFixtureStorageProbe, FixtureStorageProbe>();
    }
}
