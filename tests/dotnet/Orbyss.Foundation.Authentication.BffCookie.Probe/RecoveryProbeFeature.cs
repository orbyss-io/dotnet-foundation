using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using CShells;
using CShells.AspNetCore.Features;
using CShells.Features;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Orbyss.Foundation.Authentication;
using Orbyss.Foundation.Authentication.BffCookie;
using Orbyss.Foundation.Authentication.Core;
using Orbyss.Foundation.WebDefaults;
using Orbyss.Foundation.Web.ProblemDetails;
using Orbyss.Foundation.Web.ProblemDetails.Core;

[ShellFeature("RecoveryProbe", DependsOn = [typeof(FoundationBffCookieFeature)])]
public sealed class RecoveryProbeFeature(ShellSettings settings) : IWebShellFeature
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton(new RecoveryControl { Mode = settings.Id.ToString() });
        services.Replace(ServiceDescriptor.Singleton<IAuthenticationErrorWriter, RecoveryErrorWriter>());
        services.AddOptions<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme)
            .Configure<RecoveryControl>((options, control) =>
            {
                options.BackchannelHttpHandler = new RecoveryBackchannel(options, control);
                options.Events.OnRedirectToIdentityProvider = context => control.Mode switch
                {
                    "programming" => throw new InvalidOperationException("private-programming-marker"),
                    "program-http" => throw new HttpRequestException("private-event-marker"),
                    _ => Task.CompletedTask
                };
                options.Events.OnPushAuthorization = context => control.Mode == "par-program"
                    ? throw new HttpRequestException("private-PAR-event-marker") : Task.CompletedTask;
            });
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment)
    {
        endpoints.MapGet("/fixture/session", async (HttpContext context, IOptions<FoundationWebOptions> web) =>
        {
            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(AuthenticationClaimTypes.ValidatedIssuer, web.Value.Authority),
                new Claim(AuthenticationClaimTypes.ValidatedSubject, "existing-owner")], "fixture")));
            return Results.NoContent();
        }).AllowAnonymous();
        endpoints.MapGet("/fixture/provider", (HttpContext context, IOptions<FoundationWebOptions> web) =>
        {
            var query = context.Request.Query;
            var code = CallbackProbeFeature.IssueCode(web.Value.Authority, web.Value.ClientId, query["nonce"].ToString());
            var callback = WebUtility.HtmlEncode(query["redirect_uri"].ToString());
            var state = WebUtility.HtmlEncode(query["state"].ToString());
            return Results.Content($"<main><h1>Synthetic identity provider</h1><form action=\"{callback}\" method=\"get\"><label>Username<input name=\"username\" value=\"fixture-user\"></label><label>Password<input name=\"password\" type=\"password\" value=\"synthetic-only\"></label><input type=\"hidden\" name=\"code\" value=\"{code}\"><input type=\"hidden\" name=\"state\" value=\"{state}\"><button>Sign in</button></form></main>", "text/html");
        }).AllowAnonymous();
        endpoints.MapGet("/landing", (HttpContext context, IValidatedAccountIdentityReader identities) => Results.Content(
            identities.TryRead(context.User, out _) ? "Application sign-out is not yet confirmed."
                : "You're signed out of this application. Sign-out from your identity provider could not be confirmed.", "text/html")).AllowAnonymous();
        endpoints.MapGet("/api/private", () => Results.Text("admitted synthetic session")).RequireAuthorization();
    }
}

public sealed class RecoveryControl
{
    public string Mode { get; set; } = "";
    public string BrowserProvider { get; set; } = "https://identity.example/authorize";
    public int Writes;
    public int Requests;
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Finished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}

