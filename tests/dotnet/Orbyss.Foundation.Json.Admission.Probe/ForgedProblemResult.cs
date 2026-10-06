using Orbyss.Foundation.Web.ProblemDetails;

namespace Orbyss.Foundation.Json.Admission.Probe;

/// <summary>Uses the public compatibility marker without actual bounded Foundation admission.</summary>
public sealed class ForgedProblemResult(int statusCode) : IFoundationProblemResult
{
    /// <inheritdoc />
    public int? StatusCode => statusCode;
    /// <inheritdoc />
    public async Task ExecuteAsync(HttpContext context)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync("{\"forged\":\"FORGED_UNBOUNDED_BODY" + new string('x', 2048) + "\"}");
    }
}
