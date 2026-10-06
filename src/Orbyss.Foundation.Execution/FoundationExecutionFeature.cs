using CShells.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Orbyss.Foundation.Execution;

/// <summary>Registers independently selected deadline mechanics for a shell.</summary>
[ShellFeature(name: "Orbyss.Foundation.Execution", DisplayName = "Orbyss Foundation Execution", Description = "Provides monotonic owned operation deadlines and capped stages.")]
public sealed class FoundationExecutionFeature : IShellFeature
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services) => services.AddFoundationExecution();
}
