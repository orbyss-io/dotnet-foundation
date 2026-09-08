using CShells.Features;
using CShells.Lifecycle;
using Microsoft.Extensions.DependencyInjection;

namespace Orbyss.Foundation.Tasks;

/// <summary>Composes Orbyss Foundation task lifecycle services into one CShells generation.</summary>
[ShellFeature(
    name: "FoundationTasks",
    DisplayName = "Orbyss Foundation Tasks",
    Description = "Owns startup, background, and recurring work for one shell generation.")]
public sealed class FoundationTasksFeature : IShellFeature
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ShellTaskManager>();
        services.AddSingleton<IShellTaskManager>(provider => provider.GetRequiredService<ShellTaskManager>());
        services.AddShellInitializer<StartShellTasksInitializer>(LifecyclePhase.Start, order: 0);
        services.AddShellTerminator<StopShellTasksTerminator>(LifecyclePhase.Start, order: 0);
    }
}
