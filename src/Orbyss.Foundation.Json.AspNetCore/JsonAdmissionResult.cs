using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Orbyss.Foundation.Json.AspNetCore;

/// <summary>Keeps response ownership guarded through the deferred native result execution.</summary>
internal sealed class JsonAdmissionResult(IResult result, IHttpResponseBodyFeature original, JsonAdmissionState admission, bool problem) : IResult
{
    /// <summary>Identifies an admitted failure without opening success writes.</summary>
    public bool IsProblem => problem;
    /// <inheritdoc />
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        try
        {
            if (problem) admission.Writing = true;
            await result.ExecuteAsync(httpContext).ConfigureAwait(false);
        }
        finally { httpContext.Features.Set(original); }
    }
}
