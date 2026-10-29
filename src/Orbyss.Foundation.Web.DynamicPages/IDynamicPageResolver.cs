namespace Orbyss.Foundation.Web.DynamicPages;

/// <summary>Owns current resource lookup, publication, tenancy, permissions and projection in the consumer's request scope.</summary>
public interface IDynamicPageResolver
{
    /// <summary>Reads current eligibility and returns a coherent authorized model; never reuse a deployment snapshot.</summary>
    Task<DynamicPageResolution> ResolveAsync(DynamicPageRequest request, CancellationToken cancellationToken);
    /// <summary>Rechecks current eligibility and the exact rendered version immediately before commitment; stale or unknown means rejection.</summary>
    Task<DynamicPageStatus> VerifyAsync(DynamicPageRequest request, string version, CancellationToken cancellationToken);
}
