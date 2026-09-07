using CShells.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Orbyss.Foundation.DomainEvents;

/// <summary>Composes in-process domain-event publication into one CShells generation.</summary>
[ShellFeature(
    name: "Orbyss.Foundation.DomainEvents",
    DisplayName = "Orbyss Foundation Domain Events",
    Description = "Provides awaited, scoped, non-durable publication of domain-owned events.")]
public sealed class FoundationDomainEventsFeature : IShellFeature
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services) => services.AddFoundationDomainEvents();
}
