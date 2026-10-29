using Orbyss.Foundation.Web.DynamicPages;

internal sealed class HostedStallResolver(HostedStallState state) : IDynamicPageResolver
{
    public Task<DynamicPageResolution> ResolveAsync(DynamicPageRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(state.Published && request.RouteValues["slug"] == "market"
            ? new DynamicPageResolution(DynamicPageStatus.Admitted, "1", new DynamicPageDocument
                { Title = "Packaged live stall", Heading = "Packaged live stall", Description = "Current publication", Locale = "en" })
            : new DynamicPageResolution(DynamicPageStatus.NotFound));
    }
    public Task<DynamicPageStatus> VerifyAsync(DynamicPageRequest request, string version, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(state.Published && version == "1" ? DynamicPageStatus.Admitted : DynamicPageStatus.NotFound);
    }
}
