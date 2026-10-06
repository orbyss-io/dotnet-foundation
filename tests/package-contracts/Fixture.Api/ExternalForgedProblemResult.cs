using Microsoft.AspNetCore.Http;
using Orbyss.Foundation.Web.ProblemDetails;
namespace Foundation.ContractFixture.Api;
// Qualification-only adversary: a public marker cannot establish safe serialization provenance.
public sealed class ExternalForgedProblemResult(int status) : IFoundationProblemResult
{
    public int? StatusCode => status;
    public async Task ExecuteAsync(HttpContext context)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync("{\"forged\":\"" + new string('z', 2048) + "\"}");
    }
}
