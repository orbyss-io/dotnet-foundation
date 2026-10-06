using CShells;
using CShells.Features;
using Foundation.ContractFixture.Core;
using Microsoft.Extensions.DependencyInjection;
using Orbyss.Foundation.Execution;
namespace Foundation.ContractFixture;
[ShellFeature("ContractFixture", DependsOn = [typeof(FoundationExecutionFeature)])]
public sealed class FixtureFeature(ShellSettings settings) : IShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton(new FixtureGeneration(settings.Id.ToString()));
        services.AddScoped<IFixtureAccountProjection, FixtureAccountProjection>();
    }
}
