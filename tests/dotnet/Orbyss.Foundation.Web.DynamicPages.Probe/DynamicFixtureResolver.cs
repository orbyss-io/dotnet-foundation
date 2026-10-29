using System.Text.Json;
using Orbyss.Foundation.Web.DynamicPages;

internal sealed class DynamicFixtureResolver(DynamicFixtureState state) : IDynamicPageResolver
{
    private readonly Guid instance = Guid.NewGuid();
    public async Task<DynamicPageResolution> ResolveAsync(DynamicPageRequest request, CancellationToken cancellationToken)
    {
        state.Reads++;
        state.ResolverInstances.Add(instance);
        var slug = request.RouteValues["slug"];
        if (slug == "timeout") await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
        if (slug == "cancel")
        {
            state.CancellationEntered.TrySetResult();
            try { await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken); }
            catch (OperationCanceledException) { state.CancellationObserved.TrySetResult(); throw; }
        }
        if (slug == "failure") throw new InvalidOperationException("private-provider-exception");
        if (slug == "forbidden") return new(DynamicPageStatus.Forbidden);
        if (slug == "unknown") return new((DynamicPageStatus)0);
        if (!state.Published || slug == "hidden") return new(DynamicPageStatus.NotFound);
        var data = JsonSerializer.SerializeToElement(new { slug, text = "</script><script>window.injected=true</script>", scope = request.ApplicationPathBase });
        if (slug == "big") data = JsonSerializer.SerializeToElement(new { value = new string('x', 100000) });
        return new(DynamicPageStatus.Admitted, state.Version.ToString(), new DynamicPageDocument
        {
            Title = "Stall <script>window.injected=true</script>", Description = "Fresh \"metadata\" & publication",
            Heading = "Fresh stall " + slug, Text = "Consumer approved public projection", Locale = "nl",
            CanonicalPath = slug == "bad-url" ? "javascript:alert(1)" : "/stalls/" + slug, Bootstrap = data
        });
    }
    public async Task<DynamicPageStatus> VerifyAsync(DynamicPageRequest request, string version, CancellationToken cancellationToken)
    {
        state.Verifications++;
        if (state.BlockVerification)
        {
            state.VerificationEntered.TrySetResult();
            await state.ContinueVerification.Task.WaitAsync(cancellationToken);
        }
        if (state.ThrowVerification) throw new InvalidOperationException("private-verification-exception");
        if (state.RejectVerification) return DynamicPageStatus.Unavailable;
        if (state.ChangeVersionOnVerify) state.Version++;
        return state.Published && version == state.Version.ToString() ? DynamicPageStatus.Admitted : DynamicPageStatus.NotFound;
    }
}
