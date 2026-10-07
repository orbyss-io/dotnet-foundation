using Nuplane.Reconciliation;
using Nuplane.Reconciliation.Models;

namespace Orbyss.Foundation.Host.Feed;

/// <summary>Loads the initial package catalog before configured shells are activated.</summary>
internal sealed class InitialPackageReconciliationHostedService(IReconciliationService reconciliation) : IHostedService
{
    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var result = await reconciliation.TriggerAsync(
            new ReconciliationTrigger(TriggerType.Startup, "foundation-host-startup"),
            cancellationToken).ConfigureAwait(false);
        if (result.Skipped || result.IsDegraded || result.FailedPackages.Count > 0)
            throw new InvalidOperationException("Initial package reconciliation did not complete successfully.");
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
