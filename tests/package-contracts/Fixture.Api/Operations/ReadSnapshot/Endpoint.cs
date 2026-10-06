using System.Reflection;
using System.Security.Claims;
using CShells;
using CShells.Features;
using Foundation.ContractFixture.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orbyss.Foundation.Authentication;
using Orbyss.Foundation.Authentication.Core;
using Orbyss.Foundation.Collections.Core;
using Orbyss.Foundation.Execution.Core;
using Orbyss.Foundation.Json;
using Orbyss.Foundation.Json.AspNetCore;
using Orbyss.Foundation.Web.ProblemDetails;
using Orbyss.Foundation.Web.ProblemDetails.Core;
namespace Foundation.ContractFixture.Api;
internal sealed class SnapshotEndpoint(ShellSettings settings, IValidatedAccountIdentityReader reader,
    IFixtureAccountProjection projection, IJsonResponseFactory<SnapshotResponse> responses, IExecutionDeadlineFactory deadlines)
{
    public static void Map(IEndpointRouteBuilder endpoints) => endpoints.MapGet("/snapshot/{mode?}",
        (SnapshotEndpoint endpoint, string? mode) => endpoint.Execute(mode))
        .AllowAnonymous().WithJsonResponse<SnapshotResponse>();
    public IResult Execute(string? mode)
    {
        // Controlled synthetic ticket input tests the public packaged reader, not an OIDC simulation.
        var claims = new List<Claim> { new(AuthenticationClaimTypes.ValidatedIssuer, "fixture-issuer"),
            new(AuthenticationClaimTypes.ValidatedSubject, settings.Name + "-owner") };
        if (mode == "duplicate") claims.Add(new(AuthenticationClaimTypes.ValidatedSubject, "duplicate-owner"));
        if (mode == "missing") claims.RemoveAll(claim => claim.Type == AuthenticationClaimTypes.ValidatedIssuer);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "controlled-qualification-ticket"));
        if (mode == "multiple") principal.AddIdentity(new ClaimsIdentity(claims, "second-ticket"));
        if (!reader.TryRead(principal, out var account))
            return FoundationProblemResults.Problem(new ProblemDefinition(400, "fixture_identity_invalid"));
        var model = projection.Project(account);
        using var canonical = new CanonicalUtf8Writer(512);
        canonical.Raw("{\"message\":"); canonical.String("😀 < > é"); canonical.Raw("}");
        using var deadline = deadlines.Create(TimeSpan.FromSeconds(1));
        var contracts = new[] { typeof(ValidatedAccountIdentity).Assembly, typeof(ProblemDefinition).Assembly,
            typeof(ValueSequence<string>).Assembly, typeof(IExecutionDeadline).Assembly, typeof(FixtureAccountSnapshot).Assembly }
            .Select(assembly => new ContractAssemblyObservation(assembly.GetName().Name!,
                assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "missing",
                !assembly.GetReferencedAssemblies().Any(reference => reference.Name!.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)
                    || reference.Name.StartsWith("Npgsql", StringComparison.Ordinal) || reference.Name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
                    || reference.Name == "System.Text.Json" || reference.Name.StartsWith("CShells", StringComparison.Ordinal)),
                !assembly.GetTypes().Any(type => typeof(IShellFeature).IsAssignableFrom(type))))
            .ToArray();
        return responses.Create(new(settings.Name, model.Issuer, model.Subject, model.Labels,
            model == new FixtureAccountSnapshot(model.Issuer, model.Subject, new ValueSequence<string>(model.Labels)),
            reader.GetType().Name, canonical.CompleteSha256(), deadline.Remaining.TotalMilliseconds, contracts));
    }
}
