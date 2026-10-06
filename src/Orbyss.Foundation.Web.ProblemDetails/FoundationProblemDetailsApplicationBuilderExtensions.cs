using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Orbyss.Foundation.Web.ProblemDetails.Core;

namespace Orbyss.Foundation.Web.ProblemDetails;

/// <summary>Composes native empty-status handling independently of optional exception dispatch.</summary>
public static class FoundationProblemDetailsApplicationBuilderExtensions
{
    /// <summary>Writes uncommitted empty failures through the same bounded DI representation.</summary>
    /// <remarks>Existing bodies and aborted requests retain their native outcome. No exception handler is activated.</remarks>
    public static IApplicationBuilder UseFoundationProblemStatusCodePages(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        _ = app.ApplicationServices.GetRequiredService<IProblemDetailsService>();
        return app.UseStatusCodePages(async statusContext =>
        {
            var context = statusContext.HttpContext;
            if (context.RequestAborted.IsCancellationRequested || context.Response.HasStarted)
                return;
            await FoundationProblemResults.Problem(new ProblemDefinition(context.Response.StatusCode,
                FoundationProblemResults.CodeForStatus(context.Response.StatusCode)))
                .ExecuteAsync(context).ConfigureAwait(false);
        });
    }
}