public sealed class RecoveryBackchannel(OpenIdConnectOptions options, RecoveryControl control) : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref control.Requests);
        control.Entered.TrySetResult();
        try
        {
            var metadata = request.RequestUri!.AbsolutePath.EndsWith("openid-configuration", StringComparison.Ordinal);
            if (metadata && control.Mode == "transport") throw new HttpRequestException("private-provider-marker");
            if (metadata && control.Mode is "timeout" or "abort") await Task.Delay(Timeout.Infinite, cancellationToken);
            if (metadata && control.Mode == "badjson") return Response(HttpStatusCode.OK, "not-metadata-json");
            if (metadata && control.Mode is "status" or "notfound") return Response(control.Mode == "status" ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.NotFound, "private-provider-body");
            if (metadata) return Response(HttpStatusCode.OK, JsonSerializer.Serialize(new
            {
                issuer = options.Authority,
                authorization_endpoint = control.BrowserProvider,
                token_endpoint = "https://identity.example/token",
                userinfo_endpoint = "https://identity.example/userinfo",
                jwks_uri = "https://identity.example/keys",
                end_session_endpoint = control.Mode == "missinglogout" ? null : "https://identity.example/end-session",
                pushed_authorization_request_endpoint = control.Mode.StartsWith("par", StringComparison.Ordinal) ? "https://identity.example/par" : null
            }));
            if (request.RequestUri.AbsolutePath == "/keys")
                return Response(control.Mode == "jwks" ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK,
                    control.Mode == "jwks" ? "private-jwks-body" : JsonSerializer.Serialize(CallbackProbeFeature.JwksDocument()));
            if (request.RequestUri.AbsolutePath == "/par")
                return Response(control.Mode == "par" ? HttpStatusCode.ServiceUnavailable : control.Mode == "par401" ? HttpStatusCode.Unauthorized : HttpStatusCode.OK,
                    control.Mode == "parbadjson" ? "not-par-json" : "{\"error\":\"invalid_client\",\"error_description\":\"private-PAR-body\"}");
            if (request.RequestUri.AbsolutePath == "/token")
            {
                var form = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(await request.Content!.ReadAsStringAsync(cancellationToken));
                var token = CallbackProbeFeature.RedeemCode(form["code"].ToString()) ?? throw new InvalidOperationException("Unissued fixture code");
                return Response(HttpStatusCode.OK, JsonSerializer.Serialize(new { id_token = token, access_token = "fixture-access-token", token_type = "Bearer", expires_in = 300 }));
            }
            if (request.RequestUri.AbsolutePath == "/userinfo") return Response(HttpStatusCode.OK, JsonSerializer.Serialize(new { sub = options.ClientId, name = "fixture" }));
            throw new InvalidOperationException("Unexpected fixture backchannel route");
        }
        finally { control.Finished.TrySetResult(); }
    }

    private static HttpResponseMessage Response(HttpStatusCode status, string content) => new(status) { Content = new StringContent(content, Encoding.UTF8, "application/json") };
}

public sealed class RecoveryErrorWriter(RecoveryControl control, IValidatedAccountIdentityReader identities) : IAuthenticationErrorWriter
{
    public async Task WriteAsync(HttpContext context, int statusCode, string code)
    {
        Interlocked.Increment(ref control.Writes);
        if (context.Request.Headers.Accept.Any(value => value?.Contains("text/html", StringComparison.Ordinal) == true))
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "text/html; charset=utf-8";
            var continuation = identities.TryRead(context.User, out _) ? "Continue to the application. " : "";
            var pathBase = context.Features.Get<IAuthenticationFeature>()?.OriginalPathBase ?? context.Request.PathBase;
            var restart = pathBase + "/bff/login?returnUrl=" + Uri.EscapeDataString(pathBase + "/api/private");
            await context.Response.WriteAsync("<main>Sign-in could not be completed. " + continuation + "<a href=\"" + restart + "\">Try signing in again</a></main>", context.RequestAborted);
            return;
        }
        await FoundationProblemResults.Problem(new ProblemDefinition(statusCode, code)).ExecuteAsync(context);
    }
}
