using CShells.Features;
using CShells.Lifecycle;

namespace Orbyss.Foundation.Host.Shells;

/// <summary>Uses the CShells registry to activate configured shells during host startup.</summary>
internal sealed class EagerShellActivationHostedService(
    IShellRegistry registry,
    IRuntimeFeatureCatalog featureCatalog,
    IConfiguration configuration,
    ILogger<EagerShellActivationHostedService> logger) : IHostedService
{
    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (configuration.GetValue("Foundation:Boot:EagerShellActivation", defaultValue: true) is false)
            return;

        var failFast = configuration.GetValue("Foundation:Boot:FailOnShellActivationError", defaultValue: true);
        var shellNames = configuration.GetSection("CShells:Shells").GetChildren()
            .Select(child => child.Key)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray();
        if (shellNames.Length == 0)
            throw new InvalidOperationException("No shells are configured under 'CShells:Shells'.");

        var available = (await featureCatalog.GetSnapshotAsync(cancellationToken).ConfigureAwait(false))
            .FeatureMap;
        foreach (var name in shellNames)
        {
            var required = configuration.GetSection($"CShells:Shells:{name}:Features").GetChildren()
                .Where(feature => !string.Equals(feature.Value, "false", StringComparison.OrdinalIgnoreCase))
                .Select(feature => feature.Key)
                .Order(StringComparer.Ordinal)
                .ToArray();
            var missing = required.Where(feature => !available.ContainsKey(feature)).ToArray();
            if (missing.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Shell '{name}' requires feature identities that are absent from the runtime catalog: " +
                    string.Join(", ", missing) + ". Every packaged feature must declare an exact " +
                    "[ShellFeature] identity matching FoundationFeatureIdentity and shells.json.");
            }
        }

        foreach (var name in shellNames)
        {
            try
            {
                await registry.GetOrActivateAsync(name, cancellationToken).ConfigureAwait(false);
                logger.LogInformation("Activated shell {ShellName} during host startup.", name);
            }
            catch (Exception exception) when (!failFast && exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Shell {ShellName} failed eager activation; lazy activation remains available.", name);
            }
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

}
